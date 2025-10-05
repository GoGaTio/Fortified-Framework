using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System;
using UnityEngine;
using Verse;

namespace Fortified
{
    [StaticConstructorOnStartup]
    public class RoyalTitlePermitWorker_PawnJoin : RoyalTitlePermitWorker_Targeted
    {
        private Faction faction;


        public override void OrderForceTarget(LocalTargetInfo target)
        {
            CallPawn(target.Cell);
        }
        public override IEnumerable<FloatMenuOption> GetRoyalAidOptions(Map map, Pawn pawn, Faction faction)
        {
            if (faction.HostileTo(Faction.OfPlayer))
            {
                yield return new FloatMenuOption("CommandCallRoyalAidFactionHostile".Translate(faction.Named("FACTION")), null);
                yield break;
            }

            Action action = null;
            string description = def.LabelCap + ": ";
            if (FillAidOption(pawn, faction, ref description, out var free))
            {
                action = delegate
                {
                    BeginCallPawn(pawn, faction, map, free);
                };
            }

            yield return new FloatMenuOption(description, action, faction.def.FactionIcon, faction.Color);
        }

        public override IEnumerable<Gizmo> GetCaravanGizmos(Pawn pawn, Faction faction)
        {
            if (!FillCaravanAidOption(pawn, faction, out var description, out free, out var disableNotEnoughFavor))
            {
                yield break;
            }

            Command_Action command_Action = new Command_Action
            {
                defaultLabel = def.LabelCap + " (" + pawn.LabelShort + ")",
                defaultDesc = description,
                icon = ContentFinder<Texture2D>.Get("UI/Commands/CallAid"),
                action = delegate
                {
                        CallPawnToCaravan(pawn, faction, free);              
                }
            };
            if (faction.HostileTo(Faction.OfPlayer))
            {
                command_Action.Disable("CommandCallRoyalAidFactionHostile".Translate(faction.Named("FACTION")));
            }

            if (disableNotEnoughFavor)
            {
                command_Action.Disable("CommandCallRoyalAidNotEnoughFavor".Translate());
            }

            yield return command_Action;
        }

        private void BeginCallPawn(Pawn caller, Faction faction, Map map, bool free)
        {
            targetingParameters = new TargetingParameters();
            targetingParameters.canTargetLocations = true;
            targetingParameters.canTargetBuildings = false;
            targetingParameters.canTargetPawns = false;
            base.caller = caller;
            base.map = map;
            this.faction = faction;
            base.free = free;
            targetingParameters.validator = delegate (TargetInfo target)
            {
                if (def.royalAid.targetingRange > 0f && target.Cell.DistanceTo(caller.Position) > def.royalAid.targetingRange)
                {
                    return false;
                }

                if (!target.Cell.Walkable(map))
                {
                    return false;
                }

                return (!target.Cell.Fogged(map)) ? true : false;
            };
            Find.Targeter.BeginTargeting(this);
        }

        private void CallPawn(IntVec3 cell) //改成叫人的
        {
            List<Thing> list = new List<Thing>();
            if (def.GetModExtension<PawnKindExtension>() != null)
            {
                foreach (Member m in def.GetModExtension<PawnKindExtension>().members)
                {
                    Pawn thing = PawnGenerator.GeneratePawn(m.pawnKind, FactionUtility.DefaultFactionFrom(def.faction));
                    thing.SetFaction(Faction.OfPlayer);
                    if (m.fixedWeapon != null)
                    {
                        thing.equipment.Remove(thing.equipment.Primary);
                        Thing weapon = ThingMaker.MakeThing(m.fixedWeapon);
                        thing.equipment.AddEquipment(weapon as ThingWithComps);
                    }
                    foreach (var item in m.additionalThings)
                    {
                        Thing i = ThingMaker.MakeThing(item.ThingDef);
                        i.stackCount = item.Count;
                        thing.inventory.TryAddItemNotForSale(i);
                    }
                    list.Add(thing);
                }
            }
            else
            {
                for (int i = 0; i < def.royalAid.pawnCount; i++)
                {
                    Thing thing = PawnGenerator.GeneratePawn(def.royalAid.pawnKindDef, FactionUtility.DefaultFactionFrom(def.faction));
                    thing.SetFaction(Faction.OfPlayer);
                    list.Add(thing);
                }
            }
            if (list.Any())
            {
                ActiveTransporterInfo activeDropPodInfo = new ActiveTransporterInfo();
                activeDropPodInfo.innerContainer.TryAddRangeOrTransfer(list);
                DropPodUtility.MakeDropPodAt(cell, map, activeDropPodInfo);
                Messages.Message("MessagePermitTransportDrop".Translate(faction.Named("FACTION")), new LookTargets(cell, map), MessageTypeDefOf.NeutralEvent);
                caller.royalty.GetPermit(def, faction).Notify_Used();
                if (!free)
                {
                    caller.royalty.TryRemoveFavor(faction, def.royalAid.favorCost);
                }
            }
        }

        private void CallPawnToCaravan(Pawn caller, Faction faction, bool free)
        {
            Caravan caravan = caller.GetCaravan();
            if (def.GetModExtension<PawnKindExtension>() != null)
            {
                foreach (Member m in def.GetModExtension<PawnKindExtension>().members)
                {
                    Pawn pawn = PawnGenerator.GeneratePawn(m.pawnKind, FactionUtility.DefaultFactionFrom(def.faction));
                    pawn.SetFaction(Faction.OfPlayer);
                    if (m.fixedWeapon != null)
                    {
                        pawn.equipment.Remove(pawn.equipment.Primary);
                        Thing weapon = ThingMaker.MakeThing(m.fixedWeapon);
                        pawn.equipment.AddEquipment(weapon as ThingWithComps);
                    }
                    foreach (ThingDefCount item in m.additionalThings)
                    {
                        Thing i = ThingMaker.MakeThing(item.ThingDef);
                        i.stackCount = item.Count;
                        pawn.inventory.TryAddItemNotForSale(i);
                    }
                    caravan.AddPawn(pawn, false);
                }
            }
            else
            {
                for (int i = 0; i < def.royalAid.pawnCount; i++)
                {
                    Pawn pawn = PawnGenerator.GeneratePawn(def.royalAid.pawnKindDef, FactionUtility.DefaultFactionFrom(def.faction));
                    pawn.SetFaction(Faction.OfPlayer);
                    caravan.AddPawn(pawn, false);
                }
            }

            Messages.Message("MessagePermitTransportDropCaravan".Translate(faction.Named("FACTION"), caller.Named("PAWN")), caravan, MessageTypeDefOf.NeutralEvent);
            caller.royalty.GetPermit(def, faction).Notify_Used();
            if (!free)
            {
                caller.royalty.TryRemoveFavor(faction, def.royalAid.favorCost);
            }
        }
    }
}
