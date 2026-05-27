using UnityEngine;
using Verse;

namespace Fortified
{
    // 照明弹投放数据
    public class AirSupportData_LaunchFlare : AirSupportData_LaunchProjectile
    {
        // 照明弹颜色
        public Color color = new Color(1f, 0.95f, 0.6f, 1f);
        // 点亮持续时间
        public int igniteDurationTicks = 900;
        // 开伞悬浮高度
        public float igniteHoverHeight = 4.5f;
        // 下坠起始高度
        public float descendStartHeight = 30f;
        // 下坠时长
        public int descendDurationTicks = 90;
        // 入射方向单位向量
        public Vector3 approachDir;
        // 视觉尺寸倍率
        public float sizeScale = 1f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref color, "color", new Color(1f, 0.95f, 0.6f, 1f));
            Scribe_Values.Look(ref igniteDurationTicks, "igniteDurationTicks", 900);
            Scribe_Values.Look(ref igniteHoverHeight, "igniteHoverHeight", 4.5f);
            Scribe_Values.Look(ref descendStartHeight, "descendStartHeight", 30f);
            Scribe_Values.Look(ref descendDurationTicks, "descendDurationTicks", 90);
            Scribe_Values.Look(ref approachDir, "approachDir");
            Scribe_Values.Look(ref sizeScale, "sizeScale", 1f);
        }

        protected override void LaunchProjectile(Thing projectile, Thing launcher)
        {
            if (projectile is Projectile_FFF_Flare flare)
            {
                flare.flareColor = color;
                flare.igniteDurationTicks = igniteDurationTicks;
                flare.igniteHoverHeight = igniteHoverHeight;
                flare.descendStartHeight = descendStartHeight;
                flare.descendDurationTicks = descendDurationTicks;
                flare.approachDir = approachDir;
                flare.sizeScale = sizeScale;
            }
            base.LaunchProjectile(projectile, launcher);
        }
    }
}
