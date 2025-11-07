namespace NaturesSwiftnessParse
{
    public class WarcraftLogsClient
    {
        public const string NAME = "WarcraftLogsClient.json";
        public const string CLIENT_URL = "https://fresh.warcraftlogs.com/api/clients/";

        public string ClientId { get; set; } = $"FILL IN FROM {CLIENT_URL}";
        public string ClientSecret { get; set; } = $"FILL IN FROM {CLIENT_URL}";
    }
}
