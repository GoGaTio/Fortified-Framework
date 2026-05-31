using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Fortified
{
    // CompProperties
    public class CompProperties_AmmoSwitch : CompProperties
    {
        public List<AmmoOption> ammos = new List<AmmoOption>();
        public int defaultIndex = 0;
        public int switchCooldown = 90;
        public SoundDef soundSwitch;

        public CompProperties_AmmoSwitch()
        {
            compClass = typeof(CompAmmoSwitch);
        }
    }
	public class CompAmmoSwitch: ThingComp
	{
		private int selectedIndex;
		/// <summary>
		/// Stores the target ammo index when a pawn is switching ammo via job.
		/// Reset after job completion or can be checked by job driver.
		/// </summary>
		private int switchingToIndex;

		private int cooldownUntilTick;

        public CompProperties_AmmoSwitch Props => (CompProperties_AmmoSwitch)props;

        public bool HasAnyAmmoOption => Props?.ammos != null && Props.ammos.Count > 0;
        public int OptionCount => HasAnyAmmoOption ? Props.ammos.Count : 0;
        public int SelectedIndex => selectedIndex;

		public int SwitchingToIndex => switchingToIndex;

		public AmmoOption CurrentAmmo
        {
            get
            {
                if (!HasAnyAmmoOption) return null;
                // -1 means using the weapon's verb default projectile (no AmmoOption)
                if (selectedIndex < 0) return null;
                int idx = Mathf.Clamp(selectedIndex, 0, Props.ammos.Count - 1);
                return Props.ammos[idx];
            }
        }

        public override IEnumerable<FloatMenuOption> CompMultiSelectFloatMenuOptions(IEnumerable<Pawn> selPawns)
        {
            if (!HasAnyAmmoOption) yield break;

            // collect selected comps of same def
            List<CompAmmoSwitch> selectedComps = new List<CompAmmoSwitch>();
            foreach (object obj in Find.Selector.SelectedObjects)
            {
                if (obj is Thing t && t.def == parent.def)
                {
                    var c = t.TryGetComp<CompAmmoSwitch>();
                    if (c != null) selectedComps.Add(c);
                }
            }
            if (selectedComps.Count == 0) yield break;

            // if selPawns provided, map each comp to first pawn that can reach it
            Dictionary<CompAmmoSwitch, Pawn> compPawnMap = new Dictionary<CompAmmoSwitch, Pawn>();
            List<Pawn> selPawnList = selPawns?.ToList() ?? new List<Pawn>();
            if (selPawnList.Any())
            {
                foreach (var c in selectedComps)
                {
                    Pawn found = selPawnList.FirstOrDefault(p => p.CanReach(c.parent, PathEndMode.Touch, Danger.Deadly));
                    if (found != null) compPawnMap[c] = found;
                }
                if (compPawnMap.Count == 0)
                {
                    yield return new FloatMenuOption("CannotSwitchAmmo".Translate(parent.Label) + ": " + "NoPath".Translate().CapitalizeFirst(), null);
                    yield break;
                }
            }

            string label = "FFF.AmmoSwitch.Label".Translate(CurrentLabel);
            yield return new FloatMenuOption(label, delegate
            {
                List<FloatMenuOption> list = new List<FloatMenuOption>();

                var baseVerb = parent?.GetComp<CompEquippable>()?.AllVerbs?.FirstOrDefault() as Verb_LaunchProjectile;
                ThingDef baseProjectile = baseVerb?.Projectile;

                // default projectile option
                list.Add(new FloatMenuOption("FFF.AmmoSwitch.DefaultAmmo".Translate(), delegate
                {
                    if (compPawnMap.Count > 0)
                    {
                        foreach (var kv in compPawnMap)
                        {
                            kv.Key.QueueSwitchJob(kv.Value, -1);
                        }
                    }
                    else
                    {
                        foreach (var c in selectedComps) c.SetAmmo(-1, startCooldown: true);
                    }
                }, baseProjectile?.uiIcon ?? BaseContent.BadTex, Color.white, extraPartWidth: 29f, extraPartOnGUI: (Rect r) => Widgets.InfoCardButton(r.x + 5f, r.y + (r.height - 24f) / 2f, baseProjectile)));

                for (int i = 0; i < OptionCount; i++)
                {
                    int idx = i;
                    AmmoOption ammo = GetAmmoAt(idx);
                    if (ammo == null) continue;
                    ThingDef projectileForCard = ammo.useDefaultProjectile ? baseProjectile : ammo.projectileDef;
                    list.Add(new FloatMenuOption(ammo.ResolveLabel(), delegate
                    {
                        if (compPawnMap.Count > 0)
                        {
                            foreach (var kv in compPawnMap)
                            {
                                kv.Key.QueueSwitchJob(kv.Value, idx);
                            }
                        }
                        else
                        {
                            foreach (var c in selectedComps) c.SetAmmo(idx, startCooldown: true);
                        }
                    }, ammo.ResolveIcon(), Color.white, extraPartWidth: 29f, extraPartOnGUI: (Rect r) => Widgets.InfoCardButton(r.x + 5f, r.y + (r.height - 24f) / 2f, projectileForCard)));
                }

                Find.WindowStack.Add(new FloatMenu(list));
            }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
        }

        public void QueueSwitchJob(Pawn pawn, int idx)
        {
            if (pawn == null || parent == null) return;
            switchingToIndex = idx;
            pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(FFF_DefOf.FFF_SwitchAmmo, parent), JobTag.Misc);
        }

        public ThingDef CurrentProjectile
        {
            get
            {
                if (CurrentAmmo == null) return null;
                // If this ammo option uses default projectile, return null to let Verb use base projectile
                if (CurrentAmmo.useDefaultProjectile) return null;
                return CurrentAmmo.projectileDef;
            }
        }

        /// <summary>
        /// Gets whether the current ammo option uses the weapon's default projectile.
        /// </summary>
        public bool IsUsingDefaultProjectile
        {
            get
            {
                // If selectedIndex == -1 we are explicitly using the verb's default projectile
                if (selectedIndex < 0) return true;
                return CurrentAmmo?.useDefaultProjectile ?? false;
            }
        }
        public string CurrentLabel
        {
            get
            {
                if (selectedIndex < 0)
                {
                    return "FFF.AmmoSwitch.DefaultAmmo".Translate();
                }
                return CurrentAmmo?.ResolveLabel() ?? "N/A";
            }
        }
        public Texture2D CurrentIcon
        {
            get
            {
                if (selectedIndex < 0)
                {
                    // Try to use the base verb projectile icon if available
                    var verb = parent?.GetComp<CompEquippable>()?.AllVerbs?.FirstOrDefault() as Verb_LaunchProjectile;
                    return verb?.Projectile?.uiIcon ?? BaseContent.BadTex;
                }
                return CurrentAmmo?.ResolveIcon() ?? BaseContent.BadTex;
            }
        }

        public bool IsOnSwitchCooldown
        {
            get
            {
                if (Find.TickManager == null) return false;
                return Find.TickManager.TicksGame < cooldownUntilTick;
            }
        }

		public override float GetStatFactor(StatDef stat)
		{
			if (CurrentAmmo != null && !Mathf.Approximately(CurrentAmmo.accuracyFactor, 1f) && stat.defName.StartsWith("Accuracy"))
			{
				return CurrentAmmo.accuracyFactor;
			}
			return base.GetStatFactor(stat);
		}
        public void PlaySound(SoundInfo soundInfo)
        {
            if (Props?.soundSwitch != null)
            {
                Props.soundSwitch.PlayOneShot(soundInfo);
            }
            else
            {
                parent.def.soundInteract?.PlayOneShot(soundInfo);
            }
        }
		public override void GetStatsExplanation(StatDef stat, StringBuilder sb, string whitespace = "")
		{
			if (CurrentAmmo != null && !Mathf.Approximately(CurrentAmmo.accuracyFactor, 1f) && stat.defName.StartsWith("Accuracy"))
			{
				sb.AppendLine();
				sb.AppendLine(whitespace + "FFF.AmmoSwitch.StatFactor".Translate() + ": x" + CurrentAmmo.accuracyFactor.ToStringByStyle(ToStringStyle.PercentZero));
			}
		}

        public AmmoOption GetAmmoAt(int index)
        {
            if (!HasAnyAmmoOption) return null;
            if (index < 0 || index >= Props.ammos.Count) return null;
            return Props.ammos[index];
        }

        public void SetAmmo(int index, bool startCooldown = true)
        {
            if (!HasAnyAmmoOption) return;

            // allow -1 to represent 'use verb default projectile'
            int clamped = Mathf.Clamp(index, -1, Props.ammos.Count - 1);
            bool changed = clamped != selectedIndex;
            selectedIndex = clamped;

            if (changed && startCooldown)
            {
                if (Props.switchCooldown > 0 && Find.TickManager != null)
                    cooldownUntilTick = Find.TickManager.TicksGame + Props.switchCooldown;
            }
        }

        public string GetAmmoTooltip(int index)
        {
            // Special-case for -1: represent using the weapon/verb default projectile
            if (index < 0)
            {
                var verb = parent?.GetComp<CompEquippable>()?.AllVerbs?.FirstOrDefault() as Verb_LaunchProjectile;
                string baseProjText = verb?.Projectile?.LabelCap ?? "FFF.AmmoSwitch.DefaultProjectile".Translate();
                string label = "FFF.AmmoSwitch.DefaultAmmo".Translate();
                return "FFF.AmmoSwitch.AmmoTooltip".Translate(label, baseProjText);
            }

            AmmoOption ammo = GetAmmoAt(index);
            if (ammo == null) return "N/A";

            string projText;
            if (ammo.useDefaultProjectile)
            {
                projText = "FFF.AmmoSwitch.DefaultProjectile".Translate();
            }
            else
            {
                projText = ammo.projectileDef != null
                    ? ammo.projectileDef.LabelCap
                    : "N/A";
            }

            return "FFF.AmmoSwitch.AmmoTooltip".Translate(ammo.ResolveLabel(), projText);
        }

        public string GetGizmoDesc()
        {
            var sb = new StringBuilder();
            // If explicitly using verb default (selectedIndex == -1), show a simple description
            if (selectedIndex < 0)
            {
                sb.AppendLine("FFF.AmmoSwitch.Desc".Translate(CurrentLabel));
                sb.AppendLine();
                sb.AppendLine("[" + "FFF.AmmoSwitch.UsingDefault".Translate() + "]");
                return sb.ToString().TrimEnd();
            }

            if (CurrentAmmo == null) return "N/A";
            sb.AppendLine("FFF.AmmoSwitch.Desc".Translate(CurrentLabel));
            sb.AppendLine(CurrentAmmo.description ?? "");

            // Add note if using default projectile (either via AmmoOption flag)
            if (IsUsingDefaultProjectile)
            {
                sb.AppendLine();
                sb.AppendLine("[" + "FFF.AmmoSwitch.UsingDefault".Translate() + "]");
            }

            return sb.ToString().TrimEnd();
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            if (HasAnyAmmoOption)
                selectedIndex = Mathf.Clamp(Props.defaultIndex, -1, Props.ammos.Count - 1);
            else
                selectedIndex = -1;
        }

		public virtual Gizmo GetSwitchGizmo(Thing user)
		{
			Command_Action command = new Command_Action
			{
				defaultLabel = "FFF.AmmoSwitch.Label".Translate(CurrentLabel),
				defaultDesc = GetGizmoDesc(),
				icon = CurrentIcon
			};
			command.action = delegate
			{
				List<FloatMenuOption> list = new List<FloatMenuOption>();
                // Add an option for using the weapon's base/verb default projectile
                var baseVerb = parent?.GetComp<CompEquippable>()?.AllVerbs?.FirstOrDefault() as Verb_LaunchProjectile;
                ThingDef baseProjectile = baseVerb?.Projectile;
                FloatMenuOption defaultOption = new FloatMenuOption("FFF.AmmoSwitch.DefaultAmmo".Translate(), delegate
                {
                    if (user is Pawn pawn)
                    {
                        switchingToIndex = -1;
                        pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(FFF_DefOf.FFF_SwitchAmmo, parent), JobTag.Misc);
                    }
                    else
                    {
                        SetAmmo(-1, startCooldown: true);
                    }
                }, baseProjectile?.uiIcon ?? BaseContent.BadTex, Color.white, extraPartWidth: 29f, extraPartOnGUI: (Rect r) => Widgets.InfoCardButton(r.x + 5f, r.y + (r.height - 24f) / 2f, baseProjectile));
                defaultOption.tooltip = new TipSignal(GetAmmoTooltip(-1));
                if (SelectedIndex == -1) defaultOption.Disabled = true;
                list.Add(defaultOption);
				for (int i = 0; i < OptionCount; i++)
				{
					int idx = i;
					AmmoOption ammo = GetAmmoAt(idx);
					if (ammo == null) continue;
					string label = ammo.ResolveLabel();
					Texture2D icon = ammo.ResolveIcon();

					// Get projectile for info card: use ammo's projectile if not default, otherwise try base verb projectile
					ThingDef projectileForCard = null;
					if (!ammo.useDefaultProjectile)
					{
						projectileForCard = ammo.projectileDef;
					}
					else
					{
						// Try to get base projectile from parent weapon's verb
						var verb = parent?.GetComp<CompEquippable>()?.AllVerbs?.FirstOrDefault() as Verb_LaunchProjectile;
						if (verb != null)
						{
							projectileForCard = verb.Projectile;
						}
					}

					FloatMenuOption option = new FloatMenuOption(label, delegate
					{
						if(user is Pawn pawn)
						{
							switchingToIndex = idx;
							pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(FFF_DefOf.FFF_SwitchAmmo, parent), JobTag.Misc);
						}
						else
						{
							SetAmmo(idx, startCooldown: true);
						}
					}, icon, Color.white, extraPartWidth: 29f, extraPartOnGUI: (Rect r) => Widgets.InfoCardButton(r.x + 5f, r.y + (r.height - 24f) / 2f, projectileForCard));
					option.tooltip = new TipSignal(GetAmmoTooltip(idx));
					if (idx == SelectedIndex)
					{
						option.Disabled = true;
					}
					list.Add(option);
				}
				Find.WindowStack.Add(new FloatMenu(list));
			};
			return command;
		}

        public override void PostExposeData()
        {
            base.PostExposeData();
            int defaultIdx = Props?.defaultIndex ?? -1;
            Scribe_Values.Look(ref selectedIndex, "selectedIndex", defaultIdx);
            Scribe_Values.Look(ref cooldownUntilTick, "cooldownUntilTick", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && HasAnyAmmoOption)
                selectedIndex = Mathf.Clamp(selectedIndex, -1, Props.ammos.Count - 1);
        }
    }

	public class CompProperties_SwitchableAmmo : CompProperties
	{
		public CompProperties_SwitchableAmmo()
		{
			compClass = typeof(CompSwitchableAmmo);
		}

		public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		{
            ThingDef def = req.Thing?.def ?? (req.Def as ThingDef);
			if(def == null || def.projectile == null)
            {
                yield break;
            }
			StatCategoryDef statCat = StatCategoryDefOf.Weapon_Ranged;
			if (def.projectile.damageDef != null && def.projectile.damageDef.harmsHealth)
			{
				StringBuilder stringBuilder2 = new StringBuilder();
				stringBuilder2.AppendLine("Stat_Thing_Damage_Desc".Translate());
				stringBuilder2.AppendLine();
				float num3 = def.projectile.GetDamageAmount(req.Thing, stringBuilder2);
				yield return new StatDrawEntry(statCat, "Damage".Translate(), num3.ToString(), stringBuilder2.ToString(), 5500);
				if (def.projectile.damageDef.armorCategory != null)
				{
					StringBuilder stringBuilder3 = new StringBuilder();
					float armorPenetration = def.projectile.GetArmorPenetration(req.Thing, stringBuilder3);
					TaggedString taggedString = "ArmorPenetrationExplanation".Translate();
					if (stringBuilder3.Length != 0)
					{
						taggedString += "\n\n" + stringBuilder3;
					}
					yield return new StatDrawEntry(statCat, "ArmorPenetration".Translate(), armorPenetration.ToStringPercent(), taggedString, 5400);
				}
				float buildingDamageFactor = def.projectile.damageDef.buildingDamageFactor;
				float dmgBuildingsImpassable = def.projectile.damageDef.buildingDamageFactorImpassable;
				float dmgBuildingsPassable = def.projectile.damageDef.buildingDamageFactorPassable;
				if (buildingDamageFactor != 1f)
				{
					yield return new StatDrawEntry(statCat, "BuildingDamageFactor".Translate(), buildingDamageFactor.ToStringPercent(), "BuildingDamageFactorExplanation".Translate(), 5410);
				}
				if (dmgBuildingsImpassable != 1f)
				{
					yield return new StatDrawEntry(statCat, "BuildingDamageFactorImpassable".Translate(), dmgBuildingsImpassable.ToStringPercent(), "BuildingDamageFactorImpassableExplanation".Translate(), 5420);
				}
				if (dmgBuildingsPassable != 1f)
				{
					yield return new StatDrawEntry(statCat, "BuildingDamageFactorPassable".Translate(), dmgBuildingsPassable.ToStringPercent(), "BuildingDamageFactorPassableExplanation".Translate(), 5430);
				}
			}
			float stoppingPower = def.projectile.stoppingPower;
			if (stoppingPower > 0f)
			{
				StringBuilder stoppingPowerExplanation = new StringBuilder("StoppingPowerExplanation".Translate());
				stoppingPowerExplanation.AppendLine();
				stoppingPowerExplanation.AppendLine();
				stoppingPowerExplanation.AppendLine("StatsReport_BaseValue".Translate() + ": " + stoppingPower.ToString("F1"));
				stoppingPowerExplanation.AppendLine();
				stoppingPowerExplanation.AppendLine();
				stoppingPowerExplanation.AppendLine("StatsReport_FinalValue".Translate() + ": " + stoppingPower.ToString("F1"));
				yield return new StatDrawEntry(statCat, "StoppingPower".Translate(), stoppingPower.ToString("F1"), stoppingPowerExplanation.ToString(), 5402);
			}
		}
	}
	public class CompSwitchableAmmo : ThingComp
	{
	}

	/*public class Command_AmmoSwitch : Command_Action
    {
        public CompAmmoSwitch comp;
        public LocalTargetInfo messageTarget;

        public void OpenCurrentProjectileInfoCard()
        {
            if (comp?.CurrentProjectile == null)
            {
                Messages.Message("目前彈種未設定投射物。", MessageTypeDefOf.RejectInput, false);
                return;
            }

            Find.WindowStack.Add(new Dialog_InfoCard(comp.CurrentProjectile));
        }

        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get
            {
                if (comp == null || !comp.HasAnyAmmoOption) yield break;

                for (int i = 0; i < comp.OptionCount; i++)
                {
                    int idx = i;
                    AmmoOption ammo = comp.GetAmmoAt(idx);

                    if (ammo == null) continue;
                    if (idx == comp.SelectedIndex) continue;

                    string label = ammo.ResolveLabel();

                    FloatMenuOption opt;
                    Texture2D icon = ammo.ResolveIcon();
                    if (icon != null)
                    {
                        opt = new FloatMenuOption(label, () => SelectAmmo(idx), icon, Color.white);
                    }
                    else
                    {
                        opt = new FloatMenuOption(label, () => SelectAmmo(idx));
                    }

                    opt.tooltip = new TipSignal(comp.GetAmmoTooltip(idx));
                    yield return opt;
                }
            }
        }

        private void SelectAmmo(int index)
        {
            comp.SetAmmo(index, startCooldown: true);
        }
    }*/
}