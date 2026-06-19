using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Fortified
{
    // 服装阻挡
    public class Comp_DamageBlocker : ThingComp, IReplenishable, IDamageBlockerDisplay, IPreApplyDamageHandler
    {
        private CompProperties_DamageBlocker Props => (CompProperties_DamageBlocker)props;
        private int currentCharges;
        private int rechargeTicker;
        private bool applyingERA;
        private Pawn Wearer => (parent as Apparel)?.Wearer;
        private ThingDef Stuff => parent?.Stuff;

        public int CurrentCharges => currentCharges;
        public int MaxCharges => Mathf.Max(0, Mathf.RoundToInt(Props.blockCharges * ChargeFactor));
        public float Threshold => Props.damageThreshold * ThresholdFactor;
        public string ThresholdOperator => Props.thresholdInclusive ? "≥" : ">";
        public string ThresholdLabelKey => Props.thresholdLabelKey;
        public string ChargesLabelKey => Props.chargesLabelKey;
        public bool IsArmorMode => !Props.consumeChargeAbove && !Props.consumeChargeBelow;
        public int PreApplyDamagePriority => Props.preApplyDamagePriority;

        private float CurrentThreshold => Threshold;
        private float ChargeFactor => DamageBlockerUtility.StuffFactor(Props.chargeFactorStat, Stuff);
        private float ThresholdFactor => DamageBlockerUtility.StuffFactor(Props.thresholdFactorStat, Stuff);
        private ThingDef RepairMaterial => DamageBlockerUtility.RepairMaterial(Props.useStuffForReplenish, Stuff, Props.repairMaterial);
        public float DurabilityRestorePerMaterial => Props.chargesPerMaterial;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
                currentCharges = MaxCharges;
        }

        // 穿戴时注册pawn层钩子
        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            PreApplyDamageRegistry.EnsurePawnComp(pawn);
        }

        // 读档恢复时补挂钩子
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref currentCharges, "blockCharges", MaxCharges);
            Scribe_Values.Look(ref rechargeTicker, "rechargeTicker");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                currentCharges = Math.Min(currentCharges, MaxCharges);
                PreApplyDamageRegistry.EnsurePawnComp(Wearer);
            }
        }

        // 改由pawn层池调用 关闭服装层拦截
        public void HandlePreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (applyingERA) return;
            if (!IsArmorMode && currentCharges <= 0) return;
            if (!dinfo.Def.harmsHealth) return;
            if (!DamageBlockerUtility.PassesFilter(ref dinfo, Props.allowedDamageDefs, Props.excludedDamageDefs, Props.allowedWeaponTags, Props.allowRanged, Props.allowMelee, Props.allowDirect)) return;

            bool isAbove = IsAboveThreshold(dinfo.Amount);
            if (isAbove)
                HandleAboveThreshold(ref dinfo, ref absorbed);
            else
                HandleBelowThreshold(ref dinfo, ref absorbed);
        }

        private bool IsAboveThreshold(float amount)
        {
            return DamageBlockerUtility.IsAboveThreshold(amount, CurrentThreshold, Props.thresholdInclusive);
        }

        private void HandleAboveThreshold(ref DamageInfo dinfo, ref bool absorbed)
        {
            float original = dinfo.Amount;
            bool consumed = false;
            if (Props.consumeChargeAbove)
            {
                currentCharges--;
                consumed = true;
            }
            if (Props.blockAboveThreshold)
            {
                BlockDamage(ref dinfo, ref absorbed);
                ShowBlockerText(Props.aboveThresholdTextKey);
                LogBattle(dinfo.Instigator, original);
            }
            else if (Props.clampAboveToThreshold)
            {
                dinfo.SetAmount(CurrentThreshold);
                ShowBlockerText(Props.aboveThresholdTextKey);
                LogBattle(dinfo.Instigator, original - CurrentThreshold);
            }
            PlayHitEffect(Props.aboveThresholdEffecter);
            TryERA(consumed, isAbove: true);
        }

        private void HandleBelowThreshold(ref DamageInfo dinfo, ref bool absorbed)
        {
            float original = dinfo.Amount;
            bool consumed = false;
            if (Props.consumeChargeBelow)
            {
                currentCharges--;
                consumed = true;
            }
            if (Props.blockBelowThreshold)
            {
                BlockDamage(ref dinfo, ref absorbed);
                ShowBlockerText(Props.belowThresholdTextKey);
                LogBattle(dinfo.Instigator, original);
            }
            PlayHitEffect(Props.belowThresholdEffecter);
            TryERA(consumed, isAbove: false);
        }

        private void BlockDamage(ref DamageInfo dinfo, ref bool absorbed)
        {
            absorbed = true;
            dinfo.SetAmount(0f);
        }

        private void TryERA(bool chargeConsumed, bool isAbove)
        {
            if (Props.eraEntries.NullOrEmpty()) return;
            Pawn wearer = Wearer;
            if (wearer == null) return;
            if (!DamageBlockerUtility.ShouldTriggerERA(chargeConsumed, isAbove,
                Props.eraOnConsumeCharge, Props.eraOnHit, Props.eraOnAboveThreshold, Props.eraOnBelowThreshold)) return;

            // 预存坐标 自伤可能致死卸下装甲
            Map map = wearer.MapHeld;
            IntVec3 pos = wearer.PositionHeld;

            try
            {
                applyingERA = true;
                if (DamageBlockerUtility.ApplyERAEntries(Props.eraEntries, wearer))
                    PlayEffectAt(Props.eraEffecter, pos, map);
            }
            finally
            {
                applyingERA = false;
            }
        }

        private void PlayEffectAt(EffecterDef effecterDef, IntVec3 pos, Map map)
        {
            if (effecterDef == null || map == null || !pos.IsValid) return;
            effecterDef.Spawn(pos, map).Cleanup();
        }

        private void PlayHitEffect(EffecterDef effecterDef)
        {
            Thing target = parent.Spawned ? parent : Wearer;
            if (effecterDef == null || target == null || !target.Spawned) return;
            effecterDef.SpawnAttached(target, target.Map).Cleanup();
        }

        private void LogBattle(Thing instigator, float damageAbsorbed)
        {
            if (Props.battleLogRulePack == null) return;
            Pawn wearer = Wearer;
            if (wearer == null) return;
            Find.BattleLog.Add(new BattleLogEntry_DamageBlocker(wearer, Props.battleLogRulePack, instigator, damageAbsorbed));
        }

        private void ShowBlockerText(string textKey)
        {
            if (textKey.NullOrEmpty()) return;
            Pawn wearer = Wearer;
            if (wearer == null || !wearer.Spawned) return;
            MoteMaker.ThrowText(wearer.DrawPos, wearer.Map, textKey.Translate());
        }

        public int GetMaterialCostForRefill()
        {
            if (RepairMaterial == null || Props.chargesPerMaterial <= 0) return 0;
            int needed = MaxCharges - currentCharges;
            if (needed <= 0) return 0;
            return Math.Max(1, (int)Math.Ceiling((float)needed / Props.chargesPerMaterial));
        }

        public void Replenish(Pawn actor, int materialCount)
        {
            if (materialCount <= 0) return;
            currentCharges = Math.Min(MaxCharges, currentCharges + materialCount * Props.chargesPerMaterial);
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            FloatMenuOption option = BuildReplenishOption(selPawn, parent);
            if (option != null) yield return option;
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetWornGizmosExtra()) yield return gizmo;
            if (Props.showGizmo) yield return new Gizmo_DamageBlockerStatus { blocker = this };
            Command_Action command = BuildSelfReplenishCommand();
            if (command != null) yield return command;
        }

        private FloatMenuOption BuildReplenishOption(Pawn actor, Thing workTarget)
        {
            if (!CanReplenish(actor)) return null;
            string label = ReplenishLabel();
            return new FloatMenuOption(label, () => TryStartReplenishJob(actor, workTarget, parent));
        }

        private Command_Action BuildSelfReplenishCommand()
        {
            Pawn wearer = Wearer;
            if (!CanReplenish(wearer)) return null;
            return new Command_Action
            {
                defaultLabel = ReplenishLabel(),
                icon = parent.def.uiIcon,
                action = () => TryStartReplenishJob(wearer, wearer, parent)
            };
        }

        private bool CanReplenish(Pawn actor)
        {
            return GetMaterialCostForRefill() > 0 && RepairMaterial != null
                && DamageBlockerUtility.CanUseWork(actor, Props.requiredReplenishWorkType);
        }

        private string ReplenishLabel()
        {
            return Props.replenishLabelKey.Translate(RepairMaterial.label, GetMaterialCostForRefill());
        }

        private void TryStartReplenishJob(Pawn actor, Thing workTarget, Thing exactTarget)
        {
            List<Thing> materials = HaulAIUtility.FindFixedIngredientCount(actor, RepairMaterial, GetMaterialCostForRefill());
            if (materials.NullOrEmpty()) return;
            Job job = MakeReplenishJob(materials[0], workTarget, exactTarget);
            actor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        private Job MakeReplenishJob(Thing material, Thing workTarget, Thing exactTarget)
        {
            Job job = JobMaker.MakeJob(FFF_DefOf.FFF_Replenish, material, workTarget);
            job.count = Math.Max(1, Math.Min(material.stackCount, GetMaterialCostForRefill()));
            if (exactTarget != workTarget) job.SetTarget(TargetIndex.C, exactTarget);
            return job;
        }

        public override void CompTick()
        {
            if (!Props.autoRecharge || currentCharges >= MaxCharges) return;
            rechargeTicker++;
            if (rechargeTicker >= Props.rechargeIntervalTicks)
            {
                rechargeTicker = 0;
                currentCharges++;
            }
        }

        public override string CompInspectStringExtra()
        {
            return Props.chargesLabelKey.Translate(currentCharges, MaxCharges)
                + "\n" + Props.thresholdLabelKey.Translate(Props.thresholdInclusive ? "≥" : ">", CurrentThreshold.ToString("F0"));
        }
    }
}
