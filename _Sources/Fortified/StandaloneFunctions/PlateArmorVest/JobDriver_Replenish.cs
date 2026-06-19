using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Fortified
{
    public class JobDriver_Replenish : JobDriver
    {
        private const int DurationTicks = 600;

        private Thing Material => job.GetTarget(TargetIndex.A).Thing;
        private Thing WorkTarget => job.GetTarget(TargetIndex.B).Thing;
        private Thing ExactTarget => job.GetTarget(TargetIndex.C).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(Material, job, 1, -1, null, errorOnFailed)) return false;
            if (WorkTarget == pawn) return true;
            return pawn.Reserve(WorkTarget, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOnDespawnedOrNull(TargetIndex.A);

            yield return Toils_Haul.StartCarryThing(TargetIndex.A);

            if (WorkTarget != pawn)
            {
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch)
                    .FailOnDespawnedOrNull(TargetIndex.B);
            }

            // 等待整备
            Toil wait = Toils_General.WaitWith(TargetIndex.B, DurationTicks, true, true, face: TargetIndex.B);
            wait.FailOnDespawnedOrNull(TargetIndex.B);
            if (WorkTarget != pawn) wait.FailOnCannotTouch(TargetIndex.B, PathEndMode.Touch);
            wait.WithEffect(EffecterDefOf.ConstructMetal, TargetIndex.B);
            yield return wait;

            yield return Toils_General.Do(PerformReplenish);
        }

        private void PerformReplenish()
        {
            Thing workTarget = WorkTarget;
            if (workTarget == null || !workTarget.Spawned) return;

            IReplenishable replenishable = FindReplenishable(workTarget, ExactTarget);
            if (replenishable == null) return;

            Thing carried = pawn.carryTracker?.CarriedThing;
            if (carried == null) return;

            int use = Mathf.Min(replenishable.GetMaterialCostForRefill(), carried.stackCount);
            if (use <= 0) return;

            Thing used = carried.SplitOff(use);
            used.Destroy();

            replenishable.Replenish(pawn, use);
        }

        private static IReplenishable FindReplenishable(Thing workTarget, Thing exactTarget)
        {
            IReplenishable comp = FindThingCompReplenishable(exactTarget);
            if (comp != null) return comp;
            comp = FindThingCompReplenishable(workTarget);
            if (comp != null) return comp;
            return FindPawnReplenishable(workTarget as Pawn);
        }

        private static IReplenishable FindThingCompReplenishable(Thing target)
        {
            if (target is not ThingWithComps twc || twc.AllComps.NullOrEmpty()) return null;
            foreach (ThingComp comp in twc.AllComps)
            {
                if (comp is IReplenishable r && r.GetMaterialCostForRefill() > 0) return r;
            }
            return null;
        }

        private static IReplenishable FindPawnReplenishable(Pawn target)
        {
            if (target?.health?.hediffSet == null) return null;
            IReplenishable apparel = FindPawnApparelReplenishable(target);
            if (apparel != null) return apparel;
            foreach (HediffComp_DamageBlocker comp in target.health.hediffSet.GetHediffComps<HediffComp_DamageBlocker>())
            {
                if (comp.GetMaterialCostForRefill() > 0) return comp;
            }
            return null;
        }

        private static IReplenishable FindPawnApparelReplenishable(Pawn target)
        {
            if (target?.apparel?.WornApparel == null) return null;
            foreach (Apparel apparel in target.apparel.WornApparel)
            {
                IReplenishable comp = FindThingCompReplenishable(apparel);
                if (comp != null) return comp;
            }
            return null;
        }
    }
}
