# Karolina 通用测试诊断包

Karolina 提供的 `com.karolina.test-pipeline` 是可由 Unity 工程直接引用的第一方 UPM 包。包内程序集名为 `Karolina.Testing`；Karolina 桌面程序也通过项目引用使用同一份通用实现。

## 适用范围

- Unity 2022.3 及以上，使用 .NET Standard 2.1 API 配置。
- 普通 .NET 工程可以引用 Karolina 仓库中的 `Karolina.Testing` 项目，或直接引用构建得到的 `Karolina.Testing.dll`。
- 核心程序集 `Karolina.Testing` 不依赖 Karolina 桌面程序、Unity API 或特定项目的 Tick 系统。
- 同一 UPM 包还提供可选的 `Karolina.Testing.Unity` 程序集；不需要 Unity 生命周期适配的项目无需引用它。

## 安装

在 Unity Package Manager 中使用 Karolina 仓库地址并指定包目录：

```text
https://github.com/Mosya-CX/Karolina.git?path=/Packages/com.karolina.test-pipeline#ae8735cd87c98b52a575e8f0fb839fd6163ffdd7
```

也可以在 `Packages/manifest.json` 中添加：

```json
{
  "dependencies": {
    "com.karolina.test-pipeline": "https://github.com/Mosya-CX/Karolina.git?path=/Packages/com.karolina.test-pipeline#ae8735cd87c98b52a575e8f0fb839fd6163ffdd7"
  }
}
```

包版本由 `package.json` 管理。项目应固定到已验证的 Git tag 或 commit；未发布的本地工作区内容不能通过公开 Git 地址取得。

本工程云端版本固定到上面已经推送到GitHub的提交，锁文件使用同一URL和提交SHA；其它电脑无需复现本机目录布局。当前开发机工作区继续使用本地相对路径 `file:../../../../Github/Karolina/Packages/com.karolina.test-pipeline` 便于调试，只有提交索引使用固定Git引用。工作区因此会显示两个Package文件相对HEAD的本机配置差异；它们不是待推送的产品修改。

## 基本接入

```csharp
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Karolina.Testing;
using UnityEngine;

public static class TestDiagnosticsExample
{
    public static async Task RunAsync()
    {
        var identity = new TestSessionIdentity(
            runId: "run-20261011-001",
            scenarioId: "buff-expiration",
            attemptId: "attempt-1",
            attemptNumber: 1);
        var recorder = new TestSessionRecorder(identity);
        await using var log = AsyncTestSessionLog.OpenFile(Path.Combine(
            Application.persistentDataPath, "KarolinaTests", "run-20261011-001.jsonl"));

        recorder.Record(
            log,
            TestSessionLogLevel.Info,
            layer: "prepare",
            eventType: "scenario.started",
            message: "测试夹具已准备",
            timeline: null,
            data: new Dictionary<string, string> { ["mode"] = "playmode" });

        await log.FlushAsync();
    }
}
```

项目负责选择写入路径及确保会话结束时刷新、关闭日志。`Record` 只进行有界队列入列；耗时的流写入由后台执行。容量、批量大小和刷新间隔可通过 `TestSessionLogOptions` 配置。队列饱和会增加丢弃数；写入异常可从 `LastFailure` 和 `Stats` 查询，刷新时也会报告不完整日志。

## 项目测试适配合同

项目在自己的程序集实现 `ITestPipelineScenario<TFixture, TSample>`，用类型化夹具和采样值连接项目逻辑，再通过 `TestPipelineScenarioRegistry` 显式注册场景工厂。注册表按场景 ID 排序列出场景、拒绝重复 ID，不扫描或自动激活工程程序集；`Get` 会验证工厂返回的 ID/标题，并通过进程级弱身份表拒绝任一注册表在旧实例仍存活时再次返回同一实例。工厂应当每次创建新场景对象，不能返回单例或缓存对象。

```text
PrepareAsync → InjectAsync → SampleAsync → AnalyzeAsync → CleanupAsync → 刷新诊断日志
```

Unity 适配器可在回调中等待 Unity 生命周期并使用主线程 API；普通 .NET 工程可以使用自己的测试夹具。Unity 调用方必须从 Unity 主线程启动流水线，或由适配器把每次 Unity API 操作显式派发回主线程；`ConfigureAwait(true)` 只保留调用时已有的同步上下文，通用库不会自行切换线程。执行器会校验会话 `ScenarioId` 与场景 ID 一致，并阻止同一场景实例被多个执行器并发运行；不同会话必须取得独立实例。每个阶段接收取消令牌；调用方负责设置用例总时限并在超时后取消。清理阶段使用独立的有限时限取消令牌，适配器需要响应该令牌；运行库无法强制终止忽略取消令牌的项目代码。准备阶段若在返回夹具前失败，适配器必须自行回滚部分创建的状态。

