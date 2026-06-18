using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Fortified
{
    public class CompProperties_PowerWithInternalBattery : CompProperties_Power
    {
        public float internalBatteryMax = 50f;
        public float chargeRateWatts = 15f;
        public CompProperties_PowerWithInternalBattery()
        {
            compClass = typeof(CompPowerTrader_InternalBattery);
        }
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string e in base.ConfigErrors(parentDef))
                yield return e;

            if (internalBatteryMax <= 0f)
                yield return $"{nameof(CompProperties_PowerWithInternalBattery)}: internalBatteryMax must be > 0.";
            if (chargeRateWatts < 0f)
                yield return $"{nameof(CompProperties_PowerWithInternalBattery)}: chargeRateWatts must be >= 0.";
        }
    }
    public class CompPowerTrader_InternalBattery : CompPowerTrader
    {
        public const string Signal_SelfPoweredOn  = "FFF_SelfPoweredOn";
        public const string Signal_SelfPoweredOff = "FFF_SelfPoweredOff";

        private float storedEnergy;   // Watt-days
        private bool  selfPowered;    // true = running off internal battery

        private CompProperties_PowerWithInternalBattery BatteryProps =>
            (CompProperties_PowerWithInternalBattery)props;
        public float StoredEnergy    => storedEnergy;
        public float StoredEnergyPct => storedEnergy / BatteryProps.internalBatteryMax;
        public bool  SelfPowered     => selfPowered;
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref storedEnergy, nameof(storedEnergy), 0f);
            Scribe_Values.Look(ref selfPowered,  nameof(selfPowered),  false);

            // Guard against corrupt saves.
            storedEnergy = Mathf.Clamp(storedEnergy, 0f, BatteryProps.internalBatteryMax);
        }
        public override void SetUpPowerVars()
        {
            base.SetUpPowerVars(); // sets PowerOutput = -basePowerConsumption
            SyncPowerOutput();
        }
        private void SyncPowerOutput()
        {
            if (selfPowered && storedEnergy > 0f)
            {
                PowerOutput = 0f;
            }
            else if (!selfPowered && storedEnergy < BatteryProps.internalBatteryMax)
            {
                PowerOutput = -(Props.PowerConsumption + BatteryProps.chargeRateWatts);
            }
            else
            {
                PowerOutput = -Props.PowerConsumption;
            }
        }

        // ── tick ──────────────────────────────────────────────────────────────────
        public override void CompTick()
        {
            base.CompTick();

            if (!parent.Spawned) return;

            if (PowerOn)
            {
                if (selfPowered)
                {
                    if (GridCanSustainUs())
                    {
                        selfPowered = false;
                        SyncPowerOutput(); // restore −basePower immediately
                        parent.BroadcastCompSignal(Signal_SelfPoweredOff);
                        return;
                    }
                    float consumePerTick = Props.PowerConsumption * WattsToWattDaysPerTick;
                    storedEnergy -= consumePerTick;
                    if (storedEnergy <= 0f)
                    {
                        storedEnergy = 0f;
                        selfPowered  = false;
                        SyncPowerOutput(); // restore −basePower
                        parent.BroadcastCompSignal(Signal_SelfPoweredOff);
                        PowerOn = false;
                    }
                }
                else
                {
                    float chargePerTick = BatteryProps.chargeRateWatts * WattsToWattDaysPerTick;
                    storedEnergy = Mathf.Min(storedEnergy + chargePerTick, BatteryProps.internalBatteryMax);
                    SyncPowerOutput();
                }
            }
            else
            {
                if (storedEnergy > 0f)
                {
                    if (!selfPowered)
                    {
                        selfPowered = true;
                        SyncPowerOutput(); // sets PowerOutput = 0 immediately
                        parent.BroadcastCompSignal(Signal_SelfPoweredOn);
                    }
                    PowerOn = true;    // re-activates glow via BroadcastCompSignal
                }
            }
        }
        private bool GridCanSustainUs()
        {
            PowerNet net = PowerNet;
            if (net == null) return false;
            float postSwitchWatts = storedEnergy < BatteryProps.internalBatteryMax
                ? Props.PowerConsumption + BatteryProps.chargeRateWatts
                : Props.PowerConsumption;

            float needPerTick = postSwitchWatts * WattsToWattDaysPerTick;
            if (net.CurrentEnergyGainRate() >= needPerTick)
                return true;
            return net.CurrentStoredEnergy() >= needPerTick * 600f;
        }

        // ── inspect string ─────────────────────────────────────────────────────────
        public override string CompInspectStringExtra()
        {
            string baseStr  = base.CompInspectStringExtra();
            float  maxWd    = BatteryProps.internalBatteryMax;

            string batteryLine = "FFF_InternalBattery".Translate()
                + ": " + storedEnergy.ToString("F0")
                + " / " + maxWd.ToString("F0") + " Wd";

            if (selfPowered)
                batteryLine += " (" + "FFF_SelfPowered".Translate() + ")";
            else if (storedEnergy >= maxWd)
                batteryLine += " (" + "FFF_BatteryFull".Translate() + ")";

            return baseStr.NullOrEmpty()
                ? batteryLine
                : batteryLine + "\n" + baseStr;
        }
    }
}