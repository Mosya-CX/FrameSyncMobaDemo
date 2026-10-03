# 韦鲁斯输入指示器与英雄测试回归

## 本次执行范围

本计划对应原编码 0151 的一次执行：韦鲁斯输入指示器与英雄测试回归。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [设备事件缓冲与 UI 门禁](../../requirements/player-input/REQ-FEAT-078_input-event-buffer.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能输入组合与提交去重](../../requirements/player-input/REQ-FEAT-079_ability-input.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [世界鼠标 Aim 与类型化 Request](../../requirements/player-input/REQ-FEAT-080_aim-requests.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能指示器与本地生命周期](../../requirements/player-input/REQ-FEAT-081_skill-indicator.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

PlayerInputController 显式订阅 InputAction，回调记录本地事件；GameplayInputGate 处理 ActionMap、UI 指针阻断和受控单位变化。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/PlayerInput/GameplayInputGate.cs`：`IGameplayInputGate`、`GameplayInputGate`。
- `Assets/Scripts/PlayerInput/SkillIndicatorDriver.cs`：`SkillIndicatorDriver`。
- `Assets/Scripts/FrameSync/CommandCollector.cs`：`CommandCollector`、`CommandMergeKey`、`UseItemMergeKey`、`GameplayCommandCanonicalComparer`。
- `Assets/Scripts/Gameplay/Ability/AbilitySignal.cs`：`AbilitySignal`、`AbilitySignalVerb`、`AimKind`、`AimSnapshot`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：

```csharp
using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [DisallowMultipleComponent]
    public sealed class Unit : MonoBehaviour, IUnitCollisionParticipant
    {
        [Header("Deterministic composition")]
        [Tooltip("Authoritative 2D physics component owned by this Unit prefab.")]
        [SerializeField] private PhysicsEntity2D physicsEntity;
        [SerializeField] private StatHandler statHandler;
        [SerializeField] private MovementHandler movementHandler;
        [SerializeField] private AttackHandler attackHandler;
        [SerializeField] private AbilityHandler abilityHandler;
        [SerializeField] private BuffHandler buffHandler;
        [SerializeField] private CrowdControlHandler crowdControlHandler;
        [SerializeField] private EquipmentHandler equipmentHandler;

        private CapabilityState capabilityState;
        private UnitAbilityMask abilityMask;
        private readonly List<UnitTag> tags =
            new List<UnitTag>();

        /// <summary>Deterministic runtime identity (SpawnLogicTick /
        /// prefab id / spawn sequence). Displayed in the Inspector for
        /// debugging spawned unit instances.</summary>
        [ShowInInspector]
        [ReadOnly]
        [PropertyOrder(-120)]
        public UnitUid UnitUid { get; private set; }
        public GameplayParticipantId GameplayParticipantId { get; private set; }
        public UnitWorld World { get; internal set; }
        public UnitUid OwnerUid { get; private set; }
        public UnitKind UnitKind { get; private set; }
        public ushort UnitSubKindId { get; private set; }
        public TeamId TeamId { get; private set; }
        public int UnitPrototypeId { get; private set; }
        public int BaseGoldValue { get; private set; }
        public int BaseExperienceValue { get; private set; }
        public int BaseCreepScoreValue { get; private set; }
        public LifeState LifeState { get; private set; }
        public ref readonly CapabilityState CapabilityState => ref capabilityState;
        public UnitAbilityMask AbilityMask => abilityMask;

        public PhysicsEntity2D PhysicsEntity => physicsEntity;
        public StatHandler StatHandler => statHandler;
        public CombatModifierSet CombatModifiers { get; private set; }
        public MovementHandler MovementHandler => movementHandler;
        public AttackHandler AttackHandler => attackHandler;
        public AbilityHandler AbilityHandler => abilityHandler;
        public BuffHandler BuffHandler => buffHandler;
        public CrowdControlHandler CrowdControl => crowdControlHandler;
        public EquipmentHandler EquipmentHandler => equipmentHandler;
        public UnitEventBus EventBus { get; private set; }

        public UnitIntent Intent { get => Planner?.CurrentIntent ?? UnitIntent.None; internal set => Planner?.SetIntent(value); }
        public BehaviorPlanner Planner { get; private set; }
        public ActionArbiter Arbiter { get; private set; }
        public ActionRuntimeSet ActionRuntimes { get; private set; }

        public UnitLocomotionAgent Locomotion { get; internal set; }
        public int Level => statHandler?.Level ?? 1;
        /// <summary>
        /// The deterministic home spawn position captured when this runtime
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/PlayerInput/GameplayInputGate.cs`：

```csharp
        public bool IsAttackAllowed(UnitType unit)
        {
            if (unit == null) return false;
            if (unit.LifeState != LifeState.Alive) return false;
            ref readonly var cap = ref unit.CapabilityState;
            if (!cap.CanAttack) return false;
            if (unit.CrowdControl != null &&
                unit.CrowdControl.IsBlocked(
                    UnitActionBlockMask.VoluntaryAttack))
                return false;
            if (unit.AbilityHandler != null &&
                unit.AbilityHandler.IsCastMovementLocked())
                return false;
            // Only a real action-owning cast stage blocks a normal attack.
            // Pure Toggles deliberately retain an AbilitySession without
            // owning Main/Base ActionRuntime resources (D-047), so their
            // persistent session must remain attack-neutral.
            if (unit.AbilityHandler != null &&
                unit.AbilityHandler.HasActiveActionStage())
                return false;
            return true;
        }
```

### 输入输出与边界

**设备事件缓冲与 UI 门禁**

PlayerInputController 显式订阅 InputAction，回调记录本地事件；GameplayInputGate 处理 ActionMap、UI 指针阻断和受控单位变化。

回放不再读设备；同帧顺序、缓冲上限、禁用时清理和松键行为明确；UI 使用 Unity Input System UI 整合。

**技能输入组合与提交去重**

从 CastModelDef 离线派生 PressCommit、LocalAimPrimaryCommit、PressFocusReleaseOrPrimaryCommit，运行本地 FocusRequested/CommitRequested/GameplayFocusing 状态。

激活 hold-release 后松键和左键走同一 Commit，首次成功抑制重复；右键不 Cancel 但可移动/普攻；配置不复制 Gameplay 费用/范围/时长。

**世界鼠标 Aim 与类型化 Request**

鼠标世界解析选择地面点或 Unit，Direction 规范化；Move/Attack/Ability typed Request 返回回执，AimSnapshot 使用唯一正式字段。

输入层不 Clamp 技能距离；本地目标仅最低检查，最终执行重查；Focus 与 Commit 同 TargetTick 保持序列顺序。

**技能指示器与本地生命周期**

SkillIndicatorDriver 根据 AbilityCastView 和 StageDef 通过本地 Resolver 显示方向、圆或点目标；不是单独 Gameplay 状态。

受控单位变化、禁用、死亡、UI 门禁和 Session 结束关闭；游戏回滚后跟随 Runtime，不反写命令或技能距离。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Bootstrap/Tests/PlayMode/HeroTestDriver.cs`：PlayMode，程序集 `FrameSyncMoba.Bootstrap.PlayModeTests`，函数 `HeroTestDriver`；输入/夹具与期望见真实断言，失败保留回执与 Console。

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
