using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using UnityEngine;
using Verse;
using static System.Collections.Specialized.BitVector32;

namespace Fortified
{
    public class RoyalTitlePermitWorker_CallAirSupport : RoyalTitlePermitWorker_Targeted
    {
        AirSupportDef supportDef => def.GetModExtension<AirSupportExtension>().airSupportDef;

        public override void DrawHighlight(LocalTargetInfo target)
        {
            GenDraw.DrawRadiusRing(caller.Position, def.royalAid.targetingRange, Color.white);
            supportDef.DrawHighlight(map, caller.Position, target);
        } 
        [SyncMethod]
        public override void OrderForceTarget(LocalTargetInfo target)
        {
            supportDef.Trigger(caller, caller.MapHeld, target);
        }

        public override IEnumerable<FloatMenuOption> GetRoyalAidOptions(Map map, Pawn pawn, Faction faction)
        {
            if (supportDef == null)
            {
                Log.Error($"{def.defName} lacks AirSupportExtension");
                yield break;
            }
            if (map.generatorDef.isUnderground)
            {
                yield return new FloatMenuOption(def.LabelCap + ": " + "CommandCallRoyalAidMapUnreachable".Translate(faction.Named("FACTION")), null);
                yield break;
            }
            if (faction.HostileTo(Faction.OfPlayer))
            {
                yield return new FloatMenuOption(def.LabelCap + ": " + "CommandCallRoyalAidFactionHostile".Translate(faction.Named("FACTION")), null);
                yield break;
            }
            string description = def.LabelCap + ": ";
            Action action = null;
            if (FillAidOption(pawn, faction, ref description, out var free))
            {
                action = delegate
                {
                    BeginCallSupport(pawn, map, faction, free);
                    AfterCallActions(pawn, map, faction, free);
                };
            }
            yield return new FloatMenuOption(description, action, faction.def.FactionIcon, faction.Color);
        }

        private void BeginCallSupport(Pawn caller, Map map, Faction faction, bool free)
        {
            targetingParameters = new TargetingParameters();
            targetingParameters.canTargetLocations = true;
            targetingParameters.canTargetSelf = true;
            targetingParameters.canTargetFires = true;
            targetingParameters.canTargetItems = true;
            base.caller = caller;
            base.map = map;
            base.free = free;
            targetingParameters.validator = delegate (TargetInfo target)
            {
                if (def.royalAid.targetingRange > 0f && target.Cell.DistanceTo(caller.Position) > def.royalAid.targetingRange)
                {
                    return false;
                }
                if (target.Cell.Fogged(map))
                {
                    return false;
                }
                RoofDef roof = target.Cell.GetRoof(map);
                return roof == null || !roof.isThickRoof;
            };
            Find.Targeter.BeginTargeting(this);
        }
        private void AfterCallActions(Pawn caller, Map map, Faction faction, bool free)
        {
            caller.royalty.GetPermit(def, faction).Notify_Used();
            if (!free)
            {
                caller.royalty.TryRemoveFavor(faction, def.royalAid.favorCost);
            }
        }
    }
}
