using UnityEngine;
using Verse;

namespace Fortified
{
    // 侦察袭击状态机
    public static class ScoutedRaidStateMachine
    {
        public static void Tick(ScoutedRaidJob job)
        {
            if (job == null) return;
            int now = Find.TickManager.TicksGame;
            switch (job.phase)
            {
                case ScoutedRaidPhase.ScoutInbound: TickScoutInbound(job, now); break;
                case ScoutedRaidPhase.ScoutActive: TickScoutActive(job, now); break;
                case ScoutedRaidPhase.BombardmentDelay: TickBombardmentDelay(job, now); break;
                case ScoutedRaidPhase.Bombardment: TickBombardment(job, now); break;
                case ScoutedRaidPhase.InterCycleWait: TickInterCycleWait(job, now); break;
                case ScoutedRaidPhase.PreMainRaidWait: TickPreMainWait(job, now); break;
                case ScoutedRaidPhase.MainRaidIssued: job.phase = ScoutedRaidPhase.Done; break;
            }
        }

        // 侦察生成
        private static void TickScoutInbound(ScoutedRaidJob job, int now)
        {
            if (now < job.phaseEndTick) return;
            var ext = job.Ext;
            if (ext == null || job.map == null) { job.phase = ScoutedRaidPhase.Done; return; }
            if (ext.scoutVariant == ScoutVariant.AerialDrone)
            {
                ScoutDroneProcedures.SpawnScoutDrone(job, ext);
            }
            else
            {
                ScoutGroundProcedures.SpawnScoutWave(job, ext);
            }
            job.phase = ScoutedRaidPhase.ScoutActive;
            job.phaseEndTick = now + ext.scoutWithdrawAfterTicks;
        }

        // 侦察活动
        private static void TickScoutActive(ScoutedRaidJob job, int now)
        {
            var ext = job.Ext;
            if (ext == null) { job.phase = ScoutedRaidPhase.Done; return; }
            if (ext.scoutVariant == ScoutVariant.AerialDrone)
            {
                TickDroneActive(job, ext, now);
                return;
            }
            TickGroundActive(job, ext, now);
        }

        // 侦察机分支
        private static void TickDroneActive(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext, int now)
        {
            bool alive = ScoutDroneProcedures.TickDroneScan(job, ext, now);
            bool timeout = (ext.withdrawCondition != ScoutWithdrawCondition.AllDeadOrLostFraction)
                            && now >= job.phaseEndTick;
            bool exhausted = ext.withdrawWhenMarksExhausted
                              && ext.maxTotalMarks > 0
                              && job.marks.Count >= ext.maxTotalMarks;
            if (!alive || timeout || exhausted)
            {
                ScoutDroneProcedures.TerminateDrone(job);
                job.phase = ScoutedRaidPhase.BombardmentDelay;
                job.phaseEndTick = now + ext.bombardmentDelayTicks.RandomInRange;
            }
        }

        // 地面侦察分支
        private static void TickGroundActive(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext, int now)
        {
            // 每60tick采样一次
            if (now % 60 == 0) ScoutGroundProcedures.HarvestMarks(job, ext);
            bool timeout = (ext.withdrawCondition != ScoutWithdrawCondition.AllDeadOrLostFraction)
                            && now >= job.phaseEndTick;
            bool lossTrigger = (ext.withdrawCondition != ScoutWithdrawCondition.TimeoutOnly)
                                && ScoutGroundProcedures.ShouldTriggerLossWithdraw(job, ext);
            bool exhausted = ScoutGroundProcedures.ShouldTriggerMarksExhaustedWithdraw(job, ext);
            if (!timeout && !lossTrigger && !exhausted) return;
            ScoutGroundProcedures.OrderScoutsExit(job);
            job.phase = ScoutedRaidPhase.BombardmentDelay;
            job.phaseEndTick = now + ext.bombardmentDelayTicks.RandomInRange;
        }

        // 炮击前延迟
        private static void TickBombardmentDelay(ScoutedRaidJob job, int now)
        {
            if (now < job.phaseEndTick) return;
            var ext = job.Ext;
            if (ext == null) { job.phase = ScoutedRaidPhase.Done; return; }
            BombardmentDispatcher.LaunchFlares(job, ext);
            BombardmentDispatcher.ScheduleBombardment(job, ext);
            job.phase = ScoutedRaidPhase.Bombardment;
            job.phaseEndTick = now + EstimateBombardmentDuration(job, ext);
        }

        // 估算炮击全程时长重型按倍率展开
        private static int EstimateBombardmentDuration(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext)
        {
            int totalShots = 0;
            if (job.cachedFlareSlots != null)
            {
                int perCellMax = Mathf.Max(1, ext.projectilesPerMark.max);
                bool hasHeavyPool = !ext.heavyBombardmentShellPool.NullOrEmpty();
                float heavyMul = Mathf.Max(1f, ext.heavyFlareProjectileMultiplier);
                for (int i = 0; i < job.cachedFlareSlots.Count; i++)
                {
                    var slot = job.cachedFlareSlots[i];
                    float mul = (slot.heavy && !hasHeavyPool) ? heavyMul : 1f;
                    totalShots += Mathf.RoundToInt(perCellMax * mul);
                }
            }
            if (totalShots <= 0) totalShots = 1;
            return ext.firstStrikeDelayTicks.max
                   + (ext.wavesPerStrike - 1) * ext.waveIntervalTicks
                   + totalShots * ext.projectileIntervalTicks
                   + 600;
        }

        // 炮击进行中末段提前推进
        private static void TickBombardment(ScoutedRaidJob job, int now)
        {
            var ext = job.Ext;
            if (ext == null) { job.phase = ScoutedRaidPhase.Done; return; }
            int lead = Mathf.Max(0, ext.earlyScoutLeadTicks);
            if (now < job.phaseEndTick - lead) return;
            job.currentCycle++;
            if (job.currentCycle < ext.cycleCount)
            {
                job.phase = ScoutedRaidPhase.InterCycleWait;
                job.phaseEndTick = now + ext.interCycleDelayTicks.RandomInRange;
                return;
            }
            // 主袭击前等待
            job.phase = ScoutedRaidPhase.PreMainRaidWait;
            job.phaseEndTick = now + ext.preMainRaidDelayTicks.RandomInRange;
        }

        private static void TickInterCycleWait(ScoutedRaidJob job, int now)
        {
            if (now < job.phaseEndTick) return;
            var ext = job.Ext;
            if (ext == null) { job.phase = ScoutedRaidPhase.Done; return; }
            // 进入下一轮侦察
            job.phase = ScoutedRaidPhase.ScoutInbound;
            job.phaseEndTick = now;
            job.ResetForNewCycle();
            // 清侦察机残留下轮重新生成
            ScoutDroneProcedures.TerminateDrone(job);
        }

        private static void TickPreMainWait(ScoutedRaidJob job, int now)
        {
            if (now < job.phaseEndTick) return;
            var ext = job.Ext;
            if (ext == null) { job.phase = ScoutedRaidPhase.Done; return; }
            MainRaidIssuer.Issue(job, ext);
            job.phase = ScoutedRaidPhase.MainRaidIssued;
        }
    }
}
