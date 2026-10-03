# 技能创作与烘焙创作管线

## 本次执行范围

本计划对应原编码 0054 的一次执行：技能创作与烘焙创作管线。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [技能信号与会话状态](../../requirements/abilities/REQ-FEAT-042_ability-signal-session.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [施法模型与阶段推进](../../requirements/abilities/REQ-FEAT-043_cast-stages.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [阶段效果与确定性黑板](../../requirements/abilities/REQ-FEAT-044_stage-effects.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能目录消耗冷却与升级](../../requirements/abilities/REQ-FEAT-045_ability-catalog.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [主动附带被动与固定被动](../../requirements/abilities/REQ-FEAT-046_ability-passives.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

AbilityHandler 接受 AbilitySignal，AbilityRuntime 常驻，AbilitySession 只承载本次施法；Session 结束回传执行器，外部只读 AbilityCastView。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Ability/AbilityAsset.cs`：`AbilityAsset`、`value`、`CastModelAuthoring`、`CommitCastModelAuthoring`、`HoldReleaseCastModelAuthoring`、`ChannelCastModelAuthoring`、`ActiveSignalCastModelAuthoring`。
- `Assets/Scripts/RuntimeConfig/Editor/AbilityAssetBakeValidator.cs`：`AbilityAssetBakeValidator`、`ValidationResult`。
- `Assets/Scripts/Gameplay/Ability/AbilityDefinitionRegistry.cs`：`AbilityDefinitionRegistry`。
- `Assets/Scripts/RuntimeConfig/Editor/AbilityRegistryPopulator.cs`：`AbilityRegistryPopulator`、`AbilityAssetPostprocessor`。
- `Assets/Scripts/Gameplay/Ability/StageDef.cs`：`StageDef`、`StageResult`。
- `Assets/Scripts/Gameplay/Ability/AbilityDef.cs`：`AbilityDef`、`AbilityLevelValue`、`AbilityCostTiming`、`AbilityCostPlan`、`AbilityCastContext`、`AbilityCastConditionDef`。
- `Assets/Scripts/Gameplay/Ability/CastModelDef.cs`：`CastModelKind`、`CastStage`、`CastModelDef`、`CommitCastModelDef`、`HoldReleaseCastModelDef`、`HoldTimeoutPolicy`、`ChannelCastModelDef`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Ability/AbilityAsset.cs`：

```csharp
        public AbilityDef Bake(int tickRate = 30)
        {
            DeterministicTimeConversion.ValidateSupportedTickRate(
                tickRate);
            if (abilityId <= 0)
                throw new InvalidOperationException(
                    $"AbilityAsset '{name}' has invalid AbilityId {abilityId}.");
            if (string.IsNullOrWhiteSpace(abilityName))
                throw new InvalidOperationException(
                    $"AbilityAsset '{name}' requires a name.");
            if (float.IsNaN(castRange) ||
                float.IsInfinity(castRange) ||
                castRange < 0f)
            {
                throw new InvalidOperationException(
                    $"AbilityAsset '{name}' cast range must be finite and nonnegative.");
            }
            if (!Enum.IsDefined(typeof(AimKind), aimKind) ||
                !Enum.IsDefined(
                    typeof(AbilityCostTiming),
                    costTiming))
            {
                throw new InvalidOperationException(
                    $"AbilityAsset '{name}' contains an undefined enum value.");
            }
            ValidateStageAuthoring(stageDefs, tickRate);

            var def = new AbilityDef
            {
                AbilityId = abilityId,
                Name = abilityName,
                IconAddress = iconAddress,
                IsUltimate = isUltimate,
                CooldownByLevel = BakeCooldownLevelValues(
                    cooldownMillisecondsByLevel,
                    cooldownTicksByLevel,
                    tickRate),
                AimKind = aimKind,
                CastRange = (Unity.Mathematics.FixedPoint.fp)castRange,
                CastModel = castModel?.Bake(
                    stageDefs,
                    tickRate),
                CostPlan = new AbilityCostPlan(
                    BakeLevelValues(
                        castResourceCostByLevel,
                        nameof(castResourceCostByLevel)),
                    BakeLevelValues(
                        healthCostByLevel,
                        nameof(healthCostByLevel)),
                    costTiming),
                CastConditions = BakeConditions(castConditions),
                PassiveEffect = passiveEffect?.Bake(),
            };

            if (!def.IsValid)
                throw new InvalidOperationException(
                    $"AbilityAsset '{name}' baked an invalid definition.");
            return def;
        }
```

`Assets/Scripts/RuntimeConfig/Editor/AbilityAssetBakeValidator.cs`：

```csharp
        public static ValidationResult Validate(AbilityAsset asset)
        {
            if (asset == null)
                return ValidationResult.Failure("AbilityAsset is null.");

            var errors = new List<string>();

            if (asset.AbilityId <= 0)
                errors.Add(string.Format("AbilityAsset '{0}': AbilityId must be positive.", asset.name));

            if (string.IsNullOrWhiteSpace(asset.AbilityName))
                errors.Add(string.Format("AbilityAsset '{0}': AbilityName is required.", asset.name));

            var castModel = asset.CastModel;
            if (castModel == null)
            {
                errors.Add(string.Format("AbilityAsset '{0}': CastModel is required.", asset.name));
            }
            else
            {
                ValidateCastModel(asset, castModel, errors);
            }

            ValidateAimKindConsistency(asset, castModel, errors);
            ValidateStages(asset, errors);
            ValidatePassiveEffect(asset, errors);

            ValidateLevelValues(
                asset,
                asset.CastResourceCostByLevel,
                "CastResourceCost",
                errors);
            ValidateLevelValues(
                asset,
                asset.HealthCostByLevel,
                "HealthCost",
                errors);

            if (errors.Count == 0)
                return ValidationResult.Success;

            return ValidationResult.Failure(errors.ToArray());
        }
```

### 输入输出与边界

**技能信号与会话状态**

AbilityHandler 接受 AbilitySignal，AbilityRuntime 常驻，AbilitySession 只承载本次施法；Session 结束回传执行器，外部只读 AbilityCastView。

HandleSignal 返回是否接受，不让 Planner 私自推进 Session；同 Tick Focus 和 Commit 需正式 CommandSeq 顺序。

**施法模型与阶段推进**

CastModelDef 决定 CastStageKey、阶段进入/退出及 Timeout，StageDef 独立返回完成或失败；每阶段明确时长，0 Tick 阶段有限推进。

不重建已删除 CastFlowDef、StageDriver；切换不必触发主动施法事件；蓄力 timeout 的自动释放或取消及退款由模型明确。

**阶段效果与确定性黑板**

StageDef 用 AbilityStageContext、AbilityPorts 和受限 AbilityBlackboard；技能范围约束按需启用，效果写入所属系统，不增加 EffectPlan/EffectStep。

Handle 由创建 Effect 自己清理；Stage 成长不放在 AbilityDef 全局重复字段；结构过滤配置之外仍保留中央准入。

**技能目录消耗冷却与升级**

AbilityBook 登记槽位；AbilityDef 提供条件、CostPlan 与按等级默认冷却；AbilityRankUpEffectDef 管理升级瞬时效果。

资源不足、技能满级、无技能点拒绝；特殊模型冷却不回写统一默认字段；连续再施法 UI 只投影当前可用段。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/RuntimeConfig/Editor/Tests/AbilityBakeTests.cs`：EditMode，程序集 `FrameSyncMoba.RuntimeConfig.Editor.Tests`，函数 `Bake_CommitModel_ProducesValidAbilityDef`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void Bake_CommitModel_ProducesValidAbilityDef()
        {
            var asset = CreateValidCommitAsset();
            var def = asset.Bake();

            Assert.That(def, Is.Not.Null);
            Assert.That(def.AbilityId, Is.GreaterThan(0));
            Assert.That(def.CastModel, Is.Not.Null);
            Assert.That(def.CastModel.Kind, Is.EqualTo(CastModelKind.Commit));
            Assert.That(def.IsValid, Is.True);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
