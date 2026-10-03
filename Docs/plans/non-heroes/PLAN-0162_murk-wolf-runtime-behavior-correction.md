# 三狼运行行为修正

## 本次执行范围

本计划对应原编码 0162 的一次执行：三狼运行行为修正。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [野怪营地刷新与共享仇恨](../../requirements/non-heroes/REQ-FEAT-070_jungle-camps.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

JungleCamp 拥有营地、respawn 和 member slot；MonsterAIController 管理战斗、共享目标、追击与回营，复用普通行为链。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/NonHero/JungleCamp.cs`：`JungleCampSpawnSlot`、`JungleCamp`。
- `Assets/Scripts/FrameSync/UnitAnimationDriver.cs`：`UnitAnimationDriver`。
- `Assets/Scripts/Gameplay/Pathfinding/UnitLocomotionAgent.cs`：`UnitLocomotionAgent`、`value`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Unit/Team/TeamId.cs`：`TeamId`。
- `Assets/Scripts/Bootstrap/Editor/MurkWolfCampContentSetup.cs`：`MurkWolfCampContentSetup`、`WolfPresentationAssets`。
- `Assets/Scripts/Gameplay/Combat/CombatSystem.cs`：`CombatSystem`、`ShieldRequestComparer`、`HealRequestComparer`、`DamageRequestComparer`、`DamageAllocationGroup`、`EvaluatedDamage`、`HeroDamageContribution`。
- `Assets/Scripts/Gameplay/Pathfinding/MovementTask.cs`：`MovePurpose`、`MovementTaskState`、`MoveTarget`、`MovementTask`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/NonHero/JungleCamp.cs`：

```csharp
        public void Rebuild(
            in RollbackContext context)
        {
        }
```

`Assets/Scripts/FrameSync/UnitAnimationDriver.cs`：

```csharp
        private bool TryGetLoopClip(
            out AnimatorStateInfo stateInfo,
            out AnimationClip clip,
            out bool isWalkClip)
        {
            bool useNext = _animator.IsInTransition(0);
            stateInfo = useNext
                ? _animator.GetNextAnimatorStateInfo(0)
                : _animator.GetCurrentAnimatorStateInfo(0);
            _loopClipInfos.Clear();
            if (useNext)
                _animator.GetNextAnimatorClipInfo(
                    0,
                    _loopClipInfos);
            else
                _animator.GetCurrentAnimatorClipInfo(
                    0,
                    _loopClipInfos);

            clip = null;
            float bestWeight = float.MinValue;
            for (int i = 0; i < _loopClipInfos.Count; i++)
            {
                AnimatorClipInfo info = _loopClipInfos[i];
                if (info.clip != null && info.weight > bestWeight)
                {
                    clip = info.clip;
                    bestWeight = info.weight;
                }
            }

            if (clip == null || !clip.isLooping)
            {
                isWalkClip = false;
                return false;
            }

            string clipName = clip.name;
            isWalkClip = clipName.IndexOf(
                "Walk",
                System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isIdleClip = clipName.IndexOf(
                "Idle",
                System.StringComparison.OrdinalIgnoreCase) >= 0;
            return isWalkClip || isIdleClip;
        }
```

### 输入输出与边界

**野怪营地刷新与共享仇恨**

JungleCamp 拥有营地、respawn 和 member slot；MonsterAIController 管理战斗、共享目标、追击与回营，复用普通行为链。

主怪/小怪死亡、营地清空、目标远离和不可达分别处理；三狼回营/寻路旧计划未完成，不能标成已验收。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

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
- `Assets/Scripts/Bootstrap/Tests/EditMode/MurkWolfFormalContentTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `UnitCatalog_ContainsAuthoredGreaterAndMiniWolfValues`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void UnitCatalog_ContainsAuthoredGreaterAndMiniWolfValues()
        {
            UnitRuntimeCatalogAsset catalog =
                AssetDatabase.LoadAssetAtPath<UnitRuntimeCatalogAsset>(
                    CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            UnitPrototypeAuthoring greater = FindPrototype(
                catalog,
                MurkWolfContentIds.GreaterPrototype);
            UnitPrototypeAuthoring mini = FindPrototype(
                catalog,
                MurkWolfContentIds.MiniPrototype);

            AssertPrototype(
                greater,
                MurkWolfContentIds.GreaterPrefab,
                NonHeroUnitSubKindId.GreaterMurkWolf,
                1600f,
                30f,
                42f,
                42f,
                0.625f,
                175f,
                525f,
                0.8f,
                55,
                50,
                2);
            CollectionAssert.AreEqual(
                new[] { MurkWolfContentIds.GreaterCurrentHealthOnHitBuff },
                greater.InitialBuffConfigIds);

            AssertPrototype(
                mini,
                MurkWolfContentIds.MiniPrefab,
                NonHeroUnitSubKindId.MurkWolf,
                630f,
                10f,
                20f,
                20f,
                0.625f,
                125f,
                525f,
                0.5f,
                13,
                15,
                1);
            Assert.That(mini.InitialBuffConfigIds, Is.Empty);
        }
```
- `Assets/Scripts/Gameplay/Tests/IntegratedPathfindingPipelineTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `LaneAdvance_SelectsTeamFlowField`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void LaneAdvance_SelectsTeamFlowField()
        {
            PathGridMap2D grid = CreateGrid();
            FlowFieldRegistry registry =
                CreateRegistry(
                    grid,
                    1,
                    RadiusClass.Small);
            Unit unit = CreateUnit(
                1,
                new fp2((fp)2, (fp)10),
                new TeamId(1));
            var locomotion =
                new UnitLocomotionAgent(
                    unit,
                    grid);
            locomotion.SetFlowFieldRegistry(
                registry);

            RouteMoveRequest request =
                RouteMoveRequest.ToPosition(
                    new fp2((fp)18, (fp)10));
            request.Purpose =
                MovePurpose.LaneAdvance;
            request.AllowRVO = true;
            Assert.That(
                locomotion.AcceptRouteRequest(
                    request),
                Is.EqualTo(
                    MoveAcceptResult.Accepted));

            LocomotionResult result =
                locomotion.Evaluate();

            Assert.That(
                locomotion.Route.Kind,
                Is.EqualTo(
                    RouteKind.FlowField));
            Assert.That(
                locomotion.Route.FlowFieldKey,
                Is.EqualTo(
                    new FlowFieldKey(
                        1,
                        RadiusClass.Small)
                        .Packed));
            Assert.That(
                result.Status,
                Is.EqualTo(
                    RouteEvaluationStatus.Moving));
            Assert.That(
// 方法后续请阅读上述真实源码；这里是节选。
```
- `Assets/Scripts/Bootstrap/Tests/PlayMode/MurkWolfPrefabPlayModeTests.cs`：PlayMode，程序集 `FrameSyncMoba.Bootstrap.PlayModeTests`，函数 `MurkWolfPrefabPlayModeTests`；输入/夹具与期望见真实断言，失败保留回执与 Console。

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
