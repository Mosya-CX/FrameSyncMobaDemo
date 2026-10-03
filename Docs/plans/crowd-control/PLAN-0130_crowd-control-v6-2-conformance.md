# 控制系统合同补全

## 本次执行范围

本计划对应原编码 0130 的一次执行：控制系统合同补全。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [控制实例与模块参数](../../requirements/crowd-control/REQ-FEAT-051_crowd-control-instances.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [控制免疫净化与汇总裁决](../../requirements/crowd-control/REQ-FEAT-052_control-immunity.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

CrowdControlHandler 唯一运行入口；Definition 经 Bake 形成 module op 与 ParamLayout；实例按 Key 暴露、按 Offset 存储，不创建每实例模块对象。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/CrowdControl/CrowdControlTypes.cs`：`CrowdControlId`、`CrowdControlIntensity`、`CrowdControlDurationRule`、`CrowdControlTagMask`、`CrowdControlTagQuery`、`CrowdControlCleanseSpec`、`UnitActionBlockMask`。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。
- `Assets/Scripts/Gameplay/Ability/Stages/PullStageDef.cs`：`PullStageDef`。
- `Assets/Scripts/Gameplay/Ability/Stages/StunStageDef.cs`：`StunStageDef`。
- `Assets/Scripts/Gameplay/Buff/BuffCatalogAsset.cs`：`BuffCatalogAsset`。
- `Assets/Scripts/Gameplay/CrowdControl/CrowdControlCatalogAsset.cs`：`CrowdControlCatalogAsset`。
- `Assets/Scripts/Gameplay/CrowdControl/CrowdControlDefinition.cs`：`CrowdControlParamAuthoring`、`CrowdControlModuleAuthoring`、`CrowdControlDefinition`、`ControlTagBits`。
- `Assets/Scripts/Gameplay/CrowdControl/CrowdControlDefinitionRegistry.cs`：`CrowdControlDefinitionRegistry`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/CrowdControl/CrowdControlTypes.cs`：

```csharp
using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Stable global identifier of a CrowdControlDefinition (CC v6.2 2.2).
    /// </summary>
    [Serializable]
    public readonly struct CrowdControlId : IEquatable<CrowdControlId>, IComparable<CrowdControlId>
    {
        public readonly int Value;
        public CrowdControlId(int value) { Value = value; }
        public bool IsValid => Value > 0;
        public bool Equals(CrowdControlId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CrowdControlId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(CrowdControlId other) => Value.CompareTo(other.Value);
        public static bool operator ==(CrowdControlId a, CrowdControlId b) => a.Equals(b);
        public static bool operator !=(CrowdControlId a, CrowdControlId b) => !a.Equals(b);
        public override string ToString() => Value.ToString();
    }

    public enum CrowdControlIntensity : byte
    {
        Low = 0,
        Medium = 1,
        High = 2,
    }

    public enum CrowdControlDurationRule : byte
    {
        DefaultTenacity = 0,
        IgnoreTenacity = 1,
    }

    /// <summary>
    /// Lightweight logical tag bits (CC v6.2 5.2). One ulong covers the
    /// suggested tag set; bit order is stable and authored at bake time.
    /// </summary>
    public readonly struct CrowdControlTagMask : IEquatable<CrowdControlTagMask>
    {
        public readonly ulong Bits;
        public CrowdControlTagMask(ulong bits) { Bits = bits; }

        public bool HasAny(in CrowdControlTagMask other) =>
            (Bits & other.Bits) != 0UL;
        public bool HasAll(in CrowdControlTagMask other) =>
            (Bits & other.Bits) == other.Bits;
        public bool HasNone(in CrowdControlTagMask other) =>
            (Bits & other.Bits) == 0UL;

        public static CrowdControlTagMask Union(
            in CrowdControlTagMask left,
            in CrowdControlTagMask right) =>
            new CrowdControlTagMask(left.Bits | right.Bits);

        public static CrowdControlTagMask None => default;

        public bool Equals(CrowdControlTagMask other) => Bits == other.Bits;
        public override bool Equals(object obj) => obj is CrowdControlTagMask other && Equals(other);
        public override int GetHashCode() => Bits.GetHashCode();
        public static bool operator ==(CrowdControlTagMask a, CrowdControlTagMask b) => a.Equals(b);
        public static bool operator !=(CrowdControlTagMask a, CrowdControlTagMask b) => !a.Equals(b);
    }

    /// <summary>
    /// All/Any/None tag query shared by immunity, cleanse and state queries
    /// (CC v6.2 5.4).
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/FrameSync/GameplaySnapshot.cs`：

```csharp
using System;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.FrameSync
{
    public struct UnitSnapshot
    {
        public UnitUid UnitUid;
        public GameplayParticipantId GameplayParticipantId;
        public UnitUid OwnerUid;
        public UnitKind UnitKind;
        public ushort UnitSubKindId;
        public TeamId TeamId;
        public int UnitPrototypeId;
        /// <summary>Home spawn position used when this unit respawns.</summary>
        public fp2 RespawnPosition;
        public LifeState LifeState;
        public CapabilityState CapabilityState;
        public HitReactionState HitReactionState;
        public UnitIntent IntentState;
        public ActionRuntimeSetSnapshot ActionRuntimeState;
        public PhysicsTransform2D PhysicsTransform;
        public PhysicsShape2D PhysicsShape;
        public StatHandlerSnapshot StatState;
        public CombatModifierSetSnapshot CombatModifierState;
        public AttackSnapshot AttackState;
        public MovementSnapshot MovementState;
        public AbilityHandlerSnapshot AbilityState;
        public BuffHandlerSnapshot BuffState;
        public CrowdControlHandlerSnapshot CCState;
        public LocomotionAgentSnapshot LocomotionState;
        public EquipmentHandlerSnapshot EquipmentState;
        public UnitTag[] Tags;
    }

    /// <summary>
    /// Snapshot of the entire UnitWorld for rollback.
    /// Uses T[] arrays per Snapshot Appendix v7.2 section 5.
    /// </summary>
    public struct UnitWorldSnapshot
    {
        public UnitSnapshot[] Units;
        public MinionSystemSnapshot MinionSystemState;
        public RespawnTimerSnapshot PendingUnitLifecycleState;
        public JungleCampSnapshot[] JungleCampStates;
        public UnitAIControllerSnapshot[] AIControllerStates;
        public int RuntimeRevision;

        public static UnitWorldSnapshot CreateEmpty() => new UnitWorldSnapshot
        {
            Units = Array.Empty<UnitSnapshot>(),
            JungleCampStates = Array.Empty<JungleCampSnapshot>(),
            AIControllerStates = Array.Empty<UnitAIControllerSnapshot>(),
        };
    }

    public struct GameplaySnapshot
    {
        public const int CurrentSchemaVersion = 25;
        public int SchemaVersion;

        public DeterministicRandomSnapshot RandomState;
        public MatchRuleRuntimeSnapshot MatchRuleState;
        public UnitWorldSnapshot UnitWorldState;
        public CombatSnapshot CombatState;
        public ProjectileWorldSnapshot ProjectileState;
        public EquipmentShopRuntimeSnapshot EquipmentShopState;
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**控制实例与模块参数**

CrowdControlHandler 唯一运行入口；Definition 经 Bake 形成 module op 与 ParamLayout；实例按 Key 暴露、按 Offset 存储，不创建每实例模块对象。

不新增 Kind、软硬控枚举或 Combat 控制管线；模块重入使用明确延迟规则；缺键和容量溢出可见失败。

**控制免疫净化与汇总裁决**

TagMask、Intensity、ImmunitySpec、UnitActionBlockMask 各司其职；Restrictions 并合，ForcedBehavior 按正式胜者规则，ForcedMove 由控制系统唯一仲裁。

净化由效果拥有者选择规则；免疫不等于所有法术盾；结构外源控制在访问可选 Handler 前拒绝，自身合法控制保留。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/CrowdControlCatalogAssetTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `CatalogDefinitions_AreBakedAndValid_OnDisk`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void CatalogDefinitions_AreBakedAndValid_OnDisk()
        {
            CrowdControlCatalogAsset catalog =
                AssetDatabase.LoadAssetAtPath<CrowdControlCatalogAsset>(
                    CatalogPath);
            Assert.That(
                catalog,
                Is.Not.Null,
                "CrowdControl catalog asset must exist at the runtime path.");
            Assert.That(
                catalog.Definitions,
                Is.Not.Null.And.Not.Empty,
                "CrowdControl catalog must contain definitions.");

            for (int i = 0;
                 i < catalog.Definitions.Length;
                 i++)
            {
                CrowdControlDefinition definition =
                    catalog.Definitions[i];
                Assert.That(
                    definition,
                    Is.Not.Null,
                    $"definition {i} must not be null.");
                Assert.That(
                    definition.IsBaked,
                    Is.True,
                    $"definition '{definition.name}' must be baked " +
                    "(serialized hidden fields must survive reload).");
                Assert.That(
                    definition.IsValid,
                    Is.True,
                    $"definition '{definition.name}' must be valid " +
                    "after bake.");
            }
        }
```
- `Assets/Scripts/Gameplay/Tests/CrowdControlHandlerTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `Add_CreatesIndependentInstances_NoMerge`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void Add_CreatesIndependentInstances_NoMerge()
        {
            CrowdControlAddResult first =
                unit.CrowdControl.Add(
                    CrowdControlIds.Stun,
                    30,
                    default);
            CrowdControlAddResult second =
                unit.CrowdControl.Add(
                    CrowdControlIds.Stun,
                    30,
                    default);

            Assert.That(first.Added, Is.True);
            Assert.That(second.Added, Is.True);
            Assert.That(unit.CrowdControl.Count,
                Is.EqualTo(2));
            Assert.That(
                first.Handle.InstanceId,
                Is.Not.EqualTo(
                    second.Handle.InstanceId));
            Assert.That(
                unit.CrowdControl.State.BlockedActions,
                Is.EqualTo(
                    UnitActionBlockMask.VoluntaryMove |
                    UnitActionBlockMask.Turn |
                    UnitActionBlockMask.VoluntaryAttack |
                    UnitActionBlockMask.AbilityCast |
                    UnitActionBlockMask.Mobility |
                    UnitActionBlockMask.ControlMove |
                    UnitActionBlockMask.ControlAttack));
        }
```
- `Assets/Scripts/Gameplay/Tests/MovementConformanceTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `ForcedMove_OverridesRouteMove`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void ForcedMove_OverridesRouteMove()
        {
            Unit unit = CreateUnit(100, fp2.zero);
            RegisterKnockBack(unit);
            CrowdControlAddResult added =
                unit.CrowdControl.Add(
                    CrowdControlIds.KnockBack,
                    2,
                    CreateForcedMoveParams(
                        new fp2(fp.one, fp.zero),
                        2,
                        (short)5));
            Assert.That(added.Added, Is.True);

            unit.MovementHandler.ApplyRouteMovement(
                new LocomotionResult
                {
                    UnitUid = unit.UnitUid,
                    HasMovement = true,
                    DesiredDirection =
                        new fp2(fp.zero, fp.one),
                    DesiredSpeed = (fp)4,
                });
            unit.MovementHandler.TickUpdate();

            Assert.That(
                unit.PhysicsEntity.Transform2D.Position,
                Is.EqualTo(
                    new fp2(fp.one, fp.zero)));
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
