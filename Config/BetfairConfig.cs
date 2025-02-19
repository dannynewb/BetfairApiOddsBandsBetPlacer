namespace BetfairSpOddsBandBetPlacer.Config
{
    public static class BetfairConfig
    {
        public static string AppKey { get; private set; }
        public static string Username { get; private set; }
        public static string Password { get; private set; }
        public static double Stake { get; private set; }

        public static void Initialize(string appKey, string username, string password, string stake)
        {
            AppKey = appKey;
            Username = username;
            Password = password;
            Stake = Convert.ToDouble(stake);
        }
    }
}
