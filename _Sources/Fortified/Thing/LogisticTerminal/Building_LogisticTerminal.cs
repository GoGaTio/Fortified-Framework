using RimWorld;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;
using Verse.Sound;
using static HarmonyLib.Code;
using static RimWorld.MechClusterSketch;

namespace Fortified
{
	
	public class Building_LogisticTerminal : Building, IThingHolder, IShowContents
	{
		public enum Mode
		{
			WaitingEmpty,
			Arriving,
			Leaving,
			WaitingFull
		}
		//Junky method to draw only part of cart, better option would be great
		private Graphic CartGraphic
		{
			get
			{
				if (cartOffset < 2f)
				{
					if (cachedCartGraphic == null)
					{
						cachedCartGraphic = GraphicDatabase.Get<Graphic_Single>(def.graphicData.texPath + "_Cart", ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white);
					}
					return cachedCartGraphic;
				}
				if (cartOffset < 2.4f)
				{
					if (cachedCartGraphicE == null)
					{
						cachedCartGraphicE = GraphicDatabase.Get<Graphic_Single>(def.graphicData.texPath + "_CartE", ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white);
					}
					return cachedCartGraphicE;
				}
				if (cartOffset < 2.8f)
				{
					if (cachedCartGraphicD == null)
					{
						cachedCartGraphicD = GraphicDatabase.Get<Graphic_Single>(def.graphicData.texPath + "_CartD", ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white);
					}
					return cachedCartGraphicD;
				}
				if (cartOffset < 3.2f)
				{
					if (cachedCartGraphicC == null)
					{
						cachedCartGraphicC = GraphicDatabase.Get<Graphic_Single>(def.graphicData.texPath + "_CartC", ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white);
					}
					return cachedCartGraphicC;
				}
				if (cartOffset < 3.6f)
				{
					if (cachedCartGraphicB == null)
					{
						cachedCartGraphicB = GraphicDatabase.Get<Graphic_Single>(def.graphicData.texPath + "_CartB", ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white);
					}
					return cachedCartGraphicB;
				}
				if (cachedCartGraphicA == null)
				{
					cachedCartGraphicA = GraphicDatabase.Get<Graphic_Single>(def.graphicData.texPath + "_CartA", ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white);
				}
				return cachedCartGraphicA;
			}
		}

		[Unsaved(false)]
		private Graphic cachedCartGraphic;

		[Unsaved(false)]
		private Graphic cachedCartGraphicA;

		[Unsaved(false)]
		private Graphic cachedCartGraphicB;

		[Unsaved(false)]
		private Graphic cachedCartGraphicC;

		[Unsaved(false)]
		private Graphic cachedCartGraphicD;

		[Unsaved(false)]
		private Graphic cachedCartGraphicE;

		private Graphic TopGraphic
		{
			get
			{
				if (cachedTopGraphic == null)
				{
					cachedTopGraphic = GraphicDatabase.Get<Graphic_Single>(def.graphicData.texPath + "_Top", ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white);
				}
				return cachedTopGraphic;
			}
		}

		[Unsaved(false)]
		private Graphic cachedTopGraphic;

		public CompLogisticTerminal Comp
		{
			get
			{
				if (cachedComp == null)
				{
					cachedComp = GetComp<CompLogisticTerminal>();
				}
				return cachedComp;
			}
		}

		private CompLogisticTerminal cachedComp;

		public Mode currentMode = Mode.WaitingEmpty;

		public bool cartCalled = false;

		public int lootPortionsLeft = 0;

		public float cartOffset;

		public int ticksLeftTillArrival;

		public ThingOwner innerContainer;

		public bool ShowContentsTab => currentMode == Mode.WaitingFull;

		public Building_LogisticTerminal()
		{
			innerContainer = new ThingOwner<Thing>(this, oneStackOnly: false);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref currentMode, "logisticTerminalMode");
			Scribe_Values.Look(ref cartOffset, "hacartOffsetsCart");
			Scribe_Values.Look(ref cartCalled, "cartCalled");
			Scribe_Values.Look(ref lootPortionsLeft, "lootPortionsLeft");
			Scribe_Values.Look(ref ticksLeftTillArrival, "ticksLeftTillArrival");
			Scribe_Deep.Look(ref innerContainer, "logisticTerminalInnerContainer", this);
		}

