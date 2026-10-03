# 投射物系统基础

## 本次执行范围

本计划对应原编码 0026 的一次执行：投射物系统基础。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [定义与强类型生成黑板](../../requirements/projectiles/REQ-FEAT-039_projectile-definitions.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [提交运动寿命与回收](../../requirements/projectiles/REQ-FEAT-040_projectile-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [命中过滤记忆与等距裁决](../../requirements/projectiles/REQ-FEAT-041_projectile-hit-order.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

ProjectileDef 配置逻辑、PrefabId、阶段模块和形状；SpawnBoard 是静态布局的强类型黑板，RequestSpawn 形成稳定 pending record。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Projectile/ProjectileWorld.cs`：`PendingSpawnEntry`、`ProjectileWorld`。
- `Assets/Scripts/Gameplay/Attack/AttackHandler.cs`：`AttackPlanStatus`、`AttackTimerResetReason`、`AttackHandler`。
- `Assets/Scripts/Physics/Core/PhysicsWorld.cs`：`PhysicsWorld`。
- `Assets/Scripts/Gameplay/Combat/CombatSystem.cs`：`CombatSystem`、`ShieldRequestComparer`、`HealRequestComparer`、`DamageRequestComparer`、`DamageAllocationGroup`、`EvaluatedDamage`、`HeroDamageContribution`。
- `Assets/Scripts/Gameplay/Projectile/ProjectileUid.cs`：`ProjectileUid`。
- `Assets/Scripts/FrameSync/FrameSyncGameRuntime.cs`：`FrameSyncGameRuntime`。
- `Assets/Scripts/Gameplay/Ability/AbilityHandler.cs`：`AbilityHandler`、`PassiveEventKind`、`AbilityHandlerSnapshot`、`AbilityBook`、`AbilitySlotRuntime`、`AbilitySlotSnapshot`、`AbilityBookSnapshot`。
- `Assets/Scripts/Gameplay/Projectile/ProjectileDef.cs`：`ProjectileDef`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Projectile/ProjectileWorld.cs`：

```csharp
        public void TickAll()
        {
            AdvanceMotion();
            UpdateLifecycle();
            FlushDestroy();
        }
```

`Assets/Scripts/Gameplay/Attack/AttackHandler.cs`：

```csharp
        public void TickUpdate()
        {
            if (!_state.CurrentTargetUid.IsValid() ||
                _state.ImpactCommitted)
            {
                return;
            }

            // A crowd-control block (knock-up / knock-back / stun ...) that
            // landed mid-windup must cancel the in-progress attack, not just
            // prevent new attacks.
            bool controlAttackActive =
                Owner?.ActionRuntimes != null &&
                Owner.ActionRuntimes.Main.IsOccupied &&
                Owner.ActionRuntimes.Main.Kind == ActionKind.Attack &&
                Owner.ActionRuntimes.Main.IsControlAction;
            if (Owner != null &&
                !Owner.CapabilityState.CanAttack &&
                !controlAttackActive)
            {
                CancelBeforeCommit();
                return;
            }

            if (Owner.HitReaction.InterruptsAttack)
            {
                CancelBeforeCommit();
                return;
            }

            if (SimulationTickContext.Current.Tick >=
                _state.ImpactLogicTick)
            {
                CommitAttack();
            }
        }
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
- `Assets/Scripts/Physics/Tests/PlayMode/PhysicsWorldRegistrationTests.cs`：PlayMode，程序集 `FrameSyncMoba.Physics.PlayModeTests`，函数 `RegisterUnit_AddsToUnitEntities`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void RegisterUnit_AddsToUnitEntities()
        {
            var world = new PhysicsWorld();
            var entity = CreateEntity();

            world.RegisterUnit(entity);

            Assert.That(world.UnitEntities.Count, Is.EqualTo(1));
            Assert.That(world.UnitEntities[0], Is.SameAs(entity));
            Assert.That(world.ProjectileEntities.Count, Is.EqualTo(0));
        }
```
- `Assets/Scripts/Physics/Tests/PlayMode/PhysicsWorldBuildFinalGridTests.cs`：PlayMode，程序集 `FrameSyncMoba.Physics.PlayModeTests`，函数 `BuildUnitFinalGrid_AllRegisteredUnits_Inserted`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void BuildUnitFinalGrid_AllRegisteredUnits_Inserted()
        {
            var world = new PhysicsWorld();
            world.Settings.GridCellSize = (fp)10m;

            var e1 = CreateUnitEntity(100, 1, 0, (fp)5m, (fp)5m);
            var e2 = CreateUnitEntity(100, 2, 0, (fp)15m, (fp)15m);

            world.RegisterUnit(e1);
            world.RegisterUnit(e2);

            world.BuildUnitFinalGrid();

            var results = new List<PhysicsEntity2D>();
            var queryBounds = new PhysicsBounds2D(new fp2(0, 0), new fp2(20, 20));
            world.UnitFinalGrid.CollectCandidates(queryBounds, results);

            Assert.AreEqual(2, results.Count);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
