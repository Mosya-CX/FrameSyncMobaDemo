# 完整对局测试准备

> Document class: Operational Test Guide
> Default read: only when preparing full-match acceptance fixtures

> 更新日期：2026-08-10（交接状态同步）
> 目标：用中立测试内容跑通一场“一条兵线、双方英雄与防御塔、一个普通野怪营地、基地胜负、UI 与本地双客户端”的完整对局。  
> 本文只规定测试资源、绑定责任和开始门禁，不把测试内容视为正式英雄或正式平衡内容。

## 1. 当前结论

本文件最初的 2026-07-29 缺口判断已经过时。当前真实状态是：

- 小兵、塔、测试英雄、三路兵线、生成点、基地胜负、确定性寻路/流场/RVO、
  正式单位组件、动画/表现宿主以及 Lua 页面宿主已经实现并完成过聚焦验证。
- `Assets/Config/Formal/` 是当前 C/S 唯一运行资源链；七个正式 Hero/Minion/
  Tower runtime Prefab 位于 `Assets/Config/Formal/Prefabs/`，旧
  `Assets/Resources/Prefab/Unit/` 链已删除。
- Main/Match/Select/Load/HUD/Shop/Result 七个页面 Prefab 已由 `UIManager`
  管理并绑定 Lua；HUD 具有 51 个序列化引用。仍有最终图标、提示、视觉验收等
  表现缺口，但不再是“没有 UI 宿主/页面”的状态。
- 本地 packaged Dedicated Server + 两 Client 流程已由仓库所有者暂时接受。
- 真实 UOS 已完成 allocation -> Ready -> matchmaking -> 两客户端连接 ->
  identity -> hero lock -> GameScene -> Loaded/Ready -> StartTick 3 -> 持续
  Gameplay。它仍有启动 UTP 队列告警、Loading/HUD 时间观测和未完成的
  result/return/remote settlement 验收；详见 `CURRENT_HANDOFF.md`。

因此现在不能宣称“全功能完整对局测试已经全部通过”的主要原因，已经不是塔、
小兵、UI 或基础网络未实现，而是：

1. **正式 Equipment Shop 商品目录仍为空**，真实场景无法完成购买/出售/撤销。
2. **JungleCamp / TestMonster 内容和整套营地逻辑验收仍缺失**。
3. **完整 Result -> Return to Lobby -> UOS settlement 尚未实机闭环**。
4. **UOS 启动可靠性尚需修正和复验**：UOS/LocalDirect connection-owner race
   的当前修复尝试只覆盖 `Update()`，没有覆盖 NGO connection callback；服务端
   还出现一次 send-queue-full。

第 8 节已改为按当前事实标记门禁。未勾选项目完成前，可以继续做现有兵线、塔、
英雄和网络链路的专项验收，但不得把它表述为包含商店、野区和结算的整局全通过。

## 2. 双方责任

| 内容 | 用户负责 | Codex 负责 |
|---|---|---|
| 测试单位视觉 | 模型、材质、Rig、动画 Clip、AnimatorController、单位图标和展示名称 | 检查导入设置；绑定 Gameplay、Presentation 和目录配置 |
| 测试单位数值 | 提供可快速验收的测试数值意图 | 转写到 `UnitRuntimeCatalogAsset`，Bake 为 `fp` 并校验 |
| 单位 Gameplay 组件 | 不需要手工添加 | 通过 Unity MCP 添加、绑定并校验 `Unit`、`PhysicsEntity2D` 和各 Handler |
| 单位表现组件 | 提供 Animator 和语义挂点 Transform | 绑定 `UnitPresentationHost`、`UnitAnimationDriver`、`PresentationSocketSet` 和 Profile |
| UI Prefab | 层级、图片、TMP 文本、按钮、Slider、布局和动效 | 添加页面宿主与 LuaHost，绑定序列化引用、点击路由和只读 Gameplay 查询 |
| Lua | 可提出显示与交互需求 | 建立真实 xLua 环境，编写/修正页面 Lua 与 Cell Lua |
| 地图测试拓扑 | 可摆放美术几何体 | 配置 Lane、出生点、塔、基地、营地、波次和确定性地图 Bake |
| 自动测试 | 不需要编写 | 为代码修正和绑定增加最小 EditMode/PlayMode 验证 |
| 完整人工验收 | 操作两个 Client 并判断视觉/手感 | 启动前检查、日志判读、问题定位和修正 |

资源交接采用“冻结后绑定”：

