using RimWorld;
using UnityEngine;
using Verse;

namespace Fortified
{
    // 照明弹投射物两段式高空斜飞下坠至目标上空开伞悬浮燃烧
    public class Projectile_FFF_Flare : Projectile, IThingGlower
    {
        // 照明弹颜色
        public Color flareColor = new Color(1f, 0.95f, 0.6f, 1f);

        // 点亮持续时间
        public int igniteDurationTicks = 900;
        // 开伞悬浮高度
        public float igniteHoverHeight = 4.5f;
        // 缓降终点高度
        public float descendEndHeight = 0.4f;
        // 下坠起始高度
        public float descendStartHeight = 12f;
        // 下坠时长
        public int descendDurationTicks = 90;
        // 粒子发射间隔
        public int particleIntervalTicks = 5;
        // 烟雾尺寸
        public float smokeSize = 0.8f;
        // 火光尺寸
        public float glowSize = 1.6f;
        // 照明半径
        public float lightRadius = 14f;
        // 照明强度倍率
        public float lightIntensity = 1.5f;
        // 视觉尺寸倍率
        public float sizeScale = 1f;

        // 入射方向单位向量
        public Vector3 approachDir;
        private bool descending;
        private int descendTicksLeft;
        private bool ignited;
        private int igniteTicksLeft;
        private int particleAcc;

        // 兜底正北
        private void EnsureApproachDir()
        {
            if (approachDir.sqrMagnitude > 0.0001f) return;
            approachDir = Vector3.forward;
        }

        // 计算视觉锚点
        private Vector3 ResolveAnchor(float offset)
        {
            EnsureApproachDir();
            Vector3 anchor = destination + approachDir * offset;
            anchor.y = def.Altitude;
            if (Map != null)
            {
                anchor.x = Mathf.Clamp(anchor.x, 0.01f, Map.Size.x - 1.01f);
                anchor.z = Mathf.Clamp(anchor.z, 0.01f, Map.Size.z - 1.01f);
            }
            return anchor;
        }

        // 高空下坠位置
        protected Vector3 DescendingPos
        {
            get
            {
                float t = Mathf.Clamp01(1f - (float)descendTicksLeft / Mathf.Max(1, descendDurationTicks));
                float offset = Mathf.Lerp(descendStartHeight, igniteHoverHeight, t);
                return ResolveAnchor(offset);
            }
        }

        // 开伞悬浮缓降位置
        protected Vector3 IgnitedPos
        {
            get
            {
                float t = Mathf.Clamp01(1f - (float)igniteTicksLeft / Mathf.Max(1, igniteDurationTicks));
                float offset = Mathf.Lerp(igniteHoverHeight, descendEndHeight, t);
                return ResolveAnchor(offset);
            }
        }

        public override Vector3 ExactPosition
        {
            get
            {
                if (ignited) return IgnitedPos;
                if (descending) return DescendingPos;
                return base.ExactPosition;
            }
        }

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            // 跳过抛物飞行
            BeginDescend();
        }

        protected override void ImpactSomething()
        {
            // 兜底跳到点亮态
            if (!ignited) Ignite();
        }

        private void BeginDescend()
        {
            descending = true;
            descendTicksLeft = Mathf.Max(1, descendDurationTicks);
            if (DestinationCell.InBounds(Map)) Position = DestinationCell;
        }

        private void Ignite()
        {
            descending = false;
            ignited = true;
            igniteTicksLeft = igniteDurationTicks;
            if (DestinationCell.InBounds(Map)) Position = DestinationCell;
            RefreshGlower();
        }

        // 注入照明颜色与半径注册到glowGrid
        private void RefreshGlower()
        {
            if (!Spawned) return;
            var glower = GetComp<CompGlower>();
            if (glower == null) return;
            ColorInt c = new ColorInt(
                Mathf.Clamp(Mathf.RoundToInt(flareColor.r * 255f * lightIntensity), 0, 255),
                Mathf.Clamp(Mathf.RoundToInt(flareColor.g * 255f * lightIntensity), 0, 255),
                Mathf.Clamp(Mathf.RoundToInt(flareColor.b * 255f * lightIntensity), 0, 255),
                0);
            glower.GlowColor = c;
            glower.GlowRadius = lightRadius * Mathf.Max(0.1f, sizeScale);
            glower.ForceRegister(Map);
        }

        // 仅点亮态发光
        public bool ShouldBeLitNow() => ignited;

