# 工程基线调查与确定性框架规划

## 参考需求

- [确定性随机与定点计算](../../requirements/determinism/REQ-FEAT-014_deterministic-random.md)：目标实现、技术方案、边界情况、附录，引用版本 1。
- [稳定 UID 与参与者身份](../../requirements/determinism/REQ-FEAT-015_stable-unit-identity.md)：目标实现、技术方案、边界情况、附录，引用版本 1。
- [命令序列化与类型化派发](../../requirements/frame-sync/REQ-FEAT-007_command-dispatch.md)：目标实现、技术方案、边界情况、附录，引用版本 1。
- [快照树与字段归属](../../requirements/frame-sync/REQ-FEAT-012_snapshot-ownership.md)：目标实现、技术方案、边界情况、附录，引用版本 1。

## 本次执行范围

原0000是确定性MOBA工程的只读基线调查与首次框架切片规划：核对fp、UID、Command、Snapshot/Checksum等实际合同和程序集依赖，给后续0001起的具体实现建立事实入口。它不对应Karolina软件产品目标，也不包含生产功能实现。

## 调查方法与数据流

1. 枚举 Assets/Scripts 中的C#、asmdef、Tests及组合根，建立类型→程序集→依赖边的索引；引用名称/GUID先解到实际asmdef，区分运行时、Editor、测试和预定义程序集。
2. 检索唯一fp、UnitUid、GameplayParticipantId、Command、Snapshot和规范字节写入，检查重复权威与当前需求的冲突；将实际路径和字段归属放工程事实，不能凭旧设计文档宣称实现。
3. 通过Unity MCP只读核对工程身份、Unity版本、场景与Console；当期编译/测试记录只作为当期证据保留。
4. 输出模块依赖和风险，后续确定性Tick、随机、UID、空间与序列化功能分别以独立计划执行；原候选机制已退出当前体系。

## 具体事实入口

- Docs/rules/index/FACT-4E2A6CA25B98_engineering-index.md：当前组合根、源码与模块事实。
- Docs/rules/index/assembly-dependencies.json：当前程序集与直接引用。
- Assets/Scripts/Gameplay/Unit/Core/UnitUid.cs：稳定生成身份。
- Assets/Scripts/FrameSync/SimulationTickPipeline.cs：实际全局阶段与快照归属。
- Docs/rules/facts/migration-coverage.json：原0000位置与可恢复指纹。

## 执行结果

调查当期已完成，本期保留已关闭记录。没有认领或撤销已有Gameplay和资源修改；源文本中的历史测试结果未在本轮重跑。当前原工程仍有脏工作区，按工程宪法保护。

## Agent验收与限制

索引每个类型/程序集必须落到真实路径，缺失/重复引用显式记录；协议审计按所列具体需求复核，不直接修改公开合同。人工功能测试不能证明程序集图无环，应由当前程序集事实检查。原已关闭记录不重启，后续工程调查或修复新建计划。
