using System;
using System.Collections.Generic;

namespace NaturesSwiftnessParse
{
    public class HealthPointTimeline
    {
        private string Name;
        private List<HealthPointEvent> Events;

        public HealthPointTimeline(string name)
        {
            Name = name;
            Events = new List<HealthPointEvent>();

            // Assume the player starts at full health at the start of the fight
            Events.Add(new HealthPointEvent(0, 0, 100, Name));
        }

        public void AddEvent(HealthPointEvent hpe)
        {
            Events.Add(hpe);
        }

        public void Print()
        {
            Console.WriteLine($"Health Point Timeline for {Name}");
            for (int i = 0; i < Events.Count; i++)
            {
                // Skip first event, starting at 100%
                if (i == 0) continue;

                Console.WriteLine($"\t{Events[i]}");
            }
        }
    }
}
