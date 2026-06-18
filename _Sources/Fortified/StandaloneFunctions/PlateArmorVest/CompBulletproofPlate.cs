using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Fortified;

public class CompProperties_BulletproofPlate : CompProperties
{
    public float armorRating = 0.4f;
    public float maxDurability = 150f;
    public ThingDef plateThing;
    public float durabilityRestorePerMaterial = 10f;
    public string durabilityDescKey = "offering additional {0} on covered body parts, will lose durability after taking damage.\n{1}";
    public string refillDescKey = "durability can be replenished using {0}, restoring {1} points per material.";
    
    public string refillVerbKey = "replenished plates {0} ({1}x {2})";
    public SimpleCurve qualityMultipliers = new SimpleCurve
    {
        new CurvePoint(0f, 0.6f),
        new CurvePoint(1f, 0.8f),
        new CurvePoint(2f, 1f),
        new CurvePoint(3f, 1.2f),
        new CurvePoint(4f, 1.4f),
        new CurvePoint(5f, 1.7f),
        new CurvePoint(6f, 2f)
    };

    public CompProperties_BulletproofPlate()
    {
        compClass = typeof(CompBulletproofPlate);
    }
}

public class CompBulletproofPlate : ThingComp, IReplenishable
{
    protected CompProperties_BulletproofPlate Props => (CompProperties_BulletproofPlate)props;
    protected float currentDurability;

    CompQuality qualityComp;

    public float DurabilityPercent => currentDurability / CurrentMaxDurability();
    public virtual string armorString => "FFF.Armor.Vanilla".Translate($"{CurrentArmorRating():P0}");
    public string DurabilityDescription => Props.durabilityDescKey.Translate(armorString,RefillDescString);
    public string RefillDescString => Props.refillDescKey.Translate(Props.plateThing, Props.durabilityRestorePerMaterial);

    public string RefillVerbString => Props.refillVerbKey.Translate(parent.LabelShort, GetMaterialCostForRefill(), Props.plateThing);
    public Pawn Wearer => ((Apparel)parent)?.Wearer;

