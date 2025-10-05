using System;
using Verse;

namespace Fortified;

/// <summary>
/// 用於追蹤地圖上殖民者數量的組件。計畫應用於隱形裝置等需要根據殖民者數量調整功耗的設備。以及對人物數量具有限制的特殊劇本。
/// </summary>
public class MapComponent_Population : MapComponent
{
    public event Action<int> OnColonistCountChanged;

    public int ColonistCount => lastColonistCount;
    private int lastColonistCount = -1;
    private int checkIntervalTicks = 600; // 每 600 ticks ≈ 10 秒
    private int nextCheckTick = 0;

    public MapComponent_Population(Map map) : base(map) { }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        ForceRefreshNow(); // 讓裝置一出生就拿到正確數值
    }

    public override void MapComponentTick()
    {
        if (Find.TickManager.TicksGame >= nextCheckTick)
        {
            nextCheckTick = Find.TickManager.TicksGame + checkIntervalTicks;
            RefreshIfChanged();
        }
    }

    public int GetColonistCount() => lastColonistCount >= 0 ? lastColonistCount : 0;

    public void ForceRefreshNow()
    {
        int current = map.mapPawns.FreeColonistsSpawnedCount;
        if (current != lastColonistCount)
        {
            lastColonistCount = current;
            OnColonistCountChanged?.Invoke(lastColonistCount);
        }
    }

    private void RefreshIfChanged()
    {
        int current = map.mapPawns.FreeColonistsSpawnedCount;
        if (current != lastColonistCount)
        {
            lastColonistCount = current;
            OnColonistCountChanged?.Invoke(lastColonistCount);
        }
    }
}
