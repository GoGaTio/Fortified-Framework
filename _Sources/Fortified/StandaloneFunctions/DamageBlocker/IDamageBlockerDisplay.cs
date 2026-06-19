namespace Fortified
{
    // Gizmo显示接口
    public interface IDamageBlockerDisplay
    {
        int CurrentCharges { get; }
        int MaxCharges { get; }
        float Threshold { get; }
        string ThresholdOperator { get; }
        string ThresholdLabelKey { get; }
        string ChargesLabelKey { get; }
        bool IsArmorMode { get; }
    }
}
