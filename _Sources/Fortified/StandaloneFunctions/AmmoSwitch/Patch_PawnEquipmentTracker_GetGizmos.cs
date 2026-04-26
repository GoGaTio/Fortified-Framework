using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Fortified
{
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.GetGizmos))]
    public static class Patch_PawnEquipmentTracker_GetGizmos
    {
        public static void Postfix(Pawn_EquipmentTracker __instance, ref IEnumerable<Gizmo> __result)
        {
            ThingWithComps primary = __instance?.Primary;
            CompAmmoSwitch comp = primary?.TryGetComp<CompAmmoSwitch>();
            if (comp == null || !comp.HasAnyAmmoOption) return;

            __result = Append(__result, comp, __instance.pawn);
        }

        private static IEnumerable<Gizmo> Append(IEnumerable<Gizmo> original, CompAmmoSwitch comp, Pawn pawn)
        {
            foreach (var g in original) yield return g;
            yield return comp.GetSwitchGizmo(pawn);
        }
    }
}