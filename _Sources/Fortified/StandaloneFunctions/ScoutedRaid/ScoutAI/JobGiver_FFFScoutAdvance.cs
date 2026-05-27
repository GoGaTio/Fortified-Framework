using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Fortified
{
    // 侦察兵主动深入玩家基地选取未近距访问的玩家建筑作目的地
    public class JobGiver_FFFScoutAdvance : ThinkNode_JobGiver
    {
        // 单次行动expire上限tick内会被重新规划
        public int jobMaxDuration = 1500;
        // 视为已访问的距离格
        public float visitedRange = 6f;
        // 候选建筑采样上限
        public int sampleLimit = 24;
        // 偏向更深的随机抖动
        public float depthJitter = 0.25f;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            var obj = (JobGiver_FFFScoutAdvance)base.DeepCopy(resolve);
            obj.jobMaxDuration = jobMaxDuration;
            obj.visitedRange = visitedRange;
            obj.sampleLimit = sampleLimit;
            obj.depthJitter = depthJitter;
            return obj;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var map = pawn.Map;
            if (map == null) return null;
            var list = map.listerBuildings?.allBuildingsColonist;
            if (list == null || list.Count == 0) return null;
            IntVec3 dest = PickAdvanceTarget(pawn, list);
            if (!dest.IsValid) return null;
            var job = JobMaker.MakeJob(JobDefOf.Goto, dest);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            job.expiryInterval = jobMaxDuration;
            job.checkOverrideOnExpire = true;
            job.canBashDoors = false;
            job.canBashFences = false;
            return job;
        }

        // 选择最远未访问玩家建筑附近一格
        private IntVec3 PickAdvanceTarget(Pawn pawn, List<Building> list)
        {
            float bestScore = float.MinValue;
            IntVec3 bestCell = IntVec3.Invalid;
            int sampled = 0;
            float visitR2 = visitedRange * visitedRange;
            int step = Mathf.Max(1, list.Count / Mathf.Max(1, sampleLimit));
            int start = Rand.RangeInclusive(0, step - 1);
            for (int i = start; i < list.Count; i += step)
            {
                var b = list[i];
                if (b == null || !b.Spawned || b.Destroyed) continue;
                if ((b.Position - pawn.Position).LengthHorizontalSquared <= visitR2) continue;
                if (!pawn.CanReach(b.Position, PathEndMode.Touch, Danger.Deadly)) continue;
                float depth = (b.Position - pawn.Position).LengthHorizontal;
                float score = depth * (1f + Rand.Range(-depthJitter, depthJitter));
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = ResolveApproachCell(pawn, b);
                }
                sampled++;
                if (sampled >= sampleLimit) break;
            }
            return bestCell;
        }

        // 在建筑相邻格里挑一个可达的
        private static IntVec3 ResolveApproachCell(Pawn pawn, Building b)
        {
            foreach (var c in GenAdjFast.AdjacentCells8Way(b))
            {
                if (!c.InBounds(pawn.Map)) continue;
                if (!c.Standable(pawn.Map)) continue;
                if (c.IsForbidden(pawn)) continue;
                if (!pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly)) continue;
                return c;
            }
            return b.Position;
        }
    }
}
