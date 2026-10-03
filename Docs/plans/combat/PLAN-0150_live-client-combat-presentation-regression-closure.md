# 真实客户端战斗表现回归

## 本次执行范围

本计划对应原编码 0150 的一次执行：真实客户端战斗表现回归。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [战斗请求封存与因果波次](../../requirements/combat/REQ-FEAT-029_combat-causal-waves.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [伤害配方抗性与吸血](../../requirements/combat/REQ-FEAT-030_damage-resistance.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [濒死批次与公平击杀归属](../../requirements/combat/REQ-FEAT-034_death-attribution.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [死亡奖励与贡献窗口](../../requirements/combat/REQ-FEAT-035_death-rewards.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [单位视图绑定与语义挂点](../../requirements/presentation-ui/REQ-FEAT-072_unit-view-binding.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [攻击技能动画与插值采样](../../requirements/presentation-ui/REQ-FEAT-073_animation-sampling.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [特效音效与回滚账本](../../requirements/presentation-ui/REQ-FEAT-074_presentation-ledger.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

收集强类型 Shield、Damage、Heal 请求；封存成因果波次后才分配最终 SequenceInTick；同目标基于批次开始时冻结状态结算。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/PlayerInput/PlayerCommandRequester.cs`：`IPlayerGameplayCommandRequester`、`IPlayerShopCommandRequester`、`IPlayerAbilityInputProfileProvider`、`IPlayerAbilityAimProfileProvider`、`ILocalAbilityRuntimeView`、`GameplayCommandRequestReceipt`、`LocalAbilityInputStateKind`。
- `Assets/Scripts/Bootstrap/CameraController.cs`：`CameraController`。
- `Assets/Scripts/Bootstrap/MobaCameraPresentationConfig.cs`：`CameraSideSettings`、`MobaCameraPresentationConfig`。
- `Assets/Scripts/FrameSync/ChecksumDiagnosticFormatter.cs`：`ChecksumDiagnosticFormatter`。
- `Assets/Scripts/FrameSync/FrameSyncGameRuntime.cs`：`FrameSyncGameRuntime`。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。
- `Assets/Scripts/Physics/Core/PhysicsEntity2D.cs`：`PhysicsEntity2D`。

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

`Assets/Scripts/PlayerInput/PlayerCommandRequester.cs`：

```csharp
using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.PlayerInput
{
    public interface IPlayerGameplayCommandRequester
    {
        bool RequestMove(in fp2 targetPoint);
        bool RequestAttack(in UnitUid targetUnitUid);
        bool RequestCastAbility(
            byte slot,
            AbilitySignalVerb signal,
            in AimSnapshot aim,
            out GameplayCommandRequestReceipt receipt);
        bool RequestAllocateAbilitySkillPoint(byte slot);
    }

    public interface IPlayerShopCommandRequester
    {
        bool RequestEquipmentPurchase(int equipmentId);
        bool RequestEquipmentSell(byte sourceSlot);
        bool RequestEquipmentUndo();
    }

    public interface IPlayerAbilityInputProfileProvider
    {
        bool TryGetTemplate(
            byte slot,
            out InputMappingTemplate template);
        bool TryGetAimKind(byte slot, out AimKind aimKind);
    }

    public interface IPlayerAbilityAimProfileProvider
    {
        bool TryGetAimConfiguration(
            byte slot,
            out AimKind aimKind,
            out fp castRange);
    }

    public interface ILocalAbilityRuntimeView
    {
        bool HasActiveSession(UnitUid ownerUid, byte slot);
        bool IsWaitingForCommit(UnitUid ownerUid, byte slot);
        /// <summary>
        /// Whether the slot may open a local aim indicator right now: the
        /// ability is ready (learned, not on cooldown, no session) or the
        /// active session can accept a real next-stage Commit immediately.
        /// </summary>
        bool CanOpenLocalAim(UnitUid ownerUid, byte slot);
    }

    public readonly struct GameplayCommandRequestReceipt
    {
        public readonly int TargetTick;
        public readonly uint CommandSeq;

        public GameplayCommandRequestReceipt(int targetTick, uint commandSeq)
        {
            TargetTick = targetTick;
            CommandSeq = commandSeq;
        }
    }

// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**战斗请求封存与因果波次**

收集强类型 Shield、Damage、Heal 请求；封存成因果波次后才分配最终 SequenceInTick；同目标基于批次开始时冻结状态结算。

同批治疗封顶、盾参与吸收、总生命伤害一次提交；由结果产生的新反应进下一波；死亡反应产生的普通请求进下一 Tick。

**伤害配方抗性与吸血**

DamageRecipe/FormulaTerm 生成伤害，固定槽位 Modifier 合并后进入暴击、抗性、盾、生命、偷取及反应；超额伤害按定点权重分摊 ActualLifeDamage。

免疫、零伤害、纯盾伤害和纯过量不计击杀优势；非法请求仍报错；结构拒绝政策合法拒绝是成功空操作。

**濒死批次与公平击杀归属**

Combat 同步请求 UnitWorld 更新 Dying/Dead；致死批次按有效敌方英雄 ActualLifeDamage 总和取最大，纯中性分数处理最高伤害并列。

旧末次伤害者方案已被修订；队伍、Prefab、提交序列不决定平局；FormalDeathResult 唯一输出给统计和奖励。

**死亡奖励与贡献窗口**

DeathRewardContext 从正式死亡与贡献日志选收受者，整数稳定分配；经验立即结算，金币统一 RequestGoldIncome，批次摘要参与共享校验。

D-041 的生产者归属与既有复仇/击杀统计日志描述存在冲突，未确认部分不能静默改写；助攻窗口不受杀手修订而丢失。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Bootstrap/Tests/PlayMode/CameraControllerPlayModeTests.cs`：PlayMode，程序集 `FrameSyncMoba.Bootstrap.PlayModeTests`，函数 `SharedConfig_UsesOppositeBlueAndRedViewDirections`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SharedConfig_UsesOppositeBlueAndRedViewDirections()
        {
            MobaCameraPresentationConfig config =
                ScriptableObject.CreateInstance<
                    MobaCameraPresentationConfig>();
            try
            {
                CameraSideSettings blue = config.ResolveSide((byte)1);
                CameraSideSettings red = config.ResolveSide((byte)2);
                Assert.That(red.EulerAngles.y - blue.EulerAngles.y,
                    Is.EqualTo(180f).Within(.001f));
                Assert.That(red.FollowOffset.z,
                    Is.EqualTo(-blue.FollowOffset.z).Within(.001f));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
```
- `Assets/Scripts/Bootstrap/Tests/PlayMode/PlayerInputSimulationPlayModeTests.cs`：PlayMode，程序集 `FrameSyncMoba.Bootstrap.PlayModeTests`，函数 `PlayerInputSimulationPlayModeTests`；输入/夹具与期望见真实断言，失败保留回执与 Console。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/VarusAnimationPlayModeTests.cs`：PlayMode，程序集 `FrameSyncMoba.Bootstrap.PlayModeTests`，函数 `VarusAnimationPlayModeTests`；输入/夹具与期望见真实断言，失败保留回执与 Console。
- `Assets/Scripts/FrameSync/Tests/AnimationSamplingTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `PresentationTime_UsesConfiguredTickRateAndSubTickAlpha`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void PresentationTime_UsesConfiguredTickRateAndSubTickAlpha()
        {
            var atTwentyHz = new AnimationPresentationTime(
                9,
                20,
                0.5d);
            var atSixtyHz = new AnimationPresentationTime(
                9,
                60,
                0.5d);

            Assert.That(atTwentyHz.LogicTimeTicks,
                Is.EqualTo(9.5d).Within(0.000001d));
            Assert.That(atTwentyHz.LogicTimeSeconds,
                Is.EqualTo(0.475d).Within(0.000001d));
            Assert.That(atSixtyHz.LogicTimeSeconds,
                Is.EqualTo(9.5d / 60d).Within(0.000001d));
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
