using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Fortified
{
    public static class FFF_HostilityEventUtility
    {
        public static void NotifyAllSitesHostilityStarted(Map map, Faction faction)
        {
            if (map == null || faction == null || !faction.HostileTo(Faction.OfPlayer)) return;

            var worldObjects = Find.WorldObjects?.AllWorldObjects;
            if (worldObjects == null) return;

            foreach (var obj in worldObjects)
            {
                if (obj == null || obj.Destroyed || obj.AllComps.NullOrEmpty()) continue;
                foreach (var comp in obj.AllComps)
                {
                    if (comp is WorldObjectComp_PeriodicAirSupport airSupport)
                        airSupport.OnHostilityStarted(map, faction);
                }
            }
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryExecuteWorker")]
    public static class Patch_IncidentWorkerRaid_TryExecuteWorker
    {
        public static void Postfix(bool __result, IncidentParms parms)
        {
            if (!__result) return;
            var map = parms.target as Map;
            FFF_HostilityEventUtility.NotifyAllSitesHostilityStarted(map, parms.faction);
        }
    }
}
