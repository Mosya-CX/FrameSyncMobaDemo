# 移动子管线与双网格顺序

## 本功能范围

本案细化“普通移动冲刺与强制位移”中的移动子管线与双网格顺序，仅覆盖下列明确接口与边界。

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

实现证据与已有测试位置关联总案；字段存在不能认定行为已验收。

### 移动子管线推荐顺序

移动系统在函数内部统一读取 `SimulationTickContext.Current`，不把上下文作为参数传递，也不自行维护第二套逻辑时钟。

```text
1. UnitWorld 对已注册单位执行本 Tick 的 Handler Tick；
   生成 Tick 的单位也参与，用于推进 Buff、控制、数值等被动状态。
2. 主动行为链统一读取 Unit.CanRunActiveGameplayThisTick：
   生成 Tick 不推进主动 Order、Planner、ActionRuntime 和普通主动移动。
3. 行为层仅为允许主动 Gameplay 的单位，
   向 UnitLocomotionAgent 提交或取消普通寻路任务。
4. CrowdControlHandler 在控制实例 OnAdd / Replace / OnRemove 时，
   向 MovementHandler 提交强制位移启动、替换或停止；
   该流程不受主动 Gameplay 门禁阻止。
5. 所有 UnitLocomotionAgent 按稳定 UnitUid 顺序调用 Evaluate()；
   生成 Tick 返回 `LocomotionResult.Idle`。
6. 允许主动寻路的 UnitLocomotionAgent 读取 PhysicsEntity2D 当前位置，
   更新路线、检测偏离、推进 PathCursor、判断到达，
   输出本 Tick LocomotionResult。
7. PhysicsWorld.BuildRvoGrid()，使用本 Tick 移动前位置。
8. DeterministicRVOSystem 使用全部 LocomotionResult 求解 RvoResult；
   Idle 单位的原始期望速度为零。
9. 所有 MovementHandler 按稳定 UnitUid 顺序执行：
   已生效 ForcedMove 优先；
   其后仅对允许主动 Gameplay 的单位执行 Dash / RouteMove；
   其余执行 Idle。
10. MovementHandler 进行静态墙体约束并调用 PhysicsEntity2D 应用 Pose。
11. PhysicsWorld 检测异常墙体穿透，生成 MovementCorrectionRequest。
12. MovementHandler.ApplyMovementCorrection() 应用修正。
13. PhysicsWorld.BuildUnitFinalGrid()，使用本 Tick 最终位置。
14. 后续单位碰撞、范围查询、投掷物命中等系统读取 UnitFinalGrid。
```

---

### 新生单位生成 Tick 的统一解释

单位框架的规则不是“生成 Tick 不调用 Handler”，而是：

```text
Handler Tick 正常执行；
主动 Gameplay 通过 CanRunActiveGameplayThisTick 门禁。
```

因此移动系统必须区分：

```text
被动执行：
    CrowdControlHandler 已裁决的强制位移
    墙体修正
    传送
    生命周期空间初始化

主动执行：
    普通寻路移动
    Action 发起的 Dash
```

生成 Tick 可以执行前者，不执行后者。  
该规则直接依赖单位框架的统一门禁，移动系统不新增出生 Tick 状态。

---

### 为什么先计算全部 `LocomotionResult`

RVO 需要同时看到：

```text
所有单位相同时间切片的移动前位置
所有普通移动单位当前 Tick 的原始期望速度
```

如果边计算边移动，后处理结果会依赖单位遍历顺序。

---

### 为什么 `RvoGrid` 使用移动前位置

RVO 是本 Tick 移动决策阶段。  
它必须基于统一的移动前空间状态构建邻居关系，不能读取已经移动完成的部分单位。

---

### 为什么最后构建 `UnitFinalGrid`

后续空间查询必须看到：

```text
普通移动
Dash
强制位移
传送
静态墙体约束
异常墙体修正
```

全部完成后的最终位置。

---



## 需求演进

### 2026-08-06

变动内容：控制配置采用唯一 Definition、模块表和参数布局，不引入额外分类层。

legacyDecision：D-036

### 2026-08-22

变动内容：结构化仲裁和固定 Main/Base Runtime；覆盖旧申请列表/执行器所有权。

legacyDecision：D-047

