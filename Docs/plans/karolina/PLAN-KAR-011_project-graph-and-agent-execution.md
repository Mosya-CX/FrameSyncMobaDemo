# 工程图谱、Agent 执行闭环与统一设置页

## 参考需求

- [文档参考与工程资源索引](../../requirements/karolina/REQ-KAR-010_document-context-and-resource-index.md)：外部仓库引用、默认选择及文档仅由关联仓库持有；本次校正所依据的新增条款。

- [工程代码与 Unity 资源关系图谱](../../requirements/karolina/REQ-KAR-013_project-code-resource-graph.md)：索引数据、关系推导、搜索与进度。
- [Agent 任务验证与有限修复闭环](../../requirements/karolina/REQ-KAR-014_agent-verification-repair-loop.md)：真实工具事件、验证指引、任务审批和恢复。
- [统一设置页与模型挡位预设](../../requirements/karolina/REQ-KAR-015_unified-settings-and-model-profiles.md)：项目设置、默认模型/挡位与统一入口。
- [Karolina 独立仓库迁移](../../requirements/karolina/REQ-KAR-016_standalone-karolina-repository.md)：源码、文档、启动与独立版本库。

## 实施细节

### 领域边界

- Core 新增工程图谱节点/边模型、只读文件索引器和原子持久化快照；不引用 Desktop/Unity 程序集。
- Core 新增项目设置模型与 JSON store；不保存密钥，主题持久化继续由现有 AppearanceStore 独立拥有。
- Desktop Workbench 组合实例、路由与真实 Codex 事件；进度来自 app-server 事件，不推断自然语言状态。运行文件/日志继续由 EvidenceStore 拥有。
- Web 新增工程图谱页和统一设置页，使用现有纯控制器依赖、API 请求、主题组件和 Markdown 样式。

### 实施步骤

1. 为四个需求写明计划引用快照，验证设置与图谱数据的根路径/原子写入边界。
2. 实现只读图谱索引：规范 Assets/Packages/ProjectSettings 路径；排除链接/生成树/.meta展示节点；构建代码文件/类型和 GUID 资源关系，确定性排序，记录跳过与容量错误。
3. 提供图谱状态、节点搜索、节点详情和异步重建端点；错误不能丢弃上次成功快照。
4. 建立有 schemaVersion 的项目级设置读写接口，校验模式、模型挡位可用项与修复次数；为旧版本填补安全默认值。
5. 统一 rail 设置入口，迁入当前外观编辑视图，加入 AI/执行配置与工程图谱状态分区；对话从预设加载初值且允许当前对话覆盖。
6. 记录 Codex 工具与轮次状态到 RunRecord 和审批详情；加入计划约束、真实验证回执和配置修复轮数的执行指引。原有审批文件版本、人工意见和最终批准流程保持。
7. 更新 Karolina README 的真实功能边界、设置入口和限制，补充源码/编译证据；没有运行的行为与Unity测试不得标通过。
8. 将应用源码、设计资源和构建入口迁至独立仓库；Karolina 产品需求、计划、规则和事实留在当前连接 Unity 工程的 `Docs`，不复制进源码仓库；移除 Unity 项目中的旧应用入口并保留项目文档与未提交改动。

### 关键数据和算法

- `ProjectGraphSnapshot`: schemaVersion、root、indexedAt、scan counters、warnings、`Node[]` 与 `Edge[]`。节点 id 从类型+规范路径或完整符号名稳定哈希；边用 `(from, relation, to)` 去重排序。
- 代码分析仅从非注释/非字符串文本解析命名空间/类型声明；`contains` 边精确，类型引用仅在符号名唯一时产生。语言语法不完整时留下文件节点和 warning，不尝试修改/编译源码。
- 资源分析先从 `.meta` 读取 GUID 映射，再从受限可读文本资源读取 GUID 引用。重 GUID、缺 GUID、读取失败和大小限制都写入统计/warning；不读取或返回整个资源正文。
- 每工程设置文件 `.karolina/state/settings.json` 使用 ProjectContext.PathForState 与临时文件原子替换；读取严格验证结构，非法记录不覆盖原文件。
- 事件进度以 `item/started`、`item/completed`、`turn/started`、`turn/completed` 作为依据；将类型/工具/结果时间写入当前 RunRecord 并进入既有 JSON 证据目录。测试输出保存工具回执，`failed`、`interrupted`、缺终态和无效结果都不映射为通过。
- Agent 验证指引从计划及用户设置生成，同一线程、同一审批任务内执行；轮数只对遵循该执行协议的 Agent 有效，应用不声称强制控制任意第三方 Harness。

