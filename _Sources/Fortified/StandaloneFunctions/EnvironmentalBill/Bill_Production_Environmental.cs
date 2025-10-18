using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Fortified
{
    public class Bill_Production_Environmental : Bill_Production
    {
        public Building_WorkTable WorkBench => billStack.billGiver as Building_WorkTable;

        public ModExt_EnvironmentalBill Extension => recipe.GetModExtension<ModExt_EnvironmentalBill>();

        public Bill_Production_Environmental()
        {
        }
        public Bill_Production_Environmental(RecipeDef recipe, Precept_ThingStyle precept = null) : base(recipe, precept)
        {
        }
        public override void ExposeData()
        {
            base.ExposeData();
        }

        public override bool ShouldDoNow()
        {
            if (suspended || !base.ShouldDoNow())
            {
                return false;
            }
            if (!CheckEnvironment())
            {
                if (billStack.billGiver is Building_WorkTableAutonomous at && at.IsWorking())
                {
                    at.Cancel();
                }
                return false;
            }
            return true;
        }
        protected bool CheckEnvironment()
        {
            bool passed = true;
            if (Extension == null) return passed;
            if (Extension.OnlyInVacuum && !(WorkBench.Map.Biome.inVacuum || OrbitUtility.InVacuum(WorkBench)))
            {
                suspended = true;
                Messages.Message("FFF.Message.BillSuspendedInNonVacuum".Translate(Label, WorkBench.Label), MessageTypeDefOf.CautionInput);
                passed = false;
            }
            if (Extension.OnlyInMicroGravity && !OrbitUtility.InMicroGravity(WorkBench))
            {
                suspended = true;
                Messages.Message("FFF.Message.BillSuspendedInNonMicroGravity".Translate(Label, WorkBench.Label), MessageTypeDefOf.CautionInput);
                passed = false;
            }
            if (Extension.OnlyInDarkness && !OrbitUtility.InDarkness(WorkBench))
            {
                suspended = true;
                Messages.Message("FFF.Message.BillSuspendedInLight".Translate(Label, WorkBench.Label), MessageTypeDefOf.CautionInput);
                passed = false;
            }
            if (Extension.OnlyInCleanliness && !OrbitUtility.InCleanRoom(WorkBench))
            {
                suspended = true;
                Messages.Message("FFF.Message.BillSuspendedInDirtiness".Translate(Label, WorkBench.Label), MessageTypeDefOf.CautionInput);
                passed = false;
            }
            return passed;
        }

        public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
        {
            if (repeatMode == BillRepeatModeDefOf.RepeatCount)
            {
                if (repeatCount > 0)
                {
                    repeatCount--;
                }
                if (repeatCount == 0)
                {
                    Messages.Message("MessageBillComplete".Translate(LabelCap), (Thing)billStack.billGiver, MessageTypeDefOf.TaskCompletion);
                }
            }
            recipe.Worker.Notify_IterationCompleted(billDoer, ingredients);
        }
    }
}