using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Fortified;

[HarmonyPatch(typeof(RecipeDef), nameof(RecipeDef.SpecialDisplayStats))]
internal static class Patch_RecipeDef_SpecialDisplayStats
{
    public static IEnumerable<StatDrawEntry> Postfix(
        IEnumerable<StatDrawEntry> values,
        RecipeDef __instance)
    {
        foreach (StatDrawEntry statDrawEntry in values)
            yield return statDrawEntry;
        IEnumerable<StatDrawEntry> stats = __instance
            .GetModExtension<ModExt_EnvironmentalBill>()?
            .SpecialDisplayStats();
        if (stats is null)
            yield break;
        foreach (StatDrawEntry statDrawEntry in stats)
            yield return statDrawEntry;
    }
}