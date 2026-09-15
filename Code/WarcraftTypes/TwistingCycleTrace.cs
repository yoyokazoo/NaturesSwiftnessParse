namespace NaturesSwiftnessParse
{
    // Debug trace for one Windfury-Totem-to-next-Windfury-Totem "cycle" of a shaman's twisting
    // timeline -- see WindfuryUptimeParse.ComputeTwistingStats. WindowStart/WindowEnd is this
    // cycle's *ideal* slot (10s Windfury duration, minus the 1.5s global cooldown after the
    // Windfury cast, further capped to fight end for a trailing cycle) for the *other* Air totem the
    // shaman twists against (Grace of Air Totem or Tranquil Air Totem -- treated identically, see
    // TotemBuffEvent.NON_WINDFURY_AIR_TOTEM_CAST_ABILITY_IDS); AvailableMs is that window's length,
    // and is what Twisting Efficiency's denominator sums across cycles. AirTotemCastTime is when (if
    // at all) the shaman actually cast one of those that cycle; CapturedStart/CapturedEnd is however
    // long it was actually down that cycle -- uncapped by WindowEnd, since Twisted Totem Seconds
    // counts real totem-active time, not just the ideal window (null if neither was cast that
    // cycle). TwistedMs is that same uncapped span's length; LossMs is how much of it (if any) fed
    // Twisting Windfury Loss, i.e. ran past the 10s Windfury buff expiring without a fresh Windfury
    // cast. EfficiencyNetMs is this cycle's own contribution to Twisting Efficiency's numerator --
    // TwistedMs capped to the ideal window, minus LossMs -- and CountsTowardEfficiency is whether
    // this cycle is at or after the shaman's first Air totem cast this fight (cycles before that
    // don't count toward either side of the ratio at all, not even as available-but-unused).
    public class TwistingCycleTrace
    {
        public long WindowStart { get; private set; }
        public long WindowEnd { get; private set; }
        public long? AirTotemCastTime { get; private set; }
        public long? CapturedStart { get; private set; }
        public long? CapturedEnd { get; private set; }
        public long AvailableMs { get; private set; }
        public long TwistedMs { get; private set; }
        public long LossMs { get; private set; }
        public long EfficiencyNetMs { get; private set; }
        public bool CountsTowardEfficiency { get; private set; }

        public TwistingCycleTrace(long windowStart, long windowEnd, long? airTotemCastTime, long? capturedStart, long? capturedEnd,
            long availableMs, long twistedMs, long lossMs, long efficiencyNetMs, bool countsTowardEfficiency)
        {
            WindowStart = windowStart;
            WindowEnd = windowEnd;
            AirTotemCastTime = airTotemCastTime;
            CapturedStart = capturedStart;
            CapturedEnd = capturedEnd;
            AvailableMs = availableMs;
            TwistedMs = twistedMs;
            LossMs = lossMs;
            EfficiencyNetMs = efficiencyNetMs;
            CountsTowardEfficiency = countsTowardEfficiency;
        }
    }
}
