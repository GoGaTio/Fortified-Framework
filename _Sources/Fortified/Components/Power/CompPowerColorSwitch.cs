using RimWorld;
using Verse;

namespace Fortified;
public class CompProperties_PowerColorSwitch : CompProperties
{
    public ColorInt gridColor = new ColorInt(-1, -1, -1, -1);
    public ColorInt batteryColor = new ColorInt(200, 80, 60, 0);

    public CompProperties_PowerColorSwitch()
    {
        compClass = typeof(CompPowerColorSwitch);
    }

    public override System.Collections.Generic.IEnumerable<string> ConfigErrors(ThingDef parentDef)
    {
        foreach (string e in base.ConfigErrors(parentDef))
            yield return e;

        if (parentDef.GetCompProperties<CompProperties_PowerWithInternalBattery>() == null)
            yield return $"{nameof(CompProperties_PowerColorSwitch)}: requires " +
                         $"{nameof(CompProperties_PowerWithInternalBattery)} on the same ThingDef.";

        if (parentDef.GetCompProperties<CompProperties_Glower>() == null)
            yield return $"{nameof(CompProperties_PowerColorSwitch)}: requires " +
                         $"CompProperties_Glower on the same ThingDef.";
    }
}
public class CompPowerColorSwitch : ThingComp
{
    private ColorInt? savedGridColor;

    private CompProperties_PowerColorSwitch SwitchProps => (CompProperties_PowerColorSwitch)props;
    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref savedGridColor, nameof(savedGridColor));
    }
    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        var powerComp = parent.GetComp<CompPowerTrader_InternalBattery>();
        if (powerComp == null) return;

        if (powerComp.SelfPowered)
            ApplyBatteryColor();
        else
            ApplyGridColor();
    }
    public override void ReceiveCompSignal(string signal)
    {
        switch (signal)
        {
            case CompPowerTrader_InternalBattery.Signal_SelfPoweredOn:
                ApplyBatteryColor();
                break;

            case CompPowerTrader_InternalBattery.Signal_SelfPoweredOff:
                ApplyGridColor();
                break;
        }
    }
    private void ApplyBatteryColor()
    {
        CompGlower glower = parent.GetComp<CompGlower>();
        if (glower == null) return;
        savedGridColor = glower.GlowColor;
        glower.GlowColor = SwitchProps.batteryColor;
    }

    private void ApplyGridColor()
    {
        CompGlower glower = parent.GetComp<CompGlower>();
        if (glower == null) return;

        bool usePropsColor = SwitchProps.gridColor.r >= 0;

        if (usePropsColor)
        {
            glower.GlowColor = SwitchProps.gridColor;
        }
        else if (savedGridColor.HasValue)
        {
            glower.GlowColor = savedGridColor.Value;
        }
        savedGridColor = null;
    }
}
