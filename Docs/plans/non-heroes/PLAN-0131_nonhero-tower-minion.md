# 防御塔与小兵

## 本次执行范围

本计划对应原编码 0131 的一次执行：防御塔与小兵。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [AI 注册调度与多态快照](../../requirements/non-heroes/REQ-FEAT-068_ai-scheduling.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [小兵波次与兵线 AI](../../requirements/non-heroes/REQ-FEAT-069_minion-waves.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [野怪营地刷新与共享仇恨](../../requirements/non-heroes/REQ-FEAT-070_jungle-camps.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [防御塔目标优先级与攻击红线](../../requirements/non-heroes/REQ-FEAT-071_tower-targeting.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UnitAIController 子类提供决策与专有快照；按稳定 Uid 调度，但主动生效晚于出生 Tick。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/FrameSync/TowerTargetLinePresenter.cs`：`TowerTargetLinePresenter`。
- `Assets/Scripts/Gameplay/Attack/AttackHandler.cs`：`AttackPlanStatus`、`AttackTimerResetReason`、`AttackHandler`。
- `Assets/Scripts/Gameplay/Attack/AttackSnapshot.cs`：`AttackSnapshot`、`AttackAnimationSnapshot`、`HitReactionAnimationInfo`。
- `Assets/Scripts/Gameplay/Attack/TowerAttackHandler.cs`：`TowerAttackHandler`。
- `Assets/Scripts/Gameplay/Buff/BuffHandler.cs`：`BuffHandler`、`BuffReactionKind`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。
- `Assets/Scripts/Gameplay/Unit/Kind/UnitKind.cs`：`UnitKind`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/FrameSync/TowerTargetLinePresenter.cs`：

```csharp
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Client presentation-only red line from a tower to its current attack
    /// intent. Target replacement is followed immediately; the component
    /// never modifies deterministic Gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerTargetLinePresenter :
        MonoBehaviour
    {
        [SerializeField] private Unit towerUnit;
        private LineRenderer line;

        private void Awake()
        {
#if UNITY_SERVER
            enabled = false;
            return;
#else
            line = gameObject.AddComponent<LineRenderer>();
            Shader shader =
                Shader.Find("Sprites/Default");
            if (shader != null)
            {
                line.material =
                    new Material(shader);
                line.material.color =
                    new Color(1f, 0.1f, 0.1f, 0.9f);
            }
            line.startWidth = 0.15f;
            line.endWidth = 0.15f;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.enabled = false;
#endif
        }

        private void LateUpdate()
        {
            if (line == null)
            {
                return;
            }
            line.enabled = false;
            if (towerUnit == null)
            {
                towerUnit =
                    GetComponentInParent<Unit>();
            }
            if (towerUnit == null ||
                !(towerUnit.AttackHandler is
                    TowerAttackHandler) ||
                towerUnit.World == null)
            {
                return;
            }

            // Never fall back to the last projectile lock: that UID may be
            // stale after the target dies or later respawns.
            if (!TryResolveDisplayTarget(
                    towerUnit,
                    out Unit target))
            {
                return;
            }

            Vector3 from = towerUnit.transform.position;
// 方法后续请阅读上述真实源码；这里是节选。
```

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

### 输入输出与边界

**AI 注册调度与多态快照**

UnitAIController 子类提供决策与专有快照；按稳定 Uid 调度，但主动生效晚于出生 Tick。

不增加通用模拟按键层；AI Runtime 与管理者状态区分；死亡注销不能在恢复时静默漏建。

**小兵波次与兵线 AI**

MinionSystem 固定波次序列和出生配置；MinionAIController 复用 UnitOrder/Planner/Attack；兵线参数与单位参数两类配置。

同 Tick 生效门、目标失效、追击距离与英雄协防条件明确；初始 Buff 和 Participant 来源由票据固定。

**野怪营地刷新与共享仇恨**

JungleCamp 拥有营地、respawn 和 member slot；MonsterAIController 管理战斗、共享目标、追击与回营，复用普通行为链。

主怪/小怪死亡、营地清空、目标远离和不可达分别处理；三狼回营/寻路旧计划未完成，不能标成已验收。

**防御塔目标优先级与攻击红线**

TowerAIController 过滤范围和合法目标；TowerAttackHandler 管理周期与 Commit，TowerTargetLinePresenter 读取当前锁定状态。

塔不追击；在途炮弹不因重新索敌改目标；英雄正在攻击己方英雄的判定来源固定；结构效果准入在中央入口。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/MinionInitialBuffTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `InitialBuffs_AppliedAutomaticallyFromPrototype`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void InitialBuffs_AppliedAutomaticallyFromPrototype()
        {
            controller.BeginTick(
                1,
                ExecutionMode.ServerAuthority);
            Unit minion = Spawn(
                2001,
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new[] { 9101, 9103 });

            Assert.That(
                minion.BuffHandler.HasBuff(
                    Muncher),
                Is.True);
            Assert.That(
                minion.BuffHandler.HasBuff(
                    TowerPillow),
                Is.True);
        }
```
- `Assets/Scripts/Gameplay/Tests/TowerAttackHandlerTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `HeroRamp_FirstHitIsBase_ThenMultipliesByOnePointFive`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void HeroRamp_FirstHitIsBase_ThenMultipliesByOnePointFive()
        {
            fp baseDamage = (fp)180m;
            Assert.That(
                TowerAttackHandler.ResolveRampDamage(
                    baseDamage, 0),
                Is.EqualTo((fp)180m));
            Assert.That(
                TowerAttackHandler.ResolveRampDamage(
                    baseDamage, 1),
                Is.EqualTo((fp)270m));
            Assert.That(
                TowerAttackHandler.ResolveRampDamage(
                    baseDamage, 2),
                Is.EqualTo((fp)405m));
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
