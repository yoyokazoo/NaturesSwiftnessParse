using System.Collections.Generic;
using System.Linq;

namespace NaturesSwiftnessParse
{
    // One eligible player's merged Windfury uptime for a single fight, plus which shaman gets
    // credit (attributed via inferred party membership -- see WindfuryUptimeParse.InferPartyGroups).
    public class WindfuryPlayerFightResult
    {
        public int FightId { get; private set; }
        public int PlayerActorId { get; private set; }
        public string PlayerName { get; private set; }
        // Null when this player's inferred party couldn't be matched to any shaman's -- there's no
        // signal for whose group they belong to, so they can't be credited to anyone.
        public int? ShamanActorId { get; private set; }
        public string ShamanName { get; private set; }
        public List<TotemBuffEvent> MergedIntervals { get; private set; }
        // Fight-relative (StartTime = 0) timestamps, in ms, of each raw applybuff/refreshbuff that
        // fed into MergedIntervals -- kept purely for debug output, to let someone check the merge
        // by hand against WCL's own aura timeline for this player.
        public List<long> RawApplyTimestampsMs { get; private set; }
        public long FightStartTime { get; private set; }
        public long CoveredMs { get; private set; }
        public long FightDurationMs { get; private set; }

        public double UptimePercent => FightDurationMs == 0 ? 0 : (100.0 * CoveredMs / FightDurationMs);

        public WindfuryPlayerFightResult(int fightId, int playerActorId, string playerName, int? shamanActorId, string shamanName, List<TotemBuffEvent> mergedIntervals, List<long> rawApplyTimestampsMs, long fightStartTime, long fightDurationMs)
        {
            FightId = fightId;
            PlayerActorId = playerActorId;
            PlayerName = playerName;
            ShamanActorId = shamanActorId;
            ShamanName = shamanName;
            MergedIntervals = mergedIntervals;
            RawApplyTimestampsMs = rawApplyTimestampsMs;
            FightStartTime = fightStartTime;
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

        // Fight-relative seconds, e.g. 19044ms -> "19.044s" -- matches how someone would read
        // timestamps off WCL's own aura timeline when checking this by hand.
        private static string FormatRelativeSeconds(long absoluteMs, long fightStartMs)
        {
            return $"{(absoluteMs - fightStartMs) / 1000.0:0.000}s";
        }

        // Prints the raw applybuff/refreshbuff timestamps and the resulting merged intervals, both
        // in fight-relative seconds, so the merge-intervals math can be checked by hand.
        public string FormatDebugTrace()
        {
            var lines = new List<string>();

            var rawTimes = string.Join(", ", RawApplyTimestampsMs.Select(t => FormatRelativeSeconds(t, FightStartTime)));
            lines.Add($"      raw (re)applications: [{rawTimes}]");

            lines.Add("      merged intervals:");
            foreach (var interval in MergedIntervals)
            {
                var start = FormatRelativeSeconds(interval.StartTime, FightStartTime);
                var end = FormatRelativeSeconds(interval.EndTime, FightStartTime);
                var durationSeconds = (interval.EndTime - interval.StartTime) / 1000.0;
                lines.Add($"        [{start} - {end}] ({durationSeconds:0.000}s)");
            }

            var fightEndSeconds = FightDurationMs / 1000.0;
            lines.Add($"      fight duration: {fightEndSeconds:0.000}s, covered: {CoveredMs / 1000.0:0.000}s -> {UptimePercent:0.#}%");

            return string.Join("\n", lines);
        }
    }
}
