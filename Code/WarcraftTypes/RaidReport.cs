using System;
using System.Collections.Generic;
using System.Linq;

namespace NaturesSwiftnessParse
{
    public class RaidReport
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Dictionary<int, FightReport> Fights { get; set; }
        public List<NaturesSwiftnessEvent> NaturesSwiftnessEvents { get; set; }
        public Dictionary<int, string> ActorsById { get; set; }
        public Dictionary<int, string> AbilitiesById { get; set; }

        public RaidReport(string id, string name)
        {
            Id = id;
            Name = name;
            Fights = new Dictionary<int, FightReport>();
            NaturesSwiftnessEvents = new List<NaturesSwiftnessEvent>();
            ActorsById = new Dictionary<int, string>();
            AbilitiesById = new Dictionary<int, string>();
        }

        public void AddFight(FightReport fight)
        {
            Fights.Add(fight.Id, fight);
        }

        public FightReport GetFight(int fightId)
        {
            if (!Fights.ContainsKey(fightId)) return null;

            return Fights[fightId];
        }

        public void AddNaturesSwiftnessEvent(NaturesSwiftnessEvent nsEvent)
        {
            NaturesSwiftnessEvents.Add(nsEvent);
        }

        public void AddActor(int id, string name)
        {
            ActorsById.Add(id, name);
        }

        public string GetActor(int id)
        {
            if (!ActorsById.ContainsKey(id)) return id.ToString();
            
            return ActorsById[id];
        }

        public void AddAbility(int id, string name)
        {
            AbilitiesById.Add(id, name);
        }

        public string GetAbility(int id)
        {
            if (!AbilitiesById.ContainsKey(id)) return id.ToString();

            return AbilitiesById[id];
        }

        public void Print()
        {
            Console.WriteLine($"Raid Report {Id}: {Name}");
        }

        public void PrintFights()
        {
            Console.WriteLine($"Raid Report {Id}: {Name} has {Fights.Count} fights:");
            foreach(var fight in Fights)
            {
                Console.WriteLine($"\t{fight}");
            }
        }

        public void PrintActors()
        {
            Console.WriteLine($"Raid Report {Id}: {Name} has {ActorsById.Count()} actors:");
            foreach (var key in ActorsById.Keys)
            {
                Console.WriteLine($"\t{key}: {ActorsById[key]}");
            }
        }

        public void PrintNaturesSwiftnessEvents()
        {
            Console.WriteLine($"Raid Report {Id}: {Name} has {NaturesSwiftnessEvents.Count} nature's swiftness events:");
            foreach (var nsEvent in NaturesSwiftnessEvents)
            {
                Console.WriteLine($"\t{nsEvent}");
            }
        }

        public void PrintMostCriticalNaturesSwiftnesses()
        {
            Console.WriteLine($"Most Critical Nature's Swiftnesses:");

            List<NaturesSwiftnessEvent> criticalSwiftnessEvents = new List<NaturesSwiftnessEvent>(NaturesSwiftnessEvents);
            NaturesSwiftnessEventSorter.SortByNSHealthPercent(criticalSwiftnessEvents);
            foreach(var nsEvent in criticalSwiftnessEvents)
            {
                Console.WriteLine($"\t{nsEvent}");
            }

            // Now that we have the top nature's swiftness events, present them in a pleasing manner
            const int NATURES_SWIFTNESS_EVENTS_TO_HIGHLIGHT = 5;
            for(int i = 0; i < NATURES_SWIFTNESS_EVENTS_TO_HIGHLIGHT; i++)
            {
                var highlightEvent = criticalSwiftnessEvents[i];
                var highlightFight = Fights[highlightEvent.FightId];

                var healDelay = highlightEvent.HealEvent.Time - highlightEvent.NSDamageEvent.Time;
                var healDelayString = FormatMilliseconds(healDelay);

                var damageLink = $"https://vanilla.warcraftlogs.com/reports/{Id}?fight={highlightFight.Id}&type=resources&source={highlightEvent.HealHealthPointEvent.Id}&view=events";
                var fightLink = $"https://vanilla.warcraftlogs.com/reports/{Id}?fight={highlightFight.Id}";

                string highlightString = $"{highlightEvent.CasterName} NS'ed {highlightEvent.HealEvent.TargetName} {healDelayString} after they got [hit to {highlightEvent.HealHealthPointEvent.Percent}%]({damageLink}) during [{highlightFight.Name}]({fightLink})";

                // Write method to find follow up Nature's Swiftnesses and call them out

                Console.WriteLine(highlightString);
            }
        }

        public static string FormatMilliseconds(long ms)
        {
            double seconds = ms / 1000.0;

            // Format with 2 decimal places, then trim trailing zeros
            string formatted = seconds.ToString("0.##");

            // Drop leading zero if under 1 second (e.g., 0.55 → .55)
            if (formatted.StartsWith("0"))
                formatted = formatted.TrimStart('0');

            return $"{formatted}s";
        }
    }

    public static class NaturesSwiftnessEventSorter
    {
        /// <summary>
        /// Sorts the given list of Nature's Swiftness events in ascending order of
        /// the player's HP% when NS was cast (lowest HP first).
        /// Events without NSHealthPointEvent are sorted last.
        /// </summary>
        public static void SortByNSHealthPercent(List<NaturesSwiftnessEvent> events)
        {
            events.Sort((a, b) =>
            {
                // Handle null NSHealthPointEvent safely
                bool aHasHp = a.NSHealthPointEvent != null;
                bool bHasHp = b.NSHealthPointEvent != null;

                if (!aHasHp && !bHasHp)
                    return 0;
                if (!aHasHp)
                    return 1; // nulls last
                if (!bHasHp)
                    return -1;

                // Compare by Percent (ascending = lowest HP first)
                return a.NSHealthPointEvent.Percent.CompareTo(b.NSHealthPointEvent.Percent);
            });
        }
    }
}
