using Verse;
using RimWorld;
using HarmonyLib;

namespace Fortified
{
    [HarmonyPatch(typeof(ITab_Pawn_Gear), "CanControlColonist", MethodType.Getter)]
    public static class Patch_CanDropWeapon
    {
        [HarmonyPostfix]
        public static void CanControl(ref bool __result)
        {
            if (__result) return;
            if (Find.Selector.SingleSelectedThing is Pawn pawn)
            {
                if (pawn is IWeaponUsable && pawn.Faction != null && pawn.Faction.IsPlayer)
                {
                    __result = true;
                }
                if (pawn.TryGetComp<CompDrone>(out var d) && d.CanDraft) __result = true;
            }
        }
    }

	[HarmonyPatch]
	public static class Patch_CanDropWeapon_Sandy_Detailed_RPG_Inventory
	{
		public static MethodBase TargetMethod()
		{
			return AccessTools.Method("Sandy_Detailed_RPG_Inventory.Sandy_Detailed_RPG_GearTab:get_CanControlColonist");
		}

		public static bool Prepare(MethodBase method)
		{
			return AccessTools.Method("Sandy_Detailed_RPG_Inventory.Sandy_Detailed_RPG_GearTab:get_CanControlColonist") != null;
		}

		[HarmonyPostfix]
		public static void Postfix(ref bool __result)
		{
			if (__result) return;
			if (Find.Selector.SingleSelectedThing is Pawn pawn)
            {
                if (pawn is IWeaponUsable && pawn.Faction != null && pawn.Faction.IsPlayer)
                {
                    __result = true;
                }
                if (pawn.TryGetComp<CompDrone>(out var d) && d.CanDraft) __result = true;
            }
		}
	}
}
