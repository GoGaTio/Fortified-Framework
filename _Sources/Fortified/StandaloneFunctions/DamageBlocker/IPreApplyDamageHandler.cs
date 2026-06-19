using Verse;

namespace Fortified
{
    // 统一伤害前处理接口 进pawn层优先级池
    public interface IPreApplyDamageHandler
    {
        // 处理优先级 高者先挡
        int PreApplyDamagePriority { get; }

        // 伤害前处理 挡住置absorbed
        void HandlePreApplyDamage(ref DamageInfo dinfo, out bool absorbed);
    }
}
