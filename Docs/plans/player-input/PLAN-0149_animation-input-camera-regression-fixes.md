# 动画输入与相机回归修复

## 本次执行范围

本计划对应原编码 0149 的一次执行：动画输入与相机回归修复。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [设备事件缓冲与 UI 门禁](../../requirements/player-input/REQ-FEAT-078_input-event-buffer.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能输入组合与提交去重](../../requirements/player-input/REQ-FEAT-079_ability-input.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [世界鼠标 Aim 与类型化 Request](../../requirements/player-input/REQ-FEAT-080_aim-requests.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能指示器与本地生命周期](../../requirements/player-input/REQ-FEAT-081_skill-indicator.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [攻击技能动画与插值采样](../../requirements/presentation-ui/REQ-FEAT-073_animation-sampling.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [单位视图绑定与语义挂点](../../requirements/presentation-ui/REQ-FEAT-072_unit-view-binding.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

PlayerInputController 显式订阅 InputAction，回调记录本地事件；GameplayInputGate 处理 ActionMap、UI 指针阻断和受控单位变化。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Physics/Core/PhysicsEntity2D.cs`：`PhysicsEntity2D`。
- `Assets/Scripts/PlayerInput/PlayerInputController.cs`：`PlayerInputController`。
- `Assets/Scripts/FrameSync/UnitAnimationDriver.cs`：`UnitAnimationDriver`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Physics/Core/PhysicsEntity2D.cs`：

```csharp
        private void ProjectPresentationPose(
            Vector3 desiredPosition,
            Quaternion desiredRotation)
        {
            bool smoothingEnabled =
                PhysicsPresentationSettings.Enabled &&
                Application.isPlaying;
            float snapDistance =
                PhysicsPresentationSettings.SnapDistance;
            bool exceedsSnapDistance =
                presentationInitialized &&
                (desiredPosition - presentationTargetPosition)
                    .sqrMagnitude > snapDistance * snapDistance;
            if (!smoothingEnabled ||
                !presentationInitialized ||
                presentationSnapRequested ||
                exceedsSnapDistance)
            {
                transform.SetPositionAndRotation(
                    desiredPosition,
                    desiredRotation);
                presentationStartPosition = desiredPosition;
                presentationTargetPosition = desiredPosition;
                presentationStartRotation = desiredRotation;
                presentationTargetRotation = desiredRotation;
                presentationPositionElapsed =
                    PhysicsPresentationSettings.DurationSeconds;
                presentationRotationElapsed =
                    PhysicsPresentationSettings.DurationSeconds;
                presentationInitialized = true;
                presentationSnapRequested = false;
                return;
            }

            if ((desiredPosition - presentationTargetPosition)
                    .sqrMagnitude > 0.0000001f)
            {
                presentationStartPosition = transform.position;
                presentationTargetPosition = desiredPosition;
                presentationPositionElapsed = 0f;
            }
            if (Quaternion.Angle(
                    desiredRotation,
                    presentationTargetRotation) > 0.001f)
            {
                presentationStartRotation = transform.rotation;
                presentationTargetRotation = desiredRotation;
                presentationRotationElapsed = 0f;
            }

            presentationPositionElapsed += Time.unscaledDeltaTime;
            presentationRotationElapsed += Time.unscaledDeltaTime;
            float positionT = Mathf.Clamp01(
                presentationPositionElapsed /
                PhysicsPresentationSettings.DurationSeconds);
            float rotationT = Mathf.Clamp01(
                presentationRotationElapsed /
                PhysicsPresentationSettings.DurationSeconds);
            transform.SetPositionAndRotation(
                Vector3.LerpUnclamped(
                    presentationStartPosition,
                    presentationTargetPosition,
                    positionT),
                Quaternion.SlerpUnclamped(
                    presentationStartRotation,
                    presentationTargetRotation,
                    rotationT));
        }
```

`Assets/Scripts/PlayerInput/PlayerInputController.cs`：

```csharp
        private void UpdateIndicator()
        {
            if (indicatorDriver == null)
            {
                if (!indicatorDriverMissingLogged &&
                    HasPendingIndicatorState())
                {
                    Debug.Log(
                        "[IndicatorTrace] pending aim state exists but " +
                        "SkillIndicatorDriver is not assigned.");
                    indicatorDriverMissingLogged = true;
                }
                return;
            }
            if (pointerResolver == null ||
                commandRequester == null ||
                commandRequester.ControlledUnit == null)
            {
                if (!indicatorDependencyMissingLogged)
                {
                    Debug.Log(
                        "[IndicatorTrace] indicator evaluation skipped: " +
                        $"pointer={(pointerResolver != null ? "ready" : "null")} " +
                        $"requester={(commandRequester != null ? "ready" : "null")} " +
                        $"unit={(commandRequester?.ControlledUnit != null ? "ready" : "null")}");
                    indicatorDependencyMissingLogged = true;
                }
                HideIndicator();
                return;
            }
            indicatorDependencyMissingLogged = false;

            // Player Input v1.1 §§15.3/17.4: keep the preparatory indicator
            // visible while a Focus/Commit command is pending. The command may
            // target a future prediction Tick, so waiting for the Gameplay
            // Session before showing it creates a false "Q did not start"
            // gap and can hide the indicator again after Commit.
            for (byte slot = 0; slot < 4; slot++)
            {
                ref readonly var state = ref commandRequester.GetAbilityState(slot);
                if (state.Kind == LocalAbilityInputStateKind.LocalAiming ||
                    state.Kind == LocalAbilityInputStateKind.FocusRequested ||
                    state.Kind == LocalAbilityInputStateKind.GameplayFocusing ||
                    state.Kind == LocalAbilityInputStateKind.CommitRequested)
                {
                    // Get the aim kind and cast range for this slot
                    bool aimInfo = commandRequester.TryGetAimInfo(
                        slot,
                        out var aimKind,
                        out var castRange,
                        out var casterPos,
                        out var casterForward);
                    LogIndicatorState(
                        slot,
                        state.Kind,
                        aimInfo,
                        aimKind);
                    if (aimInfo)
                    {
                        fp groundRadius = fp.zero;
                        commandRequester.TryGetGroundTargetRadius(
                            slot,
                            out groundRadius);
                        DirectionalMultiZoneDamageStageDef zone = null;
                        AbilityIndicatorGeometryResolver
                            .TryResolveDirectionalZone(
                                commandRequester.ControlledUnit
                                    .AbilityHandler,
                                slot,
                                out zone);
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**设备事件缓冲与 UI 门禁**

PlayerInputController 显式订阅 InputAction，回调记录本地事件；GameplayInputGate 处理 ActionMap、UI 指针阻断和受控单位变化。

回放不再读设备；同帧顺序、缓冲上限、禁用时清理和松键行为明确；UI 使用 Unity Input System UI 整合。

**技能输入组合与提交去重**

从 CastModelDef 离线派生 PressCommit、LocalAimPrimaryCommit、PressFocusReleaseOrPrimaryCommit，运行本地 FocusRequested/CommitRequested/GameplayFocusing 状态。

激活 hold-release 后松键和左键走同一 Commit，首次成功抑制重复；右键不 Cancel 但可移动/普攻；配置不复制 Gameplay 费用/范围/时长。

**世界鼠标 Aim 与类型化 Request**

鼠标世界解析选择地面点或 Unit，Direction 规范化；Move/Attack/Ability typed Request 返回回执，AimSnapshot 使用唯一正式字段。

输入层不 Clamp 技能距离；本地目标仅最低检查，最终执行重查；Focus 与 Commit 同 TargetTick 保持序列顺序。

**技能指示器与本地生命周期**

SkillIndicatorDriver 根据 AbilityCastView 和 StageDef 通过本地 Resolver 显示方向、圆或点目标；不是单独 Gameplay 状态。

受控单位变化、禁用、死亡、UI 门禁和 Session 结束关闭；游戏回滚后跟随 Runtime，不反写命令或技能距离。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

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
- `Assets/Scripts/Physics/Tests/PlayMode/PhysicsEntity2DPlayModeTests.cs`：PlayMode，程序集 `FrameSyncMoba.Physics.PlayModeTests`，函数 `OrdinaryPositionAndDelta_AdvancePreviousPosition`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void OrdinaryPositionAndDelta_AdvancePreviousPosition()
        {
            entity.TeleportLogicPosition(Vector(2, 3));
            entity.SetLogicPosition(Vector(5, 7));

            AssertVector(entity.Transform2D.PrevPosition, Vector(2, 3));
            AssertVector(entity.Transform2D.Position, Vector(5, 7));

            entity.ApplyLogicPositionDelta(Vector(-1, 4));

            AssertVector(entity.Transform2D.PrevPosition, Vector(5, 7));
            AssertVector(entity.Transform2D.Position, Vector(4, 11));
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
