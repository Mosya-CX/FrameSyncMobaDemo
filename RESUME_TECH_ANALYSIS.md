# FrameSyncMobaDemo 面向 Unity / 游戏客户端岗位的技术价值分析

> 分析日期：2026-08-25  
> 分析对象：当前仓库源码、配置、README、`PROJECT_AUDIT.md`，以及用户提供的 `陈曦_Unity游戏客户端简历_校招版_v2.pdf` 中 `FrameSyncMobaDemo` 板块。  
> 边界：本文分析“项目能证明什么技术能力”，不替代个人贡献说明。仓库能证明代码和资产存在，不能单独证明每段代码的个人作者归属；现有简历已披露“主要代码实现由 Codex 完成”，因此后续表达应继续使用“主导设计 / 约束 / 审查 / 验收 / 推动实现”等符合实际职责的动词，不能改写成“个人独立实现全部系统”。  
> 验证方式：对所有 A 类候选重新检查了源码、配置和调用链；本轮没有重新运行 Unity 编译、测试、Player Build 或多进程/UOS 实机验收。

## 1. 项目招聘价值总结

这是一个以**确定性帧同步 MOBA 垂直切片**为目标的 Unity 2022.3 LTS 技术工程。它的招聘价值不在“做出了多少英雄或完整玩法”，而在于把以下高约束问题放进了同一套可运行架构：

- 固定 Tick、定点数 Gameplay、canonical Command、服务端 AuthorityFrame、客户端预测、Snapshot/Restore/Replay 和 checksum 校验；
- Unit 行为规划、动作资源仲裁、技能阶段状态机、投射物和战斗结算均进入同一确定性 Tick；
- 同 Tick 多来源伤害不是简单按容器遍历顺序扣血，而是进行封口、稳定排序、批量护盾/生命分配和确定性击杀归属；
- 自定义固定点 2D 空间网格、A*、团队 Flow Field、确定性 RVO 和投射物 sweep 检测取代 Unity Physics/NavMesh 作为 Gameplay 权威；
- 客户端 Addressables View 与同步逻辑 Prefab 分离，并通过程序集约束、场景剥离和构建产物审计控制 Dedicated Server 资源边界。

对 Unity / 游戏客户端岗位而言，项目最能证明的是：**复杂 Gameplay 数据流设计、确定性约束、网络纠错思维、状态恢复、跨模块契约、资源与构建工程化**。它不适合包装成商业化完整 MOBA、成熟线上网络产品或性能压测项目。

### 招聘判断

- **技术深度：高。** Snapshot、重演、稳定身份、同 Tick 公平结算、动作仲裁等均有非表面实现。
- **工程复杂度：高。** 核心结论跨 FrameSync、Gameplay、Physics、Bootstrap、ClientContent 和 RuntimeConfig 多程序集。
- **差异化：高。** 相比常规 MonoBehaviour 项目，确定性恢复边界和战斗公平性更容易形成有效面试讨论。
- **产品完成度：中低。** 当前是技术垂直切片；正式内容量、HUD、丛林、结果结算和当前版本 UOS 实机验收仍有限。
- **性能证明力：低。** 有避免全量查询、复用缓冲、对象池和预计算等代码结构，但没有当前可信的 CPU、GC、内存、网络带宽或实体规模数据。
- **个人贡献表达风险：中高。** 现有简历明确披露 AI Agent 参与主要实现。技术点可以写，但必须能解释关键设计、审查标准、失败案例和验收证据，且不能模糊个人与 Agent 的贡献边界。

### 对 `PROJECT_AUDIT.md` 的复核结论

审计文档的核心技术判断与抽查源码基本一致，但有三点需要在简历分析中进一步收紧：

1. `GameplaySnapshot.CurrentSchemaVersion` 的源码事实是 **24**（`Assets/Scripts/FrameSync/GameplaySnapshot.cs:62`），README 的 23 已过时。
2. “UOS 已验收”只适用于历史包；`Docs/Implementation/CURRENT_HANDOFF.md:201-229` 和 `MODULE_STATUS.md:41,46` 明确说明 schema-24 / GameplayDataVersion-4 匹配端点及修正后资源包仍待实机复验。
3. `PROJECT_AUDIT.md` 的 77,023 行运行代码、40,695 行测试代码是**非空行口径**；静态 `[Test*]` 数也只是文本属性计数。数值可复现，但不应作为简历成果。

## 2. 最强技术卖点

按“招聘价值 × 源码可信度 × 面试展开空间”排序：

1. **确定性帧同步、客户端预测与 Snapshot/Rollback/Replay 闭环。** 这是项目定位和最强差异化能力。
2. **同 Tick 战斗公平结算与稳定 action identity。** 体现对顺序依赖、随机流、伤害分配和击杀归属的深入处理。
3. **Intent → Planner → Arbiter → Main/Base ActionRuntime → Handler 的 Gameplay 动作架构。** 能说明复杂状态机如何统一移动、普攻、施法与控制打断，并进入 Snapshot。
4. **固定点自研 2D 空间查询、A*/Flow Field/RVO/投射物命中。** 算法真实接入 Tick，而不是独立 Demo；但没有性能规模数据。
5. **客户端 View / Server Logic 资源拆分与构建守卫。** 兼具客户端资源管理、回滚表现绑定和 Dedicated Server 打包工程价值。

NGO/UTP/UOS 不是独立的第一卖点。它们的价值在于承载上述帧同步协议和应用流；若只写“使用 NGO/UOS”，技术含量会显著下降。

## 3. A/B/C/D 技术点分类

评分均为 1～5。除“面试风险”外，分数越高越好；面试风险 1 表示低风险，5 表示极易因归属、成熟度或数据不足而被追问失守。

### A：强烈推荐写

| 技术点 | 技术深度 | 工程复杂度 | 差异化 | 招聘价值 | 可验证性 | 面试风险 | 判断 |
|---|---:|---:|---:|---:|---:|---:|---|
| 确定性帧同步 + 预测回滚 | 5 | 5 | 5 | 5 | 5 | 3 | 有完整命令、权威帧、快照、校验和重演调用链；最应保留。 |
| 同 Tick 战斗公平结算 | 5 | 5 | 5 | 4 | 5 | 4 | 工程含金量高，但必须能解释批量分配、稳定键和为何不能按遍历顺序结算。 |
| Unit 动作仲裁 + 数据驱动技能阶段 | 4 | 5 | 4 | 5 | 5 | 4 | 比“做了技能/Buff 系统”更有架构辨识度；需避免罗列模块名。 |
| 固定点 2D 查询、A*/Flow Field/RVO/投射物命中 | 4 | 4 | 4 | 4 | 5 | 4 | 算法接入主 Tick，适合客户端 Gameplay 岗；无规模/帧耗数据。 |
| Addressables View / Logic 拆分 + Dedicated Server 构建审计 | 4 | 5 | 4 | 5 | 5 | 3 | 解决真实跨平台资源污染与 Server 包裁剪问题；最终修正包尚待验收。 |

### B：可以写

