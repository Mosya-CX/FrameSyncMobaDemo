# 完整对局测试计划

> 更新日期：2026-08-10（执行状态同步）
> 前置条件：`Docs/Implementation/TEST_PREPARATION.md` 第 8 节门禁全部通过。  
> 测试内容均为中立夹具，不代表正式英雄、技能、装备或平衡内容。

> 当前执行状态：本地 packaged C/S 已由仓库所有者暂时接受；真实 UOS 两客户端
> 已跑到持续 Gameplay，但本计划尚未全通过。JungleCamp/TestMonster、测试装备
> 商品目录、Result/Return/remote settlement 仍缺；UOS owner-race 的当前
> 修复尝试还漏掉 NGO connection callback，启动 send queue/Loading 时间也需
> 修正和新包复验。不要因为网络和兵线已运行，就跳过这些
> 未完成条目。实时交接证据见 `Docs/Implementation/CURRENT_HANDOFF.md`。

## 1. 通过标准

完整通过必须同时证明：

- Server 与两个 Client 使用同一开局配置和确定性数据版本。
- Client 0/1 分别控制自己的 TestHero，不串 UID、输入或 UI。
- 兵线、塔、野怪、英雄、基地和胜负流程在连续 Gameplay Tick 中工作。
- 所有 Gameplay 行为从正式 Order/Command/Handler/Combat 管线进入。
- 两个 Client 的权威 Tick、SharedGameplayChecksum 和可见结果一致。
- Snapshot/Restore/Replay 后结果等价，无恢复引用错误。
- UI/Lua 只读 Gameplay，不向 Gameplay 回写表现状态。
- 一方基地死亡后由 MatchRuleRuntime 产生正式结果并显示 Result。

## 2. 测试拓扑

```text
Dedicated Server
├── Team 1: TestHero A + TestTower A + TestBase A + Minion waves
├── Team 2: TestHero B + TestTower B + TestBase B + Minion waves
└── Neutral: one TestJungleCamp + TestMonster

Client 0 -> PlayerSlot 0 -> TestHero A
Client 1 -> PlayerSlot 1 -> TestHero B
```

第一轮只使用一条 Lane、每队一座塔、每波少量近战/远程兵和一个普通野怪
营地。测试小而完整，不同时验证多路兵线或史诗野怪。

## 3. 阶段 A：资源和 Prefab 门禁

由 Codex 使用 Unity MCP 执行：

1. 打开每个单位 Prefab，检查组件、Animator、Profile、Sockets 和序列化引用。
2. Bake GlobalPrefabTable、UnitRuntimeCatalog、Ability/Projectile 目录。
3. 打开所有 UI Prefab，检查页面、Cell、TMP、Button、Slider/Image 和 LuaHost。
4. 打开 ClientBootstrap 和 ServerBootstrap，检查场景引用。
5. 触发一次脚本编译并读取 Console。
6. 只运行资源 Bake、Prefab composition、Lua 生命周期和场景绑定的聚焦测试。

失败条件：

- Missing Script、Missing Reference、重复稳定 ID。
- Unit/Handler/Physics/Animator/Socket 绑定缺失。
- Lua 模块不能实例化或页面生命周期不完整。
- ClientBootstrap 仍为空 Lane/Wave/Camp/Equipment。

## 4. 阶段 B：单单位局部验收

### TestHero

1. 出生后 Idle 正常。
2. Move Command 经过正式输入转换，位置只由 PhysicsEntity2D 权威更新。
3. 普通攻击完成 Begin/Commit/Recovery，并驱动正确动画。
4. 三个中立技能至少各完成一次 Focus/Commit 或普通 Commit。
5. 生命、资源、冷却、经验、等级和金币在 HUD 正确刷新。
6. 死亡进入 Dead，随后按测试时间 Respawning -> Alive。

### TestMonster

1. Camp 初次刷新生成正确 Prototype。
2. Idle 时发现合法 Hero，整个营地进入 InCombat。
3. 追击不越过 HardLeash。
4. 脱战后 ReturnToCamp，不直接写生命或清除其它系统状态。
5. 主怪死亡后进入 WaitingRespawn，并生成新 UnitUid。

### Minion 与 Tower

1. 波次按稳定 TeamId/LaneId/EntryIndex 顺序生成。
2. Minion 沿 Lane 推进、选择敌方 Minion、合法协防并在越界后回线。
3. Tower 不移动、不追击，按正式六级优先级选目标。
4. Tower 在上一发塔弹结束前不能开始下一发。
5. 红线只读取锁定状态，不拥有 Gameplay 目标。

## 5. 阶段 C：单进程完整逻辑闭环

