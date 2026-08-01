# 完整对局前的修正候选

> 更新日期：2026-07-29  
> 这些候选来自当前代码、Unity MCP 场景/资产检查和正式设计，不是按展示效果排序。  
> 这里只制定候选，不在本轮执行或占用正式 ExecPlan 编号。

## 真实阻断摘要

| 级别 | 证据 | 对完整测试的影响 |
|---|---|---|
| P0 | 当前 `TowerAttackHandler` 是包裹普通 Handler 的纯 C# 类，不是设计要求的 `AttackHandler` 子类；`TowerAIController` 不做索敌，也没有运行时注册调用 | 塔不会形成正式、可恢复的自主攻击闭环 |
| P0 | `LuaRuntime` 明确只是 Dictionary；仓库没有 `LuaManager`、`LuaHost`、`UIPanel`、`UIManager`，现有 `.lua` 不会执行 | 无法完成用户要求的 Lua/UI 绑定与页面流程 |
| P0 | ClientBootstrap 的 Lane、Wave、Camp 为空；目录只有 Hero/Base，GlobalPrefabTable 只有一个 Unit Prefab | 没有兵线、塔、野怪可用于整局 |
| P0 | `EquipmentDatabase` 在 Bootstrap 中直接创建为空，仓库没有测试装备配置资产 | Shop 完整测试没有商品 |
| P1 | Minion AI 未消费锁定时间/协防状态，默认优先英雄而非设计的敌方小兵；普通 Spawn 未绑定 FlowFieldRegistry | 兵线行为与 NonHero v5 不一致 |
| P1 | `UnitAnimationDriver` 硬编码另一套 Animator 参数且不使用 `UnitAnimationProfile`；Host 不持有 Driver/SocketSet；单位 Prefab 无 Animator/Socket | 单位不能完成设计规定的动画与挂点验收 |
| P1 | UI 控制器使用 legacy Text 和运行时动态占位界面；现有 Prefab 使用 TMP 且多数引用为空 | 用户 Prefab 无法可靠接入，视觉成功会掩盖真实缺失 |
| P1 | `PushUiSnapshot` 选择“第一个受控 Unit”，而非 `LocalControlledUnitUid`；本地 KDA 始终写 0 | Client 1 可能显示 Client 0 数据 |
| P2 | Scoreboard 每 Tick 分配 List 并生成占位名；现有中立 Hero 不能升级/复活 | 不阻断底层 Tick，但不适合作为完整验收夹具 |

## 候选 A：正式塔 AI 与塔攻击

- **优先级**：P0，核心玩法优先。
- **估算生产代码**：450-850 行。
- **设计来源**：NonHero v5 §8-9，Attack v6.2，Unit v27.3。
- **范围**：
  - 让正式 `TowerAttackHandler` 继承已有 `AttackHandler`。
  - 只新增 `LastCommittedProjectileUid` 权威状态并纳入快照。
  - 实现 `HasUnresolvedProjectile` 门控和锁定只读 View。
  - `TowerAIController` 按 `(PriorityBand, DistanceSq, UnitUid)` 索敌，
    下达 `allowChase=false` 的 AttackOrder。
  - 注册/恢复 TowerAI，增加塔 Prefab 组合校验。
- **不做**：正式塔数值、美术、塔皮肤或地图目标系统。
- **验证**：EditMode 覆盖六级优先级、同距 UID、在途炮弹门控、快照恢复；
  PlayMode 只验证 Tower MonoBehaviour Prefab 组合。
- **完成结果**：塔可以在确定性 Pipeline 中自主攻击且不追击。

## 候选 B：Minion AI、协防和 Lane FlowField 接入

- **优先级**：P0（完整兵线），在 A 后执行。
- **估算生产代码**：500-900 行。
- **设计来源**：NonHero v5 §4-5，Pathfinding v13.1。
- **范围**：
  - 把硬编码 AI 常量改为冻结的 MinionAIProfile。
  - 实现 TargetLock、PendingAssist、合法性过滤和正式优先级。
  - 使用平方距离和复用候选缓冲。
  - LaneAdvance 选择正式 FlowField Route；普通 Spawn 和 Restore 都绑定同一
    FlowFieldRegistry。
  - 保持 Centerline 只用于回线/追击边界。
- **不做**：多路兵线、超级兵规则或正式波次平衡。
- **验证**：插入顺序不影响目标、协防通知顺序不影响结果、回线、快照、
  连续执行与 Replay 等价。
- **完成结果**：一条测试兵线可以按正式规则持续推进与交战。

## 候选 C：正式 xLua 环境与页面宿主

- **优先级**：P0（用户明确要求 Lua 绑定）。
- **估算生产代码**：650-1,100 行。
- **设计来源**：UI/Lua v9.1 §2-6。
- **范围**：
  - 复用仓库已有 xLua，不添加 Package。
  - 建立唯一 `LuaManager`、Loader、LuaInit 和 `LuaEnv.Tick/Dispose`。
  - 建立 `UIPage`、`UIPageLayer`、`UIManager`、`UIPanel`、`LuaHost`。
  - 缓存类型化 Show/Refresh/Hide/Dispose 委托。
  - 删除 Dictionary `LuaRuntime` 被当作正式 VM 的路径。