## 文件预估

### 预计新增

- `Karolina.Core/ProjectGraph.cs`：图谱模型、扫描、持久化。
- `Karolina.Core/WorkbenchSettings.cs`：设置记录、校验和存储。
- `Karolina.Desktop/Web/project-graph.js`：图谱页面控制器。
- `Karolina.Desktop/Web/settings.js`：统一设置页面控制器。
- `Karolina.Desktop/Web/styles/pages/project-graph.css`：图谱布局。
- `Karolina.Desktop/Web/styles/pages/settings.css`：设置布局。

### 预计修改

- `Karolina.Desktop/Workbench.cs`、`Workbench.Routes.cs`、`Workbench.Contracts.cs`：注入图谱/设置服务、返回状态并注册路由。
- `Karolina.Core/Processes.cs`、`McpClient.cs`：保存进度事件，并按真实终态呈现命令/测试结果。
- `Karolina.Desktop/Workbench.Chat.cs`、`Workbench.cs`：把设置化验证策略加入执行上下文和真实事件阶段记录。
- `Karolina.Desktop/Web/index.html`、`app.js`、`core/context.js`、`features/navigation.js`、`features/providers.js`：统一导航、状态与模型预设。
- `Karolina.Desktop/Web/appearance/controller.js`、`ui/icon-registry.js`：复用现有外观能力于统一设置页。
- `Karolina.Desktop/Web/style.css` 与页面样式文件：挂载新增 CSS 模块。
- `README.md`：记录当前功能与不能保证的执行边界。
- `Docs/catalog.json`：登记三份需求案和本计划并定位 `activePlan`。

## 机器测试与 Agent 审查设计

### 当前校正：资料引用与聊天反馈

此次校正承接用户验收问题，不重启关闭计划，也不建立另一份产品Docs。只修改独立Karolina程序和关联工程的本文档：

1. ResourceReference增加可选仓库身份，合并resourceRefs与externalRefs，按仓库/路径选择键去重；外部引用只描述来源，不自动读写，工程内ResourcePath检查不变。
2. 聊天上下文说明关联工程根目录和各资源来源，保留所选文档标题和路径；用户测试既有功能时Agent先交代理解的验收目标。引用资料不改变工作模式或授权写入。
3. Core将命令事件映射为可读活动，保留原始回执供诊断。测试通过要求外层工具成功终态和完整逐项结果；命令退出码0不等于功能验收通过。
4. Desktop按匹配thread/turn发布活动、公开reasoning summary和Agent计划步骤；请求summary=auto，模型与effort保持用户选择。原始reasoning content/textDelta不进入显示或摘要记录。
5. 前端独立chat-progress模块呈现本轮状态、等待反馈、步骤与公开摘要；不渲染CLI/工具JSON卡片。历史仅恢复公开summary，新对话清除旧卡，发送失败保留草稿，正在发送时锁住模型/模式/新对话切换。本轮完成后只读补取历史摘要，严格核对thread/turn；读取失败可见。空reasoning完成条目不能覆盖有效摘要，事件日志使用单行JSONL。
6. 先编译和定向测试、真实浏览器/协议/低档模型只读验证，再在旧程序完全退出后更新原EXE目录。真实文件与hash、回执保存在本计划独立元数据。

预计新增AgentActivity、chat-progress和两组专项测试；修改DocumentLibrary/DocumentResources、Processes、Workbench/Chat、chat/providers/documents控制器和聊天样式。无Unity实现/资产修改、无新增包、无产品文档搬入Karolina仓库。审批资源关联按含仓库的SelectionKey保存，兼容不同仓库同名文件。

本轮机器设计：实际PLAN-KAR-011默认资料解析、资源空选/逐项选和越界；跨仓库同名审批关联；工具外层失败而内层Passed不能通过；原始CLI、输出与私有推理标记不得出现在对话DOM；公开摘要实时/历史恢复、线程/轮次隔离、失败/中断/断连、新对话清理、主题输入不回归。全部禁止截图验收。

