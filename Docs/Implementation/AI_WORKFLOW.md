# FrameSyncMobaDemo — 低开销 ExecPlan 执行循环协议

> Version: 2  
> Effective from: 2026-07-22  
> Source: `Unity_MOBA_Low_Overhead_ExecPlan_Workflow_v2.md`

本协议在持续开发中有效。首次接管时读取一次；后续用户只需批准候选编号。

## 1. 核心循环

```text
用户批准候选 A / B / C
    -> 仅为被选候选生成一份正式 ExecPlan
    -> 立即执行该 ExecPlan
    -> 实施过程中同步完成测试
    -> 定向收口检查
    -> 更新受影响的状态文档
    -> 生成下一轮三个精简候选摘要
    -> 停止并等待再次批准
```

不得为三个候选提前分别编写完整 ExecPlan。三个候选统一写入：

```text
Docs/Implementation/NEXT_CANDIDATES.md
```

用户选中后才生成：

```text
Docs/Implementation/Plans/<NNNN>_<selected_name>_execplan.md
```

## 2. 信息来源

当前事实按以下证据确认：

1. 当前代码和 Unity 资产；
2. Git 状态与 diff；
3. Unity 编译、Console 和测试结果；
4. `CURRENT_HANDOFF.md`；
5. `MODULE_STATUS.md`；
6. `REPOSITORY_MAP.md`；
7. 已完成 ExecPlan。

目标规范按以下优先级确认：

1. 用户当前批准的候选范围；
2. `Docs/Architecture/DECISION_LOG.md`；
3. `Docs/Architecture/DESIGN_INDEX.md`；
4. `DESIGN_INDEX.md` 中列为 Current 的 `Docs/Design/` 正式设计案；
5. 当前正式 ExecPlan；
6. 现有实现。

代码证明当前状态，但不能覆盖正式设计。ExecPlan 规定本轮范围，但不能修改设计契约。不得使用归档、Superseded 或未列为 Current 的设计案。

## 3. 项目边界

当前目标是通用确定性框架、通用 Runtime、数据驱动创作管线和自动测试能力。除非用户明确批准，不实现具体英雄、正式技能、正式 Buff、正式装备效果、正式平衡数据、最终动画/VFX/音频/UI/地图内容或内容专属核心框架分支。设计中的具体玩法仅作为通用行为示例和测试场景。

## 4. 候选规模规则

代码修改量统计为预计新增行加预计删除行，仅统计生产 C#、测试 C# 和必要 asmdef JSON；不统计 Markdown、`.meta`、自动生成文件、Package lock 和纯格式化。

- 每个候选必须为 200～3000 行。
- 常规目标约 500 行，建议 350～800 行。
- 同轮三个候选的平均值应尽量接近 500 行。
- 少于 200 行时，与同一职责和测试闭环的相邻工作合并。
- 超过 1000 行必须说明仍属于单一内聚切片的原因。
- 超过 3000 行必须拆分。
- 实施中预计超过 3000 行时，在最近完整可编译检查点收口并将剩余工作转入下一轮。

不得通过重复代码、无意义包装、超量注释、占位类型、机械格式化或拼接无关任务满足行数。完成后报告实际代码修改量并与预估比较。

## 5. 首次接管模式

首次加载本协议或用户发送“接管项目”时，只读取：

```text
AGENTS.md
当前正式 PLANS.md
Docs/Implementation/AI_WORKFLOW.md
Docs/Implementation/CURRENT_HANDOFF.md
Docs/Implementation/NEXT_CANDIDATES.md
Docs/Architecture/DESIGN_INDEX.md
Docs/Architecture/DECISION_LOG.md
Docs/Implementation/MODULE_STATUS.md
```

随后只针对三个候选读取其正式设计章节、现有代码和测试。不得默认重读全部设计、全部历史 ExecPlan、运行全部测试、重新扫描全仓库或重写 `REPOSITORY_MAP.md`。只有 `CURRENT_HANDOFF.md` 缺失、明显过期或与代码冲突时才扩大检查。

接管输出仅含当前状态摘要、三个候选简短比较、推荐顺序和 Go/Conditional Go/No-Go，然后等待批准。

## 6. 用户命令

正常命令为 `执行候选：A`、`执行候选：B` 或 `执行候选：C`。也允许“重新比较候选”和“暂停，不生成下一轮候选”。

## 7. 执行候选模式

### 7.1 生成正式 ExecPlan

从 `NEXT_CANDIDATES.md` 读取选中候选，只为它生成一份精炼的正式 ExecPlan，包含：

