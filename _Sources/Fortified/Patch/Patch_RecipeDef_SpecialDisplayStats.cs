using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Fortified;

[HarmonyPatch(typeof(RecipeDef), nameof(RecipeDef.SpecialDisplayStats))]
internal static class Patch_RecipeDef_SpecialDisplayStats
{
    // 参考阅读: https://harmony.pardeike.net/articles/patching-postfix.html#pass-through-postfixes
    public static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> values)
    {
        foreach (StatDrawEntry statDrawEntry in values)
            yield return statDrawEntry;

        // 把额外需要返回的StatDrawEntry直接写到这就完事了
    }
}