```csharp
registry.Register<BuffFixture, IReadOnlyList<BuffSample>>(
    "buff.apply", "Buff 应用与周期采样", () => new BuffApplyScenario());
var scenario = registry.Get<BuffFixture, IReadOnlyList<BuffSample>>("buff.apply");
var result = await executor.RunAsync(scenario, identity, log, cancellationToken);
```

`BuffApplyScenario`、夹具和采样类型均由项目实现；`Get` 返回的实例只用于这一轮执行。Karolina 的 Unity 调用适配器通过项目场景目录连接 Unity Test Framework/UnityMCP，项目仍负责提供真实测试方法。

### Unity 生命周期采样

Unity 项目可额外引用包内的 `Karolina.Testing.Unity` 程序集。`UnityFrameClock` 在真实 `Update` 回调中发布帧样本，可用于无自有 Tick 的项目；等待只会由调用之后帧号严格递增的 `Update` 完成，即使调用发生在本帧 `Update` 之前，也不会把同帧当作“下一帧”。帧号只作为 `UnityFrame` 诊断坐标，不是权威游戏时间，也不决定断言结果。

```csharp
using Karolina.Testing.Unity;

var clock = gameObject.AddComponent<UnityFrameClock>();
UnityFrameSample sample = await clock.WaitForNextFrameAsync(cancellationToken);
context.Record(
    TestSessionLogLevel.Info,
    "sample",
    "lifecycle.frame.sample",
    "采集 Unity Update 帧样本",
    sample.ToTimelinePosition());
```

从 Unity 主线程开始等待，使后续调用 Unity API 的异步阶段可以捕获 Unity 同步上下文。禁用或销毁时，未完成的等待会以明确错误结束；取消令牌会移除对应等待。适配器不会模拟输入、推进游戏逻辑或替项目判断功能正确性。

`AnalyzeAsync` 必须返回至少一条明确的 `TestAssertionResult`。全部断言通过且清理和诊断封存完整时，流水线才返回通过；断言失败、结论不确定、没有断言、阶段异常、被取消、清理失败和日志不完整分别保留状态。日志中的阶段事件只提供诊断，`TestPipelineExecutionResult` 和断言结果才是本次机器执行回执。执行器刷新日志但不关闭日志流，流的创建和关闭仍由调用方负责。

## 时间与事件

### 预编排运行态与复现

需要连续 PlayMode 测试时，在项目测试的 `SampleAsync` 中使用 `PreparedRuntimeRunner`。项目实现 `IPreparedRuntimeAdapter<TSample>`：`WaitForNextSampleAsync` 等真实下一帧、逻辑 Tick 或事件，`Read` 返回当前快照，`Timeline` 提供可选时间域，`Describe` 仅输出需要分析的字段。领域动作通过 `PreparedRuntimeAction<TSample>` 提前登记触发条件和调用；例如准备单位后调用 `AddBuff`，无需模拟鼠标。

运行器按顺序执行动作，每项只执行一次；动作执行后重新采样，再判断是否结束。只有全部动作执行且项目结束条件满足才标记 `Completed`；达到采样上限或超时会返回未完成，项目分析层必须将其纳入断言。默认上限600个样本、30秒安全超时；可配置最多10000个样本、5分钟。外部取消保留中断，异常进入外层流水线清理。取消是协作式，项目回调必须响应取消，不能启动脱离会话的后台写入。

`TestReproduction` 保存项目显式提供的输入、适配器标识、实际依赖版本、时间域和可选种子；不使用随机输入时种子为空。运行器自动写 `runtime.reproduction`，每一段有独立 `segmentId`，采样事件带对应 `sampleId`。断言使用实际样本的 `Id` 关联数据，而非写死编号。不同段样本不会互相覆盖；墙上时间只用于安全超时，不成为游戏的权威 Tick。

界面右侧仅显示当前检查的机器计数、阻塞和证据入口。打开证据后可看具体断言的实际值、预期值，并点击样本编号展开本次采样；复现输入和多个运行段可分别展开。日志默认收起。没有采样关联会明确提示，不以旧通过或模型总结替代当前回执。

