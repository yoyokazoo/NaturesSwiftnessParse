using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NaturesSwiftnessParse
{
    public class HealthPointEvent
    {
        public float Time { get; private set; }
        public int Damage { get; private set; }
        public float Percent { get; private set; }
        public string Name { get; private set; }

        public HealthPointEvent(float time, int damage, float percent, string name)
        {
            Time = time;
            Damage = damage;
            Percent = percent;
            Name = name;
        }

        public override string ToString()
        {
            var direction = Damage < 0 ? "up" : "down";
            return $"{Time} - {Name} {Damage} {direction} to {Percent}";
        }
    }
}