    // 新增：讓外部 JobDriver 取得每個材料回復量
    public float DurabilityRestorePerMaterial => Props.durabilityRestorePerMaterial;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        qualityComp = parent.TryGetComp<CompQuality>();
        if (currentDurability == 0 && !respawningAfterLoad)
        {
            currentDurability = CurrentMaxDurability();
        }
    }

    private float GetQualityMultiplier()
    {
        if (qualityComp == null)
        {
            return 1f;
        }
        return Props.qualityMultipliers.Evaluate((int)qualityComp.Quality);
    }

    private float CurrentArmorRating()
    {
        return Props.armorRating * GetQualityMultiplier() * (parent.def.useHitPoints ? ((float)parent.HitPoints / parent.MaxHitPoints) : 1);
    }

    public float CurrentMaxDurability()
    {
        return Props.maxDurability * GetQualityMultiplier() * (parent.def.useHitPoints ? ((float)parent.HitPoints / parent.MaxHitPoints) : 1);
    }

    public bool IsInCoveredGroups(DamageInfo dinfo)
    {
        if (dinfo.HitPart == null || dinfo.HitPart.groups.NullOrEmpty())
        {
            return true; // 如果没有特定的受击部位或该部位没有分组，则默认覆盖
        }
        return parent.def.apparel.bodyPartGroups.ContainsAny(p => (bool)(dinfo.HitPart?.groups?.Contains(p)));
    }

    public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
    {
        absorbed = false;

        if (currentDurability <= 0)
        {
            return;
        }
        if (!dinfo.Def.harmsHealth)
        {
            return;
        }
        if (BypassChance()) return; // 有一定几率完全无视部位覆盖，模拟子弹偶尔会偏移或击中装甲缝隙
        //if (!IsInCoveredGroups(dinfo)) return;

        DamageDef damage = dinfo.Def;
        if (dinfo.HitPart == null) dinfo.SetHitPart(Wearer.health.hediffSet.GetBodyPartRecord(BodyPartDefOf.Torso));
        float damageAmount = ArmorUtility.GetPostArmorDamage(Wearer, dinfo.Amount, dinfo.ArmorPenetrationInt- CurrentArmorRating(), dinfo.HitPart, ref damage, out var _A, out var _B);

        dinfo.Def = damage;
        currentDurability -= damageAmount;
        if (currentDurability < 0) currentDurability = 0;

        DeflectEffect(dinfo);

        if (currentDurability > 0)
        {
            // 耐久仍有剩餘：吸收一半傷害
            dinfo.SetAmount(damageAmount / 2);
            absorbed = true;
        }
        else
        {
            // 耐久耗盡：全額傷害穿透
            dinfo.SetAmount(damageAmount);
        }
    }
    public void DeflectEffect(DamageInfo dinfo)
    {
        if (Wearer != null && Wearer.MapHeld != null)
        {
            FleckMakerEx.ThrowHitDeflectSpark(Wearer.Position.ToVector3() + (CircleConst.AngleRandom) * (Wearer.BodySize - 0.8f), dinfo.Angle, Wearer.MapHeld, FFF_DefOf.FFF_Fleck_DeflectShell, null);
            EffecterDefOf.Deflect_Metal_Bullet.SpawnAttached(Wearer, Wearer.MapHeld).Trigger(new TargetInfo(Wearer.Position, Wearer.MapHeld), Wearer);
        }
    }
    protected bool BypassChance()
    {
        return Rand.Chance(0.1f * (1.01f - DurabilityPercent));
    }
    public void RestoreDurability(float amount)
    {
        currentDurability = Math.Min(currentDurability + amount, CurrentMaxDurability());
    }

    public float GetCurrentDurability()
    {
        return currentDurability;
    }

    public int GetMaterialCostForRefill()
    {
        if (Props.plateThing == null || Props.durabilityRestorePerMaterial <= 0)
        {
            return 0;
        }

        float durabilityNeeded = CurrentMaxDurability() - currentDurability;
        return Math.Max(1, (int)Math.Ceiling(durabilityNeeded / Props.durabilityRestorePerMaterial));
    }

    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
    {
        if (Props.plateThing == null)
        {
            yield break;
        }

        int materialCost = GetMaterialCostForRefill();
        string label = RefillVerbString;

        FloatMenuOption option = new FloatMenuOption(label, () =>
        {
            List<Thing> availableMaterials = HaulAIUtility.FindFixedIngredientCount(selPawn, Props.plateThing, materialCost);

            if (!availableMaterials.NullOrEmpty())
            {
                Job job = JobMaker.MakeJob(FFF_DefOf.FFF_Replenish, availableMaterials[0], parent);
                if (job != null)
                {
                    // 修正：job.count 可能為 -1，使用材料堆疊數與需求量取最小值，並確保至少為 1
                    int availableStack = availableMaterials[0].stackCount;
                    int desired = Mathf.Min(availableStack, materialCost);
                    job.count = Math.Max(1, desired);

                    job.targetQueueB = new List<LocalTargetInfo> { new LocalTargetInfo(parent) };
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
            }
            else
            {
                Messages.Message($"Not enough {Props.plateThing.label} to refill {parent.LabelCap}.", MessageTypeDefOf.RejectInput);
            }
        });

        yield return option;
    }
    public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
    {
        foreach (Gizmo item in base.CompGetWornGizmosExtra())
        {
            yield return item;
        }
        if (parent is Apparel apparel && apparel.Wearer != null && (apparel.Wearer.Faction == Faction.OfPlayer))
        {
            yield return new Gizmo_BulletproofPlateStatus
            {
                bulletproofPlate = this
            };
        }

        // 顯示開發者模式按鈕（只在 Prefs.DevMode 開啟時）
        if (Prefs.DevMode && parent is Apparel devApparel && devApparel.Wearer != null)
        {
            yield return new Command_Action
            {
                defaultLabel = "DEV: Restore Durability",
                defaultDesc = "Developer only: fully restore the bulletproof plate's durability to its quality-adjusted maximum.",
                // 若專案有適合的圖示可放在此處，否則留 null 以使用預設
                icon = null,
                action = delegate
                {
                    float before = currentDurability;
                    float max = CurrentMaxDurability();
                    RestoreDurability(max);
                    Messages.Message($"{parent.LabelCap} durability restored: {before:F1} -> {currentDurability:F1}", MessageTypeDefOf.TaskCompletion);
                }
            };
        }
    }

    public override string CompInspectStringExtra()
    {
        return $"{(GetCurrentDurability()).ToString("F1")}/{CurrentMaxDurability()}\n{RefillDescString}";
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref currentDurability, "currentDurability", Props.maxDurability);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            // 先還原 qualityComp，CurrentMaxDurability() 才能正確計算品質加成上限
            qualityComp = parent.TryGetComp<CompQuality>();
            float cap = CurrentMaxDurability();
            if (currentDurability > cap)
            {
                currentDurability = cap;
            }
        }
    }

    public void Replenish(Pawn actor, int materialCount)
    {
        // 確保不會小於 0
        if (materialCount < 0) materialCount = 0;

        // 計算實際回復的耐久度
        float restoreAmount = Props.durabilityRestorePerMaterial * materialCount;

        // 觸發效果
        for (int i = 0; i < materialCount; i++)
        {
            FleckMaker.ThrowDustPuff(parent.Position.ToVector3(), parent.Map, Rand.Range(0.4f, 0.6f));
        }

        // 提升耐久度
        RestoreDurability(restoreAmount);
    }
}