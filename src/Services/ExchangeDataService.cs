using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using vtecPOS.GlobalFunctions;

namespace VtecInventory.Services
{
    public class ExchangeDataService : IExchangeDataService
    {
        private readonly IAuthService _authService;
        private readonly IApiCoreService _apiCoreService;
        private readonly POSModule _posModule;
        private readonly InventModule _invModule;
        private string _mySQLConnectionString;

        private int _retrySendCounter;

        public ExchangeDataService(IApiCoreService apiCoreService, IAuthService authService, POSModule posModule, InventModule invModule, string mySQLConnectionString)
        {
            _apiCoreService = apiCoreService;
            _authService = authService;
            _posModule = posModule;
            _invModule = invModule;
            _mySQLConnectionString = mySQLConnectionString;
        }

        public async Task ReceiveInventDataAsync(int shopId)
        {
            var json = "";
            try
            {
                json = await _apiCoreService.ExchangeInventDataHqAsync("", shopId, 0, 1);
            }
            catch (Exception ex)
            {
                var errMsg = ex.Message;
                if (ex.InnerException != null)
                    errMsg = $"{errMsg} ({ex.InnerException.Message})";
            }

            using (var conn = new MySqlConnection(_mySQLConnectionString))
            {
                await conn.OpenAsync();
                var respText = "";
                var success = _posModule.ImportDocumentData(ref respText, json, conn);
                if (success)
                {
                    try
                    {
                        var jObj = JObject.Parse(json);
                        var documents = (JArray)jObj["document"];
                        var docKeys = documents.Select(d => (string)d["DocumentKey"]).ToArray();
                        await _apiCoreService.UpdateExchangeInventoryStatusAsync(shopId, docKeys);
                    }
                    catch { }
                }
            }
        }

        public async Task SendInvenDataAsync(int shopId, int keyShopId = 0, int documentId = 0, int documentTypeId = 0)
        {
            using (var conn = new MySqlConnection(_mySQLConnectionString))
            {
                await conn.OpenAsync();
                var respText = "";
                var ds = new DataSet();
                var json = "";
                var success = _posModule.ExportInventData(ref respText, ref ds, ref json, 0, "", shopId, documentId, keyShopId, 0, 0, conn);
                if (!success)
                {
                    return;
                }

                var resp = await _apiCoreService.SendInventoryDataToHqAsync("", shopId, 0, 1, json);
                success = _posModule.SyncInventUpdate(ref respText, resp, conn);

                if (!success)
                {
                    throw new Exception(respText);
                }
            }
        }
    }
}
