using Verse;

namespace Fortified
{
    public class Verb_LaunchProjectile_AmmoSwitch : Verb_LaunchProjectile
    {
        private CompAmmoSwitch compInt;

		public CompAmmoSwitch Comp
        {
            get
            {
                if(compInt == null)
                {
                    compInt = EquipmentSource?.TryGetComp<CompAmmoSwitch>();
				}
                return compInt;
			}
        }
        public override ThingDef Projectile
        {
            get
            {
                CompAmmoSwitch comp = Comp;
                if (comp?.CurrentProjectile != null)
                    return comp.CurrentProjectile;

                return base.Projectile;
            }
        }

		protected override int ShotsPerBurst => Comp?.CurrentAmmo.burstShotCountOverride ?? base.BurstShotCount;

		public override float WarmupTime => base.WarmupTime * (Comp?.CurrentAmmo.warmUpFactor ?? 1f);

		public override float EffectiveRange => base.EffectiveRange * (Comp?.CurrentAmmo.rangeFactor ?? 1f);

        public override bool Available()
        {
            if (!base.Available()) return false;

            CompAmmoSwitch comp = Comp;
            if (comp != null && comp.IsOnSwitchCooldown && state != VerbState.Bursting)
                return false;

            return true;
        }

        public override bool TryStartCastOn(
            LocalTargetInfo castTarg,
            LocalTargetInfo destTarg,
            bool surpriseAttack = false,
            bool canHitNonTargetPawns = true,
            bool preventFriendlyFire = false,
            bool nonInterruptingSelfCast = false)
        {
            CompAmmoSwitch comp = EquipmentSource?.TryGetComp<CompAmmoSwitch>();
            if (comp != null && comp.IsOnSwitchCooldown && state != VerbState.Bursting)
                return false;

            return base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
        }
    }
}