using HarmonyLib;
using UnityEngine;
using Verse;

namespace Fortified;
[HarmonyPatch(typeof(DarklightUtility), nameof(DarklightUtility.IsDarklight))]
public static class Patch_DarklightUtility_IsDarklight
{
    public static void Postfix(Color color, ref bool __result)
    {
        if (__result) return;           // vanilla already recognised it — nothing to do
        __result = IsRedDarklight(color);
    }
    private static bool IsRedDarklight(Color color)
    {
        // Red must be the dominant channel.
        if (color.g > color.r || color.b > color.r)
            return false;

        float maxMinor = Mathf.Max(color.g, color.b);
        float minMinor = Mathf.Min(color.g, color.b);

        // Red must be non-zero to avoid matching pure black.
        if (color.r == 0f)
            return false;

        // The minor channels must each be less than half of red
        // (mirrors: r > num/2f → false in vanilla).
        if (maxMinor > color.r / 2f)
            return false;

        // When both minor channels are non-zero they must be similar
        // (mirrors: num2/num <= 0.85f → false in vanilla).
        // If maxMinor == 0 (pure red), the channels are trivially equal — allow it.
        if (maxMinor > 0f && minMinor / maxMinor <= 0.85f)
            return false;

        return true;
    }
}
