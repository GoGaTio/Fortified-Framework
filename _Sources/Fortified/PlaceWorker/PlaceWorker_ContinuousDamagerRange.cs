using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Fortified
{
    public class PlaceWorker_ContinuousDamagerRange : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            CompProperties_ContinuousDamager props =
                def.GetCompProperties<CompProperties_ContinuousDamager>();

            if (props == null) return;

            IntVec3        dir   = rot.FacingCell;
            List<IntVec3>  cells = new List<IntVec3>(props.range);

            for (int i = 1; i <= props.range; i++)
                cells.Add(center + dir * i);

            // Orange tint — visually distinct from the normal green ghost
            GenDraw.DrawFieldEdges(cells, Color.Lerp(Color.yellow, Color.red, 0.45f));
        }
    }
}
