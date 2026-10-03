# 工程资源与代码清理

## 本次执行范围

本计划对应原编码 0163 的一次执行：工程资源与代码清理。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [按对局加载内容闭包](../../requirements/configuration-content/REQ-FEAT-018_match-content-closure.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

GlobalPrefabTable 是唯一聚合；生产根索引路径型子表，Addressables 在 Tick 0 前异步加载，组成对局内非序列化同步表。MapConfigId 和按 ID 排序的英雄集合决定闭包。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Ability/AbilitySignal.cs`：`AbilitySignal`、`AbilitySignalVerb`、`AimKind`、`AimSnapshot`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentTag.cs`：`EquipmentTagUid`。
- `Assets/Scripts/Gameplay/Equipment/PlayerSlot.cs`：`PlayerSlot`。
- `Assets/Scripts/Gameplay/Unit/Core/BehaviorPlanner.cs`：`BehaviorPlanner`。
- `Assets/Scripts/RuntimeConfig/GlobalPrefabSubTableAsset.cs`：`GlobalPrefabPartitionKind`、`MatchContentAssetKind`、`MatchContentAssetAddress`、`GlobalPrefabPartitionReference`、`GlobalPrefabSubTableAsset`。

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

`Assets/Scripts/Gameplay/Ability/AbilitySignal.cs`：

```csharp
using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public struct AbilitySignal
    {
        public byte Slot;
        public AbilitySignalVerb Verb;
        public AimSnapshot Aim;
        public static readonly AbilitySignal None = default;
    }

    public enum AbilitySignalVerb : byte
    {
        Focus = 0,
        Commit = 1,
        Cancel = 2,
    }

    public enum AimKind : byte
    {
        None = 0,
        Self = 1,
        Point = 2,
        Unit = 3,
        Direction = 4,
    }

    public readonly struct AimSnapshot : IEquatable<AimSnapshot>
    {
        public readonly AimKind Kind;
        public readonly UnitUid TargetUnitUid;
        public readonly fp2 TargetPoint;
        public readonly fp2 Direction;

        private AimSnapshot(
            AimKind kind,
            UnitUid targetUnitUid,
            fp2 targetPoint,
            fp2 direction)
        {
            Kind = kind;
            TargetUnitUid = targetUnitUid;
            TargetPoint = targetPoint;
            Direction = direction;
        }

        public static AimSnapshot Self => new AimSnapshot(
            AimKind.Self, default, default, default);

        public static AimSnapshot ForPoint(fp2 targetPoint) => new AimSnapshot(
            AimKind.Point, default, targetPoint, default);

        public static AimSnapshot ForUnit(UnitUid targetUnitUid)
        {
            if (!targetUnitUid.IsValid())
            {
                throw new ArgumentException("Unit aim requires a valid UnitUid.", nameof(targetUnitUid));
            }

            return new AimSnapshot(AimKind.Unit, targetUnitUid, default, default);
        }

        public static AimSnapshot ForDirection(fp2 direction)
        {
            if (!Physics.PhysicsGeometry2D.TryCreateFacing(
                    direction, out fp2 normalized, out _))
            {
                throw new ArgumentException(
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**按对局加载内容闭包**

GlobalPrefabTable 是唯一聚合；生产根索引路径型子表，Addressables 在 Tick 0 前异步加载，组成对局内非序列化同步表。MapConfigId 和按 ID 排序的英雄集合决定闭包。

缺分区、重复 ID、版本/哈希或阵容不一致在初始快照前失败；不回退全量旧目录；禁止 WaitForCompletion。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/EquipmentTagAndCatalogTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `UniqueTagInTable_ConflictingPurchase_Rejected`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void UniqueTagInTable_ConflictingPurchase_Rejected()
        {
            EquipmentTagDefinition bootsTag =
                EquipmentTagDefinition.Create(
                    "Boots",
                    1001);
            EquipmentDefinition bootsA =
                MakeDefinition(1, "BootsA", 300, bootsTag);
            EquipmentDefinition bootsB =
                MakeDefinition(2, "BootsB", 400, bootsTag);

            TestContext context =
                CreateContext(
                    bootsA,
                    bootsB,
                    bootsTag);
            Assert.IsTrue(
                context.Handler.Add(bootsA, 0));

            Assert.IsFalse(
                context.Shop.TryBuildPurchasePlan(
                    0,
                    bootsB.Id,
                    1000,
                    context.Handler,
                    out _,
                    out EquipmentShopFailureReason failure));
            Assert.That(
                failure,
                Is.EqualTo(
                    EquipmentShopFailureReason
                        .UniqueTagConflict));
        }
```
- `Assets/Scripts/Gameplay/Tests/AatroxFormalContentTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `CombinedAbilityCatalog_BakesAatroxAndVarus`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void CombinedAbilityCatalog_BakesAatroxAndVarus()
        {
            AbilityRuntimeCatalogAsset catalog = Load<AbilityRuntimeCatalogAsset>(
                Root + "Abilities/FormalHeroAbilityRuntimeCatalog.asset");
            AbilityDefinitionRegistry registry = catalog.BakeOrThrow();

            for (int id = 10021; id <= 10024; id++)
                Assert.That(registry.TryGet(id, out _), Is.True, $"Ability {id}");
            Assert.That(registry.TryGetPassive(10020, out _), Is.True);
            Assert.That(registry.TryGet(10011, out _), Is.True, "Varus Q remains registered");
            Assert.That(registry.TryGetSlot(0, out AbilitySlotDef qSlot), Is.True);
            Assert.That(qSlot.AbilityIds, Does.Contain(10011));
            Assert.That(qSlot.AbilityIds, Does.Contain(10021));
        }
```
- `Assets/Scripts/Bootstrap/Tests/EditMode/FrameworkSmokeBootstrapTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `Bootstrap_BakesAssetsSpawnsUnitAndBoundsCatchUpTicks`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void Bootstrap_BakesAssetsSpawnsUnitAndBoundsCatchUpTicks()
        {
            GlobalGameplayData global = AssetDatabase.LoadAssetAtPath<GlobalGameplayData>(
                "Assets/Config/Formal/GlobalGameplayData.asset");
            UnitRuntimeCatalogAsset catalog =
                AssetDatabase.LoadAssetAtPath<UnitRuntimeCatalogAsset>(
                    "Assets/Config/Formal/FullMatchUnitRuntimeCatalog.asset");
            AbilityRuntimeCatalogAsset abilityCatalog =
                AssetDatabase.LoadAssetAtPath<AbilityRuntimeCatalogAsset>(
                    "Assets/Config/Formal/Abilities/VarusAbilityRuntimeCatalog.asset");
            DeterministicMapConfig mapConfig =
                AssetDatabase.LoadAssetAtPath<DeterministicMapConfig>(
                    "Assets/Config/Formal/FullMatchDeterministicMapConfig.asset");
            Assert.That(global, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(abilityCatalog, Is.Not.Null);
            Assert.That(mapConfig, Is.Not.Null);

            GlobalGameplayData testGlobal =
                CreateLegacyTestGlobal(global, out GlobalPrefabTable runtimeTable);
            var root = new GameObject("FrameworkSmokeBootstrapTest");
            try
            {
                GameBootstrap bootstrap = root.AddComponent<GameBootstrap>();
                SetField(bootstrap, "globalGameplayData", testGlobal);
                SetField(bootstrap, "unitRuntimeCatalog", catalog);
                SetField(
                    bootstrap,
                    "abilityRuntimeCatalog",
                    abilityCatalog);
                SetField(
                    bootstrap,
                    "deterministicMapConfig",
                    mapConfig);
                SetField(bootstrap, "dedicatedServer", true);
                SetField(bootstrap, "driveSimulationFromUnityUpdate", false);
                SetField(bootstrap, "initialUnitSpawns", new System.Collections.Generic.List<
                    InitialUnitSpawnAuthoring>
                {
                    new InitialUnitSpawnAuthoring
                    {
                        StableSpawnOrder = 0,
                        UnitPrototypeId = 1001,
                        TeamId = 1,
                        UseMapSpawnPoint = true,
                        SpawnPointId = 0,
                        PlayerControlled = true,
                        PlayerSlot = 0,
                    },
                    new InitialUnitSpawnAuthoring
// 方法后续请阅读上述真实源码；这里是节选。
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
