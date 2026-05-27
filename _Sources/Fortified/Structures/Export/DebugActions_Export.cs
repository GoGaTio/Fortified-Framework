// 当白昼倾坠之时
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using LudeonTK;

namespace Fortified.Structures
{
    public static class DebugActions_Export
    {
        [DebugAction("Fortified", "Export FFF Visual Tool...", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void OpenExportTool()
        {
            DebugToolsGeneral.GenericRectTool("Select for Export", rect => {
                Find.WindowStack.Add(new Dialog_ExportStructure(rect, Find.CurrentMap));
            });
        }

		public static Rot4 Rotation = Rot4.North;

		[DebugAction("Fortified", "Change IFFF_Structure rotation", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
		public static void ChangeRotation()
		{
			Rotation.Rotate(RotationDirection.Clockwise);
			Messages.Message("Structure rotation: " + Rotation.ToStringHuman(), MessageTypeDefOf.NeutralEvent, historical: false);
		}

		[DebugAction("Fortified", "Spawn IFFF_Structure...", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void SpawnStructure()
        {
            List<DebugMenuOption> options = new List<DebugMenuOption>();
            
            // 收集所有实现了 IFFF_Structure 的 Def
            var layouts = DefDatabase<StructureLayoutDef>.AllDefs;
            foreach (var def in layouts)
            {
                options.Add(new DebugMenuOption(def.defName + " (Legacy)", DebugMenuOptionMode.Action, () => 
                {
                    DebugTargetStep(def);
                }));
            }

            var structures = DefDatabase<FFF_StructureDef>.AllDefs;
            foreach (var def in structures)
            {
                options.Add(new DebugMenuOption(def.defName + " (New)", DebugMenuOptionMode.Action, () => 
                {
                    DebugTargetStep(def);
                }));
            }

            var settlements = DefDatabase<FFF_SettlementDef>.AllDefs;
            foreach (var def in settlements)
            {
                options.Add(new DebugMenuOption(def.defName + " (Settlement)", DebugMenuOptionMode.Action, () => 
                {
                    DebugTargetStep(def);
                }));
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        private static void DebugTargetStep(IFFF_Structure def)
        {
            DebugTool tool = null;
            tool = new DebugTool("Click to spawn " + (def as Def)?.defName, () => 
            {
                FFF_StructureUtility.Generate(def, UI.MouseCell(), Find.CurrentMap, Faction.OfPlayer, Rotation);
																												  //DebugTools.curTool = null;
			}, null);
            DebugTools.curTool = tool;
        }
    }
}
