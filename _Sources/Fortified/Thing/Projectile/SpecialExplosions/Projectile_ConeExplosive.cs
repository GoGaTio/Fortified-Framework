using RimWorld;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace Fortified
{
    public class Projectile_ConeExplosive : Projectile_Explosive
    {
        /// <summary>
        /// 當此旗標為 true 時，表示目前正在處理由敵對 Pawn 發射的錐形爆炸，
        /// 應抑制對玩家好感度的負面影響。
        /// 使用 [ThreadStatic] 確保多執行緒安全。
        /// </summary>
        [System.ThreadStatic]
        public static bool SuppressConeExplosiveGoodwillPenalty;

        protected float coneSway = 10f;
        protected Vector3 Angle => (destination - origin).normalized;
        protected float Sway => Angle.RotatedBy(coneSway).ToAngleFlat();

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // 只有當發射者屬於對玩家敵對的陣營時才抑制好感度懲罰。
            // 防禦性判斷：若 launcher 或其陣營為 null、或玩家陣營未載入，直接跳過設旗。
            bool suppress = launcher?.Faction != null
                && Faction.OfPlayer != null
                && launcher.Faction.HostileTo(Faction.OfPlayer);

            if (suppress)
                SuppressConeExplosiveGoodwillPenalty = true;
            try
            {
                DoExplosion();
                base.Impact(hitThing, blockedByShield);
            }
            finally
            {
                // 無論是否發生例外，都必須還原旗標以免影響後續邏輯
                if (suppress)
                    SuppressConeExplosiveGoodwillPenalty = false;
            }
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
                        intendedTarget: intendedTarget.Thing,
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
                    intendedTarget: intendedTarget.Thing,
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