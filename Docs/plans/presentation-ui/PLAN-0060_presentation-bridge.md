# 表现桥接补全

## 本次执行范围

本计划对应原编码 0060 的一次执行：表现桥接补全。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [单位视图绑定与语义挂点](../../requirements/presentation-ui/REQ-FEAT-072_unit-view-binding.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [攻击技能动画与插值采样](../../requirements/presentation-ui/REQ-FEAT-073_animation-sampling.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [特效音效与回滚账本](../../requirements/presentation-ui/REQ-FEAT-074_presentation-ledger.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UnitPresentationHost/Registry 和 PresentationSocketSet 只读逻辑；SocketProfile 定义挂点，缺失挂点按可见校验策略处理。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/FrameSync/AudioManager.cs`：`AudioManager`。
- `Assets/Scripts/FrameSync/VfxManager.cs`：`VfxManager`。
- `Assets/Scripts/Gameplay/Presentation/SfxEvent.cs`：`PresentationAnchor`、`SfxEvent`。
- `Assets/Scripts/FrameSync/UnitAnimationDriver.cs`：`UnitAnimationDriver`。
- `Assets/Scripts/Bootstrap/PresentationEventDispatcher.cs`：`PresentationEventDispatcher`、`PresentationEventHistory`、`IVfxHandler`、`ISfxHandler`。
- `Assets/Scripts/FrameSync/AudioLibrary.cs`：`AudioLibrary`、`AudioClipEntry`。
- `Assets/Scripts/FrameSync/VfxLibrary.cs`：`VfxLibrary`、`VfxPrefabEntry`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/FrameSync/AudioManager.cs`：

```csharp
using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private AudioLibrary _library;
        [SerializeField] private int _defaultPoolSize = 8;
        [SerializeField] private int _maxPerDefId = 4;

        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private readonly Dictionary<int, int> _activeCounts = new Dictionary<int, int>();
        private readonly Dictionary<int, IPresentationAssetLease<AudioClip>>
            _clipLeases =
                new Dictionary<int, IPresentationAssetLease<AudioClip>>();
        private readonly Dictionary<int, Task<IPresentationAssetLease<AudioClip>>>
            _pendingLoads =
                new Dictionary<int, Task<IPresentationAssetLease<AudioClip>>>();
        private IClientPresentationAssetLoader _assetLoader;
        private CancellationTokenSource _lifetimeCancellation;

        private void Awake()
        {
            _lifetimeCancellation = new CancellationTokenSource();
            for (int i = 0; i < _defaultPoolSize; i++)
            {
                var go = new GameObject("AudioSource_" + i.ToString());
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                _pool.Add(source);
            }
        }

        public void SetLibrary(AudioLibrary library)
        {
            _library = library;
        }

        public void SetAssetLoader(IClientPresentationAssetLoader assetLoader)
        {
            _assetLoader = assetLoader;
        }

        public async void PlayOrReconcile(Unit.SfxEvent evt)
        {
            Debug.Log(
                $"[AudioManager] enter id={evt.SfxDefId} " +
                $"library={_library != null}");
            if (_library == null)
            {
                Debug.Log(string.Format("[AudioManager] SFX {0} (no AudioLibrary configured)", evt.SfxDefId));
                return;
            }

            AudioClip clip;
            try
            {
                clip = await GetClipAsync(evt.SfxDefId);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/FrameSync/VfxManager.cs`：

```csharp
using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FrameSyncMoba.FrameSync
{
    public sealed class VfxManager : MonoBehaviour
    {
        [SerializeField] private VfxLibrary _library;
        [SerializeField] private int _defaultPoolSize = 16;

        private readonly Dictionary<int, Queue<GameObject>> _poolByDefId =
            new Dictionary<int, Queue<GameObject>>();
        private readonly Dictionary<int, IPresentationAssetLease<GameObject>>
            _prefabLeases =
                new Dictionary<int, IPresentationAssetLease<GameObject>>();
        private readonly Dictionary<int, Task<IPresentationAssetLease<GameObject>>>
            _pendingLoads =
                new Dictionary<int, Task<IPresentationAssetLease<GameObject>>>();
        private IClientPresentationAssetLoader _assetLoader;
        private CancellationTokenSource _lifetimeCancellation;

        private void Awake()
        {
            _lifetimeCancellation = new CancellationTokenSource();
        }

        public void SetAssetLoader(IClientPresentationAssetLoader assetLoader)
        {
            _assetLoader = assetLoader;
        }

        public void SetLibrary(VfxLibrary library)
        {
            _library = library;
            foreach (Queue<GameObject> queue in _poolByDefId.Values)
                while (queue.Count > 0)
                    DestroyOwnedInstance(queue.Dequeue());
            ReleasePrefabLeases();
            _poolByDefId.Clear();
        }

        /// <summary>
        /// Loads shared entries and entries owned by selected heroes and
        /// creates one inactive pool instance before Gameplay can emit its
        /// first event. The manager retains both leases and instances for its
        /// normal lifetime, so warmup does not introduce a second resource
        /// owner.
        /// </summary>
        public async Task PreloadAsync(
            CancellationToken cancellationToken)
        {
            await PreloadAsync(
                null,
                cancellationToken);
        }

        /// <summary>
        /// Loads only shared VFX and entries owned by one of the selected
        /// heroes. A null hero list preserves legacy/full-library warmup for
        /// standalone scenes that do not have a match content scope.
        /// </summary>
        public async Task PreloadAsync(
            IReadOnlyList<int> selectedHeroConfigIds,
            CancellationToken cancellationToken)
        {
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

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

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
