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
        public Dictionary<int, string> ActorTypeById { get; set; }
        public Dictionary<int, string> ActorClassById { get; set; }
        public Dictionary<int, int?> ActorPetOwnerById { get; set; }
        public Dictionary<int, string> AbilitiesById { get; set; }

        public RaidReport(string id, string name)
        {
            Id = id;
            Name = name;
            Fights = new Dictionary<int, FightReport>();
            NaturesSwiftnessEvents = new List<NaturesSwiftnessEvent>();
            ActorsById = new Dictionary<int, string>();
            ActorTypeById = new Dictionary<int, string>();
            ActorClassById = new Dictionary<int, string>();
            ActorPetOwnerById = new Dictionary<int, int?>();
            AbilitiesById = new Dictionary<int, string>();
            PopulateAbilities();
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

        public void AddActor(int id, string name, string type = null, string subType = null, int? petOwner = null)
        {
            ActorsById[id] = name;
            ActorTypeById[id] = type ?? string.Empty;
            ActorClassById[id] = subType ?? string.Empty;
            ActorPetOwnerById[id] = petOwner;
        }

        public string GetActor(int id)
        {
            if (!ActorsById.ContainsKey(id)) return id.ToString();

            return ActorsById[id];
        }

        // "Player", "NPC", "Pet" -- empty string if unknown
        public string GetActorType(int id)
        {
            return ActorTypeById.TryGetValue(id, out var type) ? type : string.Empty;
        }

        // Player class (e.g. "Warrior", "Rogue") -- empty string for non-players/unknown
        public string GetActorClass(int id)
        {
            return ActorClassById.TryGetValue(id, out var subType) ? subType : string.Empty;
        }

        // Actor id of the owning player, for pets/totems -- null if not a pet or unknown
        public int? GetActorPetOwner(int id)
        {
            return ActorPetOwnerById.TryGetValue(id, out var petOwner) ? petOwner : null;
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

        public void PrintMostCriticalNaturesSwiftnesses(int eventCountToHighlight)
        {
            Console.WriteLine();
            //Console.WriteLine($"Most Critical Nature's Swiftnesses:");

            List<NaturesSwiftnessEvent> criticalSwiftnessEvents = new List<NaturesSwiftnessEvent>(NaturesSwiftnessEvents);
            NaturesSwiftnessEventSorter.SortByHealHealthPercent(criticalSwiftnessEvents);
            foreach(var nsEvent in criticalSwiftnessEvents)
            {
                //Console.WriteLine($"\t{nsEvent}");
            }

            // Now that we have the top nature's swiftness events, present them in a pleasing manner
            Console.WriteLine($"Top {eventCountToHighlight} Nature's Swiftnesses of {Name}\n");
            for(int i = 0; i < eventCountToHighlight && i < criticalSwiftnessEvents.Count; i++)
            {
                var highlightEvent = criticalSwiftnessEvents[i];
                var highlightFight = Fights[highlightEvent.FightId];

                // Casters can cast NS but then not cast a follow-up heal, so just skip it
                if (highlightEvent.HealEvent == null)
                {
                    continue;
                }

                var healDelay = highlightEvent.HealEvent.Time - highlightEvent.NSDamageEvent.Time;
                var healDelayString = FormatMilliseconds(healDelay);

                var damageLink = $"https://vanilla.warcraftlogs.com/reports/{Id}?fight={highlightFight.Id}&type=resources&source={highlightEvent.HealHealthPointEvent.Id}&view=events";
                var fightLink = $"https://vanilla.warcraftlogs.com/reports/{Id}?fight={highlightFight.Id}";

                // Write method to find follow up Nature's Swiftnesses and call them out
                List<NaturesSwiftnessEvent> followUpNS = new List<NaturesSwiftnessEvent>();
                foreach(var nsEvent in NaturesSwiftnessEvents)
                {
                    var followUpTime = nsEvent.HealTime - highlightEvent.HealTime;
                    if (nsEvent.FightId == highlightFight.Id &&
                        nsEvent.HealEvent?.TargetName == highlightEvent.HealEvent?.TargetName &&
                        followUpTime >= 0 && followUpTime < 2000 &&
                        nsEvent.CasterName != highlightEvent.CasterName)
                    {
                        followUpNS.Add(nsEvent);
                        criticalSwiftnessEvents.Remove(nsEvent);
                    }
                }

                // NS casts landing at the exact same moment as the highlight get folded into the
                // caster list instead of being reported as a "followed up" NS
                var simultaneousNS = followUpNS.Where(ns => ns.HealTime == highlightEvent.HealTime).ToList();
                var laterNS = followUpNS.Except(simultaneousNS).OrderBy(ns => ns.HealTime).ToList();

                var casterNames = new List<string> { highlightEvent.CasterName };
                casterNames.AddRange(simultaneousNS.Select(ns => ns.CasterName));
                var casterString = JoinNames(casterNames);
                var simultaneousSuffix = simultaneousNS.Count > 0 ? " at the exact same time," : ",";

                string highlightString = $"{i+1}: {casterString} NS'ed {highlightEvent.HealEvent.TargetName} at {highlightEvent.HealHealthPointEvent.Percent}%{simultaneousSuffix} {healDelayString} after they got [hit to {highlightEvent.NSDamageEvent.Percent}%]({damageLink}) during [{highlightFight.Name}]({fightLink})";

                Console.WriteLine(highlightString);
                foreach (var nsEvent in laterNS)
                {
                    //Console.WriteLine($"\tFollow up {FormatMilliseconds(nsEvent.Time - highlightEvent.Time)} later: {nsEvent}");
                    Console.WriteLine($"\t{nsEvent.CasterName} followed up with an NS {FormatMilliseconds(nsEvent.HealTime - highlightEvent.HealTime)} later, when {highlightEvent.HealEvent.TargetName} was at {nsEvent.HealHealthPointEvent.Percent}%");
                }
            }

            Console.WriteLine();
        }

        public void SortHPTimelines()
        {
            foreach (var fightId in Fights.Keys)
            {
                foreach (var hpTimeline in GetFight(fightId).HealthPointTimelines.Values)
                {
                    hpTimeline.SortByTime();
                }
            }
        }

        public void LinkNaturesSwiftnessesAndHeals()
        {
            SortHPTimelines();

            foreach (var nsEvent in NaturesSwiftnessEvents)
            {
                // Link the ns to the heal
                FightReport fight = GetFight(nsEvent.FightId);
                HealTimeline healTimeline = fight.GetHealTimeline(nsEvent.CasterName);

                // Casters can cast NS but then not cast a follow-up heal, so just skip it
                if (healTimeline == null)
                {
                    continue;
                }

                long currentTimestamp = 0;
                for (int healEventIndex = 0; healEventIndex < healTimeline.Events.Count; healEventIndex++)
                {
                    HealEvent heal = healTimeline.Events[healEventIndex];

                    // Simultaneous heals are chain heal bounces.  They're already in order, so only care about the 1st one
                    if (TimesApproximatelyMatch(heal.Time, currentTimestamp)) continue;
                    currentTimestamp = heal.Time;

                    if (heal.Time < nsEvent.Time) continue;
                    if (heal.HotTick) continue;

                    if (heal.Time >= nsEvent.Time)
                    {
                        nsEvent.AddHealEvent(heal);
                        break;
                    }
                }

                //Console.WriteLine(nsEvent);

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
                //healthPointTimeline.Print();

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
        }

        public void PopulateAbilities()
        {
            // These will be filled dynamically eventually, but for now...
            AddAbility(1064, "Chain Heal (Rank 1)");
            AddAbility(10622, "Chain Heal (Rank 2)");
            AddAbility(10623, "Chain Heal (Rank 3)");
            AddAbility(8004, "Lesser Healing Wave (Rank 1)");
            AddAbility(8008, "Lesser Healing Wave (Rank 2)");
            AddAbility(8010, "Lesser Healing Wave (Rank 3)");
            AddAbility(10466, "Lesser Healing Wave (Rank 4)");
            AddAbility(10467, "Lesser Healing Wave (Rank 5)");
            AddAbility(10468, "Lesser Healing Wave (Rank 6)");
            AddAbility(331, "Healing Wave (Rank 1)");
            AddAbility(332, "Healing Wave (Rank 2)");
            AddAbility(547, "Healing Wave (Rank 3)");
            AddAbility(913, "Healing Wave (Rank 4)");
            AddAbility(939, "Healing Wave (Rank 5)");
            AddAbility(959, "Healing Wave (Rank 6)");
            AddAbility(8005, "Healing Wave (Rank 7)");
            AddAbility(10395, "Healing Wave (Rank 8)");
            AddAbility(10396, "Healing Wave (Rank 9)");
            AddAbility(25357, "Healing Wave (Rank 10)");
        }

        public static string JoinNames(List<string> names)
        {
            if (names.Count == 1)
                return names[0];

            if (names.Count == 2)
                return $"{names[0]} and {names[1]}";

            return $"{string.Join(", ", names.Take(names.Count - 1))}, and {names[names.Count - 1]}";
        }

        public static string FormatMilliseconds(long ms)
        {
            double seconds = ms / 1000.0;

            // Format with 3 decimal places, then trim trailing zeros
            string formatted = seconds.ToString("0.###");

            // Drop leading zero if under 1 second (e.g., 0.55 → .55), but keep "0" as-is
            if (formatted.StartsWith("0") && formatted != "0")
                formatted = formatted.TrimStart('0');

            return $"{formatted}s";
        }

        public static bool TimesApproximatelyMatch(long time1, long time2)
        {
            return Math.Abs(time1 - time2) < 5;
        }
    }

    public static class NaturesSwiftnessEventSorter
    {
        /// <summary>
        /// Sorts the given list of Nature's Swiftness events in ascending order of
        /// the player's HP% when the NS'ed heal was cast (lowest HP first).
        /// Events without HealHealthPointEvent are sorted last.
        /// </summary>
        public static void SortByHealHealthPercent(List<NaturesSwiftnessEvent> events)
        {
            events.Sort((a, b) =>
            {
                // Handle null HealHealthPointEvent safely
                bool aHasHp = a.HealHealthPointEvent != null;
                bool bHasHp = b.HealHealthPointEvent != null;

                if (!aHasHp && !bHasHp)
                    return 0;
                if (!aHasHp)
                    return 1; // nulls last
                if (!bHasHp)
                    return -1;

                // Compare by Percent (ascending = lowest HP first)
                return a.HealHealthPointEvent.Percent.CompareTo(b.HealHealthPointEvent.Percent);
            });
        }
    }
}
