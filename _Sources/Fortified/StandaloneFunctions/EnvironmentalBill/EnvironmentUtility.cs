using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Fortified
{
    public static class EnvironmentUtility
    {
        public static AcceptanceReport InMicroGravity(Thing thing)
        {

            if (!ModsConfig.OdysseyActive) Log.WarningOnce($"Warning, {thing} checking Gravity without OdysseyActive.", 123457);

            if (thing.Map.TileInfo.Layer.Def == PlanetLayerDefOf.Surface)
            {
                return "FFF.Cannot.TableNotInMicroGravity".Translate();
            }
            return true;
        }

        public static AcceptanceReport InPressureBetween(Thing thing, FloatRange range)
        {
            if (!ModsConfig.OdysseyActive) Log.WarningOnce($"Warning, {thing} checking Pressure without OdysseyActive.", 123457);

            float vacuum = thing.Position.GetVacuum(thing.Map);
            if (vacuum < range.min || vacuum > range.max) return "FFF.Cannot.TableNotInPressureBetween".Translate(vacuum.ToStringPercent(), range.min.ToStringPercent(), range.max.ToStringPercent());
            return true;
        }
        public static AcceptanceReport InPressure(Thing thing, float requirement = 0.75f)
        {
            if (!ModsConfig.OdysseyActive) Log.WarningOnce($"Warning, {thing} checking Pressure without OdysseyActive.", 123457);

            float vacuum = thing.Position.GetVacuum(thing.Map);
            if (vacuum > requirement) return "FFF.Cannot.TableNotInPressure".Translate(vacuum,requirement.ToStringPercent());
            return true;
        }
        public static AcceptanceReport InVacuum(Thing thing, float requirement = 0.25f)
        {
            if (!ModsConfig.OdysseyActive) Log.WarningOnce($"Warning, {thing} checking Vacuum without OdysseyActive.", 123457);
            float vacuum = thing.Position.GetVacuum(thing.Map);
            if (vacuum < requirement) return "FFF.Cannot.TableNotInVacuum".Translate(vacuum.ToStringPercent(), requirement.ToStringPercent());
            return true;
        }

        public static AcceptanceReport InLightnessBetween(Thing thing, FloatRange range)
        {
            float lightLevel = Mathf.Clamp01(thing.Map.glowGrid.GroundGlowAt(thing.Position));
            if (lightLevel < range.max || lightLevel > range.min) return "FFF.Cannot.TableNotInLightnessBetween".Translate(lightLevel.ToStringPercent(), range.min.ToStringPercent(), range.max.ToStringPercent());
            return true;
        }
        public static AcceptanceReport InLightness(Thing thing, float requirement = 0.75f)
        {
            float lightLevel = Mathf.Clamp01(thing.Map.glowGrid.GroundGlowAt(thing.Position));
            if (lightLevel < requirement) return "FFF.Cannot.TableNotInLightness".Translate(lightLevel.ToStringPercent(), requirement.ToStringPercent());
            return true;
        }
        public static AcceptanceReport InDarkness(Thing thing, float requirement = 0.25f)
        {
            float lightLevel = Mathf.Clamp01(thing.Map.glowGrid.GroundGlowAt(thing.Position));
            if (lightLevel > requirement) return "FFF.Cannot.TableNotInDarkness".Translate(lightLevel.ToStringPercent(), requirement.ToStringPercent());
            return true;
        }

        public static AcceptanceReport InCleanRoom(Thing thing, float requirement = 0.1f)
        {
            Room room = thing.Position.GetRoom(thing.Map);
            if (room == null) return false;
            float cleanliness = room.GetStat(RoomStatDefOf.Cleanliness);
            if (cleanliness < requirement) return "FFF.Cannot.TableNotInCleanRoom".Translate(cleanliness.ToString("0.##"), requirement.ToString("0.##"));
            return true;
        }

        public static AcceptanceReport InTemperature(Thing thing, FloatRange allowedRange)
        {
            float temperature = thing.AmbientTemperature;
            if (!allowedRange.Includes(temperature)) return "FFF.Cannot.TableNotInTemperatureBetween".Translate(temperature.ToStringTemperature("F0"), allowedRange.min.ToStringTemperature("F0"), allowedRange.max.ToStringTemperature("F0"));
            return true;
        }
    }
}