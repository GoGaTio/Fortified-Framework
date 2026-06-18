using Fortified;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;

namespace Fortified
{
    [StaticConstructorOnStartup]
    public class CompProperties_AbilitySelfExplosion : CompProperties_AbilityEffect
    {
        public float range;
        public DamageDef explosionDamage;
        public int damageAmount = 5;
        public float armorPenetration = 0.2f;
        public SoundDef explosionSound;
        public EffecterDef explosionEffect;

        public CompProperties_AbilitySelfExplosion()
        {
            compClass = typeof(CompAbilityEffect_AbilitySelfExplosion);
        }
        public override IEnumerable<string> ConfigErrors(AbilityDef parentDef)
        {
            if (explosionDamage == null) explosionDamage = DamageDefOf.Stun;
            return base.ConfigErrors(parentDef);
        }
    }

    public class CompAbilityEffect_AbilitySelfExplosion : CompAbilityEffect
    {
        private new CompProperties_AbilitySelfExplosion Props => (CompProperties_AbilitySelfExplosion)props;
        private Pawn Pawn => parent.pawn;
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            IntVec3 position = parent.pawn.Position;
            GenExplosion.DoExplosion(center: position, map: parent.pawn.MapHeld, radius: Props.range, damType: Props.explosionDamage, instigator: Pawn, damAmount: Props.damageAmount, armorPenetration: Props.armorPenetration, explosionSound: Props.explosionSound, weapon: null, projectile: null, intendedTarget: null, postExplosionSpawnThingDef: null, postExplosionSpawnChance: 0f, postExplosionSpawnThingCount: 0, postExplosionGasType: null, applyDamageToExplosionCellsNeighbors: true, preExplosionSpawnThingDef: null, preExplosionSpawnChance: 0f, preExplosionSpawnThingCount: 0, chanceToStartFire: 0.8f, damageFalloff: false, direction: null, ignoredThings: null, doVisualEffects: true, propagationSpeed: -1, excludeRadius: 0f, doSoundEffects: true);
            base.Apply(target, dest);
            if (Props.explosionEffect != null)
            {
                Effecter effecter = Props.explosionEffect.Spawn();
                effecter.Trigger(new TargetInfo(Pawn.PositionHeld, Pawn.MapHeld), new TargetInfo(Pawn.PositionHeld, Pawn.MapHeld));
                effecter.Cleanup();
            }
        }
        public override void PostApplied(List<LocalTargetInfo> targets, Map map)
        {
            base.PostApplied(targets, map);
            if (!parent.pawn.Destroyed)
            {
                parent.pawn.Destroy(DestroyMode.KillFinalize);
            }
        }
        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            GenDraw.DrawFieldEdges((from x in GenRadial.RadialCellsAround(parent.pawn.PositionHeld, Props.range, useCenter: true)
                                    where x.InBounds(Find.CurrentMap)
                                    select x).ToList(), Color.red);
        }
        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            if (Pawn.Faction != null)
            {
                if(GenAI.EnemyIsNear(this.Pawn, Props.range / 3))
                { return true; }
            }
            return false;
        }
    }
}
