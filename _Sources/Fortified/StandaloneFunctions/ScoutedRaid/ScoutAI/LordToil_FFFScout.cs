using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Fortified
{
    // 侦察兵分配自定义duty深入玩家基地仅在被压制时还击
    public class LordToil_FFFScout : LordToil
    {
        public override bool ForceHighStoryDanger => true;
        public override bool AllowSatisfyLongNeeds => false;

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                var p = lord.ownedPawns[i];
                if (p?.mindState == null) continue;
                p.mindState.duty = new PawnDuty(FFF_DutyDefOf.FFF_ScoutAdvance);
                p.TryGetComp<CompCanBeDormant>()?.WakeUp();
            }
        }
    }

    [DefOf]
    public static class FFF_DutyDefOf
    {
        public static DutyDef FFF_ScoutAdvance;

        static FFF_DutyDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FFF_DutyDefOf));
        }
    }
}
