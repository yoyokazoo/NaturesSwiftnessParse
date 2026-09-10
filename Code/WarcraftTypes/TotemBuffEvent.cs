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

        // Cast/summon ids for Windfury Totem and Grace of Air Totem -- distinct from
        // WINDFURY_ABILITY_ID, which is the applied weapon-enchant/buff id, not the cast id (totems
        // generally use a different spell entry for the cast than for the buff/aura they grant).
        // Used for twisting-loss tracking (see WindfuryUptimeParse.ComputeTwistingWindfuryLossMs),
        // which cares only about which totem occupies the Air slot at a given moment. Only the ranks
        // an actual level-60 raider would use are tracked -- rank 3 Windfury Totem (the only rank
        // worth casting once available), and ranks 2-3 Grace of Air Totem (rank 1's bonus is small
        // enough nobody raiding twists with it).
        public static readonly int[] WINDFURY_TOTEM_CAST_ABILITY_IDS = { 10614 }; // Rank 3
        public static readonly int[] GRACE_OF_AIR_TOTEM_CAST_ABILITY_IDS = { 10627, 25359 }; // Rank 2, 3

        // Shared global cooldown: after casting a totem, nothing else can be cast for 1.5s. Used by
        // Twisting Efficiency (see WindfuryUptimeParse.ComputeTwistingStats) to know how much of each
        // 10s Windfury window is actually available for Grace of Air -- the first 1.5s after a
        // Windfury Totem cast can't be used to drop Grace of Air, no matter how fast the shaman is.
        public const long TOTEM_GLOBAL_COOLDOWN_MS = 1500;

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
