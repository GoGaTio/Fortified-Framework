using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Fortified
{
    // 侦察袭击配置
    public class IncidentExtension_ScoutedRaid : DefModExtension
    {
        // 侦察炮击循环次数
        public int cycleCount = 2;

        // 单轮侦察战力比
        public float scoutPointsFactor = 0.35f;
        // 主袭击战力比
        public float mainPointsFactor = 1.0f;
        // 战力下限
        public float scoutMinPoints = 80f;
        // 炮击末段提前触发下一轮
        public int earlyScoutLeadTicks = 300;

        // 侦察派系覆写
        public FactionDef scoutFactionDef;
        // 主袭击派系覆写
        public FactionDef mainFactionDef;
        // 侦察派系池优先级高于scoutFactionDef
        public List<WeightedFactionEntry> scoutFactionPool;
        // 主袭击派系池优先级高于mainFactionDef
        public List<WeightedFactionEntry> mainFactionPool;

        // 侦察到达模式
        public PawnsArrivalModeDef scoutArriveMode;
        // 主袭击到达模式
        public PawnsArrivalModeDef mainArriveMode;
        // 主袭击战术
        public RaidStrategyDef mainStrategyDef;
        // 侦察到达模式池优先级高于scoutArriveMode
        public List<WeightedArrivalModeEntry> scoutArriveModePool;
        // 主袭击到达模式池优先级高于mainArriveMode
        public List<WeightedArrivalModeEntry> mainArriveModePool;
        // 主袭击战术池优先级高于mainStrategyDef
        public List<WeightedRaidStrategyEntry> mainStrategyPool;

        // 侦察组类型
        public PawnGroupKindDef scoutGroupKind;
        // 侦察组类型池优先级高于scoutGroupKind
        public List<WeightedPawnGroupKindEntry> scoutGroupKindPool;

        // 侦察方式
        public ScoutVariant scoutVariant = ScoutVariant.GroundParty;
        // 侦察小队屋顶分级
        public MarkRoofTier scoutMarkRoofTier = MarkRoofTier.AllowThinRock;
        // 侦察机屋顶分级
        public MarkRoofTier droneMarkRoofTier = MarkRoofTier.NoRoofOnly;
        // 侦察机ThingDef
        public ThingDef droneFlyByThingDef;
        // 扫描带半宽
        public float droneScanHalfWidth = 8f;
        // 扫描带半长
        public float droneScanHalfLength = 6f;
        // 扫描间隔
        public int droneScanIntervalTicks = 12;

        // 侦察撤离触发条件
        public ScoutWithdrawCondition withdrawCondition = ScoutWithdrawCondition.Either;
        // 撤离超时上限
        public int scoutWithdrawAfterTicks = 8000;
        // 损失比例触发撤离
        public float scoutLossWithdrawFraction = 0.5f;
        // 标记容量耗尽时撤离
        public bool withdrawWhenMarksExhausted = false;
        // 侦察兵绕行炮塔与敌人威胁区
        public bool scoutAvoidEnemyRanges = false;
        // 空手侦察兵死亡时按最后位置就近补一发玩家建筑标记
        public bool deadScoutMarksFallback = false;

        // 撤退后到炮击延迟
        public IntRange bombardmentDelayTicks = new IntRange(180, 360);
        // 炮击间隔
        public IntRange interCycleDelayTicks = new IntRange(2500, 4500);
        // 炮击到主袭击延迟
        public IntRange preMainRaidDelayTicks = new IntRange(900, 1800);

        // 照明弹弹种
        public ThingDef flareProjectileDef;
        // 照明弹颜色
        public Color flareColor = new Color(1f, 0.85f, 0.4f, 1f);
        // 照明弹点亮时长
        public int flareIgniteDurationTicks = 900;
        // 照明弹悬浮高度
        public float flareIgniteHoverHeight = 4.5f;
        // 照明弹下坠起始高度
        public float flareDescendStartHeight = 17f;
        // 照明弹下坠时长
        public int flareDescendDurationTicks = 90;
        // 入射方向锥角度
        public float flareConeAngleDegrees = 25f;
        // 入射origin距离范围
        public FloatRange originDistanceRange = new FloatRange(20f, 30f);

        // 视野半径
        public float scoutSightRadius = 21f;
        // 武器射程参与视野开启时取max(scoutSightRadius,武器射程)
        public bool weaponRangeExtendsSight = false;
        // 武器defname额外视野
        public List<DefNameSightBonus> weaponDefNameBonuses;
        // 服装defname额外视野
        public List<DefNameSightBonus> apparelDefNameBonuses;
        // 玩家pawn最近开火时窗
        public int recentFireWindowTicks = 600;
        // 邻域统计半径
        public float emplacementCheckRadius = 5f;

        // 单标记基础权重
        public float markBaseWeight = 2f;
        // 邻域内单玩家建筑追加权重
        public float nearbyBuildingWeight = 0.5f;

        // 重型合成半径
        public float markFusionRadius = 5f;
        // 单重型消耗mark数
        public int marksPerHeavyFlare = 3;
        // 重型间最小间距
        public float heavyFlareMinSeparation = 6f;
        // 重型炮击弹道散布倍率
        public float heavySpreadMultiplier = 2.5f;
        // 重型标记弹弹种
        public ThingDef heavyFlareProjectileDef;
        // 重型标记弹颜色
        public Color heavyFlareColor = new Color(1f, 0.2f, 0.2f, 1f);
        // 重型下坠起始高度
        public float heavyFlareDescendStartHeight = 25f;
        // 重型尺寸倍率
        public float heavyFlareScale = 2f;
        // 重型缺省时投弹倍率
        public float heavyFlareProjectileMultiplier = 3f;
        // 重型炮击专用弹种池
        public List<WeightedShellEntry> heavyBombardmentShellPool;

        // 炮击弹药池
        public List<WeightedShellEntry> bombardmentShellPool;
        // 弹种选取模式
        public ShellSelectionMode shellSelectionMode = ShellSelectionMode.Weighted;
        // 缺省弹药
        public ThingDef fallbackShellDef;

        // 单标记弹数范围
        public IntRange projectilesPerMark = new IntRange(2, 3);
        // 子波次
        public int wavesPerStrike = 1;
        // 波次间隔
        public int waveIntervalTicks = 45;
        // 弹间隔
        public int projectileIntervalTicks = 8;
        // 首发延迟
        public IntRange firstStrikeDelayTicks = new IntRange(150, 240);
        // 弹道散布半径
        public float spreadRadius = 4.5f;
        // 弹道生成高度
        public float projectileOriginHeight = 120f;

        // 单pawn持有标记上限
        public int maxMarksPerPawn = 1;
        // 全局标记总上限
        public int maxTotalMarks = 0;
        // 邻域计数缓存有效期
        public int nearbyCacheTicks = 600;
        // 标记最大保留时间
        public int maxMarkAgeTicks = 4500;
        // 邻近合并半径
        public float markMergeRadius = 6f;
        // 单次炮击簇上限
        public int maxImpactCellsPerStrike = 8;

        // 信件标签
        public string scoutLetterLabel;
        public string scoutLetterText;
        public string bombardLetterLabel;
        public string bombardLetterText;
        public string mainRaidLetterLabel;
        public string mainRaidLetterText;
    }

    // 侦察撤离触发条件
    public enum ScoutWithdrawCondition
    {
        TimeoutOnly,
        AllDeadOrLostFraction,
        Either,
    }
}
