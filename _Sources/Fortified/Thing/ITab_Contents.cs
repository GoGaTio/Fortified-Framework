using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Fortified
{
	public interface IShowContents
	{
		bool ShowContentsTab {  get; }

		ThingOwner GetContents();
	}

	public class ITab_Contents : ITab_ContentsBase
	{
		private List<Thing> listInt = new List<Thing>();

		private static readonly CachedTexture DropTex = new CachedTexture("UI/Buttons/Drop");

		public IShowContents Parent => SelThing as IShowContents;

		public override bool IsVisible => Parent.ShowContentsTab;

		public override IList<Thing> container
		{
			get
			{
				listInt.Clear();
				listInt = Parent.GetContents().ToList();
				return listInt;
			}
		}

		public ITab_Contents()
		{
			labelKey = "TabCasketContents";
			containedItemsKey = "ContainedItems";
			canRemoveThings = true;
		}

		protected override void DoItemsLists(Rect inRect, ref float curY)
		{
			ListContainedThings(inRect, container, ref curY);
		}

		private void ListContainedThings(Rect inRect, IList<Thing> things, ref float curY)
		{
			GUI.BeginGroup(inRect);
			float num = curY;
			Widgets.ListSeparator(ref curY, inRect.width, containedItemsKey.Translate());
			Rect rect = new Rect(0f, num, inRect.width, curY - num - 3f);
			bool flag = false;
			for (int i = 0; i < things.Count; i++)
			{
				Thing thing = things[i];
				DoRow(thing, inRect.width, i, ref curY);
				flag = true;
			}
			if (!flag)
			{
				Widgets.NoneLabel(ref curY, inRect.width);
			}
			GUI.EndGroup();
		}

		private void DoRow(Thing thing, float width, int i, ref float curY)
		{
			Rect rect = new Rect(0f, curY, width, 28f);
			Widgets.InfoCardButton(0f, curY, thing);
			if (Mouse.IsOver(rect))
			{
				Widgets.DrawHighlightSelected(rect);
			}
			else if (i % 2 == 1)
			{
				Widgets.DrawLightHighlight(rect);
			}
			Rect rect2 = new Rect(rect.width - 24f, curY, 24f, 24f);
			if (Widgets.ButtonImage(rect2, DropTex.Texture))
			{
				if (!SelThing.OccupiedRect().AdjacentCells.Where((IntVec3 x) => x.Walkable(SelThing.Map)).TryRandomElement(out var result))
				{
					result = SelThing.Position;
				}
				Parent.GetContents().TryDrop(thing, result, SelThing.Map, ThingPlaceMode.Near, thing.stackCount, out var resultingThing);
				if (resultingThing.TryGetComp(out CompForbiddable comp))
				{
					comp.Forbidden = false;
				}
			}
			//TooltipHandler.TipRegionByKey(rect2, "DMSRC_EjectThingTooltip");
			Widgets.ThingIcon(new Rect(24f, curY, 28f, 28f), thing);
			Rect rect3 = new Rect(60f, curY, rect.width - 36f, rect.height);
			rect3.xMax = rect2.xMin;
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(rect3, thing.LabelCap.Truncate(rect3.width));
			Text.Anchor = TextAnchor.UpperLeft;
			if (Mouse.IsOver(rect))
			{
				TargetHighlighter.Highlight(thing, arrow: true, colonistBar: false);
				TooltipHandler.TipRegion(rect, thing.DescriptionDetailed);
			}
			curY += 28f;
		}
	}
}
