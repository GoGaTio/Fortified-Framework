using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Fortified;

[HarmonyPatch(typeof(IncidentWorker), "TryExecute")]
public static class Patch_IncidentWorkerForDevice
{
    public static bool Prefix(IncidentWorker __instance, IncidentParms parms)
    {
        Map targetMap = parms.target as Map;

        var candidates = CompShieldingDevice
            .GetTowerByIncident(__instance.def)
            .Where(t =>
                t != null &&
                t.Spawned &&
                (targetMap == null || t.Map == targetMap) &&
                (t.Faction == null || t.Faction == Faction.OfPlayer))
            .ToList();

        if (candidates.NullOrEmpty())
        {
            return true;
        }
        Thing towerThing = candidates.RandomElement();
        CompShieldingDevice comp = towerThing.TryGetComp<CompShieldingDevice>();
        if (comp == null || !comp.Active)
        {
            return true;
        }

        // 啟動裝置（進入冷卻）
        comp.TriggerCooldown();

        // 視覺與訊息（盡量用安全、普遍存在的 Fleck / Message）
        try
        {
            if (towerThing.Map != null)
            {
                // 小型火花效果做為觸發提示
                FleckMaker.Static(towerThing.Position, towerThing.Map, FleckDefOf.PsycastAreaEffect, 1.2f);
                MoteMaker.ThrowText(towerThing.DrawPos, towerThing.Map, "Shielded", 2f);
            }
        }
        catch { /* 忽略美術特效失敗 */ }

        // 對玩家顯示攔截訊息
        string label = __instance.def.label ?? "incident";
        Messages.Message(
            "Shielding device intercepted: " + label.CapitalizeFirst(),
            new TargetInfo(towerThing.Position, towerThing.Map, false),
            MessageTypeDefOf.PositiveEvent
        );

        // 攔截成功 -> 阻止原始事件執行
        return false;
    }
}