- Core 专项用例：完整临时工程索引、GUID 往返引用、C# 同名歧义、注释/字符串伪引用、缺失/重复 GUID、链接/生成目录排除、超量文件、损坏旧索引保留、路径根隔离。
- 设置 store 用例：默认值、Round 0–3、非法模式/模型挡位、原子保存/读取、损坏 JSON 不覆盖、旧 schema 回退以及不包含凭据字段。
- HTTP/DOM 用例：重建状态、搜索/节点方向关系、settings 保存并刷新、无模型/Unity未连/工具失败状态、对话当前模型覆盖默认、所有现有 theme import/export 控件保留。
- 执行事件用例：started/completed/turn 事件阶段映射；真实测试失败、无有效终态、用户中断和旧 RunRecord 不误报通过；审批基线和终态文件关联一致。
- 桌面集成行为后续需通过本机 HTTP/DOM 以及真实原生 Windows 鼠标/键盘；不截图验收，不把源码断言或编译代替行为回执。
- 本次交付不运行 Unity 构建或工程测试；独立只读 Agent 审查关注依赖方向、快照保留、路径边界、旧设置兼容、前端 API合同与任务锁/审批生命周期。

## 人工验收

用户从 rail 打开工程图谱搜索脚本/资源并浏览关系，再在统一设置页面修改各模式模型挡位、有限修复轮数和外观；重启后检查设置持久化。使用一个明确包含 Unity 聚焦测试的执行计划，观察 Codex 真实工具过程；Unity 未连接或测试失败时确认界面显示未通过/未运行，且任务仍需用户审批。禁止截图验收。

## 恢复与兼容

仅删除/重建 `.karolina/state/project-graph.json` 可恢复图谱，旧成功快照保留到新快照完整写入后再替换。设置格式升级前保留原 settings.json 副本；任何损坏或未识别字段冲突都不覆盖。RunRecord/任务文件以新增可空字段兼容旧版本。所有 Unity 文件只读，不改 .meta、GUID、场景或资产。

## 进度

- [x] 准备：需求与实施边界已确认；登记计划与文件预估。
- [x] 执行：实现 Core 索引/设置、Desktop 连接及前端页面，并迁入独立仓库。
- [x] 测试：定向 Core、任务审批测试，Release 编译、JavaScript 语法检查与真实工程只读图谱扫描通过。
- [x] 校正：独立审查发现 C# 命名空间/record struct、失效模型提示、损坏设置覆盖、验证回执展示和进度列表并发问题；已修复并补用例。
- [ ] 验收：等待用户实际操作确认。
- [ ] 关闭：人工确认后关闭，不提前自动关闭。

## 实施与机器取证 · 2026-10-05

- 工程图谱索引 C# 声明与引用、Unity `.meta` GUID 关系；本机只读扫描帧同步工程，得到 1,123 个解析文件、2,836 个节点、9,525 条关系。扫描还记录 35 个跳过项和 437 个未解析 GUID；后者可能属于外部、内置或重复 GUID，不能据此断定为工程错误。
- 合成夹具覆盖同文件多个 block namespace、file-scoped namespace、`record struct`、继承/类型引用、Prefab GUID 关系、缺失 GUID、二进制跳过、损坏索引/设置保护与备份恢复。
- 执行进度只认匹配当前 thread/turn 的 Codex 事件；命令有退出码时显示真实退出码；`tests-run` 仅在结构化逐项回执全部通过时显示通过，否则显示失败或未确认。无结果字段的工具完成只记录调用完成。
- 运行进度读写和快照持久化共用锁，状态 API 读取进度副本。
- 设置页会标出失效模型配置；模型不在当前目录时不得静默当成原配置有效。损坏设置禁止普通保存，显式恢复先备份原始字节。
- 应用已从 Unity 仓库移到独立 Windows/.NET 仓库 `E:/Github/Karolina`；应用、Web 资源和设计源文件存于源码仓库，Karolina 产品文档由当前连接工程的 `Docs` 持有。迁移期间未提交 Unity 仓库改动。
- 当前静态图谱采用受限文本解析启发式；不代表完整 C# 语义分析、运行时依赖或可执行行为。Agent 修复轮数是遵循协议的提示，不是对任意 Harness 的强制执行。

