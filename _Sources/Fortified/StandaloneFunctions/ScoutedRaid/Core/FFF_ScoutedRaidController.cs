using System.Collections.Generic;
using Verse;

namespace Fortified
{
    // 侦察袭击中央调度器
    public class FFF_ScoutedRaidController : GameComponent
    {
        private static FFF_ScoutedRaidController instance;
        public static FFF_ScoutedRaidController Instance => instance;

        private List<ScoutedRaidJob> jobs = new List<ScoutedRaidJob>();
        private List<ScoutedRaidJob> tempScratch = new List<ScoutedRaidJob>();

        public FFF_ScoutedRaidController(Game game) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            instance = this;
            if (jobs == null) jobs = new List<ScoutedRaidJob>();
        }

        public void RegisterJob(ScoutedRaidJob job)
        {
            if (job == null) return;
            jobs.Add(job);
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (jobs.Count == 0) return;
            // 复制副本避免tick过程中变更
            tempScratch.Clear();
            tempScratch.AddRange(jobs);
            foreach (var job in tempScratch)
            {
                try { ScoutedRaidStateMachine.Tick(job); }
                catch (System.Exception ex)
                {
                    Log.Error($"[Fortified] ScoutedRaid tick失败 {ex}");
                    job.phase = ScoutedRaidPhase.Done;
                }
            }
            jobs.RemoveAll(j => j.phase == ScoutedRaidPhase.Done || j.map == null || j.map.Disposed);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref jobs, "jobs", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && jobs == null) jobs = new List<ScoutedRaidJob>();
        }
    }
}
