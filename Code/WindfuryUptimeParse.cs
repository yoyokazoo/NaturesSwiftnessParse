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

            // If we're debugging a single fight, only fetch Windfury events for that one -- but
            // CombatantInfo and party-buff data are always fetched report-wide (see below),
            // regardless of debugFightId.
            var allFightIds = debugFightId.HasValue ? new List<int> { debugFightId.Value } : allReportFightIds;

            // CombatantInfo (gear/enchant snapshots) only exists for real boss pulls -- WCL never
            // captures it for trash. Querying it for every boss fight in the report (a small subset
            // of all fights) rather than just the debug fight lets ComputeWindfuryForFight fall back
            // to a nearby boss pull's snapshot for fights (trash, or a boss with no snapshot of its
            // own) that don't have one -- see GetNearestGearInfo.
            var bossFightIds = allReportFightIds.Where(id => raidReport.GetFight(id).IsBossFight).ToList();
            var combatantInfoResults = await GetCombatantInfo(raidReport, reportId, bossFightIds);
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

        // CombatantInfo is only ever snapshotted at boss pulls, so a trash fight (or occasionally a
        // boss fight with no snapshot of its own) has to borrow the closest-in-time boss pull's gear
        // info for a given actor instead. Returns null if that actor has no snapshot anywhere in the
        // whole report.
        private static CombatantGearInfo GetNearestGearInfo(RaidReport raidReport, int actorId, FightReport targetFight)
        {
            CombatantGearInfo nearest = null;
            long nearestDistance = long.MaxValue;

            foreach (var fight in raidReport.Fights.Values)
            {
                var gearInfo = fight.GetCombatantGearInfo(actorId);
                if (gearInfo == null) continue;

                long distance = Math.Abs(fight.StartTime - targetFight.StartTime);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = gearInfo;
                }
            }

            return nearest;
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
        // when one of them applies to several players at the exact same timestamp *from the same
        // caster*, that's one cast hitting up to 5 party members at once.
        //
        // This used to be a blind, order-independent union-find over all such batches report-wide
        // -- but confirmed against a real report (x1nhpR2mrYzcwMk7), that eventually welds every
        // real party in the raid into one blob: it only takes one batch, anywhere in the whole
        // night, that happens to touch two already-known-distinct real parties (e.g. after a raid
        // regroup, or simply because someone drifted into another party's buff range) for their
        // *entire* histories to be merged forever, since a union-find can't un-merge.
        //
        // Instead, each player has at most one *current* group, established and updated as
        // evidence arrives in chronological (Timestamp) order -- a party we've inferred is treated
        // as true until a later batch clearly proves it stale, rather than every batch being
        // treated as equally authoritative regardless of when it happened:
        //   - A batch touching no already-grouped player starts a brand new group.
        //   - A batch touching players from exactly one existing group extends that group with any
        //     ungrouped targets -- the common case, for a party that's stayed together.
        //   - A batch touching players from two or more existing groups means someone's tracked
        //     group is stale: whichever existing group has the most of this batch's players wins,
        //     and every other batch member (whether previously untracked or tagged to a losing
        //     group) is (re)assigned to it. A tie has no majority to trust, so it's skipped rather
        //     than guessing.
        // It's not required (or expected) that every player lands in a group from the very first
        // fight that mentions them -- groups just keep accreting/correcting as more fights are
        // processed; whatever's been inferred so far is used as-is by the caller.
        //
        // A batch that hits more than 5 *player* targets can't be a single party-restricted cast (a
        // party is capped at 5), so it's skipped up front rather than feeding it into the above at
        // all. Pets can tag along in a batch without counting against that cap.
        private static Dictionary<int, int> InferPartyGroups(RaidReport raidReport, List<EventRow> partyBuffEvents)
        {
            bool traceUnions = Environment.GetEnvironmentVariable("WF_DEBUG_UNION") == "1";

            var groupOf = new Dictionary<int, int>();
            int nextGroupId = 0;

            // OrderBy is a stable sort and GroupBy preserves first-encountered key order over an
            // already-ordered source, so batches below are visited in ascending Timestamp order --
            // i.e. chronologically, regardless of what order the caller accumulated fights in.
            var byAbilityTimeAndSource = partyBuffEvents
                .Where(e => e.TargetID.HasValue && e.AbilityGameID.HasValue && e.SourceID.HasValue)
                .OrderBy(e => e.Timestamp)
                .GroupBy(e => (e.AbilityGameID.Value, e.Timestamp, e.SourceID.Value));

            foreach (var batch in byAbilityTimeAndSource)
            {
                var targets = batch.Select(e => e.TargetID.Value).Distinct().ToList();
                var playerTargetCount = targets.Count(id => raidReport.GetActorType(id) == "Player");
                if (playerTargetCount > 5)
                {
                    Console.WriteLine($"DEBUG: ability {batch.Key.Item1} cast by source {batch.Key.Item3} hit {playerTargetCount} player targets at the same instant (> 5, a party's max size) -- skipping this batch for group inference.");
                    continue;
                }

                // Tally how many of this batch's targets are already tagged to each existing group.
                var groupCounts = new Dictionary<int, int>();
                foreach (var id in targets)
                {
                    if (groupOf.TryGetValue(id, out var g))
                    {
                        groupCounts[g] = groupCounts.TryGetValue(g, out var c) ? c + 1 : 1;
                    }
                }

                int winningGroup;
                if (groupCounts.Count == 0)
                {
                    winningGroup = nextGroupId++;
                }
                else if (groupCounts.Count == 1)
                {
                    winningGroup = groupCounts.Keys.First();
                }
                else
                {
                    var ranked = groupCounts.OrderByDescending(kv => kv.Value).ToList();
                    if (ranked[0].Value == ranked[1].Value)
                    {
                        if (traceUnions)
                        {
                            var sourceName = raidReport.GetActor(batch.Key.Item3);
                            Console.WriteLine($"UNIONTRACE: ability={batch.Key.Item1} ts={batch.Key.Item2} source={sourceName}({batch.Key.Item3}) -- ambiguous batch ties between groups [{string.Join(", ", ranked.Select(kv => kv.Key))}], skipping.");
                        }
                        continue;
                    }
                    winningGroup = ranked[0].Key;
                }

                // The per-batch instantaneous check above (>5 targets at once) only catches an
                // impossible SINGLE cast -- it doesn't stop a group from growing past 5 real
                // members one batch at a time as sticky expansions and majority-wins accumulate
                // (confirmed against report x1nhpR2mrYzcwMk7: this is exactly how a real shaman's
                // own party -- Melior's -- silently absorbed into another shaman's group despite
                // never once losing a single-batch majority vote by more than a member or two).
                // So before committing, check what the winning group's real-player membership
                // would become if this batch's targets join it; a party is capped at 5, so a
                // projected overflow means this batch's evidence conflicts with the group as
                // currently understood -- skip it rather than let the group overflow.
                var projectedMembers = new HashSet<int>(groupOf.Where(kv => kv.Value == winningGroup).Select(kv => kv.Key));
                foreach (var id in targets) projectedMembers.Add(id);
                int projectedRealSize = projectedMembers.Count(id => raidReport.GetActorType(id) == "Player");
                if (projectedRealSize > FULL_PARTY_SIZE)
                {
                    if (traceUnions)
                    {
                        var sourceName = raidReport.GetActor(batch.Key.Item3);
                        Console.WriteLine($"UNIONTRACE: ability={batch.Key.Item1} ts={batch.Key.Item2} source={sourceName}({batch.Key.Item3}) -- joining group {winningGroup} would grow it to {projectedRealSize} real player(s) (> {FULL_PARTY_SIZE}), skipping.");
                    }
                    continue;
                }

                foreach (var id in targets)
                {
                    if (traceUnions && groupOf.TryGetValue(id, out var previousGroup) && previousGroup != winningGroup)
                    {
                        Console.WriteLine($"UNIONTRACE: ability={batch.Key.Item1} ts={batch.Key.Item2} source={raidReport.GetActor(batch.Key.Item3)}({batch.Key.Item3}) -- reassigning {raidReport.GetActor(id)} from group {previousGroup} to {winningGroup} (contradicting evidence)");
                    }
                    groupOf[id] = winningGroup;
                }
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

            (int? ShamanActorId, string ShamanName) AttributeShamanForActor(int actorId)
            {
                if (groupOf.TryGetValue(actorId, out var groupId) && shamanByGroupId.TryGetValue(groupId, out var shamanId))
                {
                    return (shamanId, raidReport.GetActor(shamanId));
                }
                return (null, null);
            }

            var eventsByTarget = windfuryEvents
                .Where(e => e.TargetID.HasValue)
                .GroupBy(e => e.TargetID.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Timestamp).ToList());

            bool debugGear = Environment.GetEnvironmentVariable("WF_DEBUG_GEAR") == "1";
            int consideredCount = 0, noWeaponCount = 0, assumedEligibleCount = 0, tempEnchantCount = 0;

            foreach (var actorId in raidReport.ActorsById.Keys)
            {
                if (raidReport.GetActorType(actorId) != "Player") continue;
                if (!EligibleClasses.Contains(raidReport.GetActorClass(actorId))) continue;

                consideredCount++;

                // CombatantInfo only exists for boss pulls (never trash), so this fight might have no
                // snapshot of its own -- fall back to the nearest boss pull that does have one for
                // this actor. Gear/enchants rarely change mid-raid-night.
                var gearInfo = fight.GetCombatantGearInfo(actorId) ?? GetNearestGearInfo(raidReport, actorId, fight);

                if (gearInfo == null)
                {
                    // No snapshot anywhere in the whole report for this actor -- can't confirm
                    // eligibility either way. Assume eligible rather than excluding them: a real
                    // disqualifying temp enchant has turned out to be rare/nonexistent in practice
                    // once Windfury's own enchant markers are excluded (see WINDFURY_TOTEM_ENCHANT_IDS),
                    // while unconditionally excluding would zero out every trash fight, since WCL
                    // never snapshots those at all.
                    assumedEligibleCount++;
                    if (debugGear) Console.WriteLine($"DEBUG: no main-hand weapon data anywhere in the report for {raidReport.GetActor(actorId)} (fight {fightId}, {fight.Name}) -- assuming eligible");
                }
                else if (!gearInfo.HasMainHandWeapon)
                {
                    // A real snapshot exists (this fight's own, or the nearest boss pull's) and it
                    // explicitly shows no main-hand weapon -- that's a real signal, not missing data.
                    noWeaponCount++;
                    if (debugGear) Console.WriteLine($"DEBUG: skipping {raidReport.GetActor(actorId)} for fight {fightId} ({fight.Name}) -- nearest known snapshot shows no main-hand weapon");
                    continue;
                }
                else
                {
                    bool hasDisqualifyingEnchant = gearInfo.MainHandHasTemporaryEnchant
                        && !TotemBuffEvent.WINDFURY_TOTEM_ENCHANT_IDS.Contains(gearInfo.MainHandTemporaryEnchantId.Value);

                    if (hasDisqualifyingEnchant)
                    {
                        tempEnchantCount++;
                        var (dqShamanId, dqShamanName) = AttributeShamanForActor(actorId);
                        fightResult.DisqualifiedPlayers.Add(new WindfuryDisqualifiedPlayer(
                            actorId, raidReport.GetActor(actorId), dqShamanId, dqShamanName,
                            gearInfo.MainHandTemporaryEnchantId.Value, gearInfo.FightId));
                        if (debugGear) Console.WriteLine($"DEBUG: {raidReport.GetActor(actorId)} ({raidReport.GetActorClass(actorId)}) ineligible for fight {fightId} -- {gearInfo}");
                        continue; // Sharpening Stone / Instant Poison / etc. -- not Windfury eligible this fight
                    }
                    else if (debugGear && gearInfo.MainHandHasTemporaryEnchant)
                    {
                        Console.WriteLine($"DEBUG: {raidReport.GetActor(actorId)} ({raidReport.GetActorClass(actorId)}) already has Windfury (enchant {gearInfo.MainHandTemporaryEnchantId}) at pull for fight {fightId} -- eligible, not disqualifying");
                    }
                }

                var relevantEvents = eventsByTarget.TryGetValue(actorId, out var events) ? events : new List<EventRow>();

                var mergedIntervals = MergeWindfuryIntervals(fightId, fightStart, fightEnd, relevantEvents);

                var (shamanActorId, shamanName) = AttributeShamanForActor(actorId);

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

            // "Maximum uptime" per shaman-group: union each eligible attributed player's own merged
            // intervals together, so a moment counts as covered as long as ANY of them had Windfury
            // -- players can drift in and out of totem range independently, so the group as a whole
            // can be covered even when every individual's own uptime looks partial.
            foreach (var shamanGroup in fightResult.PlayerResults.Where(r => r.ShamanActorId.HasValue).GroupBy(r => (r.ShamanActorId.Value, r.ShamanName)))
            {
                var rawIntervals = shamanGroup.SelectMany(r => r.MergedIntervals.Select(iv => (iv.StartTime, iv.EndTime))).ToList();
                var unionIntervals = MergeIntervals(rawIntervals)
                    .Select(m => new TotemBuffEvent(m.Start, m.End, fightId, TotemBuffEvent.WINDFURY_ABILITY_ID))
                    .ToList();

                fightResult.GroupResults.Add(new WindfuryGroupFightResult(
                    fightId, shamanGroup.Key.Item1, shamanGroup.Key.ShamanName, unionIntervals, shamanGroup.Count(), fightEnd - fightStart));
            }

            if (debugGear)
            {
                Console.WriteLine($"DEBUG: fight {fightId} ({fight.Name}) eligibility: {consideredCount} warrior/rogue actor(s) considered, {noWeaponCount} had no main-hand weapon, {assumedEligibleCount} had no snapshot anywhere (assumed eligible), {tempEnchantCount} had a main-hand temp enchant, {fightResult.PlayerResults.Count} eligible.");
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

            return MergeIntervals(rawIntervals)
                .Select(m => new TotemBuffEvent(m.Start, m.End, fightId, TotemBuffEvent.WINDFURY_ABILITY_ID))
                .ToList();
        }

        // Classic merge-intervals over arbitrary (start, end) pairs, not necessarily sorted or
        // non-overlapping going in. Shared by both per-player interval merging and the per-group
        // union merge above.
        private static List<(long Start, long End)> MergeIntervals(List<(long Start, long End)> rawIntervals)
        {
            var sorted = rawIntervals.OrderBy(i => i.Start).ToList();

            var merged = new List<(long Start, long End)>();
            foreach (var interval in sorted)
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

            return merged;
        }

        // ----- Printing -----

        private static void PrintDebugInfo(List<WindfuryFightResult> fightResults, string playerNameFilter)
        {
            foreach (var fightResult in fightResults)
            {
                var fightType = fightResult.IsBossFight ? "Boss" : "Trash";
                Console.WriteLine($"\nFight {fightResult.FightId} ({fightResult.FightName}) [{fightType}]:");

                var eligibleByShaman = fightResult.PlayerResults
                    .Where(r => r.ShamanActorId.HasValue)
                    .GroupBy(r => (r.ShamanActorId.Value, r.ShamanName))
                    .ToDictionary(g => g.Key, g => g.ToList());
                var disqualifiedByShaman = fightResult.DisqualifiedPlayers
                    .Where(d => d.ShamanActorId.HasValue)
                    .GroupBy(d => (d.ShamanActorId.Value, d.ShamanName))
                    .ToDictionary(g => g.Key, g => g.ToList());

                var allShamanKeys = eligibleByShaman.Keys.Union(disqualifiedByShaman.Keys).OrderBy(k => k.ShamanName);

                bool printedAny = false;
                foreach (var shamanKey in allShamanKeys)
                {
                    if (playerNameFilter != null && !shamanKey.ShamanName.Equals(playerNameFilter, StringComparison.OrdinalIgnoreCase)) continue;

                    printedAny = true;
                    eligibleByShaman.TryGetValue(shamanKey, out var eligibleMembers);
                    eligibleMembers = eligibleMembers ?? new List<WindfuryPlayerFightResult>();

                    var members = eligibleMembers.Select(r => r.PlayerName).ToList();
                    Console.WriteLine($"  {shamanKey.ShamanName}'s group: {string.Join(", ", members)}");
                    foreach (var playerResult in eligibleMembers)
                    {
                        Console.WriteLine($"    {playerResult}");
                        // Only show the full interval-merge trace when narrowed to one shaman --
                        // otherwise this would dump a trace per player per fight for the whole raid.
                        if (playerNameFilter != null)
                        {
                            Console.WriteLine(playerResult.FormatDebugTrace());
                        }
                    }

                    // Otherwise-eligible-class group members excluded this fight because their
                    // main-hand weapon had a real (non-Windfury) temporary enchant -- called out by
                    // player, fight, and enchant id so the exclusion is visible, not silent.
                    if (disqualifiedByShaman.TryGetValue(shamanKey, out var disqualified))
                    {
                        foreach (var dq in disqualified)
                        {
                            Console.WriteLine($"    (excluded, possible Sharpening Stone/Poison) {dq}");
                        }
                    }

                    var groupResult = fightResult.GroupResults.FirstOrDefault(g => (g.ShamanActorId, g.ShamanName) == shamanKey);
                    if (groupResult != null)
                    {
                        Console.WriteLine($"    {groupResult}");
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
            var allGroupResults = fightResults.SelectMany(f => f.GroupResults).ToList();
            var bossFightIds = new HashSet<int>(fightResults.Where(f => f.IsBossFight).Select(f => f.FightId));

            Console.WriteLine();
            Console.WriteLine("Windfury Uptime Summary");
            Console.WriteLine();

            var shamanNamesById = new Dictionary<int, string>();
            foreach (var r in allResults) shamanNamesById[r.ShamanActorId.Value] = r.ShamanName;
            foreach (var g in allGroupResults) shamanNamesById[g.ShamanActorId] = g.ShamanName;

            foreach (var shamanId in shamanNamesById.Keys.OrderBy(id => shamanNamesById[id]))
            {
                var shamanName = shamanNamesById[shamanId];
                if (playerNameFilter != null && !shamanName.Equals(playerNameFilter, StringComparison.OrdinalIgnoreCase)) continue;

                var shamanResults = allResults.Where(r => r.ShamanActorId.Value == shamanId).ToList();
                var shamanGroupResults = allGroupResults.Where(g => g.ShamanActorId == shamanId).ToList();
                PrintUptimeBucket(shamanName, shamanResults, shamanGroupResults, bossFightIds);
            }

            if (playerNameFilter == null)
            {
                Console.WriteLine();
                PrintUptimeBucket("Raid-wide (all shamans)", allResults, allGroupResults, bossFightIds);
            }
        }

        private static void PrintUptimeBucket(string label, List<WindfuryPlayerFightResult> results, List<WindfuryGroupFightResult> groupResults, HashSet<int> bossFightIds)
        {
            var overall = ComputeTimeWeightedUptime(results);
            var boss = ComputeTimeWeightedUptime(results.Where(r => bossFightIds.Contains(r.FightId)).ToList());
            var trash = ComputeTimeWeightedUptime(results.Where(r => !bossFightIds.Contains(r.FightId)).ToList());
            Console.WriteLine($"{label}: Overall {overall:0.#}%, Boss {boss:0.#}%, Trash {trash:0.#}%");

            // Max uptime: union of the group's members' coverage per fight, so groups of different
            // sizes count equally (one data point per fight, not one per player-fight).
            var maxOverall = ComputeTimeWeightedMaxUptime(groupResults);
            var maxBoss = ComputeTimeWeightedMaxUptime(groupResults.Where(g => bossFightIds.Contains(g.FightId)).ToList());
            var maxTrash = ComputeTimeWeightedMaxUptime(groupResults.Where(g => !bossFightIds.Contains(g.FightId)).ToList());
            Console.WriteLine($"{label} (max, any group member covered): Overall {maxOverall:0.#}%, Boss {maxBoss:0.#}%, Trash {maxTrash:0.#}%");
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

        // Same time-weighted formula as ComputeTimeWeightedUptime, but over one entry per (shaman,
        // fight) instead of one per (shaman, player, fight) -- since WindfuryGroupFightResult is
        // already one value per group per fight, this naturally normalizes by fight count rather
        // than by how many eligible players happened to be in each group.
        private static double ComputeTimeWeightedMaxUptime(List<WindfuryGroupFightResult> results)
        {
            if (results.Count == 0) return 0;

            long coveredMs = results.Sum(r => r.CoveredMs);
            long totalMs = results.Sum(r => r.FightDurationMs);
            return totalMs == 0 ? 0 : (100.0 * coveredMs / totalMs);
        }
    }
}
