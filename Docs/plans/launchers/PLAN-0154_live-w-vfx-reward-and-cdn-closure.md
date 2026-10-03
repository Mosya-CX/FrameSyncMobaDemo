# W 特效奖励与 CDN 收尾

## 本次执行范围

本计划对应原编码 0154 的一次执行：W 特效奖励与 CDN 收尾。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [安全分片上传与可选发布包](../../requirements/launchers/REQ-FEAT-084_safe-content-upload.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [增量更新与内容寻址分片](../../requirements/launchers/REQ-FEAT-083_content-updates.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

正式客户端固定 Builds/Demo/Game/AAALOL.exe，CDN 打包默认不启用；开启后成功 Player 才生成 schema-v3 清单和内容分片。上传按哈希与清单校验、受限重试。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/FrameSync/VfxManager.cs`：`VfxManager`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/FrameSync/VfxLibrary.cs`：`VfxLibrary`、`VfxPrefabEntry`。
- `Assets/Scripts/Gameplay/Ability/Stages/AreaDamageStageDef.cs`：`AreaDamageStageDef`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/FrameSync/VfxManager.cs`：

```csharp
        public async Task PreloadAsync(
            CancellationToken cancellationToken)
        {
            await PreloadAsync(
                null,
                cancellationToken);
        }
```

`Assets/Scripts/Bootstrap/GameBootstrap.cs`：

```csharp
        private async Task InitializeAsync(
            CancellationToken cancellationToken)
        {
            if (GameSessionContext.IsDedicatedServer)
                dedicatedServer = true;
            FrameSyncDiagnosticsUnityHost.EnsureInitialized(
                dedicatedServer);
            if (GameSessionContext.FlowManagedExternally)
            {
                // External flow ownership is exclusive. Serialized scene
                // defaults must never leak LocalDirect behavior into UOS.
                enableOnlineApplicationFlow =
                    GameSessionContext.FlowMode ==
                    FrameFlowMode.UosOnline;
                localDevelopmentNetworkFlow =
                    GameSessionContext.FlowMode ==
                    FrameFlowMode.LocalDirect;
                autoApplyLocalFixturePayload = false;
            }
            else
            {
                // Legacy unmanaged path (scene loads GameScene directly):
                // honor the -onlineFlow/-localFlow command-line override.
                enableOnlineApplicationFlow =
                    UosApplicationConfig.IsOnlineFlowRequested(
                        enableOnlineApplicationFlow);
            }
            PrimeExternalLoadingPresentation();
            if (globalGameplayData == null)
                throw new InvalidOperationException(
                    $"{nameof(GameBootstrap)} requires GlobalGameplayData.");
            BakedGlobalGameplayData config = globalGameplayData.BakeOrThrow();
            if (config.PrefabTable.Partitions.Count > 0)
            {
                MatchContentSelection selection =
                    ResolveMatchContentSelection(
                        config.PrefabTable);
                AddressableMatchContentScope loadedScope =
                    await AddressableMatchContentService.LoadAsync(
                        config.PrefabTable,
                        selection,
                        cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (isDestroying)
                        throw new OperationCanceledException(
                            cancellationToken);
                    config = config.WithPrefabTable(
                        loadedScope.PrefabTable);
                    deterministicMapConfig = loadedScope.MapConfig;
                    cancellationToken.ThrowIfCancellationRequested();
                    if (isDestroying)
                        throw new OperationCanceledException(
                            cancellationToken);
                    matchContentScope = loadedScope;
                    loadedScope = null;
                }
                finally
                {
                    loadedScope?.Dispose();
                }
            }
            ResolveMapTopologyAuthoring(config.PrefabTable);
            bakedConfig = config;
            Debug.Log(
                $"[FrameSyncConfig] tickRate={config.TickRate} " +
                $"maxPredictionLead={config.MaxPredictionLeadTicks} " +
                $"maxTicksPerFrame={config.MaxLogicTicksPerUnityFrame} " +
                $"launchDelay={config.LaunchDelayMilliseconds}ms " +
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**安全分片上传与可选发布包**

正式客户端固定 Builds/Demo/Game/AAALOL.exe，CDN 打包默认不启用；开启后成功 Player 才生成 schema-v3 清单和内容分片。上传按哈希与清单校验、受限重试。

不修改测试 Builds/UosClient；上传失败不能报告发布成功；私钥不进库；Release ZIP 仅在用户接受后进入 Git。

**增量更新与内容寻址分片**

schema-v3 以 content/<sha256> 标识不超过 95,000,000 字节分片；旧完整内容经校验可复用，新文件按清单组装。

哈希不匹配不能复用；缺旧分片时走完整获取；目标路径规范化和目录穿越检查必需。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/FrameSync/Tests/MatchRewardDistanceTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `MinionExperienceRadius_UsesStatToLogicDistanceScale`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void MinionExperienceRadius_UsesStatToLogicDistanceScale()
        {
            var world = new UnitWorld
            {
                StatDistanceToLogicDistanceScale =
                    (fp)0.01m,
            };
            UnitType minion = Spawn(
                world,
                701,
                UnitKind.Minion,
                new TeamId(2),
                fp.zero,
                100);
            UnitType nearHero = Spawn(
                world,
                702,
                UnitKind.Hero,
                new TeamId(1),
                (fp)11.99m,
                0);
            UnitType farHero = Spawn(
                world,
                703,
                UnitKind.Hero,
                new TeamId(1),
                (fp)12.01m,
                0);
            var statistics =
                new MatchStatisticsRuntime();

            Assert.That(
                nearHero.LifeState,
                Is.EqualTo(LifeState.Alive));
            Assert.That(
                nearHero.StatHandler.CanLevelUp,
                Is.True);
            Assert.That(
                world.GetAllUnits().Count,
                Is.EqualTo(3));
            Assert.That(
                world.StatDistanceToLogicDistanceScale,
                Is.EqualTo((fp)0.01m));
            Assert.That(
                fpmath.lengthsq(
                    nearHero.PhysicsEntity.Transform2D.Position -
                    minion.PhysicsEntity.Transform2D.Position),
                Is.LessThan((fp)144m));

            statistics.Consume(
// 方法后续请阅读上述真实源码；这里是节选。
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
