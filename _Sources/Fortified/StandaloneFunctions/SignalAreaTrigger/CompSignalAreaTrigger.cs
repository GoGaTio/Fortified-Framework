using RimWorld;
using System.Collections.Generic;
using Verse;
using UnityEngine;

namespace Fortified;

// Sender: scans a rectangular area at configured intervals and sends a global Signal
// when a valid target is found. Scanning is spread using parent.IsHashIntervalTick(checkInterval)
// to reduce per-tick workload.
public class CompSignalAreaTrigger : ThingComp
{
    private bool triggered;
    private bool showArea;

    public CompProperties_SignalAreaTrigger Props => (CompProperties_SignalAreaTrigger)props;

    public virtual bool IsActive => parent != null && parent.Spawned && !Props.triggerOnce || !triggered;
    public virtual void OnTriggered()
    {        triggered = true;
        //do what ever it need to do.
    }
    public override void CompTick()
    {
        base.CompTick();

        if (parent == null || parent.Map == null || !parent.Spawned) return;

        int interval = Props.checkInterval > 0 ? Props.checkInterval : 60;
        if (!parent.IsHashIntervalTick(interval)) return;
        if (Props.triggerOnce && triggered) return;

        // Center of rectangle is parent position + offset
        IntVec3 center = parent.Position + Props.areaOffset;
        CellRect rect = CellRect.CenteredOn(center, Props.areaSize);

        // Iterate cells inside rectangle. Skip out-of-bounds cells.
        foreach (IntVec3 c in rect)
        {
            if (!c.InBounds(parent.Map)) continue;

            List<Thing> list = c.GetThingList(parent.Map);
            if (list == null || list.Count == 0) continue;

            for (int i = 0; i < list.Count; i++)
            {
                Thing t = list[i];
                if (t is Pawn pawn)
                {
                    if (!pawn.Spawned || pawn.Map != parent.Map) continue;
                    if (Props.onlyHumanlike && !pawn.RaceProps.Humanlike) continue;

                    // TODO: if requireLineOfSight is true, perform LOS check here (raycast / visibility)

                    // Send signal with some useful Named args so receivers can inspect context.
                    // Using mod-prefixed signalTag avoids collisions.
                    Find.SignalManager.SendSignal(new Signal(Props.signalTag,
                        parent.Named("SUBJECT"),
                        parent.Position.Named("POSITION"),
                        parent.Map.Named("MAP"),
                        parent.Named("TRIGGER")));

                    if (Props.triggerOnce)
                    {
                        triggered = true;
                    }

                    OnTriggered();
                    // Found a valid target; stop further scanning for this interval.
                    return;
                }
            }
        }
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref triggered, "triggered", false);
        Scribe_Values.Look(ref showArea, "showArea", false);
    }
    public override void PostDrawExtraSelectionOverlays()
    {
        base.PostDrawExtraSelectionOverlays();
        if (!showArea) return;
        if (!(parent is Building)) return;
        if (parent.Map != Find.CurrentMap) return;

        IntVec3 center = parent.Position + Props.areaOffset;
        CellRect rect = CellRect.CenteredOn(center, Props.areaSize);

        // Collect cells and draw field edges. This is intentionally simple and only runs when toggled on.
        var cells = new List<IntVec3>(rect.Cells);
        if (!cells.NullOrEmpty())
        {
            GenDraw.DrawFieldEdges(cells, Color.yellow);
        }
    }

    //public override IEnumerable<Gizmo> CompGetGizmosExtra()
    //{
    //    foreach (var g in base.CompGetGizmosExtra()) yield return g;

    //    // Only available for buildings (visual toggle)
    //    if (parent is Building)
    //    {
    //        Command_Toggle toggle = new Command_Toggle
    //        {
    //            defaultLabel = "Show trigger area",
    //            defaultDesc = "Toggle display of the configured signal trigger area for this building.",
    //            icon = ContentFinder<Texture2D>.Get("UI/Buttons/Desync", true),
    //            isActive = () => showArea,
    //            toggleAction = () => showArea = !showArea
    //        };
    //        yield return toggle;
    //    }
    //}
}

public class CompProperties_SignalAreaTrigger : CompProperties
{
    // Global signal tag to send. Use a mod-specific prefix to avoid collisions.
    public string signalTag = "Fortified.SignalAreaTrigger";

    // Scan interval in ticks. Uses parent.IsHashIntervalTick to spread work.
    public int checkInterval = 60;

    // Rectangular size (x = width, z = height)
    public IntVec2 areaSize = new IntVec2(3, 3);

    // Offset from parent.Position for center of area.
    public IntVec3 areaOffset = IntVec3.Zero;

    public bool onlyHumanlike = true;
    public bool triggerOnce = false;

    // Placeholder for future: if true, require line of sight from parent to pawn
    public bool requireLineOfSight = false; // TODO: implement LOS checking

    public CompProperties_SignalAreaTrigger()
    {
        compClass = typeof(CompSignalAreaTrigger);
    }
}

// Abstract receiver base: filters signals by tag and spawn state, then calls abstract handler.
public abstract class CompSignalReceiverBase : ThingComp
{
    public CompProperties_SignalReceiverBase Props => (CompProperties_SignalReceiverBase)props;

    public override void Notify_SignalReceived(Signal signal)
    {
        base.Notify_SignalReceived(signal);

        if (Props.listenSignalTag.NullOrEmpty()) return;
        if (signal.tag != Props.listenSignalTag) return;
        if (Props.requireSpawned && (parent == null || !parent.Spawned)) return;

        try
        {
            OnSignalReceived(signal);
        }
        catch (System.Exception e)
        {
            Log.Error($"CompSignalReceiverBase.OnSignalReceived threw: {e}");
        }
    }

    protected abstract void OnSignalReceived(Signal signal);
}

public class CompProperties_SignalReceiverBase : CompProperties
{
    public string listenSignalTag;
    public bool requireSpawned = true;

    public CompProperties_SignalReceiverBase()
    {
        compClass = typeof(CompSignalReceiverBase);
    }
}

// Example receiver: plays an effecter and shows a message when signal received.
public class CompSignalReceiverDoEffect : CompSignalReceiverBase
{
    public new CompProperties_SignalReceiverDoEffect Props => (CompProperties_SignalReceiverDoEffect)props;

    protected override void OnSignalReceived(Signal signal)
    {
        if (parent == null) return;

        if (Props.effecterDef != null && parent.Spawned)
        {
            var eff = new Effecter(Props.effecterDef);
            eff.Trigger(new TargetInfo(parent.Position, parent.Map), TargetInfo.Invalid);
            eff.Cleanup();
        }

        if (!Props.message.NullOrEmpty())
        {
            string text = Props.message.Formatted(parent.LabelCap);
            Messages.Message(text, parent, MessageTypeDefOf.NeutralEvent);
        }
    }
}

public class CompProperties_SignalReceiverDoEffect : CompProperties_SignalReceiverBase
{
    public EffecterDef effecterDef;
    public string message;

    public CompProperties_SignalReceiverDoEffect()
    {
        compClass = typeof(CompSignalReceiverDoEffect);
    }
}
