# 金币批次确认与可用余额

## 目标实现

所有收入有唯一批次、摘要和确认累计，UI 查询一个派生余额。

## 技术方案

GoldIncomeRuntime 拥有 builder、未确认批次、digest、confirmed earned total/progress。CurrentAvailableGold=GetConfirmedEarnedGoldTotal(player)+EffectiveShopGoldDelta，只读派生。

## 边界情况

Account 不保存第二局内金币累计；派生余额不进 Snapshot；T 收入确认不主动回滚或补造本地已拒绝 Command；金币生产者待确认项单列。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/FrameSync/GoldIncomeRuntime.cs`：当前关联实现定义 GoldIncomeBatchDigest、GoldIncomeReason、GoldIncomeRecord、GoldIncomeRecordBatch、GoldIncomeSnapshot（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/FrameSync/Tests/GoldIncomeRuntimeContractTests.cs`：EmptyAndNonemptyTicks_SealDigestAndConfirmContinuously、Restore_RejectsTamperedDigest。
- `Assets/Scripts/FrameSync/Tests/LocalCommandGoldMatchFlowTests.cs`：FutureCommand_IsRetainedAndConsumedOnlyAtTargetTick、CastIntentAndAction_PreserveCommitVerbAndDirectionAim、PlanCastIntent_UsesAbilityCastRange_NotHardcoded、NaturalGold_IsTickDerivedCanonicalAndInsideOpenBatch、ClientPredictionCannotEnterEnding_ButServerAuthorityCan。
- `Assets/Scripts/Bootstrap/Tests/EditMode/GameplayIntegrationTests.cs`：TickContext_InitializesWithCorrectTick、TickContext_AdvancesCorrectly、DeterministicRandom_ProducesSameSequenceForSameSeed、UnitUid_ComparisonAndSorting、PathGrid_Initialise_ProducesValidGrid、AStar_FindPath_ReturnsValidPath、FlowField_BuildAndQuery_ReturnsValidDirection。
- `Assets/Scripts/FrameSync/Tests/BootstrapDeterminismProbeTests.cs`：ServerFirstTick_MatchesClientPredictionFirstTick。
- `Assets/Scripts/FrameSync/Tests/EquipmentShopViewTests.cs`：CurrentAvailableGold_UsesConfirmedIncomeAndEffectiveShopDelta。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 唯一职责边界

正式以 `moba_equipment_system_design_v11_unified_gold_income_runtime.md` 为准。

`GoldIncomeRuntime` 是一局比赛内 Gameplay 金币获取记录、未确认批次和确认累计收入的唯一所有者：

```text
GoldIncomeRuntime
    CurrentBatchBuilder
    UnconfirmedBatchHistory
    GoldIncomeBatchDigestHistory
    InitialEarnedGoldByPlayer[]
    ConfirmedEarnedGoldTotalByPlayer[]
    ConfirmedIncomeThroughTick
    CurrentBuildingTick
    NextIncomeSequenceInTick
    BuildState
    ServerSettlementSink optional
```

帧同步总控不得持有第二套金币批次缓存、确认 Ledger 或摘要历史。

### 对外接口

所有金币来源只调用：

```csharp
public interface IGoldIncomeRequester
{
    void RequestGoldIncome(
        PlayerSlot receiver,
        int amount,
        GoldIncomeReason reason);
}
```

调用方不传：

```text
LogicTick
IncomeSequenceInTick
BatchId
确认标记
累计金币
```

商店和 UI 只读取：

```csharp
public interface IConfirmedGoldIncomeView
{
    int GetConfirmedEarnedGoldTotal(
        PlayerSlot player);

    int ConfirmedIncomeThroughTick
    {
        get;
    }
}
```

### 初始化

```text
GoldIncomeRuntime.Initialize(
    MatchStartTick,
    InitialEarnedGoldByPlayer)
```

初始化后：

```text
ConfirmedEarnedGoldTotalByPlayer[player]
    =
    InitialEarnedGoldByPlayer[player]

ConfirmedIncomeThroughTick
    =
    MatchStartTick - 1
```

初始金币不生成记录，不等待 AuthorityFrame，也不提交持久化。

### Tick 构建与固定请求顺序