- 2026-10-05 文档归属校正：用户确认产品需求、计划、规则、事实及 `catalog.json` 只放在 Karolina 当前连接工程的 `Docs`，不存放于 `E:/Github/Karolina/Docs`。本计划、关联案和证据归回 FrameSyncMoba 的 `Docs`；源码 GitHub 仓库移除产品 `Docs`。
- 2026-10-05 启动入口校正：Release EXE 位于 Karolina 仓库 `artifacts/current/Karolina.Desktop.exe`；桌面快捷方式直接指向 EXE，不调用命令解释器。EXE 从本机 `%LOCALAPPDATA%/Karolina/last-project.txt` 读取 FrameSyncMoba 工程路径。
- 迁移仅涉及文档归属、目录索引和启动入口；没有提交、推送 FrameSyncMoba 工作区改动，没有修改三狼资源或代码，也未运行截图验收。

## 前端启动故障校正与取证 · 2026-10-05

实际页面复现：设置控制器将方法写入不存在的 `state.actions`，抛出 `Cannot set properties of undefined (setting 'applyModelDefaults')`。错误发生在模块注册阶段，早于主题、组件、工程载入和自动连接的初始化；页面停留于“载入工程…”，图标与按钮装饰数量为 0。此前 Release 编译与 JavaScript 语法检查未覆盖这个运行时合同错误。

校正删除两处错误赋值，保留控制器已正确注册到 `actions` 的方法。新增独立 `bootstrap.js` 捕获模块导入与注册失败，在页面上明确显示初始化错误。补充无第三方依赖的 Node 回归用例，验证真实工作台上下文的状态/动作边界，以及设置标签切换和外观挂载。

本轮机器回执：

- Release 解决方案编译、`artifacts/current` 交付编译：均为 0 警告、0 错误。
- 前端注册回归 1/1、图谱/设置隔离检查 3/3、外观隔离检查 15/15 通过。
- 完整浏览器页面启动、文字输入、文档阅读、图谱浏览、统一设置与外观标签切换通过；初始 48 个图标全部加载，动态夜空与桌宠可用，未出现页面异常。注入模块启动异常的夹具验证了可见错误提示。
- 实际桌面 EXE 的 WebView2 页面也完成上述启动及输入/设置检查：工程为 `E:/Unity/Item/FrameSyncMobaDemo`，使用完整动效；打开设置后 64 个图标全部加载，动态夜空已启用，桌宠图片已加载；Codex 自动连接并返回 8 个模型，Unity 自动连接且工程身份核对通过。检查后回到 AI 对话并清空临时输入，没有发送执行任务。

本轮没有变更 Unity 实现或资源，没有运行 Unity 正式构建，没有使用截图验收。计划保持“验收”，等待用户确认修复后的实际使用体验。

## 本次聊天校正的机器取证 · 2026-10-06

- 源码解决方案和原 EXE 目录 Release 编译均为 0 警告、0 错误；资料/反馈专项 10/10、前端状态与图谱/注册检查 14/14、既有资源与审批回归 13/13 通过。
- 浏览器经实际 app-server 协议夹具验证：所选计划的工程内/外部关联资源默认勾选后可发送；活动说明、失败状态、公开摘要、结束后历史补读和重开历史均可见；空摘要不清除有效摘要。原始命令/输出和私有推理标记未出现在聊天 DOM，新聊天清除旧进度。新日志逐行解析为合法 JSONL。
- 实际 Codex 使用 gpt-6-luna / low / read-only，携带 PLAN-KAR-011 及 54 个资源选择控件发送并完成；返回 23 条活动记录和公开摘要，普通页面没有原始工具卡片。本轮只读，不触发 Unity 操作或正式构建。
- 原 artifacts/current EXE 的真实 Windows 鼠标/键盘确认中文输入、计划勾选与公开历史摘要；临时输入已恢复。48 个图标全部加载，动态夜空与桌宠保持；Codex 自动连接。Unity 服务当前不在线，连接失败不得作为已通过集成验证。
- 独立只读增量审查的两项 P2 已修复（外层工具失败误读内层 Passed、跨仓库同名资源关联冲突），复核无剩余 P1/P2。原始机器回执及关联源码 hash 记录于本计划独立元数据，人工验收仍待用户确认。
