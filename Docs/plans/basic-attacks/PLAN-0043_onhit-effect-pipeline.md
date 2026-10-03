# 攻击特效管线基础

## 本次执行范围

本计划对应原编码 0043 的一次执行：攻击特效管线基础。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [近战远程与攻击特效输出](../../requirements/basic-attacks/REQ-FEAT-038_attack-effects.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [装备主动被动与可重复命中](../../requirements/equipment-shop/REQ-FEAT-054_equipment-effects.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

默认普通攻击采用固定来源和配方；远程输出 ProjectileSpawnRequest；AttackSequenceIndex 为确定性 byte，音效通过独立 SfxEvent。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Attack/AttackHandler.cs`：`AttackPlanStatus`、`AttackTimerResetReason`、`AttackHandler`。
- `Assets/Scripts/Gameplay/Equipment/OnHitRepeatModule.cs`：`OnHitRepeatModule`。
- `Assets/Scripts/Gameplay/Projectile/ProjectileSpawnRequest.cs`：`ProjectileSpawnRequest`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Attack/AttackHandler.cs`：

```csharp
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    public enum AttackPlanStatus : byte
    {
        Unavailable = 0,
        TargetInvalid = 1,
        OutOfRange = 2,
        WaitingForReady = 3,
        Ready = 4,
    }

    public enum AttackTimerResetReason : byte
    {
        AbilityEffect = 0,
        ScriptedRule = 1,
        MoveCancelRecovery = 2,
    }

    public class AttackHandler : UnitHandler, IRollback<AttackSnapshot>
    {
        private const int InvalidLogicTick = -1;

        private AttackSnapshot _state;
        private fp runtimeWindupRatio;
        private int runtimeTickRate;
        private int runtimeSequenceResetIntervalTicks;

        [Header("Authoring")]
        [Tooltip("Fraction of the attack period before impact. Converted to fixed point once at runtime initialization.")]
        [SerializeField, Range(0f, 1f)] private float windupRatio = 0.2f;
        [SerializeField, Min(0)] private int projectileDefId;
        [SerializeField, Min(0)] private int commitSfxEventId;
        [SerializeField] private PresentationAnchor commitSfxAnchor =
            PresentationAnchor.UnitRoot;

        public fp WindupRatio
        {
            get => runtimeWindupRatio;
            set => runtimeWindupRatio = fpmath.clamp(value, fp.zero, fp.one);
        }

        public int ProjectileDefId
        {
            get => projectileDefId;
            set => projectileDefId = value;
        }

        public ProjectileWorld ProjectileWorld { get; set; }

        public int CommitSfxEventId
        {
            get => commitSfxEventId;
            set => commitSfxEventId = value;
        }

        public PresentationAnchor CommitSfxAnchor
        {
            get => commitSfxAnchor;
            set => commitSfxAnchor = value;
        }

        public ref readonly AttackSnapshot Snapshot => ref _state;
        public bool ImpactCommitted => _state.ImpactCommitted;
        public UnitUid CurrentTargetUid => _state.CurrentTargetUid;
        public byte AttackSequenceIndex => _state.AttackSequenceIndex;
        public int LastSuccessfulAttackLogicTick =>
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/Gameplay/Equipment/OnHitRepeatModule.cs`：

```csharp
namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Requests one repeat of eligible On-Hit effects after a stable number of
    /// real attack hits while a required Buff is at full stacks. The repeat is
    /// not an attack and cannot advance this module again.
    /// </summary>
    [System.Serializable]
    public sealed class OnHitRepeatModule : EquipmentEffectModule
    {
        public BuffConfigId RequiredBuffConfigId;
        public int RequiredStacks = 1;
        public int TriggerEvery = 3;

        public override bool CanExecute(
            ref EquipmentEffectExecutionContext context,
            ref EquipmentEffectModuleRuntimeState state)
        {
            return context.Owner?.BuffHandler != null &&
                   context.Dispatch != null &&
                   RequiredBuffConfigId.IsValid &&
                   RequiredStacks > 0 &&
                   TriggerEvery > 0;
        }

        public override void Execute(
            ref EquipmentEffectExecutionContext context,
            ref EquipmentEffectModuleRuntimeState state)
        {
            if (context.OnHit.IsRepeated)
                return;

            int stacks = context.Owner.BuffHandler.TryGetRuntime(
                RequiredBuffConfigId,
                out BuffRuntime runtime)
                    ? runtime.CurrentStacks
                    : 0;
            if (stacks < RequiredStacks)
            {
                state.TriggerCount = 0;
                return;
            }

            state.TriggerCount = checked(state.TriggerCount + 1);
            if (state.TriggerCount < TriggerEvery)
                return;

            state.TriggerCount = 0;
            context.Dispatch.RequestRepeatedOnHit();
        }
    }
}
```

### 输入输出与边界

**近战远程与攻击特效输出**

默认普通攻击采用固定来源和配方；远程输出 ProjectileSpawnRequest；AttackSequenceIndex 为确定性 byte，音效通过独立 SfxEvent。

在途飞弹目标锁定不跟随攻击者换目标；强化攻击、On-Hit 重复必须保留来源与动作身份，防止递归二次触发。

**装备主动被动与可重复命中**

EquipmentEffectDef 内嵌多态 Module，Runtime 持有 EffectUid/Module state；主动一次验证全部模块，再通过仲裁瞬发；On-Hit 重复保留源动作。

每装备最多一个主动 Effect；装备使用使撤销失效；EquipmentTargetPolicy 目前只存在概念提及，待用户确认是否采用及值域。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

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
- `Assets/Scripts/Gameplay/Tests/SunderedSkyEquipmentTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `Catalog_ContainsSunderedSkyTreeWithStatsAndRecipes`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEditor;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class SunderedSkyEquipmentTests
    {
        private SimulationTickContextController controller;
        private UnitWorld world;
        private UnitPrototype prototype;
        private Unit attacker;
        private Unit targetA;
        private Unit targetB;
        private CombatSystem combat;

        [SetUp]
        public void SetUp()
        {
            CombatEvents.Clear();
            controller = new SimulationTickContextController();
            controller.BeginTick(
                10,
                ExecutionMode.ServerAuthority);
            EquipmentDatabase database =
                LoadDatabase();
            var buffRegistry =
                new BuffDefinitionRegistry();
            buffRegistry.Register(
                LoadOverhealBuff());
            world = new UnitWorld
            {
                StatDefinitionTable =
                    CreateStatTable(),
                AttackSequenceResetIntervalTicks = 3,
                EquipmentDatabase = database,
                BuffDefinitions = buffRegistry,
                RandomService =
                    new DeterministicRandomService(42u),
            };
            prototype = CreatePrototype();
            attacker = world.SpawnUnit(
                prototype,
                new TeamId(1),
                10,
                fp.zero,
                fp.zero);
// 方法后续请阅读上述真实源码；这里是节选。
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
