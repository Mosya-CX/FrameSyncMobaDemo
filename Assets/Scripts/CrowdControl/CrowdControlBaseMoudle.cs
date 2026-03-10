using UnityEngine;

/// <summary>
/// 控制效果模块基类。
/// 
/// 设计目标：
/// 1. 兼容当前 CrowdControlHandler 里直接 Apply(null) 的调用方式
/// 2. 后续可平滑升级为传入完整上下文
/// </summary>
public abstract class CrowdControlBaseMoudle : ScriptableObject
{
    /// <summary>
    /// 兼容旧调用方式。
    /// 当前你可以直接 Apply(null)。
    /// </summary>
    public void Apply(object context)
    {
        if (context is CrowdControlRuntimeContext runtimeContext)
        {
            Apply(runtimeContext);
            return;
        }

        ApplyDefault();
    }

    /// <summary>
    /// 推荐后续使用的强类型入口。
    /// </summary>
    public virtual void Apply(CrowdControlRuntimeContext context)
    {
        ApplyDefault();
    }

    /// <summary>
    /// 当前没有上下文时执行的默认逻辑。
    /// 大多数模块可以直接重写这个。
    /// </summary>
    protected virtual void ApplyDefault() { }
}