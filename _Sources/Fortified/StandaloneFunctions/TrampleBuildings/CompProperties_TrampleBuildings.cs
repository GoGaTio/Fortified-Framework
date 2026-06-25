using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Fortified;

// 半径来源方式
public enum TrampleRadiusMode
{
    Fixed,      // 固定半径
    BodySize    // 体型作为直径
}

public class CompProperties_TrampleBuildings : CompProperties
{
    // 半径来源方式
    public TrampleRadiusMode radiusMode = TrampleRadiusMode.Fixed;

    // 固定半径格数
    public float fixedRadius = 1.9f;

    // 体型直径系数
    public float bodySizeFactor = 1f;

    // 伤害类型 默认钝击
    public DamageDef damageDef;

    // 每次伤害量
    public float damageAmount = 50f;

    // 护甲穿透
    public float armorPenetration = 1f;

    // 碾压挡路物品耐久
    public bool crushItems = false;

    // 沿移动方向推开挡路物品
    public bool pushItems = false;

    // 命中特效
    public EffecterDef hitEffecter;

    public CompProperties_TrampleBuildings()
    {
        compClass = typeof(CompTrampleBuildings);
    }

    public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
    {
        foreach (string e in base.ConfigErrors(parentDef)) yield return e;

        if (radiusMode == TrampleRadiusMode.Fixed && fixedRadius <= 0f)
            yield return $"{nameof(CompProperties_TrampleBuildings)}: fixedRadius 须大于0";
        if (radiusMode == TrampleRadiusMode.BodySize && bodySizeFactor <= 0f)
            yield return $"{nameof(CompProperties_TrampleBuildings)}: bodySizeFactor 须大于0";
        if (damageAmount <= 0f)
            yield return $"{nameof(CompProperties_TrampleBuildings)}: damageAmount 须大于0";
    }
}
