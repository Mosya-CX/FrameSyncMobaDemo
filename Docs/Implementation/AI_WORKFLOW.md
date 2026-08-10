# ExecPlan 执行循环协议

本协议在当前聊天持续有效。首次接管时读取一次；后续用户只需批准候选编号，不必重复发送规则。

---

# 1. 核心循环

每轮只执行以下流程：

```text
用户批准候选 A / B / C
    ↓
仅为被选候选生成一份正式 ExecPlan
    ↓
立即执行该 ExecPlan
    ↓
执行过程中同步完成测试，不另开独立验收任务
    ↓
做一次定向收口检查
    ↓
更新受影响的状态文档
    ↓
生成下一轮三个精简候选摘要
    ↓
停止，等待用户再次批准
```

不得为三个候选提前分别编写完整 ExecPlan。

三个候选统一写入：

```text
Docs/Implementation/NEXT_CANDIDATES.md
```

用户选中后，才生成：

```text
Docs/Implementation/Plans/<NNNN>_<selected_name>_execplan.md
```

这份正式 ExecPlan 才允许进入执行。

---

# 2. 信息来源

## 当前事实

以下证据说明仓库现在是什么状态：

1. 当前代码和 Unity 资产；
2. Git 状态与 diff；
3. Unity 编译、Console 和测试结果；
4. `CURRENT_HANDOFF.md`；
5. `MODULE_STATUS.md`；
6. `REPOSITORY_MAP.md`；
7. 已完成 ExecPlan。

## 目标规范

以下文件说明系统应该实现成什么样：

1. 用户当前批准的候选范围；
2. `Docs/Architecture/DECISION_LOG.md`；
3. `Docs/Architecture/DESIGN_INDEX.md`；
4. `DESIGN_INDEX.md` 中列为 Current 的 `Docs/Design/` 正式设计案；
5. 当前正式 ExecPlan；
6. 现有实现。

代码证明当前状态，但不能覆盖正式设计。  
ExecPlan 规定本轮范围，但不能修改设计契约。  
不得使用归档、Superseded 或未列为 Current 的设计案。


---

# 3. 严格设计一致性与偏离审批

## 3.1 默认规则

所有生产代码、公共契约、生命周期、数据所有权、稳定顺序、Snapshot、序列化、Checksum、Command、UID、AbilitySignal、程序集依赖和测试行为，必须严格遵照当前正式设计案。

模型不得因为以下理由自行偏离设计：

```text
认为另一种设计更优雅
认为设计过于复杂
为了减少代码量
为了复用现有实现
为了更容易测试
为了提高性能
为了提前兼容未来需求
为了采用常见行业模式
为了消除模型认为“不必要”的层
```

正式设计已经规定的内容，不属于模型可自由发挥的实现细节。

## 3.2 可以自行决定的内容

在不改变正式契约和可观察行为的前提下，模型可以自行决定：

```text
私有类型和私有方法名称
私有文件拆分
局部辅助函数
不影响协议的缓存实现
不影响稳定顺序的性能优化
测试内部组织
普通错误信息文本
局部代码风格
```

这些决定仍需符合 `AGENTS.md` 和现有代码规范。

## 3.3 必须获得用户批准的设计偏离

出现以下任一情况，必须停止受影响的实现并申请批准：

```text
新增设计案未定义的公共抽象层
删除或合并设计案要求的层
改变 Runtime 或数据所有权
改变公共 API、字段、枚举或信号语义
改变 Tick、UID、CommandSeq 或 TargetTick 语义
改变 Snapshot 成员或 Restore / Resolve / Rebuild 边界
改变 Canonical Serialization 或 Checksum 输入
改变稳定执行顺序
改变生命周期、Dying、Dead、Respawn 或清理规则
改变玩家输入、AI、Gameplay 或 Presentation 边界
改变程序集依赖方向
用另一套协议替代正式协议
为“兼容现有代码”保留与正式设计冲突的行为
设计案没有定义，而实现必须作出会影响其它模块的架构选择
```

不得先实现后补报，也不得通过修改设计案使偏离看起来合法。

## 3.4 设计偏离申请格式

偏离申请必须简短且具体：

```text
设计偏离申请

涉及计划：
正式设计：
相关章节：
设计原要求：
拟议偏离：
偏离原因：
影响的公共契约和模块：
Snapshot / 序列化 / Checksum 影响：
兼容与迁移影响：
测试影响：
不偏离时的可行方案：
推荐：
```

