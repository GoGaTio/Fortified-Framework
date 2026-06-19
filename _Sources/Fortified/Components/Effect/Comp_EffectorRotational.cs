using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Fortified
{
    /// <summary>
    /// Properties for Comp_EffectorRotational.
    /// Supports two offset modes (mutually exclusive):
    ///   1. offsetPerRotation — explicit [N, E, S, W] Vector3 list (4 entries).
    ///   2. baseOffset + rotateBaseOffset — one Vector3 auto-rotated by building Rotation.
    /// </summary>
    public class CompProperties_EffectorRotational : CompProperties
    {
        // --- Required ---
        public EffecterDef effecterDef;

        // --- Offset mode 1: explicit per-rotation offsets [N=0, E=1, S=2, W=3] ---
        public List<Vector3> offsetPerRotation;

        // --- Offset mode 2: base offset, optionally rotated ---
        public Vector3 baseOffset = Vector3.zero;
        /// <summary>
        /// When true (default), baseOffset is rotated to match building Rotation.
        /// When false, baseOffset is applied as-is regardless of rotation.
        /// </summary>
        public bool rotateBaseOffset = true;

        // --- Activation conditions ---
        /// <summary>If true, effecter only runs when the building has power.</summary>
        public bool requiresPower;
        /// <summary>If true, effecter only runs when parent.IsHashIntervalTick passes (perf).</summary>
        public int tickInterval = 1;

        public CompProperties_EffectorRotational()
        {
            compClass = typeof(Comp_EffectorRotational);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string err in base.ConfigErrors(parentDef))
                yield return err;

            if (effecterDef == null)
                yield return $"[{nameof(CompProperties_EffectorRotational)}] effecterDef is null on {parentDef.defName}";

            if (!offsetPerRotation.NullOrEmpty() && offsetPerRotation.Count != 4)
                yield return $"[{nameof(CompProperties_EffectorRotational)}] offsetPerRotation must have exactly 4 entries [N,E,S,W], found {offsetPerRotation.Count} on {parentDef.defName}";
        }
    }

    /// <summary>
    /// Maintains a continuous Effecter whose spawn offset is updated every time
    /// the parent building's Rotation changes.
    ///
    /// Place on any building that has rotatable variants (e.g. chimneys, exhaust vents).
    /// The effecter is ticked manually each CompTick so SubEffecterDef.chancePerTick
    /// and similar properties work normally.
    /// </summary>
    public class Comp_EffectorRotational : ThingComp
    {
        private Effecter effecter;
        private Rot4 cachedRotation = Rot4.Invalid;

        public CompProperties_EffectorRotational Props =>
            (CompProperties_EffectorRotational)props;

        // ── Activation ───────────────────────────────────────────────────────────

        private bool ShouldBeActive
        {
            get
            {
                if (!parent.Spawned || parent.Map == null)
                    return false;

                if (Props.requiresPower)
                {
                    CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
                    if (power != null && !power.PowerOn)
                        return false;
                }

                return true;
            }
        }

        // ── Offset resolution ────────────────────────────────────────────────────

        private Vector3 GetOffsetForRotation(Rot4 rot)
        {
            // Mode 1: explicit list wins if present and valid
            if (!Props.offsetPerRotation.NullOrEmpty())
                return Props.offsetPerRotation[rot.AsInt];

            // Mode 2: rotate baseOffset or return it raw
            if (Props.rotateBaseOffset)
                return Props.baseOffset.RotatedBy(rot);

            return Props.baseOffset;
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            // Delay actual spawn to first tick so the map is fully ready.
        }

        public override void CompTick()
        {
            base.CompTick();

            // Optional throttle: skip frames to save perf on dense maps.
            if (Props.tickInterval > 1 && !parent.IsHashIntervalTick(Props.tickInterval))
                return;

            if (!ShouldBeActive)
            {
                TryDestroyEffecter();
                return;
            }

            EnsureEffecter();
            UpdateOffsetIfRotationChanged();
            TickEffecter();
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            TryDestroyEffecter();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            TryDestroyEffecter();
        }

        // ── Internal helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Creates the Effecter if it does not exist yet.
        /// Uses new Effecter(def) instead of def.Spawn() to avoid an immediate
        /// one-shot Trigger call before we have set the correct offset.
        /// </summary>
        private void EnsureEffecter()
        {
            if (effecter != null)
                return;

            effecter = new Effecter(Props.effecterDef)
            {
                offset = GetOffsetForRotation(parent.Rotation)
            };
            cachedRotation = parent.Rotation;
        }

        /// <summary>
        /// If the building has been rotated (blueprint flip, etc.), refreshes
        /// effecter.offset so subsequent SubEffecter ticks use the new position.
        /// </summary>
        private void UpdateOffsetIfRotationChanged()
        {
            Rot4 current = parent.Rotation;
            if (current == cachedRotation)
                return;

            cachedRotation = current;
            effecter.offset = GetOffsetForRotation(current);
        }

        /// <summary>
        /// Feeds both TargetInfo slots with the parent building so that
        /// SubEffecterDef properties such as useTargetAInitialRotation and
        /// perRotationOffsets (from the def side) also work correctly.
        /// </summary>
        private void TickEffecter()
        {
            TargetInfo target = new TargetInfo(parent.Position, parent.Map);
            effecter.EffectTick(target, target);
        }

        private void TryDestroyEffecter()
        {
            if (effecter == null)
                return;

            effecter.Cleanup();
            effecter = null;
            cachedRotation = Rot4.Invalid;
        }
    }
}
