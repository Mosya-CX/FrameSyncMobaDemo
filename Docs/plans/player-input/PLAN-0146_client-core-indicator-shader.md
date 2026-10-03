# 客户端核心指示器 Shader

## 本次执行范围

本计划对应原编码 0146 的一次执行：客户端核心指示器 Shader。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [技能指示器与本地生命周期](../../requirements/player-input/REQ-FEAT-081_skill-indicator.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

SkillIndicatorDriver 根据 AbilityCastView 和 StageDef 通过本地 Resolver 显示方向、圆或点目标；不是单独 Gameplay 状态。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。


以下代码为当前真实实现节选，完整方法以对应源码为准。

### 输入输出与边界

**技能指示器与本地生命周期**

SkillIndicatorDriver 根据 AbilityCastView 和 StageDef 通过本地 Resolver 显示方向、圆或点目标；不是单独 Gameplay 状态。

受控单位变化、禁用、死亡、UI 门禁和 Session 结束关闭；游戏回滚后跟随 Runtime，不反写命令或技能距离。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Bootstrap/Tests/EditMode/DedicatedServerAddressablesBuildScopeTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `ServerScope_IncludesOnlyLogicAddressablesGroups`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void ServerScope_IncludesOnlyLogicAddressablesGroups()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetSettings.PlayerBuildOption previous =
                settings.BuildAddressablesWithPlayerBuild;
            var previousGroups = new Dictionary<string, bool>();
            foreach (AddressableAssetGroup group in settings.groups)
            {
                BundledAssetGroupSchema schema =
                    group?.GetSchema<BundledAssetGroupSchema>();
                if (schema != null)
                    previousGroups.Add(group.Name, schema.IncludeInBuild);
            }

            using (new AddressablesPlayerBuildScope(true))
            {
                Assert.That(
                    settings.BuildAddressablesWithPlayerBuild,
                    Is.EqualTo(
                        AddressableAssetSettings.PlayerBuildOption
                            .BuildWithPlayer));
                foreach (AddressableAssetGroup group in settings.groups)
                {
                    BundledAssetGroupSchema schema =
                        group?.GetSchema<BundledAssetGroupSchema>();
                    if (schema == null)
                        continue;
                    bool isLogic = System.Array.IndexOf(
                        AddressablesProjectConstants.LogicGroups,
                        group.Name) >= 0;
                    Assert.That(schema.IncludeInBuild, Is.EqualTo(isLogic),
                        group.Name);
                }
            }

            Assert.That(
                settings.BuildAddressablesWithPlayerBuild,
                Is.EqualTo(previous));
            foreach (AddressableAssetGroup group in settings.groups)
            {
                BundledAssetGroupSchema schema =
                    group?.GetSchema<BundledAssetGroupSchema>();
                if (schema != null)
                    Assert.That(schema.IncludeInBuild,
                        Is.EqualTo(previousGroups[group.Name]),
                        group.Name);
            }
        }
```
- `Assets/Scripts/Bootstrap/Tests/PlayMode/GameBootstrapPlayModeTests.cs`：PlayMode，程序集 `FrameSyncMoba.Bootstrap.PlayModeTests`，函数 `GameBootstrapPlayModeTests`；输入/夹具与期望见真实断言，失败保留回执与 Console。

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
