using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Fortified
{
    //機械體控制延伸，但這個不限於同個機械師的機體。
    public class CompSubRelay : ThingComp
    {
        public static List<CompSubRelay> allSubRelays = new List<CompSubRelay>();
        public CompProperties_SubRelay Props => (CompProperties_SubRelay)this.props;
        public float CurrentRadius => Props.relayRange;
        public bool AnySelectedDraftedMechs
        {
            get
            {
                List<Pawn> selectedPawns = Find.Selector.SelectedPawns;
                for (int i = 0; i < selectedPawns.Count; i++)
                {
                    if (selectedPawns[i].OverseerSubject != null && selectedPawns[i].Drafted)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        public float SquaredDistance => cacheDistance != 0 ? cacheDistance : GetCacheDistance();
        private float cacheDistance = 0;
        private float GetCacheDistance()
        {
            cacheDistance = Mathf.Pow(CurrentRadius, 2);
            return cacheDistance;
        }
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!allSubRelays.Contains(this)) allSubRelays.Add(this);
        }
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            if (this.parent is not Apparel a && allSubRelays.Contains(this)) allSubRelays.Remove(this);
        }
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (allSubRelays.Contains(this)) allSubRelays.Remove(this);
        }
        public Map Map
        {
            get
            {
                if (this.parent is Pawn p) return p.MapHeld;
                if (this.parent is Apparel a && a.Wearer != null) return a.Wearer.MapHeld;
                if (this.parent is ThingWithComps t && t.Spawned) return t.MapHeld;
                return null;
            }
        }
        public IntVec3 Position
        {
            get
            {
                if (this.parent is Pawn p) return p.PositionHeld;
                if (this.parent is Apparel a && a.Wearer != null) return a.Wearer.PositionHeld;
                if (this.parent is ThingWithComps t && t.Spawned) return t.PositionHeld;
                return IntVec3.Invalid;
            }
        }
        public bool IsActive => isActive;
        bool isActive = false;
        public override void CompTick()
        {
            if (parent is Apparel a)
            {
                isActive = false;
                if (parent.Spawned)
                {
                    if (allSubRelays.Contains(this)) allSubRelays.Remove(this);
                    return;
                } 
                if (a.Wearer != null && !a.WornByCorpse && a.Wearer.Faction == Faction.OfPlayer)
                {
                    isActive = true;
                }
            }
            if (!parent.Spawned) return;

            if (parent is Building b)
            {
                isActive = true;
                if (parent.Faction != Faction.OfPlayer) isActive = false;
                if (b.TryGetComp<CompPowerTrader>(out var p) && !p.PowerOn) isActive = false;
                if (b.IsBrokenDown()) isActive = false;
                if (!b.IsWorking()) isActive = false;
            }
        }
        public override void PostDraw()
        {
            base.PostDraw();
            if (parent.Map != Find.CurrentMap) return;
            if (parent is Building && !isActive) return;

            if (AnySelectedDraftedMechs)
            {
                DrawCommandRadius();
            }
        }
        public override void CompDrawWornExtras()
        {
            base.CompDrawWornExtras();
            if (AnySelectedDraftedMechs)
            {
                GenDraw.DrawRadiusRing(Position, CurrentRadius, Color.white);
            }
        }
        public void DrawCommandRadius()
        {
            if (parent.Spawned && AnySelectedDraftedMechs)
            {
                GenDraw.DrawRadiusRing(parent.Position, CurrentRadius, Color.white);
            }
        }
        public override void Notify_DefsHotReloaded()
        {
            base.Notify_DefsHotReloaded();
            foreach (var item in allSubRelays)
            {
                if (DebugSettings.godMode) Log.Message(item.parent.ToString());
            }
        }
        public override void PostExposeData()
        {
            Scribe_Values.Look(ref isActive, "isActive", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (!allSubRelays.Contains(this)) allSubRelays.Add(this);
            }
        }
    }

    public class CompProperties_SubRelay : CompProperties
    {
        public float relayRange = 10f;
        public CompProperties_SubRelay() => this.compClass = typeof(CompSubRelay);
    }
}