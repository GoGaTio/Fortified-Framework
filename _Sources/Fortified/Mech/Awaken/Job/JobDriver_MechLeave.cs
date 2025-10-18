using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace Fortified
{
    public class JobDriver_MechLeave : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil toil = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            toil.AddPreTickAction(delegate
            {
                if (this.pawn.Position.OnEdge(this.pawn.Map) || this.pawn.Map.exitMapGrid.IsExitCell(this.pawn.Position))
                {
                    Current.Game.GetComponent<GameComponent_DMS>().OutgoingMeches.Add(new OutgoingMech() { mech = this.pawn });
                    this.pawn.ExitMap(true, CellRect.WholeMap(this.pawn.Map).GetClosestEdge(this.pawn.Position));
                }
            });
            yield return toil;
            Toil toil2 = ToilMaker.MakeToil("MakeNewToils");
            toil2.initAction = delegate ()
            {
                if (DebugSettings.godMode) Log.Message(this.pawn.Position.OnEdge(this.pawn.Map));
                if (this.pawn.Position.OnEdge(this.pawn.Map) || this.pawn.Map.exitMapGrid.IsExitCell(this.pawn.Position))
                {
                    Current.Game.GetComponent<GameComponent_DMS>().OutgoingMeches.Add(new OutgoingMech() { mech = this.pawn });
                    this.pawn.ExitMap(true, CellRect.WholeMap(this.pawn.Map).GetClosestEdge(this.pawn.Position));
                }
            };
            toil2.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return toil2;
            yield break;
        }
    }
}
