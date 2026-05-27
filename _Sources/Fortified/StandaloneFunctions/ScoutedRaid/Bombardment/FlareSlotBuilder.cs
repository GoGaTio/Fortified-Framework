using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Fortified
{
    // flare槽位构建
    public static class FlareSlotBuilder
    {
        // 取槽位缓存
        public static List<FlareSlot> GetOrBuild(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext)
        {
            if (job.cachedFlareSlots != null) return job.cachedFlareSlots;
            job.cachedFlareSlots = Build(job, ext);
            return job.cachedFlareSlots;
        }

        // 构建本轮槽位
        private static List<FlareSlot> Build(ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext)
        {
            var slots = new List<FlareSlot>();
            int cap = Mathf.Max(1, ext.maxImpactCellsPerStrike);
            // 无mark直接空打侦察失败由侦察结果体现不再兜底打全图威胁
            if (job.marks == null || job.marks.Count == 0) return slots;
            var validMarks = job.marks.OrderByDescending(m => m.weight).ToList();
            FuseHeavyAndFill(slots, validMarks, job, ext, cap);
            return slots;
        }

        // 高权重anchor半径内凑组合成重型
        private static void FuseHeavyAndFill(List<FlareSlot> slots, List<ScoutMarkEntry> validMarks,
            ScoutedRaidJob job, IncidentExtension_ScoutedRaid ext, int cap)
        {
            int triSize = Mathf.Max(2, ext.marksPerHeavyFlare);
            float fuseR2 = ext.markFusionRadius * ext.markFusionRadius;
            float minSep2 = ext.heavyFlareMinSeparation * ext.heavyFlareMinSeparation;
            var heavyCenters = new List<IntVec3>();
            var consumed = new HashSet<int>();

            for (int ai = 0; ai < validMarks.Count; ai++)
            {
                if (slots.Count >= cap) break;
                if (consumed.Contains(ai)) continue;
                var anchor = validMarks[ai];
                var candidates = CollectFusionCandidates(validMarks, consumed, anchor.cell, ai, fuseR2);
                if (candidates.Count + 1 < triSize)
                {
                    slots.Add(new FlareSlot { cell = anchor.cell, heavy = false });
                    consumed.Add(ai);
                    continue;
                }
                candidates.Sort((a, b) => a.dist2.CompareTo(b.dist2));
                IntVec3 center = ComputeWeightedCentroid(validMarks, candidates, anchor, triSize, out var picked);
                if (!center.InBounds(job.map)) center = anchor.cell;
                if (!IsSpacedFromHeavy(center, heavyCenters, minSep2))
                {
                    slots.Add(new FlareSlot { cell = anchor.cell, heavy = false });
                    consumed.Add(ai);
                    continue;
                }
                slots.Add(new FlareSlot { cell = center, heavy = true });
                heavyCenters.Add(center);
                foreach (var idx in picked) consumed.Add(idx);
            }
            // 余数落普通flare
            for (int i = 0; i < validMarks.Count; i++)
            {
                if (slots.Count >= cap) break;
                if (consumed.Contains(i)) continue;
                slots.Add(new FlareSlot { cell = validMarks[i].cell, heavy = false });
            }
        }

        // 半径内未消费近邻
        private static List<(int idx, int dist2)> CollectFusionCandidates(List<ScoutMarkEntry> marks,
            HashSet<int> consumed, IntVec3 anchorCell, int anchorIdx, float fuseR2)
        {
            var result = new List<(int idx, int dist2)>();
            for (int bi = 0; bi < marks.Count; bi++)
            {
                if (bi == anchorIdx || consumed.Contains(bi)) continue;
                int d2 = (marks[bi].cell - anchorCell).LengthHorizontalSquared;
                if (d2 > fuseR2) continue;
                result.Add((bi, d2));
            }
            return result;
        }

        // 加权重心
        private static IntVec3 ComputeWeightedCentroid(List<ScoutMarkEntry> marks, List<(int idx, int dist2)> candidates,
            ScoutMarkEntry anchor, int triSize, out List<int> picked)
        {
            long sx = anchor.cell.x * (long)anchor.weight;
            long sz = anchor.cell.z * (long)anchor.weight;
            int sw = anchor.weight;
            picked = new List<int>();
            for (int k = 0; k < triSize - 1 && k < candidates.Count; k++)
            {
                int idx = candidates[k].idx;
                var m = marks[idx];
                sx += m.cell.x * (long)m.weight;
                sz += m.cell.z * (long)m.weight;
                sw += m.weight;
                picked.Add(idx);
            }
            return sw > 0
                ? new IntVec3((int)(sx / sw), 0, (int)(sz / sw))
                : anchor.cell;
        }

        // 与已落重型间距校验
        private static bool IsSpacedFromHeavy(IntVec3 center, List<IntVec3> heavyCenters, float minSep2)
        {
            for (int h = 0; h < heavyCenters.Count; h++)
            {
                if ((heavyCenters[h] - center).LengthHorizontalSquared < minSep2) return false;
            }
            return true;
        }
    }
}
