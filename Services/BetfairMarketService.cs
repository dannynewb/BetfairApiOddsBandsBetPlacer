using Newtonsoft.Json.Linq;
using BetfairSpOddsBandBetPlacer.Config;
using System.Text;

namespace BetfairSpOddsBandBetPlacer.Services
{
    public class BetfairMarketService
    {
        private readonly HttpClient _httpClient;

        public BetfairMarketService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        private readonly string _baseUrl = "https://api.betfair.com/exchange/betting/rest/v1.0/";

        public async Task<JArray> GetHorseRacingMarketsAsync(string authToken)
        {
            _httpClient.DefaultRequestHeaders.Add("X-Application", BetfairConfig.AppKey);
            _httpClient.DefaultRequestHeaders.Add("X-Authentication", authToken);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            // Get start of today (00:00 UTC) and end of today (23:59:59 UTC)
            string todayStart = DateTime.UtcNow.Date.ToString("yyyy-MM-ddT00:00:00Z");
            string todayEnd = DateTime.UtcNow.Date.AddDays(1).AddSeconds(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");

            var requestBody = new
            {
                filter = new
                {
                    eventTypeIds = new[] { "7" }, // EventTypeId 7 = Horse Racing
                    marketCountries = new[] { "GB", "IE" }, // Only UK & Ireland
                    marketTypeCodes = new[] { "WIN" }, // Only WIN markets
                    marketStartTime = new
                    {
                        from = todayStart,  // Start of today (00:00 UTC)
                        to = todayEnd       // End of today (23:59:59 UTC)
                    }
                },
                marketProjection = new[] { "MARKET_START_TIME", "EVENT", "RUNNER_METADATA" },
                maxResults = "100"
            };

            var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(_baseUrl + "listMarketCatalogue/", content);
            string jsonResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to retrieve markets: {jsonResponse}");
            }

            return JArray.Parse(jsonResponse);
        }
    }
}
