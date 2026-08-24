using System.Collections.Generic;

namespace NaturesSwiftnessParse
{
    // One eligible player's merged Windfury uptime for a single fight, plus which shaman gets
    // credit (attributed from whichever shaman's totem actually applied Windfury to them most).
    public class WindfuryPlayerFightResult
    {
        public int FightId { get; private set; }
        public int PlayerActorId { get; private set; }
        public string PlayerName { get; private set; }
        // Null when this player was never targeted by any shaman's Windfury Totem this fight --
        // there's no signal for whose group they belong to, so they can't be credited to anyone.
        public int? ShamanActorId { get; private set; }
        public string ShamanName { get; private set; }
        public List<TotemBuffEvent> MergedIntervals { get; private set; }
        public long CoveredMs { get; private set; }
        public long FightDurationMs { get; private set; }

        public double UptimePercent => FightDurationMs == 0 ? 0 : (100.0 * CoveredMs / FightDurationMs);

        public WindfuryPlayerFightResult(int fightId, int playerActorId, string playerName, int? shamanActorId, string shamanName, List<TotemBuffEvent> mergedIntervals, long fightDurationMs)
        {
            FightId = fightId;
            PlayerActorId = playerActorId;
            PlayerName = playerName;
            ShamanActorId = shamanActorId;
            ShamanName = shamanName;
            MergedIntervals = mergedIntervals;
            FightDurationMs = fightDurationMs;

            long covered = 0;
            foreach (var interval in mergedIntervals)
            {
                covered += interval.EndTime - interval.StartTime;
            }
            CoveredMs = covered;
        }

        public override string ToString()
        {
            string credit = ShamanName ?? "unattributed";
            return $"{PlayerName}: {UptimePercent:0.#}% Windfury uptime ({CoveredMs}ms / {FightDurationMs}ms), credited to {credit}";
        }
    }
}