| 技术点 | 技术深度 | 工程复杂度 | 差异化 | 招聘价值 | 可验证性 | 面试风险 | 判断 |
|---|---:|---:|---:|---:|---:|---:|---|
| NGO/UTP 自定义消息协议、命令中继和 authority recovery | 4 | 4 | 4 | 5 | 5 | 4 | 有 named message 和 wire codec，不是简单 NetworkVariable；适合作为帧同步卖点的证据。 |
| UOS Matchmaking / Multiverse / Dedicated Server 应用流 | 3 | 5 | 4 | 4 | 4 | 5 | 历史包有实测，当前 schema-24 匹配重建待验收；只能谨慎表述。 |
| 输入事件一次性转换为 Command，回滚不重读设备 | 3 | 4 | 3 | 4 | 5 | 3 | 是正确且重要的回滚边界，但独立成 Bullet 的竞争力低于 A 类。 |
| 分层自动测试与确定性诊断 | 3 | 4 | 3 | 4 | 4 | 4 | 测试源码丰富；当前全量通过数记录冲突且存在 retained failures，不能写“全量通过”。 |
| 整数时间 authoring → Tick Bake 与版本化配置 | 3 | 4 | 3 | 4 | 5 | 3 | 是容易被忽略的确定性内容管线卖点，适合作为 A 类系统的补充细节。 |

### C：视篇幅决定

| 技术点 | 判断 |
|---|---|
| Buff / CC / Equipment / Gold / AI 等通用 Gameplay 模块 | 代码真实且有 Snapshot，但单独罗列会变成模块清单；应服务于动作架构或战斗结算主线。 |
| Lua UI、UGUI、Input System、Cinemachine、DOTween、UniTask、Odin | 属于常用客户端能力；本项目中不是差异化核心。Lua 主要用于 UI/表现桥接，不是 Gameplay 热更新。 |
| 投射物对象池与复用缓冲 | 有价值但常规；没有 GC/帧耗对比，适合面试细节，不宜占主 Bullet。 |
| Editor Bake/Validator、构建菜单、诊断日志 | 工程性较好，可在工具链岗位或有额外篇幅时使用。 |
| Presentation 平滑、Camera、动画状态映射 | 实现存在，但项目核心竞争力不是渲染或动画系统。 |

### D：不建议写

| 技术点或表述 | 原因 |
|---|---|
| ECS / DOTS / Burst / Job System / NativeContainer | 当前项目首方运行代码无对应引用；这些属于简历中另一个 `ECS Shooting Demo`，不能混入本项目。 |
| Unity Physics / NavMesh 作为 Gameplay 方案 | 本项目实际使用自定义固定点 Physics/Pathfinding；写 Unity Physics 反而与源码冲突。 |
| GPU Skinning / Compute Shader / GPU 实例化 / Draw Call 优化 | 当前项目没有首方实现证据。 |
| “支持大规模实体”“完整 5v5”“10 人已压测” | `MaxPlayers=10` 只是配置上限，没有容量、帧耗、网络或长稳压测数据。 |
| “零 GC”“性能提升 X%”“网络流量降低 X%” | 没有 Profiler/benchmark 前后对比；运行时仍使用托管集合、数组复制及 View Instantiate/Destroy。 |
| “完整商业 MOBA”“完整英雄/装备/丛林内容” | 当前 Formal 内容是垂直切片；主场景 `jungleCamps` 为空，内容规模有限。 |
| “自研网络传输层” | NGO/UTP 是传输基础；项目自研的是帧同步消息、wire codec 和 Gameplay 协议。 |
| “所有测试通过”“当前线上/UOS 版本已验收” | 文档保留 10 个 Unit 和 4 个 PlayMode failure；当前 matching endpoint 实机复验待完成。 |

## 4. A 类技术源码复核

### A1. 确定性帧同步、预测回滚与权威恢复

**实际实现**

- Command 带目标 Tick、PlayerSlot、受控 UnitUid 和 CommandSeq；`CommandCollector` 先按命令语义合并，再按稳定键生成 canonical 顺序。
- 服务端在固定 Tick 执行 canonical commands，构造含最终命令 bytes、revision 和 shared checksum 的 `AuthorityFrame`。
- 客户端保存逐 Tick command history、verification record 和 Snapshot；AuthorityFrame 必须连续处理。
- 客户端同时比较**完整 canonical command bytes**与 checksum。任一不一致时，从 `frame.Tick - 1` 的 Snapshot anchor 恢复，应用该权威 Tick 命令并重演到原预测末端。
- Restore 明确分为 Restore / Resolve / Rebuild；非法 schema、无序/重复 UID、缺失稳定 participant reference 等会显式抛出确定性异常，不静默修复。
- authority frame 缺口有 recovery request/response 代码，预测还受最大领先 Tick 限制。

**代码证据**

- `Assets/Scripts/FrameSync/GameplayCommand.cs`：`GameplayCommandHeader.WriteCanonical`、Command DTO。
- `Assets/Scripts/FrameSync/CommandCollector.cs:66-116, 250-259`：canonical 收集、指定 Tick 消费、稳定排序。
- `Assets/Scripts/FrameSync/CanonicalCommandCodec.cs`：完整 command/bundle 编解码和 byte equality。
- `Assets/Scripts/FrameSync/AuthorityReplication.cs:552-607`：`AuthorityFrameReplicator.ExecuteNextTick` 执行服务端 Tick、捕获状态并生成权威帧。
- `Assets/Scripts/FrameSync/PredictionRollbackCoordinator.cs:153-229`：逐 Tick Snapshot、预测执行、authority buffer。
- `Assets/Scripts/FrameSync/PredictionRollbackCoordinator.cs:300-365`：连续处理帧，比较 command revision、canonical bytes 和 checksum。
- `Assets/Scripts/FrameSync/PredictionRollbackCoordinator.cs:421-528`：`CorrectAndReplay` 恢复 anchor、保留未来命令、注入权威命令并重演。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs:460-526`：聚合 Unit、Combat、Equipment、Projectile、Physics、Minion、Jungle、AI 等 Snapshot。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs:529-850`：schema 检查以及 Restore/Resolve/Rebuild 三阶段。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs:62`：当前 schema 为 24。
- `Assets/Scripts/Bootstrap/FrameSyncNetworkBridge.cs:189-212, 244-386`：NGO named messages 接入 bundle、relay、authority、recovery。

**调用关系**

```text
Input / UI
  -> PlayerCommandRequester
  -> GameplayCommand
  -> CommandCollector canonicalize
  -> FrameSyncNetworkBridge / NGO named message
  -> Server CommandRelayBuffer
  -> AuthorityFrameReplicator.ExecuteNextTick
  -> SimulationTickPipeline.ExecuteTick
  -> GameplaySnapshot + SharedGameplayChecksum
  -> AuthorityFrame
  -> Client PredictionRollbackCoordinator
     -> bytes/checksum match: confirm
     -> mismatch: Restore(frame.Tick-1) -> Replay(frame.Tick..predictedEnd)
