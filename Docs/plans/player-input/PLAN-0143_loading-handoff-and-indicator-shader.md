# 加载移交与指示器 Shader

## 本次执行范围

本计划对应原编码 0143 的一次执行：加载移交与指示器 Shader。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [技能指示器与本地生命周期](../../requirements/player-input/REQ-FEAT-081_skill-indicator.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

SkillIndicatorDriver 根据 AbilityCastView 和 StageDef 通过本地 Resolver 显示方向、圆或点目标；不是单独 Gameplay 状态。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/Bootstrap/UI/UIManager.cs`：`UIManager`、`PageRegistration`。
- `Assets/Scripts/Bootstrap/Editor/Addressables/ClientPresentationAssetMigration.cs`：`ClientPresentationAssetMigration`。
- `Assets/Scripts/Bootstrap/LobbyFlowController.cs`：`LobbyFlowController`。
- `Assets/Scripts/PlayerInput/SkillIndicatorDriver.cs`：`SkillIndicatorDriver`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Bootstrap/GameBootstrap.cs`：

```csharp
        private void Awake()
        {
            contentLoadCancellation = new CancellationTokenSource();
            initializationTask = InitializeWithCleanupAsync(
                contentLoadCancellation.Token);
        }
```

`Assets/Scripts/Bootstrap/UI/UIManager.cs`：

```csharp
        private void Awake()
        {
            Instance = this;
            if (luaManager == null)
                luaManager =
                    LuaManager.CreateDefault();
            lifetimeCancellation ??= new CancellationTokenSource();
            ClientSpriteRegistry.SpriteLoaded += RefreshLoadedPages;
            Initialize();
        }
```

### 输入输出与边界

**技能指示器与本地生命周期**

SkillIndicatorDriver 根据 AbilityCastView 和 StageDef 通过本地 Resolver 显示方向、圆或点目标；不是单独 Gameplay 状态。

受控单位变化、禁用、死亡、UI 门禁和 Session 结束关闭；游戏回滚后跟随 Runtime，不反写命令或技能距离。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Bootstrap/Tests/EditMode/PresentationAddressablesMigrationTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `ProjectilesAreSplitIntoLogicAndAddressableViews`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void ProjectilesAreSplitIntoLogicAndAddressableViews()
        {
            GlobalPrefabTable table =
                AssetDatabase.LoadAssetAtPath<GlobalPrefabTable>(TablePath);
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            List<PrefabEntry> entries = FindEntries(
                table,
                settings,
                PrefabKind.Projectile);
            Assert.That(entries.Count, Is.EqualTo(8));
            for (int i = 0; i < entries.Count; i++)
            {
                PrefabEntry entry = entries[i];
                AddressableAssetEntry logic =
                    FindEntryByAddress(settings, entry.LogicAssetAddress);
                Assert.That(logic, Is.Not.Null, entry.LogicAssetAddress);
                string logicPath = logic.AssetPath;
                GameObject logicPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(logicPath);
                Assert.That(logicPath,
                    Does.StartWith(
                        "Assets/Config/Formal/Prefabs/Logic/Projectile/"));
                Assert.That(
                    logicPrefab.GetComponent<PhysicsEntity2D>(),
                    Is.Not.Null,
                    logicPath);
                Assert.That(
                    logicPrefab.GetComponentsInChildren<Renderer>(true),
                    Is.Empty,
                    logicPath);
                AddressableAssetEntry view =
                    FindEntryByAddress(settings, entry.ClientViewAddress);
                Assert.That(view, Is.Not.Null, entry.ClientViewAddress);
                GameObject viewPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(view.AssetPath);
                Assert.That(viewPrefab.GetComponent<PhysicsEntity2D>(), Is.Null);
                Assert.That(
                    viewPrefab.GetComponentsInChildren<Renderer>(true),
                    Is.Not.Empty,
                    view.AssetPath);
            }
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
