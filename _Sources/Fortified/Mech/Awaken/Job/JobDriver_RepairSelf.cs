using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse.AI;
using Verse;

namespace Fortified
{

    public class JobDriver_RepairSelf : JobDriver
    {
        private const int DefaultTicksPerHeal = 120;

        protected int ticksToNextRepair;

        protected virtual bool Remote => false;

        protected int TicksPerHeal => Mathf.RoundToInt(1f / pawn.GetStatValue(StatDefOf.MechRepairSpeed) * 120f);
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (!ModLister.CheckBiotech("Mech repair"))
            {
                yield break;
            }
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnForbidden(TargetIndex.A);
            Toil toil = Toils_General.Wait(int.MaxValue, TargetIndex.None);
            toil.WithEffect(EffecterDefOf.MechRepairing, TargetIndex.A, null);
            toil.PlaySustainerOrSound(SoundDefOf.RepairMech_Touch, 1f);
            toil.AddPreInitAction(delegate
            {
                this.ticksToNextRepair = this.TicksPerHeal;
            });
            toil.tickIntervalAction = delegate (int delta)
            {
                ticksToNextRepair -= delta;
                if (ticksToNextRepair <= 0)
                {
                    pawn.needs.energy.CurLevel -= pawn.GetStatValue(StatDefOf.MechEnergyLossPerHP) * (float)delta;
                    MechRepairUtility.RepairTick(pawn);
                    ticksToNextRepair = TicksPerHeal;
                }
                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Crafting, 0.05f * (float)delta);
                }
            };
            toil.AddEndCondition(() => MechRepairUtility.CanRepair(pawn) ? JobCondition.Ongoing : JobCondition.Succeeded);
            yield return toil;
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref this.ticksToNextRepair, "ticksToNextRepair", 0, false);
        }
    }
}