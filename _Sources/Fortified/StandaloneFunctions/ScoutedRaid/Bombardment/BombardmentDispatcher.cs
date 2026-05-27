using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Fortified
{
    // 炮击调度
    public static class BombardmentDispatcher
    {
        // 投放照明弹
        public static void LaunchFlares(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext)
        {
            var slots = FlareSlotBuilder.GetOrBuild(job, ext);
            if (slots.Count == 0) return;
            ThingDef defaultFlareDef = ext.flareProjectileDef ?? ThingDef.Named("FFF_FlareShell");
            if (defaultFlareDef == null) return;
            ThingDef heavyFlareDef = ext.heavyFlareProjectileDef ?? defaultFlareDef;
            int now = Find.TickManager.TicksGame;
            int delay = 30;
            Vector3 dir = ResolveDir(job);
            job.lastFlareOrigin = OriginFor(job, slots[0].cell, ext, dir);
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                ThingDef flareDef = slot.heavy ? heavyFlareDef : defaultFlareDef;
                Color flareColor = slot.heavy ? ext.heavyFlareColor : ext.flareColor;
                float descendStart = slot.heavy ? ext.heavyFlareDescendStartHeight : ext.flareDescendStartHeight;
                float scale = slot.heavy ? Mathf.Max(0.1f, ext.heavyFlareScale) : 1f;
                GameComponent_CAS.AddData(new AirSupportData_LaunchFlare
                {
                    projectileDef = flareDef,
                    map = job.map,
                    target = slot.cell,
                    origin = OriginFor(job, slot.cell, ext, dir),
                    triggerTick = now + delay,
                    triggerFaction = job.mainFaction ?? job.scoutFaction,
                    color = flareColor,
                    igniteDurationTicks = ext.flareIgniteDurationTicks,
                    igniteHoverHeight = ext.flareIgniteHoverHeight,
                    descendStartHeight = descendStart,
                    descendDurationTicks = ext.flareDescendDurationTicks,
                    approachDir = dir,
                    sizeScale = scale,
                });
                delay += 12;
            }
        }

        // 调度炮击
        public static void ScheduleBombardment(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext)
        {
            var slots = FlareSlotBuilder.GetOrBuild(job, ext);
            if (slots.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            int baseDelay = ext.firstStrikeDelayTicks.RandomInRange;
            int waves = Mathf.Max(1, ext.wavesPerStrike);
            Vector3 dir = ResolveDir(job);
            int seq = 0;
            bool hasHeavyPool = !ext.heavyBombardmentShellPool.NullOrEmpty();
            for (int w = 0; w < waves; w++)
            {
                int wOff = w * ext.waveIntervalTicks;
                for (int si = 0; si < slots.Count; si++)
                {
                    var slot = slots[si];
                    bool heavyShells = slot.heavy && hasHeavyPool;
                    float perCellMul = (slot.heavy && !hasHeavyPool)
                        ? Mathf.Max(1f, ext.heavyFlareProjectileMultiplier) : 1f;
                    int perCell = Mathf.Max(1, ext.projectilesPerMark.RandomInRange);
                    perCell = Mathf.RoundToInt(perCell * perCellMul);
                    float bombSpread = slot.heavy
                        ? ext.spreadRadius * Mathf.Max(1f, ext.heavySpreadMultiplier)
                        : ext.spreadRadius;
                    for (int i = 0; i < perCell; i++)
                    {
                        IntVec3 impact = ScatterCell(slot.cell, bombSpread);
                        if (!impact.InBounds(job.map)) continue;
                        GameComponent_CAS.AddData(new AirSupportData_LaunchProjectile
                        {
                            projectileDef = PickShell(job, ext, heavyShells),
                            map = job.map,
                            target = impact,
                            origin = OriginFor(job, impact, ext, dir),
                            triggerTick = now + baseDelay + wOff + seq * ext.projectileIntervalTicks,
                            triggerFaction = job.mainFaction ?? job.scoutFaction,
                        });
                        seq++;
                    }
                }
            }
            if (!ext.bombardLetterLabel.NullOrEmpty())
            {
                Find.LetterStack.ReceiveLetter(
                    ext.bombardLetterLabel.Translate(),
                    (ext.bombardLetterText ?? "").Translate(),
                    LetterDefOf.ThreatBig,
                    new TargetInfo(slots[0].cell, job.map));
            }
        }

        // 落点散布
        private static IntVec3 ScatterCell(IntVec3 center, float r)
        {
            float a = Rand.Range(0f, 360f);
            float d = Rand.Range(0f, r);
            float dx = Mathf.Cos(a * Mathf.Deg2Rad) * d;
            float dz = Mathf.Sin(a * Mathf.Deg2Rad) * d;
            return new IntVec3(center.x + Mathf.RoundToInt(dx), 0, center.z + Mathf.RoundToInt(dz));
        }

        // 取袭击锁定方向
        public static Vector3 ResolveDir(ScoutedRaidJob job)
        {
            Vector3 dir = job.originAnchor;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            return dir.normalized;
        }

        // 入射点沿锁定方向延伸
        public static Vector3 OriginFor(ScoutedRaidJob job, IntVec3 target, IncidentExtension_ScoutedRaid ext, Vector3 dir)
        {
            float dist = ext.originDistanceRange.RandomInRange;
            Vector3 origin = target.ToVector3Shifted() + dir * dist;
            origin.x = Mathf.Clamp(origin.x, 0.01f, job.map.Size.x - 1.01f);
            origin.z = Mathf.Clamp(origin.z, 0.01f, job.map.Size.z - 1.01f);
            origin.y = 0f;
            return origin;
        }

        // 弹种选取
        private static ThingDef PickShell(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext, bool heavy)
        {
            var pool = heavy && !ext.heavyBombardmentShellPool.NullOrEmpty()
                ? ext.heavyBombardmentShellPool
                : ext.bombardmentShellPool;
            if (pool.NullOrEmpty())
                return ext.fallbackShellDef ?? ThingDef.Named("Bullet_Shell_HighExplosive");
            if (ext.shellSelectionMode == ShellSelectionMode.Sequential)
                return PickShellSequential(job, pool);
            return pool.RandomElementByWeight(e => e.weight).projectileDef;
        }

        private static ThingDef PickShellSequential(ScoutedRaidJob job, List<WeightedShellEntry> pool)
        {
            int total = 0;
            foreach (var e in pool) total += Mathf.Max(1, Mathf.RoundToInt(e.weight));
            int idx = job.shellSeqIndex % total;
            job.shellSeqIndex++;
            int cursor = 0;
            foreach (var e in pool)
            {
                cursor += Mathf.Max(1, Mathf.RoundToInt(e.weight));
                if (idx < cursor) return e.projectileDef;
            }
            return pool[0].projectileDef;
        }
    }
}
