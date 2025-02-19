using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using BetfairSpOddsBandBetPlacer.Config;
using System.Text;

namespace BetfairSpOddsBandBetPlacer.Services
{
    public class BetfairBettingService
    {
        private readonly HttpClient _httpClient;
        private readonly BettingStrategyService _bettingStrategyService;

        public BetfairBettingService(HttpClient httpClient, BettingStrategyService bettingStrategyService)
        {
            _httpClient = httpClient;
            _bettingStrategyService = bettingStrategyService;
        }

        private readonly string _baseUrl = "https://api.betfair.com/exchange/betting/rest/v1.0/";

        public async Task<JArray> GetMarketOddsAsync(string authToken, string marketId)
        {
            if (!_httpClient.DefaultRequestHeaders.Contains("X-Application"))
                _httpClient.DefaultRequestHeaders.Add("X-Application", BetfairConfig.AppKey);

            if (!_httpClient.DefaultRequestHeaders.Contains("X-Authentication"))
                _httpClient.DefaultRequestHeaders.Add("X-Authentication", authToken);

            if (!_httpClient.DefaultRequestHeaders.Contains("Accept"))
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var requestBody = new
            {
                marketIds = new[] { marketId },
                priceProjection = new { priceData = new[] { "EX_BEST_OFFERS", "SP_AVAILABLE", "SP_TRADED" } }
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(_baseUrl + "listMarketBook/", content);
            string jsonResponse = await response.Content.ReadAsStringAsync();

            try
            {
                var parsedJson = JToken.Parse(jsonResponse);

                if (parsedJson.Type == JTokenType.Array)
                {
                    return (JArray)parsedJson;
                }
                else if (parsedJson.Type == JTokenType.Object)
                {
                    return new JArray(parsedJson);
                }
                else
                {
                    throw new Exception("Unexpected API response format.");
                }
            }
            catch (JsonException ex)
            {
                throw new Exception("Failed to parse API response: " + ex.Message);
            }
        }

        public async Task WaitForSPConfirmationAsync(string authToken, string marketId)
        {
            Console.WriteLine($"Waiting for SP confirmation for market {marketId}...");

            while (true)
            {
                var marketOdds = await GetMarketOddsAsync(authToken, marketId);
                if (marketOdds == null || !marketOdds.Any())
                {
                    Console.WriteLine($"No market data found for {marketId}. Retrying...");
                    await Task.Delay(5000);
                    continue;
                }

                var market = marketOdds[0];
                if (market["bspReconciled"] != null && market["bspReconciled"].ToObject<bool>())
                {
                    Console.WriteLine($"SP confirmed for market {marketId}. Proceeding to place bet.");
                    break;
                }

                await Task.Delay(5000); // Wait 5 seconds before checking again
            }
        }


        public async Task<double> GetConfirmedSPAsync(string authToken, string marketId, string selectionId)
        {
            var marketOdds = await GetMarketOddsAsync(authToken, marketId);
            foreach (var runner in marketOdds[0]["runners"])
            {
                if (runner["selectionId"].ToString() == selectionId)
                {
                    return runner["sp"]["actualSP"].ToObject<double>();
                }
            }

            return 0; // Return 0 if no SP found
        }

        public async Task PlaceBetAfterSPAsync(string authToken, string marketId, string selectionId, double stake, Dictionary<string, List<string>> positiveSpBands, Dictionary<string, string> marketTrackMapping)
        {
            double confirmedSp = await GetConfirmedSPAsync(authToken, marketId, selectionId);
            Console.WriteLine($"Confirmed SP for selection {selectionId} in market {marketId}: {confirmedSp}");

            // Find the track for this market (ignore case)
            string track = marketTrackMapping
                .Where(kvp => kvp.Key.Equals(marketId, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Value)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(track))
            {
                Console.WriteLine($"Could not determine track for market {marketId}. Skipping.");
                return;
            }

            // Ensure case-insensitive lookup in positive SP bands
            string matchedTrack = positiveSpBands.Keys
                .FirstOrDefault(t => t.Equals(track, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(matchedTrack))
            {
                Console.WriteLine($"No positive SP bands found for track {track}. Skipping.");
                return;
            }

            // Check if SP is within a positive band for this track
            if (!_bettingStrategyService.IsSpWithinPositiveBands(Convert.ToDecimal(confirmedSp), positiveSpBands, track))
            {
                Console.WriteLine($"SP {confirmedSp} is NOT within a positive difference band for track {track}. No bet placed.");
                return;
            }

            if (!_httpClient.DefaultRequestHeaders.Contains("X-Application"))
                _httpClient.DefaultRequestHeaders.Add("X-Application", BetfairConfig.AppKey);

            if (!_httpClient.DefaultRequestHeaders.Contains("X-Authentication"))
                _httpClient.DefaultRequestHeaders.Add("X-Authentication", authToken);

            if (!_httpClient.DefaultRequestHeaders.Contains("Accept"))
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var betRequest = new
            {
                marketId,
                instructions = new[]
                                {
                    new
                    {
                        selectionId,
                        handicap = 0,
                        side = "BACK",
                        orderType = "LIMIT", // Places a bet at fixed odds
                        limitOrder = new
                        {
                            size = stake,
                            price = 1.01, // Minimum possible odds
                            persistenceType = "PERSIST" // Ensures the bet remains in-play if unmatched
                        }
                    }
                }
            };

            var content = new StringContent(JsonConvert.SerializeObject(betRequest), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync(_baseUrl + "placeOrders/", content);

            string jsonResponse = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Bet placed at SP: " + jsonResponse);
        }
    }
}
