namespace NaturesSwiftnessParse
{
    public class NaturesSwiftnessEvent
    {
        public long Time { get; private set; }
        public string CasterName { get; private set; }
        public int FightId { get; private set; }

        // expand to work with chain heal hitting multiple targets
        public HealEvent HealEvent { get; private set; }

        public NaturesSwiftnessEvent(string name, long time, int fightId)
        {
            CasterName = name;
            Time = time;
            HealEvent = null;
            FightId = fightId;
        }

        public void AddHealEvent(HealEvent healEvent)
        {
            HealEvent = healEvent;
        }

        public override string ToString()
        {
            string heal = "no subsequent heal cast.";
            if (HealEvent != null)
            {
                heal = $"{HealEvent.Time - Time} seconds later {HealEvent.HealName} cast on {HealEvent.TargetName} for {HealEvent.DamageHealed} ({HealEvent.Overheal} overheal)";
            }
            return $"{CasterName} cast Nature's Swiftness at {Time}, {heal}";
        }
    }
}
