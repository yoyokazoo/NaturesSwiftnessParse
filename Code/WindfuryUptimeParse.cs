using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace NaturesSwiftnessParse
{
    public static class WindfuryUptimeParse
    {
        // Vanilla combat logs don't record raid subgroup assignment -- it's client-side roster
        // state, not a combat-log event. Windfury Totem's own buff events can't fill that gap
        // either: empirically, every applybuff/refreshbuff for it has sourceID == targetID (the
        // player is logged as buffing themselves), so there's no source to trace back to a totem or
        // shaman. Instead, "the shaman's group" is inferred from party-restricted (not raid-wide)
        // buffs -- see InferPartyGroups below.
        private static readonly HashSet<string> EligibleClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Warrior", "Rogue"
        };

        // Buffs that in vanilla only affect the caster's 5-man party (20-45yd party-only radius),
        // not the whole raid -- used purely as a clustering signal to infer party membership, since
        // WCL exposes no raid-subgroup field. All known ranks included; a rank nobody in this raid
        // actually uses just returns no events, which is harmless.
        private static readonly int[] PartyBuffAbilityIds =
        {
            // Battle Shout (Warrior)
            6673, 5242, 6192, 11549, 11550, 11551, 25289,
            // Trueshot Aura (Hunter)
            19506,
            // Leader of the Pack (Druid, feral)
            24932,
            // Blood Pact (Warlock's Imp)
            6307
        };

        // Blizzard's INVSLOT_MAINHAND (16), 0-based since WCL's gear array has no explicit slot field
        private const int MAIN_HAND_ARRAY_INDEX = 15;

        public static async Task RunWindfuryUptimeReport(string reportId, int? debugFightId, string clientId, string clientSecret, string playerName = null)
        {
            Console.WriteLine($"Running Windfury Uptime Report for {reportId}");

            WarcraftLogsQuery.LoadClientIdAndSecret(clientId, clientSecret);

            var reportDataResult = await NaturesSwiftnessParse.GetFightsAndActors(reportId);
            RaidReport raidReport = NaturesSwiftnessParse.ProcessFightsAndActors(reportId, reportDataResult);

            var allReportFightIds = raidReport.Fights.Keys.ToList();

            // If we're debugging a single fight, only fetch combatant info/Windfury events for that
            // one -- but party-buff data is always fetched report-wide (see below), regardless of
            // debugFightId.
            var allFightIds = debugFightId.HasValue ? new List<int> { debugFightId.Value } : allReportFightIds;

            var combatantInfoResults = await GetCombatantInfo(raidReport, reportId, allFightIds);
            ProcessCombatantInfo(raidReport, combatantInfoResults);

            var windfuryBuffResults = await GetWindfuryBuffEvents(raidReport, reportId, allFightIds);
            var windfuryEventsByFight = ProcessWindfuryBuffEvents(windfuryBuffResults);

            bool debugGear = Environment.GetEnvironmentVariable("WF_DEBUG_GEAR") == "1";

            // Raid groups are set once per raid night, not reshuffled fight to fight, so infer party
            // membership report-wide rather than per fight -- a single short fight (or one that opens
            // with buffs already active from before pull, outside that fight's own event window) may
            // not contain the co-application timestamp needed to link its members at all, but the
            // same players almost always show up together in at least one fight across the night.
            // Querying party buffs for every fight in the report (x10 ability ranks each) can easily
            // be hundreds of requests, so this stops as soon as every shaman has been placed in some
            // group rather than exhausting every fight.
            var groupOf = await InferPartyGroupsIncrementally(raidReport, reportId, allReportFightIds, debugFightId, debugGear);
            if (debugGear) PrintInferredGroups(raidReport, groupOf);

            var fightResults = new List<WindfuryFightResult>();
            foreach (var fightId in allFightIds)
            {
                var relevantEvents = windfuryEventsByFight.TryGetValue(fightId, out var events) ? events : new List<EventRow>();

                fightResults.Add(ComputeWindfuryForFight(raidReport, fightId, relevantEvents, groupOf));
            }

            if (playerName != null)
            {
                Console.WriteLine($"Filtering to only {playerName}'s group");
            }

            // First debugging step requested: which players are in which shaman's group for each
            // fight, and each member's individual uptime result.
            PrintDebugInfo(fightResults, playerName);

            PrintSummary(fightResults, playerName);
        }

        // ----- Fetching -----

        private static async Task<List<(int FightId, List<ReportDataRoot> Roots)>> GetCombatantInfo(RaidReport raidReport, string reportId, List<int> allFightIds)
        {
            var tasks = allFightIds.Select(async fightId =>
            {
                var roots = await GetCombatantInfoForFight(raidReport, reportId, fightId);
                return (fightId, roots);
            });

            return (await Task.WhenAll(tasks)).ToList();
        }

        private static async Task<List<ReportDataRoot>> GetCombatantInfoForFight(RaidReport raidReport, string reportId, int fightId)
        {
            long nextPageTimestamp = 0;
            var endTime = raidReport.GetFight(fightId).EndTime;
            List<ReportDataRoot> combatantRoots = new List<ReportDataRoot>();

            do
            {
                var combatantJson = await WarcraftLogsQuery.QueryForCombatantInfo(reportId, fightId, nextPageTimestamp, endTime);
                var combatantRoot = JsonSerializer.Deserialize<ReportDataRoot>(combatantJson);
                combatantRoots.Add(combatantRoot);
                nextPageTimestamp = combatantRoot.Data.ReportData.Report.Events.NextPageTimestamp ?? 0;
            }
            while (nextPageTimestamp != 0);

            return combatantRoots;
        }

        private static async Task<List<(int FightId, List<ReportDataRoot> Roots)>> GetWindfuryBuffEvents(RaidReport raidReport, string reportId, List<int> allFightIds)
        {
            var tasks = allFightIds.Select(async fightId =>
            {
                var roots = await GetWindfuryBuffEventsForFight(raidReport, reportId, fightId);
                return (fightId, roots);
            });

            return (await Task.WhenAll(tasks)).ToList();
        }

        private static async Task<List<ReportDataRoot>> GetWindfuryBuffEventsForFight(RaidReport raidReport, string reportId, int fightId)
        {
            long nextPageTimestamp = 0;
            var endTime = raidReport.GetFight(fightId).EndTime;
            List<ReportDataRoot> buffRoots = new List<ReportDataRoot>();

            do
            {
                var buffJson = await WarcraftLogsQuery.QueryForBuffEvents(reportId, fightId, nextPageTimestamp, endTime, TotemBuffEvent.WINDFURY_ABILITY_ID);
                var buffRoot = JsonSerializer.Deserialize<ReportDataRoot>(buffJson);
                buffRoots.Add(buffRoot);
                nextPageTimestamp = buffRoot.Data.ReportData.Report.Events.NextPageTimestamp ?? 0;
            }
            while (nextPageTimestamp != 0);

            return buffRoots;
        }

        // All party-buff ability ranks for a single fight, fetched in parallel.
        private static async Task<List<(int AbilityId, List<ReportDataRoot> Roots)>> GetPartyBuffEventsForFight(RaidReport raidReport, string reportId, int fightId)
        {
            var tasks = PartyBuffAbilityIds.Select(async abilityId =>
            {
                var (_, _, roots) = await GetPartyBuffEventsForFightAndAbility(raidReport, reportId, fightId, abilityId);
                return (abilityId, roots);
            });

            return (await Task.WhenAll(tasks)).ToList();
        }

        private static async Task<(int FightId, int AbilityId, List<ReportDataRoot> Roots)> GetPartyBuffEventsForFightAndAbility(RaidReport raidReport, string reportId, int fightId, int abilityId)
        {
            long nextPageTimestamp = 0;
            var endTime = raidReport.GetFight(fightId).EndTime;
            List<ReportDataRoot> roots = new List<ReportDataRoot>();

            do
            {
                var buffJson = await WarcraftLogsQuery.QueryForBuffEvents(reportId, fightId, nextPageTimestamp, endTime, abilityId);
                var buffRoot = JsonSerializer.Deserialize<ReportDataRoot>(buffJson);
                roots.Add(buffRoot);
                nextPageTimestamp = buffRoot.Data.ReportData.Report.Events.NextPageTimestamp ?? 0;
            }
            while (nextPageTimestamp != 0);

            return (fightId, abilityId, roots);
        }

        // ----- CombatantInfo / gear processing -----

        private static void ProcessCombatantInfo(RaidReport raidReport, List<(int FightId, List<ReportDataRoot> Roots)> combatantInfoResults)
        {
            foreach (var (fightId, roots) in combatantInfoResults)
            {
                foreach (var root in roots)
                {
                    ProcessCombatantInfoRoot(raidReport, fightId, root);
                }
            }
        }

        private static void ProcessCombatantInfoRoot(RaidReport raidReport, int fightId, ReportDataRoot combatantRoot)
        {
            var rows = combatantRoot?.Data?.ReportData?.Report?.Events?.Data;
            if (rows == null) return;

            int matched = 0;
            foreach (var row in rows)
            {
                if (row.Type != "combatantinfo") continue;
                matched++;

                if (!row.SourceID.HasValue) continue;

                var gearInfo = ParseCombatantGearInfo(row, row.SourceID.Value, fightId);
                raidReport.GetFight(fightId)?.AddCombatantGearInfo(gearInfo);
            }

            // The exact "type" string and gear field names below are our best understanding of the
            // WCL v2 CombatantInfo shape, unverified against a live report while planning this. If
            // this fires, the field-name assumptions are wrong -- inspect the raw JSON and adjust.
            if (matched == 0 && rows.Count > 0)
            {
                var seenTypes = string.Join(", ", rows.Select(r => r.Type).Distinct());
                Console.WriteLine($"WARNING: got {rows.Count} CombatantInfo row(s) for fight {fightId} but none had type == \"combatantinfo\" (saw: {seenTypes}). Field-name assumptions may be wrong -- inspect raw JSON.");
            }
        }

        private static CombatantGearInfo ParseCombatantGearInfo(EventRow row, int actorId, int fightId)
        {
            if (row.Extra == null || !row.Extra.TryGetValue("gear", out var gearElement) || gearElement.ValueKind != JsonValueKind.Array)
            {
                Console.WriteLine($"WARNING: no \"gear\" array found on combatantinfo for actor {actorId}, fight {fightId}; treating as ineligible for Windfury this fight since we can't confirm their main-hand enchant.");
                return new CombatantGearInfo(actorId, fightId, hasMainHandWeapon: false, mainHandHasTemporaryEnchant: false, mainHandTemporaryEnchantId: null, mainHandPermanentEnchantId: null);
            }

            JsonElement? mainHandItem = FindMainHandItem(gearElement, actorId, fightId);

            if (mainHandItem == null || !mainHandItem.Value.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.Number || idProp.GetInt32() == 0)
            {
                return new CombatantGearInfo(actorId, fightId, hasMainHandWeapon: false, mainHandHasTemporaryEnchant: false, mainHandTemporaryEnchantId: null, mainHandPermanentEnchantId: null);
            }

            int? temporaryEnchantId = TryGetNonZeroInt(mainHandItem.Value, "temporaryEnchant");
            int? permanentEnchantId = TryGetNonZeroInt(mainHandItem.Value, "permanentEnchant");

            return new CombatantGearInfo(
                actorId,
                fightId,
                hasMainHandWeapon: true,
                mainHandHasTemporaryEnchant: temporaryEnchantId.HasValue,
                mainHandTemporaryEnchantId: temporaryEnchantId,
                mainHandPermanentEnchantId: permanentEnchantId
            );
        }

        // Confirmed empirically against report CLMRnrG9DcVA4txY: WCL's CombatantInfo "gear" array
        // carries no explicit slot id/number -- it's purely positional, following Blizzard's
        // 19-slot inventory ordering (Head, Neck, Shoulder, Shirt, Chest, Waist, Legs, Feet, Wrist,
        // Hands, Finger1, Finger2, Trinket1, Trinket2, Back, MainHand, OffHand, Ranged, Tabard).
        // MainHand is index 15 (0-based). Empty slots are present as placeholders (id: 0).
        private static JsonElement? FindMainHandItem(JsonElement gearElement, int actorId, int fightId)
        {
            var items = gearElement.EnumerateArray().ToList();
            if (items.Count <= MAIN_HAND_ARRAY_INDEX)
            {
                Console.WriteLine($"WARNING: gear array for actor {actorId}, fight {fightId} only has {items.Count} entries, expected at least {MAIN_HAND_ARRAY_INDEX + 1}; can't find main-hand.");
                return null;
            }

            return items[MAIN_HAND_ARRAY_INDEX];
        }

        private static int? TryGetNonZeroInt(JsonElement obj, string propertyName)
        {
            if (!obj.TryGetProperty(propertyName, out var prop)) return null;
            if (prop.ValueKind != JsonValueKind.Number) return null;

            int value = prop.GetInt32();
            return value == 0 ? (int?)null : value;
        }

        // ----- Windfury buff event processing -----

        private static Dictionary<int, List<EventRow>> ProcessWindfuryBuffEvents(List<(int FightId, List<ReportDataRoot> Roots)> buffResults)
        {
            var eventsByFight = new Dictionary<int, List<EventRow>>();

            foreach (var (fightId, roots) in buffResults)
            {
                foreach (var root in roots)
                {
                    var rows = root?.Data?.ReportData?.Report?.Events?.Data;
                    if (rows == null) continue;

                    foreach (var row in rows)
                    {
                        // Windfury Totem re-pulses show up as "refreshbuff", not just "applybuff" --
                        // both count as a (re)application that restarts the 10s window. "removebuff"
                        // is intentionally ignored: uptime is modeled as fixed 10s windows from each
                        // (re)application, not by tracking literal apply/remove pairs.
                        if (row.Type != "applybuff" && row.Type != "refreshbuff") continue;

                        if (!eventsByFight.TryGetValue(fightId, out var list))
                        {
                            list = new List<EventRow>();
                            eventsByFight[fightId] = list;
                        }
                        list.Add(row);
                    }
                }
            }

            return eventsByFight;
        }

        // ----- Party-buff processing / group inference -----

        private static Dictionary<int, List<EventRow>> ProcessPartyBuffEvents(List<(int FightId, int AbilityId, List<ReportDataRoot> Roots)> buffResults)
        {
            var eventsByFight = new Dictionary<int, List<EventRow>>();

            foreach (var (fightId, _, roots) in buffResults)
            {
                foreach (var root in roots)
                {
                    var rows = root?.Data?.ReportData?.Report?.Events?.Data;
                    if (rows == null) continue;

                    foreach (var row in rows)
                    {
                        if (row.Type != "applybuff" && row.Type != "refreshbuff") continue;

                        if (!eventsByFight.TryGetValue(fightId, out var list))
                        {
                            list = new List<EventRow>();
                            eventsByFight[fightId] = list;
                        }
                        list.Add(row);
                    }
                }
            }

            return eventsByFight;
        }

        // Minimal union-find over actor ids, used to cluster players into inferred 5-man parties.
        private class UnionFind
        {
            private readonly Dictionary<int, int> _parent = new Dictionary<int, int>();

            private int Find(int x)
            {
                if (!_parent.ContainsKey(x)) _parent[x] = x;
                if (_parent[x] != x) _parent[x] = Find(_parent[x]);
                return _parent[x];
            }

            public void Union(int a, int b)
            {
                int rootA = Find(a);
                int rootB = Find(b);
                if (rootA != rootB) _parent[rootA] = rootB;
            }

            public int GroupOf(int x) => Find(x);
        }

        // A vanilla party is capped at 5, so a "full" inferred group is the shaman plus 4 others.
        private const int FULL_PARTY_SIZE = 5;

        // Fetches party-buff events one fight at a time (all 10 ability ranks for that fight in
        // parallel), re-running group inference after each fight and stopping only once every
        // shaman actor in the raid has a *full* group of 5 (not just some group). Querying every
        // fight in a full report x10 ability ranks each can be hundreds of requests, so this still
        // stops as soon as it safely can -- but a partial group (a member who just never landed in
        // range for a tracked buff yet) isn't good enough to stop on, since it would silently under-
        // count that shaman's eligible players. Fights are checked with the debug fight (if any)
        // first, since that's the one actually being analyzed, then longest-duration-first (more
        // time for a party buff to land). If every fight gets checked and some shaman still doesn't
        // have a full group, whatever was found is used as a best effort.
        private static async Task<Dictionary<int, int>> InferPartyGroupsIncrementally(RaidReport raidReport, string reportId, List<int> allReportFightIds, int? debugFightId, bool debugGear)
        {
            var shamanIds = raidReport.ActorsById.Keys
                .Where(id => raidReport.GetActorClass(id).Equals("Shaman", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var fightsToCheck = allReportFightIds
                .OrderByDescending(id => debugFightId.HasValue && id == debugFightId.Value)
                .ThenByDescending(id => raidReport.GetFight(id).EndTime - raidReport.GetFight(id).StartTime)
                .ToList();

            var accumulatedEvents = new List<EventRow>();
            var groupOf = new Dictionary<int, int>();
            int fightsChecked = 0;

            foreach (var fightId in fightsToCheck)
            {
                fightsChecked++;
                var abilityResults = await GetPartyBuffEventsForFight(raidReport, reportId, fightId);
                var processed = ProcessPartyBuffEvents(abilityResults.Select(r => (fightId, r.AbilityId, r.Roots)).ToList());
                accumulatedEvents.AddRange(processed.Values.SelectMany(e => e));

                groupOf = InferPartyGroups(raidReport, accumulatedEvents);

                int fullShamans = CountShamansWithFullGroup(raidReport, shamanIds, groupOf);
                if (shamanIds.Count > 0 && fullShamans == shamanIds.Count)
                {
                    if (debugGear) Console.WriteLine($"DEBUG: all {shamanIds.Count} shaman(s) have a full group of {FULL_PARTY_SIZE} after checking {fightsChecked}/{fightsToCheck.Count} fight(s) for party buffs -- stopping early.");
                    return groupOf;
                }
            }

            if (debugGear)
            {
                int fullShamans = CountShamansWithFullGroup(raidReport, shamanIds, groupOf);
                int anyGroupShamans = shamanIds.Count(id => groupOf.ContainsKey(id));
                Console.WriteLine($"DEBUG: checked all {fightsChecked} fight(s) for party buffs; {fullShamans}/{shamanIds.Count} shaman(s) have a full group of {FULL_PARTY_SIZE}, {anyGroupShamans}/{shamanIds.Count} have some group.");
            }

            return groupOf;
        }

        // Pets tag along with their owner's party (e.g. a hunter's pet catching Battle Shout too)
        // but don't take up one of the party's 5 player slots -- only real players count toward
        // "full".
        private static int CountShamansWithFullGroup(RaidReport raidReport, List<int> shamanIds, Dictionary<int, int> groupOf)
        {
            var clusterPlayerSizes = groupOf
                .Where(kv => raidReport.GetActorType(kv.Key) == "Player")
                .GroupBy(kv => kv.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            return shamanIds.Count(id => groupOf.TryGetValue(id, out var groupId) && clusterPlayerSizes.TryGetValue(groupId, out var size) && size == FULL_PARTY_SIZE);
        }

        // Infers 5-man party membership from party-restricted buffs (see PartyBuffAbilityIds):
        // when one of them applies to several players at the exact same timestamp, that's one
        // cast hitting up to 5 party members at once, so they get unioned together. Pets can tag
        // along in the same batch as their owner without counting against that cap. A batch that
        // hits more than 5 *player* targets can't be a single party-restricted cast (a party is
        // capped at 5) -- most likely two different casters' buffs landing on the same tick -- so
        // it's skipped rather than risking an incorrect merge.
        private static Dictionary<int, int> InferPartyGroups(RaidReport raidReport, List<EventRow> partyBuffEvents)
        {
            var unionFind = new UnionFind();

            var byAbilityAndTime = partyBuffEvents
                .Where(e => e.TargetID.HasValue && e.AbilityGameID.HasValue)
                .GroupBy(e => (e.AbilityGameID.Value, e.Timestamp));

            foreach (var batch in byAbilityAndTime)
            {
                var targets = batch.Select(e => e.TargetID.Value).Distinct().ToList();
                var playerTargetCount = targets.Count(id => raidReport.GetActorType(id) == "Player");
                if (playerTargetCount > 5)
                {
                    Console.WriteLine($"DEBUG: ability {batch.Key.Item1} hit {playerTargetCount} player targets at the same instant (> 5, a party's max size) -- skipping this batch for group inference.");
                    continue;
                }

                for (int i = 1; i < targets.Count; i++)
                {
                    unionFind.Union(targets[0], targets[i]);
                }
            }

            var groupOf = new Dictionary<int, int>();
            foreach (var actorId in partyBuffEvents.Where(e => e.TargetID.HasValue).Select(e => e.TargetID.Value).Distinct())
            {
                groupOf[actorId] = unionFind.GroupOf(actorId);
            }
            return groupOf;
        }

        private static void PrintInferredGroups(RaidReport raidReport, Dictionary<int, int> groupOf)
        {
            var clusters = groupOf.GroupBy(kv => kv.Value).OrderBy(g => g.Key);
            Console.WriteLine($"DEBUG: report-wide, inferred {clusters.Count()} party cluster(s) from Battle Shout/Trueshot Aura/Leader of the Pack/Blood Pact:");
            foreach (var cluster in clusters)
            {
                var members = cluster.Select(kv => $"{raidReport.GetActor(kv.Key)} ({raidReport.GetActorClass(kv.Key)})");
                Console.WriteLine($"    [{string.Join(", ", members)}]");
            }
        }

        // ----- Per-fight computation -----

        private static WindfuryFightResult ComputeWindfuryForFight(RaidReport raidReport, int fightId, List<EventRow> windfuryEvents, Dictionary<int, int> groupOf)
        {
            var fight = raidReport.GetFight(fightId);
            var fightResult = new WindfuryFightResult(fightId, fight.Name, fight.IsBossFight);

            long fightStart = fight.StartTime;
            long fightEnd = fight.EndTime;

            // Every shaman actor whose inferred party we could determine, keyed by that party's
            // group id (from InferPartyGroups). Ties (rare -- e.g. two enhancement shamans sharing
            // an inferred party) are broken deterministically by lowest actor id.
            var shamanByGroupId = raidReport.ActorsById.Keys
                .Where(id => raidReport.GetActorClass(id).Equals("Shaman", StringComparison.OrdinalIgnoreCase) && groupOf.ContainsKey(id))
                .GroupBy(id => groupOf[id])
                .ToDictionary(g => g.Key, g => g.Min());

            var eventsByTarget = windfuryEvents
                .Where(e => e.TargetID.HasValue)
                .GroupBy(e => e.TargetID.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Timestamp).ToList());

            bool debugGear = Environment.GetEnvironmentVariable("WF_DEBUG_GEAR") == "1";
            int consideredCount = 0, noGearCount = 0, tempEnchantCount = 0;

            foreach (var actorId in raidReport.ActorsById.Keys)
            {
                if (raidReport.GetActorType(actorId) != "Player") continue;
                if (!EligibleClasses.Contains(raidReport.GetActorClass(actorId))) continue;

                consideredCount++;

                var gearInfo = fight.GetCombatantGearInfo(actorId);
                if (gearInfo == null || !gearInfo.HasMainHandWeapon)
                {
                    noGearCount++;
                    Console.WriteLine($"DEBUG: skipping {raidReport.GetActor(actorId)} for fight {fightId} ({fight.Name}) -- no main-hand weapon data available");
                    continue;
                }
                bool hasDisqualifyingEnchant = gearInfo.MainHandHasTemporaryEnchant
                    && !TotemBuffEvent.WINDFURY_TOTEM_ENCHANT_IDS.Contains(gearInfo.MainHandTemporaryEnchantId.Value);

                if (hasDisqualifyingEnchant)
                {
                    tempEnchantCount++;
                    if (debugGear) Console.WriteLine($"DEBUG: {raidReport.GetActor(actorId)} ({raidReport.GetActorClass(actorId)}) ineligible for fight {fightId} -- {gearInfo}");
                    continue; // Sharpening Stone / Instant Poison / etc. -- not Windfury eligible this fight
                }
                else if (debugGear && gearInfo.MainHandHasTemporaryEnchant)
                {
                    Console.WriteLine($"DEBUG: {raidReport.GetActor(actorId)} ({raidReport.GetActorClass(actorId)}) already has Windfury (enchant {gearInfo.MainHandTemporaryEnchantId}) at pull for fight {fightId} -- eligible, not disqualifying");
                }

                var relevantEvents = eventsByTarget.TryGetValue(actorId, out var events) ? events : new List<EventRow>();

                var mergedIntervals = MergeWindfuryIntervals(fightId, fightStart, fightEnd, relevantEvents);

                int? shamanActorId = null;
                string shamanName = null;
                if (groupOf.TryGetValue(actorId, out var playerGroupId) && shamanByGroupId.TryGetValue(playerGroupId, out var shamanId))
                {
                    shamanActorId = shamanId;
                    shamanName = raidReport.GetActor(shamanId);
                }

                var playerResult = new WindfuryPlayerFightResult(
                    fightId,
                    actorId,
                    raidReport.GetActor(actorId),
                    shamanActorId,
                    shamanName,
                    mergedIntervals,
                    relevantEvents.Select(e => e.Timestamp).ToList(),
                    fightStart,
                    fightEnd - fightStart
                );

                fightResult.PlayerResults.Add(playerResult);
            }

            if (debugGear)
            {
                Console.WriteLine($"DEBUG: fight {fightId} ({fight.Name}) eligibility: {consideredCount} warrior/rogue actor(s) considered, {noGearCount} had no gear data, {tempEnchantCount} had a main-hand temp enchant, {fightResult.PlayerResults.Count} eligible.");
            }

            return fightResult;
        }

        // Classic merge-intervals: eligibility grants an assumed 10s of Windfury at pull (no way to
        // know if it was already active), and every applybuff/refreshbuff restarts a 10s window.
        private static List<TotemBuffEvent> MergeWindfuryIntervals(int fightId, long fightStart, long fightEnd, List<EventRow> relevantEvents)
        {
            var rawIntervals = new List<(long Start, long End)>
            {
                (fightStart, Math.Min(fightStart + TotemBuffEvent.WINDFURY_BUFF_DURATION_MS, fightEnd))
            };

            foreach (var evt in relevantEvents)
            {
                if (evt.Timestamp >= fightEnd) continue;

                long end = Math.Min(evt.Timestamp + TotemBuffEvent.WINDFURY_BUFF_DURATION_MS, fightEnd);
                rawIntervals.Add((evt.Timestamp, end));
            }

            rawIntervals.Sort((a, b) => a.Start.CompareTo(b.Start));

            var merged = new List<(long Start, long End)>();
            foreach (var interval in rawIntervals)
            {
                if (merged.Count > 0 && interval.Start <= merged[merged.Count - 1].End)
                {
                    var last = merged[merged.Count - 1];
                    merged[merged.Count - 1] = (last.Start, Math.Max(last.End, interval.End));
                }
                else
                {
                    merged.Add(interval);
                }
            }

            return merged.Select(m => new TotemBuffEvent(m.Start, m.End, fightId, TotemBuffEvent.WINDFURY_ABILITY_ID)).ToList();
        }

        // ----- Printing -----

        private static void PrintDebugInfo(List<WindfuryFightResult> fightResults, string playerNameFilter)
        {
            foreach (var fightResult in fightResults)
            {
                var fightType = fightResult.IsBossFight ? "Boss" : "Trash";
                Console.WriteLine($"\nFight {fightResult.FightId} ({fightResult.FightName}) [{fightType}]:");

                var byShaman = fightResult.PlayerResults
                    .Where(r => r.ShamanActorId.HasValue)
                    .GroupBy(r => (r.ShamanActorId.Value, r.ShamanName));

                bool printedAny = false;
                foreach (var shamanGroup in byShaman)
                {
                    if (playerNameFilter != null && !shamanGroup.Key.ShamanName.Equals(playerNameFilter, StringComparison.OrdinalIgnoreCase)) continue;

                    printedAny = true;
                    var members = shamanGroup.Select(r => r.PlayerName).ToList();
                    Console.WriteLine($"  {shamanGroup.Key.ShamanName}'s group: {string.Join(", ", members)}");
                    foreach (var playerResult in shamanGroup)
                    {
                        Console.WriteLine($"    {playerResult}");
                        // Only show the full interval-merge trace when narrowed to one shaman --
                        // otherwise this would dump a trace per player per fight for the whole raid.
                        if (playerNameFilter != null)
                        {
                            Console.WriteLine(playerResult.FormatDebugTrace());
                        }
                    }
                }

                if (playerNameFilter == null)
                {
                    var unattributed = fightResult.PlayerResults.Where(r => !r.ShamanActorId.HasValue).Select(r => r.PlayerName).ToList();
                    if (unattributed.Count > 0)
                    {
                        Console.WriteLine($"  Unattributed eligible players (never saw Windfury Totem applied): {string.Join(", ", unattributed)}");
                    }
                }
                else if (!printedAny)
                {
                    Console.WriteLine($"  (no group found for {playerNameFilter} this fight)");
                }
            }
        }

        private static void PrintSummary(List<WindfuryFightResult> fightResults, string playerNameFilter)
        {
            var allResults = fightResults.SelectMany(f => f.PlayerResults).Where(r => r.ShamanActorId.HasValue).ToList();
            var bossFightIds = new HashSet<int>(fightResults.Where(f => f.IsBossFight).Select(f => f.FightId));

            Console.WriteLine();
            Console.WriteLine("Windfury Uptime Summary");
            Console.WriteLine();

            var shamanGroups = allResults
                .GroupBy(r => (r.ShamanActorId.Value, r.ShamanName))
                .OrderBy(g => g.Key.ShamanName);

            foreach (var shamanGroup in shamanGroups)
            {
                if (playerNameFilter != null && !shamanGroup.Key.ShamanName.Equals(playerNameFilter, StringComparison.OrdinalIgnoreCase)) continue;

                PrintUptimeBucket(shamanGroup.Key.ShamanName, shamanGroup.ToList(), bossFightIds);
            }

            if (playerNameFilter == null)
            {
                Console.WriteLine();
                PrintUptimeBucket("Raid-wide (all shamans)", allResults, bossFightIds);
            }
        }

        private static void PrintUptimeBucket(string label, List<WindfuryPlayerFightResult> results, HashSet<int> bossFightIds)
        {
            var overall = ComputeTimeWeightedUptime(results);
            var boss = ComputeTimeWeightedUptime(results.Where(r => bossFightIds.Contains(r.FightId)).ToList());
            var trash = ComputeTimeWeightedUptime(results.Where(r => !bossFightIds.Contains(r.FightId)).ToList());

            Console.WriteLine($"{label}: Overall {overall:0.#}%, Boss {boss:0.#}%, Trash {trash:0.#}%");
        }

        // Time-weighted (total covered ms / total eligible ms) rather than an average of per-fight
        // percentages, so a handful of short fights can't skew the number.
        private static double ComputeTimeWeightedUptime(List<WindfuryPlayerFightResult> results)
        {
            if (results.Count == 0) return 0;

            long coveredMs = results.Sum(r => r.CoveredMs);
            long totalMs = results.Sum(r => r.FightDurationMs);
            return totalMs == 0 ? 0 : (100.0 * coveredMs / totalMs);
        }
    }
}