		public ThingOwner GetDirectlyHeldThings()
		{
			return innerContainer;
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		}

		public ThingOwner GetContents()
		{
			return innerContainer;
		}

		protected override void Tick()
		{
			base.Tick();
			switch (currentMode)
			{
				case Mode.Arriving:
					if (cartOffset > 0)
					{
						cartOffset -= 0.05f;
						if (cartOffset < 0)
						{
							cartOffset = 0;
							currentMode = Mode.WaitingFull;
							ThingSetMakerParams parms = default(ThingSetMakerParams);
							parms.makingFaction = Faction;
							innerContainer.TryAddRangeOrTransfer(Comp.Props.lootMaker.root.Generate(parms));
							cartCalled = false;
						}
					};
					break;
				case Mode.Leaving:
					cartOffset += 0.05f;
					if (cartOffset >= 4)
					{
						cartOffset = 0;
						currentMode = Mode.WaitingEmpty;
						innerContainer.ClearAndDestroyContents();
						lootPortionsLeft--;
						if(lootPortionsLeft <= 0)
						{
							cartCalled = false;
							Messages.Message(Comp.Props.noLootLeftMessage, this, MessageTypeDefOf.NegativeEvent, true);
						}
					}
					break;
				case Mode.WaitingEmpty:
					if (cartCalled)
					{
						ticksLeftTillArrival--;
						if(ticksLeftTillArrival < 0)
						{
							CartArrive();
						}
					}
					break;
			}
		}

		public void ActivatedByPawn(Pawn pawn)
		{
			if(currentMode == Mode.WaitingFull)
			{
				CartLeave();
			}
			else
			{
				cartCalled = true;
				ticksLeftTillArrival = Comp.Props.ticksTillArrivalRange.RandomInRange;
			}
		}

		public void CartArrive()
		{
			cartOffset = 4f;
			currentMode = Mode.Arriving;
			Messages.Message(Comp.Props.cartArrivedMessage, this, MessageTypeDefOf.PositiveEvent, true);
		}

		public void CartLeave()
		{
			currentMode = Mode.Leaving;
			cartCalled = true;
			ticksLeftTillArrival = Comp.Props.ticksTillArrivalRange.RandomInRange;
		}

		protected override void DrawAt(Vector3 drawLoc, bool flip = false)
		{
			base.DrawAt(drawLoc, flip);
			drawLoc.y = AltitudeLayer.Building.AltitudeFor();
			TopGraphic.Draw(drawLoc, Rot4.North, this);
			if (currentMode == Mode.WaitingEmpty)
			{
				return;
			}
			drawLoc.z -= cartOffset;
			if (cartOffset < 1f)
			{
				drawLoc.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
			}
			else
			{
				drawLoc.y = AltitudeLayer.DoorMoveable.AltitudeFor();
			}
			CartGraphic.Draw(drawLoc, Rot4.North, this);
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo gizmo in base.GetGizmos())
			{
				yield return gizmo;
			}
			if (DebugSettings.ShowDevGizmos)
			{
				if (cartCalled)
				{
					yield return new Command_Action
					{
						defaultLabel = "DEV: Time left = 10",
						action = delegate
						{
							ticksLeftTillArrival = 10;
						}
					};
				}
				if (currentMode == Mode.WaitingFull)
				{
					yield return new Command_Action
					{
						defaultLabel = "DEV: Leave",
						action = delegate
						{
							currentMode = Mode.Leaving;
						}
					};
				}
				else if (currentMode == Mode.WaitingEmpty)
				{
					yield return new Command_Action
					{
						defaultLabel = "DEV: Arrive",
						action = delegate
						{
							cartOffset = 4f;
							currentMode = Mode.Arriving;
						}
					};
				}
			}
		}

		public override string GetInspectString()
		{
			string s = base.GetInspectString();
			if (DebugSettings.ShowDevGizmos)
			{
				if (!s.NullOrEmpty())
				{
					s += "\n";
				}
				else
				{
					s = "";
				}
				s += "DEV: ticks till cart arrival: " + ticksLeftTillArrival;
			}
			return s;
		}
	}
}
