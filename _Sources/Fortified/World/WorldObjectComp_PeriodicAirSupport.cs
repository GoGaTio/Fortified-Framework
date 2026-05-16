using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace Fortified
{
    public class WorldObjectComp_PeriodicAirSupport : WorldObjectComp
    {
        public WorldObjectCompProperties_PeriodicAirSupport Props => (WorldObjectCompProperties_PeriodicAirSupport)props;

        private int ticksToNextStrike;
        private int remainingCycles;
        private bool wasThreatActive;
        private Map targetMap; // 锁定当前正在处理的袭击地图
        private Faction raidFaction; // 记录发起袭击的派系
        private int shellSequenceIndex; // 记录弹药序列当前位置

        private bool IsActive()
        {
            if (parent is MapParent mapParent && mapParent.HasMap) return false;
            if (Props.requiresAnySitePart.NullOrEmpty()) return true;
            if (parent is Site site)
            {
                foreach (var required in Props.requiresAnySitePart)
                {
                    if (site.parts.Any(p => p.def == required)) return true;
                }
            }
            return false;
        }

        public override void Initialize(WorldObjectCompProperties props)
        {
            base.Initialize(props);
            ticksToNextStrike = 0;
        }

        public void OnHostilityStarted(Map map, Faction raidFaction = null)
        {
            if (!IsActive() || map == null) return;

            // 判定袭击是否触发支援
            if (!Rand.Chance(Props.triggerChance)) return;

            this.targetMap = map;
            this.raidFaction = raidFaction;
            remainingCycles = Props.strikeCycles;

            if (remainingCycles > 0)
            {
                wasThreatActive = true;
                if (Rand.Chance(Props.successChance))
                {
                    ExecuteStrike(map);
                }
                remainingCycles--;
                ticksToNextStrike = Props.strikeIntervalTicks;
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent.Destroyed || !IsActive()) return;
            if (remainingCycles <= 0 || !wasThreatActive || targetMap == null) return;

            // 确保目标地图依然有效
            if (!Find.Maps.Contains(targetMap))
            {
                CleanupStatus();
                return;
            }

            if (Find.TickManager.TicksGame % 60 != 0) return;

            // 事件模式处理轮次冷却
            ticksToNextStrike -= 60;
            if (ticksToNextStrike <= 0)
            {
                if (Rand.Chance(Props.successChance))
                {
                    ExecuteStrike(targetMap);
                }
                remainingCycles--;
                ticksToNextStrike = Props.strikeIntervalTicks;

                if (remainingCycles <= 0)
                {
                    CleanupStatus();
                }
            }
        }

        private void CleanupStatus()
        {
            wasThreatActive = false;
            targetMap = null;
            raidFaction = null;
        }

        private void ExecuteStrike(Map map)
        {
            var (originPos, localDir) = CalcOriginAndDir(map);
            var hostiles = GetHostilesOnMap(map);
            if (hostiles.Count == 0) return;

            var actualType = SelectTargetType(map);
            var result = TryFindTarget(actualType, map, hostiles, localDir)
                ?? TryFindTarget(BombardTargetType.Auto, map, hostiles, localDir);
            if (result == null) return;

            BombardCoordinator.Claim(map, actualType, Props.coordinationHoldTicks);
            var (targetCell, raidDir) = result.Value;

            if (HasAirSupport)
                FireAirSupport(map, originPos, targetCell);
            else
                FireBombardment(map, originPos, localDir, raidDir, targetCell);
            SendNotifications(map, targetCell);
        }

        private bool HasAirSupport
            => !Props.airSupportPool.NullOrEmpty() || Props.airSupportDef != null;

        private AirSupportDef PickAirSupportDef()
        {
            if (!Props.airSupportPool.NullOrEmpty())
                return Props.airSupportPool.RandomElementByWeight(e => e.weight).airSupportDef;
            return Props.airSupportDef;
        }

        private (Vector3 origin, Vector3 localDir) CalcOriginAndDir(Map map)
        {
            var wg = Find.WorldGrid;
            Vector3 worldDir = (wg.GetTileCenter(parent.Tile) - wg.GetTileCenter(map.Tile)).normalized;
            Vector3 localDir = new Vector3(worldDir.x, 0, worldDir.z).normalized;
            Vector3 origin = map.Center.ToVector3Shifted() + localDir * (map.Size.x * 0.8f);
            origin.y = Props.projectileOriginHeight;
            return (origin, localDir);
        }

        private List<Pawn> GetHostilesOnMap(Map map)
            => map.mapPawns.AllPawnsSpawned
                .Where(p => p.HostileTo(Faction.OfPlayer) && !p.Downed && !p.Dead)
                .ToList();

        private BombardTargetType SelectTargetType(Map map)
        {
            if (Props.targetPriorities.NullOrEmpty()) return BombardTargetType.Auto;
            foreach (var t in Props.targetPriorities)
                if (!BombardCoordinator.IsClaimed(map, t)) return t;
            return Props.targetPriorities[Props.targetPriorities.Count - 1];
        }

        private (IntVec3 cell, Vector3 raidDir)? TryFindTarget(
            BombardTargetType type, Map map, List<Pawn> hostiles, Vector3 fallbackDir)
        {
            Vector3 hCenter = Vector3.zero;
            foreach (var h in hostiles) hCenter += h.Position.ToVector3Shifted();
            hCenter /= hostiles.Count;

            var candidates = GetCandidatesForType(type, map);
            if (candidates.Count == 0) return null;

            IntVec3 cell = candidates.RandomElement().Position;
            Vector3 raid = (cell.ToVector3Shifted() - hCenter).normalized;
            if (raid == Vector3.zero) raid = fallbackDir;
            return (cell, raid);
        }

        private List<Thing> GetCandidatesForType(BombardTargetType type, Map map)
        {
            switch (type)
            {
                case BombardTargetType.TurretLine:
                    return map.listerBuildings.allBuildingsColonist
                        .Where(b => b.Spawned && b.def.building?.turretGunDef != null)
                        .Cast<Thing>().ToList();

                case BombardTargetType.PowerGrid:
                    return map.listerBuildings
                        .AllBuildingsColonistOfGroup(ThingRequestGroup.PowerTrader)
                        .Where(b => b.Spawned)
                        .Cast<Thing>().ToList();

                case BombardTargetType.HighValueStorage:
                    return map.haulDestinationManager.AllGroupsListForReading
                        .SelectMany(g => g.HeldThings)
                        .Where(t => !t.def.IsCorpse)
                        .OrderByDescending(t => t.MarketValue * t.stackCount)
                        .Take(10).ToList();

                case BombardTargetType.Colonists:
                    return map.mapPawns.FreeColonistsSpawned.Cast<Thing>().ToList();

                case BombardTargetType.MechConcentration:
                    return map.mapPawns.SpawnedColonyMechs
                        .Where(p => !p.Downed && !p.Dead)
                        .Cast<Thing>().ToList();

                default:
                    return map.listerBuildings.allBuildingsColonist
                        .Cast<Thing>()
                        .Concat(map.mapPawns.FreeColonistsSpawned.Cast<Thing>())
                        .ToList();
            }
        }

        private void FireAirSupport(Map map, Vector3 originPos, IntVec3 targetCell)
        {
            var def = PickAirSupportDef();
            if (def == null) return;
            def.tempOriginCache = originPos;
            def.originOverridedCache = true;
            def.Trigger(null, map, targetCell);
        }

        private ThingDef PickShell()
        {
            if (Props.shellPool.NullOrEmpty())
                return Props.vanillaBombardmentDef ?? ThingDef.Named("Bullet_Shell_HighExplosive");
            return Props.shellSelectionMode == ShellSelectionMode.Sequential
                ? PickShellSequential()
                : Props.shellPool.RandomElementByWeight(e => e.weight).projectileDef;
        }

        private ThingDef PickShellSequential()
        {
            // 按照权重展开弹药序列
            int total = 0;
            foreach (var e in Props.shellPool)
                total += Mathf.Max(1, Mathf.RoundToInt(e.weight));

            int idx = shellSequenceIndex % total;
            shellSequenceIndex++;

            int cursor = 0;
            foreach (var e in Props.shellPool)
            {
                cursor += Mathf.Max(1, Mathf.RoundToInt(e.weight));
                if (idx < cursor) return e.projectileDef;
            }
            return Props.shellPool[0].projectileDef;
        }

        private void FireBombardment(Map map, Vector3 originPos, Vector3 localDir,
            Vector3 raidDir, IntVec3 baseCell)
        {
            int perWave = Props.projectilesPerStrike;
            int waves = Mathf.Max(1, Props.wavesPerStrike);
            int baseDelay = Props.firstStrikeDelayTicks.RandomInRange;
            Vector3 sideDir = new Vector3(-localDir.z, 0, localDir.x).normalized;
            float scaleMult = Mathf.Max(1.5f, 40f / Mathf.Max(Props.spreadRadius * 2f, 1f));

            for (int w = 0; w < waves; w++)
            {
                int wDelay = w * Props.waveIntervalTicks;
                for (int i = 0; i < perWave; i++)
                {
                    IntVec3 impact = CalculateImpactCell(baseCell, i, perWave, localDir, raidDir, w, waves);
                    if (!impact.InBounds(map)) continue;
                    Vector3 offset = impact.ToVector3Shifted() - baseCell.ToVector3Shifted();
                    float lat = Vector3.Dot(offset, sideDir) * scaleMult + Rand.Range(-2.5f, 2.5f);
                    Vector3 origin = originPos + sideDir * lat;
                    origin.x = Mathf.Clamp(origin.x, 0.01f, map.Size.x - 1.01f);
                    origin.z = Mathf.Clamp(origin.z, 0.01f, map.Size.z - 1.01f);
                    GameComponent_CAS.AddData(new AirSupportData_LaunchProjectile()
                    {
                        projectileDef = PickShell(),
                        map = map,
                        target = impact,
                        triggerTick = Find.TickManager.TicksGame + baseDelay + wDelay + i * Props.projectileIntervalTicks,
                        origin = origin,
                        triggerFaction = raidFaction ?? parent.Faction
                    });
                }
            }
        }

        private void SendNotifications(Map map, IntVec3 targetCell)
        {
            if (!Props.messageText.NullOrEmpty())
                Messages.Message(Props.messageText.Translate(),
                    new TargetInfo(targetCell, map), Props.messageType ?? MessageTypeDefOf.NegativeEvent);
            if (!Props.letterLabel.NullOrEmpty())
                Find.LetterStack.ReceiveLetter(Props.letterLabel.Translate(),
                    Props.letterText.Translate(), LetterDefOf.ThreatBig, new TargetInfo(targetCell, map));
        }

        private IntVec3 CalculateImpactCell(IntVec3 center, int index, int total, Vector3 siteDir, Vector3 raidDir, int waveIndex, int waveCount)
        {
            switch (Props.strikePattern)
            {
                case StrikePattern.Circle:
                    float angle = (360f / total) * index;
                    float radius = Props.spreadRadius + (waveIndex * 2f);
                    return (center.ToVector3Shifted() + Vector3.forward.RotatedBy(angle) * radius).ToIntVec3();

                case StrikePattern.Fan:
                    float startAngle = -Props.fanAngle / 2f;
                    float fanAngleStep = Props.fanAngle / (Mathf.Max(1, total - 1));
                    float currentFanAngle = startAngle + (fanAngleStep * index);
                    float fanDist = Props.spreadRadius + (waveIndex * 3f);
                    // 使用袍击者进攻角度展开山形
                    return (center.ToVector3Shifted() + Vector3.forward.RotatedBy(raidDir.AngleFlat() + currentFanAngle) * fanDist).ToIntVec3();

                case StrikePattern.Creeping:
                    int cycleStep = Props.strikeCycles - remainingCycles - 1;
                    // 确定徐进及展开方向
                    Vector3 creepForward = raidDir;
                    Vector3 cycleBase = center.ToVector3Shifted() + creepForward * (cycleStep * Props.creepingStep);
                    Vector3 waveBase = cycleBase + creepForward * (waveIndex * 4f);
                    // 计算横向垂直偏移向量
                    Vector3 sideDir = new Vector3(creepForward.z, 0, -creepForward.x);
                    float fullWidth = Props.spreadRadius * 2;
                    float lateralPos = (total > 1) ? (-Props.spreadRadius + (fullWidth / (total - 1)) * index) : 0f;
                    float jitter = Rand.Range(-1.5f, 1.5f);
                    return (waveBase + sideDir * (lateralPos + jitter) + creepForward * Rand.Range(-1f, 1f)).ToIntVec3();

                case StrikePattern.Random:
                default:
                    return center + GenRadial.RadialPattern[Rand.RangeInclusive(0, 15)];
            }
        }

        public override string CompInspectStringExtra()
        {
            if (parent.Destroyed) return null;
            if (parent is MapParent mapParent && mapParent.HasMap) return "FFF_Site_LocalMonitoring".Translate();
            if (!IsActive()) return null;
            // 无威胁待机或轮次结束
            if (!wasThreatActive || remainingCycles <= 0)
                return "FFF_NextStrikeStatus".Translate("FFF_Status_Standby".Translate());
            return "FFF_NextStrikeIn".Translate(ticksToNextStrike.ToStringTicksToPeriod());
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksToNextStrike, "ticksToNextStrike", 0);
            Scribe_Values.Look(ref remainingCycles, "remainingCycles", 0);
            Scribe_Values.Look(ref wasThreatActive, "wasThreatActive", false);
            Scribe_Values.Look(ref shellSequenceIndex, "shellSequenceIndex", 0);
            Scribe_References.Look(ref raidFaction, "raidFaction");
        }
    }
}