提交后停止受影响部分，等待用户明确批准。

## 3.5 批准命令

普通候选不存在设计偏离时，用户发送：

```text
执行候选：A
```

即可执行。

候选存在待批准设计偏离时，模型必须先停止并展示偏离申请。用户只有发送：

```text
批准偏离并执行：A
```

或者明确批准列出的偏离内容后，模型才能继续。

仅批准“执行候选：A”不等于批准未明确披露的设计偏离。

## 3.6 ExecPlan 和候选中的强制字段

每个正式 ExecPlan 和候选摘要都必须包含：

```text
Design conformance:
    Strict — no deviation

或

Design deviation:
    Approval required
    <偏离摘要>
```

存在 `Approval required` 时：

```text
候选状态必须是 Conditional Go
不得自动执行
不得在生成计划时预先实现偏离
```

## 3.7 设计缺口和歧义

设计没有规定私有实现细节时，采用最小方案继续。

设计没有规定且会影响公共契约或其它模块时，不得猜测，必须提交设计偏离申请或设计决策申请。

两个 Current 设计案冲突时：

```text
记录文件和章节
停止受影响部分
继续不受影响部分
等待用户决定
```

模型不得自行选择自己偏好的设计。

---

# 4. UnityMCP 优先原则

## 4.1 默认操作通道

凡是 Unity 相关操作，只要 UnityMCP 支持，就必须优先使用 UnityMCP。

包括但不限于：

```text
检查 Unity 版本和 Packages
检查 Project Settings
检查 asmdef
触发脚本编译
读取 Console
运行 EditMode 测试
运行 PlayMode 测试
检查当前 Scene
检查 GameObject 和 Component
检查 Prefab
检查 ScriptableObject
检查 Input Actions
检查 EventSystem 和 InputSystemUIInputModule
创建或修改 Unity 资产
验证序列化引用
刷新 AssetDatabase
```

不得仅因为直接编辑文本更快，就绕过 UnityMCP 操作 Unity 资产。

## 4.2 禁止优先手改 Unity 序列化文件

以下文件默认不得优先直接编辑：

```text
.unity
.prefab
.asset
.controller
.anim
.inputactions
ProjectSettings 下由 Unity 管理的序列化文件
```

正确顺序：

```text
1. 先尝试 UnityMCP。
2. UnityMCP 不支持或明确失败时，记录失败原因。
3. 再选择最安全的替代方式。
4. 替代操作后必须通过 UnityMCP 重新加载、检查引用并验证结果。
```

不得手工修改 YAML 后跳过 Unity 验证。

## 4.3 C#、Markdown 和普通文本

以下内容可以直接通过仓库文件工具编辑：

```text
C# 源文件
Markdown
JSON 配置
普通文本
非 Unity 管理的工具脚本
```

但修改 C# 后仍必须优先通过 UnityMCP：

```text
触发 Unity 编译
读取 Console
运行相关测试
```

不能只依赖外部 C# 编译器或文本检查声称 Unity 项目已经验证。

## 4.4 UnityMCP 不可用时

UnityMCP 不可用、能力缺失或连续失败时，可以使用替代工具，但必须记录：

```text
原计划使用的 UnityMCP 操作
不可用或失败的具体原因
采用的替代方式
替代方式的风险
后续如何通过 Unity 验证
```

如果缺少 UnityMCP 导致无法可靠验证：

```text
场景
Prefab
ScriptableObject
Input Actions
序列化引用
PlayMode 行为
```

不得把相关工作标记为完全验证。

## 4.5 候选和 ExecPlan 要求

涉及 Unity 操作的候选摘要和正式 ExecPlan 必须写明：

```text
UnityMCP operations:
    <需要执行的 MCP 操作>
```

如果计划不需要 UnityMCP，也要写：

```text
UnityMCP operations:
    Compilation and relevant test execution only
```

不得只写模糊的“验证 Unity”。

## 4.6 收口报告

每轮最终汇报必须注明：

```text
UnityMCP 使用情况
通过 UnityMCP 完成的操作
未能通过 UnityMCP 完成的操作及原因
采用的替代方式
是否仍存在未验证的 Unity 资产或生命周期行为
```

---

# 5. 项目边界

当前目标是实现：

