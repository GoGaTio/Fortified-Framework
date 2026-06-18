using RimWorld;
using Verse;

namespace Fortified
{
    /// <summary>
    /// Use-effect that activates a <see cref="CompContinuousDamager"/> on the same building.
    /// Attach this alongside a <see cref="CompUsable"/> (useJob = UseItem) so a colonist
    /// must walk up and interact before the device fires.
    /// </summary>
    public class CompUseEffect_ActivateContinuousDamager : CompUseEffect
    {
        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            CompContinuousDamager comp = parent.TryGetComp<CompContinuousDamager>();
            if (comp == null)
                return "FFF_NoContinuousDamagerComp".Translate();

            if (comp.Active)
                return "FFF_ContinuousDamagerAlreadyActive".Translate();

            return true;
        }

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            parent.TryGetComp<CompContinuousDamager>()?.Activate();
        }
    }
}
