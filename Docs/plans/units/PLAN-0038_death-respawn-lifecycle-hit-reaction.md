# 死亡复活与命中反应

## 本次执行范围

本计划对应原编码 0038 的一次执行：死亡复活与命中反应。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [强类型单位事件与反应](../../requirements/units/REQ-FEAT-027_unit-events.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UnitEventBus 按正式事件数据强类型路由 Handler；支持列表和即时顺序明确，Reaction 只生成所允许的后续请求。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Unit/Core/RespawnTimer.cs`：`RespawnTimer`、`RespawnEntry`、`RespawnTimerSnapshot`、`DeathDisposalEntry`。
- `Assets/Scripts/Gameplay/Combat/DeathEffectDispatcher.cs`：`DeathEffectDispatcher`。
- `Assets/Scripts/Gameplay/Combat/CombatSystem.cs`：`CombatSystem`、`ShieldRequestComparer`、`HealRequestComparer`、`DamageRequestComparer`、`DamageAllocationGroup`、`EvaluatedDamage`、`HeroDamageContribution`。
- `Assets/Scripts/Gameplay/Combat/HitReactionState.cs`：`HitReactionKind`、`HitReactionState`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。
- `Assets/Scripts/Gameplay/Combat/CombatEvents.cs`：`CombatEvents`、`DamageEventData`、`HealEventData`、`ShieldEventData`、`OnHitEventData`。

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

`Assets/Scripts/Gameplay/Unit/Core/RespawnTimer.cs`：

```csharp
        public void RegisterDeath(UnitUid unitUid, int deathTick, int respawnDelayTicks)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].UnitUid == unitUid)
                {
                    _entries.RemoveAt(i);
                    break;
                }
            }

            var newEntry = new RespawnEntry
            {
                UnitUid = unitUid,
                DeathLogicTick = deathTick,
                RespawnLogicTick = deathTick + respawnDelayTicks,
            };
            int insertIndex = _entries.Count;
            while (insertIndex > 0 &&
                _entries[insertIndex - 1].UnitUid.CompareTo(unitUid) > 0)
                insertIndex--;
            _entries.Insert(insertIndex, newEntry);
        }
```

### 输入输出与边界

**强类型单位事件与反应**

UnitEventBus 按正式事件数据强类型路由 Handler；支持列表和即时顺序明确，Reaction 只生成所允许的后续请求。

死亡/击杀事件回调在 T 立即发生，但新普通 Shield、Damage、Heal 延迟到 T+1，合法序列缺口不重编号。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/CombatSystemTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `SubmitDamage_ValidRequest_ReducesHealth`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SubmitDamage_ValidRequest_ReducesHealth()
        {
            BeginTick(1);
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            var target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            fp initialHealth = target.StatHandler.CurrentHealth;

            _combat.BeginTick();
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                attacker.UnitUid, target.UnitUid, (fp)100));
            _combat.SettleActiveRequests();
            _combat.EndTick();

            fp finalHealth = target.StatHandler.CurrentHealth;
            Assert.Less(finalHealth, initialHealth);
            Assert.Greater(finalHealth, fp.zero);
        }
```
- `Assets/Scripts/Gameplay/Tests/UnitWorldIntegrationTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `SpawnMultipleKinds_GetByKind`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SpawnMultipleKinds_GetByKind()
        {
            world.SpawnUnit(heroProto, TeamId.Neutral, 1, 0m, 0m);
            world.SpawnUnit(heroProto, TeamId.Neutral, 1, 0m, 0m);
            world.SpawnUnit(minionProto, TeamId.Neutral, 1, 0m, 0m);

            var heroes = world.GetUnitsByKind(UnitKind.Hero);
            var minions = world.GetUnitsByKind(UnitKind.Minion);
            var all = world.GetAllUnits();

            Assert.AreEqual(2, heroes.Count);
            Assert.AreEqual(1, minions.Count);
            Assert.AreEqual(3, all.Count);
        }
```
- `Assets/Scripts/Gameplay/Tests/UnitWorldTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `InternalRegistration_PublicLookupReturnsSameRuntime`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void InternalRegistration_PublicLookupReturnsSameRuntime()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(300, 9, 1), UnitKind.Hero, 0, TeamId.Neutral);

            world.RegisterUnit(unit);

            Assert.That(world.TryGetUnit(unit.UnitUid, out Unit resolved), Is.True);
            Assert.That(resolved, Is.SameAs(unit));
            Assert.That(world.TryGetUnit(new UnitUid(300, 9, 2), out _), Is.False);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
