using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Fortified
{
    // 伤害阻挡配置
    public class CompProperties_DamageBlocker : CompProperties
    {
        public float damageThreshold = 10f;
        public bool thresholdInclusive = true;
        public int blockCharges = 3;
        public StatDef chargeFactorStat;
        public StatDef thresholdFactorStat;

        // 伤害处理优先级 高者先挡
        public int preApplyDamagePriority = 0;

        // 过滤
        public List<DamageDef> allowedDamageDefs;
        public List<DamageDef> excludedDamageDefs;
        public List<string> allowedWeaponTags;
        public bool allowRanged = true;
        public bool allowMelee = true;
        public bool allowDirect = true;

        // 高阈值行为
        public bool consumeChargeAbove = true;
        public bool blockAboveThreshold = true;
        public bool clampAboveToThreshold = false;

        // 低阈值行为
        public bool consumeChargeBelow = false;
        public bool blockBelowThreshold = false;

        // 特效
        public EffecterDef belowThresholdEffecter;
        public EffecterDef aboveThresholdEffecter;
        public EffecterDef eraEffecter;

        // 战斗日志
        public RulePackDef battleLogRulePack;

        // Gizmo显示
        public bool showGizmo = true;

        // ERA
        public bool eraOnConsumeCharge = false;
        public bool eraOnHit = false;
        public bool eraOnAboveThreshold = false;
        public bool eraOnBelowThreshold = false;
        public List<ERAEntry> eraEntries;

        // 显示翻译键
        public string thresholdLabelKey = "FFF.DamageBlocker.Threshold";
        public string chargesLabelKey = "FFF.DamageBlocker.Charges";
        public string replenishLabelKey = "FFF.DamageBlocker.Replenish";
        public string aboveThresholdTextKey = "FFF.DamageBlocker.Absorbed";
        public string belowThresholdTextKey = "FFF.DamageBlocker.Absorbed";

        // 信息卡可覆盖键 留空用默认
        public string statThresholdDescKey;
        public string statChargesLabelKey;
        public string statChargesDescKey;
        public string statBehaviorLabelKey;
        public string statBehaviorDescKey;
        public string statFilterLabelKey;
        public string statFilterDescKey;
        public string statEraLabelKey;

        // 整备
        public bool useStuffForReplenish = false;
        public ThingDef repairMaterial;
        public int chargesPerMaterial = 1;
        public WorkTypeDef requiredReplenishWorkType;
        public bool autoRecharge = false;
        public int rechargeIntervalTicks = 2500;

        public CompProperties_DamageBlocker()
        {
            compClass = typeof(Comp_DamageBlocker);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats(req))
                yield return entry;
            foreach (StatDrawEntry entry in DamageBlockerDisplayUtility.BuildStats(BuildStatData()))
                yield return entry;
        }

        // 组装信息卡参数
        private DamageBlockerStatData BuildStatData()
        {
            return new DamageBlockerStatData
            {
                threshold = damageThreshold,
                inclusive = thresholdInclusive,
                charges = blockCharges,
                isArmorMode = !consumeChargeAbove && !consumeChargeBelow,
                consumeAbove = consumeChargeAbove,
                blockAbove = blockAboveThreshold,
                clampAbove = clampAboveToThreshold,
                consumeBelow = consumeChargeBelow,
                blockBelow = blockBelowThreshold,
                allowedDamageDefs = allowedDamageDefs,
                excludedDamageDefs = excludedDamageDefs,
                allowedWeaponTags = allowedWeaponTags,
                allowRanged = allowRanged,
                allowMelee = allowMelee,
                allowDirect = allowDirect,
                eraEntries = eraEntries,
                eraOnConsume = eraOnConsumeCharge,
                eraOnHit = eraOnHit,
                eraOnAbove = eraOnAboveThreshold,
                eraOnBelow = eraOnBelowThreshold,
                thresholdDescKey = statThresholdDescKey,
                chargesLabelKey = statChargesLabelKey,
                chargesDescKey = statChargesDescKey,
                behaviorLabelKey = statBehaviorLabelKey,
                behaviorDescKey = statBehaviorDescKey,
                filterLabelKey = statFilterLabelKey,
                filterDescKey = statFilterDescKey,
                eraLabelKey = statEraLabelKey
            };
        }
    }

    // Hediff配置
    public class HediffCompProperties_DamageBlocker : HediffCompProperties
    {
        public float damageThreshold = 10f;
        public bool thresholdInclusive = true;
        public int blockCharges = 3;

        // 伤害处理优先级 高者先挡
        public int preApplyDamagePriority = 0;

        // 过滤
        public List<DamageDef> allowedDamageDefs;
        public List<DamageDef> excludedDamageDefs;
        public List<string> allowedWeaponTags;
        public bool allowRanged = true;
        public bool allowMelee = true;
        public bool allowDirect = true;

        // 高阈值行为
        public bool consumeChargeAbove = true;
        public bool blockAboveThreshold = true;
        public bool clampAboveToThreshold = false;

        // 低阈值行为
        public bool consumeChargeBelow = false;
        public bool blockBelowThreshold = false;

        // 特效
        public EffecterDef belowThresholdEffecter;
        public EffecterDef aboveThresholdEffecter;
        public EffecterDef eraEffecter;

        // 战斗日志
        public RulePackDef battleLogRulePack;

        // Gizmo显示
        public bool showGizmo = true;

        // ERA
        public bool eraOnConsumeCharge = false;
        public bool eraOnHit = false;
        public bool eraOnAboveThreshold = false;
        public bool eraOnBelowThreshold = false;
        public List<ERAEntry> eraEntries;

        // 显示翻译键
        public string thresholdLabelKey = "FFF.DamageBlocker.Threshold";
        public string chargesLabelKey = "FFF.DamageBlocker.Charges";
        public string replenishLabelKey = "FFF.DamageBlocker.Replenish";
        public string aboveThresholdTextKey = "FFF.DamageBlocker.Absorbed";
        public string belowThresholdTextKey = "FFF.DamageBlocker.Absorbed";

        // 信息卡可覆盖键 留空用默认
        public string statThresholdDescKey;
        public string statChargesLabelKey;
        public string statChargesDescKey;
        public string statBehaviorLabelKey;
        public string statBehaviorDescKey;
        public string statFilterLabelKey;
        public string statFilterDescKey;
        public string statEraLabelKey;

        // 整备
        public bool autoRecharge = false;
        public int rechargeIntervalTicks = 2500;
        public ThingDef repairMaterial;
        public int chargesPerMaterial = 1;
        public WorkTypeDef requiredReplenishWorkType;

        public HediffCompProperties_DamageBlocker()
        {
            compClass = typeof(HediffComp_DamageBlocker);
        }

        // 组装信息卡参数
        public DamageBlockerStatData BuildStatData()
        {
            return new DamageBlockerStatData
            {
                threshold = damageThreshold,
                inclusive = thresholdInclusive,
                charges = blockCharges,
                isArmorMode = !consumeChargeAbove && !consumeChargeBelow,
                consumeAbove = consumeChargeAbove,
                blockAbove = blockAboveThreshold,
                clampAbove = clampAboveToThreshold,
                consumeBelow = consumeChargeBelow,
                blockBelow = blockBelowThreshold,
                allowedDamageDefs = allowedDamageDefs,
                excludedDamageDefs = excludedDamageDefs,
                allowedWeaponTags = allowedWeaponTags,
                allowRanged = allowRanged,
                allowMelee = allowMelee,
                allowDirect = allowDirect,
                eraEntries = eraEntries,
                eraOnConsume = eraOnConsumeCharge,
                eraOnHit = eraOnHit,
                eraOnAbove = eraOnAboveThreshold,
                eraOnBelow = eraOnBelowThreshold,
                thresholdDescKey = statThresholdDescKey,
                chargesLabelKey = statChargesLabelKey,
                chargesDescKey = statChargesDescKey,
                behaviorLabelKey = statBehaviorLabelKey,
                behaviorDescKey = statBehaviorDescKey,
                filterLabelKey = statFilterLabelKey,
                filterDescKey = statFilterDescKey,
                eraLabelKey = statEraLabelKey
            };
        }
    }
}
