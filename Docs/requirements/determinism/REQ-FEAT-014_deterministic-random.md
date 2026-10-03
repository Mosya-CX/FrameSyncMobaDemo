# 确定性随机与定点计算

## 目标实现

相同种子、状态、请求得到可重演的随机与数值。

## 技术方案

唯一随机服务维护显式 Snapshot 状态，集合采样定义稳定顺序；权威类型为 Unity.Mathematics.FixedPoint.fp，作者 float 仅在验证/Bake 边界转换一次。

## 边界情况

不新增定点类型；动作暴击使用动作键纯哈希，不消耗此共享随机流；禁止 UnityEngine.Random。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Deterministic/Random/DeterministicRandomService.cs`：当前关联实现定义 DeterministicRandomService（以源码为实际命名）。
- `Assets/Scripts/Deterministic/Random/DeterministicRandomSnapshot.cs`：当前关联实现定义 DeterministicRandomSnapshot（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Deterministic/Tests/DeterministicRandomServiceTests.cs`：SameSeedAndCalls_ProduceIdenticalSequence、SameSeedAndMixedPrimitiveCalls_ProduceIdenticalResults、CaptureRestore_ReplaysSubsequentSequence、CaptureRestore_ReplaysMixedPrimitiveSequence、ZeroSeed_IsRejected、ZeroRestoredState_IsRejectedWithoutChangingSequence、NextFp01_UsesOneUnsignedFractionalDrawWithinUnitRange。
- `Assets/Scripts/Gameplay/Tests/CombatEnhancementTests.cs`：Crit_100PercentChance_DoublesDamage、Crit_ZeroPercentChance_NoCrit、Crit_DamageEventData_IsCriticalFlag、Crit_OneHundredPercent_DoesNotRequireRandomService、ProbabilisticCrit_DoesNotConsumeGlobalRandomState、AttackSpeed_ModifiesCooldown、AttackSpeed_PositiveBase_StartsAttack。
- `Assets/Scripts/Bootstrap/Tests/EditMode/BootstrapPayloadWireCodecTests.cs`：Payload_RoundTrip_IsCanonical、Payload_TrailingBytes_AreRejected。
- `Assets/Scripts/Bootstrap/Tests/EditMode/GameplayIntegrationTests.cs`：TickContext_InitializesWithCorrectTick、TickContext_AdvancesCorrectly、DeterministicRandom_ProducesSameSequenceForSameSeed、UnitUid_ComparisonAndSorting、PathGrid_Initialise_ProducesValidGrid、AStar_FindPath_ReturnsValidPath、FlowField_BuildAndQuery_ReturnsValidDirection。
- `Assets/Scripts/Deterministic/Tests/DeterministicAssemblyBoundaryTests.cs`：RuntimeAssembly_HasNoForbiddenDirectDependency。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 单一逻辑随机源

当前版本只保留一个：

```text
DeterministicRandomService
```

不划分 World、Combat、Spawner、AI、Loot、Visual 等随机流。

纯视觉随机可以使用 Unity 随机，因为它不影响 Gameplay；需要跨客户端视觉近似一致时，可使用 `VisualEventId` 派生表现种子，但不消耗 Gameplay 随机状态。

### 状态与快照

```text
DeterministicRandomSnapshot
    State
    CallCount optional
```

状态进入 `GameplaySnapshot`。

回滚恢复随机状态后，重演必须产生相同随机结果。

### 常用函数

```text
NextUInt()
NextInt()
NextInt(minInclusive, maxExclusive)
NextFp01()
NextFp(minInclusive, maxExclusive)
NextBool()
Chance01(probabilityFp)
ChancePercent(percentFp)
PickIndex(count)
PickOne(readOnlyList)
ShuffleInPlace(list)
RandomDirection2D()
RandomPointInsideCircle(radius)
RandomPointOnCircle(radius)
```

Gameplay 2D 逻辑不提供 `Direction3D` 作为核心函数；表现层自行转换。

### 核心伪代码

思路：所有高级随机函数最终只消耗稳定数量的基础随机值；列表遍历和 Shuffle 必须使用稳定顺序容器。

```pseudo
function NextInt(minInclusive, maxExclusive):
    range = maxExclusive - minInclusive
    value = NextUInt()
    return minInclusive + value mod range

function Chance01(probability):
    return NextFp01() < Clamp01(probability)

function ShuffleInPlace(list):
    for i from list.Count - 1 down to 1:
        j = NextInt(0, i + 1)
        swap list[i], list[j]
```

---

### 十一、`DeterministicRandomSnapshot`

```text
State
CallCount optional
```

恢复后必须产生相同随机序列。

---


## 需求演进

### 2026-10-02

变动内容：作者 float 仅在 Bake/初始化边界转正式 fp，Tick 内不回转作为权威。

legacyDecision：D-022

