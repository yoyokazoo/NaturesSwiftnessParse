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

        public override string ToString()
        {
            var direction = Damage < 0 ? "up" : "down";
            return $"{Time} - {Name} {Damage} {direction} to {Percent}";
        }
    }
}
