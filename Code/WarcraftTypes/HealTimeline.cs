using System;
using System.Collections.Generic;

namespace NaturesSwiftnessParse
{
    public class HealTimeline
    {
        private string Name;
        private List<HealEvent> Events;

        public HealTimeline(string name)
        {
            Name = name;
            Events = new List<HealEvent>();
        }

        // Right now we assume that the events will be added in order.
        // Sometimes chain heal bounces get added out of order, perhaps this is the place
        // we should do the fixup?
        public void AddEvent(HealEvent healEvent)
        {
            Events.Add(healEvent);
        }

        public void Print()
        {
            Console.WriteLine($"Heal Timeline for {Name}");
            for (int i = 0; i < Events.Count; i++)
            {
                Console.WriteLine(Events[i]);
            }
        }
    }
}
