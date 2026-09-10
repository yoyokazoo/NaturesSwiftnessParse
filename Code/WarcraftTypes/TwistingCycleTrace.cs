namespace NaturesSwiftnessParse
{
    // Debug trace for one Windfury-Totem-to-next-Windfury-Totem "cycle" of a shaman's twisting
    // timeline -- see WindfuryUptimeParse.ComputeTwistingStats. WindowStart/WindowEnd is the
    // theoretical availability window for the cycle (10s Windfury duration, minus the 1.5s global
    // cooldown after the Windfury cast) for the *other* Air totem the shaman twists against (Grace of
    // Air Totem or Tranquil Air Totem -- treated identically, see
    // TotemBuffEvent.NON_WINDFURY_AIR_TOTEM_CAST_ABILITY_IDS); AirTotemCastTime is when (if at all)
    // the shaman actually cast one of those that cycle; CapturedStart/CapturedEnd is the overlap
    // between the window and however long it was actually down, i.e. what Twisting Efficiency
    // actually credits for this cycle (null if neither was cast that cycle).
    public class TwistingCycleTrace
    {
        public long WindowStart { get; private set; }
        public long WindowEnd { get; private set; }
        public long? AirTotemCastTime { get; private set; }
        public long? CapturedStart { get; private set; }
        public long? CapturedEnd { get; private set; }
        public long AvailableMs { get; private set; }
        public long ActualMs { get; private set; }

        public TwistingCycleTrace(long windowStart, long windowEnd, long? airTotemCastTime, long? capturedStart, long? capturedEnd, long availableMs, long actualMs)
        {
            WindowStart = windowStart;
            WindowEnd = windowEnd;
            AirTotemCastTime = airTotemCastTime;
            CapturedStart = capturedStart;
            CapturedEnd = capturedEnd;
            AvailableMs = availableMs;
            ActualMs = actualMs;
        }
    }
}
