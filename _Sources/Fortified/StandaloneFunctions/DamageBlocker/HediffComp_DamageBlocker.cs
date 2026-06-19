using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Fortified
{
    // Hediff阻挡
    public class HediffComp_DamageBlocker : HediffComp_PreApplyDamage, IReplenishable, IDamageBlockerDisplay
    {
        private HediffCompProperties_DamageBlocker Props => (HediffCompProperties_DamageBlocker)props;
        public HediffCompProperties_DamageBlocker PropsForDisplay => Props;
        private int currentCharges;
        private int rechargeTicker;
        private bool applyingERA;

        public int CurrentCharges => currentCharges;
        public int MaxCharges => Props.blockCharges;
        public float Threshold => Props.damageThreshold;
        public string ThresholdOperator => Props.thresholdInclusive ? "≥" : ">";
        public string ThresholdLabelKey => Props.thresholdLabelKey;
        public string ChargesLabelKey => Props.chargesLabelKey;
        public bool IsArmorMode => !Props.consumeChargeAbove && !Props.consumeChargeBelow;
        public override int PreApplyDamagePriority => Props.preApplyDamagePriority;

        // 整备接口
        public float DurabilityRestorePerMaterial => Props.chargesPerMaterial;
        public int GetMaterialCostForRefill() => Props.chargesPerMaterial <= 0 || currentCharges >= Props.blockCharges ? 0
            : Math.Max(1, (int)Math.Ceiling((float)(Props.blockCharges - currentCharges) / Props.chargesPerMaterial));
        public void Replenish(Pawn actor, int materialCount)
        {
            if (materialCount <= 0) return;
            currentCharges = Math.Min(Props.blockCharges, currentCharges + materialCount * Props.chargesPerMaterial);
        }

        public override void CompPostMake()
        {
            base.CompPostMake();
            currentCharges = Props.blockCharges;
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (applyingERA) return;
            if (!dinfo.Def.harmsHealth) return;
            if (!IsArmorMode && currentCharges <= 0) return;
            if (!DamageBlockerUtility.PassesFilter(ref dinfo, Props.allowedDamageDefs, Props.excludedDamageDefs, Props.allowedWeaponTags, Props.allowRanged, Props.allowMelee, Props.allowDirect)) return;

            bool isAbove = IsAboveThreshold(dinfo.Amount);
            if (isAbove)
                HandleAboveThreshold(ref dinfo, ref absorbed);
            else
                HandleBelowThreshold(ref dinfo, ref absorbed);
        }

        private bool IsAboveThreshold(float amount)
        {
            return DamageBlockerUtility.IsAboveThreshold(amount, Props.damageThreshold, Props.thresholdInclusive);
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
                dinfo.SetAmount(Props.damageThreshold);
                ShowBlockerText(Props.aboveThresholdTextKey);
                LogBattle(dinfo.Instigator, original - Props.damageThreshold);
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
            Pawn pawn = parent.pawn;
            if (pawn == null) return;
            if (!DamageBlockerUtility.ShouldTriggerERA(chargeConsumed, isAbove,
                Props.eraOnConsumeCharge, Props.eraOnHit, Props.eraOnAboveThreshold, Props.eraOnBelowThreshold)) return;

            // 预存坐标 自伤可能致死
            Map map = pawn.MapHeld;
            IntVec3 pos = pawn.PositionHeld;

            try
            {
                applyingERA = true;
                if (DamageBlockerUtility.ApplyERAEntries(Props.eraEntries, pawn))
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

        private void ShowBlockerText(string textKey)
        {
            if (textKey.NullOrEmpty()) return;
            Pawn pawn = parent.pawn;
            if (pawn == null || !pawn.Spawned) return;
            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, textKey.Translate());
        }

        private void PlayHitEffect(EffecterDef effecterDef)
        {
            Pawn pawn = parent.pawn;
            if (effecterDef == null || pawn == null || !pawn.Spawned) return;
            effecterDef.SpawnAttached(pawn, pawn.Map).Cleanup();
        }

        private void LogBattle(Thing instigator, float damageAbsorbed)
        {
            if (Props.battleLogRulePack == null) return;
            Pawn pawn = parent.pawn;
            if (pawn == null) return;
            Find.BattleLog.Add(new BattleLogEntry_DamageBlocker(pawn, Props.battleLogRulePack, instigator, damageAbsorbed));
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (!Props.autoRecharge || currentCharges >= Props.blockCharges) return;
            rechargeTicker++;
            if (rechargeTicker >= Props.rechargeIntervalTicks)
            {
                rechargeTicker = 0;
                currentCharges++;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (Props.showGizmo) yield return new Gizmo_DamageBlockerStatus { blocker = this };
        }

        public IEnumerable<FloatMenuOption> GetReplenishFloatMenuOptions(Pawn selPawn)
        {
            if (Props.repairMaterial == null || currentCharges >= Props.blockCharges) yield break;
            if (!DamageBlockerUtility.CanUseWork(selPawn, Props.requiredReplenishWorkType)) yield break;
            int cost = GetMaterialCostForRefill();
            string label = Props.replenishLabelKey.Translate(Props.repairMaterial.label, cost);
            List<Thing> mats = HaulAIUtility.FindFixedIngredientCount(selPawn, Props.repairMaterial, cost);
            if (mats.NullOrEmpty())
            {
                yield return new FloatMenuOption(label + " (" + "NoMaterialsAvailable".Translate() + ")", null);
                yield break;
            }
            yield return new FloatMenuOption(label, () =>
            {
                Job job = JobMaker.MakeJob(FFF_DefOf.FFF_Replenish, mats[0], parent.pawn);
                job.count = Math.Min(mats[0].stackCount, cost);
                selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            });
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref currentCharges, "blockCharges", Props.blockCharges);
            Scribe_Values.Look(ref rechargeTicker, "rechargeTicker");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                currentCharges = Math.Min(currentCharges, Props.blockCharges);
        }
    }
}