每个 Tick：

```text
GoldIncomeRuntime.BeginTick(T)

1. NaturalGoldIncomeSystem
       按 PlayerSlot 升序请求自然金币。

2. CombatSystem.SettleTick。

3. MatchStatisticsRuntime
       消费 FormalDeathResults。

4. CombatGoldIncomeProducer
       按 GoldIncomeAllocations 规范数组顺序请求。

5. Map / MatchRule Gold Producers
       按代码固定生产者顺序请求。

GoldIncomeRuntime.SealTick(T)
```

不得依赖：

```text
组件注册顺序
Dictionary 枚举顺序
Unity 对象创建顺序
ScriptableObject 加载顺序
```

### 记录与摘要

```text
GoldIncomeRecordBatch
    LogicTick
    Records[]
```

```text
GoldIncomeRecord
    ReceiverPlayerSlot
    Amount
    IncomeReason
    IncomeSequenceInTick
```

每 Tick 从 0 分配 `IncomeSequenceInTick`。

`SealTick(T)` 生成：

```text
GoldIncomeRecordBatch[T]
GoldIncomeBatchDigest[T]
```

摘要覆盖：

```text
LogicTick
记录数量
ReceiverPlayerSlot
Amount
IncomeReason
IncomeSequenceInTick
稳定记录顺序
```

并且：

```text
GoldIncomeBatchDigest[T]
    必须纳入 SharedGameplayChecksum(T)。
```

AuthorityFrame 不传输具体金币记录。

### AuthorityFrame 确认

FrameSync 总控先完成：

```text
AuthorityFrame 连续性检查
CanonicalCommandBytes 对账
必要的权威纠错重演
SharedGameplayChecksum 验证
```

全部通过后，才调用装备设计案冻结的正式接口：

```text
GoldIncomeRuntime.ConfirmAuthorityFrame(
    AuthorityFrame(T))
```

确认后：

```text
按记录顺序累计 ConfirmedEarnedGoldTotalByPlayer。
ConfirmedIncomeThroughTick = T。
淘汰该 Tick 未确认批次和摘要。
Dedicated Server 提交同一确认批次。
```

### 金币确认不触发主动回滚

```text
ConfirmAuthorityFrame(T)
    只确认 Tick T 的金币记录。
    不扫描后续商店 Command。
    不产生金币专用 Dirty Tick。
    不主动重演预测后缀。
```

本地 RequestCheck 因确认金币不足失败时：

```text
不生成 EquipmentShopCommand。
后续收入确认不追溯创建该 Command。
玩家需要重新发起购买请求。
```

远端实际 Command 在本地预测时因确认金币不足而失败也是允许的。等该 Command 所属 Tick 的 AuthorityFrame 到达时，通过普通 Command 与 Checksum 对账修正。

### 收入可用时机

Tick `T` 的收入：

```text
只有 AuthorityFrame(T) 被正式接受后才计入确认累计。
从 Tick T + 1 起可用于之后执行的商店逻辑。
```

已经完成的预测 Tick 不因收入确认自动重演。

服务端必须在开始 Tick `T + 1` 前确认 Tick `T` 的金币批次。

### 商店可用金币

```text
EffectiveShopGoldDelta =
    Sum(
        OperationLog 中所有
        Reverted == false 的 GoldDelta)
```

```text
CurrentAvailableGold =
    GoldIncomeRuntime
        .GetConfirmedEarnedGoldTotal(player)
    + EffectiveShopGoldDelta
```

`CurrentAvailableGold` 只读，不状态同步、不快照、不保存每 Tick 历史。

### 普通回滚

`GoldIncomeRuntime` 不进入 GameplaySnapshot。

回滚前只调用：

```text
GoldIncomeRuntime.DiscardUnconfirmedFromTick(
    ReplayFromTick)
```

它删除对应 Tick 及之后的未确认批次和摘要，保留：

```text
ConfirmedEarnedGoldTotal
ConfirmedIncomeThroughTick
```

重演时重新生成批次与摘要。

### 服务端持久化

```csharp
public interface IConfirmedGoldSettlementSink
{
    void SubmitConfirmedGoldIncome(
        in GoldIncomeRecordBatch batch);
}
```

