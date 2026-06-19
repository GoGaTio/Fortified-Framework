using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace Fortified
{
    // 信息卡条目构建
    public static class DamageBlockerDisplayUtility
    {
        private const int BasePriority = 1140;

        private static StatCategoryDef categoryInt;

        // 独立信息卡分类
        private static StatCategoryDef Category
        {
            get
            {
                if (categoryInt == null)
                    categoryInt = new StatCategoryDef
                    {
                        defName = "FFF_DamageBlocker",
                        label = "FFF.DamageBlocker.Stat.Category".Translate(),
                        displayOrder = 11
                    };
                return categoryInt;
            }
        }

        // 生成阻挡配置条目
        public static IEnumerable<StatDrawEntry> BuildStats(DamageBlockerStatData data)
        {
            if (data == null) yield break;
            StatCategoryDef cat = Category;

            yield return BuildThreshold(cat, data);
            yield return BuildBehavior(cat, data);
            if (!data.isArmorMode)
                yield return BuildCharges(cat, data);
            StatDrawEntry filter = BuildFilter(cat, data);
            if (filter != null) yield return filter;
            if (!data.eraEntries.NullOrEmpty())
                yield return BuildEra(cat, data);
        }

        // 取键 留空用默认
        private static string Key(string custom, string fallback)
        {
            return custom.NullOrEmpty() ? fallback : custom;
        }

        // 阈值行
        private static StatDrawEntry BuildThreshold(StatCategoryDef cat, DamageBlockerStatData data)
        {
            string op = data.inclusive ? "≥" : ">";
            return new StatDrawEntry(cat,
                Key(data.thresholdLabelKey, "FFF.DamageBlocker.Stat.Threshold").Translate(),
                op + data.threshold.ToString("F0"),
                Key(data.thresholdDescKey, "FFF.DamageBlocker.Stat.ThresholdDesc").Translate(),
                BasePriority);
        }

        // 层数行
        private static StatDrawEntry BuildCharges(StatCategoryDef cat, DamageBlockerStatData data)
        {
            return new StatDrawEntry(cat,
                Key(data.chargesLabelKey, "FFF.DamageBlocker.Stat.Charges").Translate(),
                data.charges.ToString(),
                Key(data.chargesDescKey, "FFF.DamageBlocker.Stat.ChargesDesc").Translate(),
                BasePriority - 1);
        }

        // 高低阈值行为行
        private static StatDrawEntry BuildBehavior(StatCategoryDef cat, DamageBlockerStatData data)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("FFF.DamageBlocker.Stat.BehaviorAbove".Translate(
                AboveActionText(data), ConsumeText(data.consumeAbove)));
            sb.AppendLine("FFF.DamageBlocker.Stat.BehaviorBelow".Translate(
                BelowActionText(data), ConsumeText(data.consumeBelow)));
            return new StatDrawEntry(cat,
                Key(data.behaviorLabelKey, "FFF.DamageBlocker.Stat.Behavior").Translate(),
                AboveActionText(data),
                Key(data.behaviorDescKey, "FFF.DamageBlocker.Stat.BehaviorDesc").Translate()
                    + "\n\n" + sb.ToString().TrimEndNewlines(),
                BasePriority - 2);
        }

        // 高阈值动作文本
        private static string AboveActionText(DamageBlockerStatData data)
        {
            if (data.blockAbove) return "FFF.DamageBlocker.Stat.ActionBlock".Translate();
            if (data.clampAbove) return "FFF.DamageBlocker.Stat.ActionClamp".Translate(data.threshold.ToString("F0"));
            return "FFF.DamageBlocker.Stat.ActionPass".Translate();
        }

        // 低阈值动作文本
        private static string BelowActionText(DamageBlockerStatData data)
        {
            return data.blockBelow
                ? "FFF.DamageBlocker.Stat.ActionBlock".Translate()
                : "FFF.DamageBlocker.Stat.ActionPass".Translate();
        }

        // 扣层文本
        private static string ConsumeText(bool consume)
        {
            return consume
                ? "FFF.DamageBlocker.Stat.ConsumeYes".Translate()
                : "FFF.DamageBlocker.Stat.ConsumeNo".Translate();
        }

        // 生效条件行 全开则省略
        private static StatDrawEntry BuildFilter(StatCategoryDef cat, DamageBlockerStatData data)
        {
            StringBuilder sb = new StringBuilder();
            AppendSourceFilter(sb, data);
            AppendDamageFilter(sb, data);
            AppendWeaponFilter(sb, data);
            if (sb.Length == 0) return null;
            return new StatDrawEntry(cat,
                Key(data.filterLabelKey, "FFF.DamageBlocker.Stat.Filter").Translate(),
                "FFF.DamageBlocker.Stat.FilterValue".Translate(),
                Key(data.filterDescKey, "FFF.DamageBlocker.Stat.FilterDesc").Translate()
                    + "\n\n" + sb.ToString().TrimEndNewlines(),
                BasePriority - 3);
        }

        // 攻击来源 仅在有限制时显示
        private static void AppendSourceFilter(StringBuilder sb, DamageBlockerStatData data)
        {
            if (data.allowRanged && data.allowMelee && data.allowDirect) return;
            List<string> sources = new List<string>();
            if (data.allowRanged) sources.Add("FFF.DamageBlocker.Stat.SourceRanged".Translate());
            if (data.allowMelee) sources.Add("FFF.DamageBlocker.Stat.SourceMelee".Translate());
            if (data.allowDirect) sources.Add("FFF.DamageBlocker.Stat.SourceDirect".Translate());
            string list = sources.Count == 0
                ? "FFF.DamageBlocker.Stat.SourceNone".Translate().ToString()
                : string.Join(", ", sources);
            sb.AppendLine("FFF.DamageBlocker.Stat.FilterSource".Translate(list));
        }

        // 伤害类型白黑名单
        private static void AppendDamageFilter(StringBuilder sb, DamageBlockerStatData data)
        {
            if (!data.allowedDamageDefs.NullOrEmpty())
                sb.AppendLine("FFF.DamageBlocker.Stat.FilterAllowed".Translate(
                    string.Join(", ", data.allowedDamageDefs.Select(d => d.label))));
            if (!data.excludedDamageDefs.NullOrEmpty())
                sb.AppendLine("FFF.DamageBlocker.Stat.FilterExcluded".Translate(
                    string.Join(", ", data.excludedDamageDefs.Select(d => d.label))));
        }

        // 武器标签白名单
        private static void AppendWeaponFilter(StringBuilder sb, DamageBlockerStatData data)
        {
            if (!data.allowedWeaponTags.NullOrEmpty())
                sb.AppendLine("FFF.DamageBlocker.Stat.FilterWeaponTags".Translate(
                    string.Join(", ", data.allowedWeaponTags)));
        }

        // ERA自伤行
        private static StatDrawEntry BuildEra(StatCategoryDef cat, DamageBlockerStatData data)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("FFF.DamageBlocker.Stat.EraTrigger".Translate(
                TriggerText(data.eraOnConsume, data.eraOnHit, data.eraOnAbove, data.eraOnBelow)));
            foreach (ERAEntry entry in data.eraEntries)
                sb.AppendLine(EntryText(entry));
            return new StatDrawEntry(cat,
                Key(data.eraLabelKey, "FFF.DamageBlocker.Stat.Era").Translate(),
                "FFF.DamageBlocker.Stat.EraValue".Translate(data.eraEntries.Count),
                sb.ToString().TrimEndNewlines(),
                BasePriority - 4);
        }

        // 单条自伤描述 含触发条件
        private static string EntryText(ERAEntry entry)
        {
            if (!entry.labelKey.NullOrEmpty())
                return entry.labelKey.Translate();
            if (entry.damageDef == null || entry.damageAmount <= 0f)
                return "FFF.DamageBlocker.Stat.EraEntryNone".Translate();
            string part = entry.hitPart != null
                ? entry.hitPart.label
                : "FFF.DamageBlocker.Stat.EraPartRandom".Translate().ToString();
            string effect = "FFF.DamageBlocker.Stat.EraEntry".Translate(
                entry.damageAmount.ToString("F0"), entry.damageDef.label, part);
            if (entry.armorStat == null)
                return effect;
            return "FFF.DamageBlocker.Stat.EraEntryCond".Translate(
                entry.armorStat.label, entry.requiredArmorValue.ToStringPercent(), effect);
        }

        // 触发时机文本
        private static string TriggerText(bool onConsume, bool onHit, bool onAbove, bool onBelow)
        {
            List<string> conds = new List<string>();
            if (onHit) conds.Add("FFF.DamageBlocker.Stat.EraOnHit".Translate());
            if (onConsume) conds.Add("FFF.DamageBlocker.Stat.EraOnConsume".Translate());
            if (onAbove) conds.Add("FFF.DamageBlocker.Stat.EraOnAbove".Translate());
            if (onBelow) conds.Add("FFF.DamageBlocker.Stat.EraOnBelow".Translate());
            if (conds.Count == 0) return "FFF.DamageBlocker.Stat.EraNever".Translate();
            return string.Join(", ", conds);
        }
    }
}
