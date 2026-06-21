using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Fortified
{
    public class CompProperties_SubstationPower : CompProperties_Power
    {
        /// <summary>Minimum Intellect skill level to shut down without risk of electric shock.</summary>
        public int safeShutdownSkillLevel = 8;

        /// <summary>Probability of electric shock when Intellect is below <see cref="safeShutdownSkillLevel"/>.</summary>
        public float shockChance = 0.45f;

        /// <summary>Raw damage dealt to the pawn on a shock event.</summary>
        public float shockDamageAmount = 18f;

        public CompProperties_SubstationPower()
        {
            compClass = typeof(CompSubstationPower);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string e in base.ConfigErrors(parentDef))
                yield return e;
            if (safeShutdownSkillLevel < 0 || safeShutdownSkillLevel > 20)
                yield return $"{nameof(CompProperties_SubstationPower)}: safeShutdownSkillLevel must be 0–20.";
            if (shockChance < 0f || shockChance > 1f)
                yield return $"{nameof(CompProperties_SubstationPower)}: shockChance must be 0–1.";
            if (shockDamageAmount < 0f)
                yield return $"{nameof(CompProperties_SubstationPower)}: shockDamageAmount must be >= 0.";
        }
    }

    /// <summary>
    /// Power plant comp for the underground substation cabinet.
    /// Generates a fixed wattage, can be shut down via the vanilla Hack job
    /// (with an Intellect shock risk for low-skill operators), and is
    /// temporarily disabled by EMP.
    /// </summary>
    public class CompSubstationPower : CompPowerPlant
    {
        // ── reflection handles for resetting CompHackable private state ───────────
        private static readonly FieldInfo FI_hacked =
            AccessTools.Field(typeof(CompHackable), "hacked");
        private static readonly FieldInfo FI_progress =
            AccessTools.Field(typeof(CompHackable), "progress");

        // ElectricalBurn is [MayRequireAnomaly] — fall back to Burn when absent.
        private static DamageDef ShockDamageDef =>
            DamageDefOf.ElectricalBurn
            ?? DefDatabase<DamageDef>.GetNamed("ElectricalBurn", errorOnFail: false)
            ?? DamageDefOf.Burn;

        // ── state ─────────────────────────────────────────────────────────────────
        private bool isShutdown;
        private CompStunnable stunnableComp;
        private CompHackable hackableComp;

        // ── accessors ─────────────────────────────────────────────────────────────
        public new CompProperties_SubstationPower Props =>
            (CompProperties_SubstationPower)props;

        public bool IsShutdown => isShutdown;

        public bool IsEMPDisabled =>
            stunnableComp != null
            && stunnableComp.StunHandler.Stunned
            && stunnableComp.StunHandler.StunFromEMP;

        // ── lifecycle ─────────────────────────────────────────────────────────────
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            stunnableComp = parent.TryGetComp<CompStunnable>();
            hackableComp  = parent.TryGetComp<CompHackable>();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isShutdown, "isShutdown", false);
        }

        // ── power output ──────────────────────────────────────────────────────────
        public override void UpdateDesiredPowerOutput()
        {
            if (isShutdown || IsEMPDisabled || !base.PowerOn)
                base.PowerOutput = 0f;
            else
                base.PowerOutput = DesiredPowerOutput;
        }

        // ── hack callback ─────────────────────────────────────────────────────────
        /// <summary>
        /// Called by CompHackable.OnHacked when the vanilla Hack job completes.
        /// Applies the electric-shock risk, then shuts the unit down.
        /// </summary>
        public override void Notify_Hacked(Pawn hacker = null)
        {
            if (hacker != null)
                TryApplyShock(hacker);

            ExecuteShutdown();
        }

        // ── public API ────────────────────────────────────────────────────────────
        public void ExecuteShutdown()
        {
            isShutdown = true;
            UpdateDesiredPowerOutput();
        }

        /// <summary>
        /// Restore power and reset CompHackable so the unit can be shut down again.
        /// </summary>
        public void ExecuteStartup()
        {
            isShutdown = false;

            if (hackableComp != null)
            {
                FI_hacked.SetValue(hackableComp,  false);
                FI_progress.SetValue(hackableComp, 0f);
            }

            UpdateDesiredPowerOutput();
        }

        // ── gizmos ────────────────────────────────────────────────────────────────
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
                yield return g;

            if (parent.Faction != Faction.OfPlayer || !isShutdown)
                yield break;

            // "Restore Power" — shown only while offline; no skill check required.
            yield return new Command_Action
            {
                defaultLabel = "FFF_Substation_TurnOn".Translate(),
                defaultDesc  = "FFF_Substation_TurnOnDesc".Translate(),
                icon         = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower"),
                action       = ExecuteStartup
            };
        }

        // ── inspect string ────────────────────────────────────────────────────────
        public override string CompInspectStringExtra()
        {
            string baseStr = base.CompInspectStringExtra();

            string statusLine;
            if (IsEMPDisabled)
                statusLine = "FFF_Substation_EMPDisabled".Translate();
            else if (isShutdown)
                statusLine = "FFF_Substation_Offline".Translate();
            else
                statusLine = "FFF_Substation_Online".Translate();

            return baseStr.NullOrEmpty() ? statusLine : statusLine + "\n" + baseStr;
        }

        // ── helpers ───────────────────────────────────────────────────────────────
        private void TryApplyShock(Pawn hacker)
        {
            if (hacker.skills == null) return;

            int intellect = hacker.skills.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;
            if (intellect >= Props.safeShutdownSkillLevel) return;
            if (!Rand.Chance(Props.shockChance)) return;

            DamageInfo dinfo = new DamageInfo(
                ShockDamageDef,
                Props.shockDamageAmount,
                armorPenetration: 0f,
                angle: -1f,
                instigator: parent);

            hacker.TakeDamage(dinfo);

            Messages.Message(
                "FFF_Substation_ShockMessage".Translate(hacker.Named("PAWN")),
                hacker,
                MessageTypeDefOf.NegativeEvent);
        }
    }
}