持久化端口只接收已确认批次，用于数据库、战绩、审计和幂等重试。它不维护比赛内金币总量，不修改 GoldIncomeRuntime，也不通知商店增加余额。

---

### 九、`GoldIncomeRuntime` 与快照边界

`GoldIncomeRuntime` 是以下状态的唯一所有者：

```text
CurrentBatchBuilder
UnconfirmedBatchHistory
GoldIncomeBatchDigestHistory
InitialEarnedGoldByPlayer[]
ConfirmedEarnedGoldTotalByPlayer[]
ConfirmedIncomeThroughTick
BuildState
```

整体不进入 GameplaySnapshot。

回滚前：

```text
GoldIncomeRuntime.DiscardUnconfirmedFromTick(T)
```

删除 Tick `T` 及之后的未确认批次和摘要，保留确认累计与确认进度。

确认金币：

```text
不扫描商店 Command。
不生成 Dirty Tick。
不主动重演预测后缀。
```

商店恢复：

```text
恢复 OperationLog 和 Undo Stack。
重建 EffectiveShopGoldDelta。
读取 ConfirmedEarnedGoldTotal。
计算 CurrentAvailableGold。
```

不需要每 Tick 收入镜像或金币余额历史。

`IConfirmedGoldSettlementSink` 只接收已确认批次，不进入 GameplaySnapshot，也不维护比赛内金币总量。

---

### 定位

`GoldIncomeRuntime` 是一局比赛内所有 Gameplay 金币获取的唯一总控，存在于客户端、Dedicated Server、回放模拟端和确定性测试端。

它统一负责：

```text
接收所有金币获取请求。
生成当前 Tick 的 GoldIncomeRecord。
封闭 GoldIncomeRecordBatch[T]。
保存未确认批次。
生成 GoldIncomeBatchDigest[T]。
把摘要接入 SharedGameplayChecksum(T)。
根据已接受的 AuthorityFrame 确认对应批次。
维护 ConfirmedEarnedGoldTotal。
维护 ConfirmedIncomeThroughTick。
服务端提交确认批次到账户持久化层。
普通回滚时丢弃并重建未确认批次。
```

它不负责网络收包、全局 Command 对账、选择快照、驱动其它 Gameplay 重演、商店交易或数据库写入实现。

---

### 对外接口

所有金币来源只依赖：

```csharp
public interface IGoldIncomeRequester
{
    void RequestGoldIncome(
        PlayerSlot receiver,
        int amount,
        GoldIncomeReason reason);
}
```

商店和 UI 只依赖：

```csharp
public interface IConfirmedGoldIncomeView
{
    int GetConfirmedEarnedGoldTotal(
        PlayerSlot player);

    int ConfirmedIncomeThroughTick
    {
        get;
    }
}
```

帧同步总控只能通过以下正式接口访问金币批次和摘要：

```csharp
public bool TryGetSealedBatch(
    int logicTick,
    out GoldIncomeRecordBatch batch);

public bool TryGetBatchDigest(
    int logicTick,
    out GoldIncomeBatchDigest digest);

public void DiscardUnconfirmedFromTick(
    int replayFromTick);

public void ConfirmAcceptedTick(
    int logicTick);
```

服务端持久化端口：

```csharp
public interface IConfirmedGoldSettlementSink
{
    void SubmitConfirmedGoldIncome(
        in GoldIncomeRecordBatch batch);
}
```

帧同步总控不得绕过接口直接读取内部构建器、批次历史、摘要历史或累计金币数组。

---

### 主体结构与唯一所有权

```csharp
public sealed class GoldIncomeRuntime :
    IGoldIncomeRequester,
    IConfirmedGoldIncomeView
{
    private GoldIncomeRecordBatchBuilder
        _currentBatchBuilder;

    private GoldIncomeBatchHistory
        _unconfirmedBatchHistory;

    private GoldIncomeBatchDigestHistory
        _batchDigestHistory;

    private int[]
        _initialEarnedGoldByPlayer;

    private int[]
        _confirmedEarnedGoldTotalByPlayer;

    private int _confirmedIncomeThroughTick;
    private int _currentBuildingTick;
    private int _nextIncomeSequenceInTick;

    private GoldIncomeBuildState
        _buildState;

    private IConfirmedGoldSettlementSink
        _serverSettlementSink;
}
```

