using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerticalTec.Core.Models;

namespace VtecInventory.Services
{
    public interface IApiCoreService
    {
        string ReqId { get; }

        void InitBaseUrl(string baseUrl);

        Task<MerchantProfile> GetMerchantInfoAsync(string key);

        Task<string> SendInventoryDataToHqAsync(string deviceKey, int shopId, int computerId, int langId, object data);

        Task<string> ExchangeInventDataHqAsync(string deviceKey, int shopId, int computerId, int langId);
        Task<string> UpdateExchangeInventoryStatusAsync(int shopId, string[] docKeys);
    }
}
