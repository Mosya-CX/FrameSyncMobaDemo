# 完整对局测试准备

> 更新日期：2026-07-29  
> 目标：用中立测试内容跑通一场“一条兵线、双方英雄与防御塔、一个普通野怪营地、基地胜负、UI 与本地双客户端”的完整对局。  
> 本文只规定测试资源、绑定责任和开始门禁，不把测试内容视为正式英雄或正式平衡内容。

## 1. 当前结论

当前仓库已经能完成 Dedicated Server + 两个 Client 的 NGO 启动、开局
Payload、初始 Snapshot 和连续 Tick 冒烟验证，但还不能据此开始完整 MOBA
对局验收。

当前缺口分为三类：

1. **代码侧阻断**：塔、小兵、正式 Lua/UI 宿主和动画驱动仍与当前设计不符。
2. **测试配置缺失**：没有小兵、塔、野怪 UnitPrototype，没有波次、Lane、
   JungleCamp、装备测试目录和完整场景绑定。
3. **用户资源待交付**：测试单位和 UI 的视觉 Prefab、模型、动画、图标与布局。

完整测试只有在第 8 节门禁全部通过后开始。

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
4. Codex返回缺失项或“已通过绑定门禁”。
5. 用户继续做纯视觉迭代时，不更改已冻结的稳定根节点和语义挂点名。

## 3. 用户需要准备的测试单位资源

### 3.1 最小单位集合

| Prefab | 完整对局是否必需 | 最低表现 |
|---|---:|---|
| `TestHeroVisual` | 是 | Idle、Move、NormalAttack、GenericCast、Death、Respawn |
| `TestMonsterVisual` | 是 | Idle、Move、NormalAttack、Hit（可选）、Death |
| `TestMeleeMinionVisual` | 是 | Idle、Move、NormalAttack、Death |
| `TestRangedMinionVisual` | 是 | Idle、Move、NormalAttack、Death；需要发射挂点 |
| `TestTowerVisual` | 是 | Idle、Attack；需要发射挂点 |
| `TestTeamBaseVisual` | 可先复用中立几何体 | Idle、Destroyed 或禁用表现 |

这些名称是中立测试夹具名。已有具体英雄模型可以被选作视觉素材，但运行时
Prototype、测试、Lua 和计划中不得使用具体英雄名或写入英雄专属逻辑。

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
| Main / Match | `LobbyPanel` 有视觉层级但契约不完整 | 开始匹配、取消、状态文本、Ready 区域 |
| Select | `SelectPanel` 和 `HeroSelectCell` 已有基础视觉 | 英雄列表、确认按钮、确认状态；Cell 增加可点击 Button |
| Load | `LoadingPanel` 已存在 | ProgressBar、ProgressText |
| HUD | `GameplayHUD` 已有基础视觉 | 补生命/资源/经验/金币/KDA/技能/装备/小地图引用 |
| Shop | 没有可绑定的正式 Prefab | 商品列表、详情、Buy/Sell/Undo/Close、金币、装备格 |
| Result | 没有可绑定的正式 Prefab | 胜负标题、结束原因、KDA、Continue |
| UI Root | `UIManager` 只有 Canvas/EventSystem | `PageRoot`、`OverlayRoot` |

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

用户交付单位 Prefab 后，Codex 将：

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
- UOS 上传只在本地完整对局通过且用户明确授权后进行。

## 8. 完整测试开始门禁

以下条件缺一不可：

1. 塔正式 AI/Attack、Minion AI/兵线移动修正完成。
2. 正式 xLua 环境、UIManager/UIPanel/LuaHost 和 Prefab 页面绑定完成。
3. UnitAnimationDriver/UnitPresentationHost/Socket 绑定符合 Presentation v13.2。
4. 用户交付的 TestHero、TestMonster、Minion、Tower 和 UI Prefab 通过资源验收。
5. Lane、Wave、Camp、Tower、Base、Equipment 和 Hero 初始配置 Bake 成功。
6. Unity 编译无新增错误。
7. 相关最小 EditMode/PlayMode 门禁通过。
8. Server + 两 Client 构建各完成一次。

在此之前可以做局部资源验收和单系统测试，但不得报告“整局已跑通”。
