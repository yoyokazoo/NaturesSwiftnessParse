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
        // Seconds (in ms) this shaman's group went without Windfury during this fight specifically
        // because the shaman cast Grace of Air Totem and didn't re-cast Windfury Totem before the
        // previous application's 10s ran out -- see WindfuryUptimeParse.ComputeTwistingWindfuryLossMs.
        // Computed from cast events only, independent of (and not folded into) UnionIntervals/CoveredMs
        // above, which are computed from applied-buff events.
        public long TwistingWindfuryLossMs { get; private set; }
        // Fight-relative (StartTime = 0) Windfury Totem/Grace of Air Totem cast timestamps that fed
        // into TwistingWindfuryLossMs, and the (start, end) gaps that were counted as loss -- kept
        // purely for debug output, to let someone check the twisting-loss math by hand. See
        // FormatTwistingDebugTrace.
        public List<(long Timestamp, bool IsWindfuryCast)> RawTwistingCasts { get; private set; }
        public List<(long Start, long End)> TwistingLossIntervals { get; private set; }
        public long FightStartTime { get; private set; }
        // Of the Grace of Air uptime theoretically available this fight (10s Windfury window minus
        // the 1.5s global cooldown, per Windfury-to-Windfury cycle starting from the shaman's first
        // Windfury Totem cast -- see WindfuryUptimeParse.ComputeTwistingStats), how much the shaman
        // actually captured. Independent of TwistingWindfuryLossMs above: a cycle can be "efficient"
        // (little missed Grace of Air time) while still causing a Windfury loss if it runs long, and
        // vice versa.
        public long TwistingGraceOfAirAvailableMs { get; private set; }
        public long TwistingGraceOfAirActualMs { get; private set; }
        public List<TwistingCycleTrace> TwistingCycles { get; private set; }

        public double MaxUptimePercent => FightDurationMs == 0 ? 0 : (100.0 * CoveredMs / FightDurationMs);
        public double TwistingEfficiencyPercent => TwistingGraceOfAirAvailableMs == 0 ? 0 : (100.0 * TwistingGraceOfAirActualMs / TwistingGraceOfAirAvailableMs);
        // Whether the shaman cast Grace of Air Totem at all this fight -- used to exclude fights
        // where they never attempted to twist from the "twisted fights" aggregate (see
        // WindfuryUptimeParse.PrintTwistedFightsSummary), rather than letting them drag it toward 0.
        public bool WasTwisted => RawTwistingCasts.Any(c => !c.IsWindfuryCast);

        public WindfuryGroupFightResult(int fightId, int shamanActorId, string shamanName, List<TotemBuffEvent> unionIntervals, int memberCount, long fightDurationMs,
            long twistingWindfuryLossMs = 0, List<(long Timestamp, bool IsWindfuryCast)> rawTwistingCasts = null, List<(long Start, long End)> twistingLossIntervals = null, long fightStartTime = 0,
            long twistingGraceOfAirAvailableMs = 0, long twistingGraceOfAirActualMs = 0, List<TwistingCycleTrace> twistingCycles = null)
        {
            FightId = fightId;
            ShamanActorId = shamanActorId;
            ShamanName = shamanName;
            UnionIntervals = unionIntervals;
            MemberCount = memberCount;
            FightDurationMs = fightDurationMs;
            TwistingWindfuryLossMs = twistingWindfuryLossMs;
            RawTwistingCasts = rawTwistingCasts ?? new List<(long, bool)>();
            TwistingLossIntervals = twistingLossIntervals ?? new List<(long, long)>();
            FightStartTime = fightStartTime;
            TwistingGraceOfAirAvailableMs = twistingGraceOfAirAvailableMs;
            TwistingGraceOfAirActualMs = twistingGraceOfAirActualMs;
            TwistingCycles = twistingCycles ?? new List<TwistingCycleTrace>();

            long covered = 0;
            foreach (var interval in unionIntervals)
            {
                covered += interval.EndTime - interval.StartTime;
            }
            CoveredMs = covered;
        }

        public override string ToString()
        {
            return $"{ShamanName}'s group ({MemberCount} eligible member(s)): {MaxUptimePercent:0.#}% max uptime ({CoveredMs}ms / {FightDurationMs}ms), " +
                $"Twisting Windfury Loss {TwistingWindfuryLossMs / 1000.0:0.#}s, Twisting Efficiency {TwistingEfficiencyPercent:0.#}% ({TwistingGraceOfAirActualMs / 1000.0:0.#}s / {TwistingGraceOfAirAvailableMs / 1000.0:0.#}s)";
        }

        // Fight-relative seconds, e.g. 19044ms -> "19.044s" -- matches how someone would read
        // timestamps off WCL's own cast timeline when checking this by hand.
        private static string FormatRelativeSeconds(long absoluteMs, long fightStartMs)
        {
            return $"{(absoluteMs - fightStartMs) / 1000.0:0.000}s";
        }

        private string FormatRelativeSecondsOrNull(long? absoluteMs)
        {
            return absoluteMs.HasValue ? FormatRelativeSeconds(absoluteMs.Value, FightStartTime) : "none";
        }

        // Prints the raw Windfury Totem/Grace of Air Totem cast timeline, the resulting Twisting
        // Windfury Loss intervals, and a cycle-by-cycle Twisting Efficiency breakdown -- all in
        // fight-relative seconds, so both metrics can be checked by hand against WCL's own cast
        // timeline for this shaman.
        public string FormatTwistingDebugTrace()
        {
            var lines = new List<string>();

            var rawCasts = string.Join(", ", RawTwistingCasts.Select(c => $"{FormatRelativeSeconds(c.Timestamp, FightStartTime)} {(c.IsWindfuryCast ? "WF" : "GoA")}"));
            lines.Add($"      raw casts: [{rawCasts}]");

            lines.Add("      loss intervals:");
            foreach (var interval in TwistingLossIntervals)
            {
                var start = FormatRelativeSeconds(interval.Start, FightStartTime);
                var end = FormatRelativeSeconds(interval.End, FightStartTime);
                var durationSeconds = (interval.End - interval.Start) / 1000.0;
                lines.Add($"        [{start} - {end}] ({durationSeconds:0.000}s)");
            }
            lines.Add($"      total Twisting Windfury Loss: {TwistingWindfuryLossMs / 1000.0:0.000}s");

            lines.Add("      twisting cycles (Grace of Air availability):");
            foreach (var cycle in TwistingCycles)
            {
                var windowStart = FormatRelativeSeconds(cycle.WindowStart, FightStartTime);
                var windowEnd = FormatRelativeSeconds(cycle.WindowEnd, FightStartTime);
                var goaCast = FormatRelativeSecondsOrNull(cycle.GraceOfAirCastTime);
                var captured = cycle.CapturedStart.HasValue
                    ? $"{FormatRelativeSecondsOrNull(cycle.CapturedStart)} - {FormatRelativeSecondsOrNull(cycle.CapturedEnd)}"
                    : "none";
                lines.Add($"        window [{windowStart} - {windowEnd}] ({cycle.AvailableMs / 1000.0:0.000}s avail): GoA cast @ {goaCast}, captured [{captured}] ({cycle.ActualMs / 1000.0:0.000}s)");
            }
            lines.Add($"      total Twisting Efficiency: {TwistingGraceOfAirActualMs / 1000.0:0.000}s / {TwistingGraceOfAirAvailableMs / 1000.0:0.000}s -> {TwistingEfficiencyPercent:0.#}%");

            return string.Join("\n", lines);
        }
    }
}
