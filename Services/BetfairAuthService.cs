using Newtonsoft.Json.Linq;
using BetfairSpOddsBandBetPlacer.Config;

namespace BetfairSpOddsBandBetPlacer.Services
{
    public class BetfairAuthService
    {
        private readonly HttpClient _httpClient;

        public BetfairAuthService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> AuthenticateAsync()
        {
            _httpClient.DefaultRequestHeaders.Add("X-Application", BetfairConfig.AppKey);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", BetfairConfig.Username),
                new KeyValuePair<string, string>("password", BetfairConfig.Password)
            });

            HttpResponseMessage response = await _httpClient.PostAsync("https://identitysso.betfair.com/api/login", content);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var json = JObject.Parse(responseBody);
                string token = json["token"]?.ToString();

                if (!string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("Successfully authenticated with Betfair.");
                    return token;
                }
                else
                {
                    throw new Exception("Authentication failed: Token not found in response.");
                }
            }

            throw new Exception("Failed to authenticate with Betfair: " + responseBody);
        }
    }
}
