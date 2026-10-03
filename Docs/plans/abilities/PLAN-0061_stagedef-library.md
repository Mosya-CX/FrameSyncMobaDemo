# 技能 Stage 定义库

## 本次执行范围

本计划对应原编码 0061 的一次执行：技能 Stage 定义库。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [阶段效果与确定性黑板](../../requirements/abilities/REQ-FEAT-044_stage-effects.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

StageDef 用 AbilityStageContext、AbilityPorts 和受限 AbilityBlackboard；技能范围约束按需启用，效果写入所属系统，不增加 EffectPlan/EffectStep。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Ability/Stages/HealStageDef.cs`：`HealStageDef`。
- `Assets/Scripts/Gameplay/Ability/Stages/HealStageDefAuthoring.cs`：`HealStageDefAuthoring`。
- `Assets/Scripts/Gameplay/Ability/Stages/PullStageDef.cs`：`PullStageDef`。
- `Assets/Scripts/Gameplay/Ability/Stages/PullStageDefAuthoring.cs`：`PullStageDefAuthoring`。
- `Assets/Scripts/Gameplay/Ability/Stages/ShieldStageDef.cs`：`ShieldStageDef`。
- `Assets/Scripts/Gameplay/Ability/Stages/ShieldStageDefAuthoring.cs`：`ShieldStageDefAuthoring`。
- `Assets/Scripts/Gameplay/Ability/Stages/StunStageDef.cs`：`StunStageDef`。
- `Assets/Scripts/Gameplay/Ability/Stages/StunStageDefAuthoring.cs`：`StunStageDefAuthoring`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Ability/Stages/HealStageDef.cs`：

```csharp
        public override StageResult OnEnter(AbilitySession session, AbilityRuntime runtime)
        {
            if (runtime.World?.CombatSystem == null || BaseHeal <= fp.zero)
                return StageResult.Failed;
            if (!runtime.World.TryGetUnit(runtime.CasterUnitUid, out Unit caster))
                return StageResult.Failed;

            UnitUid targetUid = TargetRule == BuffTargetRule.Self
                ? runtime.CasterUnitUid
                : session.Aim.TargetUnitUid;
            if (!targetUid.IsValid())
                return StageResult.Failed;

            fp healAmount = BaseHeal;
            if (runtime.World.TryGetUnit(runtime.CasterUnitUid, out Unit src) && src.StatHandler != null)
            {
                fp healPower = src.StatHandler.GetStat(StatId.HealPower);
                healAmount *= (fp.one + healPower);
            }

            var request = new HealRequest
            {
                TargetUnitUid = targetUid,
                SourceUnitUid = runtime.CasterUnitUid,
                BaseValue = healAmount,
            };
            runtime.World.CombatSystem.SubmitHeal(request);
            return StageResult.Completed;
        }
```

`Assets/Scripts/Gameplay/Ability/Stages/HealStageDefAuthoring.cs`：

```csharp
using System;
using UnityEngine;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class HealStageDefAuthoring : StageDefAuthoring
    {
        [Min(0f)]
        [SerializeField] private float baseHeal = 50f;
        [SerializeField] private BuffTargetRule targetRule = BuffTargetRule.Self;

        public override StageDef Bake(int tickRate = 30)
        {
            return new HealStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                BaseHeal = (fp)baseHeal,
                TargetRule = targetRule,
            };
        }
    }
}
```

### 输入输出与边界

**阶段效果与确定性黑板**

StageDef 用 AbilityStageContext、AbilityPorts 和受限 AbilityBlackboard；技能范围约束按需启用，效果写入所属系统，不增加 EffectPlan/EffectStep。

Handle 由创建 Effect 自己清理；Stage 成长不放在 AbilityDef 全局重复字段；结构过滤配置之外仍保留中央准入。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

没有找到原范围精确命名的当前测试文件；关闭记录依据用户人工功能确认，不伪造自动化用例。

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
