# 普通移动冲刺与强制位移

## 目标实现

普通路线、Dash、控制位移与传送均由统一移动入口提交空间。

## 技术方案

MovementHandler 依正式优先级执行 Route、Dash 和 ResolvedForcedMove；ForcedMove 胜者仅由控制系统选出。

## 边界情况

死亡清理移动任务；仲裁决定占用而不让技能控制直接写 Transform；墙体约束与异常挤出分开。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Movement/ForcedMoveExecutor.cs`：当前关联实现定义 ForceMoveKind、ForceMoveWallPolicy、DashRequest、DashRuntime、ResolvedForcedMove（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Movement/MovementHandler.cs`：当前关联实现定义 MovementHandler、MovementMode（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/MovementHandlerTests.cs`：Constructor_SetsInitialPosition、Constructor_DefaultFacing_IsPositiveX、MoveIntent_Constructor_SetsDirectionAndHasInput、MoveIntent_None_HasNoInput、MoveIntent_FromDirection_Zero_ReturnsNone、MoveIntent_FromDirection_Normalizes、ApplyMoveInput_ThenTickUpdate_MovesPosition。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/AatroxPrefabPlayModeTests.cs`：RuntimePrefab_InstantiatesWithModelAndEditorGizmo、ClientUnitOutline_CoversEveryAatroxSubMesh、TetherArea_InstantiatesAsStationaryProjectile、AnimatorController_RoutesPassiveUltimateAndEmpoweredAttack、AnimatorController_LocomotionVariantsAdvanceWithoutSelfReentry、UnitAnimationDriver_UsesNewLocomotionStateOnChangeFrame、AnimatorController_UltimateEndPlaysExitClip。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/ClientBootstrapFirstWavePlayModeTests.cs`：GameScene_FirstWaveUsesFlowFieldsAndMoves、GameScene_MapViewAnchorsToStaticTopologyRootAtWorldOrigin。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/VarusAnimationPlayModeTests.cs`：BoundDriver_QFocusMovementChangesResolveLoopSameFrame。
- `Assets/Scripts/FrameSync/Tests/AggregateSnapshotContractTests.cs`：Restore_UsesStableUnitUidAndRestoresRandomAndPhysicsState、Restore_RejectsNonCanonicalOrMissingUnitIdentity、Restore_RejectsDuplicateGameplayParticipantIdentity、Restore_RejectsMissingGameplayParticipantIdentity、SnapshotStore_WritesExplicitOuterSchemaAndNextTick。

## 细化功能与技术附录

- [移动职责与提交接口](REQ-FEAT-067-PART-1_movement-contracts.md)：接口、数据流、公式、边界与配置。
- [移动运行数据与普通路线](REQ-FEAT-067-PART-2_movement-routes.md)：接口、数据流、公式、边界与配置。
- [冲刺控制位移与传送](REQ-FEAT-067-PART-3_dash-forced-teleport.md)：接口、数据流、公式、边界与配置。
- [移动子管线与双网格顺序](REQ-FEAT-067-PART-4_movement-grid-order.md)：接口、数据流、公式、边界与配置。
- [移动状态快照与重建](REQ-FEAT-067-PART-5_movement-snapshot.md)：接口、数据流、公式、边界与配置。
- [移动改造实施顺序与帧算法](REQ-FEAT-067-PART-6_movement-implementation.md)：接口、数据流、公式、边界与配置。


## 需求演进

### 2026-08-06

变动内容：控制配置采用唯一 Definition、模块表和参数布局，不引入额外分类层。

legacyDecision：D-036

### 2026-08-22

变动内容：结构化仲裁和固定 Main/Base Runtime；覆盖旧申请列表/执行器所有权。

legacyDecision：D-047

### 2026-10-02

变动内容：总案保留目标和事实，精确细节拆为独立功能

