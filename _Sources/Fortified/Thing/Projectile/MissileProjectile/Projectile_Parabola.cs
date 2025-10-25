using RimWorld;
using UnityEngine;
using Verse;

namespace Fortified
{
    public class Projectile_Parabola_ConeExplosive : Projectile_Parabola
    {
        protected float coneSway = 10f;
        protected Vector3 Angle => (destination - origin).normalized;
        protected float Sway => Angle.RotatedBy(coneSway).ToAngleFlat();
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            DoExplosion();
            base.Impact(hitThing, blockedByShield);
        }
        protected void DoExplosion()
        {
            if (def.HasModExtension<ExplosiveExtension>())
            {
                ExplosiveExtension ext = def.GetModExtension<ExplosiveExtension>();
                IntVec3 offsetPos = Position - (Angle * ext.preExplosionOffset).ToIntVec3();
                if (ext.damage != null)
                {
                    int dmg = ext.damageAmount != -1 ? ext.damageAmount : DamageAmount;
                    float armorPen = ext.armorPen != -1 ? ext.armorPen : ArmorPenetration;
                    var things = Map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);
                    GenExplosion.DoExplosion(
                        center: offsetPos,
                        Map,
                        ext.range,
                        ext.damage,
                        Launcher,
                        dmg, armorPen,
                        ext.sound,
                        EquipmentDef,
                        projectile: def,
                        affectedAngle: new FloatRange(Angle.ToAngleFlat() - ext.swayAngle, Angle.ToAngleFlat() + ext.swayAngle),
                        doVisualEffects: ext.doVisualEffects,
                        doSoundEffects: ext.sound != null,
                        ignoredThings: things);
                }
                ext.effecterDef?.Spawn(offsetPos, DestinationCell, Map, 1);
            }
            else //默認值
            {
                GenExplosion.DoExplosion(center: Position - (Angle * 2).ToIntVec3(), Map, 7,
                    DamageDefOf.Bullet, launcher,
                    30, 0.5f, weapon: EquipmentDef,
                    direction: Angle.ToAngleFlat(), affectedAngle: new FloatRange(Angle.ToAngleFlat() - Sway, Angle.ToAngleFlat() + Sway),
                    doVisualEffects: true, doSoundEffects: false
                    );
            }
        }
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            base.Destroy(mode);
        }
    }


    public class Projectile_Parabola : Projectile_Explosive
    {
        protected CompAfterBurner compAfterBurner;
        public override Vector3 ExactPosition
        {
            get
            {
                Vector3 vector = (destination - origin).Yto0() * DistanceCoveredFraction;
                return origin.Yto0() + vector + Vector3.up * def.Altitude;
            }
        }
        private float ArcHeightFactor
        {
            get
            {
                float num = def.projectile.arcHeightFactor;
                float num2 = (destination - origin).MagnitudeHorizontalSquared();
                if (Mathf.Pow(num, 2) > num2 * 0.2f * 0.2f)
                {
                    num = Mathf.Sqrt(num2) * 0.2f;
                }
                return num;
            }
        }
        protected float Progress => base.DistanceCoveredFraction;
        protected Vector3 LookTowards => new Vector3(destination.x - origin.x, def.Altitude, destination.z - origin.z + ArcHeightFactor * Accelerate);
        public override Quaternion ExactRotation => Quaternion.LookRotation(LookTowards);
        protected float Accelerate => 5f - 10f * Progress;
        public override Vector3 DrawPos => ExactPosition + new Vector3(0f, 0f, 1f) * ArcHeightFactor * GenMath.InverseParabola(Progress);

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            compAfterBurner = this.TryGetComp<CompAfterBurner>();
            compAfterBurner?.ThrowLaunchSmoke(origin, (origin - destination).ToAngleFlat() + 90);
        }
        protected override void Tick()
        {
            base.Tick();
            if (Spawned && compAfterBurner != null)
            {
                compAfterBurner.drawOnProjectile = true;
                float num = ArcHeightFactor * GenMath.InverseParabola(base.DistanceCoveredFraction);
                Vector3 drawPos = DrawPos;
                Vector3 vector = drawPos + new Vector3(0f, 0f, 1f) * num;
                compAfterBurner.ThrowExhaust(vector - LookTowards.normalized * 0.1f, Progress);
            }
        }
    }
}