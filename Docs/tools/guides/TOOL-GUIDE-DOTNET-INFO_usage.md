# .NET环境查看使用说明

## 用途与入口

执行已安装 dotnet 的 --info，读取本机 SDK/运行时信息，不安装依赖、不编译工程。

## 使用

在拓展工具页选.NET环境，参数为空对象，点击调用。成功输出来自真实标准输出；失败显示退出码和标准错误。

## 自定义外部工具

注册 external 类型，填写 executable、arguments 字符串数组、defaultArguments 和配套说明。参数可用 {name} 占位，值独立传入原生 ArgumentList，不拼接 shell 命令。调用以当前Windows用户运行，readOnly是注册者的副作用声明，不是操作系统沙箱；写入工具应声明为非只读并选择执行任务计划。
