# 表现事件桥接基础

## 本次执行范围

本计划对应原编码 0042 的一次执行：表现事件桥接基础。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [单位视图绑定与语义挂点](../../requirements/presentation-ui/REQ-FEAT-072_unit-view-binding.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [攻击技能动画与插值采样](../../requirements/presentation-ui/REQ-FEAT-073_animation-sampling.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [特效音效与回滚账本](../../requirements/presentation-ui/REQ-FEAT-074_presentation-ledger.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UnitPresentationHost/Registry 和 PresentationSocketSet 只读逻辑；SocketProfile 定义挂点，缺失挂点按可见校验策略处理。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/FrameSync/UnitPresentationHost.cs`：`UnitPresentationHost`。
- `Assets/Scripts/Gameplay/Presentation/SfxEvent.cs`：`PresentationAnchor`、`SfxEvent`。
- `Assets/Scripts/Gameplay/Presentation/VfxEvent.cs`：`VfxEvent`。
- `Assets/Scripts/FrameSync/AudioManager.cs`：`AudioManager`。
- `Assets/Scripts/FrameSync/UnitAnimationDriver.cs`：`UnitAnimationDriver`。
- `Assets/Scripts/FrameSync/VfxManager.cs`：`VfxManager`。
- `Assets/Scripts/Gameplay/Attack/AttackHandler.cs`：`AttackPlanStatus`、`AttackTimerResetReason`、`AttackHandler`。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/FrameSync/UnitPresentationHost.cs`：

```csharp
using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    public sealed class UnitPresentationHost : MonoBehaviour
    {
        [SerializeField] private Unit.Unit _ownerUnit;
        private Unit.UnitUid _registeredUid;

        public Unit.Unit OwnerUnit => _ownerUnit;

        /// <summary>
        /// The UnitAnimationProfile for this unit. Provides Animator parameter hashes.
        /// Stored as a ScriptableObject referenced by the Unit prefab.
        /// </summary>
        [SerializeField] private Presentation.UnitAnimationProfile _profile;
        public Presentation.UnitAnimationProfile Profile => _profile;

        private PresentationSocketSet _sockets;

        /// <summary>
        /// Named socket transforms for VFX / SFX attachment (Presentation
        /// Design v13.2 section 6). Null when the unit prefab has no socket
        /// set; consumers fall back to the host root.
        /// </summary>
        public PresentationSocketSet Sockets =>
            _sockets != null
                ? _sockets
                : _sockets =
                    GetComponent<PresentationSocketSet>();

        public void Bind(Unit.Unit unit)
        {
            if (_registeredUid.IsValid())
                UnitPresentationRegistry.Unregister(_registeredUid);
            _ownerUnit = unit;
            RefreshRegistration();
        }

        private void OnEnable()
        {
            if (_ownerUnit == null)
                _ownerUnit = GetComponent<Unit.Unit>();
            RefreshRegistration();
        }

        private void LateUpdate()
        {
            RefreshRegistration();
        }

        private void OnDisable()
        {
            if (_registeredUid.IsValid())
                UnitPresentationRegistry.Unregister(_registeredUid);
            _registeredUid = default;
        }

        private void RefreshRegistration()
        {
            if (_ownerUnit == null)
                return;

            Unit.UnitUid currentUid = _ownerUnit.UnitUid;
            if (!currentUid.IsValid() || currentUid == _registeredUid)
                return;

            if (_registeredUid.IsValid())
                UnitPresentationRegistry.Unregister(_registeredUid);
            UnitPresentationRegistry.Register(currentUid, this);
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/Gameplay/Presentation/SfxEvent.cs`：

```csharp
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Semantic attachment anchor for SFX events (Attack Design v6.2 2.2,
    /// Presentation Design v13.2 section 5). Values match the common socket
    /// names of PresentationSocketSet; managers resolve the anchor to a
    /// socket Transform on the unit presentation host.
    /// </summary>
    public enum PresentationAnchor : byte
    {
        UnitRoot = 0,
        Head = 1,
        Chest = 2,
        HandR = 3,
        HandL = 4,
        FootR = 5,
        FootL = 6,
        ProjectileOrigin = 7,
    }

    public struct SfxEvent
    {
        public PresentationEventId Id;
        public int SfxDefId;
        public SfxAnchor Anchor;
        public fp2 WorldPosition;
        public UnitUid? AttachToUnit;
        public int SocketKey;
        public fp PitchScale;
        public fp VolumeScale;
    }
}
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
- `Assets/Scripts/FrameSync/Tests/VfxManagerPreloadTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `PreloadAsync_LoadsOnceAndCreatesOneInactivePoolInstance`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void PreloadAsync_LoadsOnceAndCreatesOneInactivePoolInstance()
        {
            var prefab = new GameObject("VfxPrefab");
            var managerObject = new GameObject("VfxManager");
            var library = ScriptableObject.CreateInstance<VfxLibrary>();
            var loader = new FakeLoader(prefab);
            try
            {
                SetEntries(
                    library,
                    new VfxLibrary.VfxPrefabEntry
                    {
                        VfxDefId = 4001,
                        Address = "vfx/4001",
                    });
                VfxManager manager =
                    managerObject.AddComponent<VfxManager>();
                manager.SetLibrary(library);
                manager.SetAssetLoader(loader);

                manager.PreloadAsync(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                Assert.That(loader.AcquireCount, Is.EqualTo(1));
                Assert.That(managerObject.transform.childCount, Is.EqualTo(1));
                Assert.That(
                    managerObject.transform.GetChild(0).gameObject.activeSelf,
                    Is.False);

                manager.PreloadAsync(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                Assert.That(
                    loader.AcquireCount,
                    Is.EqualTo(1),
                    "The manager-owned lease must serve subsequent warmup.");
                Assert.That(
                    managerObject.transform.childCount,
                    Is.EqualTo(1),
                    "Warmup must not grow the pool when one instance is ready.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(library);
            }
        }
```
- `Assets/Scripts/FrameSync/Tests/SnapshotChecksumCompletenessTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `AggregateSnapshot_RestoresIntentDashAndLocomotion`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void AggregateSnapshot_RestoresIntentDashAndLocomotion()
        {
            UnitWorld world = CreateWorld(withPathGrid: true);
            UnitType source = Spawn(world, 100, 0);
            UnitType target = Spawn(world, 101, 0);
            source.Planner.SetIntent(new UnitIntent
            {
                Kind = IntentKind.AttackTarget,
                TargetUnit = target.UnitUid,
                AllowChase = true,
                AllowReplan = true,
            });

            var tick = new SimulationTickContextController();
            tick.BeginTick(2, ExecutionMode.ServerAuthority);
            try
            {
                source.MovementHandler.ApplyDash(
                    new fp2(fp.one, fp.zero), (fp)8, (fp)4);
                Assert.That(
                    source.Locomotion.AcceptRouteRequest(
                        RouteMoveRequest.ToPosition(new fp2(9, 3), (fp)0.5m)),
                    Is.EqualTo(MoveAcceptResult.Accepted));
                var spec = new ActionStartSpec(
                    ActionSlot.Base,
                    ActionResource.BaseAction |
                        ActionResource.Movement |
                        ActionResource.Facing,
                    ActionResource.BaseAction |
                        ActionResource.Movement |
                        ActionResource.Facing,
                    ActionInterruptLevel.Ordinary,
                    true,
                    false);
                source.ActionRuntimes.Start(ActionKind.Move, spec);
            }
            finally
            {
                tick.EndTick();
            }

            var pipeline = new SimulationTickPipeline(world, world.PhysicsWorld);
            GameplaySnapshot snapshot = pipeline.CaptureAggregateSnapshot();

            source.Planner.ClearIntent();
            source.MovementHandler.Restore(MovementSnapshot.Default);
            source.Locomotion.CancelRoute(MoveCancelReason.UserCommand);
            source.ActionRuntimes.ClearWithoutCancel();

            pipeline.RestoreFromSnapshot(snapshot, 3);
// 方法后续请阅读上述真实源码；这里是节选。
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