1. 用户完成一批 Prefab 后说明路径和“可绑定”。
2. 用户暂时停止修改这批 Prefab。
3. Codex 用 Unity MCP 检查并绑定。
4. Codex 返回缺失项或“已通过绑定门禁”。
5. 用户继续做纯视觉迭代时，不更改已冻结的稳定根节点和语义挂点名。

## 3. 测试单位资源责任与当前状态

### 3.1 最小单位集合

| Prefab | 完整对局是否必需 | 当前状态 | 最低表现/剩余工作 |
|---|---:|---|---|
| Test Hero | 是 | 已进入 Formal Prefab/Catalog，现有英雄动画与 Q/W/E/R 测试能力已接入 | 继续做人工动画、手感、图标和 UI 验收；不新增英雄专属核心协议 |
| Test Monster | 是 | **缺失** | 用户准备模型/Animator/单位信息，Codex 绑定 Unit/Handler/Physics/AI/Camp 配置 |
| 蓝/红近战 Minion | 是 | 已进入 Formal Prefab/Catalog | Idle/Move/Attack/Death 和 Gameplay 组件已绑定，保留人工视觉验收 |
| 蓝/红远程 Minion | 是 | 已进入 Formal Prefab/Catalog | 同上，并已使用发射挂点/Projectile 路径 |
| 蓝/红 Tower | 是 | 已进入 Formal Prefab/Catalog | 正式资源只有 Idle/Death；塔没有攻击动画，攻击期间保持 Idle，不得伪造 Attack 动画；发射/锁定由 Gameplay/Presentation 组件表达 |
| Team Base | 是（胜负） | 当前地图/正式配置已能完成基地死亡和胜负闭环 | 最终 Destroyed 视觉仍可继续验收，不影响已有逻辑证据 |

表中的类别用于测试责任划分。已有具体英雄模型可以作为当前显式测试内容，
但通用框架不得为该英雄复制 UID、Command、Snapshot、Aim、AbilitySignal、
Checksum、FixedPoint 或新增只服务单一英雄的核心协议。

### 3.2 Prefab 根结构

建议结构：

```text
TestHeroVisual
├── Model
│   ├── Animator
│   └── Rig / Mesh
└── Sockets
    ├── Center
    ├── Head
    ├── Chest
    ├── RightHand
    ├── Weapon
    ├── WeaponTip
    └── Ground
```

用户交付时满足：

- Prefab 根局部位置 `(0,0,0)`、旋转 `(0,0,0)`、缩放 `(1,1,1)`。
- 尺寸和朝向修正在 `Model` 子节点处理，不修改 Gameplay 根。
- Animator 放在 `Model` 或其子节点，`Apply Root Motion` 关闭。
- Idle/Move Clip 循环；Attack/Cast/Death/Respawn 是否循环按状态语义设置。
- 不用 Animation Event 造成伤害、生成投掷物、推进技能或修改单位状态。
- 不依赖 Rigidbody、Collider 或 Unity Physics 驱动 Gameplay 位置。
- 可以保留表现用 Collider，但不得与 Gameplay 逻辑混用。
- 远程单位和塔至少提供 `WeaponTip`；近战单位至少提供 `Center`、`Chest`、
  `Ground`。
- 骨骼实际名称可以不同，Codex 通过 `PresentationSocketSet` 绑定语义挂点。

### 3.3 Animator 准备边界

用户现在可以完成 Clip、State Machine 和 BlendTree，但在
`UnitAnimationDriver` 修正计划完成前，不要把参数名视为最终冻结接口。

正式设计要求的公共语义包括：

```text
IsMoving                 Bool
MoveSpeed                Float
IsAttacking              Bool
IsEmpoweredAttack        Bool
IsAttackRecovering       Bool
AttackSequenceIndex      Int
AttackMotionTime         Float
AttackStart              Trigger
IsCasting                Bool
AbilityStageProgress     Float
LifeState                Int
IsControlled             Bool
```

第一轮测试 Animator 最少需要：

- Hero：Idle / Move / Attack_0 / Cast_Generic / Death / Respawn。
- Monster、Minion：Idle / Move / Attack_0 / Death。
- Tower：Idle / Attack_0。

技能、攻击 Commit、死亡和复活的 Gameplay Tick 不由动画长度决定。

### 3.4 单位信息交付表

用户为每种测试单位提供下列值；稳定 ID 由 Codex 分配，避免与现有协议和目录
冲突。

