namespace NaturesSwiftnessParse
{
    public class TotemBuffEvent
    {
        public const int WINDFURY_ABILITY_ID = 10610;

        public long StartTime { get; private set; }
        public long EndTime { get; private set; }
        public int FightId { get; private set; }
        public int AbilityId { get; private set; }

        public TotemBuffEvent(long startTime, long endTime, int fightId, int abilityId)
        {
            StartTime = startTime;
            EndTime = endTime;
            FightId = fightId;
            AbilityId = abilityId;
        }

        public override string ToString()
        {
            return $"TotemBuff {AbilityId} started at {StartTime} and ended at {EndTime} during fight {FightId}";
        }
    }
}
