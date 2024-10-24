using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace VtecInventory.Services
{
    public class AuthService : IAuthService
    {
        private const string ClientId = "inventory.v4";
        private const string ClientSecret = "04ee66f6d7eda54a";

        private HttpClient _httpClient;

        public AuthService()
        {
            _httpClient = new HttpClient();
        }

        public string AccessToken { get; private set; }

        public void InitBaseUrl(string baseUrl)
        {
            if(!baseUrl.EndsWith("/"))
                baseUrl += "/";
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        public async Task RequestTokenAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"api/identity/token?clientid={ClientId}&clientSecret={ClientSecret}");
            var token = new
            {
                access_token = "",
                expires_in = 0,
                token_type = "Bearer",
                scope = "verticaltec.api"
            };
            var resp = await _httpClient.SendAsync(request);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            token = JsonConvert.DeserializeAnonymousType(json, token);
            AccessToken = token?.access_token;
        }
    }
}
