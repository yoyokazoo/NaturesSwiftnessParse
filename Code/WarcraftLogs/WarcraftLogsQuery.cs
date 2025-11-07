using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NaturesSwiftnessParse
{
    public static class WarcraftLogsQuery
    {
        private static readonly HttpClient _client = new HttpClient();

        private static string OAuthToken = String.Empty;

        private static string ClientId = null;
        private static string ClientSecret = null;

        public static void LoadClientIdAndSecret(string clientId, string clientSecret)
        {
            if (clientId != null && clientSecret != null)
            {
                ClientId = clientId;
                ClientSecret = clientSecret;
                return;
            }

            Console.WriteLine("Client ID or Secret not passed in, checking WarcraftLogsClient.json");

            var client = new WarcraftLogsClient();
            try
            {
                using (StreamReader stream = new StreamReader($"{WarcraftLogsClient.NAME}"))
                {
                    client = JsonSerializer.Deserialize<WarcraftLogsClient>(stream.ReadToEnd());
                }
            }
            catch (Exception)
            {
                // create empty file for user to fill in
                using (StreamWriter stream = new StreamWriter($"{WarcraftLogsClient.NAME}"))
                {
                    string jsonString = JsonSerializer.Serialize<WarcraftLogsClient>(client);
                    stream.Write(jsonString);
                }

                throw new Exception($"No ClientID or Secret passed in, and no WarcraftLogsClient.json exists.  Empty file was created, fill it in with info from {WarcraftLogsClient.CLIENT_URL}.  Quitting so the ClientId and ClientSecret can be filled in.");
            }

            ClientId = client.ClientId;
            ClientSecret = client.ClientSecret;
        }

        // Assumes LoadClientIdAndSecret was called and succeeded before this
        private static async Task<string> GetOauthToken()
        {
            if (OAuthToken == String.Empty)
            {
                try
                {
                    OAuthToken = await GetAccessToken(
                        clientId: ClientId,
                        clientSecret: ClientSecret
                    );
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to obtain OAuth token.", ex);
                }
            }

            return OAuthToken;
        }

        private static async Task<string> GetAccessToken(string clientId, string clientSecret)
        {
            var body = new StringContent(
                $"grant_type=client_credentials&client_id={clientId}&client_secret={clientSecret}",
                Encoding.UTF8,
                "application/x-www-form-urlencoded"
            );

            var resp = await _client.PostAsync("https://www.warcraftlogs.com/oauth/token", body);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            // Extract "access_token" from JSON
            var token = System.Text.Json.JsonDocument.Parse(json)
                .RootElement.GetProperty("access_token")
                .GetString();

            return token;
        }

        public static async Task<string> QueryWarcraftLogs(string payload)
        {
            var token = await GetOauthToken();
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _client.PostAsync("https://www.warcraftlogs.com/api/v2/client", content);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync();
        }

        public static async Task<string> QueryForReport(string reportId)
        {
            var query = $@"
            {{
              reportData {{
                report(code: ""{reportId}"") {{
                  title
                  fights {{
                    id
                    name
                    startTime
                    endTime
                  }}
                  masterData {{
                    actors {{
                      id
                      name
                      type
                      petOwner
                    }}
                  }}
                }}
              }}
            }}";

            var payload = JsonSerializer.Serialize(new { query });

            return await QueryWarcraftLogs(payload);
        }

        public const int DAMAGE_TAKEN_QUERY_LIMIT = 250;
        public static async Task<string> QueryForDamageTaken(string reportId, int fightId, long startTime, long endTime)
        {
            var query = $@"
            {{
              reportData {{
                report(code: ""{reportId}"") {{
                  events(
                    dataType: DamageTaken
                    fightIDs: [{fightId}]
                    includeResources: true
                    startTime: {startTime}
                    endTime: {endTime}
                    limit: {DAMAGE_TAKEN_QUERY_LIMIT}
                  ) {{
                    data
                    nextPageTimestamp
                  }}
                }}
              }}
            }}
            ";

            var payload = JsonSerializer.Serialize(new { query });

            return await QueryWarcraftLogs(payload);
        }

        public static async Task<string> QueryForAbilityCastEvents(string reportId, List<int> fightIds, int abilityId)
        {
            string fightIdList = string.Join(",", fightIds);
            var query = $@"
            {{
              reportData {{
                report(code: ""{reportId}"") {{
                  events(
                    dataType: Casts
                    abilityID: {abilityId}
                    fightIDs: [{fightIdList}]
                  ) {{
                    data
                    nextPageTimestamp
                  }}
                }}
              }}
            }}
            ";

            var payload = JsonSerializer.Serialize(new { query });

            return await QueryWarcraftLogs(payload);
        }

        public const int HEALING_EVENT_QUERY_LIMIT = 250;
        public static async Task<string> QueryForHealingEvents(string reportId, int fightId, long startTime, long endTime)
        {
            var query = $@"
            {{
              reportData {{
                report(code: ""{reportId}"") {{
                  events(
                    dataType: Healing
                    fightIDs: [{fightId}]
                    includeResources: true
                    startTime: {startTime}
                    endTime: {endTime}
                    limit: {HEALING_EVENT_QUERY_LIMIT}
                  ) {{
                    data
                    nextPageTimestamp
                  }}
                }}
              }}
            }}
            ";

            var payload = JsonSerializer.Serialize(new { query });

            return await QueryWarcraftLogs(payload);
        }
    }
}