```

**真正体现的能力**

- 能把“网络消息到达”与“Gameplay Tick 权威”分离；
- 理解预测回滚必须保存输入历史、状态历史和校验历史，而不只是“把位置拉回去”；
- 能定义恢复拓扑、引用解析和派生缓存重建的边界；
- 能处理乱序/缺帧、预测领先限制、未来命令保留和不可恢复的硬分叉。

**建议简历表达方向**

围绕“设计并验收一套 canonical Command + AuthorityFrame + Snapshot/Checksum 的确定性同步闭环；客户端在命令 bytes 或状态校验不一致时按合法 anchor 恢复并重演”展开。应强调问题、恢复边界和验证方式，不只罗列 Rollback/Replay 名词。

**不能写成什么**

- 不能写“完整 Rollback 已在线上大规模验证”。
- 不能写“自研底层网络传输”；底层是 NGO/UTP。
- 不能写“服务器下发完整状态同步”；常规 AuthorityFrame 主要携带 canonical commands + checksum，Snapshot 用于本地回滚及 recovery 路径。
- 不能把 schema 23 写成当前版本；源码是 24。

**面试官可能追问**

1. 为什么既比较 command bytes 又比较 checksum，只比较 checksum 不够吗？
2. Snapshot 的 Tick 语义是什么，为什么 ordinary rollback anchor 是 `frame.Tick - 1`？
3. Restore / Resolve / Rebuild 各自允许做什么，为什么不能合成一个方法？
4. AuthorityFrame 中间缺 Tick 时客户端如何处理？为什么不能直接确认更新的帧？
5. 回滚时未来尚未执行的本地 Command 为什么需要单独保留？
6. 哪些状态必须进入 schema 24，哪些派生缓存不应序列化？
7. 重演后 checksum 仍不一致时如何定位？
8. 当前方案的 Snapshot 内存和网络恢复成本是多少？——仓库没有 profiler/带宽数据，应明确“无法确认”。

### A2. 同 Tick 战斗公平结算与稳定 action identity

**实际实现**

- Combat 每 Tick 维护 Shield/Heal/Damage 请求队列；结算时把当前队列长度封口，反应产生的新请求进入下一波/延迟边界，避免递归回调改变当前遍历顺序。
- Damage 按目标形成 batch，先在同一批次起始状态上评估，再依次分配物理专属盾、魔法专属盾、白盾和生命值。
- 当可用护盾/生命不足时按伤害权重分摊，并用稳定 remainder score 处理定点数余数，避免“先遍历到的请求吃掉全部资源”。
- `OriginActionId` 由 GameplayParticipantId、来源类型、来源 ID、起始 Tick 和 source-local sequence 组成，刻意与技术性 UnitUid/PrefabId 解耦。
- 暴击与等距投射物目标不是消费全局随机流，而是使用 match seed + action identity + target participant + effect ordinal 生成稳定 hash。
- 同 Tick 致死 batch 会根据实际生命伤害贡献和稳定 tie score 决定 killer；正式死亡在队列清空后通过 UnitWorld 生命周期处理，同 Tick 治疗仍可能救回 Dying 单位。

**代码证据**

- `Assets/Scripts/Gameplay/Combat/CombatSystem.cs:137-229`：`BeginTick`、sealed wave 结算、`ResolveDying`、`EndTick`。
- `CombatSystem.cs:358-403`：请求 header 封口与 wave-start health。
- `CombatSystem.cs:440-470`：按 target 分组并批量评估 Damage。
- `CombatSystem.cs:597-725`：护盾/生命批量分配、守恒检查、按权重分摊。
- `CombatSystem.cs:799-870`：同批 lethal killer 的贡献与稳定 tie 选择。
- `CombatSystem.cs:1244-1330`：Dying 恢复、kill/assist 和正式 death 请求。
- `Assets/Scripts/Gameplay/Combat/CombatActionIdentity.cs:11-100`：`OriginActionId` 结构和稳定哈希。
- `CombatActionIdentity.cs:130-231`：`RollCrit`、`ProjectileTieScore`、EffectOrdinal 与 participant-local sequence。
- `Assets/Scripts/Gameplay/Projectile/ProjectileEffectDispatcher.cs:105-170, 250-265`：等距目标稳定排序及 DamageRequest provenance。
- `Assets/Scripts/Gameplay/Tests/CombatSameTickFairnessTests.cs`、`CombatActionIdentityTests.cs`：同 Tick 插入顺序、UID relabel、Crit/投射物稳定性定向测试源码。

**调用关系**

```text
Attack / Ability / Projectile / Buff reaction
  -> DamageRequest / HealRequest / ShieldRequest
  -> CombatSystem queues
  -> SettleActiveRequests
  -> seal current wave
  -> evaluate by target on wave-start state
  -> proportional shield/life allocation
  -> emit results / defer reactions
  -> ResolveDying through UnitWorld lifecycle
```

**真正体现的能力**

- 能识别 deterministic simulation 中“稳定排序仍不等于公平”的问题；
- 能在离散定点数下处理比例分配、余数守恒和稳定 tie-break；
- 能把 Gameplay 公平身份与运行时技术 UID 分开；
- 能设计死亡、治疗、反应事件与奖励之间的同步结算边界。

**建议简历表达方向**

突出“为同 Tick 多来源伤害建立批次结算与稳定身份键，消除容器插入顺序和技术 UID 对护盾分配、暴击、等距命中及击杀归属的影响”。这是比“实现 Combat 系统”更有技术价值的描述。

**不能写成什么**

- 不能只写“使用随机数保证公平”；核心是 action-keyed deterministic hash 与批量结算。
- 不能写“绝对公平/数学无误”；只能说实现了代码定义的稳定规则和守恒检查。
- 不能用这些测试源码推导“所有 Combat 测试通过”；当前全量 Unit suite 仍有保留失败记录。

**面试官可能追问**

1. 为什么稳定按 UnitUid 排序仍可能导致不公平？
2. 比例分配的定点数余数如何处理，如何保证资源守恒？
3. `OriginActionId` 为什么不能直接用 projectile UID 或 source UnitUid？
4. 暴击为什么不用一个可 Snapshot 的全局 PRNG 流？两种方案的权衡是什么？
5. 同 Tick 伤害和治疗的先后规则是什么？单位何时从 Dying 变成正式 Death？
6. 反应产生的新伤害为什么要延迟，如何防止递归改变当前波次？
7. 多个来源同 Tick 致死时 killer/assist 如何决定？

### A3. Unit 动作仲裁与数据驱动技能阶段

**实际实现**

- Command 首先更新 `UnitIntent`；`BehaviorPlanner.Tick` 将移动、追击、施法、线路推进、回营和控制强制行为转换为 `ActionRequest`。
- `ActionArbiter.Submit` 检查能力、控制阻断、Main/Base 槽与 Movement/Rotation 等资源冲突，再决定拒绝、抢占或启动对应 Handler。
- `ActionRuntimeSet` 只有固定 Main/Base 两个权威槽；它不是泛用动态状态机容器，而是负责移动、普攻、施法的生命周期所有权，并可 Capture/Restore/Resolve。
- `AbilityAsset.Bake` 将 ScriptableObject authoring 转换为纯运行时 `AbilityDef`；实际包含 Commit、Hold/Release、Channel、Active Signal、Toggle、Ground Target、Vector Target、Sequential Recast 等 CastModel。
- `AbilityHandler.HandleSignal/TickUpdate` 驱动 Stage 的 OnEnter/OnTick/OnSignal/OnExit，处理超时、打断、Toggle、蓄力和连段转移；运行状态进入 Unit Snapshot。
- Pipeline 在所有 Unit 间使用全局子阶段顺序运行 Buff、Equipment、HitReaction、Ability、Movement、Attack，避免一个 Unit 的后期 Handler 先于另一个 Unit 的早期 Handler。

**代码证据**

- `Assets/Scripts/Gameplay/Unit/Core/BehaviorPlanner.cs:32-210`：Intent 转换、攻击追击、施法距离判断。
- `Assets/Scripts/Gameplay/Unit/Core/ActionArbiter.cs:24-125`：资源/槽位/控制检查、抢占、Handler 启动、Runtime 刷新。
- `ActionArbiter.cs:248-310`：控制打断和运行时冲突检查。
- `Assets/Scripts/Gameplay/Unit/Core/ActionRuntimeSet.cs:6-129`：固定 Main/Base 生命周期和 Cancel。
- `ActionRuntimeSet.cs:150-224`：Capture/Restore/Resolve 及恢复一致性失败。
- `Assets/Scripts/Gameplay/Ability/AbilityAsset.cs:83-166`：Bake 与 Stage authoring 校验。
- `AbilityAsset.cs:258-691`：八类 CastModel authoring 到 runtime definition 的 Bake。
- `Assets/Scripts/Gameplay/Ability/CastModelDef.cs`：各模型的 signal/stage transition。
- `Assets/Scripts/Gameplay/Ability/AbilityHandler.cs:90-270, 455-575`：信号处理与逐 Tick 阶段推进。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs:192-300`：Planner/Arbiter/Handler 的确定性全局阶段顺序。
- `SimulationTickPipeline.cs:485-495, 769-785`：Ability 与 ActionRuntime 的 Capture/Resolve 接入。

