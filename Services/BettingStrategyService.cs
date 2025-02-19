using Newtonsoft.Json;

namespace BetfairSpOddsBandBetPlacer.Services
{
    public class BettingStrategyService
    {
        public Dictionary<string, List<string>> LoadBetfairSpBands()
        {
            string filePath = @"C:\Users\danny\Documents\BetfairSpPositiveDifferences.json";

            if (!File.Exists(filePath))
            {
                Console.WriteLine("BetfairSpPositiveDifferences.json not found.");
                return new Dictionary<string, List<string>>();
            }

            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);
        }

        public bool IsSpWithinPositiveBands(decimal confirmedSp, Dictionary<string, List<string>> positiveSpBands, string track)
        {
            if (!positiveSpBands.ContainsKey(track))
                return false;

            foreach (var band in positiveSpBands[track])
            {
                var bounds = band.Split('-').Select(decimal.Parse).ToList();
                if (confirmedSp >= bounds[0] && confirmedSp <= bounds[1])
                {
                    Console.WriteLine($"SP {confirmedSp} is within positive band {band} for track {track}. Placing bet.");
                    return true;
                }
            }

            return false;
        }
    }
}
