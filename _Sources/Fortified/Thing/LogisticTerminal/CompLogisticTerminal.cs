using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Fortified
{
	public class CompProperties_LogisticTerminal : CompProperties_Interactable
	{
		public ThingSetMakerDef lootMaker;

		public IntRange ticksTillArrivalRange = new IntRange(2500, 5000);

		public IntRange lootPortionsRange = new IntRange(2, 5);

		[MustTranslate]
		public string noLootLeftReport;

		[MustTranslate]
		public string noLootLeftMessage;

		[MustTranslate]
		public string cartArrivedMessage;

		public CompProperties_LogisticTerminal()
		{
			compClass = typeof(CompLogisticTerminal);
		}
	}
	public class CompLogisticTerminal : CompInteractable
	{
		public Building_LogisticTerminal Terminal => (Building_LogisticTerminal)parent;

		public new CompProperties_LogisticTerminal Props => (CompProperties_LogisticTerminal)props;

		public override void PostPostMake()
		{
			base.PostPostMake();
			Terminal.lootPortionsLeft = Props.lootPortionsRange.RandomInRange;
		}

		public override AcceptanceReport CanInteract(Pawn activateBy = null, bool checkOptionalItems = true)
		{
			if (Terminal.lootPortionsLeft <= 0)
			{
				return Props.noLootLeftReport;
			}
			return base.CanInteract(activateBy, checkOptionalItems);
		}

		public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
		{
			if (Terminal.cartCalled)
			{
				yield break;
			}
			foreach (FloatMenuOption item in base.CompFloatMenuOptions(selPawn))
			{
				yield return item;
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if (Terminal.cartCalled)
			{
				yield break;
			}
			foreach (Gizmo item in base.CompGetGizmosExtra())
			{
				yield return item;
			}
		}

		public override string CompInspectStringExtra()
		{
			if (Terminal.lootPortionsLeft > 0)
			{
				return null;
			}
			return Props.noLootLeftReport;
		}

		protected override void OnInteracted(Pawn caster)
		{
			Terminal.ActivatedByPawn(caster);
		}
	}
}
