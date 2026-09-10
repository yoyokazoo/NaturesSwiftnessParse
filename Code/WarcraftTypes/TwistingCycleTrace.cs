namespace NaturesSwiftnessParse
{
    // Debug trace for one Windfury-Totem-to-next-Windfury-Totem "cycle" of a shaman's twisting
    // timeline -- see WindfuryUptimeParse.ComputeTwistingStats. WindowStart/WindowEnd is the
    // theoretical Grace of Air availability window for the cycle (10s Windfury duration, minus the
    // 1.5s global cooldown after the Windfury cast); GraceOfAirCastTime is when (if at all) the
    // shaman actually cast Grace of Air Totem that cycle; CapturedStart/CapturedEnd is the overlap
    // between the window and however long Grace of Air was actually down, i.e. what Twisting
    // Efficiency actually credits for this cycle (null if Grace of Air was never cast that cycle).
    public class TwistingCycleTrace
    {
        public long WindowStart { get; private set; }
        public long WindowEnd { get; private set; }
        public long? GraceOfAirCastTime { get; private set; }
        public long? CapturedStart { get; private set; }
        public long? CapturedEnd { get; private set; }
        public long AvailableMs { get; private set; }
        public long ActualMs { get; private set; }

        public TwistingCycleTrace(long windowStart, long windowEnd, long? graceOfAirCastTime, long? capturedStart, long? capturedEnd, long availableMs, long actualMs)
        {
            WindowStart = windowStart;
            WindowEnd = windowEnd;
            GraceOfAirCastTime = graceOfAirCastTime;
            CapturedStart = capturedStart;
            CapturedEnd = capturedEnd;
            AvailableMs = availableMs;
            ActualMs = actualMs;
        }
    }
}