```text
通用确定性框架
通用 Runtime
数据驱动创作管线
自动测试能力
```

除非用户明确批准，不实现：

```text
具体英雄
正式技能
正式 Buff
正式装备效果
正式平衡数据
最终动画、VFX、音频、UI 或地图内容
内容专属的核心框架分支
```

正式设计中的具体玩法内容只作为通用行为示例和测试场景，不影响实施优先级。

---

# 6. 候选规模规则

每个候选必须给出预计代码修改量。

## 统计口径

代码修改量使用：

```text
预计新增行 + 预计删除行
```

只统计：

```text
生产 C# 代码
测试 C# 代码
必要的 asmdef JSON 代码行
```

不统计：

```text
Markdown 文档
Unity .meta
自动生成文件
Package lock 文件
纯格式化改动
```

## 硬性范围

每个候选的预计代码修改量必须在：

```text
200 ～ 3000 行
```

常规目标：

```text
约 500 行
```

建议常规区间：

```text
350 ～ 800 行
```

同一轮三个候选的预计代码修改量平均值应尽量接近：

```text
500 行
```

## 拆分规则

```text
预计少于 200 行
    与同一职责、同一测试闭环的相邻工作合并。

预计超过 1000 行
    必须说明为什么仍然是单一内聚切片。

预计超过 3000 行
    禁止作为单个候选，必须拆分。

实施中发现手写代码实际将超过 3000 行
    在最近的完整可编译检查点收口；
    将剩余内容拆入下一轮候选；
    不通过扩大本轮范围完成。
```

不得通过以下方式人为满足行数：

```text
重复代码
无意义包装层
超量注释
机械格式化
生成大量占位类型
把不相关任务拼在一起
```

候选完成后必须报告实际代码修改量，并与预估比较。

---

# 7. 首次接管模式

用户发送：

```text
接管项目
```

或新聊天首次加载本协议时：

只读取：

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

然后只针对三个候选读取：

```text
候选引用的正式设计章节
候选涉及的现有代码和测试
```

不得默认：

```text
重读全部设计案
重读全部历史 ExecPlan
运行全部测试
重新扫描整个仓库
重写 REPOSITORY_MAP
```

仅当 `CURRENT_HANDOFF.md` 缺失、明显过期或与代码冲突时，才扩大检查范围。

接管输出只包含：

```text
当前项目状态摘要
三个候选的简短比较
首选 / 次选 / 第三选择
Go / Conditional Go / No-Go
```

然后停止等待用户批准。

---

# 8. 用户命令

正常情况下，用户只需发送：

```text
执行候选：A
```

或者：

```text
执行候选：B
```

或者：

```text
执行候选：C
```

也允许：

```text
重新比较候选
```

和：

```text
暂停，不生成下一轮候选
```

---

# 9. 执行候选模式

收到：

```text
执行候选：<A|B|C>
```

后自动完成整轮。

## 9.1 即时生成正式 ExecPlan

从 `NEXT_CANDIDATES.md` 读取被选候选，只为它生成一份正式 ExecPlan。

正式 ExecPlan 保持精炼，只包含：

1. Purpose 和可观察行为；
2. 正式设计文件与相关章节；
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

不得复制：

```text
AGENTS.md
本协议全文
完整项目背景
无关设计摘要
历史审计内容
```

## 9.2 最小执行前检查

只检查：

```text
当前 Git 状态
计划涉及的文件是否变化
相关程序集当前能否编译
计划依赖的正式契约是否仍一致
```

不默认运行全量测试。

已有近期可靠基线，且相关代码没有变化时，直接复用该基线。

## 9.3 实施

严格执行正式 ExecPlan。

不得：

```text
顺手实现下一阶段
扩大到多个独立系统
创建重复 UID、Command、Snapshot、Aim、AbilitySignal、Checksum、FixedPoint 或 DTO
修改正式设计迁就实现
增加未经批准的 Package
覆盖无关修改
使用占位成功、吞异常、TODO 或弱化测试
```

普通私有实现细节直接采用最小、清晰、可测试方案，并记录在 ExecPlan 的 Decision log。

任何会改变正式设计契约或跨模块架构的方案都不属于“普通实现细节”，必须走设计偏离审批。

## 9.4 测试随实现同步完成

不再执行“编码结束后重新开始一轮完整验收”。

开发过程中按增量运行相关测试：

