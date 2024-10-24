using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VtecInventory.Services
{
    public class RequestProvider : IRequestProvider
    {
        private HttpClient _httpClient;
        private IAuthService _authService;

        private int _retryAuthCount;

        public RequestProvider(IAuthService authService)
        {
            _authService = authService;

            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
        }

        public void InitBaseUrl(string baseUrl)
        {
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        public async Task<T> SendRequestAsync<T>(HttpMethod method, string requestUri, object body = null)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authService.AccessToken);

            var request = new HttpRequestMessage(method, requestUri);
            if (body != null)
            {
                var jsonBody = "";
                if (body.GetType() == typeof(string))
                    jsonBody = body.ToString();
                else
                    jsonBody = JsonConvert.SerializeObject(body);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                request.Content = content;
            }

            var response = await _httpClient.SendAsync(request);
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    if (++_retryAuthCount == 5)
                        throw new Exception("Cannot get token after retry 5 times");

                    await _authService.RequestTokenAsync();
                    _retryAuthCount = 0;
                    return await SendRequestAsync<T>(method, requestUri, body);
                }
                else
                {
                    throw;
                }
            }
            var serializer = new JsonSerializer();
            var result = default(T);
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var sr = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(sr))
                result = serializer.Deserialize<T>(jsonReader);
            return result;
        }
    }
}
