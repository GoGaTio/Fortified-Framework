using System.Collections.Generic;

using Verse;
using RimWorld;
using UnityEngine;
using Verse.AI;
using System.Linq;
using RimWorld.Planet;
using static RimWorld.MechClusterSketch;
using Fortified;

namespace Fortified
{
    public class HumanlikeMech : Pawn, IWeaponUsable
    {
        public MechWeaponExtension MechWeapon => def.GetModExtension<MechWeaponExtension>();
        public HumanlikeMechExtension Extension => def.GetModExtension<HumanlikeMechExtension>();
        private Graphic headGraphic;
        public Graphic HeadGraphic
        {
            get
            {
                headGraphic ??= Extension.headGraphic.Graphic;
                if (Extension.canChangeHairStyle && HasHair) headGraphic = Extension.headGraphicHaired.Graphic;
                return headGraphic;
            }
        }
        private bool HasHair => story.hairDef != HairDefOf.Bald || story.hairDef == null;
        public override void PostMake()
        {
            base.PostMake();
            CheckTracker();
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            CheckTracker();
        }
        public override void Kill(DamageInfo? dinfo, Hediff exactCulprit = null)
        {
            if (dinfo == null)//解體殺
            {
                List<Hediff> hediffs = health.hediffSet.hediffs.Where(h => h.def.spawnThingOnRemoved != null).ToList();
                foreach (Hediff item in hediffs)
                {
                    health.RemoveHediff(item);
                    Thing thing = ThingMaker.MakeThing(item.def.spawnThingOnRemoved);
                    thing.stackCount = 1;
                    GenPlace.TryPlaceThing(thing, this.Position, this.Map, ThingPlaceMode.Near);
                }
            }
            base.Kill(dinfo, exactCulprit);
        }
        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                this.Drawer?.renderer?.SetAllGraphicsDirty();
            }
        }
        private void CheckTracker()
        {
            if (Extension != null)
            {
                outfits ??= new Pawn_OutfitTracker(this);
                story ??= new Pawn_StoryTracker(this);
                story.bodyType ??= Extension.bodyTypeOverride;
                story.headType ??= Extension.headTypeOverride;
                story.SkinColorBase = Color.white;

                style ??= new Pawn_StyleTracker(this)
                {
                    beardDef = BeardDefOf.NoBeard,
                    FaceTattoo = null,
                    BodyTattoo = null,
                };

                if (Extension.canChangeHairStyle)
                {
                    story.HairColor = Color.white;
                    story.hairDef ??= HairDefOf.Bald;
                }

                interactions ??= new(this);
                if (skills == null)
                {
                    skills = new(this);
                    skills.skills.ForEach(s => s.Level = def.race.mechFixedSkillLevel);
                    if (!Extension.skills.NullOrEmpty())
                    {
                        foreach (SkillRange item in Extension.skills)
                        {
                            skills.GetSkill(item.Skill).Level = item.Range.RandomInRange;
                        }
                    }
                }
                
                // 初始化工作设置，让机械体能够被分配工作
                workSettings ??= new Pawn_WorkSettings(this);
                workSettings.EnableAndInitializeIfNotAlreadyInitialized();
            }
        }
        public void Equip(ThingWithComps equipment)
        {
            equipment.SetForbidden(false);
            this.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Equip, equipment), JobTag.DraftedOrder);
        }

        public void Wear(ThingWithComps apparel)
        {
            apparel.SetForbidden(false);
            this.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Wear, apparel), JobTag.DraftedOrder);
        }
    }
}