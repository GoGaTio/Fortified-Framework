using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Fortified
{
    // ════════════════════════════════════════════════════════════
    //  一、地下設施封鎖
    //  AlertEffect_FacilityLockdown
    // ════════════════════════════════════════════════════════════
    /// <summary>
    /// 警戒值滿後觸發地下設施封鎖程序：
    /// <list type="bullet">
    ///   <item>每 <see cref="alarmIntervalTicks"/>（預設 10 秒）播一次警報音 / 訊息。</item>
    ///   <item>倒數 <see cref="lockdownDelay"/>（預設 60 秒）後：
    ///     <list type="bullet">
    ///       <item>地圖 Despawn（PocketMap 回收）。</item>
    ///       <item>地面入口 <see cref="FacilityEntrance"/> 設為封閉（不可通行）。</item>
    ///       <item>仍在地圖內的玩家 Pawn 被判定為失蹤（移除）。</item>
    ///     </list>
    ///   </item>
    /// </list>
    /// </summary>
    public class AlertEffectWorker_FacilityLockdown : AlertEffectWorker
    {
        // ── 可配置 ──────────────────────────────────────────────
        /// <summary>警報週期（ticks），預設 10 秒 = 600。</summary>
        public int alarmIntervalTicks = 600;
        /// <summary>封鎖倒數（ticks），預設 60 秒 = 3600。</summary>
        public int lockdownDelay = 3600;

        // ── 狀態 ────────────────────────────────────────────────
        private int ticksElapsed = 0;
        private int nextAlarmTick = 0;
        private bool lockdownExecuted = false;
        private Map targetMap;

        public override void Execute(Map map)
        {
            if (map == null) return;
            targetMap = map;
            ticksElapsed = 0;
            nextAlarmTick = 0;
            lockdownExecuted = false;

            // 立即顯示首次警告
            Messages.Message("FFF_Alert_FacilityLockdown_Warning".Translate(), MessageTypeDefOf.ThreatBig);

            // 用 GameComponent 來驅動後續 Tick（避免 MapComponent 被 Despawn 一起移除前出問題）
            Find.World.GetComponent<WorldComponent_AlertLockdownDriver>()
                ?.RegisterLockdown(this);
        }

        /// <summary>每 tick 由 <see cref="WorldComponent_AlertLockdownDriver"/> 呼叫。</summary>
        public void Tick()
        {
            if (lockdownExecuted || targetMap == null) return;
            if (!Find.Maps.Contains(targetMap)) { lockdownExecuted = true; return; }

            ticksElapsed++;

            // 週期警報
            if (ticksElapsed >= nextAlarmTick)
            {
                nextAlarmTick = ticksElapsed + alarmIntervalTicks;
                int remaining = lockdownDelay - ticksElapsed;
                if (remaining > 0)
                {
                    Messages.Message(
                        "FFF_Alert_FacilityLockdown_Countdown".Translate(remaining.ToStringTicksToPeriod()),
                        MessageTypeDefOf.ThreatBig);
                    // 閃爍警報燈 Effecter 可擴充
                }
            }

            // 封鎖執行
            if (ticksElapsed >= lockdownDelay)
            {
                ExecuteLockdown();
            }
        }

        private void ExecuteLockdown()
        {
            if (lockdownExecuted) return;
            lockdownExecuted = true;

            if (targetMap == null || !Find.Maps.Contains(targetMap)) return;

            // 1. 判定仍在地圖內的玩家 Pawn → 失蹤
            List<Pawn> playerPawns = targetMap.mapPawns.PawnsInFaction(Faction.OfPlayer).ToList();
            foreach (Pawn pawn in playerPawns)
            {
                if (pawn.Spawned && pawn.Map == targetMap)
                {
                    // 移除 Pawn（判為失蹤）
                    pawn.SetFaction(null);
                    pawn.DeSpawn(DestroyMode.Vanish);
                    Find.LetterStack.ReceiveLetter(
                        "FFF_Alert_PawnMissing_Label".Translate(pawn.LabelShort),
                        "FFF_Alert_PawnMissing_Desc".Translate(pawn.LabelShort),
                        LetterDefOf.NegativeEvent);
                }
            }

            // 2. 關閉地面入口
            Map parentMap = targetMap.Parent?.Map;
            if (parentMap != null)
            {
                foreach (FacilityEntrance entrance in parentMap.listerThings.ThingsOfDef(
                    DefDatabase<ThingDef>.GetNamedSilentFail("FFF_FacilityEntrance"))
                    .OfType<FacilityEntrance>()
                    .Where(e => e.PocketMap == targetMap))
                {
                    entrance.SetFaction(null);   // 簡易封閉：移除控制派系
                    // 若有專屬的 CompCloseable 可在此呼叫 Close()
                }
            }

            // 3. Despawn 地圖（PocketMap 回收）
            if (targetMap.Parent is MapParent mp && mp.Spawned)
            {
                mp.Destroy();
            }

            Messages.Message("FFF_Alert_FacilityLockdown_Executed".Translate(), MessageTypeDefOf.ThreatBig);
        }

        public bool IsFinished => lockdownExecuted;
    }

    // ════════════════════════════════════════════════════════════
    //  WorldComponent_AlertLockdownDriver
    //  驅動 FacilityLockdown 的 WorldComponent
    // ════════════════════════════════════════════════════════════
    public class WorldComponent_AlertLockdownDriver : WorldComponent
    {
        private List<AlertEffectWorker_FacilityLockdown> active
            = new List<AlertEffectWorker_FacilityLockdown>();

        public WorldComponent_AlertLockdownDriver(World world) : base(world) { }

        public void RegisterLockdown(AlertEffectWorker_FacilityLockdown worker)
        {
            if (!active.Contains(worker))
                active.Add(worker);
        }

        public override void WorldComponentTick()
        {
            if (active.Count == 0) return;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                active[i].Tick();
                if (active[i].IsFinished)
                    active.RemoveAt(i);
            }
        }
    }

    // ════════════════════════════════════════════════════════════
    //  二、空域封鎖
    //  AlertEffect_AirspaceBlockade
    // ════════════════════════════════════════════════════════════
    /// <summary>
    /// 警戒值滿後觸發空域封鎖：
    /// <list type="bullet">
    ///   <item>每 <see cref="strikeIntervalTicks"/>（預設 30 秒）對露天目標派出空襲掃射。</item>
    ///   <item>封鎖期間禁用穿梭機起飛（透過 <see cref="Patch_DisableShuttleLaunch"/>）。</item>
    ///   <item>持續 <see cref="durationTicks"/>（預設 2 小時）後解除。</item>
    /// </list>
    /// </summary>
    public class AlertEffectWorker_AirspaceBlockade : AlertEffectWorker
    {
        // ── 可配置 ──────────────────────────────────────────────
        /// <summary>空襲週期（ticks），預設 30 秒 = 1800。</summary>
        public int strikeIntervalTicks = 1800;
        /// <summary>封鎖持續時間（ticks），預設 2 小時。</summary>
        public int durationTicks = GenDate.TicksPerHour * 2;
        /// <summary>空襲使用的 IncidentDef，建議指向空中掃射 Incident。</summary>
        public IncidentDef strikeIncident;
        /// <summary>使用的派系 Def（null 則使用最近敵對派系）。</summary>
        public FactionDef strikeFactionDef;

        // ── 狀態 ────────────────────────────────────────────────
        private bool active = false;
        private int endTick;
        private int nextStrikeTick;
        private Map targetMap;

        public bool IsAirspaceBlocked => active && targetMap != null && Find.Maps.Contains(targetMap);

        public override void Execute(Map map)
        {
            if (map == null) return;
            targetMap = map;
            active = true;
            endTick = Find.TickManager.TicksGame + durationTicks;
            nextStrikeTick = Find.TickManager.TicksGame + strikeIntervalTicks;

            Messages.Message("FFF_Alert_AirspaceBlockade_Start".Translate(), MessageTypeDefOf.ThreatBig);

            // 登記到 MapComponent 讓其驅動 Tick
            map.GetComponent<MapComponent_AlertCounter>()?
                .RegisterAirspaceBlockade(this);
        }

        /// <summary>由 MapComponent_AlertCounter 每 tick 呼叫。</summary>
        public void Tick()
        {
            if (!active) return;
            int now = Find.TickManager.TicksGame;

            if (now >= endTick)
            {
                active = false;
                Messages.Message("FFF_Alert_AirspaceBlockade_End".Translate(), MessageTypeDefOf.NeutralEvent);
                return;
            }

            if (now >= nextStrikeTick)
            {
                nextStrikeTick = now + strikeIntervalTicks;
                LaunchAirStrike();
            }
        }

        private void LaunchAirStrike()
        {
            if (targetMap == null || !Find.Maps.Contains(targetMap)) return;

            // 優先使用指定 Incident
            IncidentDef def = strikeIncident
                ?? DefDatabase<IncidentDef>.GetNamedSilentFail("FFF_AirStrikeStrafe");
            if (def == null) { Log.Warning("[FFF] AirspaceBlockade: no strikeIncident defined."); return; }

            Faction faction = null;
            if (strikeFactionDef != null)
                faction = Find.FactionManager.FirstFactionOfDef(strikeFactionDef);
            faction ??= Find.FactionManager.AllFactions
                .Where(f => f.HostileTo(Faction.OfPlayer) && !f.def.hidden)
                .RandomElementWithFallback();

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, targetMap);
            parms.target = targetMap;
            parms.faction = faction;
            parms.forced = true;

            if (def.Worker.CanFireNow(parms))
                def.Worker.TryExecute(parms);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  三、精確砲擊
    //  AlertEffect_PrecisionBombardment
    // ════════════════════════════════════════════════════════════
    /// <summary>
    /// 警戒值滿後觸發精確砲擊：
    /// 以 10~30 秒的隨機間隔轟炸地圖上的露天目標，
    /// 持續 <see cref="durationTicks"/>（預設 1 小時）後停止。
    /// </summary>
    public class AlertEffectWorker_PrecisionBombardment : AlertEffectWorker
    {
        // ── 可配置 ──────────────────────────────────────────────
        /// <summary>最短砲擊間隔（ticks），預設 600（10 秒）。</summary>
        public int minIntervalTicks = 600;
        /// <summary>最長砲擊間隔（ticks），預設 1800（30 秒）。</summary>
        public int maxIntervalTicks = 1800;
        /// <summary>持續時間（ticks），預設 1 小時。</summary>
        public int durationTicks = GenDate.TicksPerHour;
        /// <summary>砲擊使用的彈藥 ThingDef（預設使用遊戲內自帶砲彈）。</summary>
        public ThingDef shellDef;
        /// <summary>每次砲擊的齊射數量。</summary>
        public int shotsPerSalvo = 3;

        // ── 狀態 ────────────────────────────────────────────────
        private bool active = false;
        private int endTick;
        private int nextStrikeTick;
        private Map targetMap;

        public bool IsActive => active;

        public override void Execute(Map map)
        {
            if (map == null) return;
            targetMap = map;
            active = true;
            endTick = Find.TickManager.TicksGame + durationTicks;
            ScheduleNextStrike();

            Messages.Message("FFF_Alert_PrecisionBombardment_Start".Translate(), MessageTypeDefOf.ThreatBig);

            map.GetComponent<MapComponent_AlertCounter>()?
                .RegisterBombardment(this);
        }

        /// <summary>由 MapComponent_AlertCounter 每 tick 呼叫。</summary>
        public void Tick()
        {
            if (!active) return;
            int now = Find.TickManager.TicksGame;

            if (now >= endTick)
            {
                active = false;
                Messages.Message("FFF_Alert_PrecisionBombardment_End".Translate(), MessageTypeDefOf.NeutralEvent);
                return;
            }

            if (now >= nextStrikeTick)
            {
                ScheduleNextStrike();
                FireSalvo();
            }
        }

        private void ScheduleNextStrike()
        {
            nextStrikeTick = Find.TickManager.TicksGame
                + Rand.Range(minIntervalTicks, maxIntervalTicks);
        }

        private void FireSalvo()
        {
            if (targetMap == null || !Find.Maps.Contains(targetMap)) return;

            // 選取露天的敵方或玩家高價值目標
            List<Thing> candidates = targetMap.listerThings.AllThings
                .Where(t => t != null
                    && t.Spawned
                    && !t.Position.Roofed(targetMap)
                    && (t is Pawn p && p.Faction == Faction.OfPlayer
                        || t is Building b && b.Faction == Faction.OfPlayer))
                .ToList();

            if (candidates.Count == 0) return;

            ThingDef shell = shellDef
                ?? ThingDefOf.Shell_HighExplosive;

            for (int i = 0; i < shotsPerSalvo; i++)
            {
                Thing target = candidates.RandomElement();
                IntVec3 pos = target.Position;
                // 加入少量誤差，避免完全精確
                pos += new IntVec3(Rand.RangeInclusive(-2, 2), 0, Rand.RangeInclusive(-2, 2));
                if (!pos.InBounds(targetMap)) pos = target.Position;

                // 生成砲彈（直接在目標格爆炸）
                Projectile proj = (Projectile)GenSpawn.Spawn(
                    shell, pos, targetMap, WipeMode.Vanish);
                proj?.Launch(null, pos.ToVector3(), target, target,
                    ProjectileHitFlags.All, false, null, null);
            }
        }
    }

    // ════════════════════════════════════════════════════════════
    //  MapComponent_AlertCounter 擴充部分
    //  （透過 partial class 將持續效果 Tick 整合進 MapComponent）
    // ════════════════════════════════════════════════════════════
    public partial class MapComponent_AlertCounter
    {
        // ── 持續效果 Tick 支援 ───────────────────────────────────
        private AlertEffectWorker_AirspaceBlockade activeAirspaceBlockade;
        private AlertEffectWorker_PrecisionBombardment activeBombardment;

        public void RegisterAirspaceBlockade(AlertEffectWorker_AirspaceBlockade worker)
            => activeAirspaceBlockade = worker;

        public void RegisterBombardment(AlertEffectWorker_PrecisionBombardment worker)
            => activeBombardment = worker;

        /// <summary>是否正在執行空域封鎖（供 Shuttle 起飛 Patch 查詢）。</summary>
        public bool IsAirspaceBlocked
            => activeAirspaceBlockade?.IsAirspaceBlocked ?? false;

        // 在 MapComponentTick 追加 Tick（透過 partial 擴充）
        // 注意：partial 方法需在同檔案，這裡改用 hook 方式
        public void TickActiveEffects()
        {
            activeAirspaceBlockade?.Tick();
            activeBombardment?.Tick();
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Patch：禁用穿梭機起飛（空域封鎖）
    // ════════════════════════════════════════════════════════════
    /// <summary>
    /// 空域封鎖期間禁止穿梭機起飛。
    /// 不使用 [HarmonyPatch] 自動掃描（HarmonyTargetMethod 回傳 null 會讓整個
    /// PatchAll 崩潰）；改由 HarmonyEntry 顯式呼叫 TryApply，方法不存在時靜默跳過。
    /// </summary>
    public static class Patch_DisableShuttleLaunch
    {
        /// <summary>
        /// 由 HarmonyEntry 呼叫。找到目標方法才套用 postfix，否則靜默略過。
        /// </summary>
        public static void TryApply(HarmonyLib.Harmony harmony)
        {
            System.Reflection.MethodBase target = FindTarget();
            if (target == null)
            {
                Log.Warning("[FFF] Patch_DisableShuttleLaunch: could not find patch target on CompShuttle. Airspace blockade shuttle prevention is disabled.");
                return;
            }

            var postfix = new HarmonyLib.HarmonyMethod(
                typeof(Patch_DisableShuttleLaunch),
                nameof(Postfix));
            harmony.Patch(target, postfix: postfix);
        }

        // 以反射動態定位目標方法，支援多版本 RimWorld
        private static System.Reflection.MethodBase FindTarget()
        {
            const System.Reflection.BindingFlags bf =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;

            // 嘗試 get_IsBlocked（屬性 getter）
            var getter = typeof(CompShuttle).GetProperty("IsBlocked", bf)?.GetGetMethod();
            if (getter != null) return getter;

            // 退而求其次：攔截 LoadingInProgressOrReadyToLaunch getter
            getter = typeof(CompShuttle).GetProperty("LoadingInProgressOrReadyToLaunch", bf)?.GetGetMethod();
            return getter; // null 亦可，由呼叫端處理
        }

        static void Postfix(CompShuttle __instance, ref bool __result)
        {
            if (__result) return;
            Map map = __instance.parent?.Map;
            if (map == null) return;
            if (map.GetComponent<MapComponent_AlertCounter>()?.IsAirspaceBlocked == true)
                __result = true;
        }
    }
}
