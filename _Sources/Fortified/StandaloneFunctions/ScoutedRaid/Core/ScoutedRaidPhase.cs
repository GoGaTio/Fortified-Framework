namespace Fortified
{
    // 侦察袭击状态
    public enum ScoutedRaidPhase
    {
        ScoutInbound,       // 侦察生成等待
        ScoutActive,        // 侦察执行
        BombardmentDelay,   // 炮击前延迟
        Bombardment,        // 炮击调度中
        InterCycleWait,     // 周期间等待
        PreMainRaidWait,    // 主袭击前等待
        MainRaidIssued,     // 主袭击发起
        Done,
    }
}
