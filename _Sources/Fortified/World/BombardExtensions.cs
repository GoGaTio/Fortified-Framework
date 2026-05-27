using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Fortified
{
    // 轰炸目标优先类型
    public enum BombardTargetType
    {
        Auto,              // 自动追随敌军聚集区
        TurretLine,        // 炮塔防线
        PowerGrid,         // 发电机和蓄电池
        HighValueStorage,  // 高价值储仓区域
        Colonists,         // 殖民者本人
        MechConcentration, // 玩家殖民地机械体聚集区
    }

    // 弹药选择模式
    public enum ShellSelectionMode
    {
        // 按权重概率随机选择弹药
        // 弹药权重分配规则
        Weighted,
        // 按固定顺序循环发射弹药
        // 顺序循环逻辑描述
        Sequential,
    }

    // 弹药权重条目定义
    public class WeightedShellEntry
    {
        public ThingDef projectileDef;
        // 记录弹药模式对应配置值
        public float weight = 1f;
    }

    // 空袭定义权重条目
    public class WeightedAirSupportEntry
    {
        public AirSupportDef airSupportDef;
        public float weight = 1f;
    }

    // 袭击战术权重条目
    public class WeightedRaidStrategyEntry
    {
        public RaidStrategyDef raidStrategyDef;
        public float weight = 1f;
    }

    // 到达模式权重条目
    public class WeightedArrivalModeEntry
    {
        public PawnsArrivalModeDef arrivalModeDef;
        public float weight = 1f;
    }

    // 派系权重条目
    public class WeightedFactionEntry
    {
        public FactionDef factionDef;
        public float weight = 1f;
    }

    // PawnGroupKind权重条目
    public class WeightedPawnGroupKindEntry
    {
        public PawnGroupKindDef pawnGroupKindDef;
        public float weight = 1f;
    }

    // 标记屋顶分级
    public enum MarkRoofTier
    {
        // 仅露天目标
        NoRoofOnly = 1,
        // 露天或建筑屋顶
        AllowConstructed = 2,
        // 露天 建筑屋顶 薄岩顶
        AllowThinRock = 3,
        // 任意屋顶
        AllowAny = 4,
    }

    // 侦察方式
    public enum ScoutVariant
    {
        // 地面侦察小队
        GroundParty,
        // 空中侦察机
        AerialDrone,
    }

    // 屋顶分级判定
    public static class RoofTierUtility
    {
        // 单元格是否通过分级
        public static bool CellPasses(Map map, IntVec3 cell, MarkRoofTier tier)
        {
            if (map == null || !cell.InBounds(map)) return false;
            var roof = map.roofGrid?.RoofAt(cell);
            // 露天直接通过
            if (roof == null) return true;
            switch (tier)
            {
                case MarkRoofTier.NoRoofOnly:
                    return false;
                case MarkRoofTier.AllowConstructed:
                    return !roof.isNatural;
                case MarkRoofTier.AllowThinRock:
                    return !roof.isNatural || !roof.isThickRoof;
                case MarkRoofTier.AllowAny:
                    return true;
                default:
                    return true;
            }
        }
    }

    // 列表与单值统一抽取
    public static class WeightedPickUtility
    {
        // 列表优先 单值兜底
        public static RaidStrategyDef PickStrategy(List<WeightedRaidStrategyEntry> pool, RaidStrategyDef fallback)
        {
            if (pool != null)
            {
                var entry = pool.Where(e => e?.raidStrategyDef != null && e.weight > 0f).RandomElementByWeightWithFallback(e => e.weight);
                if (entry != null) return entry.raidStrategyDef;
            }
            return fallback;
        }

        public static PawnsArrivalModeDef PickArrivalMode(List<WeightedArrivalModeEntry> pool, PawnsArrivalModeDef fallback)
        {
            if (pool != null)
            {
                var entry = pool.Where(e => e?.arrivalModeDef != null && e.weight > 0f).RandomElementByWeightWithFallback(e => e.weight);
                if (entry != null) return entry.arrivalModeDef;
            }
            return fallback;
        }

        public static FactionDef PickFactionDef(List<WeightedFactionEntry> pool, FactionDef fallback)
        {
            if (pool != null)
            {
                var entry = pool.Where(e => e?.factionDef != null && e.weight > 0f).RandomElementByWeightWithFallback(e => e.weight);
                if (entry != null) return entry.factionDef;
            }
            return fallback;
        }

        public static PawnGroupKindDef PickPawnGroupKind(List<WeightedPawnGroupKindEntry> pool, PawnGroupKindDef fallback)
        {
            if (pool != null)
            {
                var entry = pool.Where(e => e?.pawnGroupKindDef != null && e.weight > 0f).RandomElementByWeightWithFallback(e => e.weight);
                if (entry != null) return entry.pawnGroupKindDef;
            }
            return fallback;
        }

        // 派系下解析可用的PawnGroupKindDef preferred缺失则按池剩余项补 再退Combat 最后退派系任意可用kind
        public static PawnGroupKindDef ResolvePawnGroupKindForFaction(Faction faction, PawnGroupKindDef preferred, List<WeightedPawnGroupKindEntry> pool = null)
        {
            var makers = faction?.def?.pawnGroupMakers;
            if (preferred != null && HasGroupMaker(makers, preferred)) return preferred;
            // 池内剩余可用项随机
            if (pool != null && makers != null)
            {
                var avail = pool
                    .Where(e => e?.pawnGroupKindDef != null && e.pawnGroupKindDef != preferred && e.weight > 0f && HasGroupMaker(makers, e.pawnGroupKindDef))
                    .ToList();
                if (avail.Count > 0)
                {
                    var pick = avail.RandomElementByWeightWithFallback(e => e.weight);
                    if (pick != null) return pick.pawnGroupKindDef;
                }
            }
            // 退Combat
            if (HasGroupMaker(makers, PawnGroupKindDefOf.Combat)) return PawnGroupKindDefOf.Combat;
            // 派系实际拥有的任意kind
            if (makers != null && makers.Count > 0)
            {
                for (int i = 0; i < makers.Count; i++)
                {
                    if (makers[i]?.kindDef != null) return makers[i].kindDef;
                }
            }
            return PawnGroupKindDefOf.Combat;
        }

        private static bool HasGroupMaker(List<PawnGroupMaker> makers, PawnGroupKindDef kind)
        {
            if (makers == null || kind == null) return false;
            for (int i = 0; i < makers.Count; i++)
            {
                if (makers[i]?.kindDef == kind) return true;
            }
            return false;
        }
    }
}
