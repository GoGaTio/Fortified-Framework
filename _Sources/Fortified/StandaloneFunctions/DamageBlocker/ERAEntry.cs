using System.Linq;
using RimWorld;
using Verse;

namespace Fortified
{
    public class ERAEntry : IExposable
    {
        public StatDef armorStat;
        public float requiredArmorValue;
        public DamageDef damageDef;
        public float damageAmount;
        public RulePackDef battleLogRulePack;
        // 自伤命中部位 省略则随机
        public BodyPartDef hitPart;
        // 信息卡自定义文本键 留空用默认
        public string labelKey;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref armorStat, "armorStat");
            Scribe_Values.Look(ref requiredArmorValue, "requiredArmorValue");
            Scribe_Defs.Look(ref damageDef, "damageDef");
            Scribe_Values.Look(ref damageAmount, "damageAmount");
            Scribe_Defs.Look(ref battleLogRulePack, "battleLogRulePack");
            Scribe_Defs.Look(ref hitPart, "hitPart");
            Scribe_Values.Look(ref labelKey, "labelKey");
        }

        public bool ShouldApply(Pawn pawn)
        {
            if (pawn == null) return false;
            if (armorStat == null) return true;
            return pawn.GetStatValue(armorStat) < requiredArmorValue;
        }

        public void Apply(Pawn pawn)
        {
            if (pawn == null || damageDef == null || damageAmount <= 0f) return;
            if (battleLogRulePack != null)
                Find.BattleLog.Add(new BattleLogEntry_DamageBlocker(pawn, battleLogRulePack, null, damageAmount));
            DamageInfo dinfo = new DamageInfo(damageDef, damageAmount, instigator: null, intendedTarget: pawn);
            dinfo.SetIgnoreArmor(true);
            BodyPartRecord part = ResolveHitPart(pawn);
            if (part != null) dinfo.SetHitPart(part);
            pawn.TakeDamage(dinfo);
        }

        // 取活着的指定部位 缺失则返回null走随机
        private BodyPartRecord ResolveHitPart(Pawn pawn)
        {
            if (hitPart == null) return null;
            return pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(p => p.def == hitPart);
        }
    }
}

