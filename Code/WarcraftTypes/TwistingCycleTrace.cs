namespace NaturesSwiftnessParse
{
    // Debug trace for one Windfury-Totem-to-next-Windfury-Totem "cycle" of a shaman's twisting
    // timeline -- see WindfuryUptimeParse.ComputeTwistingStats. WindowStart/WindowEnd is the
    // theoretical *ideal* slot for the cycle (10s Windfury duration, minus the 1.5s global cooldown
    // after the Windfury cast) for the *other* Air totem the shaman twists against (Grace of Air
    // Totem or Tranquil Air Totem -- treated identically, see
    // TotemBuffEvent.NON_WINDFURY_AIR_TOTEM_CAST_ABILITY_IDS) -- kept purely as a per-cycle reference
    // point in the debug trace; it no longer feeds Twisting Efficiency directly (see
    // WindfuryGroupFightResult.TwistingEfficiencyPercent, which is computed once over the whole span
    // from the shaman's first Air totem cast to fight end, not summed cycle-by-cycle).
    // AirTotemCastTime is when (if at all) the shaman actually cast one of those that cycle;
    // CapturedStart/CapturedEnd is however long it was actually down that cycle -- uncapped by
    // WindowEnd, since Twisted Totem Seconds counts real totem-active time, not just the ideal
    // window (null if neither was cast that cycle). TwistedMs is that same span's length;
    // LossMs is how much of it (if any) fed Twisting Windfury Loss, i.e. ran past the 10s Windfury
    // buff expired without a fresh Windfury cast.
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

        public TwistingCycleTrace(long windowStart, long windowEnd, long? airTotemCastTime, long? capturedStart, long? capturedEnd, long availableMs, long twistedMs, long lossMs)
        {
            WindowStart = windowStart;
            WindowEnd = windowEnd;
            AirTotemCastTime = airTotemCastTime;
            CapturedStart = capturedStart;
            CapturedEnd = capturedEnd;
            AvailableMs = availableMs;
            TwistedMs = twistedMs;
            LossMs = lossMs;
        }
    }
}