1. Purpose 和可观察行为；
2. 正式设计文件与章节；
3. 当前真实代码路径；
4. In scope / Out of scope；
5. 准确生产类型和程序集；
6. 公共契约及所有权；
7. Snapshot、序列化、Checksum、生命周期影响；
8. 实施步骤；
9. 自动测试；
10. Unity MCP 验证；
11. 风险、停止条件和完成标准；
12. 预计代码修改量。

不得复制 AGENTS.md、本协议全文、完整项目背景、无关设计摘要或历史审计。

### 7.2 最小执行前检查

只检查当前 Git 状态、计划涉及文件是否变化、相关程序集能否编译以及正式契约是否仍一致。已有近期可靠且相关代码未变化的基线可以复用；不默认运行全量测试。

### 7.3 实施约束

严格执行正式 ExecPlan，不得顺手实施下一阶段、扩大到多个独立系统、创建重复 UID/Command/Snapshot/Aim/AbilitySignal/Checksum/FixedPoint/DTO、修改正式设计迁就实现、增加未经批准 Package、覆盖无关修改或使用占位成功/吞异常/TODO/弱化测试。普通实现细节采用最小、清晰、可测试方案，并记录到 Decision log。

### 7.4 测试同步

验证采用按风险设门，不按每次修改机械运行：

- 日常实现检查点默认只触发 Unity 编译并读取 Console；编译通过时不运行测试。
- 新增或修改公共协议、Snapshot、Canonical Serialization、Checksum 时，运行最小相关 EditMode/跨模块测试。
- 涉及 Unity 生命周期、GameObject、场景、资产、Input、UI 或 Presentation 时，运行最小相关 PlayMode 测试。
- 仅在正式 ExecPlan 最终收口时运行一次受影响测试集合；已有近期可信结果且相关代码未变化时允许复用。
- 不因文档更新、注释、纯命名整理或无行为变化的重排运行测试。

不默认运行全项目测试，也不在连续实现检查点重复运行同一测试。任何跳过都不降低完成标准：命中上述风险门或出现可疑 Console/行为证据时必须运行对应测试，且禁止通过删除、禁用或弱化测试获得通过结果。

### 7.5 定向收口

只检查本轮 Purpose、正式设计章节、Scope、重复协议、程序集依赖、稳定顺序、Snapshot/序列化/Checksum、测试有效性及实际代码修改量。范围内 P0/P1 立即修复并重跑；范围外问题写入 `CURRENT_HANDOFF.md`；P2 不顺手清理。通过后将 ExecPlan 标记为 `Completed and verified`。

## 8. 文档最小化

每轮必须更新当前正式 ExecPlan、`Docs/Implementation/CURRENT_HANDOFF.md` 和 `MODULE_STATUS.md` 受影响行。

只有程序集、组合根、协议所有权、仓库结构或关键资产路径变化时更新 `REPOSITORY_MAP.md`；只有新冻结架构决策时更新 `DECISION_LOG.md`。

`CURRENT_HANDOFF.md` 建议不超过约 150 行，只记录当前分支/Git 状态、最近完成计划、编译与相关测试、已完成能力、真实 P0/P1/P2、冻结契约、下一轮候选和接管限制。

## 9. 下一轮候选摘要

执行收口后覆盖更新 `Docs/Implementation/NEXT_CANDIDATES.md`，不得提前创建三份完整 ExecPlan。每个候选只写：

```text
Candidate ID
名称
Purpose / 可观察行为
正式设计文件与章节
前置依赖
主要生产类型和程序集
公共契约影响
Snapshot / 序列化 / Checksum 影响
主要测试
预计生产代码行
预计测试代码行
预计总代码修改量
主要风险
下游解锁价值
```

每个摘要建议 150～300 字。三个候选必须真实不同、有实施价值、满足 200～3000 行、常规目标约 500 行、可独立编译测试、不依赖未冻结协议且不实现正式内容。最后附简短比较表，然后停止等待批准。

## 10. 允许停止的阻断

只有 Current 设计案存在无法消解的公共契约冲突、必须修改批准范围外核心协议、必须新增未经批准 Package、存在范围内无法解决的外部编译阻断或正式设计彼此矛盾时，才停止并请求决策。普通实现细节、私有命名与局部文件组织不得反复请求确认。

## 11. 每轮最终输出

最终输出简短汇报正式 ExecPlan、可观察行为、实际代码修改量、主要文件与公共契约、编译、EditMode/PlayMode、设计一致性和范围外问题；随后列 A/B/C 简短比较、推荐顺序、`NEXT_CANDIDATES.md` 路径，并确认均未执行、等待批准。

## 12. 当前动作

首次使用进入接管模式。后续收到 `执行候选：<A|B|C>` 后，自动执行完整单轮闭环。
