using System.Collections.Generic;

namespace NaturesSwiftnessParse
{
    public class FightReport
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public Dictionary<string, HealthPointTimeline> HealthPointTimelines { get; private set; }
        public Dictionary<string, HealTimeline> HealTimelines { get; private set; }

        public FightReport(int id, string name)
        {
            Id = id;
            Name = name;
            HealthPointTimelines = new Dictionary<string, HealthPointTimeline>();
            HealTimelines = new Dictionary<string, HealTimeline>();
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
                HealthPointTimeline newTimeline = new HealthPointTimeline(healthPointEvent.Name);
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