**调用关系**

```text
GameplayCommand
  -> UnitIntent
  -> BehaviorPlanner
  -> ActionRequest
  -> ActionArbiter
     -> capability / CC / resource / Main-Base conflict
     -> Handler start / preempt / reject
  -> Ability / Attack / Movement Handler state machine
  -> ActionRuntimeSet reconcile
  -> UnitSnapshot / checksum
```

**真正体现的能力**

- 能用统一动作资源模型处理移动、攻击、施法、位移技能和控制打断，而不是在各技能里互相写 if/else；
- 能把 authoring 配置、运行时定义、状态机实例和 Snapshot 状态分层；
- 能在回滚系统中处理状态机引用恢复和不变量校验；
- 能设计跨 Unit 的全局阶段顺序以避免 UID 顺序造成系统性偏差。

**建议简历表达方向**

写“统一动作仲裁和可恢复技能状态机”，并用 1～2 个具体约束说明价值，例如 Main/Base 资源抢占、控制打断、Hold/Release/Sequential Recast 以及 Snapshot 恢复。不要把 Unit、Attack、Ability、Buff、CC、Projectile、Equipment 全部平铺成一行。

**不能写成什么**

- 不能写“完整数据驱动任意英雄系统”；当前 Formal content 主要是两个英雄的垂直切片。
- 不能把 ScriptableObject 使用本身写成核心创新；价值来自 Bake、运行时状态边界、仲裁和回滚接入。
- 不能写“行为树 AI”；当前主要是 Intent/Planner 和专用 AI Controller，不是通用行为树框架。

**面试官可能追问**

1. 为什么需要 Intent、ActionRequest、Handler 三层，直接 Command 调 Handler 有什么问题？
2. Main/Base 两个槽分别解决什么冲突？Dash、移动施法、普攻如何占资源？
3. 抢占前后 Handler 和 Runtime 的状态如何保持一致？
4. Hold/Release、Toggle、Sequential Recast 在同一个 CastModel 接口下如何表达？
5. 哪些 Ability 数据在 Bake 时验证，哪些在运行时检查？
6. ActionRuntime 和 AbilitySession 为什么都需要 Snapshot，它们如何互相校验？
7. 为什么 Pipeline 使用“所有 Unit 先跑 Ability，再全部跑 Movement”一类全局子阶段？

### A4. 固定点自研 2D 空间查询、寻路、避障与投射物命中

**实际实现**

- `PhysicsSpatialGrid2D` 按固定点 AABB 覆盖的 cell 插入实体；查询时用 visited 去重，最终按 RuntimeUid 稳定排序，避免 Dictionary/HashSet 枚举顺序进入 Gameplay。
- `AStarPathService` 使用固定点代价数组和可 decrease-key 的 `IndexedMinHeap`，支持半径类别与迭代上限；不是 Unity NavMesh。
- `TeamFlowFieldService` 以整数 Dijkstra 构造线路 cost field，再合并团队 field，并将方向编码为数组；运行时 `GetFlowDirection` 是 O(1) cell lookup。heap 在 cost 相同时按 cell index 决胜。
- `DeterministicRVOSystem` 对邻居按 UnitUid 排序，在预计算候选方向上计算定点 penalty，并用稳定 velocity tie-break 选择速度。
- Tick Pipeline 中先构建 RVO grid、应用路线移动，再做墙体穿透修正、构建最终碰撞 grid、处理 Unit collision 和 Projectile hit。
- `ProjectileHitResolver` 通过空间网格筛选候选并执行固定点 sweep/shape 测试，命中结果按距离、match-seeded participant score、participant ID、UnitUid 排序。

**代码证据**