```text
纯确定性代码
    相关 EditMode 或纯逻辑测试。

Unity 生命周期、GameObject、场景、资产、Input、UI、Presentation
    相关 PlayMode 测试。

公共协议、Snapshot、Canonical Serialization、Checksum
    相关跨模块回归测试。
```

不默认运行整个项目的所有 EditMode 和 PlayMode 测试。

代码修改完成后只做一次最终：

```text
通过 UnityMCP 触发 Unity 编译
通过 UnityMCP 检查 Console
通过 UnityMCP 运行相关测试集
最终 diff 检查
```

只有 UnityMCP 明确不可用时才允许使用替代方式，并必须记录原因。

## 9.5 定向收口检查

只检查本轮触及内容：

```text
计划 Purpose 是否实现
设计章节是否满足
是否超出 Scope
是否产生重复协议
程序集依赖是否正确
稳定顺序是否明确
Snapshot / 序列化 / Checksum 是否符合计划
测试是否真正验证行为
实际代码修改量是否在 200～3000 行
是否逐项符合正式设计章节
是否存在未经批准的设计偏离
```

范围内 P0/P1 立即修复并重跑相关测试。

范围外问题只写入 `CURRENT_HANDOFF.md`，不在本轮扩展处理。

P2 不顺手清理。

通过后将正式 ExecPlan 标记：

```text
Completed and verified
```

---

# 10. 文档更新最小化

每轮必须更新：

```text
当前正式 ExecPlan
Docs/Implementation/CURRENT_HANDOFF.md
MODULE_STATUS.md 中受影响的行
```

只有以下变化时才更新 `REPOSITORY_MAP.md`：

```text
程序集
组合根
协议所有权
仓库结构
关键资产路径
```

只有形成新冻结架构决策时才更新 `DECISION_LOG.md`。

不要每轮重写完整状态文档。只记录增量。

`CURRENT_HANDOFF.md` 保持短小，建议不超过约 150 行，只记录：

```text
当前分支和 Git 状态
最近完成计划
编译和相关测试结果
已完成能力
真实 P0/P1/P2
冻结契约
下一轮候选
接管限制
```

---

# 11. 下一轮候选只写摘要

执行收口后，覆盖更新：

```text
Docs/Implementation/NEXT_CANDIDATES.md
```

不得提前创建三份完整 ExecPlan。

每个候选只写：

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
Design conformance：Strict，或 Approval required
UnityMCP operations
```

每个候选摘要建议控制在：

```text
150 ～ 300 字
```

三个候选必须：

```text
真实不同
都具有实施价值
都满足 200～3000 行范围
常规目标约 500 行
可独立编译和测试
不依赖未冻结公共协议
不实现正式游戏内容
默认严格符合正式设计
任何偏离都明确标记 Approval required，且不得自动执行
```

最后给出一张简短比较表：

| 候选 | 预计行数 | 风险 | 解锁价值 | 推荐 |
| ---- | -------: | ---- | -------- | ---- |

然后停止，等待用户下一次发送：

```text
执行候选：A
```

---

# 12. 允许停止的阻断

只有以下情况可以停止执行并请求用户决策：

```text
两个 Current 设计案存在无法消解的公共契约冲突
必须修改批准范围外的核心协议
必须增加未经批准的 Package
存在无法在计划范围内解决的外部编译阻断
正式设计要求彼此矛盾
实现需要偏离正式设计但尚未得到用户批准
```

普通实现细节、私有类型命名和局部文件组织不得反复请求确认。

---

# 13. 每轮最终输出

最终输出保持简短：

## 本轮完成

```text
正式 ExecPlan
可观察行为
实际代码修改量
主要文件和公共契约变化
编译结果
EditMode / PlayMode 结果
设计一致性结论
是否存在设计偏离，以及批准记录
UnityMCP 使用和验证结果
范围外问题
```

## 下一轮候选

```text
A / B / C 简短比较
推荐顺序
NEXT_CANDIDATES.md 路径
均未执行，等待批准
```

不得重复项目背景、全局规则、完整设计内容或完整测试日志。

---

# 14. 当前动作

首次使用时进入接管模式。

后续用户只需发送：

```text
执行候选：A
```

模型便完成：

```text
生成被选正式 ExecPlan
→ 实施
→ 同步测试
→ 定向收口
→ 更新最少文档
→ 生成下一轮三个摘要候选
→ 停止等待批准
```
