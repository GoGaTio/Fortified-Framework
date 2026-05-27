using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Fortified
{
	public class JobGiver_WanderInDutyRoom : JobGiver_Wander
	{
		public JobGiver_WanderInDutyRoom()
		{
			wanderRadius = 7f;
			ticksBetweenWandersRange = new IntRange(125, 200);
		}

		protected override IntVec3 GetWanderRoot(Pawn pawn)
		{
			return pawn.mindState.duty.focus.Cell;
		}

		protected override IntVec3 GetExactWanderDest(Pawn pawn)
		{
			IntVec3 wanderRoot = GetWanderRoot(pawn);
			if (!wanderRoot.IsValid)
			{
				return IntVec3.Invalid;
			}
			Room room = wanderRoot.GetRoom(pawn.Map);
			if(room == null)
			{
				District district = wanderRoot.GetDistrict(pawn.Map);
				if (district.Cells.TryRandomElement((c) => CanWanderToCell(c, pawn), out var result))
				{
					return result;
				}
			}
			else if (room.Cells.TryRandomElement((c) => CanWanderToCell(c, pawn), out var result))
			{
				return result;
			}
			float value = wanderRadius;
			PawnDuty duty = pawn.mindState.duty;
			if (duty != null && duty.wanderRadius.HasValue)
			{
				value = duty.wanderRadius.Value;
			}
			return RCellFinder.RandomWanderDestFor(pawn, wanderRoot, value, wanderDestValidator, PawnUtility.ResolveMaxDanger(pawn, maxDanger), canBashDoors);
		}

		private static bool CanWanderToCell(IntVec3 c, Pawn pawn)
		{
			if (!c.WalkableBy(pawn.Map, pawn))
			{
				return false;
			}
			if (c.IsForbidden(pawn))
			{
				return false;
			}
			if (!c.Standable(pawn.Map))
			{
				return false;
			}
			if (!pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly, false))
			{
				return false;
			}
			if (PawnUtility.KnownDangerAt(c, pawn.Map, pawn))
			{
				return false;
			}
			if (!pawn.Map.pawnDestinationReservationManager.CanReserve(c, pawn))
			{
				return false;
			}
			if (c.GetDoor(pawn.Map) != null)
			{
				return false;
			}
			if (c.ContainsStaticFire(pawn.Map))
			{
				return false;
			}
			if (c.GetTerrain(pawn.Map).dangerous)
			{
				return false;
			}
			return true;
		}
	}
}
