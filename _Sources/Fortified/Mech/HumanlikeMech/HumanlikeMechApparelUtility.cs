using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
	
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;

namespace Fortified
{
    delegate bool AvailableFunc(Verb verb, DefModExtension extension);

    public static class HumanlikeMechApparelUtility
    {
        private static NeededWarmth neededWarmth;
        private static StringBuilder debugSb;
        private readonly static List<float> wornApparelScores = new List<float>();
        private readonly static SimpleCurve InsulationColdScoreFactorCurve_NeedWarm = new SimpleCurve
        {
            new CurvePoint(0f, 1f),
            new CurvePoint(30f, 8f)
        };

        private readonly static SimpleCurve HitPointsPercentScoreFactorCurve = new SimpleCurve
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(0.2f, 0.2f),
            new CurvePoint(0.22f, 0.3f),
            new CurvePoint(0.5f, 0.3f),
            new CurvePoint(0.52f, 1f)
        };

        public static void TryOptimizeApparel(this Pawn pawn)
        {
            Job job = pawn.TryMakeJob_OptimizeApparel();
            if(job != null) pawn.jobs.StartJob(job);
        }

        public static Job TryMakeJob_OptimizeApparel(this Pawn pawn)
        {
            if (pawn.outfits == null)
            {
                Log.ErrorOnce(string.Concat(pawn, " tried to run JobGiver_OptimizeApparel without an OutfitTracker"), 5643897);
                return null;
            }

            if (pawn.Faction != Faction.OfPlayer)
            {
                //Log.ErrorOnce(string.Concat("Non-colonist ", pawn, " tried to optimize apparel."), 764323);
                return null;
            }

            if (pawn.IsQuestLodger())
            {
                return null;
            }

            if (!DebugViewSettings.debugApparelOptimize)
            {
                if (Find.TickManager.TicksGame < pawn.mindState.nextApparelOptimizeTick)
                {
                    return null;
                }
            }
            else
            {
                debugSb = new StringBuilder();
                debugSb.AppendLine(string.Concat("Scanning for ", pawn, " at ", pawn.Position));
            }

            ApparelPolicy currentOutfit = pawn.outfits.CurrentApparelPolicy;
            List<Apparel> wornApparel = pawn.apparel.WornApparel;
            for (int i = wornApparel.Count - 1; i >= 0; i--)
            {
                if (!currentOutfit.filter.Allows(wornApparel[i]) && pawn.outfits.forcedHandler.AllowedToAutomaticallyDrop(wornApparel[i]) && !pawn.apparel.IsLocked(wornApparel[i]))
                {
                    Job jobRemoveApparel = JobMaker.MakeJob(JobDefOf.RemoveApparel, wornApparel[i]);
                    jobRemoveApparel.haulDroppedApparel = true;
                    return jobRemoveApparel;
                }
            }
            

            Thing thing = null;
            float minScore = 0f;
            List<Thing> list = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel);
            if (list.Count == 0)
            {
                SetNextOptimizeTick(pawn);
                return null;
            }

            neededWarmth = MechApparelGenerator.CalculateNeededWarmth(pawn, pawn.Map.Tile, GenLocalDate.Twelfth(pawn));
            wornApparelScores.Clear();
            for (int i = 0; i < wornApparel.Count; i++)
            {
                wornApparelScores.Add(ApparelScoreRaw(pawn, wornApparel[i]));
            }

            for (int j = 0; j < list.Count; j++)
            {
                Apparel apparel = (Apparel)list[j];
                if (currentOutfit.filter.Allows(apparel) && apparel.IsInAnyStorage() && !apparel.IsForbidden(pawn) && !apparel.IsBurning() && (apparel.def.apparel.gender == Gender.None || apparel.def.apparel.gender == pawn.gender))
                {
                    float score = ApparelScoreGain(pawn, apparel, wornApparelScores);
                    if (DebugViewSettings.debugApparelOptimize)
                    {
                        debugSb.AppendLine(apparel.LabelCap + ": " + score.ToString("F2"));
                    }

                    if (!(score < 0.05f) && !(score < minScore) && (!CompBiocodable.IsBiocoded(apparel) || CompBiocodable.IsBiocodedFor(apparel, pawn)) && ApparelUtility.HasPartsToWear(pawn, apparel.def) && pawn.CanReserveAndReach(apparel, PathEndMode.OnCell, pawn.NormalMaxDanger()) && apparel.def.apparel.developmentalStageFilter.Has(pawn.DevelopmentalStage))
                    {
                        thing = apparel;
                        minScore = score;
                    }
                }
            }

            if (DebugViewSettings.debugApparelOptimize)
            {
                debugSb.AppendLine("BEST: " + thing);
                Log.Message(debugSb.ToString());
                debugSb = null;
            }

            if (thing == null)
            {
                SetNextOptimizeTick(pawn);
                return null;
            }

