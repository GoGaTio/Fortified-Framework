using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using static HarmonyLib.Code;

namespace Fortified
{
	public class JobDriver_SwitchAmmo : JobDriver
	{
		public CompAmmoSwitch Comp
		{
			get
			{
				if(compInt == null)
				{
					compInt = TargetThingA.TryGetComp<CompAmmoSwitch>();
				}
				return compInt;
			}
		}

		private CompAmmoSwitch compInt;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			Toil wait = ToilMaker.MakeToil("WaitWith");
			wait.initAction = delegate
			{
				wait.actor.pather.StopDead();
			};
			wait.defaultCompleteMode = ToilCompleteMode.Delay;
			wait.defaultDuration = Comp.Props.switchCooldown;
			wait.WithProgressBarToilDelay(TargetIndex.None);
			yield return wait;
			Toil toil = ToilMaker.MakeToil("Switch");
			toil.defaultCompleteMode = ToilCompleteMode.Instant;
			toil.initAction = delegate
			{
				Comp.SetAmmo(Comp.SwitchingToIndex, false);
			};
			yield return toil;
		}
	}
}
