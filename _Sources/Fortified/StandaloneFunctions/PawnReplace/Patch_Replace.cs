using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Fortified
{
    [HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.Spawn), new Type[]
    {
    typeof(Thing),    typeof(IntVec3),    typeof(Map),    typeof(Rot4),    typeof(WipeMode),    typeof(bool),
    typeof(bool)
    })]
    public class Patch_Replace
    {
        [HarmonyPrefix]
        public static bool Prefix(ref Thing newThing, IntVec3 loc, Map map, Rot4 rot)
        {
            try
            {
                if (map == null)
                    return true;

                if (newThing is Pawn pawn && pawn.kindDef?.GetModExtension<ModExtension_ReplacePawn>()
                    is ModExtension_ReplacePawn ex && map.Parent is Site site)
                {
                    // 检查pawn的health是否已初始化
                    if (pawn.health == null)
                    {
                        Log.Error($"[PawnReplace] Pawn {pawn.Label} has null health before replacement");
                        return true;
                    }

                    float point = site.ActualThreatPoints;
                    
                    if (ex.replaces.ToList().Find(r => r.Key.Includes(point)) is
                        KeyValuePair<FloatRange, PawnKindDef> replace)
                    {
                        if (replace.Value == null)
                        {
                            return true;
                        }
                        
                        // 保留原始Pawn的Faction
                        Faction originalFaction = pawn.Faction ?? Faction.OfPlayer;
                        Pawn newPawn = PawnGenerator.GeneratePawn(replace.Value, originalFaction, map.Tile);
                        
                        // 验证新生成的Pawn
                        if (newPawn == null)
                        {
                            Log.Error($"[PawnReplace] Failed to generate replacement pawn for {replace.Value.defName}");
                            return true;
                        }
                        
                        if (newPawn.health == null)
                        {
                            Log.Error($"[PawnReplace] Generated pawn {newPawn.Label} has null health");
                            return true;
                        }
                        
                        newThing = newPawn;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"[PawnReplace] Error in Patch_Replace: {e}");
            }
            return true;
        }
    }
}