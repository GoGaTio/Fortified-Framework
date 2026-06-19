using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Fortified
{
    // Hediff信息卡补充阻挡配置
    [HarmonyPatch(typeof(Hediff), nameof(Hediff.SpecialDisplayStats))]
    internal static class Patch_Hediff_SpecialDisplayStats
    {
        public static IEnumerable<StatDrawEntry> Postfix(
            IEnumerable<StatDrawEntry> values,
            Hediff __instance)
        {
            foreach (StatDrawEntry entry in values)
                yield return entry;

            HediffComp_DamageBlocker comp = (__instance as HediffWithComps)?.TryGetComp<HediffComp_DamageBlocker>();
            if (comp == null)
                yield break;

            foreach (StatDrawEntry entry in DamageBlockerDisplayUtility.BuildStats(comp.PropsForDisplay.BuildStatData()))
                yield return entry;
        }
    }
}
