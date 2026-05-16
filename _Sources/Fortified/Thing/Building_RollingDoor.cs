using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Fortified
{
	public class Building_RollingDoor : Building_Door
	{
		protected override bool CanDrawMovers => false;

		protected override void DrawAt(Vector3 drawLoc, bool flip = false)
		{
			DoorPreDraw();
			drawLoc.z += 1f * OpenPct;
			drawLoc.y += 4.39024404f * OpenPct;
			Graphic.Draw(drawLoc, flip ? base.Rotation.Opposite : base.Rotation, this);
			base.DrawAt(drawLoc, flip);
		}
	}
}
