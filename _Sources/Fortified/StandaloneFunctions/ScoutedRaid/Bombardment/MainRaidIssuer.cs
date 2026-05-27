using RimWorld;
using UnityEngine;
using Verse;

namespace Fortified
{
    // 主袭击发起
    public static class MainRaidIssuer
    {
        public static void Issue(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext)
        {
            // 列表优先单值兜底
            var arriveMode = WeightedPickUtility.PickArrivalMode(ext.mainArriveModePool, ext.mainArriveMode);
            var strategy = WeightedPickUtility.PickStrategy(ext.mainStrategyPool, ext.mainStrategyDef);
            var parms = new IncidentParms
            {
                target = job.map,
                faction = job.mainFaction,
                points = Mathf.Max(50f, job.originalPoints * ext.mainPointsFactor),
                raidArrivalMode = arriveMode,
                raidStrategy = strategy,
            };
            // 复用侦察解析的进入点
            if (job.spawnLocked && job.lockedSpawnCenter.IsValid && job.lockedSpawnCenter.InBounds(job.map))
            {
                parms.spawnCenter = job.lockedSpawnCenter;
                parms.spawnRotation = job.lockedSpawnRotation;
            }
            // 仅原版信件保留小人列表自定义信件已废弃
            if (!IncidentDefOf.RaidEnemy.Worker.TryExecute(parms))
            {
                Log.Warning("[Fortified] ScoutedRaid主袭击执行失败");
            }
        }
    }
}
