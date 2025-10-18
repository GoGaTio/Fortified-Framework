using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Fortified
{
    public class JobDriver_FinishAutonomousBill : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Thing thing = job.GetTarget(TargetIndex.A).Thing;
            if (!pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed))
            {
                return false;
            }
            if (thing != null && thing.def.hasInteractionCell && !pawn.ReserveSittableOrSpot(thing.InteractionCell, job, errorOnFailed))
            {
                return false;
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Building_WorkTableAutonomous building = base.TargetThingA as Building_WorkTableAutonomous;
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Toil toil = Toils_General.WaitWith(TargetIndex.A, building.GetWorkTime(), useProgressBar: true, maintainPosture: true);
            yield return toil;
            var t = new Toil();
            t.AddPreInitAction(() =>
            {
                building.Finish((pawn));
            });
            yield return t;
        }
    }
}