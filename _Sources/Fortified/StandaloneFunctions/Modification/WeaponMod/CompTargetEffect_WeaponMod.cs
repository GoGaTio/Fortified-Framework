using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Fortified
{
    public class CompProperties_Targetable_WeaponMod : CompProperties_Targetable
    {
        public WeaponTraitDef traitDef;
        public string availableUsers = "availableUsers".Translate();

        public CompProperties_Targetable_WeaponMod()
        {
            compClass = typeof(CompTargetable_WeaponMod);
        }
    }
    public class CompTargetable_WeaponMod : CompTargetable
    {
        public new CompProperties_Targetable_WeaponMod Props => (CompProperties_Targetable_WeaponMod)props;

        protected override bool PlayerChoosesTarget => true;

        protected override TargetingParameters GetTargetingParameters()
        {
            return new TargetingParameters
            {
                canTargetPawns = false,
                canTargetHumans = false,
                canTargetSubhumans = false,
                canTargetAnimals = false,
                canTargetMechs = false,
                canTargetBuildings = false,
                canTargetLocations = false,
                canTargetItems = true,
            };
        }

        public override IEnumerable<Thing> GetTargets(Thing targetChosenByPlayer = null)
        {
            yield return targetChosenByPlayer;
        }
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!base.ValidateTarget(target, showMessages)) return false;
            if (!target.HasThing || target.Thing is not ThingWithComps thingWithComps) return false;

            if (!CanApplyOnWeapon(thingWithComps))
            {
                if (showMessages) Messages.Message("FFF.Mod.CannotUseDueToNotSupport".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }
            return true;

        }
        protected AcceptanceReport CanApplyOnWeapon(ThingWithComps target)
        {
            if (!target.def.IsWeapon) return new AcceptanceReport("FFF.Mod.NotAGun".Translate());
            if (!target.TryGetComp<CompWeaponMod>(out var u)) return new AcceptanceReport("FFF.Mod.NotModifiable".Translate());

            if (Props.availableUsers.NullOrEmpty()) return true;
            else if (Props.availableUsers.Contains(target.def.defName)) return true;

            return new AcceptanceReport("FFF.Mod.NotValidForThisWeapon".Translate());
        }
        public override void DoEffect(Pawn usedBy)
        {
            if (usedBy.IsColonistPlayerControlled)
            {
                Job job = JobMaker.MakeJob(FFF_DefOf.FFF_Modification, selectedTarget, this.parent);
                job.count = 1;
                job.playerForced = true;
                usedBy.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }
        }
        public override string CompInspectStringExtra()
        {
            string s = Props.availableUsers.Translate();
            if (Props.availableUsers.NullOrEmpty())
            {
                return base.CompInspectStringExtra();
            }
            foreach (var item in Props.availableUsers)
            {
                s += "\n" + item;
            }
            return s;
        }

        internal virtual WeaponTraitDef Apply()
        {
            return Props.traitDef;
        }
    }
}