- `Assets/Scripts/Physics/Core/PhysicsSpatialGrid2D.cs:13-150`：空间 hash、去重查询和 UID 稳定排序。
- `Assets/Scripts/Gameplay/Pathfinding/AStarPathService.cs:15-235`：固定点 A*、成本数组、IndexedMinHeap。
- `Assets/Scripts/Gameplay/Pathfinding/TeamFlowFieldService.cs:37-161`：整数 Dijkstra cost field。
- `TeamFlowFieldService.cs:306-420`：多线路/team field 合并和稳定 lane tie。
- `TeamFlowFieldService.cs:750-766, 791-895`：O(1) 方向查询与确定性 integer min-heap。
- `Assets/Scripts/Gameplay/Pathfinding/DeterministicRVOSystem.cs:34-115, 169-205`：邻居稳定排序、候选速度评分与预计算方向。
- `Assets/Scripts/Gameplay/Pathfinding/RvoOrchestrator.cs:22-187`：Unit/locomotion/grid 到 RVO input/output 的组合。
- `Assets/Scripts/FrameSync/ProjectileHitResolver.cs:48-241`：候选筛选、命中检测和稳定排序。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs:217-243, 302-332`：整条路径/物理/投射物链路接入主 Tick。

**真正体现的能力**

- 能在“确定性优先”约束下选择数据结构、代价表示和 tie-break；
- 能区分预计算成本与 per-Tick 查询成本，Flow Field 以构建开销换取 O(1) 运行时方向查询；
- 能把 broad phase、narrow phase、路径、局部避障和投射物命中组织进明确的 Tick 阶段；
- 能发现并阻止无序容器枚举进入权威结果。

**建议简历表达方向**

强调“固定点 + 稳定排序 + 算法接入确定性 Tick”，以及 A* 与 Flow Field 的不同使用场景。若篇幅有限，不必把 A*、Flow Field、RVO、Spatial Hash 全部写成并列关键词，可写成“一套确定性移动/空间查询链路”并在面试展开。

**不能写成什么**

- 不能写 ECS/Job/Burst/NativeContainer；实现是 MonoBehaviour/普通 C# 与托管数组集合。
- 不能写“高性能支持海量单位”；没有实体规模、CPU 或 GC profile 数据。
- 不宜称“完整自研物理引擎”；当前是面向 MOBA 的 2D 确定性查询/碰撞子集。
- 不能写 Unity Physics 或 NavMesh 作为权威实现。

**面试官可能追问**

1. 为什么查询后还要按 UID 排序，HashSet 只用于去重不行吗？
2. A* 的 heap 如何支持 decrease-key，稳定 tie-break 是什么？
3. Flow Field 的构建复杂度、内存成本和 O(1) 查询优势分别是什么？
4. RVO 是精确线性规划还是候选速度采样？为什么这样权衡？
5. moving projectile 的 sweep 如何避免 tunneling？
6. 投射物等距命中为什么不能直接按 UnitUid？
7. 当前 per-Tick 是否零分配、最多支持多少单位？——没有 profile/压测证据，不能给数字。

### A5. 客户端 View / Server Logic 资源拆分与构建审计

**实际实现**

- `GlobalPrefabTable` 对 Unit/Projectile 保存同步逻辑 Prefab 和可选 `ClientViewAddress`；运行时逻辑生成不依赖异步客户端资源。
- `FrameSyncMoba.ClientContent.asmdef` 使用 `!UNITY_SERVER` define constraint，使客户端 View 代码不进入 Dedicated Server 程序集。
- Addressables content service 对 Prefab/Audio/Sprite 维护按地址缓存和引用计数 lease，最后一个 lease 释放时才释放 handle，并显式检查 underflow。
- Unit view binder 按 UID 对照 UnitWorld；若回滚后同一 UID 对应的 Unit 对象实例被替换，会撤销旧异步绑定并重新加载/绑定，防止表现层抓住旧对象。
- Projectile view 以“每 address 一份常驻 lease”避免短生命周期投射物频繁卸载/重载资源。
- Server build scope 禁止 Addressables 随 Player 构建、过滤 StreamingAssets 注入；临时构建场景剥离 Canvas、Renderer、Animator、Audio、Particle、Camera、Light 和表现资产引用。
- 构建后审计 Server 输出不得出现 `StreamingAssets/aa`、catalog 或 bundle；Client 审计检查 settings 的目标平台、bundle 存在和 Windows/Linux 交叉污染。

**代码证据**

- `Assets/Scripts/RuntimeConfig/GlobalPrefabTable.cs:16-42, 218-263`：logic prefab + client address 与范围/重复校验。
- `Assets/Scripts/ClientContent/FrameSyncMoba.ClientContent.asmdef:20-22`：`!UNITY_SERVER`。
- `Assets/Scripts/ClientContent/AddressablesClientContentService.cs:46-114, 200-276`：初始化、缓存、lease 和 Release。
- `Assets/Scripts/ClientContent/ClientUnitViewBinder.cs:46-88, 103-163`：按 UID reconcile、对象身份检查、取消异步绑定。
- `Assets/Scripts/ClientContent/ClientProjectileViewBinder.cs:28-37, 58-198`：per-address resident lease 与投射物 Reconcile。
- `Assets/Scripts/ClientContent/ClientContentRuntimeInstaller.cs:197-317`：Map/Projectile View 预载和 lease 所有权。
- `Assets/Scripts/Bootstrap/Editor/DedicatedServerPresentationBuildPipeline.cs:15-73`：server/client Addressables build scope。
- `DedicatedServerPresentationBuildPipeline.cs:76-183`：临时 Server scene presentation stripping。
- `DedicatedServerPresentationBuildPipeline.cs:241-309`：Server 输出审计。
- `DedicatedServerPresentationBuildPipeline.cs:312-392`：Client platform/bundle 审计。
- `Assets/AddressableAssetsData/AssetGroups/Client-*.asset`：当前 63 个本地 address；`AddressableAssetSettings.asset:20` 为 `m_BuildRemoteCatalog: 0`。

**调用关系**

```text
Formal PrefabId
  -> synchronous logic prefab (client + server Gameplay)
  -> optional ClientViewAddress
  -> AddressablesClientContentService lease
  -> Unit/Projectile binder Reconcile
  -> PresentationHost.Bind(runtime object)

Dedicated Server build
  -> disable/filter Addressables
  -> strip temporary scene presentation
  -> post-build reject catalog/bundle/client aa directory
