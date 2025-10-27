namespace NaturesSwiftnessParse
{
    public class HealthPointEvent
    {
        public long Time { get; private set; }
        public int Damage { get; private set; }
        public float Percent { get; private set; }
        public string Name { get; private set; }
        public int Id { get; private set; }

        public HealthPointEvent(long time, int damage, float percent, string name, int id)
        {
            Time = time;
            Damage = damage;
            Percent = percent;
            Name = name;
            Id = id;
        }

        // We don't yet have a way to get the player's starting health.  So if the first event added is a heal,
        // they must have started lower than 100%, so let's just say 99%.
        // TODO: This is horribly hacky, figure out why we don't get a starting health
        public void SetSubOneHundredStartingHealth()
        {
            Damage = 1;
            Percent = 99;
        }

        public override string ToString()
        {
            var direction = Damage < 0 ? "up" : "down";
            return $"{Time} - {Name} {Damage} {direction} to {Percent}";
        }
    }
}
