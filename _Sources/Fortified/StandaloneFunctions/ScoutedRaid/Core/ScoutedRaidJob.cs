using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Fortified
{
    // 单次侦察袭击运行实例
    public class ScoutedRaidJob : IExposable
    {
        public IncidentDef sourceIncidentDef;
        public Map map;
        public Faction scoutFaction;
        public Faction mainFaction;
        public float originalPoints;
        public Lord scoutLord;
        // 侦察机
        public Thing scoutDrone;
        public int currentCycle;
        public ScoutedRaidPhase phase;
        public int phaseEndTick;
        public List<ScoutMarkEntry> marks = new List<ScoutMarkEntry>();
        // 弹药序列下标
        public int shellSeqIndex;
        // 锁定入射方向北向锥单位向量
        public Vector3 originAnchor;
        // 本轮第一发照明弹入射点
        public Vector3 lastFlareOrigin;
        // 锁定的进入点
        public IntVec3 lockedSpawnCenter = IntVec3.Invalid;
        // 锁定的边缘进入方向
        public Rot4 lockedSpawnRotation = Rot4.South;
        // 进入点是否已锁定
        public bool spawnLocked;
        // 本轮flare落点缓存
        public List<FlareSlot> cachedFlareSlots;
        // 运行时pawnID到已锁thingID集合不存档PostLoad重建
        public Dictionary<int, HashSet<int>> pawnLockedThings;
        // 运行时(pawnId,thingId)到mark不存档PostLoad重建
        public Dictionary<long, ScoutMarkEntry> markIndex;
        // thingId到邻域玩家建筑数不存档
        public Dictionary<int, int> nearbyCountCache;
        // thingId到邻域计算tick不存档
        public Dictionary<int, int> nearbyTickCache;
        // 上次扫描时仍存活的侦察兵到最后位置不存档
        public Dictionary<int, IntVec3> lastSeenScoutCell;

        // 轻量缓存
        public IncidentExtension_ScoutedRaid Ext
            => sourceIncidentDef?.GetModExtension<IncidentExtension_ScoutedRaid>();

        public void ExposeData()
        {
            Scribe_Defs.Look(ref sourceIncidentDef, "sourceIncidentDef");
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref scoutFaction, "scoutFaction");
            Scribe_References.Look(ref mainFaction, "mainFaction");
            Scribe_Values.Look(ref originalPoints, "originalPoints");
            Scribe_References.Look(ref scoutLord, "scoutLord");
            Scribe_References.Look(ref scoutDrone, "scoutDrone");
            Scribe_Values.Look(ref currentCycle, "currentCycle");
            Scribe_Values.Look(ref phase, "phase");
            Scribe_Values.Look(ref phaseEndTick, "phaseEndTick");
            Scribe_Values.Look(ref shellSeqIndex, "shellSeqIndex");
            Scribe_Values.Look(ref originAnchor, "originAnchor");
            Scribe_Values.Look(ref lastFlareOrigin, "lastFlareOrigin");
            Scribe_Values.Look(ref lockedSpawnCenter, "lockedSpawnCenter", IntVec3.Invalid);
            Scribe_Values.Look(ref lockedSpawnRotation, "lockedSpawnRotation", Rot4.South);
            Scribe_Values.Look(ref spawnLocked, "spawnLocked");
            Scribe_Collections.Look(ref marks, "marks", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (marks == null) marks = new List<ScoutMarkEntry>();
                RebuildRuntimeCaches();
            }
        }

        // 从marks重建运行时索引
        public void RebuildRuntimeCaches()
        {
            pawnLockedThings = new Dictionary<int, HashSet<int>>();
            markIndex = new Dictionary<long, ScoutMarkEntry>();
            nearbyCountCache = new Dictionary<int, int>();
            nearbyTickCache = new Dictionary<int, int>();
            for (int i = 0; i < marks.Count; i++)
            {
                var m = marks[i];
                if (m.ownerPawnId == 0 || m.thingId == 0) continue;
                if (!pawnLockedThings.TryGetValue(m.ownerPawnId, out var set))
                {
                    set = new HashSet<int>();
                    pawnLockedThings[m.ownerPawnId] = set;
                }
                set.Add(m.thingId);
                long key = ((long)m.ownerPawnId << 32) | (uint)m.thingId;
                markIndex[key] = m;
            }
        }

        // 周期切换重置
        public void ResetForNewCycle()
        {
            marks.Clear();
            cachedFlareSlots = null;
            pawnLockedThings = null;
            markIndex = null;
            nearbyCountCache = null;
            nearbyTickCache = null;
            lastSeenScoutCell = null;
            lastFlareOrigin = Vector3.zero;
        }
    }
}
