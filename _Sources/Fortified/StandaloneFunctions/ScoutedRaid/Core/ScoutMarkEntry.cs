using Verse;

namespace Fortified
{
    // 侦察标记数据
    public class ScoutMarkEntry : IExposable
    {
        public IntVec3 cell;
        public int recordedTick;
        public int weight;
        // 跨pawn去重的thingID
        public int thingId;
        // 锁定该标记的pawnID
        public int ownerPawnId;

        public void ExposeData()
        {
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref recordedTick, "recordedTick");
            Scribe_Values.Look(ref weight, "weight", 1);
            Scribe_Values.Look(ref thingId, "thingId");
            Scribe_Values.Look(ref ownerPawnId, "ownerPawnId");
        }
    }
}
