using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Noise;

namespace Fortified;
public class CompProperties_ShieldingDevice : CompProperties
{
    public List<IncidentDef> incidentsWhitelist;
    public int coolDownTicks = 60000;
    public int powerCostPerColonist = 500;

    public EffecterDef activeEffecter;
    public EffecterDef cooldownEffecter;
    public EffecterDef triggerEffecter;

    public CompProperties_ShieldingDevice()
    {
        compClass = typeof(CompShieldingDevice);
    }
}
public class CompShieldingDevice : ThingComp
{
    private CompPowerTrader compPowerInt;
    public static List<CompShieldingDevice> cache = new List<CompShieldingDevice>();
    public CompProperties_ShieldingDevice Props => (CompProperties_ShieldingDevice)props;
    private Effecter effecter;
    public CompPowerTrader CompPower
    {
        get
        {
            compPowerInt ??= parent.TryGetComp<CompPowerTrader>();
            return compPowerInt;
        }
    }
    public bool HasPower => CompPower == null || CompPower.PowerOn;

    private int remainingTicks = 0;
    public bool Active => HasPower && remainingTicks < 1;

    private MapComponent_Population popCache;
    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (!cache.Contains(this)) cache.Add(this);
        if (parent.Map != null)
        {
            popCache = parent.Map.GetComponent<MapComponent_Population>();
            if (popCache == null)
            {
                popCache = new MapComponent_Population(parent.Map);
                parent.Map.components.Add(popCache);
                popCache.FinalizeInit();
            }
            popCache.OnColonistCountChanged += HandleColonistCountChanged;

            // 初始套用一次
            HandleColonistCountChanged(popCache.GetColonistCount());
        }
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        if (popCache != null)
        {
            popCache.OnColonistCountChanged -= HandleColonistCountChanged;
            popCache = null;
        }
        cache.Remove(this);
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        base.PostDestroy(mode, previousMap);
        if (popCache != null)
        {
            popCache.OnColonistCountChanged -= HandleColonistCountChanged;
            popCache = null;
        }
        cache.Remove(this);
    }
    public override void CompTick()
    {
        if (!HasPower) return;
        // 只處理冷卻；耗電調整不在 Tick 中做（由事件觸發）
        if (!Active)
        {
            if (remainingTicks > 0) remainingTicks--;
            if (remainingTicks <= 0)
            {
                remainingTicks = 0;
                Messages.Message("FFF.MessageDeviceReady".Translate(parent.LabelCap), parent, MessageTypeDefOf.PositiveEvent);
            }

            //冷卻中狀態的顯示
            if (Props.cooldownEffecter == null) return;
            if (effecter == null || effecter.def != Props.cooldownEffecter)
            {
                effecter?.Cleanup();
                effecter = Props.cooldownEffecter.SpawnAttached(parent, parent.Map);
            }
            else effecter?.EffectTick(parent, TargetInfo.Invalid);
        }
        else
        {
            //可用狀態的顯示
            if (Props.activeEffecter == null) return;
            if (effecter == null || effecter.def != Props.activeEffecter)
            {
                effecter?.Cleanup();
                effecter = Props.activeEffecter.SpawnAttached(parent, parent.Map);
            }
            else effecter?.EffectTick(parent, TargetInfo.Invalid);
        }
    }
    public override string CompInspectStringExtra()
    {
        return base.CompInspectStringExtra() + "FFF.MessageCooldownRemaining".Translate(remainingTicks.ToStringTicksToPeriod());
    }
    public void TriggerCooldown()
    {
        Props.triggerEffecter.Spawn();
        remainingTicks = Props.coolDownTicks;
    }
    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref remainingTicks, "remainingCoolDownTicks", 0);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (!cache.Contains(this)) cache.Add(this);
        }
    }
    private void HandleColonistCountChanged(int colonistCount)
    {
        if (CompPower == null) return;
        CompPower.PowerOutput = 0 - (Props.powerCostPerColonist * colonistCount + CompPower.Props.PowerConsumption);//人數*500+基礎耗電
    }

    public static IEnumerable<Thing> GetTowerByIncident(IncidentDef def)
    {
        if (cache.NullOrEmpty())
        {
            yield break;
        }
        IEnumerable<CompShieldingDevice> enumerable = cache.Where((CompShieldingDevice x) => x.Props.incidentsWhitelist.Contains(def) && x.Active);
        if (enumerable.EnumerableNullOrEmpty())
        {
            yield break;
        }
        foreach (CompShieldingDevice item in enumerable)
        {
            yield return item.parent;
        }
    }
}