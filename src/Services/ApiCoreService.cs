using System;
using System.Net.Http;
using System.Threading.Tasks;
using VerticalTec.Core.Models;
using VtecInventory.Exceptions;
using VtecInventory.Models;

namespace VtecInventory.Services
{
    public class ApiCoreService : IApiCoreService
    {
        private readonly IRequestProvider _requestProvider;

        public ApiCoreService(IRequestProvider provider)
        {
            _requestProvider = provider;
        }

        public string ReqId { get; private set; }

        public async Task<MerchantProfile> GetMerchantInfoAsync(string key)
        {
            if (string.IsNullOrEmpty(ReqId))
                ReqId = Guid.NewGuid().ToString();

            var resp = await _requestProvider.SendRequestAsync<ApiResponse<MerchantProfile>>(HttpMethod.Get, $"api/MerchantInfo/MerchantInfo?reqId={ReqId}&weburl={key}");

            if (resp.ResponseCode != "")
            {
                ReqId = "";
                throw new ApiCoreException(resp.ResponseText);
            }
            return resp.ResponseObj;
        }

        public void InitBaseUrl(string baseUrl)
        {
            if(!baseUrl.EndsWith("/"))
                baseUrl += "/";
            _requestProvider.InitBaseUrl(baseUrl);
        }

        public async Task<string> SendInventoryDataToHqAsync(string deviceKey, int shopId, int computerId, int langId, object data)
        {
            var resp = await _requestProvider.SendRequestAsync<ApiResponse<object>>(HttpMethod.Post, $"api/POSClient/SendInventoryDataToHQ?reqId={ReqId}&deviceKey={deviceKey}&shopId={shopId}&computerId={computerId}&langId={langId}", data);
            if (resp.ResponseCode != "")
            {
                throw new ApiCoreException(resp.ResponseText);
            }
            return resp.ResponseObj?.ToString();
        }

        public async Task<string> ExchangeInventDataHqAsync(string deviceKey, int shopId, int computerId, int langId)
        {
            var resp = await _requestProvider.SendRequestAsync<ApiResponse<object>>(HttpMethod.Post, $"api/POSClientCustom/ExchangeInventoryDataHQ?reqId={ReqId}&deviceKey={deviceKey}&shopId={shopId}&computerId={computerId}&langId={langId}");
            if (resp.ResponseCode != "")
            {
                throw new ApiCoreException(resp.ResponseText);
            }
            return resp.ResponseObj?.ToString();
        }
        public async Task<string> UpdateExchangeInventoryStatusAsync(int shopId, string[] docKeys)
        {
            var resp = await _requestProvider.SendRequestAsync<ApiResponse<object>>(HttpMethod.Post, $"api/POSClientCustom/UpdateExchangeInventoryStatus?reqId={ReqId}&shopId={shopId}", docKeys);
            if (resp.ResponseCode != "")
            {
                throw new ApiCoreException(resp.ResponseText);
            }
            return resp.ResponseObj?.ToString();
        }
    }
}
