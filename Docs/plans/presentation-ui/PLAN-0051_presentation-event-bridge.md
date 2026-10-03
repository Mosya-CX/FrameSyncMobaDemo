# 表现事件桥接集成

## 本次执行范围

本计划对应原编码 0051 的一次执行：表现事件桥接集成。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [单位视图绑定与语义挂点](../../requirements/presentation-ui/REQ-FEAT-072_unit-view-binding.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [攻击技能动画与插值采样](../../requirements/presentation-ui/REQ-FEAT-073_animation-sampling.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [特效音效与回滚账本](../../requirements/presentation-ui/REQ-FEAT-074_presentation-ledger.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UnitPresentationHost/Registry 和 PresentationSocketSet 只读逻辑；SocketProfile 定义挂点，缺失挂点按可见校验策略处理。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Bootstrap/PresentationEventDispatcher.cs`：`PresentationEventDispatcher`、`PresentationEventHistory`、`IVfxHandler`、`ISfxHandler`。
- `Assets/Scripts/Bootstrap/AttackSfxHandler.cs`：`AttackSfxHandler`。
- `Assets/Scripts/Bootstrap/DeathPresenter.cs`：`DeathPresenter`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/Bootstrap/HitReactionPresenter.cs`：`HitReactionPresenter`。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitUid.cs`：`UnitUid`。
- `Assets/Scripts/Gameplay/Presentation/VisualEventOutput.cs`：`VisualEventOutput`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Bootstrap/PresentationEventDispatcher.cs`：

```csharp
        public void DispatchCurrentFrame()
        {
            var vfxEvents = VisualEventOutput.ConsumeVfxEvents();
            for (int i = 0; i < vfxEvents.Count; i++)
            {
                VfxEvent evt = vfxEvents[i];
                if (!_vfxHistory.TryConsume(
                        evt.Id))
                    continue;
                for (int j = 0; j < _vfxHandlers.Count; j++)
                    _vfxHandlers[j].OnVfxEvent(evt);
            }

            var sfxEvents = VisualEventOutput.ConsumeSfxEvents();
            for (int i = 0; i < sfxEvents.Count; i++)
            {
                SfxEvent evt = sfxEvents[i];
                if (!_sfxHistory.TryConsume(
                        evt.Id))
                    continue;
                for (int j = 0; j < _sfxHandlers.Count; j++)
                    _sfxHandlers[j].OnSfxEvent(evt);
            }
        }
```

`Assets/Scripts/Bootstrap/AttackSfxHandler.cs`：

```csharp
using System.Collections.Generic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Bootstrap bridge from the presentation SFX stream to the global
    /// AudioManager (Presentation Design v13.2 section 5). Registered with
    /// the PresentationEventDispatcher; every SfxEvent is forwarded to the
    /// pooled AudioManager for playback. Units provide only sockets; they
    /// never hold or manage AudioSource instances.
    /// </summary>
    public sealed class AttackSfxHandler : MonoBehaviour, ISfxHandler
    {
        [SerializeField] private AudioManager audioManager;
        private static bool missingManagerWarned;

        public void SetAudioManager(AudioManager manager)
        {
            audioManager = manager;
        }

        public void OnSfxEvent(in SfxEvent evt)
        {
            Debug.Log(
                $"[AttackSfx] bridge id={evt.SfxDefId} " +
                $"mgr={audioManager != null} " +
                $"anchor={evt.SocketKey}");
            if (audioManager == null)
            {
                if (!missingManagerWarned)
                {
                    missingManagerWarned = true;
                    Debug.LogWarning(
                        "[AttackSfxHandler] no AudioManager configured; " +
                        "SFX events are skipped.");
                }
                return;
            }
            audioManager.PlayOrReconcile(evt);
        }
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
- `Assets/Scripts/Gameplay/Tests/NonHeroTopologyTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `MinionWave_ExpandsCanonicalTeamLaneMemberOrder`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void MinionWave_ExpandsCanonicalTeamLaneMemberOrder()
        {
            var schedule = new BakedMinionWaveConfig(
                30,
                0,
                new[]
                {
                    new MinionWavePhase
                    {
                        StartWaveIndex = 0,
                        CompositionCycle = new[]
                        {
                            new MinionWaveComposition
                            {
                                Members = new[]
                                {
                                    new MinionWaveMember
                                    {
                                        UnitPrototypeId = 20,
                                        Count = 2,
                                        FirstSpawnOffsetTicks = 5,
                                        SpawnStepTicks = 1,
                                    },
                                },
                            },
                        },
                    },
                });
            var lane = new LaneRuntimeData(
                3,
                new[]
                {
                    new LaneTeamSpawnData(
                        new TeamId(1),
                        new fp2(1, 2),
                        new fp2(1, 0)),
                    new LaneTeamSpawnData(
                        new TeamId(2),
                        new fp2(9, 2),
                        new fp2(-1, 0)),
                },
                new[] { fp2.zero, new fp2(10, 0) },
                (fp)2m);
            var system = new MinionSystem(
                new UnitWorld(),
                schedule,
                new[] { lane });
            BeginTick(0);

            system.TickLogic();
// 方法后续请阅读上述真实源码；这里是节选。
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
