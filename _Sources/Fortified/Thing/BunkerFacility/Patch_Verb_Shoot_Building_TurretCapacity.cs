using HarmonyLib;
using RimWorld;
using Verse;

namespace Fortified
{
    [HarmonyPatch(typeof(Verb_Shoot), "TryCastShot", MethodType.Normal)]
    internal static class Patch_Verb_Shoot_Building_TurretCapacity
    {
        public static void Postfix(ref bool __result, Verb_Shoot __instance)
        {
            if (!__result) return;//發射會成功的狀況

            if (__instance.Caster is not IPawnCapacity capacity) return;//給碉堡裡的操作者增加射擊經驗與紀錄

            capacity.HasPawn(out Pawn castPawn);
            Pawn pawn = __instance.CurrentTarget.Thing as Pawn;
            if (pawn != null && !pawn.Downed && !pawn.IsColonyMech && castPawn.skills != null)
            {
                float num = (pawn.HostileTo(castPawn) ? 10f : 20f);
                float num2 = __instance.verbProps.AdjustedFullCycleTime(__instance, __instance.CasterPawn);
                castPawn.skills.Learn(SkillDefOf.Shooting, num * num2);
            }
            castPawn.records?.Increment(RecordDefOf.ShotsFired);

        }
    }
}