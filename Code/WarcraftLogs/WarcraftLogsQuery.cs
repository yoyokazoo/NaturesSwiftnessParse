using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
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

            //Console.WriteLine("Client ID or Secret not passed in, checking WarcraftLogsClient.json");

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

        // Caps how many requests are in flight at once -- callers (e.g. WindfuryUptimeParse) can
        // fan out dozens/hundreds of queries via Task.WhenAll, and firing them all simultaneously is
        // what trips WCL's rate limiting. 5 concurrent requests is conservative but keeps things
        // moving without bursting.
        private static readonly SemaphoreSlim _concurrencyLimiter = new SemaphoreSlim(5, 5);

        private const int MAX_RATE_LIMIT_RETRIES = 5;

        // WCL's rate limiting is an hourly points quota, not just a burst limit -- if it's been
        // exhausted, the Retry-After it sends back can be tens of minutes. Retrying that
        // automatically would just hang the CLI, so only auto-retry short waits (a real burst) and
        // fail fast with a clear message otherwise.
        private static readonly TimeSpan MAX_AUTO_RETRY_DELAY = TimeSpan.FromSeconds(30);

        // Same reportId/fightId/ability/etc. always produces the same GraphQL query text, and a
        // given query's result never changes once fetched (WCL reports are immutable), so responses
        // are cached to disk keyed by a hash of the exact query. This is by far the biggest lever on
        // wall-clock time across repeated runs against the same report while iterating -- a cache
        // hit skips the network entirely (no OAuth token fetch, no concurrency wait).
        private const string CACHE_DIR = "WarcraftLogsCache";
        public static bool CacheEnabled = true;

        public static async Task<string> QueryWarcraftLogs(string payload)
        {
            string cacheFilePath = CacheEnabled ? Path.Combine(CACHE_DIR, ComputeCacheKey(payload) + ".json") : null;

            if (cacheFilePath != null)
            {
                var cached = TryReadCache(cacheFilePath);
                if (cached != null) return cached;
            }

            var token = await GetOauthToken();
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            await _concurrencyLimiter.WaitAsync();
            try
            {
                for (int attempt = 0; ; attempt++)
                {
                    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var resp = await _client.PostAsync("https://www.warcraftlogs.com/api/v2/client", content);

                    if ((int)resp.StatusCode == 429)
                    {
                        // Honor Retry-After when WCL sends one; otherwise back off with jitter.
                        var retryAfter = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));

                        if (retryAfter > MAX_AUTO_RETRY_DELAY)
                        {
                            throw new Exception($"WCL API rate limit hit (429) and the requested retry wait is {retryAfter.TotalMinutes:0.#} minutes -- likely the hourly points quota is exhausted, not just a burst. Not auto-retrying; wait for the quota to reset and try again.");
                        }

                        if (attempt >= MAX_RATE_LIMIT_RETRIES)
                        {
                            throw new Exception($"WCL API rate limit hit (429) {MAX_RATE_LIMIT_RETRIES} times in a row; giving up.");
                        }

                        Console.WriteLine($"WARNING: WCL API rate limit hit (429), retrying in {retryAfter.TotalSeconds:0.#}s (attempt {attempt + 1}/{MAX_RATE_LIMIT_RETRIES})...");
                        await Task.Delay(retryAfter);
                        continue;
                    }

                    resp.EnsureSuccessStatusCode();
                    var result = await resp.Content.ReadAsStringAsync();
                    if (cacheFilePath != null) WriteCache(cacheFilePath, result);
                    return result;
                }
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        }

        private static string ComputeCacheKey(string payload)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        // Returns the cached response text, or null on a miss (file doesn't exist) or anything that
        // looks like a corrupt/unreadable cache entry -- either way, falling through to a live fetch
        // is always safe, so failures here are swallowed rather than thrown.
        private static string TryReadCache(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var text = File.ReadAllText(path);
                return string.IsNullOrEmpty(text) ? null : text;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Writes via a uniquely-named temp file then a move, so a crash mid-write (or two identical
        // requests racing) never leaves behind a partially-written cache entry that TryReadCache
        // could hand back as if it were a full response.
        private static void WriteCache(string path, string content)
        {
            try
            {
                Directory.CreateDirectory(CACHE_DIR);
                var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
                File.WriteAllText(tempPath, content);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tempPath, path);
            }
            catch (Exception ex)
            {
                // Failing to cache shouldn't fail the whole run -- just means this response gets
                // re-fetched next time.
                Console.WriteLine($"WARNING: failed to write cache file {path}: {ex.Message}");
            }
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
                    encounterID
                  }}
                  masterData {{
                    actors {{
                      id
                      name
                      type
                      subType
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

        public const int CAST_EVENT_QUERY_LIMIT = 250;
        // Paginated single-fight cast query, unlike QueryForAbilityCastEvents above (which spans
        // multiple fights but doesn't page) -- needed for totem-twisting tracking, where a shaman can
        // rack up many casts of the same ability in one fight.
        public static async Task<string> QueryForCastEventsForFight(string reportId, int fightId, long startTime, long endTime, int abilityId)
        {
            var query = $@"
            {{
              reportData {{
                report(code: ""{reportId}"") {{
                  events(
                    dataType: Casts
                    abilityID: {abilityId}
                    fightIDs: [{fightId}]
                    startTime: {startTime}
                    endTime: {endTime}
                    limit: {CAST_EVENT_QUERY_LIMIT}
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

        public const int BUFF_EVENT_QUERY_LIMIT = 250;
        public static async Task<string> QueryForBuffEvents(string reportId, int fightId, long startTime, long endTime, int abilityId)
        {
            var query = $@"
            {{
              reportData {{
                report(code: ""{reportId}"") {{
                  events(
                    dataType: Buffs
                    abilityID: {abilityId}
                    fightIDs: [{fightId}]
                    includeResources: true
                    startTime: {startTime}
                    endTime: {endTime}
                    limit: {BUFF_EVENT_QUERY_LIMIT}
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

        public const int COMBATANT_INFO_QUERY_LIMIT = 250;
        // CombatantInfo rows are a per-player gear/talent/aura snapshot taken near the pull of each
        // fight -- used here to determine each warrior/rogue's main-hand weapon enchant.
        public static async Task<string> QueryForCombatantInfo(string reportId, int fightId, long startTime, long endTime)
        {
            var query = $@"
            {{
              reportData {{
                report(code: ""{reportId}"") {{
                  events(
                    dataType: CombatantInfo
                    fightIDs: [{fightId}]
                    startTime: {startTime}
                    endTime: {endTime}
                    limit: {COMBATANT_INFO_QUERY_LIMIT}
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
