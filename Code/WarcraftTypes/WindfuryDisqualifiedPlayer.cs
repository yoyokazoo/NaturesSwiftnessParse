namespace NaturesSwiftnessParse
{
    // An otherwise-eligible (warrior/rogue) player excluded from a fight's Windfury results because
    // their main-hand weapon had a real (non-Windfury) temporary enchant -- kept so the eligibility
    // determination is visible in debug output, not just silently applied.
    public class WindfuryDisqualifiedPlayer
    {
        public int ActorId { get; private set; }
        public string PlayerName { get; private set; }
        // Null when this player couldn't be matched to any shaman's inferred group.
        public int? ShamanActorId { get; private set; }
        public string ShamanName { get; private set; }
        public int EnchantId { get; private set; }
        // The fight the gear snapshot actually came from -- may differ from the fight this
        // disqualification applies to, when it was borrowed via GetNearestGearInfo (e.g. a trash
        // fight borrowing a nearby boss pull's snapshot).
        public int GearSourceFightId { get; private set; }

        public WindfuryDisqualifiedPlayer(int actorId, string playerName, int? shamanActorId, string shamanName, int enchantId, int gearSourceFightId)
        {
            ActorId = actorId;
            PlayerName = playerName;
            ShamanActorId = shamanActorId;
            ShamanName = shamanName;
            EnchantId = enchantId;
            GearSourceFightId = gearSourceFightId;
        }

        public override string ToString()
        {
            return $"{PlayerName}: main-hand enchant {EnchantId} (from fight {GearSourceFightId})";
        }
    }
}
