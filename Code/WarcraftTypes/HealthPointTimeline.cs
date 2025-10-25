using System;
using System.Collections.Generic;

namespace NaturesSwiftnessParse
{
    public class HealthPointTimeline
    {
        public string Name { get; private set; }
        public List<HealthPointEvent> Events { get; private set; }

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

        public void SortByTime()
        {
            Events.Sort((a, b) =>
            {
                // Primary: sort by time (ascending)
                int timeCompare = a.Time.CompareTo(b.Time);
                if (timeCompare != 0)
                    return timeCompare;

                // Secondary: both positive damage (taking damage)
                if (a.Damage > 0 && b.Damage > 0)
                    return b.Percent.CompareTo(a.Percent); // higher % first

                // Secondary: both negative damage (healing)
                if (a.Damage < 0 && b.Damage < 0)
                    return a.Percent.CompareTo(b.Percent); // lower % first

                // Otherwise, keep stable ordering (equal timestamps, mixed sign)
                return 0;
            });
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
