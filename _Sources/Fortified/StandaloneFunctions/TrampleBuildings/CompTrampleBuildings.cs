using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Fortified;

// 半径内敌对可通过建筑扣血 可选碾压物品
public class CompTrampleBuildings : ThingComp
{
    // 复用列表 减少分配
    private static readonly List<Thing> tmpTargets = new List<Thing>();

    // 全局去重 同物每tick只处理一次 防跨机兵竞态
    private static int processedTick = -1;
    private static readonly HashSet<Thing> processedThings = new HashSet<Thing>();

    // 缓存格数 半径运行期不变
    private int cachedNumCells = -1;

    public CompProperties_TrampleBuildings Props => (CompProperties_TrampleBuildings)props;

    private DamageDef DamageDef => Props.damageDef ?? DamageDefOf.Blunt;

    // 当前生效半径
    private float CurrentRadius
    {
        get
        {
            if (Props.radiusMode == TrampleRadiusMode.BodySize && parent is Pawn pawn)
                return pawn.BodySize * 0.5f * Props.bodySizeFactor;
            return Props.fixedRadius;
        }
    }

    // 移动跨入新格时由Harmony补丁调用
    public void Notify_EnteredCell(IntVec3 moveDir)
    {
        if (!parent.Spawned) return;
        TrampleOnce(moveDir);
    }

    // 扫描半径内目标并处理
    private void TrampleOnce(IntVec3 moveDir)
    {
        Map map = parent.Map;
        if (map == null) return;

        float radius = CurrentRadius;
        if (radius <= 0f) return;

        // 跨tick重置去重集
        int now = GenTicks.TicksGame;
        if (processedTick != now)
        {
            processedTick = now;
            processedThings.Clear();
        }

        CollectTargets(map, radius);
        for (int i = 0; i < tmpTargets.Count; i++)
        {
            Thing t = tmpTargets[i];
            // 本tick已被其他机兵处理则跳过
            if (!processedThings.Add(t)) continue;
            ProcessTarget(t, moveDir);
        }
        tmpTargets.Clear();
    }

    // 收集范围内有效目标
    private void CollectTargets(Map map, float radius)
    {
        tmpTargets.Clear();
        IntVec3 center = parent.Position;
        // 直接索引RadialPattern避免迭代器分配
        if (cachedNumCells < 0)
            cachedNumCells = GenRadial.NumCellsInRadius(radius);
        int numCells = cachedNumCells;
        IntVec3[] pattern = GenRadial.RadialPattern;
        for (int n = 0; n < numCells; n++)
        {
            IntVec3 cell = pattern[n] + center;
            if (!cell.InBounds(map)) continue;
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing t = things[i];
                if (IsValidTarget(t)) tmpTargets.Add(t);
            }
        }
    }

    // 敌对可通过建筑 或挡路有耐久物品
    private bool IsValidTarget(Thing t)
    {
        if (t == parent || t.Destroyed) return false;
        if (t.def.category == ThingCategory.Building)
            return t.def.passability == Traversability.PassThroughOnly && t.HostileTo(parent);
        // 物品无阵营 碾压或推开开关任一开启即纳入
        if ((Props.crushItems || Props.pushItems) && t.def.category == ThingCategory.Item)
            return t.def.useHitPoints;
        return false;
    }

    // 按类型处理目标
    private void ProcessTarget(Thing t, IntVec3 moveDir)
    {
        // 物品优先沿移动方向推开 推不动再按碾压开关处理
        if (t.def.category == ThingCategory.Item)
        {
            if (Props.pushItems && TryPushItem(t, moveDir)) return;
            if (Props.crushItems) DamageTarget(t);
            return;
        }
        DamageTarget(t);
    }

    // 轻量推动 直接移格不重建 推不动就放弃
    private bool TryPushItem(Thing t, IntVec3 moveDir)
    {
        if (moveDir == IntVec3.Zero) return false;
        // 守卫已被处理或销毁的物
        if (t.Destroyed || !t.Spawned) return false;
        Map map = parent.Map;
        IntVec3 dest = t.Position + moveDir;
        // 只顶前方和脚下 身后已通过不再拉回
        if (dest.DistanceToSquared(parent.Position) <= t.Position.DistanceToSquared(parent.Position))
            return false;
        if (!dest.InBounds(map) || dest.Impassable(map)) return false;
        // 目标格物品已满且无可堆叠同类物则放弃
        if (dest.GetItemCount(map) >= dest.GetMaxItemsAllowedInCell(map)
            && !CanStackAt(t, dest, map))
            return false;
        // 直接改位置 setter自动重注册thingGrid 无DeSpawn重建
        t.Position = dest;
        return true;
    }

    // 目标格是否有可堆叠同类物
    private static bool CanStackAt(Thing t, IntVec3 dest, Map map)
    {
        List<Thing> things = dest.GetThingList(map);
        for (int i = 0; i < things.Count; i++)
            if (things[i].CanStackWith(t)) return true;
        return false;
    }

    // 对单个目标造成伤害
    private void DamageTarget(Thing t)
    {
        if (t.Destroyed) return;
        DamageInfo dinfo = new DamageInfo(
            DamageDef, Props.damageAmount, Props.armorPenetration, -1f, parent);
        t.TakeDamage(dinfo);
    }
}
