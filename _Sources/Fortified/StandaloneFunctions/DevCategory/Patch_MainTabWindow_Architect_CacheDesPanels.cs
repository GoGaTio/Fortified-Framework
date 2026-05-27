using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
namespace Fortified
{
    [HarmonyPatch(typeof(MainTabWindow_Architect), "CacheDesPanels")]
    public static class Patch_MainTabWindow_Architect_CacheDesPanels
    {
        private const string TargetCategoryDefName = "FFF_DevCategory";
        // AccessTools：高效 FieldRef（避免每次 FieldInfo.GetValue）
        private static readonly AccessTools.FieldRef<MainTabWindow_Architect, List<ArchitectCategoryTab>> DesPanelsCachedRef =
            AccessTools.FieldRefAccess<MainTabWindow_Architect, List<ArchitectCategoryTab>>("desPanelsCached");

        private static readonly AccessTools.FieldRef<MainTabWindow_Architect, QuickSearchWidget> QuickSearchWidgetRef =
            AccessTools.FieldRefAccess<MainTabWindow_Architect, QuickSearchWidget>("quickSearchWidget");

        // 你的目標分類 defName
        private static readonly HashSet<string> HiddenInNonDevMode = new(StringComparer.Ordinal)
        {
            TargetCategoryDefName
        };

        // Prefix 直接接管原方法：少一次「先建後刪」
        public static bool Prefix(MainTabWindow_Architect __instance)
        {
            var list = DesPanelsCachedRef(__instance);
            var filter = QuickSearchWidgetRef(__instance).filter;
            bool devMode = Prefs.DevMode;

            // 盡量重用既有 List，降低 GC
            list.Clear();

            foreach (var def in DefDatabase<DesignationCategoryDef>.AllDefs.OrderByDescending(d => d.order))
            {
                if (!devMode && HiddenInNonDevMode.Contains(def.defName))
                    continue;

                list.Add(new ArchitectCategoryTab(def, filter));
            }

            // 若目前選中分類不在新列表中，清掉
            if (__instance.selectedDesPanel != null)
            {
                var selectedDefName = __instance.selectedDesPanel.def?.defName;
                if (selectedDefName != null && (!devMode && HiddenInNonDevMode.Contains(selectedDefName)))
                {
                    __instance.selectedDesPanel = null;
                }
            }

            return false; // 跳過原版 CacheDesPanels
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Architect), nameof(MainTabWindow_Architect.PreOpen))]
    public static class Patch_MainTabWindow_Architect_PreOpen
    {
        // AccessTools：快取 private 方法委派（避免 MethodInfo.Invoke）
        private static readonly Action<MainTabWindow_Architect> CacheDesPanelsCall =
            AccessTools.MethodDelegate<Action<MainTabWindow_Architect>>(
                AccessTools.Method(typeof(MainTabWindow_Architect), "CacheDesPanels"));

        private static readonly Action<MainTabWindow_Architect> CacheSearchStateCall =
            AccessTools.MethodDelegate<Action<MainTabWindow_Architect>>(
                AccessTools.Method(typeof(MainTabWindow_Architect), "CacheSearchState"));

        private static bool? lastDevMode;

        public static void Postfix(MainTabWindow_Architect __instance)
        {
            bool current = Prefs.DevMode && DebugSettings.godMode;
            if (lastDevMode.HasValue && lastDevMode.Value == current)
                return;

            lastDevMode = current;
            CacheDesPanelsCall(__instance);
            CacheSearchStateCall(__instance);
        }
    }
}