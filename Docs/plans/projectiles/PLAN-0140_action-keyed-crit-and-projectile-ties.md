# 动作键暴击与投射物平局

## 本次执行范围

本计划对应原编码 0140 的一次执行：动作键暴击与投射物平局。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [定义与强类型生成黑板](../../requirements/projectiles/REQ-FEAT-039_projectile-definitions.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [提交运动寿命与回收](../../requirements/projectiles/REQ-FEAT-040_projectile-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [命中过滤记忆与等距裁决](../../requirements/projectiles/REQ-FEAT-041_projectile-hit-order.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

ProjectileDef 配置逻辑、PrefabId、阶段模块和形状；SpawnBoard 是静态布局的强类型黑板，RequestSpawn 形成稳定 pending record。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Unit/Core/GameplayParticipantId.cs`：`GameplayParticipantDomain`、`GameplayParticipantId`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。
- `Assets/Scripts/Gameplay/Combat/CombatRequestHeader.cs`：`CombatSourceType`、`SourceDescriptor`、`CombatBuiltinSourceId`、`CombatBuiltinRecipeId`、`CombatRequestHeader`。
- `Assets/Scripts/Gameplay/Combat/CombatSystem.cs`：`CombatSystem`、`ShieldRequestComparer`、`HealRequestComparer`、`DamageRequestComparer`、`DamageAllocationGroup`、`EvaluatedDamage`、`HeroDamageContribution`。
- `Assets/Scripts/Gameplay/Combat/DamageRequest.cs`：`DamageRequest`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitUid.cs`：`UnitUid`。

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

`Assets/Scripts/Gameplay/Unit/Core/GameplayParticipantId.cs`：

```csharp
using System;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    public enum GameplayParticipantDomain : byte
    {
        Invalid = 0,
        InitialSpawn = 1,
        MinionWave = 2,
        JungleCamp = 3,
        DerivedSpawn = 4,
        Explicit = 5,
    }

    /// <summary>
    /// Immutable Gameplay identity that survives technical UnitUid/PrefabId
    /// relabeling. Its fields come from stable spawn provenance, never Unity
    /// registration or object identity (D-050).
    /// </summary>
    public readonly struct GameplayParticipantId :
        IEquatable<GameplayParticipantId>,
        IComparable<GameplayParticipantId>
    {
        public readonly GameplayParticipantDomain Domain;
        public readonly int Scope;
        public readonly int Generation;
        public readonly int Ordinal;

        public GameplayParticipantId(
            GameplayParticipantDomain domain,
            int scope,
            int generation,
            int ordinal)
        {
            Domain = domain;
            Scope = scope;
            Generation = generation;
            Ordinal = ordinal;
        }

        public bool IsValid =>
            Domain > GameplayParticipantDomain.Invalid &&
            Domain <= GameplayParticipantDomain.Explicit &&
            Generation >= 0 &&
            Ordinal >= 0;

        public static GameplayParticipantId InitialSpawn(
            int stableSpawnOrder) =>
            new GameplayParticipantId(
                GameplayParticipantDomain.InitialSpawn,
                stableSpawnOrder,
                0,
                0);

        public static GameplayParticipantId MinionWave(
            TeamId team,
            ushort laneId,
            int spawnLogicTick,
            int stableEntryIndex) =>
            new GameplayParticipantId(
                GameplayParticipantDomain.MinionWave,
                (team.Value << 16) | laneId,
                spawnLogicTick,
                stableEntryIndex);

        public static GameplayParticipantId JungleCamp(
            int campId,
            int spawnLogicTick,
            int memberSlot) =>
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**定义与强类型生成黑板**

ProjectileDef 配置逻辑、PrefabId、阶段模块和形状；SpawnBoard 是静态布局的强类型黑板，RequestSpawn 形成稳定 pending record。

逻辑配置 ID 与 PrefabId 不合并；不使用任意 object 字典；OnHitDamageOverride、MaxLifetime 与动作来源均保存到所属快照。

**提交运动寿命与回收**

ProjectileWorld 拥有每 Tick spawn sequence；CommitSpawns 是唯一创建入口，随后 AdvanceMotion、UpdateLifecycle、ResolveHits、EmitEffects、FlushDestroy。

FrameSync 不维护第二个投射物 BeginTick 序列；Pending 与 Active 状态均可恢复；池化实体和逻辑实例各有释放 owner。

**命中过滤记忆与等距裁决**

ProjectileHitQueryService 使用 UnitFinalGrid、扫掠形状和正式 TargetFilter；ProjectileHitMemory 保存跨 Tick 命中事实，命中结果进入 Combat 或所属效果端口。

距离为主键；等距按动作/参与者纯哈希；记忆不能依赖候选枚举顺序；结构技能效果由中央准入兜底拒绝。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

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
- `Assets/Scripts/Gameplay/Tests/UnitWorldTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `InternalRegistration_PublicLookupReturnsSameRuntime`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void InternalRegistration_PublicLookupReturnsSameRuntime()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(300, 9, 1), UnitKind.Hero, 0, TeamId.Neutral);

            world.RegisterUnit(unit);

            Assert.That(world.TryGetUnit(unit.UnitUid, out Unit resolved), Is.True);
            Assert.That(resolved, Is.SameAs(unit));
            Assert.That(world.TryGetUnit(new UnitUid(300, 9, 2), out _), Is.False);
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

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
