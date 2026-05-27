using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Fortified
{
    // 标记注册中心
    public static class ScoutMarkRegistry
    {
        // 候选标记
        public struct MarkCandidate
        {
            public Thing thing;
            public int thingId;
            public IntVec3 cell;
            public float priority;
        }

        // 运行时索引兜底
        public static void EnsureCaches(ScoutedRaidJob job)
        {
            if (job.pawnLockedThings == null) job.pawnLockedThings = new Dictionary<int, HashSet<int>>();
            if (job.markIndex == null) job.markIndex = new Dictionary<long, ScoutMarkEntry>();
            if (job.nearbyCountCache == null) job.nearbyCountCache = new Dictionary<int, int>();
            if (job.nearbyTickCache == null) job.nearbyTickCache = new Dictionary<int, int>();
            if (job.lastSeenScoutCell == null) job.lastSeenScoutCell = new Dictionary<int, IntVec3>();
        }

        // 全局已锁thingId
        public static HashSet<int> BuildGlobalLockedSet(ScoutedRaidJob job)
        {
            var set = new HashSet<int>();
            foreach (var kv in job.pawnLockedThings)
            {
                foreach (var tid in kv.Value) set.Add(tid);
            }
            return set;
        }

        // 是否还有其它pawn持有
        public static bool IsThingLockedByAny(ScoutedRaidJob job, int thingId, int excludePawn)
        {
            foreach (var kv in job.pawnLockedThings)
            {
                if (kv.Key == excludePawn) continue;
                if (kv.Value.Contains(thingId)) return true;
            }
            return false;
        }

        // 写入新mark
        public static void AddOwnedMark(ScoutedRaidJob job, int pawnId, MarkCandidate c, int now,
            HashSet<int> ownedSet, HashSet<int> globalLocked)
        {
            var entry = new ScoutMarkEntry
            {
                cell = c.cell,
                recordedTick = now,
                weight = Mathf.Max(1, Mathf.RoundToInt(c.priority)),
                thingId = c.thingId,
                ownerPawnId = pawnId,
            };
            job.marks.Add(entry);
            ownedSet.Add(c.thingId);
            globalLocked.Add(c.thingId);
            long key = ((long)pawnId << 32) | (uint)c.thingId;
            job.markIndex[key] = entry;
        }

        // 自有标记仅刷新
        public static void TouchOwnMark(ScoutedRaidJob job, int pawnId, int thingId, IntVec3 cell, int now)
        {
            long key = ((long)pawnId << 32) | (uint)thingId;
            if (job.markIndex.TryGetValue(key, out var m))
            {
                m.recordedTick = now;
                m.cell = cell;
            }
        }

        // 集合最低优先级取代
        public static bool TryReplaceLowest(ScoutedRaidJob job, int pawnId, MarkCandidate c, int now,
            HashSet<int> ownedSet, HashSet<int> globalLocked)
        {
            ScoutMarkEntry lowest = null;
            float lowestPri = float.MaxValue;
            foreach (var tid in ownedSet)
            {
                long key = ((long)pawnId << 32) | (uint)tid;
                if (!job.markIndex.TryGetValue(key, out var m)) continue;
                float pri = m.weight;
                if (pri < lowestPri)
                {
                    lowestPri = pri;
                    lowest = m;
                }
            }
            if (lowest == null) return false;
            if (c.priority <= lowestPri) return false;
            RemoveMark(job, lowest);
            ownedSet.Remove(lowest.thingId);
            if (!IsThingLockedByAny(job, lowest.thingId, pawnId)) globalLocked.Remove(lowest.thingId);
            AddOwnedMark(job, pawnId, c, now, ownedSet, globalLocked);
            return true;
        }

        public static void RemoveMark(ScoutedRaidJob job, ScoutMarkEntry m)
        {
            int idx = job.marks.IndexOf(m);
            if (idx >= 0) RemoveMarkAt(job, idx);
        }

        public static void RemoveMarkAt(ScoutedRaidJob job, int idx)
        {
            var m = job.marks[idx];
            job.marks.RemoveAt(idx);
            long key = ((long)m.ownerPawnId << 32) | (uint)m.thingId;
            job.markIndex?.Remove(key);
            if (job.pawnLockedThings != null && job.pawnLockedThings.TryGetValue(m.ownerPawnId, out var set))
            {
                set.Remove(m.thingId);
            }
        }

        // 邻域玩家建筑数缓存
        public static int GetNearbyCached(ScoutedRaidJob job, Thing thing, IncidentExtension_ScoutedRaid ext, int now)
        {
            int tid = thing.thingIDNumber;
            if (job.nearbyTickCache.TryGetValue(tid, out int t)
                && now - t < Mathf.Max(60, ext.nearbyCacheTicks)
                && job.nearbyCountCache.TryGetValue(tid, out int c))
            {
                return c;
            }
            int n = CountPlayerBuildingsNear(thing.Position, job.map, ext.emplacementCheckRadius);
            job.nearbyCountCache[tid] = n;
            job.nearbyTickCache[tid] = now;
            return n;
        }

        // 目标分类0不可标记1可标记
        public static int ClassifyAndWeight(Thing thing, IncidentExtension_ScoutedRaid ext, int now)
        {
            if (thing is Building_Turret turret)
            {
                return turret.ThreatDisabled(null) ? 0 : 1;
            }
            if (thing is Pawn pawn)
            {
                if (pawn.Faction != Faction.OfPlayer) return 0;
                if (pawn.Downed || pawn.Dead) return 0;
                int lastFire = pawn.mindState?.lastAttackTargetTick ?? -99999;
                if (now - lastFire > ext.recentFireWindowTicks) return 0;
                return 1;
            }
            return 0;
        }

        // 半径内玩家建筑数
        private static int CountPlayerBuildingsNear(IntVec3 center, Map map, float radius)
        {
            var list = map.listerBuildings?.allBuildingsColonist;
            if (list == null) return 0;
            float r2 = radius * radius;
            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if ((list[i].Position - center).LengthHorizontalSquared <= r2) count++;
            }
            return count;
        }
    }
}
