using BetfairSpOddsBandBetPlacer.Config;
using BetfairSpOddsBandBetPlacer.Services;
using Microsoft.Extensions.DependencyInjection;

class Program
{
    static async Task Main(string[] args)
    {
        // Ensure all required command-line arguments are provided
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: BetfairSpRankBetPlacer.exe <AppKey> <Username> <Password> <stake>");
            return;
        }

        // Initialize config with command-line arguments
        BetfairConfig.Initialize(args[0], args[1], args[2], args[3]);

        var services = new ServiceCollection();
        services.AddHttpClient<BetfairAuthService>();
        services.AddHttpClient<BetfairMarketService>();
        services.AddHttpClient<BetfairBettingService>();
        services.AddSingleton<BettingStrategyService>();

        var serviceProvider = services.BuildServiceProvider();

        var authService = serviceProvider.GetRequiredService<BetfairAuthService>();
        var marketService = serviceProvider.GetRequiredService<BetfairMarketService>();
        var bettingService = serviceProvider.GetRequiredService<BetfairBettingService>();
        var strategyService = serviceProvider.GetRequiredService<BettingStrategyService>();

        // Authenticate and get Betfair API token
        string authToken = await authService.AuthenticateAsync();

        // Retrieve today's horse racing markets
        var markets = await marketService.GetHorseRacingMarketsAsync(authToken);

        // Load positive SP bands from JSON
        var positiveSpBands = strategyService.LoadBetfairSpBands();

        // Map each market to its track name
        var marketTrackMapping = new Dictionary<string, string>();
        foreach (var market in markets)
        {
            string marketId = market["marketId"].ToString();
            string track = market["event"]["venue"].ToString(); // Betfair's venue field stores track name
            marketTrackMapping[marketId] = track;
        }

        // Monitor all markets simultaneously
        List<Task> marketTasks = new List<Task>();

        foreach (var market in markets)
        {
            string marketId = market["marketId"].ToString();

            foreach (var runner in market["runners"])
            {
                string selectionId = runner["selectionId"].ToString();

                marketTasks.Add(MonitorAndBetOnMarket(authToken, marketId, selectionId, bettingService, positiveSpBands, marketTrackMapping));
            }
        }

        await Task.WhenAll(marketTasks); // Run all market monitoring tasks concurrently
    }

    /// <summary>
    /// Monitors a single selection in a market, waits for SP confirmation, and places a bet if conditions are met.
    /// </summary>
    static async Task MonitorAndBetOnMarket(
        string authToken, string marketId, string selectionId,
        BetfairBettingService bettingService,
        Dictionary<string, List<string>> positiveSpBands,
        Dictionary<string, string> marketTrackMapping)
    {
        try
        {
            Console.WriteLine($"Monitoring market {marketId}, selection {selectionId} for SP confirmation...");

            // Wait for SP confirmation before betting
            await bettingService.WaitForSPConfirmationAsync(authToken, marketId);

            // Place bet only if SP is within a positive band for the correct track
            await bettingService.PlaceBetAfterSPAsync(authToken, marketId, selectionId, BetfairConfig.Stake, positiveSpBands, marketTrackMapping);
        }
        catch (Exception ex) { Console.WriteLine($"Error whilst monitoring market {marketId}, selection {selectionId}. {ex.Message}"); }
    }
}
