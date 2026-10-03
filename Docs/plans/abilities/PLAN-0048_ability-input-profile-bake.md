# 技能输入配置烘焙

## 本次执行范围

本计划对应原编码 0048 的一次执行：技能输入配置烘焙。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [技能信号与会话状态](../../requirements/abilities/REQ-FEAT-042_ability-signal-session.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [施法模型与阶段推进](../../requirements/abilities/REQ-FEAT-043_cast-stages.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [阶段效果与确定性黑板](../../requirements/abilities/REQ-FEAT-044_stage-effects.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能目录消耗冷却与升级](../../requirements/abilities/REQ-FEAT-045_ability-catalog.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [主动附带被动与固定被动](../../requirements/abilities/REQ-FEAT-046_ability-passives.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [设备事件缓冲与 UI 门禁](../../requirements/player-input/REQ-FEAT-078_input-event-buffer.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能输入组合与提交去重](../../requirements/player-input/REQ-FEAT-079_ability-input.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [世界鼠标 Aim 与类型化 Request](../../requirements/player-input/REQ-FEAT-080_aim-requests.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能指示器与本地生命周期](../../requirements/player-input/REQ-FEAT-081_skill-indicator.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

AbilityHandler 接受 AbilitySignal，AbilityRuntime 常驻，AbilitySession 只承载本次施法；Session 结束回传执行器，外部只读 AbilityCastView。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Ability/CastModelDef.cs`：`CastModelKind`、`CastStage`、`CastModelDef`、`CommitCastModelDef`、`HoldReleaseCastModelDef`、`HoldTimeoutPolicy`、`ChannelCastModelDef`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/Gameplay/Ability/AbilityDef.cs`：`AbilityDef`、`AbilityLevelValue`、`AbilityCostTiming`、`AbilityCostPlan`、`AbilityCastContext`、`AbilityCastConditionDef`。
- `Assets/Scripts/PlayerInput/PlayerCommandRequester.cs`：`IPlayerGameplayCommandRequester`、`IPlayerShopCommandRequester`、`IPlayerAbilityInputProfileProvider`、`IPlayerAbilityAimProfileProvider`、`ILocalAbilityRuntimeView`、`GameplayCommandRequestReceipt`、`LocalAbilityInputStateKind`。
- `Assets/Scripts/PlayerInput/PlayerInputController.cs`：`PlayerInputController`。
- `Assets/Scripts/Gameplay/Ability/AbilityDefinitionRegistry.cs`：`AbilityDefinitionRegistry`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Ability/CastModelDef.cs`：

```csharp
using FrameSyncMoba.Deterministic;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    public enum CastModelKind : byte
    {
        Commit = 0,
        HoldRelease = 1,
        Channel = 2,
        ActiveSignal = 3,
        Toggle = 4,
        GroundTarget = 5,
        VectorTarget = 6,
        SequentialRecast = 7,
    }

    public struct CastStage
    {
        public byte StageKey;
        public StageDef Def;
        public int DurationTicks;
        public bool NotifyAbilityCastOnEnter;
        public bool Interruptible;
        /// <summary>
        /// When true, the caster cannot issue voluntary Move / Attack while
        /// this cast stage is active (cast windup lock). Movable-cast stages
        /// (e.g. a charge Hold) set this to false.
        /// </summary>
        public bool LockMovement;
        /// <summary>Per-cast-stage client Addressables icon address.</summary>
        public string IconAddressOverride;
        public bool IsValid => Def != null && DurationTicks >= 0;
    }

    public abstract class CastModelDef
    {
        public CastModelKind Kind { get; protected set; }
        public abstract CastStage? GetStage(byte stageKey);
        public abstract int? HandleSignal(AbilitySignal signal, byte currentStageKey);

        public virtual bool CanHandleSignal(
            AbilitySignal signal,
            byte currentStageKey,
            int stageElapsedTicks) => true;
        public abstract byte? ResolveIndicatorStage(byte currentStageKey);
        public abstract bool TryInterrupt(byte currentStageKey);

        /// <summary>
        /// Resolves the deterministic transition produced by a completed or
        /// timed-out stage. A null result ends the session and starts its
        /// normal cooldown. Existing cast models preserve their historical
        /// completion-as-Commit behavior; models with explicit recast
        /// windows override this method so a timeout never invents input.
        /// </summary>
        public virtual int? ResolveStageEnd(
            byte currentStageKey,
            bool timedOut)
        {
            return HandleSignal(
                new AbilitySignal
                {
                    Verb = AbilitySignalVerb.Commit,
                },
                currentStageKey);
        }
    }

    public sealed class CommitCastModelDef : CastModelDef
    {
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

**技能信号与会话状态**

AbilityHandler 接受 AbilitySignal，AbilityRuntime 常驻，AbilitySession 只承载本次施法；Session 结束回传执行器，外部只读 AbilityCastView。

HandleSignal 返回是否接受，不让 Planner 私自推进 Session；同 Tick Focus 和 Commit 需正式 CommandSeq 顺序。

**施法模型与阶段推进**

CastModelDef 决定 CastStageKey、阶段进入/退出及 Timeout，StageDef 独立返回完成或失败；每阶段明确时长，0 Tick 阶段有限推进。

不重建已删除 CastFlowDef、StageDriver；切换不必触发主动施法事件；蓄力 timeout 的自动释放或取消及退款由模型明确。

**阶段效果与确定性黑板**

StageDef 用 AbilityStageContext、AbilityPorts 和受限 AbilityBlackboard；技能范围约束按需启用，效果写入所属系统，不增加 EffectPlan/EffectStep。

Handle 由创建 Effect 自己清理；Stage 成长不放在 AbilityDef 全局重复字段；结构过滤配置之外仍保留中央准入。

**技能目录消耗冷却与升级**

AbilityBook 登记槽位；AbilityDef 提供条件、CostPlan 与按等级默认冷却；AbilityRankUpEffectDef 管理升级瞬时效果。

资源不足、技能满级、无技能点拒绝；特殊模型冷却不回写统一默认字段；连续再施法 UI 只投影当前可用段。

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
- `Assets/Scripts/Gameplay/Tests/ActionArbiterConcurrencyTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `LockedMainCast_AllowsAuthoredDashInBaseSlot`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void LockedMainCast_AllowsAuthoredDashInBaseSlot()
        {
            UnitType unit = CreateUnit(withPathGrid: false);
            _tick.BeginTick(21, ExecutionMode.ServerAuthority);
            _tickBegan = true;
            InstallAbility(unit, 0, new AbilityDef
            {
                AbilityId = 10021,
                CastModel = new CommitCastModelDef
                {
                    Cast = new CastStage
                    {
                        StageKey = 1,
                        Def = new DelayStageDef(),
                        DurationTicks = 30,
                        Interruptible = false,
                        LockMovement = true,
                    },
                },
                AimKind = AimKind.Direction,
                CastRange = fp.zero,
                CostPlan = default,
            });
            InstallAbility(unit, 2, new AbilityDef
            {
                AbilityId = 10023,
                CastModel = new CommitCastModelDef
                {
                    Cast = new CastStage
                    {
                        StageKey = 1,
                        Def = new DashStageDef
                        {
                            SpeedPerTick = (fp).2m,
                            TotalDistance = (fp)3,
                        },
                        DurationTicks = 15,
                        Interruptible = false,
                        LockMovement = false,
                    },
                },
                AimKind = AimKind.Direction,
                CastRange = fp.zero,
                CostPlan = default,
            });

            ActionSubmitResult q = unit.Arbiter.Submit(
                new CastActionRequest(
                    0,
                    AbilitySignalVerb.Commit,
// 方法后续请阅读上述真实源码；这里是节选。
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

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
