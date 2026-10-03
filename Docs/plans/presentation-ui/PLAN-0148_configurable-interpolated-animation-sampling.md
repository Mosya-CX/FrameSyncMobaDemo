# 可配置插值动画采样

## 本次执行范围

本计划对应原编码 0148 的一次执行：可配置插值动画采样。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [攻击技能动画与插值采样](../../requirements/presentation-ui/REQ-FEAT-073_animation-sampling.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UnitAnimationDriver 读取 Attack 锁定时间与 AbilityCastView；客户端默认 20 Hz 插值，Bootstrap 发布按 UnitWorld 拥有的连续逻辑时间投影。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/FrameSync/UnitAnimationDriver.cs`：`UnitAnimationDriver`。
- `Assets/Scripts/Gameplay/Attack/AttackHandler.cs`：`AttackPlanStatus`、`AttackTimerResetReason`、`AttackHandler`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/FrameSync/AuthorityFrame.cs`：`AuthorityFrameFlags`、`AuthorityFrame`、`CanonicalCommandCodec`、`CanonicalReader`。
- `Assets/Scripts/FrameSync/FrameSyncGameRuntime.cs`：`FrameSyncGameRuntime`。

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

`Assets/Scripts/FrameSync/UnitAnimationDriver.cs`：

```csharp
        private void Update()
        {
            if (_host == null ||
                _host.OwnerUnit == null ||
                _host.OwnerUnit.World == null ||
                _animator == null)
                return;

            UnitType unit = _host.OwnerUnit;
            if (_profile == null)
                _profile = _host.Profile;
            bool isMonsterAlerted = false;
            if (unit.UnitKind == UnitKind.Monster &&
                unit.World != null &&
                unit.World.TryGetAIController(
                    unit.UnitUid,
                    out UnitAIController aiController) &&
                aiController is MonsterAIController monsterController)
            {
                isMonsterAlerted =
                    monsterController.AIState ==
                        MonsterAIState.EngageTarget ||
                    monsterController.HasNearbyAlertTarget(
                        _wasMonsterAlerted);
            }
            SetBool(
                IsMonsterAlertedHash,
                isMonsterAlerted);
            bool lifeStateChanged =
                unit.LifeState != _lastLifeState;
            bool isDead = unit.LifeState == LifeState.Dead
                       || unit.LifeState == LifeState.Respawning;
            SetInteger(
                HashOrDefault(
                    _profile?.LifeStateHash ?? 0,
                    "LifeState"),
                (int)unit.LifeState);
            SetBool(
                HashOrDefault(
                    _profile?.IsControlledHash ?? 0,
                    "IsControlled"),
                unit.ControlledByPlayerSlot >= 0);

            if (lifeStateChanged)
            {
                // LifeState parameter already drives the Animator transitions
                // (AnyState -> Death, Death -> Idle on respawn). No code-side
                // CrossFade is needed.
                _lastLifeState = unit.LifeState;
            }

            if (isDead)
            {
                SetBool(HashOrDefault(_profile?.IsMovingHash ?? 0, "IsMoving"), false);
                SetFloat(HashOrDefault(_profile?.MoveSpeedHash ?? 0, "MoveSpeed"), 0f);
                SetBool(HashOrDefault(_profile?.IsAttackingHash ?? 0, "IsAttacking"), false);
                SetBool(HashOrDefault(_profile?.IsAttackRecoveringHash ?? 0, "IsAttackRecovering"), false);
                SetBool(HashOrDefault(_profile?.IsEmpoweredAttackHash ?? 0, "IsEmpoweredAttack"), false);
                SetBool(HashOrDefault(_profile?.IsCastingHash ?? 0, "IsCasting"), false);
                SetBool(HashOrDefault(_profile?.IsPassiveReadyHash ?? 0, "IsPassiveReady"), false);
                SetBool(HashOrDefault(_profile?.IsAnimationVariantActiveHash ?? 0, "IsAnimationVariantActive"), false);
                SetBool(IsMonsterAlertedHash, false);
                _wasMoving = false;
                _wasMonsterAlerted = false;
                _wasCasting = false;
                _wasAttacking = false;
                _wasPassiveReady = false;
                _wasAnimationVariantActive = false;
                _attackProgressSampler.Clear();
                _loopProgressSampler.Clear();
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**攻击技能动画与插值采样**

UnitAnimationDriver 读取 Attack 锁定时间与 AbilityCastView；客户端默认 20 Hz 插值，Bootstrap 发布按 UnitWorld 拥有的连续逻辑时间投影。

不得跨未 Commit 的 Impact 或 Ready；loop 相位由逻辑 epoch 和实时倍率重建；未知 TickRate 不硬回退 30 Hz；独立采样不新增 Gameplay Tick。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/FrameSync/Tests/UnitAnimationAssetTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `FullMatchAnimationFixtures_HaveCompleteBindableControllers`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void FullMatchAnimationFixtures_HaveCompleteBindableControllers()
        {
            for (int i = 0; i < FixturePrefabs.Length; i++)
                ValidatePrefab(FixturePrefabs[i]);
        }
```
- `Assets/Scripts/FrameSync/Tests/BootstrapDeterminismProbeTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `ServerFirstTick_MatchesClientPredictionFirstTick`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void ServerFirstTick_MatchesClientPredictionFirstTick()
        {
            BakedGlobalGameplayData baked =
                AssetDatabase
                    .LoadAssetAtPath<GlobalGameplayData>(
                        GlobalDataPath)
                    .BakeOrThrow();
            UnitRuntimeCatalogAsset unitCatalog =
                AssetDatabase.LoadAssetAtPath<
                    UnitRuntimeCatalogAsset>(
                    UnitCatalogPath);
            AbilityRuntimeCatalogAsset abilityCatalog =
                AssetDatabase.LoadAssetAtPath<
                    AbilityRuntimeCatalogAsset>(
                    AbilityCatalogPath);
            BuffCatalogAsset buffCatalog =
                AssetDatabase.LoadAssetAtPath<
                    BuffCatalogAsset>(
                    BuffCatalogPath);
            GlobalPrefabTable resolvedPrefabTable =
                BuildResolvedFormalPrefabTable(
                    baked.PrefabTable);

            // Server: authoritative build then execute first tick.
            UnitWorld server = CreateWorld(
                baked,
                resolvedPrefabTable,
                unitCatalog,
                abilityCatalog,
                buffCatalog);
            var serverRuntime =
                new FrameSyncGameRuntime(
                    server,
                    server.PhysicsWorld,
                    baked);
            QueueHeroes(serverRuntime);
            serverRuntime.ConfigureMatchStart(
                3,
                12345u,
                2,
                baked.InitialEarnedGold);
            UnitUid[] serverSpawned =
                serverRuntime
                    .MaterializeInitialSpawnsForBootstrap(
                        3);
            serverRuntime.ConfigurePlayerSlotMappings(
                new[]
                {
                    new PlayerSlotUnitMapping(
                        0,
// 方法后续请阅读上述真实源码；这里是节选。
```
- `Assets/Scripts/Gameplay/Tests/AttackHandlerTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `FormalDeathInvalidation_AtomicallyClearsWindupAndMainRuntime`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void FormalDeathInvalidation_AtomicallyClearsWindupAndMainRuntime()
        {
            ActionSubmitResult started = attacker.Arbiter.Submit(
                new AttackActionRequest(target.UnitUid));
            Assert.That(started.IsGranted, Is.True);
            Assert.That(attacker.AttackHandler.CurrentTargetUid,
                Is.EqualTo(target.UnitUid));
            Assert.That(attacker.ActionRuntimes.Main.IsOccupied, Is.True);
            Assert.That(attacker.ActionRuntimes.Main.Kind,
                Is.EqualTo(ActionKind.Attack));

            world.RequestEnterDying(target);
            world.ConfirmUnitDeath(target);
            world.ApplyFormalDeathActionInvalidations(new[]
            {
                new DeathResult
                {
                    VictimUid = target.UnitUid,
                    DeathSequenceInTick = 0,
                    DeathLogicTick = 10,
                },
            });

            Assert.That(attacker.AttackHandler.CurrentTargetUid.IsValid(),
                Is.False);
            Assert.That(attacker.ActionRuntimes.Main.IsOccupied, Is.False);

            var attackSnapshot = default(AttackSnapshot);
            var runtimeSnapshot = default(ActionRuntimeSetSnapshot);
            attacker.AttackHandler.Capture(ref attackSnapshot);
            attacker.ActionRuntimes.Capture(ref runtimeSnapshot);
            attacker.AttackHandler.Restore(attackSnapshot);
            attacker.ActionRuntimes.Restore(runtimeSnapshot);

            Assert.DoesNotThrow(() =>
            {
                attacker.AttackHandler.Resolve(new RollbackContext(
                    10,
                    ExecutionMode.ClientReplay));
                attacker.ActionRuntimes.Resolve();
            });
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
