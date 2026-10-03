# 客户端控制表现与界面组合

## 本次执行范围

本计划对应原编码 0123 的一次执行：客户端控制表现与界面组合。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [单位视图绑定与语义挂点](../../requirements/presentation-ui/REQ-FEAT-072_unit-view-binding.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [攻击技能动画与插值采样](../../requirements/presentation-ui/REQ-FEAT-073_animation-sampling.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [特效音效与回滚账本](../../requirements/presentation-ui/REQ-FEAT-074_presentation-ledger.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [页面层级与 Lua 实例生命周期](../../requirements/presentation-ui/REQ-FEAT-075_lua-ui-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [HUD 数值技能与小地图](../../requirements/presentation-ui/REQ-FEAT-076_hud-minimap.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UnitPresentationHost/Registry 和 PresentationSocketSet 只读逻辑；SocketProfile 定义挂点，缺失挂点按可见校验策略处理。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Bootstrap/ClientBootstrap.cs`：`ClientBootstrap`。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitUid.cs`：`UnitUid`。
- `Assets/Scripts/Bootstrap/PresentationEventDispatcher.cs`：`PresentationEventDispatcher`、`PresentationEventHistory`、`IVfxHandler`、`ISfxHandler`。
- `Assets/Scripts/Bootstrap/UI/UIManager.cs`：`UIManager`、`PageRegistration`。
- `Assets/Scripts/Gameplay/Ability/AbilitySignal.cs`：`AbilitySignal`、`AbilitySignalVerb`、`AimKind`、`AimSnapshot`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/PlayerInput/PlayerCommandRequester.cs`：`IPlayerGameplayCommandRequester`、`IPlayerShopCommandRequester`、`IPlayerAbilityInputProfileProvider`、`IPlayerAbilityAimProfileProvider`、`ILocalAbilityRuntimeView`、`GameplayCommandRequestReceipt`、`LocalAbilityInputStateKind`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Bootstrap/ClientBootstrap.cs`：

```csharp
using System;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.RuntimeConfig;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Client process startup scene. Owns account bootstrap and the UOS client
    /// session, marks the NGO network root persistent and then loads the Lobby
    /// scene. It never initializes Gameplay or Lobby UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClientBootstrap :
        MonoBehaviour
    {
        [SerializeField] private bool
            enableOnlineApplicationFlow;
        [SerializeField] private GlobalGameplayData
            globalGameplayData;
        [SerializeField] private NetworkManager
            networkManager;

        private void Awake()
        {
            GameSessionContext.ResetSession();
            GameSessionContext.IsDedicatedServer =
                false;
            FrameSyncDiagnosticsUnityHost.EnsureInitialized(
                false);
            GameSessionContext.FlowManagedExternally =
                true;
            enableOnlineApplicationFlow =
                UosApplicationConfig.IsOnlineFlowRequested(
                    enableOnlineApplicationFlow);
            GameSessionContext.FlowMode =
                enableOnlineApplicationFlow
                    ? FrameFlowMode.UosOnline
                    : FrameFlowMode.LocalDirect;
            GameSessionContext.Versions =
                GameSessionContext.ComputeVersions(
                    globalGameplayData);
            GameSessionContext.HeroDisplayTable =
                globalGameplayData != null
                    ? globalGameplayData.HeroDisplayTable
                    : null;
            SharedGameplayChecksum.DetailedLoggingEnabled =
                Array.IndexOf(
                    Environment.GetCommandLineArgs(),
                    "-checksumDetail") >= 0;
            MarkNetworkRootPersistent();
        }

        private async void Start()
        {
            if (!enableOnlineApplicationFlow)
            {
                LoadLobby();
                return;
            }

            try
            {
                if (networkManager == null)
                    throw new InvalidOperationException(
                        "Client online flow requires NetworkManager.");
                string configId =
                    UosApplicationConfig
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/FrameSync/GameplaySnapshot.cs`：

```csharp
using System;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.FrameSync
{
    public struct UnitSnapshot
    {
        public UnitUid UnitUid;
        public GameplayParticipantId GameplayParticipantId;
        public UnitUid OwnerUid;
        public UnitKind UnitKind;
        public ushort UnitSubKindId;
        public TeamId TeamId;
        public int UnitPrototypeId;
        /// <summary>Home spawn position used when this unit respawns.</summary>
        public fp2 RespawnPosition;
        public LifeState LifeState;
        public CapabilityState CapabilityState;
        public HitReactionState HitReactionState;
        public UnitIntent IntentState;
        public ActionRuntimeSetSnapshot ActionRuntimeState;
        public PhysicsTransform2D PhysicsTransform;
        public PhysicsShape2D PhysicsShape;
        public StatHandlerSnapshot StatState;
        public CombatModifierSetSnapshot CombatModifierState;
        public AttackSnapshot AttackState;
        public MovementSnapshot MovementState;
        public AbilityHandlerSnapshot AbilityState;
        public BuffHandlerSnapshot BuffState;
        public CrowdControlHandlerSnapshot CCState;
        public LocomotionAgentSnapshot LocomotionState;
        public EquipmentHandlerSnapshot EquipmentState;
        public UnitTag[] Tags;
    }

    /// <summary>
    /// Snapshot of the entire UnitWorld for rollback.
    /// Uses T[] arrays per Snapshot Appendix v7.2 section 5.
    /// </summary>
    public struct UnitWorldSnapshot
    {
        public UnitSnapshot[] Units;
        public MinionSystemSnapshot MinionSystemState;
        public RespawnTimerSnapshot PendingUnitLifecycleState;
        public JungleCampSnapshot[] JungleCampStates;
        public UnitAIControllerSnapshot[] AIControllerStates;
        public int RuntimeRevision;

        public static UnitWorldSnapshot CreateEmpty() => new UnitWorldSnapshot
        {
            Units = Array.Empty<UnitSnapshot>(),
            JungleCampStates = Array.Empty<JungleCampSnapshot>(),
            AIControllerStates = Array.Empty<UnitAIControllerSnapshot>(),
        };
    }

    public struct GameplaySnapshot
    {
        public const int CurrentSchemaVersion = 25;
        public int SchemaVersion;

        public DeterministicRandomSnapshot RandomState;
        public MatchRuleRuntimeSnapshot MatchRuleState;
        public UnitWorldSnapshot UnitWorldState;
        public CombatSnapshot CombatState;
        public ProjectileWorldSnapshot ProjectileState;
        public EquipmentShopRuntimeSnapshot EquipmentShopState;
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**单位视图绑定与语义挂点**

UnitPresentationHost/Registry 和 PresentationSocketSet 只读逻辑；SocketProfile 定义挂点，缺失挂点按可见校验策略处理。

视图不能反写 Gameplay；异步加载不能复用旧生命；逻辑空间所有者不随模型层级变化。

**攻击技能动画与插值采样**

UnitAnimationDriver 读取 Attack 锁定时间与 AbilityCastView；客户端默认 20 Hz 插值，Bootstrap 发布按 UnitWorld 拥有的连续逻辑时间投影。

不得跨未 Commit 的 Impact 或 Ready；loop 相位由逻辑 epoch 和实时倍率重建；未知 TickRate 不硬回退 30 Hz；独立采样不新增 Gameplay Tick。

**特效音效与回滚账本**

VfxManager、AudioManager 分别管理定义、池和回滚账本；PresentationEventId=SourceLogicTick+SourceKind+SourceRuntimeUid+EventSequence+EventKey，支持 OneShotNoReplay、DurationCorrectable、LoopState。

Gameplay 不直接 Play 音效；每 manager 独立账本；事件序号归源运行时所有，表现不新增第二序号。

**页面层级与 Lua 实例生命周期**

UIManager、UIPanel/UIPage 管理页面，LuaManager 管理环境，LuaHost 管理实例；UIList/UICell 复用格子，显式绑定与解绑。

不由 UI 决定预测或回滚；页面关闭移除观察者；主机单元和页面实例不共用意外 mutable 状态。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/PlayerInput/Tests/PlayerCommandRequesterTests.cs`：EditMode，程序集 `FrameSyncMoba.PlayerInput.Tests`，函数 `EventBuffer_AssignsStableSequenceAndRejectsOverflow`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void EventBuffer_AssignsStableSequenceAndRejectsOverflow()
        {
            var buffer = new LocalInputEventBuffer();
            for (int i = 0; i < LocalInputEventBuffer.MaxLocalInputEventsPerUnityFrame; i++)
            {
                Assert.IsTrue(buffer.Push(
                    LocalGameplayInputEventKind.AbilityKeyPressed,
                    (byte)(i % 4),
                    new Vector2(i, i)));
            }
            Assert.IsFalse(buffer.Push(
                LocalGameplayInputEventKind.PrimaryClick, 0, Vector2.zero));

            ulong previous = 0;
            while (buffer.TryDequeue(out LocalGameplayInputEvent inputEvent))
            {
                Assert.Greater(inputEvent.LocalEventSequence, previous);
                previous = inputEvent.LocalEventSequence;
            }
        }
```
- `Assets/Scripts/Gameplay/Tests/UnitUidTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `SameComponents_ProduceEqualIdentity`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SameComponents_ProduceEqualIdentity()
        {
            var first = new UnitUid(1200, 1001, 7);
            var second = new UnitUid(1200, 1001, 7);

            Assert.That(first.SpawnLogicTick, Is.EqualTo(1200));
            Assert.That(first.RuntimeEntityPrefabId, Is.EqualTo(1001));
            Assert.That(first.SpawnSequenceInTick, Is.EqualTo(7));
            Assert.That(first.Equals(second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }
```
- `Assets/Scripts/Bootstrap/Tests/EditMode/PresentationEventDispatcherTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `Replay_DoesNotDispatchCompletedEventAgain`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void Replay_DoesNotDispatchCompletedEventAgain()
        {
            PresentationEventId id =
                CreateId(10, 1, 20);

            SubmitVfx(id);
            dispatcher.DispatchCurrentFrame();
            SubmitVfx(id);
            dispatcher.DispatchCurrentFrame();

            Assert.That(vfx.Count, Is.EqualTo(1));
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