        protected override void TickInterval(int delta)
        {
            if (descending)
            {
                descendTicksLeft -= delta;
                if (descendTicksLeft <= 0)
                {
                    Ignite();
                }
                return;
            }
            if (!ignited)
            {
                base.TickInterval(delta);
                return;
            }
            igniteTicksLeft -= delta;
            particleAcc += delta;
            int step = Mathf.Max(1, particleIntervalTicks);
            while (particleAcc >= step)
            {
                particleAcc -= step;
                EmitParticles();
            }
            if (igniteTicksLeft <= 0)
            {
                Destroy();
                return;
            }
        }

        private void EmitParticles()
        {
            if (!Spawned) return;
            Vector3 p = IgnitedPos;
            if (!p.ShouldSpawnMotesAt(Map)) return;
            float s = Mathf.Max(0.1f, sizeScale);

            // 烟雾粒子
            FleckCreationData smoke = FleckMaker.GetDataStatic(p, Map, FleckDefOf.Smoke, Rand.Range(smokeSize, smokeSize * 1.6f) * s);
            smoke.instanceColor = Color.white;
            smoke.rotationRate = Rand.Range(-30f, 30f);
            smoke.velocityAngle = Rand.Range(-15f, 15f);
            smoke.velocitySpeed = Rand.Range(0.3f, 0.55f);
            Map.flecks.CreateFleck(smoke);

            // 火光粒子
            FleckCreationData glow = FleckMaker.GetDataStatic(p, Map, FleckDefOf.FireGlow, Rand.Range(glowSize * 0.8f, glowSize * 1.2f) * s);
            glow.instanceColor = flareColor;
            glow.rotationRate = Rand.Range(-3f, 3f);
            glow.velocityAngle = Rand.Range(0, 360);
            glow.velocitySpeed = 0.06f;
            Map.flecks.CreateFleck(glow);

            // 火花粒子
            if (Rand.Chance(0.6f))
            {
                FleckCreationData spark = FleckMaker.GetDataStatic(p, Map, FleckDefOf.MicroSparks, Rand.Range(0.7f, 1.1f) * s);
                spark.instanceColor = flareColor;
                spark.rotationRate = Rand.Range(-12f, 12f);
                spark.velocityAngle = Rand.Range(0, 360);
                spark.velocitySpeed = Rand.Range(0.5f, 0.9f);
                Map.flecks.CreateFleck(spark);
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            // 存档恢复重新刷新光源
            if (ignited) RefreshGlower();
        }

        // 销毁前摘除光源
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            TryUnregisterGlower();
            base.Destroy(mode);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            TryUnregisterGlower();
            base.DeSpawn(mode);
        }

        private void TryUnregisterGlower()
        {
            if (!Spawned) return;
            var glower = GetComp<CompGlower>();
            if (glower == null) return;
            try { Map?.glowGrid?.DeRegisterGlower(glower); }
            catch { }
            ignited = false;
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // 点亮态隐藏弹体
            if (ignited) return;
            base.DrawAt(drawLoc, flip);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref flareColor, "flareColor", new Color(1f, 0.95f, 0.6f, 1f));
            Scribe_Values.Look(ref descending, "descending");
            Scribe_Values.Look(ref descendTicksLeft, "descendTicksLeft");
            Scribe_Values.Look(ref descendStartHeight, "descendStartHeight", 12f);
            Scribe_Values.Look(ref descendDurationTicks, "descendDurationTicks", 90);
            Scribe_Values.Look(ref ignited, "ignited");
            Scribe_Values.Look(ref igniteTicksLeft, "igniteTicksLeft");
            Scribe_Values.Look(ref igniteDurationTicks, "igniteDurationTicks", 900);
            Scribe_Values.Look(ref igniteHoverHeight, "igniteHoverHeight", 4.5f);
            Scribe_Values.Look(ref descendEndHeight, "descendEndHeight", 0.4f);
            Scribe_Values.Look(ref particleIntervalTicks, "particleIntervalTicks", 5);
            Scribe_Values.Look(ref smokeSize, "smokeSize", 0.8f);
            Scribe_Values.Look(ref glowSize, "glowSize", 1.6f);
            Scribe_Values.Look(ref lightRadius, "lightRadius", 14f);
            Scribe_Values.Look(ref lightIntensity, "lightIntensity", 1.5f);
            Scribe_Values.Look(ref sizeScale, "sizeScale", 1f);
            Scribe_Values.Look(ref approachDir, "approachDir");
        }
    }
}
