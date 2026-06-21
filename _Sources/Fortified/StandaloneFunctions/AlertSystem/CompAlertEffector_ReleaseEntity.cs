using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Fortified
{
    public class CompProperties_AlertEffector_ReleaseEntity : CompProperties_AlertEffector
    {
        public PawnKindDef pawnKindDef;

        // ── 選填 ────────────────────────────────────────────────
        /// <summary>每次觸發時釋放的數量。</summary>
        public int countToSpawn = 3;

        /// <summary>
        /// 釋放的派系（null 則使用建築本身的派系）。
        /// 可指定 FactionDef.defName 以跨 mod 相容。
        /// </summary>
        public FactionDef spawnFactionDef;

        /// <summary>釋放時播放的音效（null 則靜音）。</summary>
        public SoundDef spawnSound;

        /// <summary>釋放時播放的一次性 Effecter（煙霧、爆炸光等）。</summary>
        public EffecterDef spawnEffecter;

        /// <summary>
        /// 最大搜尋半徑，用於自動鎖定最近的敵方 Pawn 為目標。
        /// 0 = 不自動鎖定。
        /// </summary>
        public float targetSearchRadius = 30f;

        /// <summary>
        /// 生成時是否散佈在建築周圍（true）或全部堆在同一格（false）。
        /// </summary>
        public bool scatterAroundParent = true;

        /// <summary>
        /// 散佈時的最大偏移半徑（格數）。
        /// </summary>
        public int scatterRadius = 2;

        public CompProperties_AlertEffector_ReleaseEntity()
        {
            compClass = typeof(CompAlertEffector_ReleaseEntity);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string e in base.ConfigErrors(parentDef))
                yield return e;
            if (pawnKindDef == null)
                yield return $"[FFF] {nameof(CompProperties_AlertEffector_ReleaseEntity)} on {parentDef.defName}: pawnKindDef is null.";
        }
    }

    public class CompAlertEffector_ReleaseEntity : CompAlertEffector
    {
        public new CompProperties_AlertEffector_ReleaseEntity Props
            => (CompProperties_AlertEffector_ReleaseEntity)props;

        protected override void DoEffect()
        {
            if (!parent.Spawned) return;

            Map map = parent.Map;
            IntVec3 origin = parent.Position;

            // ── 1. 解析派系 ──────────────────────────────────────
            Faction faction = ResolveSpawnFaction();

            // ── 2. 尋找自動目標 ──────────────────────────────────
            Pawn autoTarget = FindNearestTarget(map, origin, faction);

            // ── 3. 生成 Pawn ─────────────────────────────────────
            for (int i = 0; i < Props.countToSpawn; i++)
            {
                IntVec3 spawnCell = ResolveSpawnCell(origin, map);

                PawnGenerationRequest request = new PawnGenerationRequest(
                    Props.pawnKindDef,
                    faction,
                    PawnGenerationContext.NonPlayer,
                    tile: null,
                    forceGenerateNewPawn: false,
                    allowDead: false,
                    allowDowned: false,
                    canGeneratePawnRelations: true,
                    mustBeCapableOfViolence: false,
                    colonistRelationChanceFactor: 1f,
                    forceAddFreeWarmLayerIfNeeded: false,
                    allowGay: true,
                    allowPregnant: false,
                    allowFood: true,
                    allowAddictions: true,
                    inhabitant: false,
                    certainlyBeenInCryptosleep: false,
                    forceRedressWorldPawnIfFormerColonist: false,
                    worldPawnFactionDoesntMatter: false,
                    biocodeWeaponChance: 0f,
                    biocodeApparelChance: 0f,
                    extraPawnForExtraRelationChance: null,
                    relationWithExtraPawnChanceFactor: 1f,
                    validatorPreGear: null,
                    validatorPostGear: null,
                    forcedTraits: null,
                    prohibitedTraits: null,
                    minChanceToRedressWorldPawn: null,
                    fixedBiologicalAge: 0f,
                    fixedChronologicalAge: 0f);

                Pawn spawned = PawnGenerator.GeneratePawn(request);
                if (spawned == null) continue;

                // 設定目標
                if (autoTarget != null)
                    spawned.mindState.enemyTarget = autoTarget;

                GenSpawn.Spawn(spawned, spawnCell, map);
            }

            // ── 4. 音效 ──────────────────────────────────────────
            SoundDef sound = Props.spawnSound ?? SoundDefOf.DroneTrapSpring;
            sound?.PlayOneShot(new TargetInfo(origin, map));

            // ── 5. Effecter ───────────────────────────────────────
            if (Props.spawnEffecter != null)
                Props.spawnEffecter.Spawn(origin, map).Cleanup();

            // ── 6. 玩家訊息 ───────────────────────────────────────
            if (parent.Faction != Faction.OfPlayer)
            {
                Messages.Message(
                    "FFF_Alert_ReleaseEntity_Triggered".Translate(
                        Props.countToSpawn,
                        Props.pawnKindDef.label),
                    new LookTargets(origin, map),
                    MessageTypeDefOf.ThreatBig);
            }
        }

        // ── 工具方法 ─────────────────────────────────────────────
        private Faction ResolveSpawnFaction()
        {
            if (Props.spawnFactionDef != null)
                return Find.FactionManager.FirstFactionOfDef(Props.spawnFactionDef)
                    ?? parent.Faction;
            return parent.Faction;
        }

        private Pawn FindNearestTarget(Map map, IntVec3 origin, Faction spawnFaction)
        {
            if (Props.targetSearchRadius <= 0f) return null;

            // 尋找在半徑內、與釋放派系敵對、非隱形的 Pawn
            IEnumerable<Pawn> candidates = map.mapPawns.AllPawnsSpawned
                .Where(p => !p.Dead
                    && !p.Downed
                    && !p.IsPsychologicallyInvisible()
                    && p.Faction != null
                    && (spawnFaction == null || p.Faction.HostileTo(spawnFaction))
                    && p.Position.DistanceTo(origin) <= Props.targetSearchRadius
                    && GenSight.LineOfSight(origin, p.Position, map));

            // TryMinBy：序列為空時安全回傳 false，不拋例外
            return candidates.TryMinBy(p => p.Position.DistanceTo(origin), out Pawn result)
                ? result
                : null;
        }

        private IntVec3 ResolveSpawnCell(IntVec3 origin, Map map)
        {
            if (!Props.scatterAroundParent) return origin;

            // 在 scatterRadius 內找一個可站立格
            for (int attempt = 0; attempt < 10; attempt++)
            {
                IntVec3 candidate = origin + GenRadial.RadialPattern[
                    Rand.RangeInclusive(0, GenRadial.NumCellsInRadius(Props.scatterRadius) - 1)];
                if (candidate.InBounds(map) && candidate.Standable(map))
                    return candidate;
            }
            return origin; // 兜底
        }
    }
}
