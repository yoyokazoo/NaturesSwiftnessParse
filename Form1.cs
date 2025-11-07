using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.Json;

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
        }

        static async Task RunNaturesSwiftnessReport(List<string> reportIds, int? debugFightId)
        {
            Console.WriteLine($"Running Report for {string.Join(",", reportIds)} (Only a single reportId is supported for now)");
            //var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            
            // TODO: take and handle multiple IDs in case we have split raids that we want a single report for
            string reportId = reportIds.First();

            // Get overarching report with fights and actors
            var reportJson = await WarcraftLogsQuery.QueryForReport(reportId);
            var root = JsonSerializer.Deserialize<ReportDataRoot>(reportJson);

            var raidReport = new RaidReport(reportId, root.Data.ReportData.Report.Title);
            foreach(var fight in root.Data.ReportData.Report.Fights)
            {
                var fightReport = new FightReport(fight.Id, fight.Name, fight.StartTime, fight.EndTime);
                raidReport.AddFight(fightReport);
            }
            foreach (var actor in root.Data.ReportData.Report.MasterData.Actors)
            {
                raidReport.AddActor(actor.Id, actor.Name);
            }

            //raidReport.PrintFights();
            //raidReport.PrintActors();

            var allFightIds = raidReport.Fights.Keys.ToList();
            if (debugFightId.HasValue)
            {
                allFightIds = new List<int> { debugFightId.Value };
            }

            // Get Natures Swiftnesses for whole raid
            List<int> naturesSwiftnessAbilityIDs = new List<int>{ NaturesSwiftnessEvent.SHAMAN_NS_ABILITY_ID, NaturesSwiftnessEvent.DRUID_NS_ABILITY_ID };
            foreach (var abilityId in naturesSwiftnessAbilityIDs)
            {
                var nsJson = await WarcraftLogsQuery.QueryForAbilityCastEvents(reportId, allFightIds, abilityId);
                var nsRoot = JsonSerializer.Deserialize<ReportDataRoot>(nsJson);
                foreach (var ns in nsRoot.Data.ReportData.Report.Events.Data)
                {
                    var sourceName = raidReport.GetActor(ns.SourceID.Value);
                    var nsEvent = new NaturesSwiftnessEvent(sourceName, ns.Timestamp, ns.Fight.Value);
                    raidReport.AddNaturesSwiftnessEvent(nsEvent);
                }
            }

            //raidReport.PrintNaturesSwiftnessEvents();

            // For each fight, grab the heal events and damage events
            foreach (var fightId in allFightIds)
            {
                var healRoots = await GetHealingEventsForFight(raidReport, reportId, fightId);
                foreach (var healRoot in healRoots)
                {
                    ProcessHealingEvents(raidReport, healRoot);
                }

                var damageRoots = await GetDamageEventsForFight(raidReport, reportId, fightId);
                foreach (var damageRoot in damageRoots)
                {
                    ProcessDamageEvents(raidReport, damageRoot);
                }
            }

            // now that we've inserted both, sort the hp timelines
            foreach (var fightId in allFightIds)
            {
                
                foreach (var hpTimeline in raidReport.GetFight(fightId).HealthPointTimelines.Values)
                {
                    hpTimeline.SortByTime();
                    //hpTimeline.Print();
                }
            }

            // asdf

            foreach (var nsEvent in raidReport.NaturesSwiftnessEvents)
            {
                // Link the ns to the heal
                FightReport fight = raidReport.GetFight(nsEvent.FightId);
                HealTimeline healTimeline = fight.GetHealTimeline(nsEvent.CasterName);
                foreach(HealEvent heal in healTimeline.Events)
                {
                    if (heal.Time < nsEvent.Time) continue;
                    if (heal.HotTick) continue;

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

        private static void ProcessHealingEvents(RaidReport raidReport, ReportDataRoot healRoot)
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

        private static void ProcessDamageEvents(RaidReport raidReport, ReportDataRoot damageRoot)
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