```

**真正体现的能力**

- 理解 Dedicated Server 的程序集、资产依赖和场景序列化引用是三个不同的裁剪层次；
- 能处理异步资源生命周期、取消、共享句柄和回滚导致的对象身份变化；
- 能把曾出现的跨平台 Addressables 污染转化为自动构建守卫，而不只靠人工检查。

**建议简历表达方向**

写“拆分同步逻辑 Prefab 与 Addressable View，并建立 lease/binder 和 Server/Client 构建审计，阻止客户端表现资源及错误平台 bundle 进入 Dedicated Server/Player 产物”。这是比单写“使用 Addressables”更有招聘价值的表达。

**不能写成什么**

- 不能写“资源热更新/CDN”；remote catalog 关闭，当前是随客户端安装的本地 Addressables。
- 不能写“修正后正式包已完成验收”；当前最终 Windows Client + Linux Server rebuild 仍待执行。
- 不能写“UI 全部按需懒加载”；现有 UI 初始化会获取已注册页面资源。
- 不能用 324.05 MiB 源目录或旧 bundle 大小声称包体优化结果。

**面试官可能追问**

1. 为什么逻辑 Prefab 必须同步加载，而 View 可以异步？
2. `!UNITY_SERVER` 只能排除代码，为什么还需要场景剥离和输出审计？
3. lease cache 如何处理并发 Acquire、取消和 underflow？
4. 回滚后 UID 相同但对象引用不同，View binder 为什么必须重新绑定？
5. 为什么投射物采用 per-address resident lease，而 Unit 是 per-binding lease？
6. Windows 包误带 Linux bundle 会发生什么，构建审计如何阻断？
7. Addressables 当前是否支持远程 catalog/热更新？答案是否定的。

## 5. 隐藏卖点

以下内容比 README 中的框架名更值得在面试展开，但未必都需要占独立 Bullet：

### 5.1 恢复失败是显式契约，不做静默修复

`SimulationTickPipeline.RestoreFromSnapshot` 检查 schema、UID 严格递增和 participant 唯一性；Resolve 阶段对丢失 source reference 抛 `DeterministicSimulationException`。这表明 Snapshot 不是“字段复制器”，而是有拓扑和引用完整性约束的协议。

### 5.2 GameplayParticipantId 与技术 UID 解耦

`OriginActionId`、Crit 和 projectile tie 使用稳定 participant identity，而非可能因 restore/relabel 改变的技术 UID。这个抽象能解释为什么网络/回滚工程需要区分“Gameplay 身份”和“运行时对象身份”。

### 5.3 完整 canonical bytes 是命令权威的一部分

`PredictionRollbackCoordinator` 不只比较命令数量或摘要，而是比较 revision 和 canonical byte array。其价值是把命令字段、顺序和编码都纳入权威边界，降低“语义看似相同但协议状态不同”的隐性分叉。

### 5.4 跨 Unit 全局子阶段避免 UID 顺序偏差

Pipeline 不是对每个 Unit 一次执行完整 Update，而是按系统阶段遍历全部 Unit。这是一项容易被忽略的 deterministic scheduling 设计，可用于解释为何“稳定顺序”仍可能产生系统性先手偏差。

### 5.5 回滚安全的表现层异步绑定

`ClientUnitViewBinder` 同时检查 UID 和对象引用，并取消已失效的异步任务。它把纯逻辑回滚与 Unity GameObject/Addressables 生命周期连接起来，是很贴近实际客户端工程的问题。

### 5.6 从真实构建故障沉淀自动守卫

状态文档记录过 Windows Player 误带 Linux Addressables 导致 Shader 变紫；当前 build audit 会检查 settings target、平台目录和错误平台残留。这比“写了构建脚本”更能体现问题闭环。

### 5.7 现实时间配置只在 Bake 阶段转换为 Tick

Inspector 以整数毫秒 authoring，并按 Ceil/Nearest/Floor 策略 Bake 为整数 Tick；运行时、Command、Snapshot 和 checksum 不保存浮点现实时间。可作为确定性内容管线的补充卖点。

## 6. 不建议写的内容

- 单独写“使用 Fixed-Point / NGO / Addressables / ScriptableObject / UniTask / Odin”。技术名词本身不能说明问题和方案。
- 用一个 Bullet 罗列 Unit、Combat、Attack、Ability、Buff、CC、Projectile、Equipment、Pathfinding、AI、Presentation。它证明范围大，却不能证明任何一项做得深。
- 把 Design Index、Decision Log、ExecPlan 和多 Agent 流程放在最主要的 Gameplay Bullet 前面。它们能证明治理能力，但 Unity 客户端面试首先关心代码系统、数据流和调试能力。
- 写“自研 AI Agent 工作流”却无法回答具体一次冲突如何被发现、哪条验收阻止了错误合并、本人如何判断 Agent 输出。若保留，只放最后一条或项目说明中。
- 把代码行数、测试属性数量、Addressables 地址数量、Formal asset 数量作为成果。它们是仓库规模，不是用户价值或性能结果。
- 写“两个英雄/少量装备”作为量化核心。内容数量不足以形成招聘优势，且会稀释架构深度。

## 7. 可能存在夸大的内容与技术名词分层

| 名词 | 当前项目中的真实层级 | 可以表达 | 不应表达 |
|---|---|---|---|
| 帧同步 | **核心实现** | Command 驱动、AuthorityFrame、checksum、预测和连续确认 | 商业级成熟帧同步、已承载大规模线上玩家 |
| Snapshot / Rollback / Replay | **核心实现，当前版本实机复验不足** | 聚合快照、三阶段恢复、本地纠错重演 | 已完整覆盖断线重连/任意历史回滚/线上稳定性 |
| Dedicated Server | **应用流和构建链路已实现，修正包待验收** | Server Bootstrap、资源裁剪、构建审计 | 当前最终 Windows/Linux 包已全链路验收 |
| NGO / UTP | **基础传输 + 自定义消息接入** | named messages、wire codec、可靠/不可靠消息选择 | 自研底层传输、以 NetworkVariable/RPC 完成全部同步 |
| UOS | **历史包实测，当前匹配版本待复验** | 对接 Matchmaking/Multiverse 的应用流 | 当前源码版本已完成生产公网验收 |
| ECS / Burst / Job | **本项目不存在** | 不写；若需要只放另一个 ECS 项目 | 把自研空间网格或批处理误称 DOTS |
| GPU / GPU Skinning | **本项目不存在** | 不写 | 渲染优化、大规模 GPU 动画 |
| 大规模实体 | **无压测证据** | 可谈 Flow Field/RVO 的算法目的 | 支持 N 个实体、稳定 N FPS |
| 数据驱动 | **核心辅助能力** | SO Bake → runtime definition、CastModel/Stage、PrefabId | “零代码配置任意英雄” |
| Lua | **UI/表现辅助** | Lua page/bridge 接入 | Lua 热更新权威 Gameplay |
| 性能优化 | **结构性优化，无测量结果** | 空间 broad phase、数组/缓冲复用、对象池、预计算 | 零 GC、提升百分比、包体下降百分比 |

### 当前文档与源码冲突

- README `:123,415` 仍写 Snapshot schema 23；源码 `GameplaySnapshot.cs:62` 为 24。
- `DESIGN_INDEX.md:24` 的 ActionRuntime schema 23 描述也已落后于当前 D-050 代码。
- `CURRENT_HANDOFF.md` 同时出现 Unit suite `545 passed / 10 retained failures` 和 `542 passed / 10 retained failures`；不能选择对自己有利的数字。
- README 和现有简历的“完成 UOS 实测验收”需要限定为**历史包**；当前 schema-24 / GameplayDataVersion-4、Addressables 拆分后的 matching rebuild 尚未完成实机验收。

## 8. 可量化数据

### 8.1 可以直接使用的仓库事实

这些数字可从当前配置/资产直接复现，但“可使用”不等于“值得写进简历”。

| 数据 | 当前事实 | 简历使用建议 |
|---|---:|---|
| Unity 版本 | 2022.3.62f1c1 | 可放技术栈，不必占 Bullet。 |
| 固定 Tick | 50 Tick/s | 可用于说明当前配置；不要推导性能或网络能力。 |
| Command 接受窗口 | lead 1 Tick，future 12 Tick | 适合面试解释协议，不建议孤立写成成果。 |
| Snapshot/预测配置 | window 180 Tick，最大预测领先 6 Tick，每 Unity 帧最多执行 4 logic ticks | 可作为系统参数；不是性能结果。 |
| MaxPlayers | 10 | 仅配置上限；不能写“完成 10 人/5v5 压测”。 |
| Snapshot schema | 24 | 协议事实，不是招聘量化成果。 |
| 本地 Addressables roots | 63（1 Audio、8 Projectile、4 Shared、35 UI、8 Unit、7 VFX） | 可证明拆分范围；通常不值得写主 Bullet。 |
| Logic/View Prefab | Unit 8/8、Projectile 8/8，另有 Logic Map / Map View | 可证明不是空接口；不要包装成内容规模优势。 |
| Formal 资产 | 74 个 `.asset`、17 个 logic Prefab | 仓库规模，不建议写。 |
| Build Settings | 6 个启用场景 | 不建议写。 |
| 源码规模 | 77,023 首方运行/支持代码非空行；165 个测试文件、40,695 测试非空行；静态 `[Test*]` 1,121、`[UnityTest]` 34 | 口径可复现，但不能代表个人有效产出或测试通过，不建议写。 |

### 8.2 需要进一步验证后才能使用

- 最新全量 EditMode/PlayMode 实际通过数：先重跑并消除 545/542 记录冲突及 retained failure 状态。
- schema-24 / GameplayDataVersion-4 的 Local C/S、双客户端和 UOS live acceptance。
- 修正后 Windows Client + Linux Dedicated Server 的最终产物、平台 Addressables 审计和 BuildReport。
- 最大同时 Unit/Projectile 数、持续运行时间、rollback 次数/频率。
- 每 Tick CPU、Unity 主线程耗时、GC Alloc、内存峰值、网络 bytes/s、Snapshot 内存、Draw Call。
- 当前最终 Player/Server 包体大小。

### 8.3 不要作为简历成果使用

- `MaxPlayers=10` → “支持 10 人稳定对战”。
- `SnapshotWindow=180` → “可回滚 3.6 秒且无性能问题”。虽然在 50 TPS 下数学上对应 3.6 秒配置窗口，但仓库没有证明任意边界均能低成本恢复。
- 324.05 MiB ClientContent 源目录 → “客户端包体 324 MiB”或“包体优化 X%”。
- 历史记录的 7 bundles + 1 catalog / 612,459,164 B → 当前最终构建结果。
- 测试属性数量 → “1,000+ 测试全部通过”。
- 任何 FPS、百分比提升、延迟下降、网络节省、GC 降低或实体数量——当前项目没有可用测量。

## 9. 推荐项目定位

### 项目一句话定位

**Unity 2022.3 确定性帧同步 MOBA 技术垂直切片，围绕固定点 Gameplay、服务端权威、客户端预测回滚及客户端/Server 资源边界进行架构设计与验证。**

这句话刻意使用“技术垂直切片”，避免暗示完整商业 MOBA；也保留现有简历中真实的“设计与验收”角色。

### 推荐技术栈

主栈只保留：

`Unity 2022.3 LTS / C# / Fixed-Point / NGO + UTP / Snapshot-Rollback / Addressables / Dedicated Server`

可作为次级补充：

`UOS / ScriptableObject Bake / Input System`

