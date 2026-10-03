# Karolina 架构审查

## 审查范围与结论

审查当前 Core、Desktop、前端与文档路由。当前架构**部分符合**七项要求，适合作为继续演进的 MVP；不能认定已全面达标。本轮修复持久身份、路径与并发边界，并为拓展工具引入接口；没有用文件拆分冒充应用层解耦。

## 当前组织

| 层次 | 主要职责 | 实际依赖 |
|---|---|---|
| Web 前端 | 对话、Markdown、审批、工具、页面状态 | HTTP API；app/review/workspace/tools 仍共享全局状态 |
| DesktopWindow | WinForms、WebView2、托盘、窗口退出 | Workbench 的窗口与运行状态 |
| Workbench | 本机 API、会话/审批/Unity/工具接线与执行调度 | Core 的具体实现；Tools partial 是接线拆分 |
| Core | 文档、工程身份、快照、工具定义与运行、协议客户端 | 文件系统、进程、HTTP 与 Codex app-server |
| 工程内容 | Docs 与 Unity 文件 | Karolina 位于 Tools，不进入 Unity Gameplay 程序集 |

持久数据分开：Docs 存当前文档、元数据和工具声明；`.karolina/state` 存审批、快照、聊天入口、页面位置、工具结果和共享锁，由 Git 忽略。本机旧审批保留供恢复，不作为新的当前写入位置。WebView2 profile 和既有 Codex 运行日志仍在本机应用数据目录；它们不决定审批条目是否存在。

## 七项原则逐项审查

| 要求 | 当前判断 | 具体依据与剩余问题 |
|---|---|---|
| 高内聚低耦合 | 部分符合 | 文档、审批、工程身份、工具服务各有类型；Workbench 同时调度多种用例，前端全局变量跨文件，耦合仍高。 |
| 关注点分离 | 部分符合 | 原生窗口与页面、Unity 内容与工程工具分开；HTTP 接线与任务执行仍在同一宿主，不能把 partial 当独立模块。 |
| 依赖倒置 | 部分符合 | 工具服务依赖 IExtensionToolRunner，Unity 适配器依赖 IUnityToolClient；宿主直接创建 CodexConnection、TaskReviewStore 等，其它用例尚无可替换端口。 |
| 清晰边界 | 主要边界已有 | 规范工程身份、拒绝越界/链接、审批只覆盖 Unity 内容；文档模式限制 Docs 写入，Unity 调用串行并核对工程，构建后暂停。外部程序以 Windows 用户权限执行，只读声明不是沙箱。 |
| 可测试可观测 | 部分符合 | 工具接口可替换，真实运行身份/状态/输出、审批修订和诊断可追查；大量文件系统和协议依赖仍需临时工程，尚无统一结构化日志/前端模块测试。本轮没有新增或运行行为测试。 |
| 简单实用 | 符合当前范围 | 保留 .NET/WinForms/WebView2 与普通 Markdown/JSON，无新依赖；只提供必要模式、任务审批、工具注册与调用。尚未引入数据库、通用插件装载器或完整 Harness 调度。 |
| 演进式设计 | 部分符合 | 从实际丢失记录问题提取 ProjectContext，从工具需求提取适配器接口；下一步应在具体用例变化时提取应用服务，不进行整仓框架替换。 |

## 本轮已修正的风险

1. 工程路径尾分隔符造成不同身份；现在统一规范路径。原生程序对本机旧路径曾返回空列表/找不到同名记录；旧文件有 EFS 加密属性，文件复制也真实报加密错误。恢复采用读取内容再写入工程状态目录，不继承旧加密属性。具体环境隔离机制仍未知，不把 EFS 属性等同于全部根因。
2. 状态迁移拒绝嵌套链接、来源身份不符和同名不同内容冲突；保留原记录，首次恢复后使用工程当前状态。共享写锁和桌面实例锁也在同一工程目录。
3. 工具参数、实际工具名与 Unity 身份在改变任务状态前核对；Unity 工具和原连接/操作共用闸门。构建标志在准备发出已解析的实际调用前写入；发送失败后的构建回执未知仍由用户确认，不自动重发。
4. 外部工具挂起创建，加入 Windows 作业后再恢复，管道读取遵循取消且清理有界；不把停止按钮实现为仅停止父进程。
5. 工具注册保护已有正文/元数据/声明，使用 CreateNew 和临时写入；同步目录入口、刷新文档缓存；失败回滚先核对本轮内容指纹，外部编辑保留并报出冲突。

## 后续演进顺序

1. 提取对话与任务执行应用服务：端口涵盖 Harness、审批存储、文档读取，Workbench 只映射请求/事件。
2. 前端按对话、文档、审批、工具定义状态与动作入口，替换共享全局变量，优先处理跨页面草稿与异步切换。
3. 统一结构化运行事件和恢复策略；在获测试请求后补工具超时/取消/注册故障及模式权限等行为证据。
4. 第二个真实 Harness 或第二种 Unity 接入出现时再提取更通用的接口；当前不做 Jev、图谱、自动开对话/模型映射或完整 MCP 服务。

## 取证与验证边界

当前原生进程的真实 API 已读取原三狼任务：编号 `a09bef8b3a1f47b09b6bf99e4b3efcb9`，待审批，修订 2，100 文件；没有替用户批准、退回、重建基线或改写 Unity 内容。Release 编译和 JS 语法检查通过，行为测试与人工使用验收尚未完成。本报告只读审查不证明外部工具或 Codex 模式的真实运行已通过。

本轮独立只读审查发现均已闭合，无剩余 P1/P2。实际通过托盘完全退出后重新启动 current，原生审批页显示60/全部100文件、隐藏40个.meta，恢复记录与原文件字节哈希一致。280篇当前资料零断链、路径均ASCII，2953个受保护Unity文件未变；这是读取与静态证据，不是新增工具行为测试通过。[诊断记录](../../plans/karolina/PLAN-KAR-006_workspace-inspection.json)保存本轮实际回执。

实现入口：[ProjectContext](../../../Tools/Karolina/Karolina.Core/ProjectContext.cs)、[TaskReviews](../../../Tools/Karolina/Karolina.Core/TaskReviews.cs)、[ExtensionTools](../../../Tools/Karolina/Karolina.Core/ExtensionTools.cs)、[Workbench](../../../Tools/Karolina/Karolina.Desktop/Workbench.cs)、[工具接线](../../../Tools/Karolina/Karolina.Desktop/Workbench.Tools.cs)。

进程实现依据：微软 [CreateProcessW](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createprocessw)、[进程创建标志](https://learn.microsoft.com/en-us/windows/win32/procthread/process-creation-flags)、[作业分配](https://learn.microsoft.com/en-us/windows/win32/api/jobapi2/nf-jobapi2-assignprocesstojobobject) 与 [句柄继承属性](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute)。