`GoldIncomeRuntime` 是以下状态的唯一所有者：

```text
CurrentBatchBuilder。
UnconfirmedBatchHistory。
GoldIncomeBatchDigestHistory。
ConfirmedEarnedGoldTotalByPlayer。
ConfirmedIncomeThroughTick。
```

帧同步总控不得维护第二份预测金币批次、确认账本、金币摘要历史或确认金币总量。

当前 Tick 必须经过：

```text
BeginTick
    -> RequestGoldIncome[0..N]
    -> SealTick
```

---

### 初始化与初始金币

初始金币不生成金币获取请求，而是初始化基线：

```csharp
public void Initialize(
    int matchStartTick,
    ReadOnlySpan<int>
        initialEarnedGoldByPlayer);
```

初始化后：

```text
ConfirmedEarnedGoldTotalByPlayer[player]
    =
    InitialEarnedGoldByPlayer[player]。

ConfirmedIncomeThroughTick
    =
    MatchStartTick - 1。
```

初始金币不生成 `GoldIncomeRecord`，不进入批次，不等待 AuthorityFrame，也不重复提交账户奖励。

比赛内累计获得金币的唯一 Gameplay 权威由 `GoldIncomeRuntime` 持有。

---

### 所有金币来源必须请求总控

所有 Gameplay 金币只能通过：

```csharp
IGoldIncomeRequester.RequestGoldIncome(
    PlayerSlot receiver,
    int amount,
    GoldIncomeReason reason);
```

调用方只传 `Receiver / Amount / Reason`，不传 Tick、帧内序号、BatchId、确认标记或累计金币。

职责划分：

```text
NaturalGoldIncomeSystem
    负责自然金币请求。

CombatSystem
    只结算战斗并产出 FormalDeathResults
    与其它正式战斗结果。

MatchStatisticsRuntime
    消费 FormalDeathResults，
    生成稳定 GoldIncomeAllocations。

CombatGoldIncomeProducer
    按 GoldIncomeAllocations 数组顺序
    调用 RequestGoldIncome。

MapGoldIncomeProducer
    负责地图目标金币请求。

MatchRuleGoldIncomeProducer
    负责比赛规则金币请求。
```

禁止其它系统直接修改累计金币、创建金币记录、封闭批次、写批次/摘要历史或提交账户持久化。

---

### 金币记录与稳定请求顺序

```csharp
public struct GoldIncomeRecord
{
    public PlayerSlot Receiver;
    public int Amount;
    public GoldIncomeReason Reason;
    public int IncomeSequenceInTick;
}
```

每 Tick 开始：

```text
NextIncomeSequenceInTick = 0。
```

每次合法请求：

```text
IncomeSequenceInTick =
    NextIncomeSequenceInTick++。
```

Tick `T` 的请求顺序正式冻结：

```text
A. GoldIncomeRuntime.BeginTick(T)。

B. NaturalGoldIncomeSystem：
       按 PlayerSlot 升序请求自然金币。

C. CombatSystem.SettleTick：
       产出 FormalDeathResults
       和其它正式战斗结果。

D. MatchStatisticsRuntime：
       消费 FormalDeathResults，
       生成 GoldIncomeAllocations。

E. CombatGoldIncomeProducer：
       按 GoldIncomeAllocations 数组稳定顺序
       请求补刀、击杀、助攻等金币。

F. Map / MatchRule Gold Producers：
       按代码固定生产者顺序执行，
       各生产者内部使用稳定顺序。

G. GoldIncomeRuntime.SealTick(T)。
```

`IncomeSequenceInTick` 禁止依赖组件注册顺序、`Dictionary/HashSet` 枚举顺序、ScriptableObject 资源枚举顺序、Unity Object 创建顺序或非稳定事件订阅顺序。

同类记录不自动合并。

---

### BeginTick、Request 与 Seal

```csharp
public void BeginTick();
```

读取 `SimulationTickContext.Current.Tick`，清空当前构建器，序号归零并进入 `AcceptingRequests`。

```csharp
public void RequestGoldIncome(
    PlayerSlot receiver,
    int amount,
    GoldIncomeReason reason);
```

