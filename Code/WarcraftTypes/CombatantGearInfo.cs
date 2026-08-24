namespace NaturesSwiftnessParse
{
    // Per-fight main-hand weapon snapshot for one actor, used to determine Windfury eligibility.
    //
    // WoW gear carries two independent enchant concepts: a *permanent* enchant (Crusader, Fiery
    // Weapon, a rune, etc.) which has no bearing on Windfury eligibility, and a *temporary* enchant
    // (Sharpening Stone, Instant Poison, Wildvine Weapon Oil, etc.) -- a consumable applied on top,
    // of which only one can be active at a time. It's the temporary enchant that blocks Windfury.
    public class CombatantGearInfo
    {
        public int ActorId { get; private set; }
        public int FightId { get; private set; }
        public bool HasMainHandWeapon { get; private set; }
        public bool MainHandHasTemporaryEnchant { get; private set; }
        public int? MainHandTemporaryEnchantId { get; private set; }
        public int? MainHandPermanentEnchantId { get; private set; }

        public CombatantGearInfo(int actorId, int fightId, bool hasMainHandWeapon, bool mainHandHasTemporaryEnchant, int? mainHandTemporaryEnchantId, int? mainHandPermanentEnchantId)
        {
            ActorId = actorId;
            FightId = fightId;
            HasMainHandWeapon = hasMainHandWeapon;
            MainHandHasTemporaryEnchant = mainHandHasTemporaryEnchant;
            MainHandTemporaryEnchantId = mainHandTemporaryEnchantId;
            MainHandPermanentEnchantId = mainHandPermanentEnchantId;
        }

        public override string ToString()
        {
            if (!HasMainHandWeapon) return $"actor {ActorId}, fight {FightId}: no main-hand weapon";

            string temp = MainHandHasTemporaryEnchant ? $"temp enchant {MainHandTemporaryEnchantId} (ineligible)" : "no temp enchant";
            string perm = MainHandPermanentEnchantId.HasValue ? $", permanent enchant {MainHandPermanentEnchantId} (doesn't affect eligibility)" : string.Empty;
            return $"actor {ActorId}, fight {FightId}: main-hand {temp}{perm}";
        }
    }
}
