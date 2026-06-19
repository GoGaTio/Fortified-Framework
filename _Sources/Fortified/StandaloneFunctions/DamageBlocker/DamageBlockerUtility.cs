using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Fortified
{
    public static class DamageBlockerUtility
    {
        public static bool IsAboveThreshold(float amount, float threshold, bool inclusive)
        {
            return inclusive ? amount >= threshold : amount > threshold;
        }

        public static float StuffFactor(StatDef stat, ThingDef stuff)
        {
            if (stat == null || stuff == null) return 1f;
            return Math.Max(0f, stuff.GetStatValueAbstract(stat));
        }

        public static bool CanUseWork(Pawn pawn, WorkTypeDef workType)
        {
            return pawn != null && (workType == null || !pawn.WorkTypeIsDisabled(workType));
        }

        public static ThingDef RepairMaterial(bool useStuff, ThingDef stuff, ThingDef definedMaterial)
        {
            return useStuff ? stuff : definedMaterial;
        }

        public static bool PassesFilter(ref DamageInfo dinfo, List<DamageDef> allowed, List<DamageDef> excluded, List<string> allowedWeaponTags, bool allowRanged, bool allowMelee, bool allowDirect)
        {
            if (!allowed.NullOrEmpty() && !allowed.Contains(dinfo.Def)) return false;
            if (!excluded.NullOrEmpty() && excluded.Contains(dinfo.Def)) return false;

            if (!allowedWeaponTags.NullOrEmpty())
            {
                ThingDef weapon = dinfo.Weapon;
                if (weapon?.weaponTags == null) return false;
                bool tagMatch = false;
                foreach (string tag in allowedWeaponTags)
                {
                    if (weapon.weaponTags.Contains(tag)) { tagMatch = true; break; }
                }
                if (!tagMatch) return false;
            }

            bool isRanged = dinfo.Def.isRanged || (dinfo.Weapon != null && dinfo.Weapon.IsRangedWeapon);
            bool isMelee = dinfo.WeaponBodyPartGroup != null || (dinfo.Weapon != null && dinfo.Weapon.IsMeleeWeapon);
            bool isDirect = !isRanged && !isMelee;

            if (isRanged && !allowRanged) return false;
            if (isMelee && !allowMelee) return false;
            if (isDirect && !allowDirect) return false;
            return true;
        }

        // ERA触发条件判断
        public static bool ShouldTriggerERA(bool chargeConsumed, bool isAbove,
            bool eraOnConsumeCharge, bool eraOnHit, bool eraOnAboveThreshold, bool eraOnBelowThreshold)
        {
            if (chargeConsumed && eraOnConsumeCharge) return true;
            if (eraOnHit) return true;
            if (isAbove && eraOnAboveThreshold) return true;
            if (!isAbove && eraOnBelowThreshold) return true;
            return false;
        }

        // 执行ERA词条并返回是否有词条生效
        public static bool ApplyERAEntries(List<ERAEntry> entries, Pawn target)
        {
            if (entries.NullOrEmpty() || target == null) return false;
            bool anyApplied = false;
            foreach (var entry in entries)
            {
                if (entry.ShouldApply(target))
                {
                    entry.Apply(target);
                    anyApplied = true;
                }
            }
            return anyApplied;
        }
    }
}
