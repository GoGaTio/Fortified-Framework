using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Fortified
{
    public class JobDriver_DoAutonomousBill : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Thing thing = job.GetTarget(TargetIndex.A).Thing;
            if (thing == null) return false; ;
            if (!pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed)) return false;
            if (thing != null && thing.def.hasInteractionCell && !pawn.ReserveSittableOrSpot(thing.InteractionCell, job, errorOnFailed))
            {
                return false;
            }
            pawn.ReserveAsManyAsPossible(job.GetTargetQueue(TargetIndex.B), job);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnBurningImmobile(TargetIndex.A);
            this.FailOn(delegate
            {
                if (job.GetTarget(TargetIndex.A).Thing is IBillGiver billGiver)
                {
                    if (job.bill != null && job.bill.DeletedOrDereferenced)
                    {
                        return true;
                    }
                    if (!billGiver.CurrentlyUsableForBills())
                    {
                        return true;
                    }
                }
                return false;
            });
            Building_WorkTableAutonomous building = (Building_WorkTableAutonomous)TargetThingA;
            AddEndCondition(delegate
            {
                Thing thing = GetActor().jobs.curJob.GetTarget(TargetIndex.A).Thing;
                return (!(thing is Building) || thing.Spawned) ? JobCondition.Ongoing : JobCondition.Incompletable;
            });
            Toil gotoBillGiver = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            yield return Toils_Jump.JumpIf(gotoBillGiver, () => job.GetTargetQueue(TargetIndex.B).NullOrEmpty());
            foreach (Toil item in JobDriver_DoBill.CollectIngredientsToils(TargetIndex.B, TargetIndex.A, TargetIndex.C, subtractNumTakenFromJobCount: false, failIfStackCountLessThanJobCount: true, placeInBillGiver: true))
            {
                yield return item;
            }
            yield return gotoBillGiver;
            Toil toil = Toils_General.WaitWith(TargetIndex.A, building.GetWorkTime(), useProgressBar: true, maintainPosture: true);
            toil.AddFinishAction(delegate
            {
                building.StartBill((Bill_Production)job.bill, TargetThingA, pawn);
            });
            yield return toil;
        }
    }
}