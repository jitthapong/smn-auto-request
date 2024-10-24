using System.Net.Http;
using System.Threading.Tasks;

namespace VtecInventory.Services
{
    public interface IRequestProvider
    {
        void InitBaseUrl(string baseUrl);
        Task<T> SendRequestAsync<T>(HttpMethod method, string requestUri, object body = null);
    }
}
