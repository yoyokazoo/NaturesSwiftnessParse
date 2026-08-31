using System.Collections.Generic;
using System.Linq;

namespace NaturesSwiftnessParse
{
    // A shaman's whole group's Windfury coverage for one fight, computed as a merge-intervals union
    // across all its eligible attributed players -- i.e. "was ANY eligible group member covered at
    // this moment", not an average of individual coverage. This is the "maximum uptime" metric:
    // players can drift in and out of totem range independently, so the group as a whole may have
    // had someone covered even when any single player's own uptime looks low.
    //
    // Because this produces exactly one value per (shaman, fight) regardless of how many eligible
    // players were in that group, aggregating these across fights normalizes by fight count rather
    // than by player-fight count -- a shaman with 3 eligible players and one with 2 both count as
    // one data point per fight.
    public class WindfuryGroupFightResult
    {
        public int FightId { get; private set; }
        public int ShamanActorId { get; private set; }
        public string ShamanName { get; private set; }
        public List<TotemBuffEvent> UnionIntervals { get; private set; }
        public int MemberCount { get; private set; }
        public long CoveredMs { get; private set; }
        public long FightDurationMs { get; private set; }

        public double MaxUptimePercent => FightDurationMs == 0 ? 0 : (100.0 * CoveredMs / FightDurationMs);

        public WindfuryGroupFightResult(int fightId, int shamanActorId, string shamanName, List<TotemBuffEvent> unionIntervals, int memberCount, long fightDurationMs)
        {
            FightId = fightId;
            ShamanActorId = shamanActorId;
            ShamanName = shamanName;
            UnionIntervals = unionIntervals;
            MemberCount = memberCount;
            FightDurationMs = fightDurationMs;

            long covered = 0;
            foreach (var interval in unionIntervals)
            {
                covered += interval.EndTime - interval.StartTime;
            }
            CoveredMs = covered;
        }

        public override string ToString()
        {
            return $"{ShamanName}'s group ({MemberCount} eligible member(s)): {MaxUptimePercent:0.#}% max uptime ({CoveredMs}ms / {FightDurationMs}ms)";
        }
    }
}
