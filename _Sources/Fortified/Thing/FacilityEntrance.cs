using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Fortified
{
	public class FacilityEntrance : MapPortal
	{
		public List<Pawn> enteringPawns = new List<Pawn>();
		public override void OnEntered(Pawn pawn)
		{
			base.OnEntered(pawn);
			if (pawn.Faction?.IsPlayer == false)
			{
				enteringPawns.Add(pawn);
			}
		}

		protected override void Tick()
		{
			if (!enteringPawns.NullOrEmpty())
			{
				Pawn pawn = enteringPawns.First();
				Lord prevlord = pawn.GetLord();
				if (prevlord != null)
				{
					prevlord.RemovePawn(pawn);
				}
				Lord lord = PocketMap.lordManager.lords.FirstOrDefault((x) => x.faction == pawn.Faction && typeof(LordJob_AssaultColony).IsAssignableFrom(x.LordJob.GetType()));
				if (lord == null)
				{
					lord = LordMaker.MakeNewLord(pawn.Faction, new LordJob_AssaultColony(pawn.Faction, false, false, false, true, false, false, true), PocketMap);
				}
				lord.AddPawn(pawn);
				Log.Message((Find.CurrentMap == PocketMap) + ", " + (PocketMap == Map) + ", " + (pawn.Map == PocketMap));
				pawn.jobs.CheckForJobOverride();
				enteringPawns.Remove(pawn);
			}
			if (this.IsHashIntervalTick(2500) && PocketMap != null)
			{
				foreach (Lord lord in Map.lordManager.lords.ToList())
				{
					if (lord.faction.IsPlayer == true)
					{
						continue;
					}
					string name = lord.CurLordToil.ToString();
					if (!name.Contains("Assault") && !name.Contains("Attack") && !name.Contains("Hunt"))
					{
						continue;
					}
					if (!GenHostility.AnyHostileActiveThreatTo(Map, lord.faction) && GenHostility.AnyHostileActiveThreatTo(PocketMap, lord.faction))
					{
						List<Pawn> list = lord.ownedPawns.ToList();
						foreach (Pawn p in list)
						{
							if (p.jobs == null || p.CurJobDef == JobDefOf.EnterPortal)
							{
								continue;
							}
							Job job = JobMaker.MakeJob(JobDefOf.EnterPortal, this);
							job.checkOverrideOnExpire = false;
							job.expiryInterval = 99999;
							job.expireOnEnemiesNearby = false;
							job.playerForced = true;
							p.jobs.TryTakeOrderedJob(job, JobTag.Misc);
						}
					}
				}
			}
			base.Tick();
		}
	}

	public class FacilityExit : PocketMapExit
	{
		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			if (!respawningAfterLoad && MapGenerator.mapBeingGenerated == map)
			{
				IntVec3 cell = this.OccupiedRect().ExpandedBy(1).EdgeCells.FirstOrDefault((x) => x.Standable(map));
				if(cell == null || !cell.IsValid)
				{
					return;
				}
				MapGenerator.PlayerStartSpot = cell;
			}
		}
	}
}
