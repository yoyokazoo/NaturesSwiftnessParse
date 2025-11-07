using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace NaturesSwiftnessParse
{
    public static class NaturesSwiftnessParse
    {
        public static async Task RunNaturesSwiftnessReport(List<string> reportIds, int? debugFightId, int eventsToPrint, string clientId, string clientSecret)
        {
            // TODO: take and handle multiple IDs in case we have split raids that we want a single report for
            string reportId = reportIds.First();
            Console.WriteLine($"Running Report for {reportId} (Only a single reportId is supported for now)");

            WarcraftLogsQuery.LoadClientIdAndSecret(clientId, clientSecret);

            // Get overarching report with fights and actors
            var reportDataResult = await GetFightsAndActors(reportId);
            RaidReport raidReport = ProcessFightsAndActors(reportId, reportDataResult);

            // If we're debugging a single fight, only fetch that one
            var allFightIds = debugFightId.HasValue ? new List<int> { debugFightId.Value } : raidReport.Fights.Keys.ToList();

            // Get Natures Swiftnesses for whole raid
            var nsRootResults = await GetNaturesSwiftnessEvents(raidReport, reportId, allFightIds);
            ProcessNaturesSwiftnessEvents(raidReport, nsRootResults);

            // For each fight, grab the heal events and damage events
            var healRootResults = await GetHealingEvents(raidReport, reportId, allFightIds);
            ProcessHealingEvents(raidReport, healRootResults);

            var damageRootResults = await GetDamageEvents(raidReport, reportId, allFightIds);
            ProcessDamageEvents(raidReport, damageRootResults);

            raidReport.LinkNaturesSwiftnessesAndHeals();

            raidReport.PrintMostCriticalNaturesSwiftnesses(eventsToPrint);
        }

        private static void ProcessNaturesSwiftnessEvents(RaidReport raidReport, List<ReportDataRoot>[] nsRootResults)
        {
            foreach (var rootResult in nsRootResults)
            {
                foreach (var root in rootResult)
                {
                    ProcessNaturesSwiftnessEvent(raidReport, root);
                }
            }
        }

        private static void ProcessNaturesSwiftnessEvent(RaidReport raidReport, ReportDataRoot nsRoot)
        {
            foreach (var ns in nsRoot.Data.ReportData.Report.Events.Data)
            {
                var sourceName = raidReport.GetActor(ns.SourceID.Value);
                var nsEvent = new NaturesSwiftnessEvent(sourceName, ns.Timestamp, ns.Fight.Value);
                raidReport.AddNaturesSwiftnessEvent(nsEvent);
            }
        }

        private static void ProcessHealingEvents(RaidReport raidReport, List<ReportDataRoot>[] healRootResults)
        {
            foreach (var rootResult in healRootResults)
            {
                foreach (var root in rootResult)
                {
                    ProcessHealingEvent(raidReport, root);
                }
            }
        }

        private static void ProcessHealingEvent(RaidReport raidReport, ReportDataRoot healRoot)
        {
            foreach (var heal in healRoot.Data.ReportData.Report.Events.Data)
            {
                if (heal.Type != "heal") continue; // ignore absorbs from protection potions

                string sourceName = raidReport.GetActor(heal.SourceID.Value);
                string targetName = raidReport.GetActor(heal.TargetID.Value);
                int healAmount = heal.Amount.Value;
                int overheal = heal.Extra.ContainsKey("overheal") ? heal.Extra["overheal"].GetInt32() : 0;
                bool critical = heal.Extra["hitType"].GetInt32() == 2; // 1 is normal, 2 is critical
                bool hotTick = heal.Extra.ContainsKey("tick");
                string abilityName = raidReport.GetAbility(heal.AbilityGameID.Value);

                var healEvent = new HealEvent(heal.Timestamp, healAmount, overheal, sourceName, targetName, abilityName, critical, hotTick);
                raidReport.GetFight(heal.Fight.Value).AddHealEvent(healEvent);

                // Also log heal events as HP events if they're non-zero
                if (healAmount > 0)
                {
                    HealthPointEvent hpEvent = new HealthPointEvent(heal.Timestamp, -1 * healAmount, heal.HitPoints.Value, targetName, heal.TargetID.Value);
                    raidReport.GetFight(heal.Fight.Value).AddHealthPointEvent(hpEvent);
                }
            }
        }

        private static void ProcessDamageEvents(RaidReport raidReport, List<ReportDataRoot>[] damageRootResults)
        {
            foreach (var rootResult in damageRootResults)
            {
                foreach (var root in rootResult)
                {
                    ProcessDamageEvent(raidReport, root);
                }
            }
        }
        private static void ProcessDamageEvent(RaidReport raidReport, ReportDataRoot damageRoot)
        {
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
        }
        private static RaidReport ProcessFightsAndActors(string reportId, ReportDataRoot reportDataRoot)
        {
            var raidReport = new RaidReport(reportId, reportDataRoot.Data.ReportData.Report.Title);
            foreach (var fight in reportDataRoot.Data.ReportData.Report.Fights)
            {
                var fightReport = new FightReport(fight.Id, fight.Name, fight.StartTime, fight.EndTime);
                raidReport.AddFight(fightReport);
            }
            foreach (var actor in reportDataRoot.Data.ReportData.Report.MasterData.Actors)
            {
                raidReport.AddActor(actor.Id, actor.Name);
            }
            return raidReport;
        }

        private static async Task<ReportDataRoot> GetFightsAndActors(string reportId)
        {
            var reportJson = await WarcraftLogsQuery.QueryForReport(reportId);
            return JsonSerializer.Deserialize<ReportDataRoot>(reportJson);
        }

        private static async Task<List<ReportDataRoot>[]> GetNaturesSwiftnessEvents(RaidReport raidReport, string reportId, List<int> allFightIds)
        {
            List<Task<List<ReportDataRoot>>> nsRoots = new List<Task<List<ReportDataRoot>>>();
            List<int> naturesSwiftnessAbilityIDs = new List<int> { NaturesSwiftnessEvent.SHAMAN_NS_ABILITY_ID, NaturesSwiftnessEvent.DRUID_NS_ABILITY_ID };

            foreach (var abilityId in naturesSwiftnessAbilityIDs)
            {
                nsRoots.Add(GetNaturesSwiftnessEvent(raidReport, reportId, allFightIds, abilityId));
            }

            return await Task.WhenAll(nsRoots);
        }

        private static async Task<List<ReportDataRoot>> GetNaturesSwiftnessEvent(RaidReport raidReport, string reportId, List<int> allFightIds, int abilityId)
        {
            List<ReportDataRoot> nsRoots = new List<ReportDataRoot>();

            var nsJson = await WarcraftLogsQuery.QueryForAbilityCastEvents(reportId, allFightIds, abilityId);
            var nsRoot = JsonSerializer.Deserialize<ReportDataRoot>(nsJson);

            nsRoots.Add(nsRoot);

            return nsRoots;
        }

        private static async Task<List<ReportDataRoot>[]> GetHealingEvents(RaidReport raidReport, string reportId, List<int> allFightIds)
        {
            List<Task<List<ReportDataRoot>>> healRoots = new List<Task<List<ReportDataRoot>>>();

            foreach (var fightId in allFightIds)
            {
                healRoots.Add(GetHealingEventsForFight(raidReport, reportId, fightId));
            }

            return await Task.WhenAll(healRoots);
        }

        private static async Task<List<ReportDataRoot>> GetHealingEventsForFight(RaidReport raidReport, string reportId, int fightId)
        {
            long nextPageTimestamp = 0;
            var endTime = raidReport.GetFight(fightId).EndTime;
            List<ReportDataRoot> healRoots = new List<ReportDataRoot>();

            do
            {
                var healJson = await WarcraftLogsQuery.QueryForHealingEvents(reportId, fightId, nextPageTimestamp, endTime);
                var healRoot = JsonSerializer.Deserialize<ReportDataRoot>(healJson);
                healRoots.Add(healRoot);
                nextPageTimestamp = healRoot.Data.ReportData.Report.Events.NextPageTimestamp ?? 0;
            }
            while (nextPageTimestamp != 0);

            return healRoots;
        }

        private static async Task<List<ReportDataRoot>[]> GetDamageEvents(RaidReport raidReport, string reportId, List<int> allFightIds)
        {
            List<Task<List<ReportDataRoot>>> damageRoots = new List<Task<List<ReportDataRoot>>>();

            foreach (var fightId in allFightIds)
            {
                damageRoots.Add(GetDamageEventsForFight(raidReport, reportId, fightId));
            }

            return await Task.WhenAll(damageRoots);
        }

        private static async Task<List<ReportDataRoot>> GetDamageEventsForFight(RaidReport raidReport, string reportId, int fightId)
        {
            long nextPageTimestamp = 0;
            var endTime = raidReport.GetFight(fightId).EndTime;
            List<ReportDataRoot> damageRoots = new List<ReportDataRoot>();

            do
            {
                // For each fight, grab the damage taken
                var damageJson = await WarcraftLogsQuery.QueryForDamageTaken(reportId, fightId, nextPageTimestamp, endTime);
                var damageRoot = JsonSerializer.Deserialize<ReportDataRoot>(damageJson);
                damageRoots.Add(damageRoot);
                nextPageTimestamp = damageRoot.Data.ReportData.Report.Events.NextPageTimestamp ?? 0;
            } while (nextPageTimestamp != 0);

            return damageRoots;
        }
    }
}
