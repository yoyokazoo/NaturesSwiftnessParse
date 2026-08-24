using System.Collections.Generic;

namespace NaturesSwiftnessParse
{
    public class FightReport
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public int StartTime { get; private set; }
        public int EndTime { get; private set; }
        public int EncounterId { get; private set; }
        // 0 encounterID means trash; any named encounter is a boss fight
        public bool IsBossFight => EncounterId != 0;
        public Dictionary<string, HealthPointTimeline> HealthPointTimelines { get; private set; }
        public Dictionary<string, HealTimeline> HealTimelines { get; private set; }
        public Dictionary<int, CombatantGearInfo> CombatantGearByActorId { get; private set; }

        public FightReport(int id, string name, int startTime, int endTime, int encounterId = 0)
        {
            Id = id;
            Name = name;
            HealthPointTimelines = new Dictionary<string, HealthPointTimeline>();
            HealTimelines = new Dictionary<string, HealTimeline>();
            CombatantGearByActorId = new Dictionary<int, CombatantGearInfo>();
            StartTime = startTime;
            EndTime = endTime;
            EncounterId = encounterId;
        }

        // CombatantGearByActorId
        public void AddCombatantGearInfo(CombatantGearInfo gearInfo)
        {
            CombatantGearByActorId[gearInfo.ActorId] = gearInfo;
        }

        public CombatantGearInfo GetCombatantGearInfo(int actorId)
        {
            return CombatantGearByActorId.TryGetValue(actorId, out var gearInfo) ? gearInfo : null;
        }

        // HealthPointTimelines
        public void AddHealthPointEvents(List<HealthPointEvent> healthPointEvents)
        {
            foreach (var healthPointEvent in healthPointEvents)
            {
                AddHealthPointEvent(healthPointEvent);
            }
        }

        public void AddHealthPointEvent(HealthPointEvent healthPointEvent)
        {
            if (!HealthPointTimelines.ContainsKey(healthPointEvent.Name))
            {
                HealthPointTimeline newTimeline = new HealthPointTimeline(healthPointEvent.Name, healthPointEvent.Id);
                HealthPointTimelines.Add(healthPointEvent.Name, newTimeline);
            }

            HealthPointTimelines[healthPointEvent.Name].AddEvent(healthPointEvent);
        }

        public HealthPointTimeline GetHealthPointTimeline(string name)
        {
            if (!HealthPointTimelines.ContainsKey(name)) return null;

            return HealthPointTimelines[name];
        }

        // HealPointTimelines -- lots of duplication here
        public void AddHealEvents(List<HealEvent> healEvents)
        {
            foreach (var healEvent in healEvents)
            {
                AddHealEvent(healEvent);
            }
        }

        public void AddHealEvent(HealEvent healEvent)
        {
            if (!HealTimelines.ContainsKey(healEvent.CasterName))
            {
                HealTimeline newTimeline = new HealTimeline(healEvent.CasterName);
                HealTimelines.Add(healEvent.CasterName, newTimeline);
            }

            HealTimelines[healEvent.CasterName].AddEvent(healEvent);
        }

        public HealTimeline GetHealTimeline(string name)
        {
            if (!HealTimelines.ContainsKey(name)) return null;

            return HealTimelines[name];
        }

        public override string ToString()
        {
            return $"{Id}: {Name}";
        }
    }
}
