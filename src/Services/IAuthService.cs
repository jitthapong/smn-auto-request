using System.Threading.Tasks;

namespace VtecInventory.Services
{
    public interface IAuthService
    {
        string AccessToken { get; }

        void InitBaseUrl(string baseUrl);

        Task RequestTokenAsync();
    }
}
