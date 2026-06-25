using HarmonyLib;
using Verse;
using Verse.AI;

namespace Fortified;

// 机兵跨入新格时触发碾压
[HarmonyPatch(typeof(Pawn_PathFollower), "TryEnterNextPathCell")]
internal static class Patch_TryEnterNextPathCell_Trample
{
    // ___pawn与___lastCell注入私有字段
    private static void Postfix(Pawn ___pawn, IntVec3 ___lastCell)
    {
        if (___pawn == null) return;
        CompTrampleBuildings comp = ___pawn.GetComp<CompTrampleBuildings>();
        if (comp == null) return;
        IntVec3 moveDir = ___pawn.Position - ___lastCell;
        comp.Notify_EnteredCell(moveDir);
    }
}
