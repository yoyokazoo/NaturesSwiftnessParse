using System;
using System.Collections.Generic;

namespace NaturesSwiftnessParse
{
    public class HealTimeline
    {
        public string Name { get; private set; }
        public List<HealEvent> Events { get; private set; }

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
            bool sortingChainHeal = false;
            // Chain heal bounces get logged at the same time
            // If we're on the same timestamp, sort them by pre-critical heal amount
            for(int i = Events.Count - 1; i >= 0; i--)
            {
                var otherHealEvent = Events[i];
                if (otherHealEvent.Time == healEvent.Time)
                {
                    // if we're smaller than the one we're looking at, insert where we're at
                    if (healEvent.GetPreCriticalHealAmount() < otherHealEvent.GetPreCriticalHealAmount())
                    {
                        Events.Insert(i + 1, healEvent);
                        return;
                    }
                    else
                    {
                        // otherwise if we're larger, keep looking but remember that we're mid sort so if we skip past the timestamp we can insert
                        sortingChainHeal = true;
                    }
                }
                else if (sortingChainHeal)
                {
                    // We've sorted the biggest bounce to the front
                    Events.Insert(i + 1, healEvent);
                    return;
                }
                else
                {
                    break;
                }
            }

            // Otherwise just add the heal event
            Events.Add(healEvent);
        }

        public void Print()
        {
            Console.WriteLine($"Heal Timeline for {Name}");
            for (int i = 0; i < Events.Count; i++)
            {
                Console.WriteLine($"\t{Events[i]}");
            }
        }
    }
}
