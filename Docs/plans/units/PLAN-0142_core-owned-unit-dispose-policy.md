# 核心层单位释放策略

## 本次执行范围

本计划对应原编码 0142 的一次执行：核心层单位释放策略。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [同步生成死亡复活与回池](../../requirements/units/REQ-FEAT-028_unit-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UnitWorld 同步 Spawn；生成当 Tick 可被动参与，主动工作要求 CurrentTick>SpawnLogicTick。RequestEnterDying、RequestRecoverFromDying、ConfirmUnitDeath 是正式入口。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/Gameplay/Unit/Authoring/UnitRuntimeCatalogAsset.cs`：`StatDefinitionAuthoring`、`StatPresetEntryAuthoring`、`LocomotionProfileAuthoring`、`PhysicsProfile2DAuthoring`、`UnitPrototypeAuthoring`、`BakedUnitRuntimeCatalog`、`UnitRuntimeCatalogAsset`。
- `Assets/Scripts/Gameplay/Unit/Prototype/UnitDisposePolicyTable.cs`：`UnitDisposePolicyTable`、`UnitDisposePolicyEntry`。

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

**同步生成死亡复活与回池**

UnitWorld 同步 Spawn；生成当 Tick 可被动参与，主动工作要求 CurrentTick>SpawnLogicTick。RequestEnterDying、RequestRecoverFromDying、ConfirmUnitDeath 是正式入口。

ClearForDeath、ClearForRespawn、ClearForDespawn 按固定顺序；永久 Buff 与装备跨死亡保留所属状态；回池新生命周期不沿用旧身份。

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
- `Assets/Scripts/Bootstrap/Tests/EditMode/MatchScopedContentConfigurationTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `FormalRoot_SelectsOnlyCoreMapAndRequestedHeroes`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void FormalRoot_SelectsOnlyCoreMapAndRequestedHeroes()
        {
            GlobalPrefabTable root = RequireAsset<GlobalPrefabTable>(RootPath);
            var selection = new MatchContentSelection(
                1,
                new[] { 1002, 1001, 1002 });

            IReadOnlyList<GlobalPrefabPartitionReference> both =
                root.SelectPartitions(
                    selection.MapConfigId,
                    selection.HeroConfigIds);

            Assert.That(both.Select(value => value.SubTableAddress), Is.EqualTo(
                new[]
                {
                    "content/table/core",
                    "content/table/map/1",
                    "content/table/hero/1001",
                    "content/table/hero/1002",
                }));
            IReadOnlyList<GlobalPrefabPartitionReference> varusOnly =
                root.SelectPartitions(1, new[] { 1001 });
            Assert.That(
                varusOnly.Any(value => value.OwnerConfigId == 1002),
                Is.False);
            Assert.Throws<InvalidOperationException>(
                () => root.SelectPartitions(1, new[] { 9999 }));
        }
```
- `Assets/Scripts/FrameSync/Tests/UnitAnimationAssetTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `FullMatchAnimationFixtures_HaveCompleteBindableControllers`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void FullMatchAnimationFixtures_HaveCompleteBindableControllers()
        {
            for (int i = 0; i < FixturePrefabs.Length; i++)
                ValidatePrefab(FixturePrefabs[i]);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
