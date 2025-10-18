using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Fortified
{
    public static class OrbitUtility
    {
        public static AcceptanceReport InMicroGravity(Thing thing)
        {
            if (thing?.Map == null) return false;
            if (!thing.Spawned) return false;

            if (!ModsConfig.OdysseyActive) Log.WarningOnce($"Warning, {thing} checking Gravity without OdysseyActive.", 123457);
            if (thing.Map.TileInfo.Layer.Def == PlanetLayerDefOf.Surface)
            {
                return "FFF.Cannot.TableNotInMicroGravity".Translate();
            }
            return true;
        }
        public static AcceptanceReport InVacuum(Thing thing)
        {
            if (thing?.Map == null) return false;
            if (!thing.Spawned) return false;

            if (!ModsConfig.OdysseyActive) Log.WarningOnce($"Warning, {thing} checking Vacuum without OdysseyActive.", 123457);
            if (thing.Position.GetVacuum(thing.Map) < 0.25f) return "FFF.Cannot.TableNotInVacuum".Translate();
            return true;
        }
        public static AcceptanceReport InDarkness(Thing thing)
        {
            if (thing?.Map == null) return false;
            if (!thing.Spawned) return false;
            if (Mathf.Clamp01(thing.Map.glowGrid.GroundGlowAt(thing.Position)) > 0.25f) return "FFF.Cannot.TableNotInDarkness".Translate();
            return true;
        }
        public static AcceptanceReport InCleanRoom(Thing thing)
        {
            if (thing?.Map == null) return false;
            if (!thing.Spawned) return false;
            if (thing.GetStatValue(StatDefOf.Cleanliness) < 0) return "FFF.Cannot.TableNotInCleanRoom".Translate();
            return true;
        }
    }
}