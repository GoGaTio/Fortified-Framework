using RimWorld;
using UnityEngine;
using Verse;

namespace Fortified;

[RimWorld.DefOf]
public static class FFF_DefOf
{
    static FFF_DefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(FFF_DefOf));
    }
    public static JobDef FFF_RepairSelf;
    public static JobDef FFF_MechLeave;
    public static JobDef FFF_EnterBunkerFacility;
    public static JobDef FFF_Modification;
    public static JobDef FFF_EjectDeactivatedMech;
    public static JobDef FFF_HackDeactivatedMech;
    public static JobDef FFF_ResurrectMech;
    public static HediffDef FFF_Camouflage;
}

[StaticConstructorOnStartup]
public static class FFF_Icons
{
    public static Texture2D icon_Cancel = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
}
