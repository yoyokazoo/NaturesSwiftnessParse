using System.Collections.Generic;

namespace NaturesSwiftnessParse
{
    // All eligible players' Windfury results for a single fight.
    public class WindfuryFightResult
    {
        public int FightId { get; private set; }
        public string FightName { get; private set; }
        public bool IsBossFight { get; private set; }
        public List<WindfuryPlayerFightResult> PlayerResults { get; private set; }
        public List<WindfuryDisqualifiedPlayer> DisqualifiedPlayers { get; private set; }
        // One entry per shaman who had at least one eligible attributed player this fight -- see
        // WindfuryGroupFightResult for what this represents.
        public List<WindfuryGroupFightResult> GroupResults { get; private set; }

        public WindfuryFightResult(int fightId, string fightName, bool isBossFight)
        {
            FightId = fightId;
            FightName = fightName;
            IsBossFight = isBossFight;
            PlayerResults = new List<WindfuryPlayerFightResult>();
            DisqualifiedPlayers = new List<WindfuryDisqualifiedPlayer>();
            GroupResults = new List<WindfuryGroupFightResult>();
        }
    }
}
