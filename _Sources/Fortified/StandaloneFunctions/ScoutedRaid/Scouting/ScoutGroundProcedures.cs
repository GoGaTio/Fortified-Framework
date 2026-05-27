using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Fortified
{
    // 地面侦察
    public static class ScoutGroundProcedures
    {
        // 生成侦察小队
        public static void SpawnScoutWave(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext)
        {
            if (job.map == null || job.scoutFaction == null) return;
            var parms = BuildScoutParms(job, ext);
            if (!ResolveSpawnCenter(job, parms)) return;
            var groupParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(parms.pawnGroupKind, parms);
            var pawns = PawnGroupMakerUtility.GeneratePawns(groupParms).ToList();
            if (pawns.Count == 0)
            {
                Log.Warning("[Fortified] ScoutedRaid侦察组生成为空");
                return;
            }
            parms.raidArrivalMode.Worker.Arrive(pawns, parms);
            var lordJob = new LordJob_FFFScoutAssault(job.scoutFaction, ext.scoutAvoidEnemyRanges);
            job.scoutLord = LordMaker.MakeNewLord(job.scoutFaction, lordJob, job.map, pawns);
            SendScoutLetter(ext, pawns[0]);
        }

        // 装配IncidentParms
        private static IncidentParms BuildScoutParms(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext)
        {
            int cycles = Mathf.Max(1, ext.cycleCount);
            float perCyclePoints = job.originalPoints * ext.scoutPointsFactor / cycles;
            var arriveMode = WeightedPickUtility.PickArrivalMode(ext.scoutArriveModePool, ext.scoutArriveMode) ?? PawnsArrivalModeDefOf.EdgeWalkIn;
            var preferredKind = WeightedPickUtility.PickPawnGroupKind(ext.scoutGroupKindPool, ext.scoutGroupKind) ?? PawnGroupKindDefOf.Combat;
            var groupKind = WeightedPickUtility.ResolvePawnGroupKindForFaction(job.scoutFaction, preferredKind, ext.scoutGroupKindPool);
            return new IncidentParms
            {
                target = job.map,
                faction = job.scoutFaction,
                points = Mathf.Max(ext.scoutMinPoints, perCyclePoints),
                raidArrivalMode = arriveMode,
                raidStrategy = RaidStrategyDefOf.ImmediateAttack,
                pawnGroupKind = groupKind,
                silent = true,
            };
        }

        // 已锁定复用否则解析后回写
        private static bool ResolveSpawnCenter(ScoutedRaidJob job, IncidentParms parms)
        {
            if (job.spawnLocked && job.lockedSpawnCenter.IsValid && job.lockedSpawnCenter.InBounds(job.map))
            {
                parms.spawnCenter = job.lockedSpawnCenter;
                parms.spawnRotation = job.lockedSpawnRotation;
                return true;
            }
            if (!parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms))
            {
                Log.Warning("[Fortified] ScoutedRaid无法解析侦察生成点");
                return false;
            }
            job.lockedSpawnCenter = parms.spawnCenter;
            job.lockedSpawnRotation = parms.spawnRotation;
            job.spawnLocked = true;
            return true;
        }

        private static void SendScoutLetter(IncidentExtension_ScoutedRaid ext, Thing pivot)
        {
            if (ext.scoutLetterLabel.NullOrEmpty()) return;
            Find.LetterStack.ReceiveLetter(
                ext.scoutLetterLabel.Translate(),
                (ext.scoutLetterText ?? "").Translate(),
                LetterDefOf.ThreatBig,
                pivot);
        }

        // 损失比例触发撤离
        public static bool ShouldTriggerLossWithdraw(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext)
        {
            if (job.scoutLord == null) return true;
            int gained = Mathf.Max(1, job.scoutLord.numPawnsEverGained);
            int lost = job.scoutLord.numPawnsLostViolently;
            if (job.scoutLord.ownedPawns.Count == 0) return true;
            return (float)lost / gained >= ext.scoutLossWithdrawFraction;
        }

        // 标记容量耗尽全局上限达到或全员持满
        public static bool ShouldTriggerMarksExhaustedWithdraw(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext)
        {
            if (!ext.withdrawWhenMarksExhausted) return false;
            if (job.scoutLord == null) return false;
            if (ext.maxTotalMarks > 0 && job.marks.Count >= ext.maxTotalMarks) return true;
            int cap = Mathf.Max(1, ext.maxMarksPerPawn);
            int alive = 0;
            int saturated = 0;
            foreach (var p in job.scoutLord.ownedPawns)
            {
                if (p == null || p.Destroyed || p.Dead || !p.Spawned) continue;
                alive++;
                if (job.pawnLockedThings != null
                    && job.pawnLockedThings.TryGetValue(p.thingIDNumber, out var owned)
                    && owned.Count >= cap)
                {
                    saturated++;
                }
            }
            return alive > 0 && saturated >= alive;
        }

        // 走原版PanicFlee流程
        public static void OrderScoutsExit(ScoutedRaidJob job)
        {
            if (job.scoutLord == null) return;
            try
            {
                var lord = job.scoutLord;
                Faction fac = lord.faction;
                if (fac == null || lord.Map == null || !lord.Map.CanEverExit)
                {
                    job.scoutLord = null;
                    return;
                }
                var graph = lord.Graph;
                var existing = graph.lordToils.Find(t => t is LordToil_PanicFlee);
                if (existing == null)
                {
                    existing = new LordToil_PanicFlee { useAvoidGrid = true };
                    graph.AddToil(existing);
                    existing.lord = lord;
                }
                Messages.Message(
                    "MessageFightersFleeing".Translate(fac.def.pawnsPlural.CapitalizeFirst(), fac.Name),
                    new TargetInfo(lord.ownedPawns.FirstOrDefault()?.Position ?? IntVec3.Invalid, lord.Map),
                    MessageTypeDefOf.ThreatBig);
                lord.GotoToil(existing);
                job.scoutLord = null;
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[Fortified] ScoutedRaid撤离切换失败 {ex.Message}");
            }
        }

        // 视野扫描转化为标记
        public static void HarvestMarks(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext)
        {
            if (job.scoutLord == null || job.map == null) return;
            int now = Find.TickManager.TicksGame;
            ScoutMarkRegistry.EnsureCaches(job);
            CleanExpiredMarks(job, ext, now);
            FallbackMarksForDeadEmptyHanded(job, ext, now);

            var attackCache = job.map.attackTargetsCache;
            if (attackCache == null) return;
            var hostiles = attackCache.TargetsHostileToFaction(job.scoutFaction);
            if (hostiles == null || hostiles.Count == 0)
            {
                RefreshLastSeenCells(job);
                return;
            }

            float sightR2Default = ext.scoutSightRadius * ext.scoutSightRadius;
            var globalLocked = ScoutMarkRegistry.BuildGlobalLockedSet(job);
            int globalCap = ext.maxTotalMarks;
            var perPawnBuf = new List<ScoutMarkRegistry.MarkCandidate>();

            foreach (var p in job.scoutLord.ownedPawns)
            {
                if (p == null || p.Destroyed || p.Dead || !p.Spawned) continue;
                float sightR2 = ResolveSightR2(p, ext, sightR2Default);
                CollectCandidatesFor(p, job, ext, hostiles, sightR2, now, perPawnBuf);
                AssignMarksFor(p, job, ext, perPawnBuf, globalLocked, globalCap, now);
            }
            RefreshLastSeenCells(job);
        }

        // 刷新存活侦察兵最后位置缓存
        private static void RefreshLastSeenCells(ScoutedRaidJob job)
        {
            if (job.scoutLord == null || job.lastSeenScoutCell == null) return;
            foreach (var p in job.scoutLord.ownedPawns)
            {
                if (p == null) continue;
                if (p.Dead || p.Destroyed || !p.Spawned) continue;
                job.lastSeenScoutCell[p.thingIDNumber] = p.Position;
            }
        }

        // 死者空手补标按最后位置就近找一个玩家建筑写一发
        private static void FallbackMarksForDeadEmptyHanded(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext, int now)
        {
            if (!ext.deadScoutMarksFallback) return;
            if (job.lastSeenScoutCell == null || job.lastSeenScoutCell.Count == 0) return;
            var alivePawnIds = CollectAlivePawnIds(job);
            var globalLocked = ScoutMarkRegistry.BuildGlobalLockedSet(job);
            int globalCap = ext.maxTotalMarks;
            List<int> toRemove = null;
            foreach (var kv in job.lastSeenScoutCell)
            {
                int pawnId = kv.Key;
                if (alivePawnIds.Contains(pawnId)) continue;
                if (toRemove == null) toRemove = new List<int>();
                toRemove.Add(pawnId);
                bool emptyHanded = !job.pawnLockedThings.TryGetValue(pawnId, out var owned) || owned.Count == 0;
                if (!emptyHanded) continue;
                if (globalCap > 0 && job.marks.Count >= globalCap) continue;
                TryWriteFallbackMark(job, ext, pawnId, kv.Value, globalLocked, now);
            }
            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++) job.lastSeenScoutCell.Remove(toRemove[i]);
            }
        }

        // 当前lord存活成员id
        private static HashSet<int> CollectAlivePawnIds(ScoutedRaidJob job)
        {
            var set = new HashSet<int>();
            if (job.scoutLord == null) return set;
            foreach (var p in job.scoutLord.ownedPawns)
            {
                if (p == null) continue;
                if (p.Dead || p.Destroyed || !p.Spawned) continue;
                set.Add(p.thingIDNumber);
            }
            return set;
        }

        // 就近选玩家建筑并落mark
        private static void TryWriteFallbackMark(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext,
            int pawnId, IntVec3 lastCell, HashSet<int> globalLocked, int now)
        {
            var buildings = job.map.listerBuildings?.allBuildingsColonist;
            if (buildings == null || buildings.Count == 0) return;
            Building best = null;
            int bestDist2 = int.MaxValue;
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b == null || !b.Spawned || b.Destroyed) continue;
                int tid = b.thingIDNumber;
                if (globalLocked.Contains(tid)) continue;
                if (!RoofTierUtility.CellPasses(job.map, b.Position, ext.scoutMarkRoofTier)) continue;
                int d2 = (b.Position - lastCell).LengthHorizontalSquared;
                if (d2 < bestDist2)
                {
                    bestDist2 = d2;
                    best = b;
                }
            }
            if (best == null) return;
            if (!job.pawnLockedThings.TryGetValue(pawnId, out var ownedSet))
            {
                ownedSet = new HashSet<int>();
                job.pawnLockedThings[pawnId] = ownedSet;
            }
            int nearby = ScoutMarkRegistry.GetNearbyCached(job, best, ext, now);
            float pri = ext.markBaseWeight + nearby * ext.nearbyBuildingWeight;
            ScoutMarkRegistry.AddOwnedMark(job, pawnId, new ScoutMarkRegistry.MarkCandidate
            {
                thing = best,
                thingId = best.thingIDNumber,
                cell = best.Position,
                priority = pri,
            }, now, ownedSet, globalLocked);
        }

        // 单兵视野半径平方
        private static float ResolveSightR2(Pawn p, IncidentExtension_ScoutedRaid ext, float defaultR2)
        {
            if (!ext.weaponRangeExtendsSight
                && ext.weaponDefNameBonuses.NullOrEmpty()
                && ext.apparelDefNameBonuses.NullOrEmpty())
            {
                return defaultR2;
            }
            float r = ScoutSightCalculator.Compute(p, ext);
            return r * r;
        }

        // 清理过期标记
        private static void CleanExpiredMarks(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext, int now)
        {
            for (int i = job.marks.Count - 1; i >= 0; i--)
            {
                var m = job.marks[i];
                if (now - m.recordedTick > ext.maxMarkAgeTicks)
                {
                    ScoutMarkRegistry.RemoveMarkAt(job, i);
                }
            }
        }

        // 单兵视野内候选
        private static void CollectCandidatesFor(Pawn p, ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext,
            System.Collections.Generic.IEnumerable<IAttackTarget> hostiles, float sightR2, int now,
            List<ScoutMarkRegistry.MarkCandidate> buf)
        {
            buf.Clear();
            IntVec3 eye = p.Position;
            foreach (var t in hostiles)
            {
                if (t == null) continue;
                Thing thing = t.Thing;
                if (thing == null || !thing.Spawned || thing.Destroyed) continue;
                if ((thing.Position - eye).LengthHorizontalSquared > sightR2) continue;
                if (!GenSight.LineOfSight(eye, thing.Position, job.map, skipFirstCell: true)) continue;
                if (!RoofTierUtility.CellPasses(job.map, thing.Position, ext.scoutMarkRoofTier)) continue;
                int bw = ScoutMarkRegistry.ClassifyAndWeight(thing, ext, now);
                if (bw <= 0) continue;
                int nearby = ScoutMarkRegistry.GetNearbyCached(job, thing, ext, now);
                float pri = ext.markBaseWeight + nearby * ext.nearbyBuildingWeight;
                buf.Add(new ScoutMarkRegistry.MarkCandidate
                {
                    thing = thing,
                    thingId = thing.thingIDNumber,
                    cell = thing.Position,
                    priority = pri,
                });
            }
            if (buf.Count > 1) buf.Sort((a, b) => b.priority.CompareTo(a.priority));
        }

        // 候选分配到该pawn
        private static void AssignMarksFor(Pawn p, ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext,
            List<ScoutMarkRegistry.MarkCandidate> candidates, HashSet<int> globalLocked, int globalCap, int now)
        {
            if (candidates.Count == 0) return;
            int pawnId = p.thingIDNumber;
            if (!job.pawnLockedThings.TryGetValue(pawnId, out var ownedSet))
            {
                ownedSet = new HashSet<int>();
                job.pawnLockedThings[pawnId] = ownedSet;
            }
            int cap = Mathf.Max(1, ext.maxMarksPerPawn);

            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (ownedSet.Contains(c.thingId))
                {
                    ScoutMarkRegistry.TouchOwnMark(job, pawnId, c.thingId, c.cell, now);
                    continue;
                }
                if (globalLocked.Contains(c.thingId)) continue;
                if (!c.cell.InBounds(job.map)) continue;
                if (globalCap > 0 && job.marks.Count >= globalCap) break;
                if (ownedSet.Count < cap)
                {
                    ScoutMarkRegistry.AddOwnedMark(job, pawnId, c, now, ownedSet, globalLocked);
                }
                else
                {
                    ScoutMarkRegistry.TryReplaceLowest(job, pawnId, c, now, ownedSet, globalLocked);
                }
            }
            // 二轮自己空时强抢最高优先级
            if (ownedSet.Count == 0)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    var c = candidates[i];
                    if (!c.cell.InBounds(job.map)) continue;
                    if (globalCap > 0 && job.marks.Count >= globalCap) break;
                    ScoutMarkRegistry.AddOwnedMark(job, pawnId, c, now, ownedSet, globalLocked);
                    break;
                }
            }
        }
    }
}
