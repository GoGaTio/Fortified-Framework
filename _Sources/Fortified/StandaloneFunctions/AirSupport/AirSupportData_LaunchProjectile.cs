using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Fortified
{
    public class AirSupportData_LaunchProjectile : AirSupportData
    {
        public ThingDef projectileDef;
        public Vector3 origin;
        public LocalTargetInfo usedTarget = LocalTargetInfo.Invalid;
        public SoundDef soundDef;

        // CE 兼容管线
        public static Func<Thing, Thing, Vector3, LocalTargetInfo, LocalTargetInfo, bool> ceProjectileLauncher = null;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref projectileDef, "projectileDef");
            Scribe_Values.Look(ref origin, "origin");
            Scribe_Defs.Look(ref soundDef, "soundDef");
            Scribe_TargetInfo.Look(ref usedTarget, "usedTarget");
        }

        public override void Trigger()
        {
            if (projectileDef == null)
            {
                Log.Error("[Fortified] AirSupportData_LaunchProjectile: projectileDef为空");
                return;
            }

            // 确保生成位置在地图内
            IntVec3 spawnPos = origin.ToIntVec3();
            if (!spawnPos.InBounds(map))
            {
                // 如果在地图外，将其限制到地图边缘
                spawnPos.x = Mathf.Clamp(spawnPos.x, 0, map.Size.x - 1);
                spawnPos.z = Mathf.Clamp(spawnPos.z, 0, map.Size.z - 1);
            }

            Thing spawnedThing = GenSpawn.Spawn(projectileDef, spawnPos, map);

            if (spawnedThing == null)
            {
                Log.Error($"[Fortified] AirSupportData_LaunchProjectile: 无法生成 {projectileDef.defName}");
                return;
            }

            Thing launcher = GetLauncher();

            if (launcher != null)
            {
                LaunchProjectile(spawnedThing, launcher);
            }
            else
            {
                Log.Warning($"[Fortified] AirSupportData_LaunchProjectile: 无法为抛射物 {projectileDef.defName} 创建发射者");
                spawnedThing.Destroy();
            }

            soundDef?.PlayOneShot(SoundInfo.InMap(new TargetInfo(spawnPos, map)));
        }

        protected virtual Thing GetLauncher()
        {
            if (triggerer != null) return triggerer;

            if (triggerFaction != null)
            {
                PawnKindDef kind = triggerFaction.def.basicMemberKind ?? PawnKindDefOf.Colonist;
                if (kind != null)
                {
                    Pawn virtualLauncher = PawnGenerator.GeneratePawn(kind, triggerFaction);
                    if (virtualLauncher != null)
                    {
                        // 生成的 Pawn 默认未 Spawn，无需 DeSpawn
                        return virtualLauncher;
                    }
                }
            }

            return null;
        }

        protected virtual void LaunchProjectile(Thing projectile, Thing launcher)
        {
            // 优先使用 CE 管线
            if (ceProjectileLauncher != null)
            {
                if (ceProjectileLauncher(projectile, launcher, origin, usedTarget.IsValid ? usedTarget : target, target))
                {
                    return;
                }
            }

            // 原版发射逻辑
            if (projectile is Projectile vanillaProjectile)
            {
                vanillaProjectile.Launch(launcher, origin, usedTarget.IsValid ? usedTarget : target, target, ProjectileHitFlags.IntendedTarget);
            }
            else
            {
                Log.Error($"[Fortified] AirSupportData_LaunchProjectile: {projectileDef.defName} 不是Projectile类型");
                projectile.Destroy();
            }
        }
    }
    public class AirSupportData_LaunchProjectileOnEdge : AirSupportData_LaunchProjectile
    {
        public override void Trigger()
        {
            var xEdge = origin.x > target.Cell.x ? map.Size.x - 0.01f : 0.01f;
            var zEdge = origin.z > target.Cell.z ? map.Size.z - 0.01f : 0.01f;

            var xDifference = Math.Abs((xEdge - target.Cell.x) / (origin.x - target.Cell.x));
            var zDifference = Math.Abs((-target.Cell.z) / (origin.z - target.Cell.z));

            var deltaZ = origin - target.Cell.ToVector3Shifted();
            var deltaX = deltaZ * xDifference + target.Cell.ToVector3Shifted();
            deltaX.x = xEdge;

            deltaZ *= zDifference;
            deltaZ += target.Cell.ToVector3Shifted();
            deltaZ.z = zEdge;


            if (deltaX.InBounds(map))
            {
                origin = deltaX;
            }
            else
            {
                origin = deltaZ;
            }
            if (DebugSettings.godMode) Log.Message($"{deltaX.ToIntVec3()} {deltaZ.ToIntVec3()} {origin}");
            base.Trigger();
        }
    }

    public class AirSupportData_LaunchProjectileFromPlane : AirSupportData_LaunchProjectile, IAttachedToFlyBy
    {
        public FlyByThing plane;

        public Vector3 offset;

        FlyByThing IAttachedToFlyBy.plane
        {
            set => plane = value;
        }

        public override void Trigger()
        {
            if (plane != null && plane.Spawned) origin = plane.DrawPos + offset.RotatedBy(plane.angle);
            origin.x = Mathf.Clamp(origin.x, 0, map.Size.x - 1.01f);
            origin.z = Mathf.Clamp(origin.z, 0, map.Size.z - 1.01f);
            base.Trigger();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref plane, "plane");
            Scribe_Values.Look(ref offset, "offset");
        }
    }
}