| 类别 | 用户提供 |
|---|---|
| 身份 | 测试显示名、图标、Hero/Minion/Monster/Structure、近战/远程/塔等语义 |
| 空间 | 视觉半径、Gameplay 碰撞半径、模型朝向、移动速度 |
| 生存 | MaxHealth、HealthRegeneration、Armor、MagicResistance |
| 攻击 | AttackDamage、AttackSpeed、AttackRange、近战或投掷物、攻击动画命中姿势 |
| 英雄资源 | MaxCastResource、CastResourceRegeneration |
| 英雄成长 | 初始等级、最大等级、经验曲线、需要测试的属性成长 |
| 奖励 | 被击杀给予的 Gold、Experience |
| 生命周期 | 是否复活、测试复活时间、死亡后保留或回池 |
| 技能 | 复用现有三个中立技能，或说明只需要哪些通用施法动画 |
| 野怪 | 主怪槽位、营地半径、脱战时间、刷新时间 |

测试数值应该缩短验证时间，例如英雄复活、首波出兵、野怪刷新和升级都可使用
较短测试值；这些不是正式平衡值。

## 4. 用户需要准备的 UI 资源

### 4.1 页面 Prefab

| 页面 | 当前资产状态 | 用户准备 |
|---|---|---|
| Main / Match | 页面 Prefab、Lua 生命周期、账号显示、开始/取消匹配路由已绑定；取消路径有 EditMode 覆盖 | 仅做最终布局/视觉/交互手感验收 |
| Select | 目录驱动英雄列表、头像、选择/锁定和队内重复选择约束已绑定 | 仅做最终视觉验收 |
| Load | 页面和流程已绑定 | 下一 UOS 包需增加 payload/barrier/HUD 时间诊断并复验过渡时机 |
| HUD | Lua 驱动，51 个引用覆盖生命/资源/QWER/被动/金币/属性/装备格/BuffBar/比分 | 补最终技能图标、提示、视觉布局；小地图仍保留 C# controller |
| Shop | 页面 Prefab/Lua/按钮/装备格已有绑定 | 需要正式测试商品目录；目录为空时不能验收购买闭环 |
| Result | 页面 Prefab/Lua 已绑定 | 需要完成真实 Result/Continue/Return/settlement 实机验收 |
| UI Root | `UIManager` 管理七个页面 Prefab，页面不再挤在单一 ClientUI 上 | 仅做最终视觉与层级验收 |

### 4.2 UI 组件约定

- 文本统一使用 TextMeshPro；Codex 修正旧控制器，不要求用户制作
  `UnityEngine.UI.Text`。
- Button、Slider、Image、RawImage、ScrollRect 等实际组件由用户放好。
- 子节点名称可读且稳定，但正式绑定使用序列化引用，不使用
  `GameObject.Find()`。
- 页面根只负责布局和显示，不直接访问 Gameplay。
- HUD 和页面 Lua 通过只读 Runtime/Unit/Handler/View 查询数据。
- UI 点击只调用应用流程或类型化 Request；不直接写 `StatHandler`、
  `AbilityRuntime`、`EquipmentHandler` 或 CommandCollector。
- Shop 作为 HUD 上方 Overlay；打开 Shop 时 HUD 保持显示。

## 5. Codex 的绑定工作

用户交付新的或尚缺的单位 Prefab（当前主要是 TestMonster）后，Codex 将：

1. 用 Unity MCP 检查层级、Animator、Clip、材质、根 Transform 和语义挂点。
2. 在单位根添加并绑定正式 MonoBehaviour 组件。
3. 按 Prototype 的 HandlerLoadout 配置英雄、小兵、野怪和塔。
4. 创建或绑定 `UnitAnimationProfile` 和 `PresentationSocketSet`。
5. 在 `GlobalPrefabTable` 增加唯一 Unit PrefabId。
6. 在 `UnitRuntimeCatalogAsset` 增加唯一 UnitPrototypeId 和测试数值。
7. 英雄绑定现有中立 AbilityLoadout；远程单位和塔绑定中立 ProjectileDef。
8. 校验 Inspector float 只在 Bake 时转换，Gameplay 运行值使用项目 `fp`。
9. 检查所有 Prefab 的 Required Component、稳定 ID 和目录引用。

用户交付 UI Prefab 后，Codex 将：

1. 绑定 `UIManager` 的 PageRoot、OverlayRoot 和页面注册。
2. 为页面和 Cell 绑定 `UIPanel` / `LuaHost`。
3. 建立真实 xLua Loader、模块实例、Show/Refresh/Hide/Dispose 生命周期。
4. 绑定 TMP、Button、Slider、Image、列表和 Cell 引用。
5. 修正 Client 只读取 `LocalControlledUnitUid` 对应单位。
6. 绑定 HUD、Shop、Result、Select 和大厅应用流程。
7. 禁止动态生成占位 UI 作为“成功”回退。