每条事件都带有 `RunId`、场景、尝试编号、递增序号、UTC 时间、会话内单调耗时、来源层、事件类型、严重度、消息和附加数据。每次测试尝试创建一个 `TestSessionIdentity`，该尝试中的多个 `TestSessionRecorder` 必须共用它，不能跨尝试复用；这样它们共享同一序号和单调计时器。对同一日志的并发 `Record` 会在共享身份锁内分配序号并入队。

警告和错误使用优先队列，可能先于较早入队的一般事件写到 JSONL 前面。因此行的物理顺序不保证等于逻辑发生顺序；需要按 `RunId`、`AttemptId`、`Sequence` 排序还原事件顺序。`TestTimelinePosition` 可省略；只有项目确有权威时间轴时才提供 `UnityFrame`、`SimulationTick` 等域和值。不同域不能直接比较，日志也不会据此判断测试通过。

诊断日志只用于回看执行过程。机器通过/失败必须来自已声明的测试断言和测试运行器真实回执。不要把密码、访问令牌、完整用户消息或无关个人数据写入日志。

## 从 Karolina 启动项目测试

普通“执行任务”在完成实施后由宿主自动执行计划测试，不需要另按按钮。制定计划时在正文写六层设计，独立元数据 testDesign.cases 的 checkId 对应 verificationChecks 的 id。各项包含 preparation、input、sampling、assertions、cleanup、diagnostics；mode 为 required。没有适用机器测试时明确填写 mode=not-applicable、cases=[] 和 reason，不会显示机器通过。第一次工程写入前冻结，后续不能改断言或入口。

左侧主导航“测试与验证”用于人工重现、复测或验收辅助；状态栏不再提供测试入口。选择一个或多个已登记测试条目即可启动，无需选择或新建计划。页面左侧列出条目和历史，正文展示准备、输入、采样、预期、清理和诊断，右侧显示提交反馈、真实阶段、断言、样本和复现参数。模块、标签、关联文档标题及源码路径均可搜索；需求、计划及源码等可选关联资料默认收起，只在需要时展开查找。

默认只运行机器测试，不调用Agent、不采集整个工程的审批基线、不生成空审批条目。显式勾选“失败后允许Agent自动修复与独立复审”后才显示模型和思考挡位，默认优先低成本型号；刷新保留可用的已选挡位。修复前保存实际基线，每轮独立复审后运行原冻结检查；只有实际工程变更才进入审批。没有计划的修复任务被驳回后可从审批页“返回再执行”，沿用原任务基线及原测试条目，不要求补建计划。可选关联文档缺失会提示，不能冒充已读取，也不会阻止独立复现。

默认最多两轮修复，无进展期限可在“统一设置→AI与执行”设置，默认300秒；单轮Agent最长15分钟。权限申请等待期间不按无进展判定，但仍受单轮上限约束。纯测试完成保留结果供人工查看；这不是任务审批或计划人工验收。

项目在 `Docs/tools/test-scenarios.json` 登记 JSON 数组，例如：

```json
[
  {
    "id": "buff-heal-over-time",
    "title": "周期治疗 Buff",
    "backend": "unity",
    "testMode": "EditMode",
    "testMethod": "FrameSyncMoba.Unit.Tests.KarolinaBuffTestPipelineTests.HealOverTimeScenario_UsesProjectRuntimeAndSealsEvidence",
    "runtimeScenarioId": "framesync.buff.heal-over-time",
    "paths": ["Assets/Scripts/Gameplay/Tests/KarolinaBuffTestPipelineTests.cs"],
    "preparation": "准备单位、数值和 CombatSystem",
    "input": "Apply 治疗 Buff",
    "sampling": "逐 Tick 记录生命值",
    "capabilities": {
      "features": ["Buff", "周期治疗"],
      "adapterPaths": ["Assets/Scripts/Gameplay/Tests/KarolinaBuffTestPipelineTests.cs"],
      "actions": ["BuffHandler.Apply"],
      "sampleFields": ["health", "tick"],
      "timeDomain": "SimulationTick"
    },
    "metadata": {
      "module": "战斗 / Buff",
      "tags": ["Buff", "周期治疗"],
      "requirements": ["REQ-FEAT-048"],
      "plans": [],
      "cleanup": "销毁测试单位并还原测试世界、配置与时钟",
      "diagnostics": "记录各次实际生命值与对应逻辑Tick，保持原断言"
    },
    "assertions": ["生命值符合原测试预期", "夹具清理完成"],
    "timeoutSeconds": 120
  }
]
```

