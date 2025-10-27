using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.Json;
using NaturesSwiftnessParse.Properties;
using System.Runtime.InteropServices;

namespace NaturesSwiftnessParse
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var reportId = textBox1.Text;
            if (reportId == string.Empty) 
            {
                Console.WriteLine("Fill in report Id");
                return;
            }

            _ = RunNaturesSwiftnessReport(new List<string> { reportId }, debugFightId: null);

            //QueryForDamageTaken

            /*
            Raid afflickzTestRaid = new Raid("Afflickz test raid");
            FightTimelines garr = new FightTimelines("Garr");

            afflickzTestRaid.AddFight(garr);

            string testFile = "C:\\Users\\peter\\Downloads\\Events - Garr Normal  Kill   (825 PM) - Report Wednesday MC  Warcraft Logs Vanilla (2).csv";
            List<HealthPointEvent> healthPointEvents = GetHealthPointEventsFromFile(testFile);
            garr.AddHealthPointEvents(healthPointEvents);

            Console.WriteLine($"Found HP Timelines for {string.Join(", ", garr.Timelines.Keys)}");
            foreach (var hpt in garr.Timelines.Values)
            {
                hpt.Print();
            }

            string testNaturesSwiftnessCastsFile = "C:\\Users\\peter\\Downloads\\Events - Garr Normal  Kill   (825 PM) - Report Wednesday MC  Warcraft Logs Vanilla (5).csv";
            var naturesSwiftnessEvents = GetNaturesSwiftnessEventsFromFile(testNaturesSwiftnessCastsFile);

            foreach (var nature in naturesSwiftnessEvents)
            {
                Console.WriteLine(nature);
            }

            string testHealsFile = "C:\\Users\\peter\\Downloads\\Events - Garr Normal  Kill   (825 PM) - Report Wednesday MC  Warcraft Logs Vanilla (4).csv";
            var healEvents = GetHealEventsFromFile(testHealsFile);

            foreach (var heal in healEvents)
            {
                Console.WriteLine(heal);
            }
            */
        }

        static async Task RunNaturesSwiftnessReport(List<string> reportIds, int? debugFightId)
        {
            Console.WriteLine($"Running Report for {string.Join(",", reportIds)}");
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // Get overarching report with fights
            var reportJson = await QueryForReport(reportIds.First());
            var root = JsonSerializer.Deserialize<ReportDataRoot>(reportJson, options);

            var raidReport = new RaidReport(reportIds.First(), root.Data.ReportData.Report.Title);
            foreach(var fight in root.Data.ReportData.Report.Fights)
            {
                var fightReport = new FightReport(fight.Id, fight.Name, fight.StartTime, fight.EndTime);
                raidReport.AddFight(fightReport);
            }

            //raidReport.PrintFights();

            // Get master actors
            var masterDataJson = await QueryForMasterDataActors(reportIds.First());
            var masterDataRoot = JsonSerializer.Deserialize<MasterDataRoot>(masterDataJson, options);
            foreach(var actor in masterDataRoot.Data.ReportData.Report.MasterData.Actors)
            {
                raidReport.AddActor(actor.Id, actor.Name);
            }

            // These will be filled dynamically eventually, but for now...
            raidReport.AddAbility(1064,  "Chain Heal (Rank 1)");
            raidReport.AddAbility(10622, "Chain Heal (Rank 2)");
            raidReport.AddAbility(10623, "Chain Heal (Rank 3)");
            raidReport.AddAbility(8004, "Lesser Healing Wave (Rank 1)");
            raidReport.AddAbility(8008, "Lesser Healing Wave (Rank 2)");
            raidReport.AddAbility(8010, "Lesser Healing Wave (Rank 3)");
            raidReport.AddAbility(10466, "Lesser Healing Wave (Rank 4)");
            raidReport.AddAbility(10467, "Lesser Healing Wave (Rank 5)");
            raidReport.AddAbility(10468, "Lesser Healing Wave (Rank 6)");
            raidReport.AddAbility(331, "Healing Wave (Rank 1)");
            raidReport.AddAbility(332, "Healing Wave (Rank 2)");
            raidReport.AddAbility(547, "Healing Wave (Rank 3)");
            raidReport.AddAbility(913, "Healing Wave (Rank 4)");
            raidReport.AddAbility(939, "Healing Wave (Rank 5)");
            raidReport.AddAbility(959, "Healing Wave (Rank 6)");
            raidReport.AddAbility(8005, "Healing Wave (Rank 7)");
            raidReport.AddAbility(10395, "Healing Wave (Rank 8)");
            raidReport.AddAbility(10396, "Healing Wave (Rank 9)");
            raidReport.AddAbility(25357, "Healing Wave (Rank 10)");

            //raidReport.PrintActors();

            var allFightIds = raidReport.Fights.Keys.ToList();
            if (debugFightId.HasValue)
            {
                allFightIds = new List<int> { debugFightId.Value };
            }

            // TODO: add debug option to only look at 1 fight, since otherwise it's real hard to parse by eyeball

            // Get Natures Swiftnesses for whole raid
            var nsJson = await QueryForNaturesSwiftnessEvents(reportIds.First(), allFightIds);
            var nsRoot = JsonSerializer.Deserialize<NaturesSwiftnessRoot>(nsJson, options);
            foreach (var ns in nsRoot.Data.ReportData.Report.Events.Data)
            {
                var sourceName = raidReport.GetActor(ns.SourceID.Value);
                var nsEvent = new NaturesSwiftnessEvent(sourceName, ns.Timestamp, ns.Fight.Value);
                raidReport.AddNaturesSwiftnessEvent(nsEvent);
            }

            //raidReport.PrintNaturesSwiftnessEvents();

            // For each fight, grab the heal events
            foreach(var fightId in allFightIds)
            {
                long nextPageTimestamp = 0;
                var endTime = raidReport.GetFight(fightId).EndTime;

                do
                {
                    var healJson = await QueryForHealingEvents(reportIds.First(), fightId, nextPageTimestamp, endTime);
                    var healRoot = JsonSerializer.Deserialize<HealEventRoot>(healJson, options);
                    foreach (var heal in healRoot.Data.ReportData.Report.Events.Data)
                    {
                        if (heal.Type != "heal") continue;

                        string sourceName = raidReport.GetActor(heal.SourceID.Value);
                        string targetName = raidReport.GetActor(heal.TargetID.Value);
                        int healAmount = heal.Extra["amount"].GetInt32();
                        int overheal = heal.Extra.ContainsKey("overheal") ? heal.Extra["overheal"].GetInt32() : 0;
                        bool critical = heal.Extra["hitType"].GetInt32() == 2; // 1 is normal, 2 is critical
                        string abilityName = raidReport.GetAbility(heal.AbilityGameID.Value);

                        var healEvent = new HealEvent(heal.Timestamp, healAmount, overheal, sourceName, targetName, abilityName, critical);
                        raidReport.GetFight(heal.Fight.Value).AddHealEvent(healEvent);

                        // Also log heal events as HP events if they're non-zero
                        if (healAmount > 0)
                        {
                            HealthPointEvent hpEvent = new HealthPointEvent(heal.Timestamp, -1 * healAmount, heal.HitPoints.Value, targetName, heal.TargetID.Value);
                            raidReport.GetFight(heal.Fight.Value).AddHealthPointEvent(hpEvent);
                        }
                    }

                    nextPageTimestamp = healRoot.Data.ReportData.Report.Events.NextPageTimestamp ?? 0;
                } while (nextPageTimestamp != 0);

                do
                {
                    // For each fight, grab the damage taken
                    var damageJson = await QueryForDamageTaken(reportIds.First(), fightId, nextPageTimestamp, endTime);
                    var damageRoot = JsonSerializer.Deserialize<DamageTakenRoot>(damageJson, options);
                    foreach (var damageTaken in damageRoot.Data.ReportData.Report.Events.Data)
                    {
                        if (damageTaken.Amount == 0) continue;

                        string targetName = raidReport.GetActor(damageTaken.TargetID.Value);
                        //string actorName = raidReport.GetActor(hpChange.ResourceActor.Value);
                        int hpAmount = damageTaken.Amount.Value;
                        int hpPercent = damageTaken.HitPoints.Value;
                        HealthPointEvent hpEvent = new HealthPointEvent(damageTaken.Timestamp, hpAmount, hpPercent, targetName, damageTaken.TargetID.Value);
                        raidReport.GetFight(damageTaken.Fight.Value).AddHealthPointEvent(hpEvent);
                    }

                    nextPageTimestamp = damageRoot.Data.ReportData.Report.Events.NextPageTimestamp ?? 0;
                } while (nextPageTimestamp != 0);

                // now that we've inserted both, sort the hp timelines
                foreach (var hpTimeline in raidReport.GetFight(fightId).HealthPointTimelines.Values)
                {
                    hpTimeline.SortByTime();
                    //hpTimeline.Print();
                }
            }
            
            /*
            foreach (var nsEvent in raidReport.NaturesSwiftnessEvents)
            {
                raidReport.GetFight(nsEvent.FightId).GetHealTimeline(nsEvent.CasterName).Print();
            }
            */

            //raidReport.GetFight(5).GetHealTimeline("Moodatude").Print();

            foreach (var nsEvent in raidReport.NaturesSwiftnessEvents)
            {
                // Link the ns to the heal
                FightReport fight = raidReport.GetFight(nsEvent.FightId);
                HealTimeline healTimeline = fight.GetHealTimeline(nsEvent.CasterName);
                foreach(HealEvent heal in healTimeline.Events)
                {
                    if (heal.Time < nsEvent.Time) continue;

                    if (heal.Time >= nsEvent.Time)
                    {
                        nsEvent.AddHealEvent(heal);
                        break;
                    }
                }

                Console.WriteLine(nsEvent);

                if (nsEvent.HealEvent == null)
                {
                    Console.WriteLine($"After looking, never found HealEvent for {nsEvent}, might be worth looking into");
                    continue;
                }

                // Link the heal to the target's HP
                HealthPointTimeline healthPointTimeline = fight.GetHealthPointTimeline(nsEvent.HealEvent.TargetName);
                if (healthPointTimeline == null)
                {
                    Console.WriteLine($"null healthPointTimeline for fight {fight.Id} for {nsEvent.HealEvent.TargetName}, might be worth looking into");
                    continue;
                }
                healthPointTimeline.Print();

                HealthPointEvent hpChangeBeforeNS = null;
                HealthPointEvent hpChangeBeforeHeal = null;
                HealthPointEvent damageBeforeNS = null;
                HealthPointEvent damageBeforeHeal = null;
                for (int i = 0; i < healthPointTimeline.Events.Count; i++)
                {
                    var hpChange = healthPointTimeline.Events[i];
                    if (hpChange.Time < nsEvent.Time)
                    {
                        hpChangeBeforeNS = hpChange;
                        if (hpChange.Damage > 0)
                        {
                            damageBeforeNS = hpChange;
                        }
                    }

                    if (hpChange.Time < nsEvent.HealTime)
                    {
                        hpChangeBeforeHeal = hpChange;
                        if (hpChange.Damage > 0)
                        {
                            damageBeforeHeal = hpChange;
                        }
                    }
                }

                nsEvent.NSDamageEvent = damageBeforeNS;
                nsEvent.NSHealthPointEvent = hpChangeBeforeNS;

                nsEvent.HealDamageEvent = damageBeforeHeal;
                nsEvent.HealHealthPointEvent = hpChangeBeforeHeal;
            }

            foreach (var nsEvent in raidReport.NaturesSwiftnessEvents)
            {
                Console.WriteLine(nsEvent);
            }

            raidReport.PrintMostCriticalNaturesSwiftnesses();
        }

        /*
        private List<HealthPointEvent> GetHealthPointEventsFromFile(string filePath)
        {
            List<HealthPointEvent> healthPointEvents = new List<HealthPointEvent>();
            List<string> lines = BreakApartIntoLinesFilePath(filePath);

            foreach (var line in lines)
            {
                var (success, time, hpRaw, hpPercent, targetName) = ParseHPResourceLine(line);

                if (!success) continue;

                HealthPointEvent newEvent = new HealthPointEvent(time, hpRaw, hpPercent, targetName);
                healthPointEvents.Add(newEvent);
            }

            return healthPointEvents;
        }

        private List<NaturesSwiftnessEvent> GetNaturesSwiftnessEventsFromFile(string filePath)
        {
            List<NaturesSwiftnessEvent> naturesSwiftnessEvents = new List<NaturesSwiftnessEvent>();
            List<string> nsLines = BreakApartIntoLinesFilePath(filePath);
            foreach (var nsLine in nsLines)
            {
                var (success, time, casterName) = ParseNaturesSwiftnessLine(nsLine);

                if (!success) continue;

                NaturesSwiftnessEvent newEvent = new NaturesSwiftnessEvent(casterName, time);
                naturesSwiftnessEvents.Add(newEvent);
            }

            return naturesSwiftnessEvents;
        }

        private List<HealEvent> GetHealEventsFromFile(string filePath)
        {
            List<HealEvent> healEvents = new List<HealEvent>();
            List<string> healLines = BreakApartIntoLinesFilePath(filePath);
            foreach (var healLine in healLines)
            {
                var (success, time, casterName, healName, firstTargetName, secondTargetName, healAmount, overhealAmount, criticalHeal) = ParseHealLine(healLine);

                if (!success) continue;

                var targetName = secondTargetName == string.Empty ? firstTargetName : $"{firstTargetName} ({secondTargetName})";

                HealEvent newEvent = new HealEvent(time, healAmount, overhealAmount, casterName, targetName, healName, criticalHeal);
                healEvents.Add(newEvent);
            }

            return healEvents;
        }
        */

        private List<string> BreakApartIntoLinesFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("The specified file was not found.", filePath);

            var result = new List<string>();

            foreach (var line in File.ReadLines(filePath))
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    result.Add(trimmed);
            }

            return result;
        }

        // AI: Write a method that takes a string similar to the below, breaks it apart on /r/n, and returns a list of strings
        // string testASDF = "\"00:08.713\",\"Damage\",\"Melee\",\"1141\",\"79.0%\",\"Firesworn 2 → Afflickz\",\"\"\r\n\"00:08.713\",\"Damage\",\"Melee\",\"2360\",\"37.0%\",\"Firesworn 7 → Afflickz\",\"\"\r\n\"00:08.713\",\"Damage\",\"Melee\",\"1156\",\"16.0%\",\"Firesworn 3 → Afflickz\",\"\"";
        private List<string> BreakApartIntoLinesString(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new List<string>();

            // Split on both Windows (\r\n) and Unix (\n) line endings just in case
            var lines = input.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            // Trim quotes and whitespace from each line if desired
            var result = new List<string>(lines.Length);
            foreach (var line in lines)
            {
                result.Add(line.Trim());
            }

            return result;
        }

        // Example URL to download CSV from: https://vanilla.warcraftlogs.com/reports/TF3wJrXpDLt7Zqy4?fight=27&type=casts&view=events&sourceclass=Shaman&ability=16188
        // "00:05.845","Catduck Lesser Healing Wave Supawarr +1103 (O: 245)",""
        // Catduck Lesser Healing Wave Supawarr +*1460*
        // Catduck Lesser Healing Wave Supawarr +1103 (O: 245)
        // Justfortotem Chain Heal Hedwig (Zranihuntard) +701
        // (\p{L}+) ([A-Za-z\s]+) (\p{L}+) ?\(?(\p{L}+)?\)? \+\*?(\d+)(\*?) ?\(?O?:? ?(\d+)?\)?
        // https://regex101.com/
        public static readonly Regex HEAL_REGEX = new Regex("(\\p{L}+) ([A-Za-z\\s]+) (\\p{L}+) ?\\(?(\\p{L}+)?\\)? \\+(\\*?)(\\d+)\\*? ?\\(?O?:? ?(\\d+)?\\)?");
        public static (bool success, float time, string casterName, string healName, string firstTargetName, string secondTargetName, int healAmount, int overhealAmount, bool criticalHeal) ParseHealLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                throw new ArgumentException("Line cannot be null or empty.", nameof(line));

            // Split on commas but ignore commas inside quotes
            // (since this is a consistent CSV-like format)
            string[] parts = line.Split(new[] { "\",\"" }, StringSplitOptions.None);

            if (parts.Length != 3)
                throw new FormatException("Unexpected line format.");

            // Clean up quotes
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim('"');
            }

            // Example line parts:
            // [0] = 00:05.845
            // [1] = Catduck Lesser Healing Wave Supawarr +1103 (O: 245)
            // [2] = (empty string)

            // Extract time: use substring after "00:" or just parse the seconds directly
            string timeStr = parts[0].Replace("00:", "");
            if (!float.TryParse(timeStr, out float time))
            {
                return (success: false, 0, string.Empty, string.Empty, string.Empty, string.Empty, 0, 0, false);
            }

            var healMatches = HEAL_REGEX.Match(parts[1]);
            var casterName = healMatches.Groups[1].Value;
            var healName = healMatches.Groups[2].Value;
            var firstTargetName = healMatches.Groups[3].Value;
            var secondTargetName = healMatches.Groups[4].Value;
            var critical = healMatches.Groups[5].Value != string.Empty;
            var healAmount = int.Parse(healMatches.Groups[6].Value);
            var overheal = healMatches.Groups[7].Value == string.Empty ? 0 : int.Parse(healMatches.Groups[7].Value);

            return (success: true, time, casterName, healName, firstTargetName, secondTargetName, healAmount, overheal, criticalHeal: critical);
        }

        // Example URL to download CSV from: https://vanilla.warcraftlogs.com/reports/TF3wJrXpDLt7Zqy4?fight=27&type=casts&view=events&sourceclass=Shaman&ability=16188
        public static (bool success, float time, string casterName) ParseNaturesSwiftnessLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                throw new ArgumentException("Line cannot be null or empty.", nameof(line));

            // Split on commas but ignore commas inside quotes
            // (since this is a consistent CSV-like format)
            string[] parts = line.Split(new[] { "\",\"" }, StringSplitOptions.None);

            if (parts.Length != 5)
                throw new FormatException("Unexpected line format.");

            // Clean up quotes
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim('"');
            }

            // Example line parts:
            // [0] = 00:08.713
            // [1] = Cast
            // [2] = Nature's Swiftness
            // [3] = Moodatude
            // [4] = (empty string)

            // Extract time: use substring after "00:" or just parse the seconds directly
            string timeStr = parts[0].Replace("00:", "");
            if (!float.TryParse(timeStr, out float time))
            {
                return (success: false, 0, string.Empty);
            }

            return (success: true, time, parts[3]);
        }

        // Example URL to download CSV from: https://vanilla.warcraftlogs.com/reports/TF3wJrXpDLt7Zqy4?fight=27&type=resources
        // AI: Write a method that takes a line similar to this:
        // string line = "\"00:08.713\",\"Damage\",\"Melee\",\"1141\",\"79.0%\",\"Firesworn 2 → Afflickz\",\"\";
        // and extract the time (8.713) as a float, the raw hp number (1141) as an int, the hp percent (79.0) as a float, and the name of the target, after the arrow (Afflickz) as a string
        public static (bool success, float time, int hpRaw, float hpPercent, string targetName) ParseHPResourceLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                throw new ArgumentException("Line cannot be null or empty.", nameof(line));

            // Split on commas but ignore commas inside quotes
            // (since this is a consistent CSV-like format)
            string[] parts = line.Split(new[] { "\",\"" }, StringSplitOptions.None);

            if (parts.Length != 7)
                throw new FormatException("Unexpected line format.");

            // Clean up quotes
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim('"');
            }

            // Example line parts:
            // [0] = 00:08.713
            // [1] = Damage
            // [2] = Melee
            // [3] = 1141
            // [4] = 79.0%
            // [5] = Firesworn 2 → Afflickz
            // [6] = (empty string)

            // If parts[2] starts with "Multiple Heals", ignore it as we don't care about the summary, and sometimes it batches heals to different targets
            if (parts[2].StartsWith("Multiple Heals"))
            {
                return (success: false, 0, 0, 0, string.Empty);
            }

            // Extract time: use substring after "00:" or just parse the seconds directly
            string timeStr = parts[0].Replace("00:", "");
            if (!float.TryParse(timeStr, out float time))
            {
                return (success: false, 0, 0, 0, string.Empty);
            }

            // Extract HP raw
            string hpRawStr = parts[3].Replace("+", "-");
            if (!int.TryParse(hpRawStr, out int hpRaw))
            {
                return (success: false, 0, 0, 0, string.Empty);
            }

            // Extract HP percent (remove %)
            string hpPercentStr = parts[4].Replace("%", "");
            if (!float.TryParse(hpPercentStr, out float hpPercent))
            {
                return (success: false, 0, 0, 0, string.Empty);
            }

            // Extract target name (after arrow)
            string targetField = parts[5];
            string targetName = targetField;
            int arrowIndex = targetField.IndexOf('→');
            if (arrowIndex >= 0)
                targetName = targetField.Substring(arrowIndex + 1).Trim();

            return (success: true, time, hpRaw, hpPercent, targetName);
        }

        // from chat GPT
        // 

        

        

        static async Task<string> QueryForMasterDataActors(string reportId)
        {
            var query = $@"
            {{
              reportData {{
                report(code: ""{reportId}"") {{
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

        static async Task<string> QueryForReport(string reportId)
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
                }}
              }}
            }}";

            var payload = JsonSerializer.Serialize(new { query });

            return await QueryWarcraftLogs(payload);
        }

        public const int DAMAGE_TAKEN_QUERY_LIMIT = 250;
        static async Task<string> QueryForDamageTaken(string reportId, int fightId, long startTime, long endTime)
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

        static async Task<string> QueryForNaturesSwiftnessEvents(string reportId, List<int> fightIds)
        {
            string fightIdList = string.Join(",", fightIds);
            var query = $@"
            {{
              reportData {{
                report(code: ""{reportId}"") {{
                  events(
                    dataType: Casts
                    abilityID: 16188
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
        static async Task<string> QueryForHealingEvents(string reportId, int fightId, long startTime, long endTime)
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

        private static readonly HttpClient _client = new HttpClient();

        static string OAuthToken = String.Empty;
        static async Task<string> GetOauthToken()
        {
            if (OAuthToken == String.Empty)
            {
                using (StreamReader stream = new StreamReader($"{WarcraftLogsClient.NAME}"))
                {
                    WarcraftLogsClient fileReadResult = JsonSerializer.Deserialize<WarcraftLogsClient>(stream.ReadToEnd());

                    OAuthToken = await GetAccessToken(
                        clientId: fileReadResult.ClientId,
                        clientSecret: fileReadResult.ClientSecret
                    );
                }
            }

            return OAuthToken;
        }

        static async Task<string> GetAccessToken(string clientId, string clientSecret)
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

        static async Task<string> QueryWarcraftLogs(string payload)
        {
            var token = await GetOauthToken();
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _client.PostAsync("https://www.warcraftlogs.com/api/v2/client", content);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            var reportId = textBox1.Text;
            if (reportId == string.Empty)
            {
                Console.WriteLine("Fill in report Id");
                return;
            }

            var fightId = int.Parse(textBox2.Text);

            _ = RunNaturesSwiftnessReport(new List<string> { reportId }, fightId);
        }
    }
}
