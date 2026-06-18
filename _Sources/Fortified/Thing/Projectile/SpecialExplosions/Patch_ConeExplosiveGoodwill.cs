using HarmonyLib;
using RimWorld;
using Verse;

namespace Fortified
{
    /// <summary>
    /// 當敵對 Pawn 發射 Projectile_ConeExplosive 時，
    /// 攔截 Faction.TryAffectGoodwillWith，阻止友好 NPC 的陣營對玩家產生好感度懲罰。
    ///
    /// 觸發條件：
    ///   1. Projectile_ConeExplosive.SuppressConeExplosiveGoodwillPenalty 旗標為 true
    ///      （代表目前爆炸的發射者屬於敵對陣營）
    ///   2. 此次好感度變動為負數（懲罰）
    ///   3. 牽涉到玩家陣營（OfPlayer 為 __instance 或 other 之一）
    /// </summary>
    [HarmonyPatch(typeof(Faction), nameof(Faction.TryAffectGoodwillWith))]
    public static class Patch_ConeExplosiveGoodwill
    {
        [HarmonyPrefix]
        public static bool Prefix(Faction __instance, Faction other, int goodwillChange)
        {
            // 快速路徑：旗標未設定時不做任何處理，直接執行原邏輯
            if (!Projectile_ConeExplosive.SuppressConeExplosiveGoodwillPenalty)
                return true;

            // 只抑制負面好感度變動
            if (goodwillChange >= 0)
                return true;

            // 只抑制牽涉到玩家陣營的變動
            // 防禦性判斷：若 OfPlayer 尚未初始化則直接放行
            Faction playerFaction = Faction.OfPlayer;
            if (playerFaction == null)
                return true;

            if (__instance == playerFaction || other == playerFaction)
            {
                // 跳過此次好感度減少
                return false;
            }

            return true;
        }
    }
}
