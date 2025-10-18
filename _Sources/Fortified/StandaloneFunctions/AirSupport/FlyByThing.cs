using RimWorld;
using System;
using System.Data;
using System.Security.Cryptography;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace Fortified
{
    public class FlyByThing : ThingWithComps
    {
        protected ModExt_FlyByThing ext => def.GetModExtension<ModExt_FlyByThing>();

        //AgeTicks only really means when we can discard it.
        public float ageTicks, angle;

        public Vector3 vector = Vector3.forward, exactPos = Vector3.negativeInfinity;
        public virtual bool ShouldDiscard => ageTicks > 0 && !DrawPos.ToIntVec3().InBounds(Map);

        public override Vector3 DrawPos
        {
            get
            {
                var tempLoc = exactPos;
                tempLoc.z += def.skyfaller.zPositionCurve?.Evaluate(ageTicks) ?? 0;
                return tempLoc;
            }
        }

        public virtual Vector3 ShadowDrawPos => exactPos;

        protected Graphic shadowGraphic;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            vector = vector.Yto0();
            angle = vector.AngleFlat();
            shadowGraphic = ext?.shadowGraphic?.Graphic;
            if (exactPos == Vector3.negativeInfinity) exactPos = Position.ToVector3Shifted();
            if (!respawningAfterLoad) InitAge();
            exactPos.y = Altitudes.AltitudeFor(def.altitudeLayer);
        }

        public virtual void InitAge()
        {
            ageTicks = -vector.magnitude * 60 / def.skyfaller.speed;
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Graphic.Draw(drawLoc, default, this, angle);
            if (GenCelestial.IsDaytime(GenCelestial.CurCelestialSunGlow(Map)))
            {
                Vector2 vector = GenCelestial.GetLightSourceInfo(Map, GenCelestial.LightType.LightingSun).vector;
                if (DebugSettings.godMode) Log.Message(ageTicks);
                Vector3 tempLoc = ShadowDrawPos;
                tempLoc.y = Altitudes.AltitudeFor(AltitudeLayer.Item);
                tempLoc += new Vector3(vector.x, 0, vector.y) * (def.skyfaller.zPositionCurve?.Evaluate(ageTicks) ?? 1);
                shadowGraphic?.Draw(tempLoc, default, this, angle);
            }
        }

        protected override void Tick()
        {
            base.Tick();
            ageTicks++;
            Move();

            var t = DrawPos.ToIntVec3();
            t.x = Mathf.Clamp(t.x, 0, Map.Size.x - 1);
            t.z = Mathf.Clamp(t.z, 0, Map.Size.z - 1);
            Position = t;

            if (ShouldDiscard)
            {
                DeSpawn();
                Destroy();
            }
        }

        public virtual void Move()
        {
            exactPos += vector.normalized * def.skyfaller.speed / 60;
            exactPos.y = Altitudes.AltitudeFor(def.altitudeLayer);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ageTicks, "ageTicks");
            Scribe_Values.Look(ref vector, "vector");
        }
    }

    public class ModExt_FlyByThing : DefModExtension
    {
        public GraphicData shadowGraphic;
    }
}