namespace NaturesSwiftnessParse
{
    public class NaturesSwiftnessEvent
    {
        public long Time { get; private set; }
        public string CasterName { get; private set; }
        public int FightId { get; private set; }

        // expand to work with chain heal hitting multiple targets
        public HealEvent HealEvent { get; private set; }
        public HealthPointEvent NSHealthPointEvent { get; private set; }
        public HealthPointEvent HealHealthPointEvent { get; private set; }

        public NaturesSwiftnessEvent(string name, long time, int fightId)
        {
            CasterName = name;
            Time = time;
            FightId = fightId;

            HealEvent = null;
            NSHealthPointEvent = null;
            HealHealthPointEvent = null;
        }

        // Heal cast after Nature's Swiftness
        public void AddHealEvent(HealEvent healEvent)
        {
            HealEvent = healEvent;
        }

        // HP Event right before the NS was cast
        public void AddNSHealthPointEvent(HealthPointEvent healthPointEvent)
        {
            NSHealthPointEvent = healthPointEvent;
        }

        // HP Event right before the heal was cast
        public void AddHealHealthPointEvent(HealthPointEvent healthPointEvent)
        {
            HealHealthPointEvent = healthPointEvent;
        }

        public override string ToString()
        {
            string heal = "no subsequent heal cast";
            if (HealEvent != null)
            {
                heal = $"{HealEvent.Time - Time} seconds later {HealEvent.HealName} cast on {HealEvent.TargetName} for {HealEvent.DamageHealed} ({HealEvent.Overheal} overheal)";
            }

            string nsHpEvent = "N/A";
            if (NSHealthPointEvent != null)
            {
                nsHpEvent = $"NS was cast {HealEvent?.Time - NSHealthPointEvent.Time}s after {NSHealthPointEvent.Name} dropped to {NSHealthPointEvent.Percent}%";
            }

            string healHpEvent = "N/A";
            if (HealHealthPointEvent != null)
            {
                healHpEvent = $"Heal was cast {HealEvent?.Time - HealHealthPointEvent.Time}s after {HealHealthPointEvent.Name} dropped to {HealHealthPointEvent.Percent}%";
            }

            return $"{CasterName} cast Nature's Swiftness at {Time}, {heal}. {nsHpEvent}. {healHpEvent}";
        }
    }
}