要求：

```text
BuildState == AcceptingRequests。
PlayerSlot 合法。
Amount > 0。
Reason 合法。
```

成功后自动创建记录并分配帧内序号。非法请求属于程序错误。

```csharp
public GoldIncomeRecordBatch SealTick();
```

`SealTick` 在本 Tick 所有金币来源完成后执行，生成并保存：

```text
GoldIncomeRecordBatch[T]。
GoldIncomeBatchDigest[T]。
```

Seal 后禁止继续提交本 Tick 金币请求。

---

### 批次、摘要与正式查询

```csharp
public struct GoldIncomeRecordBatch
{
    public int LogicTick;

    public GoldIncomeRecord[]
        Records;
}
```

```csharp
public readonly struct GoldIncomeBatchDigest
{
    public readonly ulong Value;
}
```

```csharp
public bool TryGetSealedBatch(
    int logicTick,
    out GoldIncomeRecordBatch batch);

public bool TryGetBatchDigest(
    int logicTick,
    out GoldIncomeBatchDigest digest);
```

查询只返回已经 Seal 的 Tick，不创建批次、不触发确认、不修改累计金币，也不暴露内部可变集合。

摘要必须覆盖 LogicTick、记录数量、Receiver、Amount、Reason、IncomeSequenceInTick 和稳定记录顺序。

```text
GoldIncomeRecordBatch[T]
    -> 规范序列化
    -> GoldIncomeBatchDigest[T]
    -> SharedGameplayChecksum(T)。
```

`AuthorityFrame.SharedGameplayChecksum` 必填。

本地完整 Checksum 历史由帧同步层持有；`GoldIncomeRuntime` 只提供金币摘要，不持有 `LocalFrameVerificationRecordByTick`。

---

### 已接受 Tick 的金币确认

帧同步总控先完成 AuthorityFrame 连续性检查、`CanonicalCommandBytes` 对账、必要的 Gameplay 回滚与重演，以及 `SharedGameplayChecksum` 校验。

Tick `T` 被正式接受后调用：

```csharp
public void ConfirmAcceptedTick(
    int logicTick);
```

内部要求：

```text
logicTick
    ==
ConfirmedIncomeThroughTick + 1。

GoldIncomeRecordBatch[logicTick]
    已经 Seal。

GoldIncomeBatchDigest[logicTick]
    已经存在。
```

然后按记录顺序累计 `ConfirmedEarnedGoldTotalByPlayer`，推进 `ConfirmedIncomeThroughTick`，淘汰对应未确认批次/摘要，并在服务端提交持久化。

`GoldIncomeRuntime` 不接收 AuthorityFrame、CanonicalCommandBytes、GameplaySnapshot 或完整 SharedGameplayChecksum 记录。

---

### 客户端与服务端统一确认

客户端：

```text
收到 AuthorityFrame(T)。
帧同步总控完成对账、必要重演和 Checksum 验证。
帧同步总控正式接受 Tick T。
GoldIncomeRuntime.ConfirmAcceptedTick(T)。
```

服务端：

```text
完成 Tick T。
GoldIncomeRuntime.SealTick(T)。
构建 AuthorityFrame(T)。
完成服务端 SharedGameplayChecksum。
服务端正式接受 Tick T。
GoldIncomeRuntime.ConfirmAcceptedTick(T)。
开始 Tick T + 1。
```

客户端与服务端使用同一个金币确认入口。

---

### 收入可用时机

Tick `T` 产生的金币在对应 AuthorityFrame 被接受并确认后：

```text
从 Tick T + 1 起可用于商店。
```

Tick `T` 内不能消费本 Tick 新生成的金币。

服务端必须在开始 Tick `T + 1` 前确认 Tick `T` 的金币批次。

---

### 累计金币与账户职责移交

统一查询：

```csharp
public int GetConfirmedEarnedGoldTotal(
    PlayerSlot player);
```

`GoldIncomeRuntime` 统一保存：

```text
InitialEarnedGoldByPlayer。
ConfirmedEarnedGoldTotalByPlayer。
ConfirmedIncomeThroughTick。
```

删除其它 Gameplay Runtime 中独立维护的累计金币总量与确认进度。

