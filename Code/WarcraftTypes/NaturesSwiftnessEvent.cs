namespace NaturesSwiftnessParse
{
    public class NaturesSwiftnessEvent
    {
        public long Time { get; private set; }
        public string CasterName { get; private set; }
        public int FightId { get; private set; }

        // expand to work with chain heal hitting multiple targets
        public HealEvent HealEvent { get; private set; }

        // There are four damage events that we care about
        // The most recent Damage the player took before the NS and the Heal
        // and the most recent HP Value of the player before the NS and the Heal
        // This helps us split out what event triggered the "oh shit I gotta NS" and
        // what actually happened with the NS, since they may have gotten healed by someone else in the meantime

        // Most recent damage before NS was cast
        public HealthPointEvent NSDamageEvent { get; set; }
        // Most recent hp before NS was cast
        public HealthPointEvent NSHealthPointEvent { get; set; }

        // Most recent damage before Heal was cast
        public HealthPointEvent HealDamageEvent { get; set; }
        // Most recent hp before Heal was cast
        public HealthPointEvent HealHealthPointEvent { get; set; }

        public NaturesSwiftnessEvent(string name, long time, int fightId)
        {
            CasterName = name;
            Time = time;
            FightId = fightId;

            HealEvent = null;

            NSDamageEvent = null;
            NSHealthPointEvent = null;

            HealDamageEvent = null;
            HealHealthPointEvent = null;
        }

        // Heal cast after Nature's Swiftness
        public void AddHealEvent(HealEvent healEvent)
        {
            HealEvent = healEvent;
        }

        public override string ToString()
        {
            string heal = "no subsequent heal cast";
            if (HealEvent != null)
            {
                heal = $"{HealEvent.Time - Time} seconds later {HealEvent.HealName} cast on {HealEvent.TargetName} for {HealEvent.DamageHealed} ({HealEvent.Overheal} overheal)";
            }

            string nsDamageEvent = "N/A";
            if (NSDamageEvent != null)
            {
                nsDamageEvent = $"NS was cast {HealEvent?.Time - NSDamageEvent.Time}s after {NSDamageEvent.Name} dropped to {NSDamageEvent.Percent}%";
            }

            string nsHpEvent = "N/A";
            if (NSHealthPointEvent != null)
            {
                nsHpEvent = $"{NSHealthPointEvent.Name} was {NSHealthPointEvent.Percent}% when NS was cast";
            }

            string healDamageEvent = "N/A";
            if (HealDamageEvent != null)
            {
                healDamageEvent = $"Heal was cast {HealEvent?.Time - HealDamageEvent.Time}s after {HealDamageEvent.Name} dropped to {HealDamageEvent.Percent}%";
            }

            string healHpEvent = "N/A";
            if (NSHealthPointEvent != null)
            {
                healHpEvent = $"{HealHealthPointEvent.Name} was {HealHealthPointEvent.Percent}% when heal was cast";
            }

            return $"{CasterName} cast Nature's Swiftness at {Time}, {heal}. {nsDamageEvent}. {nsHpEvent}. {healDamageEvent}. {healHpEvent}";
        }
    }
}
