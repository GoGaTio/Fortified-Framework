using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Fortified
{
    public class CompAbilityEffect_ActiveProtectionSystem : CompAbilityEffect
    {
        #region 反射缓存
        private static readonly FieldInfo s_originField =
            typeof(Projectile).GetField("origin", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_destField =
            typeof(Projectile).GetField("destination", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo s_explodeMethod =
            typeof(Projectile_Explosive).GetMethod("Explode", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly object[] s_emptyArgs = new object[0];
        #endregion

        #region 字段
        private new CompProperties_ActiveProtectionSystem Props => (CompProperties_ActiveProtectionSystem)props;
        private Pawn Pawn => parent.pawn;
        private bool isActive;
        protected int tickRemain;
        protected int interceptCount;
        #endregion

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            tickRemain = Props.activeTicks;
            isActive = true;
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            if (Pawn.Faction == Faction.OfPlayer) return false;
            if (!parent.CanCast || parent.Casting) return false;

            Map map = Pawn.Map;
            ThingGrid thingGrid = map.thingGrid;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(Pawn).ExpandedBy(Props.Radius).ClipInsideMap(map))
            {
                List<Thing> things = thingGrid.ThingsListAt(cell);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i] is Projectile && IsTargetProjectile(things[i]))
                        return true;
                }
            }
            return false;
        }

        public override void CompTick()
        {
            if (!isActive) return;

            // 缓存常用值
            Pawn pawn = Pawn;
            Map map = pawn.MapHeld;
            Faction pawnFaction = pawn.Faction;
            ThingGrid thingGrid = map.thingGrid;
            Vector3 pawnDrawPos = pawn.DrawPos;
            int radius = Props.Radius;

            interceptCount = 0;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(pawn).ExpandedBy(radius).ClipInsideMap(map))
            {
                List<Thing> things = thingGrid.ThingsListAt(cell);
                for (int i = 0; i < things.Count; i++)
                {
                    if (interceptCount >= Props.interceptCountMax) return;

                    Thing thing = things[i];
                    if (!(thing is Projectile proj)) continue;
                    if (proj.Launcher?.Faction == pawnFaction) continue;
                    if (!IsTargetProjectile(thing)) continue;
                    if (!IsInBound(proj, pawnDrawPos, radius)) continue;

                    interceptCount++;
                    if (Rand.Range(0f, 1f) <= Props.chanceToFail) continue;

                    // 拦截特效
                    Vector3 pos = thing.DrawPos;
                    FleckMaker.Static(pawnDrawPos + Rand.UnitVector3, map, FleckDefOf.ShotFlash, 3f);
                    if (Props.fleckDef != null)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            FleckCreationData dataStatic = FleckMaker.GetDataStatic(pawnDrawPos, map, Props.fleckDef);
                            dataStatic.spawnPosition = Vector3.Lerp(pawnDrawPos, pos, 0.2f);
                            dataStatic.scale = Rand.Range(0.5f, 1);
                            dataStatic.solidTimeOverride = 0f;
                            var noise = Rand.Range(-15, 15);
                            dataStatic.rotation = (dataStatic.spawnPosition - pawnDrawPos).AngleFlat() + noise;
                            dataStatic.velocityAngle = (dataStatic.spawnPosition - pawnDrawPos).AngleFlat() + noise;
                            dataStatic.velocitySpeed = 50f;
                            map.flecks.CreateFleck(dataStatic);
                        }
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        float angle = (Vector3.Lerp(pawnDrawPos, pos, 0.2f) - pawnDrawPos).AngleFlat() + Rand.Range(-90f, 90f);
                        FleckCreationData dataStatic = FleckMaker.GetDataStatic(pawnDrawPos, map, FleckDefOf.AirPuff);
                        dataStatic.spawnPosition = pawnDrawPos + CircleConst.GetAngle(angle) * 2f;
                        dataStatic.scale = Rand.Range(1f, 4.9f);
                        dataStatic.rotationRate = Rand.Range(-30f, 30f) / dataStatic.scale;
                        dataStatic.velocityAngle = angle;
                        dataStatic.velocitySpeed = 5 - dataStatic.scale;
                        map.flecks.CreateFleck(dataStatic);
                    }
                    DoIntercept(proj, pawnDrawPos, map);
                }
            }
            tickRemain--;
            if (tickRemain <= 0) isActive = false;
        }

        public override string CompInspectStringExtra()
        {
            if (isActive)
                return "FFF.APS_TickRemain".Translate(tickRemain.TicksToSeconds());
            else
                return base.CompInspectStringExtra();
        }

        // 拦截处理
        private void DoIntercept(Projectile target, Vector3 pawnDrawPos, Map map)
        {
            if (target == null) return;

            if (Props.fleckDef != null)
            {
                if (pawnDrawPos.ShouldSpawnMotesAt(map, false))
                {
                    if (Props.spawnLeaving != null)
                    {
                        if (Rand.Range(0f, 1f) > 0.8f)
                            GenSpawn.Spawn(Props.spawnLeaving, target.Position, target.Map);
                    }

                    Vector3 originVector = (Vector3)s_originField.GetValue(target);
                    Vector3 destVector = (Vector3)s_destField.GetValue(target);
                    Vector3 velo = (destVector - originVector).normalized;

                    if (target is Projectile_Explosive)
                    {
                        s_explodeMethod.Invoke(target, s_emptyArgs);
                    }
                    for (int i = 0; i < 9; i++)
                    {
                        FleckCreationData dataStatic = FleckMaker.GetDataStatic(target.DrawPos, target.Map, Props.interceptedFleckDef);
                        dataStatic.scale = Rand.Range(2f, 5f);
                        var noise = Rand.Range(-15, 15);
                        dataStatic.spawnPosition = target.DrawPos + CircleConst.GetAngle(velo.AngleFlat() + noise) * -2f;
                        dataStatic.rotation = velo.AngleFlat() + noise;
                        dataStatic.velocityAngle = velo.AngleFlat() + noise;
                        dataStatic.velocitySpeed = Rand.Range(target.def.projectile.speed / 3, target.def.projectile.speed / 2) / dataStatic.scale;
                        map.flecks.CreateFleck(dataStatic);
                    }
                }
            }
            Props.soundIntercepted.PlayOneShot(new TargetInfo(Pawn.Position, map, false));
        }

        // 判断投射物目标是否在范围内
        private static bool IsInBound(Projectile target, Vector3 pawnDrawPos, int radius)
        {
            Vector3 destination = (Vector3)s_destField.GetValue(target);
            float dx = destination.x - pawnDrawPos.x;
            float dz = destination.z - pawnDrawPos.z;
            return dx * dx + dz * dz < radius * radius;
        }

        // 判断是否为可拦截的投射物
        private bool IsTargetProjectile(Thing target)
        {
            if (target is null) return false;

            string defName = target.def.defName;

            // 黑名單優先排除
            if (Props.ignoreThings.Contains(defName)) return false;

            // 爆炸性投射物直接攔截
            if (target is Projectile_Explosive) return true;

            // 白名單檢查（精確匹配）
            List<string> intercepts = Props.interceptThings;
            for (int i = 0; i < intercepts.Count; i++)
            {
                if (defName == intercepts[i]) return true;
            }

            // 關鍵字匹配（大小寫不敏感）
            string defNameLower = defName.ToLowerInvariant();
            if (defNameLower.Contains("rocket") || defNameLower.Contains("missile") || defNameLower.Contains("grenade"))
            {
                return true;
            }

            return false;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isActive, "isActive", false);
            Scribe_Values.Look(ref tickRemain, "tickRemain", 100);
            Scribe_Values.Look(ref interceptCount, "interceptCount", 0);
        }
    }

    public class CompProperties_ActiveProtectionSystem : CompProperties_AbilityEffect
    {
        public int Radius = 6;
        public FleckDef fleckDef;
        public ThingDef spawnLeaving;
        public FleckDef interceptedFleckDef;
        public SoundDef soundIntercepted;
        public float chanceToFail = 0.8f;
        public int activeTicks = 2400;
        /// <summary>每 tick 最多攔截數量，預設 2</summary>
        public int interceptCountMax = 2;
        public List<string> interceptThings = new List<string>();
        public List<string> ignoreThings = new List<string>();

        public CompProperties_ActiveProtectionSystem()
        {
            compClass = typeof(CompAbilityEffect_ActiveProtectionSystem);
        }
    }
}
