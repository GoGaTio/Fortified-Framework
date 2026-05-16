using RimWorld;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
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
    public class CompAmmoSwitch : ThingComp
    {
        private int selectedIndex;
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
                int idx = Mathf.Clamp(selectedIndex, 0, Props.ammos.Count - 1);
                return Props.ammos[idx];
            }
        }

        public ThingDef CurrentProjectile => CurrentAmmo?.projectileDef;
        public string CurrentLabel => CurrentAmmo?.ResolveLabel() ?? "N/A";
        public Texture2D CurrentIcon => CurrentAmmo?.ResolveIcon() ?? BaseContent.BadTex;

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
            if (CurrentAmmo.accuracyFactor != 1f && stat.defName.StartsWith("Accuracy"))
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
            if (CurrentAmmo.accuracyFactor != 1f && stat.defName.StartsWith("Accuracy"))
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

            int clamped = Mathf.Clamp(index, 0, Props.ammos.Count - 1);
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
            AmmoOption ammo = GetAmmoAt(index);
            if (ammo == null) return "N/A";

            string projText = ammo.projectileDef != null
                ? ammo.projectileDef.LabelCap
                : "N/A";

            return "FFF.AmmoSwitch.AmmoTooltip".Translate(ammo.ResolveLabel(), projText);
        }

        public string GetGizmoDesc()
        {
            var sb = new StringBuilder();
            sb.AppendLine("FFF.AmmoSwitch.Desc".Translate(CurrentLabel));
            sb.AppendLine(CurrentAmmo.description);

            return sb.ToString().TrimEnd();
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            if (HasAnyAmmoOption)
                selectedIndex = Mathf.Clamp(Props.defaultIndex, 0, Props.ammos.Count - 1);
            else
                selectedIndex = 0;
        }

        public virtual Gizmo GetSwitchGizmo(Thing user)
        {
			Command_Action command = new Command_Action
			{
				defaultLabel = "FFF.AmmoSwitch.Label".Translate(CurrentLabel),//$"彈種: {comp.CurrentLabel}",
				defaultDesc = GetGizmoDesc(),//comp.GetGizmoDesc() + "\n\n左鍵：查看目前投射物資訊卡",
				icon = CurrentIcon
			};
            command.action = delegate
            {
				List<FloatMenuOption> list = new List<FloatMenuOption>();
				for (int i = 0; i < OptionCount; i++)
				{
					int idx = i;
					AmmoOption ammo = GetAmmoAt(idx);
					if (ammo == null) continue;
					string label = ammo.ResolveLabel();
					Texture2D icon = ammo.ResolveIcon();
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
					}, icon, Color.white, extraPartWidth: 29f, extraPartOnGUI: (Rect r) => Widgets.InfoCardButton(r.x + 5f, r.y + (r.height - 24f) / 2f, ammo.projectileDef));
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
            int defaultIdx = Props?.defaultIndex ?? 0;
            Scribe_Values.Look(ref selectedIndex, "selectedIndex", defaultIdx);
            Scribe_Values.Look(ref cooldownUntilTick, "cooldownUntilTick", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && HasAnyAmmoOption)
                selectedIndex = Mathf.Clamp(selectedIndex, 0, Props.ammos.Count - 1);
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