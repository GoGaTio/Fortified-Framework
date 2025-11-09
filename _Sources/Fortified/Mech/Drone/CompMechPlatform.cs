using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Fortified
{
    public class CompMechPlatform : ThingComp, IThingHolder
    {
        private const int LowIngredientCountThreshold = 75;

        private int cooldownTicksRemaining;

        private ThingOwner innerContainer;

        private List<Pawn> spawnedPawns = new List<Pawn>();

        public int maxToFill;

        private List<Thing> tmpResources = new List<Thing>();

        private int selectedAreaId = -1;
        public Area SelectedArea
        {
            get
            {
                return (parent?.Map != null && selectedAreaId >= 0)
                ? parent.Map.areaManager.AllAreas.FirstOrDefault(a => a?.ID == selectedAreaId)
                : null;
            }
        }

        public CompProperties_MechPlatform Props => (CompProperties_MechPlatform)props;

        public virtual AcceptanceReport CanSpawn
        {
            get
            {
                if (parent is Pawn pawn)
                {
                    if (pawn.IsSelfShutdown())
                    {
                        return "SelfShutdown".Translate();
                    }

                    if (pawn.Faction == Faction.OfPlayer && !pawn.IsColonyMechPlayerControlled)
                    {
                        return false;
                    }

                    if (!pawn.Awake() || pawn.Downed || pawn.Dead || !pawn.Spawned)
                    {
                        return false;
                    }
                }
                else if (parent is Building building)
                {
                    if (building.TryGetComp<CompPowerTrader>(out var _power)&& !_power.PowerOn )
                    {
                        return "NoPower".Translate();
                    }
                    if (building.TryGetComp<CompBreakdownable>(out var _broke)&& _broke.BrokenDown)
                    {
                        return "BrokenDown".Translate();
                    }
                    if (building.TryGetComp<CompFlickable>(out var _flick)&& !_flick.SwitchIsOn)
                    {
                        return "Deactivated".Translate();
                    }
                }

                if (MaxCanSpawn <= 0)
                {
                    return "MechCarrierNotEnoughResources".Translate();
                }

                if (cooldownTicksRemaining > 0)
                {
                    return "CooldownTime".Translate() + " " + cooldownTicksRemaining.ToStringSecondsFromTicks();
                }

                return true;
            }
        }

        public virtual int IngredientCount => innerContainer.TotalStackCountOfDef(Props.fixedIngredient);

        public virtual int AmountToAutofill => Mathf.Max(0, maxToFill - IngredientCount);

        public virtual int MaxCanSpawn => Mathf.Min(Mathf.FloorToInt(IngredientCount / Props.costPerPawn), Props.maxPawnsToSpawn);

        public bool LowIngredientCount => IngredientCount < LowIngredientCountThreshold;

        public float PercentageFull => (float)IngredientCount / (float)Props.maxIngredientCount;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (!ModLister.CheckBiotech("Mech carrier"))
            {
                parent.Destroy();
                return;
            }

            base.PostSpawnSetup(respawningAfterLoad);
            CleanupSpawnedPawns();
            if (!respawningAfterLoad && !parent.BeingTransportedOnGravship)
            {
                var c = Props.startingIngredientCount;

                if (!parent.Faction.IsPlayer) //NPC陣營自動填滿。
                {
                    this.autoDeployEnabled = true;
                    c = Props.maxIngredientCount;
                } 

                innerContainer = new ThingOwner<Thing>(this, oneStackOnly: false);
                if (c > 0)
                {
                    int count = c;
                    while (count > 0)
                    {
                        Thing thing = ThingMaker.MakeThing(Props.fixedIngredient);
                        thing.stackCount = Mathf.Min(count, Props.fixedIngredient.stackLimit);
                        innerContainer.TryAdd(thing, thing.stackCount);
                        count -= Mathf.Min(count, Props.fixedIngredient.stackLimit);
                    }
                }
                maxToFill = c;
            }
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            if (AmountToAutofill <= 0) yield break;
            FloatMenuOption floatMenuOption = new FloatMenuOption("FFF.FillMechPlatform".Translate(Props.fixedIngredient.label, AmountToAutofill), delegate
            {
                List<Thing> list = HaulAIUtility.FindFixedIngredientCount(selPawn, this.Props.fixedIngredient, AmountToAutofill);
                if (!list.NullOrEmpty())
                {
                    Job job = HaulAIUtility.HaulToContainerJob(selPawn, list[0], this.parent);
                    job.count = Mathf.Min(job.count, AmountToAutofill);
                    job.targetQueueB = (from i in list.Skip(1) select new LocalTargetInfo(i)).ToList();
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, false);
                }
            }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
            if (!floatMenuOption.Disabled)
            {
                yield return floatMenuOption;
            }
        }

        public void TrySpawnPawns()
        {
            int maxCanSpawn = MaxCanSpawn;
            if (maxCanSpawn <= 0)
            {
                return;
            }

            PawnGenerationRequest request = new PawnGenerationRequest(Props.spawnPawnKind, parent.Faction, PawnGenerationContext.NonPlayer, null, forceGenerateNewPawn: true, allowDead: false, allowDowned: false, canGeneratePawnRelations: true, mustBeCapableOfViolence: false, 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, forceNoIdeo: false, forceNoBackstory: false, forbidAnyTitle: false, forceDead: false, null, null, null, null, null, 0f, DevelopmentalStage.Newborn);
            tmpResources.Clear();
            tmpResources.AddRange(innerContainer);
            Lord lord = ((parent is Pawn p) ? p.GetLord() : null);
            for (int i = 0; i < maxCanSpawn; i++)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(request);

                // Set the pawn's platform if it is a drone.
                if (pawn.TryGetComp<CompDrone>(out var d))
                {
                    d.SetPlatform(parent);
                }

                GenSpawn.Spawn(pawn, parent.Position, parent.Map);
                spawnedPawns.Add(pawn);
                lord?.AddPawn(pawn);

                if (selectedAreaId != -1)
                {
                    pawn.playerSettings.AreaRestrictionInPawnCurrentMap = SelectedArea;
                }

                int num = Props.costPerPawn;
                for (int j = 0; j < tmpResources.Count; j++)
                {
                    Thing thing = innerContainer.Take(tmpResources[j], Mathf.Min(tmpResources[j].stackCount, Props.costPerPawn));
                    num -= thing.stackCount;
                    thing.Destroy();
                    if (num <= 0)
                    {
                        break;
                    }
                }

                if (Props.spawnedMechEffecter != null)
                {
                    Effecter effecter = new Effecter(Props.spawnedMechEffecter);
                    effecter.Trigger(Props.attachSpawnedMechEffecter ? ((TargetInfo)pawn) : new TargetInfo(pawn.Position, pawn.Map), TargetInfo.Invalid);
                    effecter.Cleanup();
                }
            }

            tmpResources.Clear();
            cooldownTicksRemaining = Props.cooldownTicks;
            if (Props.spawnEffecter != null)
            {
                Effecter effecter2 = new Effecter(Props.spawnEffecter);
                effecter2.Trigger(Props.attachSpawnedEffecter ? ((TargetInfo)parent) : new TargetInfo(parent.Position, parent.Map), TargetInfo.Invalid);
                effecter2.Cleanup();
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent is Building b && this.parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }
            if (parent is Pawn pawn && !pawn.IsColonyMech && pawn.GetOverseer() == null)
            {
                yield break;
            }
            foreach (Gizmo item in base.CompGetGizmosExtra())
            {
                yield return item;
            }

            // Add the gizmo to retract spawned pawns if there are any.
            if (!spawnedPawns.NullOrEmpty())
            {
                Command_Action command_Action = new Command_Action
                {
                    action = delegate
                    {
                        foreach (Pawn item in spawnedPawns)
                        {
                            if (item.TryGetComp<CompDrone>(out var d))
                            {
                                d.ReturnToPlatform();
                            }
                        }
                    },
                    hotKey = KeyBindingDefOf.Misc3,
                    icon = ContentFinder<Texture2D>.Get(Props.gizmoIconPath_Retract),
                    defaultLabel = "FFF.RetractDrones".Translate(Props.spawnPawnKind.labelPlural),
                    defaultDesc = "FFF.RetractDronesDesc".Translate(Props.spawnPawnKind.labelPlural, Props.fixedIngredient.label)
                };
                yield return command_Action;
            }

            AcceptanceReport canSpawn = CanSpawn;
            Command_ActionWithCooldown act = new Command_ActionWithCooldown
            {
                cooldownPercentGetter = () => Mathf.InverseLerp(Props.cooldownTicks, 0f, cooldownTicksRemaining),
                action = delegate
                {
                    TrySpawnPawns();
                },
                hotKey = KeyBindingDefOf.Misc2,
                Disabled = !canSpawn.Accepted,
                icon = ContentFinder<Texture2D>.Get(Props.gizmoIconPath),
                defaultLabel = "FFF.DeployDrone".Translate(Props.spawnPawnKind.labelPlural),
                defaultDesc = "FFF.DeployDroneDesc".Translate(Props.fixedIngredient.label, Props.maxPawnsToSpawn, Props.spawnPawnKind.label, Props.spawnPawnKind.labelPlural, Props.spawnPawnKind.label)
            };
            if (!canSpawn.Reason.NullOrEmpty())
            {
                act.Disable(canSpawn.Reason);
            }
            Command_Toggle command_Toggle = new Command_Toggle
            {
                defaultLabel = "FFF.AutoDeploy".Translate(),
                defaultDesc = "FFF.AutoDeployDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Drone_AutoDeploy"),
                isActive = () => autoDeployEnabled,
                toggleAction = () =>
                {
                    autoDeployEnabled = !autoDeployEnabled;
                }
            };
            yield return command_Toggle;


            if (DebugSettings.ShowDevGizmos)
            {
                if (cooldownTicksRemaining > 0)
                {
                    Command_Action command_Action = new Command_Action();
                    command_Action.defaultLabel = "DEV: Reset cooldown";
                    command_Action.action = delegate
                    {
                        cooldownTicksRemaining = 0;
                    };
                    yield return command_Action;
                }

                Command_Action command_Action2 = new Command_Action();
                command_Action2.defaultLabel = "DEV: Fill with " + Props.fixedIngredient.label;
                command_Action2.action = delegate
                {
                    while (IngredientCount < Props.maxIngredientCount)
                    {
                        int stackCount = Mathf.Min(Props.maxIngredientCount - IngredientCount, Props.fixedIngredient.stackLimit);
                        Thing thing = ThingMaker.MakeThing(Props.fixedIngredient);
                        thing.stackCount = stackCount;
                        innerContainer.TryAdd(thing, thing.stackCount);
                    }
                };
                yield return command_Action2;
                Command_Action command_Action3 = new Command_Action();
                command_Action3.defaultLabel = "DEV: Empty " + Props.fixedIngredient.label;
                command_Action3.action = delegate
                {
                    innerContainer.ClearAndDestroyContents();
                };
                yield return command_Action3;
            }

            yield return act;


            TaggedString currentLabel = SelectedArea != null ? SelectedArea.Label : "FFF.Drone.NoRestrict".Translate();
            yield return new Command_Action
            {
                defaultLabel = currentLabel,
                defaultDesc = "FFF.Drone.AllowedAreaDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Drone_AreaAllowed", true),
                defaultIconColor = SelectedArea?.Color ?? Color.white,
                action = () =>
                {
                    var options = AreaOptions(parent.Map);
                    if (options.NullOrEmpty()) return;
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }

        private List<FloatMenuOption> AreaOptions(Map map)
        {
            var list = new List<FloatMenuOption>
            {
                new FloatMenuOption("FFF.Drone.NoRestrict".Translate(), () =>
            {
                selectedAreaId = -1;
            })
            };
            foreach (var area in map.areaManager.AllAreas.Where(a=>a.AssignableAsAllowed()))
            {
                var label = area.Label;
                var opt = new FloatMenuOption(label, () =>
                {
                    selectedAreaId = area.ID;
                    foreach (var p in spawnedPawns)
                    {
                        p.playerSettings.AreaRestrictionInPawnCurrentMap = area;
                    }
                });
                // 額外在右側畫出顏色預覽小方塊
                opt.extraPartWidth = 24f;
                opt.extraPartOnGUI = rect =>
                {
                    var colorRect = new Rect(rect.xMax - 20f, rect.y + (rect.height - 14f) / 2f, 14f, 14f);
                    Widgets.DrawBoxSolidWithOutline(colorRect, area.Color, Color.black, 1);
                    return false;
                };

                list.Add(opt);
            }
            return list;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public override string CompInspectStringExtra()
        {
            string text = base.CompInspectStringExtra();
            if (text.NullOrEmpty()) text = "";

            text += "CasketContains".Translate() + ": " + innerContainer.ContentsString.CapitalizeFirst();
            if (autoDeployEnabled)
            {
                text += "\n" + "FFF.AutoDeployEnabled".Translate();
            }
            return text;
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            innerContainer?.ClearAndDestroyContents();
            for (int i = 0; i < spawnedPawns.Count; i++)
            {
                if (!spawnedPawns[i].Dead)
                {
                    spawnedPawns[i].Kill(null, null);
                }
            }
        }

        public void Retracted(Pawn pawn)
        {
            spawnedPawns.Remove(pawn);
            int stackCount = Mathf.Min(Props.costPerPawn, Props.fixedIngredient.stackLimit);
            Thing thing = ThingMaker.MakeThing(Props.fixedIngredient);
            thing.stackCount = stackCount;
            if (innerContainer.TotalStackCountOfDef(Props.fixedIngredient) + stackCount <= Props.maxIngredientCount)
            {
                innerContainer.TryAdd(thing, thing.stackCount);
            }
            else
            {
                GenPlace.TryPlaceThing(thing, parent.Position, parent.Map, ThingPlaceMode.Near);
            }
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            if (!Find.Selector.IsSelected(parent))
            {
                return;
            }
            

            for (int i = 0; i < spawnedPawns.Count; i++)
            {
                if (!spawnedPawns[i].Dead && spawnedPawns[i].Spawned)
                {
                    GenDraw.DrawLineBetween(parent.TrueCenter(), spawnedPawns[i].TrueCenter());
                }
            }
        }
        protected void CleanupSpawnedPawns()
        {
            // 清理失效的生成單位
            List<Pawn> pawns = spawnedPawns;
            if (pawns == null || pawns.Count <= 0) return;
            for (int i = pawns.Count - 1; i >= 0; i--)
            {
                var pawn = pawns[i];
                if (pawn == null || pawn.Dead)
                {
                    pawns.RemoveAt(i);
                }
            }
            spawnedPawns = pawns;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Values.Look(ref cooldownTicksRemaining, "cooldownTicksRemaining", 0);
            Scribe_Values.Look(ref autoDeployTicks, "autoDeployTicks", 0);
            Scribe_Values.Look(ref autoDeployEnabled, "autoDeployEnabled", false);
            Scribe_Values.Look(ref maxToFill, "maxToFill", 0);
            Scribe_Values.Look(ref selectedAreaId, "selectedAreaId", -1);
            Scribe_Collections.Look(ref spawnedPawns, "spawnedPawns", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                spawnedPawns.RemoveAll(x => x == null);
            }
        }
        public override void CompTick()
        {
            CompTickInterval(1);
        }
        public override void CompTickRare()
        {
            CompTickInterval(250);
        }

        private int autoDeployTicks = 0;
        private bool autoDeployEnabled = false;
        public override void CompTickInterval(int delta)
        {
            // 快速檢查是否需要處理冷卻
            if (cooldownTicksRemaining > 0)
            {
                cooldownTicksRemaining -= delta;
                return;
            }
            // 自動部署邏輯
            if (autoDeployEnabled)
            {
                if (autoDeployTicks > 0)
                {
                    autoDeployTicks -= delta;
                    return;
                }
                // 檢查是否可以生成新單位
                if (spawnedPawns != null && spawnedPawns.Count < Props.maxPawnsToSpawn * 2 && CanSpawn.Accepted)
                {
                    autoDeployTicks = Props.cooldownTicks * 2; // 使用整數乘法代替浮點數
                    TrySpawnPawns();
                }
            }
            CleanupSpawnedPawns();
        }
        public void ReleaseOverFilled()
        {
            if (innerContainer.Count <= Props.maxIngredientCount)
            {
                return;
            }
            int excess = innerContainer.Count - Props.maxIngredientCount;
            while (excess > 0)
            {
                Thing t = null;
                if (excess > innerContainer[0].stackCount)
                {
                    t = innerContainer[0];
                    excess -= innerContainer[0].stackCount;
                }
                else
                { 
                    t = innerContainer[0].SplitOff(excess);
                    excess = 0;
                }
                innerContainer.TryDrop(t, ThingPlaceMode.Near, out var _);
            }
        }
    }

    public class CompProperties_MechPlatform : CompProperties
    {
        [NoTranslate]
        public string gizmoIconPath = "UI/Gizmos/ReleaseWarUrchins";

        [NoTranslate]
        public string gizmoIconPath_Retract = "UI/Drone_Retract";

        public ThingDef fixedIngredient;

        public int costPerPawn;

        public int maxIngredientCount;

        public int startingIngredientCount;

        public PawnKindDef spawnPawnKind;

        public int cooldownTicks = 900;

        public int maxPawnsToSpawn = 3;

        public EffecterDef spawnEffecter;

        public EffecterDef spawnedMechEffecter;

        public bool attachSpawnedEffecter;

        public bool attachSpawnedMechEffecter;

        public CompProperties_MechPlatform()
        {
            compClass = typeof(CompMechPlatform);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string item in base.ConfigErrors(parentDef))
            {
                yield return item;
            }
        }
    }
}