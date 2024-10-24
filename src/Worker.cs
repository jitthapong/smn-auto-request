using MailKit.Net.Smtp;
using MimeKit;
using MySql.Data.MySqlClient;
using NLog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VtecInventory.Services;
using vtecPOS.GlobalFunctions;

namespace SMN_INV_AUTO_SYNC
{
    public class Worker
    {
        private static ILogger _logger = LogManager.GetCurrentClassLogger();

        private static Worker _instance;
        private static object _lock = new object();

        public static Worker Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Worker();
                }
                return _instance;
            }
        }

        private Timer _timer;

        private string _mySqlConnStr;

        private string _merchantKey;
        private int _shopId;
        private int _langId = 1;
        private string _apiCoreBaseUrl;
        private string _smtpServer;
        private int _smtpPort;
        private string _smtpUsername;
        private string _smtpPassword;
        private bool _smtpUseSsl;
        private string _emailFrom;

        private IRequestProvider _requestProvider;
        private IAuthService _authService;
        private IApiCoreService _apiCoreService;
        private IExchangeDataService _exchangeDataService;

        private POSModule _posModule;
        private InventModule _invModule;

        private string _saleDate = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private Worker()
        {
            _invModule = new InventModule();
            _posModule = new POSModule();
        }

        public async Task InitializeAsync(string dbServer, string dbName)
        {
            _logger.Info("Initialize");

            _mySqlConnStr = new MySqlConnectionStringBuilder
            {
                UserID = "vtecPOS",
                Password = "vtecpwnet",
                Server = dbServer,
                Database = dbName,
                Port = 3308,
                AllowUserVariables = true,
                DefaultCommandTimeout = 60,
                SslMode = MySqlSslMode.None,
                OldGuids = true
            }.ConnectionString;

            _merchantKey = (string)MySqlHelper.ExecuteScalar(_mySqlConnStr, "select MerchantKey from merchant_data");
            _shopId = (int)MySqlHelper.ExecuteScalar(_mySqlConnStr, "select ShopID from shop_data");

            var dtProperty = GetProperties(new int[] { 1202, 3021 });

            _apiCoreBaseUrl = dtProperty.AsEnumerable().Where(p => p["PropertyID"].ToString() == "1202")
                .SelectMany(p => p["PropertyTextValue"].ToString().Split(';'))
                .Where(x => x.Split('=')[0] == "ApiBaseServerUrl")
                .Select(x => x.Split('=')[1]).FirstOrDefault();

            var smtpProperty = dtProperty.AsEnumerable().Where(p => p["PropertyID"].ToString() == "3021" && p["PropertyValue"].ToString() == "1");
            if (smtpProperty?.Any() == true)
            {
                _smtpServer = smtpProperty.SelectMany(p => p["PropertyTextValue"].ToString().Split(';'))
                    .Where(x => x.Split('=')[0] == "MailServer")
                    .Select(x => x.Split('=')[1]).FirstOrDefault();
                _smtpPort = smtpProperty.SelectMany(p => p["PropertyTextValue"].ToString().Split(';'))
                    .Where(x => x.Split('=')[0] == "Port")
                    .Select(x => int.Parse(x.Split('=')[1])).FirstOrDefault();
                _smtpUsername = smtpProperty.SelectMany(p => p["PropertyTextValue"].ToString().Split(';'))
                    .Where(x => x.Split('=')[0] == "UserName")
                    .Select(x => x.Split('=')[1]).FirstOrDefault();
                _smtpPassword = smtpProperty.SelectMany(p => p["PropertyTextValue"].ToString().Split(';'))
                    .Where(x => x.Split('=')[0] == "Password")
                    .Select(x => x.Split('=')[1]).FirstOrDefault();
                _smtpUseSsl = smtpProperty.SelectMany(p => p["PropertyTextValue"].ToString().Split(';'))
                    .Where(x => x.Split('=')[0] == "EnableSSL")
                    .Select(x => bool.Parse(x.Split('=')[1])).FirstOrDefault();
            }

            _authService = new AuthService();
            _authService.InitBaseUrl(_apiCoreBaseUrl);

            _requestProvider = new RequestProvider(_authService);

            _apiCoreService = new ApiCoreService(_requestProvider);
            _apiCoreService.InitBaseUrl(_apiCoreBaseUrl);


            _exchangeDataService = new ExchangeDataService(_apiCoreService, _authService, _posModule, _invModule, _mySqlConnStr);

            await _apiCoreService.GetMerchantInfoAsync(_merchantKey);

            _logger.Info("Initialized");

            if (_timer != null)
                _timer.Dispose();

            _timer = new Timer(OnProcess, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

            _logger.Info("Start job create auto request document");
        }

        private void OnProcess(object state)
        {
            lock (_lock)
            {
                using (var conn = new MySqlConnection(_mySqlConnStr))
                {
                    conn.Open();
                    var respText = "";
                    var docKey = "";
                    
                    if (_invModule.Document_Auto_Request(ref respText, ref docKey, _shopId, _saleDate, conn))
                    {
                        if (!string.IsNullOrEmpty(docKey))
                        {
                            var html = Document_Detail_Html(docKey, _langId, false, "", conn);
                            if (!string.IsNullOrEmpty(html))
                            {
                                SendEmail(html);

                                try
                                {
                                    _exchangeDataService.SendInvenDataAsync(_shopId).ConfigureAwait(false);
                                }
                                catch (Exception ex)
                                {
                                    _logger.Error(ex, "Error about exchange inventory data");
                                }
                            }
                        }
                    }
                    else
                    {
                        _logger.Error("Document_Auto_Request => {0}", respText);
                    }
                }
            }
        }
        //TODO: How to send to ?
        public void SendEmail(string html)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("jitthapong", "jitthapong@gmail.com"));
            message.To.Add(new MailboxAddress("jitthapong vtec", "jitthapong@vtec-system.com"));
            message.Subject = "Test";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = html
            };
            message.Body = bodyBuilder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                client.Connect(_smtpServer, _smtpPort, _smtpUseSsl);
                
                //client.Authenticate(_smtpUsername, _smtpPassword);
                client.Authenticate("jitthapong@gmail.com", "nyxs ttwt ychi vvcj");

                client.Send(message);
                client.Disconnect(true);
            }
        }

        public string Document_Detail_Html(string DocumentKey, int LangID, bool isPrint, string URLlink, MySqlConnection conn)
        {
            var documentHtml = "";
            var dtTable = new DataTable();
            var VendorData = new DataTable();
            string sqlStatement = "";
            StringBuilder outputString = new StringBuilder();
            string htmlStr = "";

            sqlStatement = $"select * from document where DocumentKey=@docKey";

            DataTable ChkDoc = new DataTable();
            using (var reader = MySqlHelper.ExecuteReader(conn, sqlStatement, new MySqlParameter("@docKey", DocumentKey)))
            {
                ChkDoc.Load(reader);
            }

            if (ChkDoc.Rows.Count > 0)
            {

                sqlStatement = $"select a.*,DATE_FORMAT(a.DocumentDate,'%d %M %Y') As DocDateString,DATE_FORMAT(a.DueDate,'%d %M %Y') As DueDateString,DATE_FORMAT(a.InsertDate,'%d %M %Y %T') As InsertDateString,DATE_FORMAT(a.UpdateDate,'%d %M %Y %T') As UpdateDateString,DATE_FORMAT(a.CancelDate,'%d %M %Y %T') As CancelDateString,DATE_FORMAT(a.ApproveDate,'%d %M %Y %T') As ApproveDateString,v.*,CONCAT(s1.StaffCode, ' ', s1.StaffFirstName) AS InputStaff,CONCAT(s2.StaffCode, ' ', s2.StaffFirstName) AS UpdateStaff,CONCAT(s3.StaffCode, ' ', s3.StaffFirstName) AS ApproveStaff,CONCAT(s4.StaffCode, ' ', s4.StaffFirstName) AS VoidStaff,CONCAT(s5.StaffCode, ' ', s5.StaffFirstName) AS ReceiveStaff,dt.DocumentTypeHeader,dt.DocumentTypeName,ds.Description AS DocumentStatusText,tp.Description AS TermOfPaymentText,p.ShopName AS ToInvName,pv.ProvinceName,dc.DocumentTypeID AS DocumentTypeIDRef, p1.ShopName AS FromInvName,dc.DocumentNoRef,dc.DocumentKey As DocumentKeyRef from document a left outer join Vendors v ON a.VendorID=v.VendorID left outer join Staffs s1 ON a.InputBy=s1.StaffID left outer join Staffs s2 ON a.UpdateBy=s2.StaffID left outer join Staffs s3 ON a.ApproveBy=s3.StaffID left outer join Staffs s4 ON a.VoidBy=s4.StaffID left outer join Staffs s5 ON a.ReceiveBy=s5.StaffID left outer join DocumentType dt ON a.DocumentTypeID=dt.DocumentTypeID left outer join DocumentStatus ds ON a.DocumentStatus=ds.DocumentStatusID left outer join TermOfPayment tp ON a.TermOfPayment=tp.TermOfPaymentID AND tp.LangID=1 left outer join shop_data p ON a.ShopID=p.ShopID left outer join shop_data p1 ON a.FromInvID=p1.ShopID left outer join Provinces pv ON v.VendorProvince=pv.ProvinceID AND pv.LangID=2 left outer join document dc ON a.DocumentIDRef=dc.DocumentID AND a.DocIDRefShopID=dc.ShopID where 0=0 AND a.DocumentKey=@docKey";
                VendorData = new DataTable();
                using (var reader = MySqlHelper.ExecuteReader(conn, sqlStatement, new MySqlParameter("@docKey", DocumentKey)))
                {
                    VendorData.Load(reader);
                }

                sqlStatement = "select m.MaterialID,m.MaterialCode,m.MaterialName,a.*,b.* from document a inner join  docdetail b ON a.documentid=b.documentid and a.KeyShopID=b.KeyShopID left outer join materials m ON b.ProductID=m.MaterialID where a.DocumentKey=@docKey Order By a.DocumentID,b.DocDetailID";
                dtTable = new DataTable();
                using (var reader = MySqlHelper.ExecuteReader(conn, sqlStatement, new MySqlParameter("@docKey", DocumentKey)))
                {
                    dtTable.Load(reader);
                }

                int VATType = Convert.ToInt32(ChkDoc.Rows[0]["DocumentVATType"]);
                int i = 0;
                string StatusText = "";
                string DocTypeText = "";
                string DocNoText = "";
                string DocRefText = "";
                string DocDateText = "";
                string InventoryText = "";
                string RemarkText = "";
                string FromInventoryText = "";
                string FromInvText = "";
                string DocInfo = "";
                string doc_info = "";
                string VendorCodeText = "";
                string VendorNameText = "";
                string VendorAddress = "";
                string VendorAddressText = "";
                string VendorTelFax = "";
                string VendorTelFaxText = "";
                string TermOfPaymentText = "";
                string DueDateText = "";

                string SelectedText = "text";
                string summaryColor = "#f0f0f5";

                string SummaryLabel = "Summary";
                string StatusLabel = "Status";
                string DocTypeLabel = "Document Type";
                string DocNoLabel = "Document Number";
                string DocRefLabel = "Document Ref";
                string DocDateLabel = "Document Date";
                string InventoryLabel = "Inventory";
                string CreateLabel = "Created By";
                string UpdateLabel = "Updated By";
                string ApproveLabel = "Approved By";
                string CancelLabel = "Cancelled By";
                string VendorCodeLabel = "Vendor Code";
                string VendorNameLabel = "Vendor Name";
                string VendorAddressLabel = "Vendor Address";
                string VendorTelLabel = "Tel/Fax";
                string TermLabel = "Term Of Payment";
                string DueLabel = "Due Date";
                string RemarkLabel = "Remark";
                string PrintLabel = "Print this Page";
                string CloseLabel = "Close Windows";
                if (LangID == 2)
                {
                    SummaryLabel = "สรุป";
                    StatusLabel = "สถานะเอกสาร";
                    DocTypeLabel = "ประเภทเอกสาร";
                    DocNoLabel = "เอกสารเลขที่";
                    DocRefLabel = "เลขที่อ้างอิง";
                    DocDateLabel = "วันที่เอกสาร";
                    InventoryLabel = "ชื่อคลังสินค้า";
                    CreateLabel = "สร้างโดย";
                    UpdateLabel = "ปรับปรุงโดย";
                    ApproveLabel = "อนุมัติโดย";
                    CancelLabel = "ยกเลิกโดย";
                    VendorCodeLabel = "รหัสผุ้จัดจำหน่าย";
                    VendorNameLabel = "ชื่อผู้จัดจำหน่าย";
                    VendorAddressLabel = "ที่อยู่ผู้จัดจำหน่าย";
                    VendorTelLabel = "เบอร์ติดต่อ";
                    TermLabel = "เงื่อนไขการชำระ";
                    DueLabel = "วันที่ครบเงื่อนไข";
                    RemarkLabel = "หมายเหตุ";
                    PrintLabel = "พิมพ์เอกสาร";
                    CloseLabel = "ปิดหน้าต่าง";
                }

                if (VendorData.Rows.Count > 0)
                {
                    if (VendorData.Rows[0]["DocumentNoRef"] != DBNull.Value && VendorData.Rows[0]["DocumentKeyRef"] != DBNull.Value)
                        DocRefText = "<a href=\"JavaScript: newWindow = window.open( 'document_detail.aspx?DocumentKey=" + VendorData.Rows[0]["DocumentKeyRef"].ToString() + "', '', 'width=800,height=700,toolbar=0,location=0,directories=0,status=0,menuBar=0,scrollBars=1,resizable=1' ); newWindow.focus()\">" + VendorData.Rows[0]["DocumentNoRef"].ToString() + "</a>";
                    else
                        DocRefText = "-";
                    StatusText = VendorData.Rows[0]["DocumentStatusText"].ToString(); // + "::" + VendorData.Rows[0]["DocumentTypeID").ToString
                    DocTypeText = VendorData.Rows[0]["DocumentTypeName"].ToString();
                    DocNoText = VendorData.Rows[0]["DocumentNo"].ToString();
                    DocDateText = VendorData.Rows[0]["DocDateString"].ToString();
                    if (VendorData.Rows[0]["ToInvName"] != DBNull.Value)
                        InventoryText = VendorData.Rows[0]["ToInvName"].ToString();
                    else
                        InventoryText = "-";


                    if (Convert.ToInt32(VendorData.Rows[0]["DocumentTypeID"]) == 25 || Convert.ToInt32(VendorData.Rows[0]["DocumentTypeID"]) == 1001)
                    {
                        if (VendorData.Rows[0]["FromInvName"] != DBNull.Value)
                        {
                            FromInventoryText = VendorData.Rows[0]["FromInvName"].ToString();
                            FromInvText = $@"<tr>
			                                <td class=""text"" bgColor=""#f0f0f5"" align=""left"" width=""120px"">From Inventory:</td>
			                                <td class=""text"">{FromInventoryText}</td>
		                                </tr>";
                        }
                    }
                    if (VendorData.Rows[0]["Remark"] != DBNull.Value)
                    {
                        if (VendorData.Rows[0]["Remark"].ToString().Trim() == "")
                            RemarkText = "-";
                        else
                            RemarkText = VendorData.Rows[0]["Remark"].ToString();
                    }
                    else
                        RemarkText = "-";
                    if (VendorData.Rows[0]["VendorCode"] != DBNull.Value)
                        VendorCodeText = VendorData.Rows[0]["VendorCode"].ToString();
                    else
                        VendorCodeText = "";
                    if (VendorData.Rows[0]["VendorName"] != DBNull.Value)
                        VendorNameText = VendorData.Rows[0]["VendorName"].ToString();
                    else
                        VendorNameText = "";
                    if (VendorData.Rows[0]["VendorAddress1"] != DBNull.Value)
                        VendorAddress += VendorData.Rows[0]["VendorAddress1"].ToString();
                    if (VendorData.Rows[0]["VendorAddress2"] != DBNull.Value)
                        VendorAddress += " " + VendorData.Rows[0]["VendorAddress2"].ToString();
                    if (VendorData.Rows[0]["VendorCity"] != DBNull.Value)
                    {
                        if (VendorAddress.Trim() != "")
                            VendorAddress += "<br>" + VendorData.Rows[0]["VendorCity"].ToString();
                        else
                            VendorAddress += VendorData.Rows[0]["VendorCity"].ToString();
                    }
                    if (VendorData.Rows[0]["ProvinceName"] != DBNull.Value)
                        VendorAddress += " " + VendorData.Rows[0]["ProvinceName"].ToString();
                    if (VendorData.Rows[0]["VendorZipCode"] != DBNull.Value)
                        VendorAddress += " " + VendorData.Rows[0]["VendorZipCode"].ToString();
                    VendorAddressText = VendorAddress;

                    if (VendorData.Rows[0]["VendorTelephone"] != DBNull.Value)
                        VendorTelFax += "Tel:" + VendorData.Rows[0]["VendorTelephone"].ToString();
                    else
                        VendorTelFax += "Tel:-";
                    if (VendorData.Rows[0]["VendorFax"] != DBNull.Value)
                        VendorTelFax += "/Fax:" + VendorData.Rows[0]["VendorFax"].ToString();
                    else
                        VendorTelFax += "/Fax:-";
                    VendorTelFaxText = VendorTelFax;

                    if (Convert.ToInt32(VendorData.Rows[0]["TermOfPayment"]) == 2)
                        TermOfPaymentText = VendorData.Rows[0]["TermOfPaymentText"].ToString() + " " + VendorData.Rows[0]["CreditDay"].ToString() + " day(s)";
                    else if (Convert.ToInt32(VendorData.Rows[0]["TermOfPayment"]) > 0)
                        TermOfPaymentText = VendorData.Rows[0]["TermOfPaymentText"].ToString();
                    else
                        TermOfPaymentText = "-";
                    if (VendorData.Rows[0]["DueDateString"] != DBNull.Value)
                        DueDateText += VendorData.Rows[0]["DueDateString"].ToString();
                    else
                        DueDateText += "-";
                    DocInfo = "";
                    if (VendorData.Rows[0]["InputStaff"] != DBNull.Value)
                    {
                        if (VendorData.Rows[0]["InputStaff"].ToString().Trim() != "")
                            DocInfo += $"<tr><td class=\"text\" bgColor=\"#f0f0f5\" align=\"left\" width=\"120px\">{CreateLabel}</td><td class=\"text\">{VendorData.Rows[0]["InputStaff"].ToString()}: {VendorData.Rows[0]["InsertDateString"].ToString()}</td></tr>";
                    }
                    if (VendorData.Rows[0]["UpdateStaff"] != DBNull.Value)
                    {
                        if (VendorData.Rows[0]["UpdateStaff"].ToString().Trim() != "")
                            DocInfo += $"<tr><td class=\"text\" bgColor=\"#f0f0f5\" align=\"left\" width=\"120px\">{UpdateLabel}</td><td class=\"text\">{VendorData.Rows[0]["UpdateStaff"].ToString()}: {VendorData.Rows[0]["UpdateDateString"].ToString()}</td></tr>";
                    }

                    if (VendorData.Rows[0]["ApproveStaff"] != DBNull.Value)
                    {
                        if (VendorData.Rows[0]["ApproveStaff"].ToString().Trim() != "")
                            DocInfo += $"<tr><td class=\"text\" bgColor=\"#f0f0f5\" align=\"left\" width=\"120px\">{ApproveLabel}</td><td class=\"text\">{VendorData.Rows[0]["ApproveStaff"].ToString()}: {VendorData.Rows[0]["ApproveDateString"].ToString()}</td></tr>";
                    }

                    if (VendorData.Rows[0]["VoidStaff"] != DBNull.Value)
                    {
                        if (VendorData.Rows[0]["VoidStaff"].ToString().Trim() != "")
                            DocInfo += $"<tr><td class=\"text\" bgColor=\"#f0f0f5\" align=\"left\" width=\"120px\">{CancelLabel}</td><td class=\"text\">{VendorData.Rows[0]["VoidStaff"].ToString()}: {VendorData.Rows[0]["CancelDateString"].ToString()}</td></tr>";
                    }

                    doc_info = DocInfo;
                }

                if (dtTable.Rows.Count > 0)
                {
                    for (i = 0; i <= dtTable.Rows.Count - 1; i++)
                    {
                        outputString = outputString.Append("<tr><td align=\"center\" class=\"" + SelectedText + "\">" + (i + 1).ToString() + "</td>");
                        outputString = outputString.Append("<td align=\"left\" class=\"" + SelectedText + "\">" + dtTable.Rows[i]["ProductCode"].ToString() + "</td>");
                        outputString = outputString.Append("<td align=\"left\" class=\"" + SelectedText + "\">" + dtTable.Rows[i]["ProductName"].ToString() + "</td>");
                        outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + Convert.ToDecimal(dtTable.Rows[i]["ProductAmount"]).ToString("##,##0.00") + "</td>");
                        outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + Convert.ToDecimal(dtTable.Rows[i]["ProductPricePerUnit"]).ToString("##,##0.00") + "</td>");
                        outputString = outputString.Append("<td align=\"left\" class=\"" + SelectedText + "\">" + dtTable.Rows[i]["UnitName"].ToString() + "</td>");
                        outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + (Convert.ToDecimal(dtTable.Rows[i]["ProductAmount"]) * Convert.ToDecimal(dtTable.Rows[i]["ProductPricePerUnit"])).ToString("##,##0.00") + "</td>");
                        outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + Convert.ToDecimal(dtTable.Rows[i]["ProductDiscountAmount"]).ToString("##,##0.00") + "</td>");

                        if (Convert.ToInt32(dtTable.Rows[i]["VATType"].ToString()) == 1)
                        {
                            outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + Convert.ToDecimal(dtTable.Rows[i]["ProductTotalPrice"]).ToString("##,##0.00") + "</td>");
                            outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + Convert.ToDecimal(dtTable.Rows[i]["ProductTax"]).ToString("##,##0.00") + "</td>");
                            outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + Convert.ToDecimal(dtTable.Rows[i]["ProductNetPrice"]).ToString("##,##0.00") + "</td>");
                        }
                        else
                        {
                            outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + Convert.ToDecimal(dtTable.Rows[i]["ProductNetPrice"]).ToString("##,##0.00") + "</td>");
                            outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + Convert.ToDecimal(dtTable.Rows[i]["ProductTax"]).ToString("##,##0.00") + "</td>");
                            outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + Convert.ToDecimal(dtTable.Rows[i]["ProductTotalPrice"]).ToString("##,##0.00") + "</td>");
                        }

                        outputString = outputString.Append("<td align=\"center\" class=\"" + SelectedText + "\">" + dtTable.Rows[i]["VATCode"].ToString() + "</td>");
                        outputString = outputString.Append("</tr>");
                    }
                    outputString = outputString.Append("<tr bgColor=\"" + summaryColor + "\">");
                    outputString = outputString.Append("<td align=\"right\" colspan=\"6\">" + SummaryLabel + "</td>");
                    outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + Convert.ToDecimal(ChkDoc.Rows[0]["SubTotal"]).ToString("##,##0.00") + "</td>");
                    outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + Convert.ToDecimal(ChkDoc.Rows[0]["TotalDiscount"]).ToString("##,##0.00") + "</td>");

                    if (VATType == 1)
                    {
                        outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + Convert.ToDecimal(ChkDoc.Rows[0]["DocumentIncludeVAT"]).ToString("##,##0.00") + "</td>");
                        outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + Convert.ToDecimal(ChkDoc.Rows[0]["DocumentVAT"]).ToString("##,##0.00") + "</td>");
                        outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + Convert.ToDecimal(ChkDoc.Rows[0]["DocumentBeforeVAT"]).ToString("##,##0.00") + "</td>");
                    }
                    else
                    {
                        outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + Convert.ToDecimal(ChkDoc.Rows[0]["DocumentBeforeVAT"]).ToString("##,##0.00") + "</td>");
                        outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + Convert.ToDecimal(ChkDoc.Rows[0]["DocumentVAT"]).ToString("##,##0.00") + "</td>");
                        outputString = outputString.Append("<td align=\"right\" class=\"" + SelectedText + "\">" + Convert.ToDecimal(ChkDoc.Rows[0]["DocumentIncludeVAT"]).ToString("##,##0.00") + "</td>");
                    }

                    outputString = outputString.Append("<td align=\"center\" class=\"" + SelectedText + "\">" + "" + "</td>");
                    outputString = outputString.Append("</tr>");

                }

                string Text1 = "";
                string Text2 = "Material Code";
                string Text3 = "Material Name";
                string Text4 = "Qty";
                string Text5 = "Price/Unit";
                string Text6 = "Sub Total";
                string Text66 = "Unit";
                string Text7 = "Discount";
                string Text8 = "";
                string Text9 = "";
                string Text10 = "";
                string Text11 = "";
                if (VATType == 1)
                {
                    if (LangID == 2)
                    {
                        Text8 = "ยอดหลังลด";
                        Text9 = "ภาษี";
                        Text10 = "ยอดก่อนภาษี";
                    }
                    else
                    {
                        Text8 = "After Disc";
                        Text9 = "VAT";
                        Text10 = "Before VAT";
                    }

                    Text11 = "";
                }
                else
                {
                    if (LangID == 2)
                    {
                        Text8 = "ยอดหลังลด";
                        Text9 = "ภาษี";
                        Text10 = "ยอดรวมภาษี";
                    }
                    else
                    {
                        Text8 = "After Disc";
                        Text9 = "VAT";
                        Text10 = "Include VAT";
                    }

                    Text11 = "";
                }
                if (LangID == 2)
                {
                    Text2 = "รหัสวัตถุดิบ";
                    Text3 = "ชื่อวัตถุดิบ";
                    Text4 = "จำนวน";
                    Text5 = "ราคาต่อหน่วย";
                    Text6 = "ยอดรวม";
                    Text66 = "หน่วย";
                    Text7 = "ส่วนลด";
                }
                var printStr = $@"<tr>
                                <td align=""left""><div id=""HeaderText"" class=""headerText"" align=""left""></div></td>
                                <td align=""right""><div id=""GoBackText"" class=""text""><a href=""javascript: window.print()"">{PrintLabel}</a> | <a href=""javascript: window.close()"">{CloseLabel}</a></div></td>
                            </tr>";
                if (isPrint == false)
                {
                    printStr = $@"<tr>
                                <td align=""left"" colspan=""2"">{URLlink}</td>
                            </tr>";
                }
                htmlStr = $@"<html>
                    <head>
                        <title>Document Detail</title>
                        <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"">
                        <style type=""text/css"">
                            body, td, th {{
                                font-family: Tahoma, Verdana, Arial;
                                font-weight: normal;
                                font-size: 12px;
                            }}

                            .htext {{
                                font-family: Tahoma, Verdana, Arial;
                                font-weight: bold;
                                font-size: 20px;
                            }}

                            .h1text {{
                                font-family: Tahoma, Verdana, Arial;
                                font-weight: bold;
                                font-size: 16px;
                            }}

                            .errorText {{
                                font-family: Tahoma, Verdana, Arial;
                                font-weight: bold;
                                color: red;
                                font-size: 15px;
                            }}

                            .tdHeader {{
                                font-size: 11px;
                                line-height: 15px;
                                font-family: sans-serif;
                                font-family: Tahoma, Verdana, Arial;
                                font-weight: normal;
                                color: black;
                            }}

                            .tdText {{
                                font-size: 11px;
                                line-height: 15px;
                                font-family: sans-serif;
                                font-family: Tahoma, Verdana, Arial;
                                font-weight: normal;
                                color: black;
                            }}

                            .tdBoldText {{
                                font-size: 11px;
                                line-height: 15px;
                                font-family: sans-serif;
                                font-family: Tahoma, Verdana, Arial;
                                font-weight: bold;
                                color: black;
                            }}

                            @media print {{
                                .noprint {{
                                    display: none;
                                }}
                            }}
                        </style>

                    </head>
                    </head>
                    <body style=""background-color:white"">
                        <div class=""noprint"">
                        <table cellpadding=""2"" cellspacing=""2"" width=""100%"">
                            {printStr}
                        </table>
                        </div>
                        <table id=""myTable"" border=""1"" cellpadding=""4"" cellspacing=""0"" style=""border-collapse:collapse;"" width=""100%"">
                            <tr>
                                <td width=""50%"" valign=""top"">
	                                <table border=""1"" cellpadding=""4"" cellspacing=""0"" style=""border-collapse:collapse;"" width=""100%"">
		                                <tr>
			                                <td class=""text"" bgColor=""#f0f0f5"" align=""left"" width=""120px"">{StatusLabel}</td>
			                                <td class=""text"">{StatusText}</td>
		                                </tr>
		                                <tr>
			                                <td class=""text"" bgColor=""#f0f0f5"" align=""left"" width=""120px"">{DocTypeLabel}</td>
			                                <td class=""text"">{DocTypeText}</td>
		                                </tr>
		                                <tr>
			                                <td class=""text"" bgColor=""#f0f0f5"" align=""left"" width=""120px"">{DocNoLabel}</td>
			                                <td class=""text"">{DocNoText}</td>
		                                </tr>
		                                <tr>
			                                <td class=""text"" bgColor=""#f0f0f5"" align=""left"" width=""120px"">{DocRefLabel}</td>
			                                <td class=""text"">{DocRefText}</td>
		                                </tr>
		                                <tr>
			                                <td class=""text"" bgColor=""#f0f0f5"" align=""left"" width=""120px"">{DocDateLabel}</td>
			                                <td class=""text"">{DocDateText}</td>
		                                </tr>
                                        {FromInvText}
		                                <tr>
			                                <td class=""text"" bgColor=""#f0f0f5"" align=""left"" width=""120px"">{InventoryLabel}</td>
			                                <td class=""text"">{InventoryText}</td>
		                                </tr>		                                
                                        {doc_info}
	                                </table>
                                </td>
                                <td width=""50%"" valign=""top"">
	                                <table border=""1"" cellpadding=""4"" cellspacing=""0"" style=""border-collapse:collapse;"" width=""100%"">
		                                <tr>
			                                <td class=""text"" bgColor=""#f0f0f5"" align=""left"" width=""120px"">{VendorCodeLabel}</td>
			                                <td class=""text"">{VendorCodeText}</td>
		                                </tr>
		                                <tr>
			                                <td class=""text"" bgColor=""#f0f0f5"" align=""left"" width=""120px"">{VendorNameLabel}</td>
			                                <td class=""text"">{VendorNameText}</td>
		                                </tr>
		                                <tr>
			                                <td class=""text"" bgColor=""#f0f0f5"" align=""left"" width=""120px"" valign=""top"">{VendorAddressLabel}</td>
			                                <td class=""text"">{VendorAddressText}</td>
		                                </tr>
		                                <tr>
			                                <td class=""text"" bgColor=""#f0f0f5"" align=""left"" width=""120px"">{VendorTelLabel}</td>
			                                <td class=""text"">{VendorTelFaxText}</td>
		                                </tr>
		                                <tr>
			                                <td class=""text"" bgColor=""#f0f0f5"" align=""left"" width=""120px"">{TermLabel}</td>
			                                <td class=""text"">{TermOfPaymentText}</td>
		                                </tr>
		                                <tr>
			                                <td class=""text"" bgColor=""#f0f0f5"" align=""left"" width=""120px"">{DueLabel}</td>
			                                <td class=""text"">{DueDateText}</td>
		                                </tr>     
                                        <tr>
			                                <td class=""text"" bgColor=""#f0f0f5"" align=""left"" width=""120px"">{RemarkLabel}</td>
			                                <td class=""text"">{RemarkText}</td>
		                                </tr>
	                                </table>
                                </td>
                            </tr>
                        </table>
                        <table id=""myTable"" border=""1"" cellpadding=""4"" cellspacing=""0"" style=""border-collapse:collapse;"" width=""100%"">
                            <tr>
                                <td id=""headerTD1"" align=""center"" class=""tdHeader"" bgcolor=""#f0f0f5"">{Text1}</td>
                                <td id=""headerTD2"" align=""center"" class=""tdHeader"" bgcolor=""#f0f0f5"">{Text2}</td>
                                <td id=""headerTD3"" align=""center"" class=""tdHeader"" bgcolor=""#f0f0f5"">{Text3}</td>
                                <td id=""headerTD4"" align=""center"" class=""tdHeader"" bgcolor=""#f0f0f5"">{Text4}</td>
                                <td id=""headerTD5"" align=""center"" class=""tdHeader"" bgcolor=""#f0f0f5"">{Text5}</td>
                                <td id=""headerTD66"" align=""center"" class=""tdHeader"" bgcolor=""#f0f0f5"">{Text66}</td>
                                <td id=""headerTD6"" align=""center"" class=""tdHeader"" bgcolor=""#f0f0f5"">{Text6}</td>
                                <td id=""headerTD7"" align=""center"" class=""tdHeader"" bgcolor=""#f0f0f5"">{Text7}</td>
                                <td id=""headerTD8"" align=""center"" class=""tdHeader"" bgcolor=""#f0f0f5"">{Text8}</td>
                                <td id=""headerTD9"" align=""center"" class=""tdHeader"" bgcolor=""#f0f0f5"">{Text9}</td>
                                <td id=""headerTD10"" align=""center"" class=""tdHeader"" bgcolor=""#f0f0f5"">{Text10}</td>
                                <td id=""headerTD11"" align=""center"" class=""tdHeader"" bgcolor=""#f0f0f5"">{Text11}</td>
                            </tr>
                            {outputString.ToString()}
                        </table>
                    </body>
                    </html>";

                documentHtml = htmlStr;
            }
            else
            {
                var errMsg = "";
                if (LangID == 2)
                {
                    errMsg = "ไม่พบเอกสารในระบบ";
                }
                else
                {
                    errMsg = "No document found.";
                }
                throw new Exception(errMsg);
            }
            return documentHtml;
        }

        public DataTable GetProperties(int[] propIds)
        {
            var dtProperty = new DataTable();
            var propIdsParam = string.Join(",", propIds);
            var cmd = @"SELECT a.PropertyID, a.KeyID, a.PropertyValue, a.PropertyTextValue 
                        FROM programpropertyvalue a
                        JOIN programproperty b
                        ON a.PropertyID=b.PropertyID
                        where a.PropertyID in (" + propIdsParam + ")";
            using (var reader = MySqlHelper.ExecuteReader(_mySqlConnStr, cmd))
            {
                dtProperty.Load(reader);
            }
            return dtProperty;
        }
    }
}
