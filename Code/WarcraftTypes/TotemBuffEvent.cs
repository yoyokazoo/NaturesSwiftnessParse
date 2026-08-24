using System.Collections.Generic;

namespace NaturesSwiftnessParse
{
    public class TotemBuffEvent
    {
        public const int WINDFURY_ABILITY_ID = 10610;
        // Windfury Totem grants 10s of Windfury to melee in range each time it (re)applies
        public const int WINDFURY_BUFF_DURATION_MS = 10000;

        // Windfury Totem is itself implemented as a temporary main-hand weapon enchant (ranks 1-5,
        // per https://wowwiki-archive.fandom.com/wiki/EnchantId/Enchant_IDs), the same mechanism
        // Sharpening Stones/Poisons use. So a player who already has Windfury active shows up in
        // CombatantInfo with one of these ids as their main-hand temporaryEnchant -- that must NOT
        // be treated as a disqualifying enchant, or every player who had Windfury at pull (exactly
        // the case the "assume active for the first 10s" rule is meant to cover) would be wrongly
        // excluded.
        public static readonly HashSet<int> WINDFURY_TOTEM_ENCHANT_IDS = new HashSet<int> { 1783, 563, 564, 2638, 2639 };

        public long StartTime { get; private set; }
        public long EndTime { get; private set; }
        public int FightId { get; private set; }
        public int AbilityId { get; private set; }

        public TotemBuffEvent(long startTime, long endTime, int fightId, int abilityId)
        {
            StartTime = startTime;
            EndTime = endTime;
            FightId = fightId;
            AbilityId = abilityId;
        }

        public override string ToString()
        {
            return $"TotemBuff {AbilityId} started at {StartTime} and ended at {EndTime} during fight {FightId}";
        }
    }
}