在测试场景中加速运行：

1. 进入 Countdown -> Running。
2. 首波双方小兵生成并交战。
3. Hero 击杀 Minion，确认 Gold/XP/KDA 变化。
4. Hero 升级并分配一次技能点。
5. Hero 拉野、脱战、重新进入、击杀主怪并观察刷新。
6. Hero 进入敌塔范围，验证 Minion/英雄目标优先级和塔弹门控。
7. Hero 死亡并复活，确认永久状态与当前生命阶段状态边界。
8. 购买一个测试装备、出售、撤销，确认金币和装备槽。
9. 摧毁敌方基地，观察 Ending -> Finished 和 MatchResult。

该阶段证明 Gameplay 逻辑闭环，不替代网络和 UI 双客户端验收。

## 6. 阶段 D：本地 Server + 两 Client 完整对局

### 启动

三个进程都必须带 `-logFile` 写到 `Builds/LocalNgo/Logs/`（命名 `cs<N>_<yyyyMMdd>_{server,client0,client1}.log`），
完整启动命令见 `Docs/Implementation/BUILD_GUIDE.md` 第 4 节，完整测试流程/检查清单/日志解读见
`Docs/Implementation/C_S_TEST_GUIDE.md`。

1. 启动一个 Dedicated Server（不带 slot 参数）。
2. 启动 Client 0（`--LocalPlayerSlot=0`）。
3. 启动 Client 1（`--LocalPlayerSlot=1`）。
4. 两端完成身份、选择、Ready、Loading 和同一 StartTick。

### 客户端本地性

分别在两个 Client 检查：

- Camera 跟随本地 Hero。
- HUD 生命、资源、技能、装备、金币和 KDA 属于本地 Hero。
- Client 1 不显示 Client 0 的第一个受控单位。
- 输入只生成自己的 canonical GameplayCommand。
- Shop、Select、Result 的 Lua 实例互不共享页面状态。

### 对局行为

1. 两名玩家分别移动和攻击。
2. 同一技能在两个 Client 上观察到相同执行 Tick 和结果。
3. 一方击杀小兵、野怪和对方英雄，核对双方 HUD 与 Scoreboard。
4. 完成一次商店购买/出售/撤销。
5. 观察两波 Minion、一次 JungleCamp 重置/刷新和一次 Hero 复活。
6. 摧毁一方基地并进入 Result。

### 日志与确定性

保存 Server、Client 0、Client 1 日志，并检查：

- AuthorityFrame 连续。
- 没有重复 UID 或非法稳定引用。
- 没有 checksum mismatch。
- 没有越过 `LatestAuthorityFrameTick + 1` 的普通回滚。
- 没有 Snapshot topology/Resolve/Rebuild 错误。
- 没有 UI/Presentation 写回 Gameplay 的异常。
- 没有断线、恢复重试耗尽或不同胜负结果。

## 7. 阶段 E：受控回滚与恢复

只在阶段 D 稳定通过后执行一次：

1. 使用已有测试注入点制造一个允许范围内的晚到 Command。
2. 验证预测回滚和 Replay 不重读 Unity Input System。
3. 比较连续执行与 Restore/Replay 后的关键 Unit、Minion、Camp、Tower、
   MatchRule 和 Equipment 状态。
4. 验证 SharedGameplayChecksum 再次一致。
5. 验证动画、HUD、VFX/SFX 从恢复后只读状态重建，没有重复 Gameplay 输出。

不通过删除快照成员、跳过 checksum 或吞异常来获得成功。

## 8. 阶段 F：UOS

仅在本地完整测试通过并由用户明确授权后：

1. 只提交一次 Linux Dedicated Server Build。
2. 用户确认 Build 完成后，再配置/上传 UOS Game Server。
3. 验证 Allocation、健康、两 Client 加入、结果、结算和 Server 退出。
4. 保存 UOS 控制台和实例日志。

本地 NGO 成功不能代替 UOS 成功；UOS 失败也不能反向修改 Gameplay 设计。

## 9. 失败记录格式

```text
阶段：
Server Tick / Client Tick：
Client Slot：
UnitUid / Command bytes / SnapshotTick：
操作：
预期：
实际：
Console / Player log：
截图或录像：
是否可稳定复现：
```

P0：确定性、协议、数据损坏、核心行为或完整对局无法继续。  
P1：计划行为、依赖方向、绑定或测试要求未满足。  
P2：不影响当前完整对局正确性的局部技术债。

任何 P0/P1 修复后，只重复受影响的最小阶段；只有网络、Bootstrap、
Snapshot、场景或 UI 绑定改变时才重新跑阶段 D。
