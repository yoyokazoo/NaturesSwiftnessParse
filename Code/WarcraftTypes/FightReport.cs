using System.Collections.Generic;

namespace NaturesSwiftnessParse
{
    public class FightReport
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public Dictionary<string, HealthPointTimeline> Timelines { get; private set; }

        public FightReport(int id, string name)
        {
            Id = id;
            Name = name;
            Timelines = new Dictionary<string, HealthPointTimeline>();
        }

        public void AddHealthPointEvents(List<HealthPointEvent> healthPointEvents)
        {
            foreach (var healthPointEvent in healthPointEvents)
            {
                AddHealthPointEvent(healthPointEvent);
            }
        }

        public void AddHealthPointEvent(HealthPointEvent healthPointEvent)
        {
            if (!Timelines.ContainsKey(healthPointEvent.Name))
            {
                HealthPointTimeline newTimeline = new HealthPointTimeline(healthPointEvent.Name);
                Timelines.Add(healthPointEvent.Name, newTimeline);
            }

            Timelines[healthPointEvent.Name].AddEvent(healthPointEvent);
        }

        public bool HasHealthpointTimeline(string name)
        {
            return Timelines.ContainsKey(name);
        }

        public HealthPointTimeline GetHealthPointTimeline(string name)
        {
            return Timelines[name];
        }

        public override string ToString()
        {
            return $"{Id}: {Name}";
        }
    }
}
