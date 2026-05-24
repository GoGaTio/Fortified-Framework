using Verse;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using System;

namespace Fortified;
public static partial class CheckUtility
{

    private const bool DebugLog = false;
    private static void DLog(string message)
    {
        if (DebugLog)
            Verse.Log.Message($"[CheckUtility] {message}");
    }
    public static bool InRange(LocalTargetInfo A, LocalTargetInfo B, float squaredRange)
    {
        return IntVec3Utility.DistanceToSquared(A.Cell, B.Cell) <= squaredRange;
    }
    public static bool HasSubRelayInMapAndInbound(Pawn pawn, LocalTargetInfo target)
    {
        if (pawn == null || CompSubRelay.allSubRelays.NullOrEmpty())
        {
            return false;
        }

        foreach (var relay in CompSubRelay.allSubRelays)
        {
            if (!relay.IsActive || relay.Position == IntVec3.Invalid)
            {
                continue;
            }

            if (relay.parent.Spawned && relay.Map != pawn.MapHeld)
            {
                continue;
            }

            if (relay.Props.coverWholeMap || InRange(relay.Position, target, relay.SquaredDistance))
            {
                return true;
            }
        }

        return false;
    }
    public static bool IsPlayerMech(Pawn pawn)
    {
        return pawn.GetComp<CompOverseerSubject>() != null && pawn.Faction == Faction.OfPlayer;
    }
    public static bool MechanitorCheck(Map map, out Pawn mechanitor)
    {
        mechanitor = null;

        if (map == null)
        {
            return false;
        }

        foreach (Pawn colonist in map.mapPawns.FreeColonists)
        {
            if (MechanitorUtility.IsMechanitor(colonist))
            {
                mechanitor = colonist;
                return true;
            }
        }

        return false;
    }
    public static bool IsMechUseable(Thing mech, ThingWithComps weapon)
    {
        if (UseableInStatic(mech, weapon))
        {
            return true;
        }

        return UseableInRuntime(mech, weapon);
    }
    internal static bool UseableInStatic(Thing mech, ThingWithComps weapon)
    {
        if (mech == null || weapon == null)
        {
            return false;
        }

        if (!mech.def.TryGetModExtension<MechWeaponExtension>(out var extension))
        {
            return false;
        }
        if (extension.EnableWeaponFilter)
        {
            DLog("EnableWeaponFilter");
            return extension.CanUse(weapon);
        }
        else 
        {
            DLog("Heavy");
           return extension.CanUseAsHeavyWeapon(weapon, (mech as Pawn).BodySize);
        }
    }
    public static bool UseableInRuntime(Thing mech, ThingWithComps weapon)
    {
        if (mech == null || weapon == null)
        {
            return false;
        }

        // 優先檢查重型武器擴展
        if (weapon.def.TryGetModExtension<HeavyEquippableExtension>(out var heavyExtension))
        {
            return heavyExtension.CanEquippedBy(mech as Pawn);
        }

        // 檢查機械體武器擴展
        if (!mech.def.TryGetModExtension<MechWeaponExtension>(out var mechExtension))
        {
            return false;
        }

        // 檢查科技等級過濾
        if (mechExtension.EnableTechLevelFilter && !mechExtension.UsableTechLevels.Contains(weapon.def.techLevel))
        {
            return false;
        }

        // 如果啟用了武器過濾，則使用白名單判斷（與 UseableInStatic 行為一致）
        if (mechExtension.EnableWeaponFilter)
        {
            return mechExtension.CanUse(weapon);
        }

        // 當 EnableWeaponFilter 為 false 時，透過 Pawn 體型判斷是否能作為重型武器裝備（與 UseableInStatic 保持一致）
        var pawn = mech as Pawn;
        if (pawn == null)
        {
            return false;
        }

        return mechExtension.CanUseAsHeavyWeapon(weapon, pawn.BodySize);
    }
    public static bool HasAnyHediffOf(Pawn pawn, List<HediffDef> hediffDefs)
    {
        if (pawn == null)
        {
            throw new ArgumentNullException(nameof(pawn));
        }

        if (hediffDefs.NullOrEmpty())
        {
            return false;
        }

        foreach (var hediffDef in hediffDefs)
        {
            if (pawn.health.hediffSet.HasHediff(hediffDef))
            {
                return true;
            }
        }

        return false;
    }
    public static bool HasAnyApparelOf(Pawn pawn, List<ThingDef> apparels)
    {
        if (pawn == null)
        {
            throw new ArgumentNullException(nameof(pawn));
        }

        if (apparels.NullOrEmpty())
        {
            return false;
        }

        return apparels.Any(apparel => WearsApparel(pawn, apparel));
    }
    public static bool Wearable(CompMechApparel comp, ThingWithComps equipment)
    {
        if (comp.Props.ApparelLayerBlackLists.NullOrEmpty())
        {
            return true;
        }

        if (equipment.def.apparel.layers.NullOrEmpty())
        {
            return true;
        }

        foreach (ApparelLayerDef blacklistedLayer in comp.Props.ApparelLayerBlackLists)
        {
            if (equipment.def.apparel.layers.Contains(blacklistedLayer))
            {
                return false;
            }
        }

        return true;
    }
    public static bool IsMannable(TurretMannableExtension extension, Building_Turret turret)
    {
        if (extension == null || turret == null)
        {
            return false;
        }

        if (turret.GetComp<CompMannable>() == null)
        {
            return false;
        }

        if (extension.mannableByDefault)
        {
            return true;
        }

        return extension.BypassMannable != null && extension.BypassMannable.Contains(turret.def.defName);
    }
    public static bool WearsApparel(Pawn pawn, ThingDef thingDef)
    {
        return pawn?.apparel?.WornApparel != null && pawn.apparel.WornApparel.Any(apparel => apparel.def == thingDef);
    }
    public static bool HasAnyGeneOf(Pawn pawn, List<GeneDef> equippableWithGene)
    {
        if (pawn == null)
        {
            throw new ArgumentNullException(nameof(pawn));
        }

        if (equippableWithGene.NullOrEmpty() || pawn.genes == null)
        {
            return false;
        }

        return equippableWithGene.Any(gene => pawn.genes.HasActiveGene(gene));
    }
    public static bool TryGetModExtension<T>(this ThingDef def, out T result) where T : DefModExtension
    {
        result = def.GetModExtension<T>();
        return result != null;
    }
}
