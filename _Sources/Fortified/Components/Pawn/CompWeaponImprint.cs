using RimWorld;
using System.Security.Cryptography;
using UnityEngine;
using Verse;

namespace Fortified
{
    public class CompWeaponImprint : ThingComp
    {
        public CompProperties_WeaponImprint Props => (CompProperties_WeaponImprint)props;

        private string weaponDef = null;
        private int skillPoint = 1;
        bool Inprinted => ImprintedThingDef != null;
        public ThingDef ImprintedThingDef
        {
            get
            {
                if (weaponDef.NullOrEmpty()) return null;
                var intendedWeaponDef = DefDatabase<ThingDef>.GetNamed(weaponDef, false);
                if (intendedWeaponDef != null) return intendedWeaponDef;
                return null;
            }
        }
        public override void CompTickRare()
        {
            if (!parent.Spawned) return;
            if (!(parent is Pawn pawn)) return;
            if (!pawn.IsPlayerControlled) return;
            if (pawn.health == null) return;
            if (pawn.equipment == null) return;


            if (pawn.equipment.Primary != null)
            {
                CheckWeaponInprint(pawn.equipment.PrimaryEq);

                BodyPartDef part = Props.bodyPart ?? BodyPartDefOf.Head;
                BodyPartRecord bodyPartRecord = pawn.RaceProps.body.GetPartsWithDef(part).FirstOrFallback();

                if (bodyPartRecord == null) return;
                if (pawn.equipment.Primary.def != ImprintedThingDef) //沒拿武器或者沒有拿刻印武器的狀況。
                {
                    if (pawn.health.hediffSet.HasHediff(Props.imprintDef)) pawn.health.RemoveHediff(pawn.health.GetOrAddHediff(Props.imprintDef));
                }
                else
                {
                    skillPoint++;
                    if (pawn.health.hediffSet.HasHediff(Props.imprintDef))
                    {
                        (pawn.health.GetOrAddHediff(Props.imprintDef) as Hediff_Level).SetLevelTo(1 + (skillPoint / Props.pointRequire));
                    }
                    else
                    {
                        Hediff_Level h = HediffMaker.MakeHediff(Props.imprintDef, pawn) as Hediff_Level;
                        pawn.health.AddHediff(h, bodyPartRecord);
                        h.SetLevelTo(1 + (skillPoint / Props.pointRequire));
                    }
                }
            }
            else
            {
                if (pawn.health.hediffSet.HasHediff(Props.imprintDef)) pawn.health.RemoveHediff(pawn.health.GetOrAddHediff(Props.imprintDef));
            }
        }
        private int Level()
        {
            if (Props.imprintDef != null && skillPoint != 0)
            {
                return (int)Mathf.Clamp(1 + (skillPoint / Props.pointRequire), Props.imprintDef.minSeverity, Props.imprintDef.maxSeverity);
            }
            return 0;
        }
        bool CheckWeaponInprint(CompEquippable equippable)
        {
            if (equippable == null) return false;
            if (ImprintedThingDef == null) Inprint(equippable);

            if (equippable.parent.def == ImprintedThingDef)
            {
                return true;
            }
            return false;
        }
        public override string CompInspectStringExtra()
        {
            if (ImprintedThingDef != null)
            {
                return base.CompInspectStringExtra() + "FFF.WeaponImprinted".Translate(ImprintedThingDef.LabelCap, Level());
            }
            else return base.CompInspectStringExtra();
        }
        void Inprint(CompEquippable equippable)
        {
            weaponDef = equippable.parent.def.defName;
            Find.LetterStack.ReceiveLetter("FFF.Inprinted".Translate(this.parent.LabelCap), "FFF.InprintedDesc".Translate(this.parent.LabelCap), LetterDefOf.PositiveEvent, this.parent);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref weaponDef, "imprintedWeaponDef", null);
            Scribe_Values.Look(ref skillPoint, "skillPoint", 0);
        }
    }
    public class CompProperties_WeaponImprint : CompProperties
    {
        public HediffDef imprintDef = null;
        public int pointRequire = 7200; // 7200 * 250 = 1800000 = 30 days
        public BodyPartDef bodyPart = null;
        public CompProperties_WeaponImprint()
        {
            this.compClass = typeof(CompWeaponImprint);
        }
    }
}