using HarmonyLib;
using RimWorld;
using Verse;

namespace Fortified;

[HarmonyPatch(typeof(ThingDef), nameof(ThingDef.ConnectToPower), MethodType.Getter)]
public static class Patch_ThingDef_ConnectToPower
{
    public static void Postfix(ThingDef __instance, ref bool __result)
    {
        // Already true or transmits power (conduit) — nothing to do.
        if (__result || __instance.EverTransmitsPower) return;

        foreach (CompProperties comp in __instance.comps)
        {
            if (comp.compClass == null) continue;

            if (comp.compClass.IsSubclassOf(typeof(CompPowerTrader)) ||
                comp.compClass.IsSubclassOf(typeof(CompPowerBattery)))
            {
                __result = true;
                return;
            }
        }
    }
}
