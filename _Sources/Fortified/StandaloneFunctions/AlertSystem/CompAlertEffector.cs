using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Fortified
{
    // ════════════════════════════════════════════════════════════
    //  CompProperties_AlertEffector  (基類 Props)
    // ════════════════════════════════════════════════════════════
    /// <summary>
    /// 所有警報效果 Comp 的 CompProperties 基類。
    /// </summary>
    public class CompProperties_AlertEffector : CompProperties
    {
        /// <summary>監聽的 Signal 字串，需與 CompAlertScanner.signalString 一致。</summary>
        public string listenSignal = "FFF_AlertScanner_Triggered";

        /// <summary>每次收到警報時實際觸發的機率（0~1）。</summary>
        public float triggerChance = 1f;

        /// <summary>是否只觸發一次（true = 一次性，false = 可重複觸發）。</summary>
        public bool oneShot = false;

        public CompProperties_AlertEffector()
        {
            compClass = typeof(CompAlertEffector);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  CompAlertEffector  (基類)
    // ════════════════════════════════════════════════════════════
    /// <summary>
    /// 警報效果基類。
    /// 接收 MapComponent_AlertCounter 的警報通知（透過 Signal 或直接呼叫 <see cref="OnAlertNotify"/>）。
    /// 子類覆寫 <see cref="DoEffect"/> 實作具體效果。
    /// </summary>
    public class CompAlertEffector : ThingComp
    {
        private bool hasFired = false;

        /// <summary>
        /// 上次實際觸發 DoEffect() 的 tick。
        /// 用於去重：同一 tick 內無論收到幾個 Signal 都只觸發一次。
        /// （多個 CompAlertScanner 同 tick 偵測到威脅時會各自 SendSignal，
        ///   此欄位確保同一 Effector 建築不會在同 tick 重複釋放。）
        /// 不需持久化——讀檔後 tick 必然不同，不影響正確性。
        /// </summary>
        private int lastFiredTick = -1;

        public CompProperties_AlertEffector Props => (CompProperties_AlertEffector)props;

        // ── Signal 接收 ──────────────────────────────────────────
        public override void Notify_SignalReceived(Signal signal)
        {
            base.Notify_SignalReceived(signal);
            if (signal.tag == Props.listenSignal)
                OnAlertNotify();
        }

        /// <summary>由外部（MapComponent_AlertCounter 或 Signal）呼叫通知警報。</summary>
        public void OnAlertNotify()
        {
            if (Props.oneShot && hasFired) return;

            // 同 tick 去重：防止多個 Scanner 同 tick 廣播 Signal 導致重複觸發
            int now = Find.TickManager.TicksGame;
            if (lastFiredTick == now) return;

            if (!Rand.Chance(Props.triggerChance)) return;

            lastFiredTick = now;
            hasFired = true;
            DoEffect();
        }

        /// <summary>子類實作具體效果邏輯。</summary>
        protected virtual void DoEffect() { }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref hasFired, "hasFired", false);
        }
    }
}
