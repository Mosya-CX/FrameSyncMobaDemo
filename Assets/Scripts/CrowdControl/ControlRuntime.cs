using Unity.Mathematics.FixedPoint;

/// <summary>
/// 控制效果运行时上下文。
/// 供 CrowdControlBaseMoudle 子类读取/修改运行状态。
/// </summary>
public sealed class CrowdControlRuntimeContext
{
    /// <summary>
    /// 被施加控制的单位
    /// </summary>
    public UnitCore Owner;

    /// <summary>
    /// 控制配置本体
    /// </summary>
    public CrowdControlData Data;

    /// <summary>
    /// 剩余持续时间
    /// </summary>
    public fp RemainingTime;

    /// <summary>
    /// 来源单位，可为空
    /// </summary>
    public UnitCore Source;

    /// <summary>
    /// 额外自定义参数
    /// </summary>
    public object UserData;
}