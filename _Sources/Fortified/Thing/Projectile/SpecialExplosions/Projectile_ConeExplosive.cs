using RimWorld;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace Fortified
{
    public class Projectile_ConeExplosive : Projectile_Explosive
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
                        launcher,
                        dmg, armorPen,
                        ext.sound,
                        EquipmentDef,
                        projectile: this.def,
                        affectedAngle: new FloatRange(Angle.ToAngleFlat() - ext.swayAngle, Angle.ToAngleFlat() + ext.swayAngle),
                        doVisualEffects: ext.doVisualEffects,
                        doSoundEffects: ext.sound != null,
                        ignoredThings: things);
                }
                ext.effecterDef?.Spawn(offsetPos, DestinationCell, Map, 1);
            }
            else //默認值
            {
                GenExplosion.DoExplosion(center: Position - (Angle * 2).ToIntVec3(), this.Map, 7,
                    DamageDefOf.Bullet, this.launcher,
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
    public class ExplosiveExtension : DefModExtension
    {
        public EffecterDef effecterDef = null;
        public DamageDef damage = null;
        public int damageAmount = -1;
        public float armorPen = -1;
        public float preExplosionOffset = 0;
        public float range = 0;
        public float swayAngle = 0;
        public SoundDef sound = null;
        public bool doVisualEffects = false;
    }
}