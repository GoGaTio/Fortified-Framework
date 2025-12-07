using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Fortified
{
    /// <summary>
    /// 在 Pawn 生成階段添加 HumanlikeMech 的初始裝備
    /// 使用 PawnKindDef 中的原生 apparelMoney、apparelTags 和 apparelRequired 欄位
    /// 參考 RimWorld 的 PawnApparelGenerator 邏輯
    /// 
    /// 注：RimWorld 的 PawnApparelGenerator 還支持 specificApparelRequirements。
    /// 該功能可通過衣服的 ApparelRequirement 系統實現，該系統在 pawn.apparel.AllRequirements 中使用。
    /// 詳見 HumanlikeMechApparelUtility.ApparelScoreRaw 的實現。
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn),
        new Type[] { typeof(PawnKindDef), typeof(Faction), typeof(PlanetTile?) })]
    internal static class Patch_PawnGenerator_GeneratePawn
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __result, PawnKindDef kindDef, Faction faction, PlanetTile? tile)
        {
            if (__result is not HumanlikeMech humanlikeMech || kindDef == null || humanlikeMech.apparel == null)
            {
                return;
            }
            if (faction.IsPlayer) return;// 只為非玩家陣營的機兵添加初始衣服

            PawnGenerationRequest request = new PawnGenerationRequest(kindDef);
            MechApparelGenerator.GenerateStartingApparelFor(humanlikeMech, request);

            return;
            // 穿上必需的衣服（apparelRequired）
            if (!kindDef.apparelRequired.NullOrEmpty())
            {
                foreach (var apparelDef in kindDef.apparelRequired)
                {
                    TryWearApparel(humanlikeMech, apparelDef);
                }
            }
            // 根據標籤和預算穿上可選的衣服
            float apparelBudget = kindDef.apparelMoney.RandomInRange;
            if (apparelBudget > 0 && !kindDef.apparelTags.NullOrEmpty())
            {
                var candidates = GetApparelsByTags(kindDef.apparelTags);
                if (candidates.Count > 0)
                {
                    candidates.Shuffle();
                    float remainingMoney = apparelBudget;

                    foreach (var apparelDef in candidates)
                    {
                        if (remainingMoney <= 0)
                        {
                            break;
                        }

                        float marketValue = apparelDef.GetStatValueAbstract(StatDefOf.MarketValue);
                        if (marketValue <= remainingMoney && TryWearApparel(humanlikeMech, apparelDef))
                        {
                            remainingMoney -= marketValue;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 嘗試為 pawn 穿上衣服
        /// </summary>
        private static bool TryWearApparel(HumanlikeMech pawn, ThingDef apparelDef)
        {
            if (!apparelDef.IsApparel ||
                !apparelDef.apparel.CorrectGenderForWearing(pawn.gender) ||
                !ApparelUtility.HasPartsToWear(pawn, apparelDef))
            {
                return false;
            }

            // 檢查與現有衣服的兼容性
            var wornApparel = pawn.apparel.WornApparel;
            if (wornApparel.Count > 0 && !wornApparel.All(worn =>
                ApparelUtility.CanWearTogether(worn.def, apparelDef, pawn.RaceProps.body)))
            {
                return false;
            }

            Apparel apparel = MechApparelGenerator.GenerateApparelOfDefFor(pawn, apparelDef);
            if (apparel == null)
            {
                return false;
            }

            pawn.apparel.Wear(apparel);
            return true;
        }

        /// <summary>
        /// 獲取符合標籤的衣服定義
        /// </summary>
        private static List<ThingDef> GetApparelsByTags(List<string> tags)
        {
            return DefDatabase<ThingDef>.AllDefs
                .Where(def => def.IsApparel &&
                       !def.apparel?.tags.NullOrEmpty() == true &&
                       tags.Any(tag => def.apparel.tags.Contains(tag)))
                .ToList();
        }
    }

}