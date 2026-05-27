using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Fortified
{
    // 侦察机
    public static class ScoutDroneProcedures
    {
        // 生成侦察机
        public static void SpawnScoutDrone(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext)
        {
            if (job.map == null) return;
            ThingDef droneDef = ext.droneFlyByThingDef;
            if (droneDef == null)
            {
                Log.Warning("[Fortified] ScoutedRaid AerialDrone未配置droneFlyByThingDef");
                return;
            }
            // 入射方向沿originAnchor穿越地图
            Vector3 dir = job.originAnchor;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir = dir.normalized;
            Vector3 mapCenter = job.map.Center.ToVector3Shifted();
            Vector3 origin = RayHitMapEdge(mapCenter, -dir, job.map);
            Vector3 targetPos = RayHitMapEdge(mapCenter, dir, job.map);
            // 推到边界外预留飞行余量
            origin -= dir * 6f;
            targetPos += dir * 6f;
            IntVec3 targetCell = new IntVec3(
                Mathf.Clamp(Mathf.RoundToInt(targetPos.x), 0, job.map.Size.x - 1),
                0,
                Mathf.Clamp(Mathf.RoundToInt(targetPos.z), 0, job.map.Size.z - 1));

            var drone = ThingMaker.MakeThing(droneDef);
            if (drone is FlyByThing flyby)
            {
                flyby.exactPos = origin;
                flyby.vector = targetPos - origin;
                IntVec3 spawnCell = new IntVec3(
                    Mathf.Clamp(Mathf.RoundToInt(origin.x), 0, job.map.Size.x - 1),
                    0,
                    Mathf.Clamp(Mathf.RoundToInt(origin.z), 0, job.map.Size.z - 1));
                GenSpawn.Spawn(drone, spawnCell, job.map);
                job.scoutDrone = drone;
            }
            else
            {
                Log.Warning($"[Fortified] ScoutedRaid droneFlyByThingDef={droneDef.defName}不是FlyByThing");
                drone.Destroy();
                return;
            }

            if (!ext.scoutLetterLabel.NullOrEmpty())
            {
                Find.LetterStack.ReceiveLetter(
                    ext.scoutLetterLabel.Translate(),
                    (ext.scoutLetterText ?? "").Translate(),
                    LetterDefOf.ThreatBig,
                    new TargetInfo(targetCell, job.map));
            }
        }

        // center沿dir射到地图边界
        private static Vector3 RayHitMapEdge(Vector3 center, Vector3 dir, Map map)
        {
            float w = map.Size.x;
            float h = map.Size.z;
            float t = float.PositiveInfinity;
            if (Mathf.Abs(dir.x) > 0.0001f)
            {
                float tx = dir.x > 0 ? (w - center.x) / dir.x : (0f - center.x) / dir.x;
                if (tx > 0 && tx < t) t = tx;
            }
            if (Mathf.Abs(dir.z) > 0.0001f)
            {
                float tz = dir.z > 0 ? (h - center.z) / dir.z : (0f - center.z) / dir.z;
                if (tz > 0 && tz < t) t = tz;
            }
            if (float.IsPositiveInfinity(t)) t = 0f;
            return center + dir * t;
        }

        // 侦察机存活与扫描
        public static bool TickDroneScan(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext, int now)
        {
            if (job.map == null) return false;
            if (job.scoutDrone == null || job.scoutDrone.Destroyed || !job.scoutDrone.Spawned)
            {
                return false;
            }
            int interval = Mathf.Max(1, ext.droneScanIntervalTicks);
            if (now % interval == 0)
            {
                ScoutMarkRegistry.EnsureCaches(job);
                ScanAndMark(job, ext, now);
            }
            return true;
        }

        // 矩形扫描带
        private static void ScanAndMark(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext, int now)
        {
            var attackCache = job.map.attackTargetsCache;
            if (attackCache == null) return;
            Faction markFaction = job.scoutFaction ?? job.mainFaction;
            if (markFaction == null) return;
            var hostiles = attackCache.TargetsHostileToFaction(markFaction);
            if (hostiles == null || hostiles.Count == 0) return;

            IntVec3 center = job.scoutDrone.Position;
            Vector3 fwd = job.originAnchor;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd = fwd.normalized;
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            float halfW = Mathf.Max(1f, ext.droneScanHalfWidth);
            float halfL = Mathf.Max(1f, ext.droneScanHalfLength);
            float halfW2 = halfW * halfW;
            float halfL2 = halfL * halfL;

            int globalCap = ext.maxTotalMarks;
            var globalLocked = ScoutMarkRegistry.BuildGlobalLockedSet(job);
            // 侦察机视角统一归到droneId
            int droneId = job.scoutDrone.thingIDNumber;
            if (!job.pawnLockedThings.TryGetValue(droneId, out var ownedSet))
            {
                ownedSet = new HashSet<int>();
                job.pawnLockedThings[droneId] = ownedSet;
            }

            foreach (var t in hostiles)
            {
                if (t == null) continue;
                Thing thing = t.Thing;
                if (thing == null || !thing.Spawned || thing.Destroyed) continue;
                Vector3 d = thing.Position.ToVector3Shifted() - center.ToVector3Shifted();
                float along = d.x * fwd.x + d.z * fwd.z;
                float across = d.x * right.x + d.z * right.z;
                if (along * along > halfL2) continue;
                if (across * across > halfW2) continue;
                if (!RoofTierUtility.CellPasses(job.map, thing.Position, ext.droneMarkRoofTier)) continue;
                int bw = ScoutMarkRegistry.ClassifyAndWeight(thing, ext, now);
                if (bw <= 0) continue;
                int tid = thing.thingIDNumber;
                if (ownedSet.Contains(tid))
                {
                    ScoutMarkRegistry.TouchOwnMark(job, droneId, tid, thing.Position, now);
                    continue;
                }
                if (globalLocked.Contains(tid)) continue;
                if (globalCap > 0 && job.marks.Count >= globalCap) break;
                int nearby = ScoutMarkRegistry.GetNearbyCached(job, thing, ext, now);
                float pri = ext.markBaseWeight + nearby * ext.nearbyBuildingWeight;
                ScoutMarkRegistry.AddOwnedMark(job, droneId, new ScoutMarkRegistry.MarkCandidate
                {
                    thing = thing,
                    thingId = tid,
                    cell = thing.Position,
                    priority = pri,
                }, now, ownedSet, globalLocked);
            }
        }

        // 强制结束侦察机
        public static void TerminateDrone(ScoutedRaidJob job)
        {
            if (job.scoutDrone != null && !job.scoutDrone.Destroyed && job.scoutDrone.Spawned)
            {
                job.scoutDrone.Destroy();
            }
            job.scoutDrone = null;
        }
    }
}
