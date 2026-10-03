# 相机与指针表现

## 本次执行范围

本计划对应原编码 0135 的一次执行：相机与指针表现。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [单位视图绑定与语义挂点](../../requirements/presentation-ui/REQ-FEAT-072_unit-view-binding.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [攻击技能动画与插值采样](../../requirements/presentation-ui/REQ-FEAT-073_animation-sampling.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [特效音效与回滚账本](../../requirements/presentation-ui/REQ-FEAT-074_presentation-ledger.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [设备事件缓冲与 UI 门禁](../../requirements/player-input/REQ-FEAT-078_input-event-buffer.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UnitPresentationHost/Registry 和 PresentationSocketSet 只读逻辑；SocketProfile 定义挂点，缺失挂点按可见校验策略处理。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Physics/Core/PhysicsEntity2D.cs`：`PhysicsEntity2D`。
- `Assets/Scripts/Bootstrap/CameraController.cs`：`CameraController`。
- `Assets/Scripts/Bootstrap/CameraDebugPointerProbe.cs`：`CameraDebugPointerProbe`。
- `Assets/Scripts/Bootstrap/CameraDebugWorkbench.cs`：`CameraDebugSide`、`CameraDebugWorkbench`。
- `Assets/Scripts/Bootstrap/MobaCameraPresentationConfig.cs`：`CameraSideSettings`、`MobaCameraPresentationConfig`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitUid.cs`：`UnitUid`。
- `Assets/Scripts/Physics/Core/PhysicsPresentationSettings.cs`：`PhysicsPresentationSettings`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Physics/Core/PhysicsEntity2D.cs`：

```csharp
        private void LateUpdate()
        {
            if (!syncTransform) return;

            var pos2D = Transform2D.Position;
            Vector3 desiredPosition = new Vector3(
                (float)pos2D.x,
                0f,
                (float)pos2D.y);

            var fwd2D = Transform2D.Forward;
            Quaternion desiredRotation = transform.rotation;
            if (fwd2D.x != fp.zero || fwd2D.y != fp.zero)
            {
                var fwd = new Vector3((float)fwd2D.x, 0f, (float)fwd2D.y);
                if (fwd.sqrMagnitude > 0.0001f)
                    desiredRotation = Quaternion.LookRotation(
                        fwd.normalized,
                        Vector3.up);
            }

            ProjectPresentationPose(desiredPosition, desiredRotation);
        }
```

`Assets/Scripts/Bootstrap/CameraController.cs`：

```csharp
        private void LateUpdate()
        {
            if (!followLocked)
            {
                return;
            }

            FollowLocalHero();
            ClampToBounds();
        }
```

### 输入输出与边界

**单位视图绑定与语义挂点**

UnitPresentationHost/Registry 和 PresentationSocketSet 只读逻辑；SocketProfile 定义挂点，缺失挂点按可见校验策略处理。

视图不能反写 Gameplay；异步加载不能复用旧生命；逻辑空间所有者不随模型层级变化。

**攻击技能动画与插值采样**

UnitAnimationDriver 读取 Attack 锁定时间与 AbilityCastView；客户端默认 20 Hz 插值，Bootstrap 发布按 UnitWorld 拥有的连续逻辑时间投影。

不得跨未 Commit 的 Impact 或 Ready；loop 相位由逻辑 epoch 和实时倍率重建；未知 TickRate 不硬回退 30 Hz；独立采样不新增 Gameplay Tick。

**特效音效与回滚账本**

VfxManager、AudioManager 分别管理定义、池和回滚账本；PresentationEventId=SourceLogicTick+SourceKind+SourceRuntimeUid+EventSequence+EventKey，支持 OneShotNoReplay、DurationCorrectable、LoopState。

Gameplay 不直接 Play 音效；每 manager 独立账本；事件序号归源运行时所有，表现不新增第二序号。

**设备事件缓冲与 UI 门禁**

PlayerInputController 显式订阅 InputAction，回调记录本地事件；GameplayInputGate 处理 ActionMap、UI 指针阻断和受控单位变化。

回放不再读设备；同帧顺序、缓冲上限、禁用时清理和松键行为明确；UI 使用 Unity Input System UI 整合。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Bootstrap/Tests/PlayMode/AatroxPrefabPlayModeTests.cs`：PlayMode，程序集 `FrameSyncMoba.Bootstrap.PlayModeTests`，函数 `OutlineBakeRunsAfterWingVisibility`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void OutlineBakeRunsAfterWingVisibility()
        {
            int outlineOrder = typeof(ClientUnitOutline)
                .GetCustomAttributes(
                    typeof(DefaultExecutionOrder),
                    false)
                .Cast<DefaultExecutionOrder>()
                .Single().order;
            int wingOrder = typeof(BuffDrivenBoneVisibility)
                .GetCustomAttributes(
                    typeof(DefaultExecutionOrder),
                    false)
                .Cast<DefaultExecutionOrder>()
                .Single().order;

            Assert.That(outlineOrder, Is.GreaterThan(wingOrder));
        }
```
- `Assets/Scripts/Bootstrap/Tests/PlayMode/CameraControllerPlayModeTests.cs`：PlayMode，程序集 `FrameSyncMoba.Bootstrap.PlayModeTests`，函数 `SharedConfig_UsesOppositeBlueAndRedViewDirections`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SharedConfig_UsesOppositeBlueAndRedViewDirections()
        {
            MobaCameraPresentationConfig config =
                ScriptableObject.CreateInstance<
                    MobaCameraPresentationConfig>();
            try
            {
                CameraSideSettings blue = config.ResolveSide((byte)1);
                CameraSideSettings red = config.ResolveSide((byte)2);
                Assert.That(red.EulerAngles.y - blue.EulerAngles.y,
                    Is.EqualTo(180f).Within(.001f));
                Assert.That(red.FollowOffset.z,
                    Is.EqualTo(-blue.FollowOffset.z).Within(.001f));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
```
- `Assets/Scripts/Bootstrap/Tests/PlayMode/CameraDebugPointerProbePlayModeTests.cs`：PlayMode，程序集 `FrameSyncMoba.Bootstrap.PlayModeTests`，函数 `CameraDebugPointerProbePlayModeTests`；输入/夹具与期望见真实断言，失败保留回执与 Console。
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
