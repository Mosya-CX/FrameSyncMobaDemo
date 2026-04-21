using UnityEngine;

/// <summary>
/// 兼容占位组件。
/// Step-only 最终版里，被动技能不再依赖事件路由器启动，
/// 而是由 SkillExecutionController.Tick() 按 IsPassive + CanAutoStartPassive 自动轮询。
/// 
/// 这个组件保留的唯一目的，是避免旧 prefab 还挂着它时报 Missing Script / 编译引用。
/// </summary>
[RequireComponent(typeof(SkillBook))]
[RequireComponent(typeof(SkillExecutionController))]
public sealed class PassiveSkillEventRouter : UnitBaseHandler
{
    protected override void Awake()
    {
        base.Awake();
    }

    public override void Tick(Unity.Mathematics.FixedPoint.fp deltaTime)
    {
        // no-op
    }

    public override object CaptureState() => null;

    public override void RestoreState(object state)
    {
    }
}
