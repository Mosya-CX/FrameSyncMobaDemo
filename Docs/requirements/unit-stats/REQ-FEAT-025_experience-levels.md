# 经验成长与技能点

## 目标实现

正式奖励立即发放经验，升级影响成长并产生待分配技能点。

## 技术方案

ExperienceSettlement 分发确定性经验；StatHandler 按配置成长，AbilityHandler 通过 PendingSkillPoints 和类型化技能升级请求更新槽位。

## 边界情况

等级上限、无效技能槽、技能已满和资源不足明确拒绝；技能级成长与 Stage 级配置不混用。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/XP/LevelExperienceConfig.cs`：当前关联实现定义 LevelExperienceConfig、LevelUpCurrentValueRule（以源码为实际命名）。
- `Assets/Scripts/Gameplay/XP/XpRewardTable.cs`：当前关联实现定义 XpRewardTable（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/FrameSync/Tests/MatchRewardDistanceTests.cs`：MinionExperienceRadius_UsesStatToLogicDistanceScale。
- `Assets/Scripts/Gameplay/Tests/SkillPointAllocationTests.cs`：NormalSlot_AllocatesUpToMaxAndStops、Ultimate_RequiresLevelSixAndMaxThreeRanks、Ultimate_UnlocksAtLevelSix、GrantExperience_GrantsExactlyOneSkillPointPerLevel。
- `Assets/Scripts/Gameplay/Tests/UnitRuntimeCatalogAssetTests.cs`：Bake_ConvertsFloatAuthoringToExistingRuntimeContracts、Bake_ReversedAuthoringOrderProducesSameStableTables、Bake_DuplicateStatDefinitionFailsInsteadOfOverwriting、Bake_DuplicatePrototypePresetStatFails、SpawnUnit_UsesBakedAbilityMovementAndPhysicsProfiles。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 等级、经验与属性成长

#### 静态配置

```csharp
[Serializable]
public sealed class LevelExperienceConfig
{
    public bool CanLevelUp;

    [Min(1)]
    public ushort InitialLevel = 1;

    [Min(1)]
    public ushort MaxLevel = 1;

    [Min(0)]
    public int InitialExperience;

    public List<int> RequiredExperiencePerLevel;

    public LevelUpCurrentValueRule HealthOnLevelUp;
    public LevelUpCurrentValueRule CastResourceOnLevelUp;
}
```

```csharp
public enum LevelUpCurrentValueRule : byte
{
    KeepCurrent,
    AddMaximumDelta,
    PreserveRatio,
    Refill,
}
```

普通小兵、建筑和普通野怪通常不可升级。  
英雄或特殊成长单位配置完整经验表。

#### 运行时状态

```csharp
public ushort Level { get; private set; }
public int CurrentExperience { get; private set; }
public ushort MaxLevel { get; private set; }

public bool CanLevelUp { get; }
public int ExperienceRequiredForNextLevel { get; }
```

经验入口：

```csharp
public ExperienceGainResult AddExperience(
    int amount);
```

它只应用外部系统已经确定的经验量，不负责击杀奖励计算与分配。

#### 升级流程

```text
CurrentExperience += amount
    ↓
达到本级需求
    ↓
记录升级前 MaxHealth / MaxCastResource
    ↓
扣除本级经验
    ↓
Level += 1
    ↓
成长属性 Dirty
    ↓
重新读取新的最大值
    ↓
按 LevelUpCurrentValueRule 调整当前值
    ↓
发布一次 LevelUp
    ↓
继续检查是否还能升级
```

连续升级逐级发布 `LevelUp`。

普通 Modifier 导致最大值变化时：

```text
最大值降低：
    当前值高于新最大值时向下 Clamp。

最大值提高：
    默认保持当前值不变。
```

不会隐式当成治疗。

#### 生命周期

```text
英雄 Dead -> Respawning -> Alive：
    默认保留 Level 和 CurrentExperience。

对象池单位以新 UnitUid 生成：
    重新应用 InitialLevel 和 InitialExperience。

整局重开或明确玩法重置：
    才重新读取完整初始等级配置。
```

---


## 需求演进

### 2026-10-02

变动内容：全模拟端消费正式死亡统计，不只服务器。

legacyDecision：D-013

