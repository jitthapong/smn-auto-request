using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static vtecPOS.GlobalFunctions.InventObject;

namespace VtecInventory.Services
{
    public interface IExchangeDataService
    {
        Task SendInvenDataAsync(int shopId, int keyShopId = 0, int documentId = 0, int documentTypeId = 0);

        Task ReceiveInventDataAsync(int shopId);
    }
}