服务端账户或战绩系统只持久化已确认批次，不再成为比赛内第二份金币权威。

---

### 服务端账户持久化

服务端确认批次后调用：

```csharp
IConfirmedGoldSettlementSink
    .SubmitConfirmedGoldIncome(batch);
```

持久化层负责数据库、战绩、审计日志、幂等和失败重试。

它不重新计算奖励、不决定批次是否确认、不修改比赛内累计金币，也不通知商店增加余额。

---

### 出售金币不进入 GoldIncomeRuntime

装备出售由：

```text
ShopOperationRecord.GoldDelta > 0。
```

表达。撤销出售由：

```text
OriginalRecord.Reverted = true。
```

表达。

出售不调用 `RequestGoldIncome`。`GoldIncomeRuntime` 只处理 Gameplay 奖励金币，商店 `OperationLog` 独立重建可逆交易变化。

---

### 普通回滚

`GoldIncomeRuntime` 不进入 `GameplaySnapshot`。

普通回滚必须满足：

```text
ReplayFromTick
    >=
LatestAuthorityFrameTick + 1。
```

因此整个可回滚预测区间使用固定的 `ConfirmedEarnedGoldTotal / ConfirmedIncomeThroughTick` 基线。

回滚前：

```csharp
GoldIncomeRuntime.DiscardUnconfirmedFromTick(
    replayFromTick);
```

删除 `replayFromTick` 及之后的未确认批次和摘要，清空受影响构建器，但保留确认金币总量与确认进度。

随后恢复 GameplaySnapshot，并由各金币来源重新请求、重新生成批次和摘要。

不需要确认金币逐 Tick 镜像、确认金币历史快照、按重演 Tick 查询历史确认金币或金币确认后缀回滚。

---

### 金币确认不主动触发商店重演

```csharp
GoldIncomeRuntime.ConfirmAcceptedTick(T);
```

只确认 Tick `T` 的金币批次、累计确认金币、推进确认进度并提交服务端持久化。

它不扫描后续商店 Command，不生成 GoldDirtyTick，不选择 GameplaySnapshot，不主动触发预测后缀重演，也不自动补买。

本地玩家当时金币不足时：

```text
RequestCheck 失败。
没有 Purchase Command。
金币后来确认后需要玩家重新点击购买。
```

远端商店 Command 在本地预测时若因确认金币不足而暂时失败，不在金币确认时主动修正。等待该 Command 所属 Tick 的 AuthorityFrame：

```text
Checksum 一致
    -> 接受当前结果。

Checksum 不一致
    -> 走正常 AuthorityFrame
       回滚与重演流程。
```

`Purchase` 与 `Undo` 仍属于金币敏感命令，但该分类不用于确认金币后的历史扫描。

---

### AuthorityRecovery

当前 `AuthorityRecovery` 只补发缺失 AuthorityFrame。

补齐后，总控按 Tick 接受帧，必要时重演，并逐帧调用：

```text
GoldIncomeRuntime.ConfirmAcceptedTick。
```

当前版本不提供金币 Seed、累计金币镜像包、BaseSnapshot、中途加入或客户端进程重启恢复。

---

### 生命周期约束

```text
BeginTick(T)
    早于本 Tick 所有 RequestGoldIncome。

SealTick(T)
    晚于本 Tick所有金币来源，
    早于 SharedGameplayChecksum(T)。

ConfirmAcceptedTick(T)
    晚于总控接受 AuthorityFrame(T)，
    且严格按连续 Tick 调用。
```

开发环境对重复 Begin、Seal 后请求、跳 Tick Confirm 或缺失批次直接报错。

---


## 需求演进

### 2026-10-02

变动内容：金币总控唯一拥有 builder、未确认批次、摘要和确认累计。

legacyDecision：D-005

### 2026-10-02

变动内容：确认金币不主动重演后续商店命令或补建本地已拒绝命令。

legacyDecision：D-006

### 2026-10-02

变动内容：可用金币为确认累计加有效商店增量，只读派生且不保存快照。

legacyDecision：D-007

### 2026-08-11

变动内容：击杀金币整数分配和测试金币；生产者边界冲突留在待确认清单。

legacyDecision：D-041

