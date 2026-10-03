# 可运行组合根与中立测试夹具

## 本次执行范围

本计划对应原编码 0110 的一次执行：可运行组合根与中立测试夹具。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [加载确认与单调时钟开局屏障](../../requirements/match-flow/REQ-FEAT-003_startup-barrier.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [逻辑时钟与 Tick 推进](../../requirements/frame-sync/REQ-FEAT-005_simulation-tick.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [快照树与字段归属](../../requirements/frame-sync/REQ-FEAT-012_snapshot-ownership.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [稳定 UID 与参与者身份](../../requirements/determinism/REQ-FEAT-015_stable-unit-identity.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [全局配置与离线校验](../../requirements/configuration-content/REQ-FEAT-017_configuration-validation.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [按对局加载内容闭包](../../requirements/configuration-content/REQ-FEAT-018_match-content-closure.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [单位根与能力装配](../../requirements/units/REQ-FEAT-020_unit-capability-composition.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [同步生成死亡复活与回池](../../requirements/units/REQ-FEAT-028_unit-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能信号与会话状态](../../requirements/abilities/REQ-FEAT-042_ability-signal-session.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [AI 注册调度与多态快照](../../requirements/non-heroes/REQ-FEAT-068_ai-scheduling.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

采用载荷加载确认与 LaunchCommit 两阶段协议。同步网络时间给出授权时刻，本机 Stopwatch 单调时钟调度；墙钟和传输估计只在 Bootstrap。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/Bootstrap/ClientBootstrap.cs`：`ClientBootstrap`。
- `Assets/Scripts/Gameplay/Unit/Authoring/UnitRuntimeCatalogAsset.cs`：`StatDefinitionAuthoring`、`StatPresetEntryAuthoring`、`LocomotionProfileAuthoring`、`PhysicsProfile2DAuthoring`、`UnitPrototypeAuthoring`、`BakedUnitRuntimeCatalog`、`UnitRuntimeCatalogAsset`。
- `Assets/Scripts/RuntimeConfig/GlobalPrefabTable.cs`：`PrefabKind`、`PrefabEntry`、`PrefabGroup`、`PrefabKindRangeConfig`、`GlobalPrefabTable`。
- `Assets/Scripts/FrameSync/FrameSyncGameRuntime.cs`：`FrameSyncGameRuntime`。
- `Assets/Scripts/Gameplay/Stats/StatDefinitionTable.cs`：`StatDefinitionTable`。
- `Assets/Scripts/Gameplay/Stats/StatId.cs`：`StatId`。

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

`Assets/Scripts/Bootstrap/GameBootstrap.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.LuaBridge;
using FrameSyncMoba.Physics;
using FrameSyncMoba.PlayerInput;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap
{
    [Serializable]
    public struct InitialUnitSpawnAuthoring
    {
        [Min(0)] public int StableSpawnOrder;
        [Min(1)] public int UnitPrototypeId;
        [Min(0)] public int TeamId;
        public Vector2 Position;
        public Vector2 Forward;
        public bool UseMapSpawnPoint;
        [Min(0)] public int SpawnPointId;
        public MatchTopologyRole MatchTopologyRole;
        public bool EnableTowerAI;
        public bool PlayerControlled;
        [Min(0)] public int PlayerSlot;
    }

    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Project-wide deterministic configuration")]
        [SerializeField] private GlobalGameplayData globalGameplayData;
        [SerializeField, HideInInspector] private UnitRuntimeCatalogAsset unitRuntimeCatalog;
        [SerializeField, HideInInspector] private AbilityRuntimeCatalogAsset abilityRuntimeCatalog;
        [SerializeField, HideInInspector] private ProjectileRuntimeCatalogAsset projectileRuntimeCatalog;
        [SerializeField, HideInInspector] private DeterministicMapConfig deterministicMapConfig;
        [SerializeField, HideInInspector] private EquipmentCatalogAsset equipmentCatalog;
        [SerializeField, HideInInspector] private BuffCatalogAsset buffCatalog;
        [SerializeField, HideInInspector] private CrowdControlCatalogAsset crowdControlCatalog;
        [SerializeField] private bool dedicatedServer;
        [SerializeField] private bool driveSimulationFromUnityUpdate = true;

        [Header("Optional online application flow")]
        [SerializeField] private bool enableOnlineApplicationFlow;
        [Tooltip("Explicit local NGO path. It bypasses UOS only for local development and never reports provider success.")]
        [SerializeField] private bool localDevelopmentNetworkFlow;
        [SerializeField] private bool autoApplyLocalFixturePayload = true;
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private FrameSyncNetworkBridge frameSyncNetworkBridge;

        [Header("Frozen match-start composition")]
        [SerializeField] private List<InitialUnitSpawnAuthoring> initialUnitSpawns =
            new List<InitialUnitSpawnAuthoring>();

        [Header("Client-local input (unused on Dedicated Server)")]
        [SerializeField] private PlayerInputController playerInputController;
        [SerializeField] private Camera gameplayCamera;

        [Header("Presentation (client only)")]
        [SerializeField] private SkillIndicatorDriver indicatorDriver;
        [SerializeField] private PresentationEventDispatcher presentationDispatcher;
        [SerializeField] private VfxEventHandler vfxEventHandler;
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**加载确认与单调时钟开局屏障**

采用载荷加载确认与 LaunchCommit 两阶段协议。同步网络时间给出授权时刻，本机 Stopwatch 单调时钟调度；墙钟和传输估计只在 Bootstrap。

不得继续执行旧的载荷携带 UTC 授权方案；Ready 不执行 Gameplay；延迟、重复确认与过期授权不启动第二次对局。

**逻辑时钟与 Tick 推进**

ServerTick、LocalSimulationTick 都是下一待执行 Tick；LatestAuthorityFrameTick 是最近连续接受权威帧，SnapshotTick 是恢复后下一 Tick。SimulationTickContext 提供只读上下文。

预测领先上限和每 Unity 帧执行上限明确；重演不读取渲染耗时或输入设备。

**快照树与字段归属**

GameplaySnapshot 聚合 UnitWorld、Combat、Projectile、EquipmentShop、Physics、MatchRule 和随机状态；Snapshot 间隔一 Tick。技术 UID、Participant 和 OriginAction 均按所属模块保存。

Tick 内工作缓存不进入快照；恢复不能猜测缺字段；版本与 GameplayDataVersion 同步推进。

**稳定 UID 与参与者身份**

UnitUid、ProjectileUid 使用生成 Tick 与归属系统序列；GameplayParticipantId 从稳定出生来源建立，OriginActionId 由参与者、来源类型/ID、逻辑 Tick 和本地动作序列组成。

参与者缺失或重复可见失败；不得用 PrefabId、对象注册顺序、实例 ID 或队伍侧生成中性随机身份。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/UnitRuntimeCatalogAssetTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `Bake_ConvertsFloatAuthoringToExistingRuntimeContracts`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void Bake_ConvertsFloatAuthoringToExistingRuntimeContracts()
        {
            UnitRuntimeCatalogAsset catalog = CreateCatalog(
                CreateDefinitions(),
                new[] { CreatePrototype() },
                out GlobalPrefabTable prefabTable);

            BakedUnitRuntimeCatalog baked = catalog.BakeOrThrow(prefabTable);

            Assert.That(baked.StatDefinitions.Count, Is.EqualTo(2));
            Assert.That(baked.UnitPrototypes.Count, Is.EqualTo(1));
            Assert.That(
                baked.StatDefinitions.TryGet(StatId.MaxHealth, out StatDefinition health),
                Is.True);
            Assert.That(health.DefaultBaseValue, Is.EqualTo((fp)100f));
            Assert.That(
                baked.UnitPrototypes.TryGet(1001, out UnitPrototype prototype),
                Is.True);
            Assert.That(
                prototype.LocomotionProfile.BaseMoveSpeed,
                Is.EqualTo((fp)3.5f));
            Assert.That(prototype.PhysicsProfile.ShapeParam, Is.EqualTo((fp)0.5f));
            Assert.That(prototype.BaseStats.Stats[0].StatId, Is.EqualTo(StatId.MaxHealth));
            Assert.That(prototype.BaseStats.Stats[1].StatId, Is.EqualTo(StatId.MoveSpeed));
        }
```
- `Assets/Scripts/Gameplay/Tests/CombatSystemTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `SubmitDamage_ValidRequest_ReducesHealth`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SubmitDamage_ValidRequest_ReducesHealth()
        {
            BeginTick(1);
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            var target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            fp initialHealth = target.StatHandler.CurrentHealth;

            _combat.BeginTick();
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                attacker.UnitUid, target.UnitUid, (fp)100));
            _combat.SettleActiveRequests();
            _combat.EndTick();

            fp finalHealth = target.StatHandler.CurrentHealth;
            Assert.Less(finalHealth, initialHealth);
            Assert.Greater(finalHealth, fp.zero);
        }
```
- `Assets/Scripts/Gameplay/Tests/StatHandlerSnapshotTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `CaptureRestore_RoundTrip_PreservesAllState`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void CaptureRestore_RoundTrip_PreservesAllState()
        {
            StatHandler h = CreateHandler();
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            h.AddModifier(StatId.MaxHealth, StatModifierOperation.FlatAdd, (fp)100m);
            h.FinalizeTick();

            StatHandlerSnapshot snapshot = default;
            h.Capture(ref snapshot);

            // Modify after capture
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)999m);
            h.Level = 5;
            h.ClearModifiers();

            // Restore
            h.Restore(in snapshot);

            Assert.AreEqual(1, h.Level);
            Assert.AreEqual((fp)150m, h.GetStat(StatId.AttackDamage));
            Assert.AreEqual((fp)600m, h.GetStat(StatId.MaxHealth));
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
