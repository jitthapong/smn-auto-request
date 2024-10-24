using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerticalTec.Core.Models
{
    public class MerchantProfile
    {
        public int MerchantID { get; set; }
        public string MerchantKey { get; set; }
        public string MerchantName { get; set; }
        public string MobileDBName { get; set; }
        public string MobileDBServer { get; set; }
        public string CRMDBName { get; set; }
        public string CRMDBServer { get; set; }
        public string HQDBName { get; set; }
        public string HQDBServer { get; set; }
        public string ResourceDBName { get; set; }
        public string ResourceDBServer { get; set; }
        public string LinkedServer { get; set; }
        public string BackOfficeUrl { get; set; }
        public string SendDataApiUrl { get; set; }
        public string SaleModeList { get; set; }
        public int RegisterMemberGroupID { get; set; }
        public string paymentGateway { get; set; }
        public string allowPayType { get; set; }
        public LoyaltyParam LoyaltyInfo { get; set; }
        public ConfigParam ConfigInfo { get; set; }
        public AirPayParam AirPayParam { get; set; }
        public DolfinParam DolfinParam { get; set; }
        public ShopeePayParam ShopeePayParam { get; set; }
        public CPNHubParam CPNHubParam { get; set; }
        public PosNetECouponParam PosNetECouponParam { get; set; }
        public googleMapsParams googleMapsParams { get; set; }
        public mPayParams mPayParams { get; set; }
        public List<LoyaltyOnlineOrder> OnlineOrder { get; set; }
        public List<BrandInfo> BrandData { get; set; }
        public List<object> SaleModeInfo { get; set; }
        public deviceInfo deviceInfo { get; set; }
    }

    public class deviceInfo
    {
        public int ShopID { get; set; }
        public string ShopCode { get; set; }
        public string ShopName { get; set; }
        public int ComputerID { get; set; }
        public string ComputerName { get; set; }
    }

    public class mPayParams
    {
        public string returnURL { get; set; }
    }

    public class SystemParam
    {
        public LoyaltyParam LoyaltyParam { get; set; }
        public ConfigParam ConfigParam { get; set; }
        public AirPayParam AirPayParam { get; set; }
        public DolfinParam DolfinParam { get; set; }
        public CPNHubParam CPNHubParam { get; set; }
        public PosNetECouponParam PosNetECouponParam { get; set; }
        public googleMapsParams googleMapsParams { get; set; }
        public mPayParams mPayParams { get; set; }

        public List<LoyaltyOnlineOrder> OnlineOrder { get; set; }
    }

    public class AirPayParam
    {
        public bool IsProduction { get; set; }
        public string dev_baseUrl { get; set; }
        public string dev_appKey { get; set; }
        public string dev_appId { get; set; }
        public string dev_merchantName { get; set; }
        public string prod_baseUrl { get; set; }
        public string prod_appKey { get; set; }
        public string prod_appId { get; set; }
        public string prod_merchantName { get; set; }
    }

    public class DolfinParam
    {
        public bool IsProduction { get; set; }
        public string dev_baseUrl { get; set; }
        public string dev_clientKey { get; set; }
        public string dev_clientSecret { get; set; }
        public string dev_appId { get; set; }
        public string dev_aesKey { get; set; }
        public string dev_merchantId { get; set; }
        public string prod_baseUrl { get; set; }
        public string prod_clientKey { get; set; }
        public string prod_clientSecret { get; set; }
        public string prod_appId { get; set; }
        public string prod_aesKey { get; set; }
        public string prod_merchantId { get; set; }
    }

    public class ShopeePayParam
    {
        public bool IsProduction { get; set; }
        public string dev_baseUrl { get; set; }
        public string dev_clientKey { get; set; }
        public string dev_clientSecret { get; set; }
        public string dev_merchantId { get; set; }
        public string prod_baseUrl { get; set; }
        public string prod_clientKey { get; set; }
        public string prod_clientSecret { get; set; }
        public string prod_merchantId { get; set; }
    }

    public class CPNHubParam
    {
        public bool IsProduction { get; set; }
        public string dev_baseUrl { get; set; }
        public string dev_appKey { get; set; }
        public string prod_baseUrl { get; set; }
        public string prod_appKey { get; set; }
    }

    public class PosNetECouponParam
    {
        public bool IsProduction { get; set; }
        public string dev_version { get; set; }
        public string dev_baseUrl { get; set; }
        public string dev_apiType { get; set; }
        public string dev_appId { get; set; }
        public string dev_businessUnit { get; set; }
        public string dev_businessName { get; set; }
        public string prod_version { get; set; }
        public string prod_baseUrl { get; set; }
        public string prod_apiType { get; set; }
        public string prod_appId { get; set; }
        public string prod_businessUnit { get; set; }
        public string prod_businessName { get; set; }
    }

    public class LoyaltyParam
    {
        public string Logo { get; set; }
        public string PrimaryColor { get; set; }
        public string SecondaryColor { get; set; }
        public string backgroundColor { get; set; }
        public string surfaceColor { get; set; }
        public string lineColor { get; set; }
        public string primaryTextColor { get; set; }
        public string secondaryTextColor { get; set; }
        public string surfaceTextColor { get; set; }
        public bool RegisterExistingMember { get; set; }
        public bool RegisterPINCode { get; set; }
        public bool EnableDeliveryWeb { get; set; }
    }

    public class LoyaltyOnlineOrder
    {
        public int moduleId { get; set; }
        public List<object> moduleName { get; set; }
        public string moduleUrl { get; set; }
        public bool isScanQR { get; set; }
        public bool isNearStore { get; set; }
        public int ordering { get; set; }
    }

    public class ConfigParam
    {
        public bool EnableMember { get; set; }
        public bool Enable_eCoupon { get; set; }
        public bool Enalbe_eVoucher { get; set; }
        public int summaryPage { get; set; }
        public bool OnlyPayAtCounter { get; set; }
        public int SMSGateway { get; set; }
        public string SMSUsername { get; set; }
        public string SMSPassword { get; set; }
        public string SMSSender { get; set; }
        public string DeliveryAgent { get; set; }
        public int OrderPageLayout { get; set; }
        public string MobileRegex { get; set; }
        public int TableService { get; set; }
        public bool QRCodeOffLine { get; set; }
        public bool SendDataService { get; set; }
        public string SendDataMethod { get; set; }
        public string FileServerDir { get; set; }
        public string FileServerNetworkDir { get; set; }
        public string FileServerUrl { get; set; }
        public decimal MaxStoreDistance { get; set; }
        public string DBType { get; set; }
    }

    public class BrandInfo
    {
        public int BrandID { get; set; }
        public string BrandCode { get; set; }
        public string BrandKey { get; set; }
        public string BrandName { get; set; }
        public int BrandOrdering { get; set; }
    }

    public class googleMapsParams
    {
        public string key { get; set; }
        public string mapId { get; set; }
        public string version { get; set; }
        public string libraries { get; set; }
        public string language { get; set; }
        public string region { get; set; }
    }
}
