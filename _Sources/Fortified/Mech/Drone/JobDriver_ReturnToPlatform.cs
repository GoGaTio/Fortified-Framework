using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Fortified
{

    public class JobDriver_ReturnToPlatform : JobDriver
    {
        private const TargetIndex PlatformInd = TargetIndex.A;
        private const int DurationTicks = 180;

        private Pawn Actor => this.GetActor();
        private Thing Platform
        {
            get
            {
                return job.GetTarget(TargetIndex.A).TryGetPawn(out var p) ? job.GetTarget(TargetIndex.A).Pawn as Thing : job.GetTarget(TargetIndex.A).Thing;
            }
        }

        private Apparel Apparel
        {
            get
            {
                if (job.GetTarget(TargetIndex.B) == null) return null;
                return job.GetTarget(TargetIndex.B).Thing as Apparel;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Platform, job, 1, -1, null, errorOnFailed, true);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnDespawnedOrNull(TargetIndex.A);
            Toil toil = Toils_General.Wait(DurationTicks);
            toil.WithProgressBarToilDelay(TargetIndex.None);
            toil.FailOnDespawnedOrNull(TargetIndex.A);
            toil.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            yield return toil;
            yield return Toils_General.Do(RefillPlatformCost);
        }

        private static readonly FieldInfo _fiRemaining = AccessTools.Field(typeof(CompApparelReloadable), "remainingCharges");

        protected void RefillPlatformCost()
        {
            DropEquipment();
            if (Apparel !=null && Apparel.Wearer != null)
            {
                if (Apparel.TryGetComp<CompApparelReloadable>(out var apparelReloadable))
                {
                    if (apparelReloadable.RemainingCharges == apparelReloadable.MaxCharges)
                    {
                        Recycle();
                        DespawnAndDestroy();
                        return;
                    }
                    _fiRemaining.SetValue(apparelReloadable, apparelReloadable.RemainingCharges + 1);
                }
            }
            else if (Platform.TryGetComp<CompMechPlatform>(out var p))
            {
                p.Retracted(this.Actor);
            }
            DespawnAndDestroy();
        }
        protected void Recycle()
        {
            foreach (Thing item in Actor.ButcherProducts(null, 0.75f))
            {
                GenPlace.TryPlaceThing(item, Actor.Position, Actor.Map, ThingPlaceMode.Near, out _);
            }
        }
        protected void DropEquipment()
        {
            Actor.equipment?.DropAllEquipment(Actor.Position, false, false);
            Actor.inventory?.DropAllNearPawn(Actor.Position, false, false);
        }
        protected void DespawnAndDestroy()
        {
            this.EndOnDespawnedOrNull(TargetIndex.A);
            Actor.DeSpawnOrDeselect();
            Actor.Destroy();
        }
        public override void ExposeData()
        {
            base.ExposeData();
        }
    }
}