            return JobMaker.MakeJob(JobDefOf.Wear, thing);
        }

        public static void SetNextOptimizeTick(Pawn pawn)
        {
            pawn.mindState.nextApparelOptimizeTick = Find.TickManager.TicksGame + Rand.Range(6000, 9000);
        }

       

        public static float ApparelScoreGain(Pawn pawn, Apparel ap, List<float> wornScoresCache)
        {
            if (ap.def == RimWorld.ThingDefOf.Apparel_ShieldBelt && pawn.equipment.Primary != null && pawn.equipment.Primary.def.IsWeaponUsingProjectiles)
            {
                return -1000f;
            }

            if (ap.def.apparel.ignoredByNonViolent && pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return -1000f;
            }

            float num = ApparelScoreRaw(pawn, ap);
            List<Apparel> wornApparel = pawn.apparel.WornApparel;
            bool flag = false;
            for (int i = 0; i < wornApparel.Count; i++)
            {
                if (!ApparelUtility.CanWearTogether(wornApparel[i].def, ap.def, pawn.RaceProps.body))
                {
                    if (!pawn.outfits.forcedHandler.AllowedToAutomaticallyDrop(wornApparel[i]) || pawn.apparel.IsLocked(wornApparel[i]))
                    {
                        return -1000f;
                    }

                    num -= wornScoresCache[i];
                    flag = true;
                }
            }

            if (!flag)
            {
                num *= 10f;
            }

            return num;
        }

        public static float ApparelScoreRaw(Pawn pawn, Apparel ap)
        {
            if (!ap.PawnCanWear(pawn, ignoreGender: true))
            {
                return -10f;
            }

            if (ap.def.apparel.blocksVision)
            {
                return -10f;
            }

            if (ap.def.apparel.slaveApparel && !pawn.IsSlave)
            {
                return -10f;
            }

            if (ap.def.apparel.mechanitorApparel && pawn.mechanitor == null)
            {
                return -10f;
            }

            float num = 0.1f + ap.def.apparel.scoreOffset;
            float num2 = ap.GetStatValue(StatDefOf.ArmorRating_Sharp) + ap.GetStatValue(StatDefOf.ArmorRating_Blunt);
            num += num2;
            if (ap.def.useHitPoints)
            {
                float x = (float)ap.HitPoints / (float)ap.MaxHitPoints;
                num *= HitPointsPercentScoreFactorCurve.Evaluate(x);
            }

            num += ap.GetSpecialApparelScoreOffset();
            float num3 = 1f;
            if (neededWarmth == NeededWarmth.Warm)
            {
                float statValue = ap.GetStatValue(StatDefOf.Insulation_Cold);
                num3 *= InsulationColdScoreFactorCurve_NeedWarm.Evaluate(statValue);
            }

            num *= num3;
            if (ap.WornByCorpse && (pawn == null || ThoughtUtility.CanGetThought(pawn, ThoughtDefOf.DeadMansApparel, checkIfNullified: true)))
            {
                num -= 0.5f;
                if (num > 0f)
                {
                    num *= 0.1f;
                }
            }

            if (ap.Stuff == RimWorld.ThingDefOf.Human.race.leatherDef)
            {
                if (pawn.Ideo != null && pawn.Ideo.LikesHumanLeatherApparel)
                {
                    num += 0.12f;
                }
                else
                {
                    if (pawn == null || ThoughtUtility.CanGetThought(pawn, ThoughtDefOf.HumanLeatherApparelSad, checkIfNullified: true))
                    {
                        num -= 0.5f;
                        if (num > 0f)
                        {
                            num *= 0.1f;
                        }
                    }

                    if (pawn != null && ThoughtUtility.CanGetThought(pawn, ThoughtDefOf.HumanLeatherApparelHappy, checkIfNullified: true))
                    {
                        num += 0.12f;
                    }
                }
            }

            if (pawn != null && !ap.def.apparel.CorrectGenderForWearing(pawn.gender))
            {
                num *= 0.01f;
            }

            bool flag = false;
            if (pawn != null)
            {
                foreach (ApparelRequirementWithSource allRequirement in pawn.apparel.AllRequirements)
                {
                    foreach (BodyPartGroupDef item in allRequirement.requirement.bodyPartGroupsMatchAny)
                    {
                        if (ap.def.apparel.bodyPartGroups.Contains(item))
                        {
                            flag = true;
                            break;
                        }
                    }

                    if (flag)
                    {
                        break;
                    }
                }
            }

            if (flag)
            {
                bool flag2 = false;
                bool flag3 = false;
                foreach (ApparelRequirementWithSource allRequirement2 in pawn.apparel.AllRequirements)
                {
                    if (allRequirement2.requirement.RequiredForPawn(pawn, ap.def))
                    {
                        flag2 = true;
                    }

                    if (allRequirement2.requirement.AllowedForPawn(pawn, ap.def))
                    {
                        flag3 = true;
                    }
                }

                if (flag2)
                {
                    num *= 25f;
                }
                else if (flag3)
                {
                    num *= 10f;
                }
            }

            if (pawn != null && pawn.royalty != null && pawn.royalty.AllTitlesInEffectForReading.Count > 0)
            {
                QualityCategory qualityCategory = QualityCategory.Awful;
                foreach (RoyalTitle item2 in pawn.royalty.AllTitlesInEffectForReading)
                {
                    if ((int)item2.def.requiredMinimumApparelQuality > (int)qualityCategory)
                    {
                        qualityCategory = item2.def.requiredMinimumApparelQuality;
                    }
                }

                if (ap.TryGetQuality(out var qc) && (int)qc < (int)qualityCategory)
                {
                    num *= 0.25f;
                }
            }

            return num;
        }

    }
}