地图和完整夹具由 Codex 通过 Unity MCP 配置：

- 一条测试 Lane，两队 Spawn 和稳定 Centerline。
- 每队至少一座塔和一座基地。
- 一处普通 JungleCamp，至少一个 TestMonster 槽位。
- 一份短周期 MinionWaveConfig。
- 两个 TestHero 初始出生和玩家槽位。
- 至少两个中立测试装备，用于购买、出售、撤销。
- 所有 GameBootstrap 序列化引用和 Build Scene 引用。

## 6. 资源验收清单

每个单位 Prefab：

- [ ] Unity MCP 可以打开且无 Missing Script。
- [ ] 根 Transform 合格，Animator 和 Model 存在。
- [ ] Root Motion 关闭。
- [ ] 必需 Clip 可播放，状态名和参数已冻结。
- [ ] 语义挂点齐全或有明确 fallback。
- [ ] Gameplay 根只由指定 Presentation 同步点写 Transform。
- [ ] 单位、Handler、Physics 和 Presentation 组件引用不为空。
- [ ] GlobalPrefabTable 与 UnitRuntimeCatalog Bake 成功。

每个 UI Prefab：

- [ ] 页面根、Button、TMP、Slider/Image 引用齐全。
- [ ] 没有依赖动态名称查找。
- [ ] Lua 模块可以创建实例并完成 Show/Refresh/Hide/Dispose。
- [ ] 点击只进入类型化应用/Command Request。
- [ ] Client 0 和 Client 1 各自显示自己的本地 Hero。

测试场景：

- [ ] Lane、波次、塔、基地、营地和初始 Hero 全部绑定。
- [ ] UnitPrototype/PrefabId/CampId/LaneId/SpawnOrder 唯一。
- [ ] Dedicated Server 场景不引用玩家输入或客户端表现对象。
- [ ] Client 场景不拥有权威 Gameplay 配置副本。
- [ ] 空装备数据库、空波次或空 Lane 不再被当成完整测试配置。

## 7. 构建与测试频率

- 普通代码修正只触发一次脚本编译，并运行受影响的最小 EditMode 测试。
- 只有 Prefab、场景、Input、UI 或 Unity 生命周期变化时运行对应 PlayMode 测试。
- 不在每个资源小改动后运行三进程流程。
- 完整资源门禁通过后只发起一次 Server/Client 构建请求。
- 构建请求发出后停止，等待用户确认构建结束；不得重复发起 Build。
- 首次 UOS 上传和两客户端 Gameplay 已在用户授权下完成。后续上传仍只在
  服务端代码/场景或镜像内容变化时进行；只改客户端时可复用已上传镜像。

## 8. 完整测试开始门禁

当前门禁状态：

- [x] 塔 AI/Attack、Minion AI/兵线移动、生成、死亡/回收和基地胜负链存在并
  通过过真实地图聚焦验证。
- [x] 正式 xLua、`UIManager`/`UIPanel`/Lua 页面与 Cell 绑定完成。
- [x] Hero/Minion/Tower 的 Unit/Handler/Physics/Presentation/Socket 进入正式
  `Assets/Config/Formal/Prefabs/` 资源链。
- [x] Lane、Wave、Tower、Base、Hero 和六份 team/size 流场配置已绑定/Bake。
- [x] 当前 UOS owner-race 修复尝试可以编译，helper-return EditMode 1/1；这只
  是编译/局部测试证据，不代表行为修复完成。
- [x] 本地 Server + 两 Client 包和 UOS Server/Client 包均曾完成构建；UOS
  两客户端已进入持续 Gameplay。
- [ ] `TestMonster` 和 `JungleCamp` 的正式测试资源、配置、Bake 与行为验收。
- [ ] 至少一组可购买/出售/撤销的测试 Equipment 目录和场景接入。
- [ ] 修正 UTP 启动 send queue 容量，并用新包证明日志不再 queue-full。
- [ ] 先补齐 UOS owner-race 的 NGO callback guard 与行为测试，再用新包证明
  异常消失，并用时间标记解释 Loading/HUD 延迟。
- [ ] 完成基地死亡 -> Result -> Continue/Return to Lobby -> remote settlement
  的本地/实机验收。
- [ ] 由用户完成 UI、单位动画/视觉和实际操作手感的最终人工验收。

现阶段可以准确报告“本地和 UOS 两客户端已跑通到持续 Gameplay”，但不能报告
“包含商店、野区、结算和返回大厅的整局验收全部通过”。
