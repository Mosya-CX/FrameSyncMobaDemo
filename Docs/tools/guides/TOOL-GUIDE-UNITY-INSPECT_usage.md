# Unity工程只读检查使用说明

## 用途

读取当前已核对 Unity 工程的 Editor 状态或 Console 错误，不清日志、不切换运行、不保存场景。

## 使用

在 AI 对话页连接 Unity，再在拓展工具页选择 Editor状态或Console错误。参数是 MCP 参数 JSON；状态通常使用空对象，Console默认 maxEntries=100、logTypeFilter=Error、includeStackTrace=false。点击调用后查看真实输出和运行记录。

## 失败与边界

未连接、工程不符或MCP失败会显示实际错误。无错误日志不等于玩法通过；不能作为测试验收。外部工具声明使用 unity-mcp、明确 toolName 与 defaultArguments，配套说明必须描述副作用；未知Unity工具需执行任务计划。
