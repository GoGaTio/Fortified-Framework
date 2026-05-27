using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Fortified
{
    // 侦察袭击入口
    public class IncidentWorker_FFF_ScoutedRaid : IncidentWorker_RaidEnemy
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms)) return false;
            return parms.target is Map;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!(parms.target is Map map)) return false;
            var ext = def.GetModExtension<IncidentExtension_ScoutedRaid>();
            if (ext == null)
            {
                Log.Error($"[Fortified] {def.defName}缺少IncidentExtension_ScoutedRaid");
                return false;
            }

            // 战力兜底
            if (parms.points <= 0f)
            {
                parms.points = StorytellerUtility.DefaultThreatPointsNow(map);
            }
            float originalPoints = parms.points;

            // 主袭击派系列表优先单值兜底
            FactionDef mainFactionDef = WeightedPickUtility.PickFactionDef(ext.mainFactionPool, ext.mainFactionDef);
            Faction mainFaction = ResolveFactionFor(parms, mainFactionDef);
            if (mainFaction == null)
            {
                Log.Warning("[Fortified] ScoutedRaid无法解析主袭击派系");
                return false;
            }

            // 侦察派系默认与主一致
            FactionDef scoutFactionDef = WeightedPickUtility.PickFactionDef(ext.scoutFactionPool, ext.scoutFactionDef);
            Faction scoutFaction = ResolveFactionFor(parms, scoutFactionDef) ?? mainFaction;

            // 注册Job
            var ctrl = FFF_ScoutedRaidController.Instance;
            if (ctrl == null)
            {
                Log.Error("[Fortified] ScoutedRaid Controller未初始化");
                return false;
            }
            var job = new ScoutedRaidJob
            {
                sourceIncidentDef = def,
                map = map,
                scoutFaction = scoutFaction,
                mainFaction = mainFaction,
                originalPoints = originalPoints,
                phase = ScoutedRaidPhase.ScoutInbound,
                phaseEndTick = Find.TickManager.TicksGame,
                originAnchor = ResolveOriginAnchor(ext),
            };
            ctrl.RegisterJob(job);
            return true;
        }

        // 选定派系
        private Faction ResolveFactionFor(IncidentParms parms, FactionDef factionDef)
        {
            if (factionDef != null)
            {
                Faction match = Find.FactionManager.AllFactions
                    .Where(f => f.def == factionDef && f.HostileTo(Faction.OfPlayer) && !f.defeated)
                    .FirstOrDefault();
                if (match != null) return match;
            }
            // 走原版选取
            var probe = new IncidentParms
            {
                target = parms.target,
                points = parms.points,
                faction = parms.faction,
            };
            if (TryResolveRaidFaction(probe))
            {
                return probe.faction;
            }
            return parms.faction;
        }

        // 锁定北向锥内入射方向
        private static Vector3 ResolveOriginAnchor(IncidentExtension_ScoutedRaid ext)
        {
            float cone = Mathf.Max(0f, ext?.flareConeAngleDegrees ?? 25f);
            float angle = Rand.Range(-cone, cone);
            float rad = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }
    }
}
