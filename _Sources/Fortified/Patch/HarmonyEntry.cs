using Verse;
using HarmonyLib;

namespace Fortified
{
    [StaticConstructorOnStartup]
    public static class HarmonyEntry
    {
        static HarmonyEntry()
        {
            Harmony entry = new Harmony("Fortified");
            entry.PatchAll();
            // 條件式 patch（找不到目標方法時靜默略過，不中斷啟動）
            Patch_DisableShuttleLaunch.TryApply(entry);
            // 初始化动态补丁管理器，但不挂载（只有任务激活时才挂载）
            FFF_CovertOpsPatchManager.Init();
        }
    }
}