`paths` 必须包含本次检查依赖的测试及实现文件，用于使旧结果过期；示例只列测试文件，实际登记应补齐真实实现依赖。`testMethod` 是唯一精确测试方法；场景定义在任务启动后冻结，不能改断言或命令来清除失败。当前登记不自动发现或自动编写全项目测试。

`capabilities` 为可选字段，旧登记兼容；其中 `adapterPaths` 必须包含在 `paths` 中，以便冻结对应源码版本。Agent 可调用 `project_test_capabilities(query,take)` 查已登记场景、输入动作、采样字段和时间域；同时返回图谱内测试源码候选及真实方法/行号。候选只是静态识别，不能作为“已测试通过”的证明；索引过期、缺失、结果截断均会提示。制定计划先查询这些能力、读取候选实现，再填写六层设计和检查入口；缺少适配器或断言时应明确列为待实现内容。

大文件支撑实现可在capabilities中可选登记reviewSources，例如`[{"path":"Assets/Scripts/Combat/CombatSystem.cs","methods":["BeginTick","EndTick"]}]`。这些C#文件必须也在paths内；最多12个文件、每个20个方法名。宿主复用词法方法范围提取全部同名重载的完整方法与原始行号，保留整个文件SHA256，并明确其余成员省略。单文件最多256KiB、材料最多20000字符；缺声明、超限、空字节或版本变化就停止。需要的调用依赖应继续显式登记；词法片段不代表编译语义绑定。

非Unity工程可用 `backend: "command"`，提供 `command`，`testMode/testMethod` 为空，省略 `runtimeScenarioId`。命令在真实只读沙箱执行，退出0只是该命令检查成功；项目要自行配置可信断言与真实失败退出码。

### 调度与证据边界

- 准备、预输入、逐帧/事件采样、分析和清理由项目测试适配器执行；宿主负责启动、保存证据和有界修复，不通过鼠标实时操纵玩法。
- 明确失败才进入修复。每次修复后独立只读子Agent核对宿主冻结的测试条目或计划设计、可读取的关联需求、文件前后文本、原检查与失败输出，再由宿主复测。缺少必要材料或材料过大时停下，不冒充复审通过。
- 两轮失败或无有效改动时只读提出增强诊断方案。原断言保持不变，额外生产采样或继续修复需人工确认。
- `TestAssertionResult` 可传入 `sampleIds`，采样事件在附加数据中写入对应的 `sampleId`；界面据此关联断言与样本。未提供关联时明确显示缺失。
- 配置 `runtimeScenarioId` 时，Unity适配器将流水线事件导出到当前工程 `.karolina/state/scenario-evidence/<attemptId>.jsonl` 并封存，跨PlayMode域重载恢复请求。宿主校验场景、运行和尝试身份，只有真实测试结果及完整证据均成立才判为通过。
- 异常退出后，“测试与验证”可显示已有证据请求。释放须匹配已结束尝试且当前宿主无测试执行，连接正确Unity工程后再次核对编辑器及TestRunner闲置状态；保留日志，不无条件删除仍在使用的请求。该恢复入口已通过真实Unity结束请求联调；释放不会改写已有JSONL证据。
- 流程、验证和审批记录持久化在目标工程。重启显示中断，禁止自动恢复写任务；纯测试完成不产生审批；修复造成实际变更时，机器通过后等待人工审批，不自动关闭计划。


### 运行与复审的已验证边界

PlayMode域重载期间，释放证据请求可能遇到连接故障。宿主对同一尝试编号的释放最多三次有界重试，每次重新核对工程身份；不因此自动重跑机器测试。测试和释放都失败时保留两项原因，释放最终失败不能交付通过。

清理应在实际完成后记录对象销毁、世界注销、物理登记和配置引用状态；仅安排Destroy不等于已经销毁。失败、超时或诊断未封存都阻止通过。项目观察事件在复审索引保留sequence、eventType、timeline与数据，最多64条，裁剪明确提示；必要原回执仍保留。模型意见必须引用真实源码或事件字段，不能以模型文字覆盖机器结果。

PLAN-KAR-024本批已完成普通任务自动Buff/Gameplay测试、独立复审、断言样本点击和结束请求恢复。用户仍需人工验收；主页面“测试与验证”用于无需计划的复测或重现。UPM源码已推送，云端工程固定到已发布Git提交，本机继续保留本地开发引用。
