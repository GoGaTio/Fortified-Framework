using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Fortified
{
    // defname加成条目
    public class DefNameSightBonus
    {
        public ThingDef thing;
        public float add = 0f;
        public float mul = 1f;

        public void LoadDataFromXmlCustom(System.Xml.XmlNode xmlRoot)
        {
            DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, nameof(thing), xmlRoot.Name);
            var addNode = xmlRoot.SelectSingleNode("add");
            if (addNode != null) add = ParseHelper.FromString<float>(addNode.InnerText);
            var mulNode = xmlRoot.SelectSingleNode("mul");
            if (mulNode != null) mul = ParseHelper.FromString<float>(mulNode.InnerText);
        }
    }

    // pawn视野半径计算
    public static class ScoutSightCalculator
    {
        // 主流程
        public static float Compute(Pawn pawn, IncidentExtension_ScoutedRaid ext)
        {
            float baseR = ext.scoutSightRadius;
            float r = baseR;
            if (ext.weaponRangeExtendsSight)
            {
                float weaponR = TryGetWeaponRange(pawn);
                r = Mathf.Max(baseR, weaponR);
            }
            ApplyDefNameBonuses(pawn, ext, ref r);
            return Mathf.Max(0f, r);
        }

        // 主武器远战射程
        private static float TryGetWeaponRange(Pawn pawn)
        {
            var eq = pawn?.equipment?.Primary;
            if (eq?.def?.Verbs == null || eq.def.Verbs.Count == 0) return 0f;
            float best = 0f;
            for (int i = 0; i < eq.def.Verbs.Count; i++)
            {
                var v = eq.def.Verbs[i];
                if (v == null) continue;
                if (v.range > best) best = v.range;
            }
            return best;
        }

        // 装备/服装defname加成
        private static void ApplyDefNameBonuses(Pawn pawn, IncidentExtension_ScoutedRaid ext, ref float r)
        {
            float sumAdd = 0f;
            float prodMul = 1f;
            CollectFromEquipment(pawn, ext.weaponDefNameBonuses, ref sumAdd, ref prodMul);
            CollectFromApparel(pawn, ext.apparelDefNameBonuses, ref sumAdd, ref prodMul);
            r = (r + sumAdd) * prodMul;
        }

        // 装备主武器条目命中
        private static void CollectFromEquipment(Pawn pawn, List<DefNameSightBonus> table,
            ref float sumAdd, ref float prodMul)
        {
            if (table.NullOrEmpty()) return;
            var eq = pawn?.equipment?.Primary;
            if (eq?.def == null) return;
            for (int i = 0; i < table.Count; i++)
            {
                var entry = table[i];
                if (entry?.thing == null) continue;
                if (entry.thing != eq.def) continue;
                sumAdd += entry.add;
                prodMul *= entry.mul;
            }
        }

        // 服装条目命中
        private static void CollectFromApparel(Pawn pawn, List<DefNameSightBonus> table,
            ref float sumAdd, ref float prodMul)
        {
            if (table.NullOrEmpty()) return;
            var apparel = pawn?.apparel?.WornApparel;
            if (apparel == null || apparel.Count == 0) return;
            for (int i = 0; i < table.Count; i++)
            {
                var entry = table[i];
                if (entry?.thing == null) continue;
                if (!ApparelContains(apparel, entry.thing)) continue;
                sumAdd += entry.add;
                prodMul *= entry.mul;
            }
        }

        // 服装匹配
        private static bool ApparelContains(List<Apparel> worn, ThingDef def)
        {
            for (int i = 0; i < worn.Count; i++)
            {
                if (worn[i]?.def == def) return true;
            }
            return false;
        }
    }
}
