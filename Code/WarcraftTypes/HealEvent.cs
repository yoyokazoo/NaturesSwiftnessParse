using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NaturesSwiftnessParse
{
    public class HealEvent
    {
        public float Time { get; private set; }
        public int DamageHealed { get; private set; }
        public int Overheal { get; private set; }
        public string CasterName { get; private set; }
        public string TargetName { get; private set; }
        public string HealName { get; private set; }
        public bool CriticalHeal { get; private set; }

        public HealEvent(float time, int damageHealed, int overheal, string casterName, string targetName, string healName, bool criticalHeal)
        {
            Time = time;
            DamageHealed = damageHealed;
            Overheal = overheal;
            CasterName = casterName;
            TargetName = targetName;
            HealName = healName;
            CriticalHeal = criticalHeal;
        }

        public override string ToString()
        {
            string overheal = Overheal > 0 ? $" ({Overheal} overheal)" : string.Empty;
            string critical = CriticalHeal ? " (critical)" : string.Empty;
            return $"{CasterName} cast {HealName} on {TargetName} at {Time} for {DamageHealed}{overheal}{critical}";
        }
    }
}