- **前置风险**：xLua 当前位于预定义程序集，必须先确定最小 asmdef 接入，
  不能让确定性 Gameplay 依赖 xLua。
- **验证**：EditMode 验证 Loader/模块实例隔离/Dispose；PlayMode 验证页面
  生命周期和两个页面实例不共享状态。
- **完成结果**：现有和新页面 Lua 能被真实执行。

## 候选 D：用户 UI Prefab 与 HUD/Shop/Result 绑定

- **优先级**：P0，依赖 C 和用户 UI Prefab。
- **估算生产代码**：700-1,300 行。
- **设计来源**：UI/Lua v9.1 §9-14，FrameSync v10.2。
- **范围**：
  - 统一 TMP，绑定 Main/Match/Select/Load/HUD/Shop/Result。
  - 页面 Lua 直接读取导出的只读 Runtime/Unit/Handler/View。
  - 本地 Unit 只按 `LocalControlledUnitUid` 解析。
  - Shop 点击进入类型化 Command Request。
  - 移除 `GameObject.Find`、动态占位页面和每 Tick DTO/List 路径。
- **不做**：修改用户布局、美术风格或最终文案。
- **验证**：两个 Client 各显示自己的 Hero；Button 路由正确；UI 隐藏/显示
  不改变 Gameplay checksum。
- **完成结果**：用户 Prefab 成为正式页面，Lua 和组件绑定可验收。

## 候选 E：单位动画、Host 与语义挂点对齐

- **优先级**：P1，但必须在完整视觉验收前完成。
- **估算生产代码**：450-850 行。
- **设计来源**：Presentation v13.2 §2-3、§6、§10。
- **范围**：
  - Host 显式持有 AnimationDriver 和 PresentationSocketSet。
  - Driver 使用 UnitAnimationProfile，不硬编码第二套参数协议。
  - 只读 `ActionStateView`、Attack 状态和 `AbilityCastView`。
  - 实现攻击序列/后摇恢复、Stage 进度、Death/Respawn 和回滚重定位。
  - 增加 Prefab Editor 校验。
- **不做**：具体英雄动画逻辑、动画决定伤害或技能 Tick。
- **验证**：状态相同产生相同参数；回滚后定位正确；缺 Socket 按规则失败或
  fallback；PlayMode 验证 Animator/Prefab 生命周期。
- **完成结果**：用户 TestHero/TestMonster Animator 可以按正式接口绑定。

## 候选 F：测试装备目录和 Shop 小闭环

- **优先级**：P0（若 Shop 属于本次完整验收）。
- **估算生产代码**：200-450 行。
- **设计来源**：Equipment v12，UI/Lua v9.1 §11-13。
- **范围**：
  - 增加最小 Equipment authoring/Bake 入口或复用现有正式数据库填充入口。
  - 只创建 2-3 个中立测试定义：固定属性、合成价格、可出售/撤销。
  - Bootstrap 注入非空只读 EquipmentDatabase。
- **不做**：正式装备、装备主动目标策略或复杂被动内容。
- **验证**：购买、满槽复用、出售、撤销、Snapshot/Replay、双方 UI 一致。
- **完成结果**：Shop 不再是空目录，可以验证正式金币/Command 管线。

## 候选 G：完整对局夹具组合与资源绑定

- **优先级**：P0，最后组合；依赖 A-F 中适用项及用户资源交付。
- **估算生产代码**：200-500 行；主要改动为 Unity 资产和场景序列化绑定。
- **范围**：
  - 为 TestHero、TestMonster、两类 Minion、Tower/Base 分配稳定
    PrefabId/PrototypeId。
  - 创建一条 Lane、一份短周期 Wave、一个 JungleCamp。
  - 绑定双方 Hero/Tower/Base 初始 Spawn、英雄选择项和测试装备。
  - 使用用户 Prefab，不硬编码具体英雄/装备/怪物逻辑。
- **验证**：目录 Bake、Prefab 校验、场景 PlayMode 门禁，然后只构建一次
  Server/Client。
- **完成结果**：满足 `TEST_PREPARATION.md` 第 8 节，可开始完整人工测试。

## 推荐依赖顺序

```text
A 塔
  └─> B 小兵/兵线

C xLua 宿主
  └─> D UI Prefab 绑定

用户单位 Prefab ─> E 动画/Socket
用户 UI Prefab   ─> D

F 测试装备
        \
A+B+D+E+F + 用户资源 ─> G 完整夹具组合
```

A、B、C 可以在用户制作资源期间实施。D、E 需要冻结后的用户 Prefab。
G 是开始完整测试前的最终组合，不提前用占位成功路径替代。
