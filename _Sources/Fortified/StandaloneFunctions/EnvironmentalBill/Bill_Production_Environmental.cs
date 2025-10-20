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
            if (!EnvironmentCanDoNow())
            {
                if (billStack.billGiver is Building_WorkTableAutonomous at && at.IsWorking())
                {
                    at.Cancel();
                }
                //else if (billStack.billGiver is Building_WorkTable wt && wt.IsWorking())
                //{
                //    suspended = true;
                //}
                return false;
            }
            return true;
        }

        protected bool EnvironmentCanDoNow()
        {
            bool passed = true;
            if (Extension == null) return true;
            //if (WorkBench == null) return false;
            //if (WorkBench.Map == null) return false;
            //if (!WorkBench.Spawned) return false;

            //潔净度相關
            if (Extension.OnlyInCleanliness)
            {
                AcceptanceReport report = EnvironmentUtility.InCleanRoom(WorkBench, Extension.CleanlinessRequirement);
                if (!report.Accepted) passed = SendSuspendedMessage(report.Reason);
            }

            //溫度相關
            if (Extension.TemperatureRestricted)
            {
                AcceptanceReport report = EnvironmentUtility.InTemperature(WorkBench, Extension.AllowedTemperatureRange);
                if (!report.Accepted) passed = SendSuspendedMessage(report.Reason);
            }

            //光照相關
            if (Extension.LightnessRestricted && Extension.OnlyInDarkness)
            {
                AcceptanceReport report = EnvironmentUtility.InLightnessBetween(WorkBench, new FloatRange(Extension.LightnessRequirement, Extension.DarknessRequirement));
                if (!report.Accepted) passed = SendSuspendedMessage(report.Reason);
            }
            else
            {
                if (Extension.LightnessRestricted)
                {
                    AcceptanceReport report = EnvironmentUtility.InLightness(WorkBench, Extension.LightnessRequirement);
                    if (!report.Accepted) passed = SendSuspendedMessage(report.Reason);
                }
                else if (Extension.OnlyInDarkness)
                {
                    AcceptanceReport report = EnvironmentUtility.InDarkness(WorkBench, Extension.DarknessRequirement);
                    if (!report.Accepted) passed = SendSuspendedMessage(report.Reason);
                }
            }

            //真空相關
            if (Extension.OnlyInVacuum && Extension.PressureRestricted)
            {
                AcceptanceReport report = EnvironmentUtility.InPressureBetween(WorkBench, new FloatRange(Extension.PressureRequirement, Extension.VacuumRequirement));
                if (!report.Accepted) passed = SendSuspendedMessage(report.Reason);
            }
            else
            {
                if (Extension.PressureRestricted)
                {
                    AcceptanceReport report = EnvironmentUtility.InPressure(WorkBench, Extension.PressureRequirement);
                    if (!report.Accepted) passed = SendSuspendedMessage(report.Reason);
                }
                else if (Extension.OnlyInVacuum)
                {
                    AcceptanceReport report = EnvironmentUtility.InVacuum(WorkBench,Extension.VacuumRequirement);
                    if (!report.Accepted) passed = SendSuspendedMessage(report.Reason);
                }
            }

            //重力相關
            if (Extension.OnlyInMicroGravity)
            {
                AcceptanceReport report = EnvironmentUtility.InMicroGravity(WorkBench);
                if (!report.Accepted) passed = SendSuspendedMessage(report.Reason);
            }

            return passed;
        }
        private bool SendSuspendedMessage(string reason)
        {
            if(!suspended) suspended = true;
            if (DebugSettings.godMode) Log.Message(reason);
            Messages.Message("FFF.Message.BillSuspended".Translate(Label, WorkBench.Label) + ": " + reason, lookTargets: WorkBench, MessageTypeDefOf.CautionInput);
            return false;
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