建议从本项目技术栈主行移除 `UniTask`、`Odin Inspector`：它们确有使用，但不能解释该项目的核心难度。不要加入 ECS、Burst、Job、Unity Physics 或 GPU 相关词。

## 10. 推荐简历 Bullet 草稿

以下是技术语义草稿，不是最终 HR 润色。动词按现有个人贡献披露进行约束。

### Bullet 1：帧同步与回滚闭环

**草稿：** 主导设计并验收固定 Tick/定点数帧同步链路，以 canonical Command、AuthorityFrame 与 Gameplay checksum 建立服务端权威；客户端保存逐 Tick Snapshot/输入历史，在命令 bytes 或状态校验不一致时按合法 anchor 执行 Restore/Resolve/Rebuild 并重演预测区间。

- **优先级：A1，必写**
- **代码依据：** `CommandCollector`、`AuthorityFrameReplicator`、`PredictionRollbackCoordinator.CorrectAndReplay`、`SimulationTickPipeline.RestoreFromSnapshot`。
- **为什么值得写：** 一条同时体现网络、状态管理、确定性和调试边界，是项目最强差异化。
- **面试风险：中。** 必须能解释 Tick/anchor 语义、完整 command bytes、schema membership 和重演失败处理。

### Bullet 2：同 Tick 战斗公平性

**草稿：** 为多来源同 Tick 战斗设计 sealed-wave 批量结算：按目标统一评估伤害，使用固定点比例分配护盾/生命并以稳定余数键守恒；引入与技术 UID 解耦的 OriginActionId，使暴击、等距投射物选择和致死归属不受插入顺序或对象 relabel 影响。

- **优先级：A2，强烈推荐**
- **代码依据：** `CombatSystem.ProcessDamageBatch/AllocateDamageAmount/ResolveLethalBatchKiller`、`CombatActionIdentity`、`ProjectileEffectDispatcher`。
- **为什么值得写：** 比常规“战斗系统”更能证明确定性与规则工程能力。
- **面试风险：中高。** 需要能现场说明比例分配、余数和 action-keyed random 的选择。

### Bullet 3：动作仲裁与技能状态机

**草稿：** 设计 Intent → Planner → Arbiter → Handler 的 Unit 行为链路，以固定 Main/Base ActionRuntime 和资源位统一移动、普攻、施法、位移及控制打断；将 Hold/Release、Channel、Toggle、目标选择与多段重施法 CastModel 通过 ScriptableObject Bake 接入可 Snapshot 的运行时状态机。

- **优先级：A3，推荐**
- **代码依据：** `BehaviorPlanner.Tick`、`ActionArbiter.Submit`、`ActionRuntimeSet`、`AbilityAsset.Bake`、`AbilityHandler.HandleSignal/TickUpdate`。
- **为什么值得写：** 直接对应 Unity Gameplay/客户端岗位常问的复杂状态机、数据驱动和可扩展性。
- **面试风险：中高。** 不能只背模块名，需要画出资源冲突和状态迁移。

### Bullet 4：确定性移动与命中链路

**草稿：** 将自研固定点 2D spatial hash、A*、团队 Flow Field、候选采样 RVO、墙体修正与投射物 sweep 命中纳入同一 Tick；对无序容器结果、heap 等价成本及等距命中定义显式稳定排序，避免平台/插入顺序进入 Gameplay 结果。

- **优先级：A4，推荐；篇幅紧时可与 Bullet 3 二选一**
- **代码依据：** `PhysicsSpatialGrid2D`、`AStarPathService`、`TeamFlowFieldService`、`DeterministicRVOSystem`、`ProjectileHitResolver`、`SimulationTickPipeline`。
- **为什么值得写：** 同时证明算法落地与确定性约束，不是孤立算法练习。
- **面试风险：高。** 没有性能数据，且会被追问复杂度、RVO 近似、sweep 和内存分配。

### Bullet 5：客户端/Server 资源与构建边界

**草稿：** 拆分同步 Logic Prefab 与异步 Addressable View，建立引用计数 lease 和 rollback-aware UID binder；通过 `!UNITY_SERVER`、临时场景表现剥离及 Client/Server 构建后审计，阻止客户端资源、catalog/bundle 与错误平台 Addressables 混入 Dedicated Server 或 Player 产物。

- **优先级：A5，推荐**
- **代码依据：** `GlobalPrefabTable`、`AddressablesClientContentService`、`ClientUnitViewBinder`、`DedicatedServerPresentationBuildPipeline`、ClientContent asmdef。
- **为什么值得写：** 很贴近实际 Unity 客户端资源、异步生命周期和打包故障治理。
- **面试风险：中。** 必须说明最终 corrected rebuild 尚未完成验收，不能宣称已有最终包体收益。

### Bullet 6：网络/UOS 应用流（可选替换项）

**草稿：** 基于 NGO/UTP named messages 接入 CommandBundle、AcceptedRelay、AuthorityFrame 与缺帧 Recovery 协议，并将本地 C/S 与 UOS Matchmaking/Multiverse 复用到同一 Gameplay/Bootstrap 流程；历史包完成双客户端公网联调，当前 schema-24 匹配重建仍待复验。

- **优先级：B，仅在网络岗位或完成新版实测后提升**
- **代码依据：** `FrameSyncNetworkBridge`、`FrameSyncWireCodec`、`LobbyNetworkBridge`、`UosNgoApplicationAdapters`。
- **为什么值得写：** 说明同步方案不是纯本地模拟。
- **面试风险：高。** 当前版本验收不闭环；简历正文通常不适合写“仍待复验”，因此更合理的做法是完成验收后再使用强表述。

### 篇幅取舍建议

若项目只能保留 4 条：保留 1、2、3、5。  
若目标岗位偏 Gameplay/战斗：用 4 替换 5。  
若目标岗位偏网络：在新版实机复验完成后，用 6 替换 3 或 4。  
“AI Agent 工程工作流”不进入前四条；只有在岗位明确关注 AI 辅助研发/技术管理时才作为第 5～6 条。

## 11. 面试追问清单

除 A 类各节问题外，建议优先准备以下跨系统问题：

1. 用白板画出一条本地输入从 Input System 到服务端 AuthorityFrame，再到客户端确认/回滚的完整数据流。
2. 解释“客户端下一预测 Tick”“服务端下一执行 Tick”“最新连续权威 Tick”三个进度值为何不能混为一个。
3. 列出 schema 24 Snapshot 的主要成员，以及至少三类不应直接序列化的派生状态。
4. 举例说明一次 Restore 为什么必须 Resolve reference、Rebuild cache；如果引用无效为什么选择 fail-fast。
5. 给出一个“稳定排序但仍不公平”的同 Tick Combat 例子，并手算比例分配与余数。
6. 解释 GameplayParticipantId、UnitUid、OriginActionId 各自生命周期和使用场景。
7. 比较全局 PRNG Snapshot 与 action-keyed hash random 的优缺点。
8. 说明 Main/Base ActionRuntime 如何处理移动施法、Dash、攻击前摇和控制打断。
9. 比较 A* 和 Flow Field：构建频率、查询成本、适用实体和地图变化限制。
10. 解释 RVO 当前为什么是候选速度采样而不是精确解，可能出现哪些拥挤/死锁问题。
11. Addressables lease 在取消、重复 Acquire、回滚对象替换时如何避免泄漏或绑定到旧对象？
12. 为什么 `!UNITY_SERVER` 不能单独保证 Server 包不带表现资源？
13. 当前项目有哪些真实性能优化，哪些只是数据结构意图？为什么没有数据时不能写“高性能”？
14. 当前最重要的未完成验收是什么？回答应包含 schema-24 matching endpoint、修正后 Windows/Linux Addressables build 和 retained tests。
15. 主要实现由 Codex 完成时，本人具体做了哪些架构决定、如何审查错误、如何证明自己能够维护和重写关键模块？这是现有简历最可能遇到的归属追问。

