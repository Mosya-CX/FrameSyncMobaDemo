# 控制系统基础

## 本次执行范围

本计划对应原编码 0025 的一次执行：控制系统基础。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [控制实例与模块参数](../../requirements/crowd-control/REQ-FEAT-051_crowd-control-instances.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [控制免疫净化与汇总裁决](../../requirements/crowd-control/REQ-FEAT-052_control-immunity.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

CrowdControlHandler 唯一运行入口；Definition 经 Bake 形成 module op 与 ParamLayout；实例按 Key 暴露、按 Offset 存储，不创建每实例模块对象。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/CrowdControl/CrowdControlHandler.cs`：`CrowdControlHandler`。
- `Assets/Scripts/Gameplay/Movement/MovementHandler.cs`：`MovementHandler`、`MovementMode`。
- `Assets/Scripts/Gameplay/Buff/BuffHandler.cs`：`BuffHandler`、`BuffReactionKind`。
- `Assets/Scripts/Gameplay/Combat/CombatSystem.cs`：`CombatSystem`、`ShieldRequestComparer`、`HealRequestComparer`、`DamageRequestComparer`、`DamageAllocationGroup`、`EvaluatedDamage`、`HeroDamageContribution`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。
- `Assets/Scripts/Deterministic/Core/IRollback.cs`：`IRollback`。
- `Assets/Scripts/Deterministic/Core/SimulationTickContext.cs`：`SimulationTickContext`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：

```csharp
        internal void ClearForDeath()
        {
            // D-009: StatHandler and CombatModifiers survive ordinary death.
            // Stat modifiers survive, while StatHandler-owned shields do not.
            statHandler.ClearForDeath();
            movementHandler?.ClearForDeath();
            attackHandler?.ClearForDeath();
            abilityHandler?.ClearForDeath();
            buffHandler.ClearForDeath();
            crowdControlHandler?.ClearForDeath();
            equipmentHandler?.ClearForDeath();
            ClearTags();
            Locomotion?.CancelRoute(MoveCancelReason.Death);
            Planner?.ClearForDeath();
            ActionRuntimes?.ClearWithoutCancel();
            Intent = UnitIntent.None;
        }
```

`Assets/Scripts/Gameplay/CrowdControl/CrowdControlHandler.cs`：

```csharp
        public CrowdControlAddResult Add(
            CrowdControlId controlId,
            int durationTicks,
            in CrowdControlParamWriter parameters)
        {
            int currentTick =
                SimulationTickContext.Current.Tick;
            CrowdControlDefinition definition =
                ResolveDefinition(controlId);
            if (definition == null ||
                !definition.IsValid)
            {
                return new CrowdControlAddResult(
                    CrowdControlAddStatus.InvalidDefinition,
                    default);
            }
            if (!CanAcceptControl())
            {
                return new CrowdControlAddResult(
                    CrowdControlAddStatus.OwnerRejected,
                    default);
            }
            if (!parameters.Materialize(
                    definition.ParamLayout,
                    out CrowdControlParamBlock paramBlock))
            {
                return new CrowdControlAddResult(
                    CrowdControlAddStatus.InvalidParams,
                    default);
            }

            CrowdControlTagMask tags =
                definition.Tags;
            bool isForcedMove =
                tags.HasAny(
                    new CrowdControlTagMask(
                        CrowdControlDefinition.ControlTagBits.ForcedMove));

            if (IsUnstoppable && isForcedMove)
            {
                return new CrowdControlAddResult(
                    CrowdControlAddStatus.RejectedByUnstoppable,
                    default);
            }

            if (CanBeResisted(definition))
            {
                int blockingImmunityId =
                    TryBlockByImmunity(
                        definition,
                        currentTick);
                if (blockingImmunityId != 0)
                {
                    return new CrowdControlAddResult(
                        CrowdControlAddStatus.BlockedByImmunity,
                        default,
                        blockingImmunityId);
                }
            }

            CrowdControlHandle replaced =
                default;
            if (isForcedMove &&
                activeForcedMoveHandle.IsValid)
            {
                CrowdControlInstance current =
                    FindInstance(
                        activeForcedMoveHandle);
                int newPriority =
                    ReadForcedMovePriority(
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

- `Assets/Scripts/Gameplay/Tests/MovementIntegrationTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `SpawnUnit_CreatesMovementHandlerWithDefaults`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SpawnUnit_CreatesMovementHandlerWithDefaults()
        {
            Unit unit = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);

            Assert.IsNotNull(unit.MovementHandler);
            Assert.AreEqual(fp2.zero, unit.MovementHandler.Position);
            Assert.AreEqual(fp.one, unit.MovementHandler.MoveSpeed);
            Assert.AreEqual(
                (fp)0.01m,
                unit.MovementHandler.LogicMoveSpeed);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