## 12. 对现有简历的修改建议

本节只比较 PDF 第 2 页的 `FrameSyncMobaDemo` 板块；不评价或修改 `ECS Shooting Demo`、`GAZA`、实习经历及其他技能声明。

### 12.1 现有项目定位

现有：`FrameSyncMobaDemo 确定性帧同步 MOBA 技术项目`

**判断：基本准确，但略容易让人误解为内容完成度较高。** 建议后续改成“确定性帧同步 MOBA 技术垂直切片”或“确定性帧同步 MOBA 架构验证项目”。

### 12.2 现有角色说明

现有：`主导架构设计、设计审查、计划审批与最终验收；主要代码实现由 Codex 完成`

**判断：真实且必须保留贡献边界，但“计划审批”招聘价值偏低，“主要代码由 Codex 完成”会立即触发对独立编码能力的追问。**

建议后续方向：把重点从行政式“审批”转为可核验的技术职责，例如“关键契约定义、源码审查、确定性测试设计、多进程验收与故障归因”；仍需明确 AI Agent 协同实现，不能删除事实披露后改成“独立开发”。仓库无法确认个人实际参与细节，最终措辞必须由本人按事实校正。

### 12.3 现有技术栈

现有：`Unity / C# / NGO / UTP / UOS / Dedicated Server / Fixed-Point / UniTask / Odin Inspector`

**判断：没有明显虚假，但优先级失衡。** `UniTask`、`Odin Inspector` 是辅助工具；`Snapshot/Rollback`、`Addressables` 反而缺失。建议采用第 9 节的精简技术栈。

### 12.4 现有 Bullet 逐条对比

#### 现有 Bullet 1：确定性帧同步架构

> 统一固定 Tick、定点数模拟、Command 驱动、AuthorityFrame 校验、Snapshot / Rollback / Replay 与 Gameplay Checksum，并将 Gameplay 核心与 NGO / UTP 网络边界隔离。

**结论：保留并增强，源码支持。**

- 优点：覆盖项目最强主线，`SimulationTickPipeline`、`PredictionRollbackCoordinator`、`AuthorityReplication` 和 asmdef 依赖边界均有证据。
- 描述过弱处：没有说明何时触发 rollback、恢复到哪里、为何比较完整 command bytes。
- 修改方向：吸收推荐 Bullet 1 的“bytes/checksum mismatch → anchor restore → replay”因果链。
- 风险：不能暗示当前版本已经通过完整公网实测。

#### 现有 Bullet 2：正式设计体系和系统清单

> 建立 Unit、Combat、Attack、Ability、Buff、Crowd Control、Projectile、Equipment、Pathfinding、AI、Presentation 等正式设计体系……

**结论：应替换。**

- 已实现部分：这些模块大多确有代码，并非纯文档。
- 问题：招聘者读到的是 11 个名词和文档治理，没有看到最难的问题、关键算法或结果。
- 遗漏：同 Tick batch settlement、OriginActionId、Main/Base ActionRuntime、稳定排序和恢复一致性检查才是源码中的强卖点。
- 修改方向：用推荐 Bullet 2 或 3 取代；Design Index/Decision Log 可在面试或项目链接中说明。

#### 现有 Bullet 3：Dedicated Server / UOS 已落地并完成实测验收

> 推动 Dedicated Server 与 UOS Matchmaking / Multiverse 网络链路落地并完成实测验收，覆盖本地 C/S、双客户端与公网 UOS 场景……

**结论：当前表述过强且已过时，必须收紧。**

- 可确认：UOS/NGO/Dedicated Server 应用流代码真实；README 有历史公网双客户端视频和历史 owner-accepted 记录。
- 冲突：`CURRENT_HANDOFF.md:201-229`、`MODULE_STATUS.md:41,46`、README `:12,521-523` 都说明当前 schema-24 / GameplayDataVersion-4 与 Addressables 拆分后的 matching rebuild/live acceptance 尚未完成。
- 修改方向：在复验前，只能写“历史包完成过双客户端/UOS 联调，当前协议版本的网络与构建链路已实现”；更推荐先用 A5 的资源/构建审计 Bullet 替换。
- 应删除的词：无版本限定的“完成实测验收”、暗示当前源码包已闭环的“形成完整分层验证流程”。

#### 现有 Bullet 4：AI Agent 工程工作流

> 需求/初案 → 多 Agent 审查 → 正式设计案 → ExecPlan → Codex 实现 → Unity MCP/自动测试 → 人工 C/S/UOS 验收……

**结论：真实差异化，但对 Unity 客户端校招岗位优先级低于源码技术。**

- 保留条件：能举出至少两个 Agent 方案被本人否决/修正的具体技术案例，并能解释验收如何发现问题。
- 风险：它与角色行共同强调“代码由 Agent 完成”，若缺少具体源码能力证据，会让面试官怀疑候选人只能写流程/文档。
- 修改方向：压缩为一条工程治理补充，放在 3～5 条强技术 Bullet 之后；篇幅不足时删除。

### 12.5 当前简历遗漏的强点

- 同 Tick Combat batch settlement、比例分配和稳定 action identity；
- Intent/Planner/Arbiter/Main-Base Runtime 与技能 Stage 的可恢复状态机；
- 自研固定点 spatial hash、Flow Field/RVO 和 projectile sweep 的确定性 tie-break；
- rollback 后相同 UID / 新对象实例的 Addressables View 重绑定；
- Server scene stripping 与跨平台 Addressables 输出审计。

### 12.6 当前简历不应新增的词

本项目板块不要新增：`ECS`、`DOTS`、`Burst`、`Job System`、`Unity Physics`、`GPU Skinning`、`大规模实体`、`零 GC`、`热更新`、`完整 5v5`、`生产级线上帧同步`。

这些词中部分可属于简历的其他项目，但不能借其他项目经验强化本项目的技术声明。

### 12.7 修改优先级

1. **立即修正** UOS/当前包“已完成实测验收”的版本范围。
2. 用 Combat fairness 或 ActionRuntime/Ability 替换系统名词清单。
3. 在技术栈中加入 Addressables 或 Snapshot/Rollback，移除低价值的 UniTask/Odin。
4. 将 AI Agent 工作流降到最后或删除，先保证 3～4 条源码技术足够具体。
5. 新版 Local C/S + UOS + Windows/Linux build 验收完成后，再恢复更强的网络 Bullet，并记录可引用的版本、日期、拓扑和日志结果。

## 结论

这个项目最值得写的不是“用了很多 Unity 框架”，而是三条清晰能力主线：

1. **如何让复杂 MOBA Gameplay 在固定点、固定 Tick 和稳定顺序约束下可重演；**
2. **如何让 Command、Snapshot、checksum、战斗公平身份和动作状态机共同构成可恢复的权威状态；**
3. **如何把纯逻辑模拟接到 NGO/UOS、Addressables 表现和 Dedicated Server 构建，同时保持边界可验证。**

最终简历应优先讲清 3～5 个这样的“问题 → 方案 → 本人职责 → 工程价值”，而不是继续扩大系统清单。任何性能、规模、当前公网验收或“独立实现”结论，在获得新证据或与真实个人贡献一致之前都不应补写。
