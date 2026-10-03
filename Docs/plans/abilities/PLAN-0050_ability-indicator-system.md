# 技能指示器基础

## 本次执行范围

本计划对应原编码 0050 的一次执行：技能指示器基础。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [技能信号与会话状态](../../requirements/abilities/REQ-FEAT-042_ability-signal-session.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [施法模型与阶段推进](../../requirements/abilities/REQ-FEAT-043_cast-stages.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [阶段效果与确定性黑板](../../requirements/abilities/REQ-FEAT-044_stage-effects.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能目录消耗冷却与升级](../../requirements/abilities/REQ-FEAT-045_ability-catalog.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [主动附带被动与固定被动](../../requirements/abilities/REQ-FEAT-046_ability-passives.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能指示器与本地生命周期](../../requirements/player-input/REQ-FEAT-081_skill-indicator.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

AbilityHandler 接受 AbilitySignal，AbilityRuntime 常驻，AbilitySession 只承载本次施法；Session 结束回传执行器，外部只读 AbilityCastView。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/PlayerInput/SkillIndicatorDriver.cs`：`SkillIndicatorDriver`。
- `Assets/Scripts/PlayerInput/PlayerInputController.cs`：`PlayerInputController`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/PlayerInput/PlayerCommandRequester.cs`：`IPlayerGameplayCommandRequester`、`IPlayerShopCommandRequester`、`IPlayerAbilityInputProfileProvider`、`IPlayerAbilityAimProfileProvider`、`ILocalAbilityRuntimeView`、`GameplayCommandRequestReceipt`、`LocalAbilityInputStateKind`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/PlayerInput/SkillIndicatorDriver.cs`：

```csharp
        public void Hide()
        {
            HideAllInstances();
            _visible = false;
            _activeKind = AimKind.None;
            _showRangeCircle = false;
            _activeDirectionalZone = null;
            UnityEngine.Debug.Log(
                "[Indicator] Hide");
        }
```

`Assets/Scripts/PlayerInput/PlayerInputController.cs`：

```csharp
        private void Update()
        {
            if (buffer == null || commandRequester == null)
            {
                HideIndicator();
                return;
            }
            if (pointerResolver != null && pointerPosition != null)
            {
                pointerResolver.LastScreenPosition =
                    pointerPosition.ReadValue<Vector2>();
            }
            commandRequester.ProcessFrame(buffer, pointerResolver);
            UpdateIndicator();
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

- `Assets/Scripts/PlayerInput/Tests/PlayerCommandRequesterTests.cs`：EditMode，程序集 `FrameSyncMoba.PlayerInput.Tests`，函数 `EventBuffer_AssignsStableSequenceAndRejectsOverflow`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void EventBuffer_AssignsStableSequenceAndRejectsOverflow()
        {
            var buffer = new LocalInputEventBuffer();
            for (int i = 0; i < LocalInputEventBuffer.MaxLocalInputEventsPerUnityFrame; i++)
            {
                Assert.IsTrue(buffer.Push(
                    LocalGameplayInputEventKind.AbilityKeyPressed,
                    (byte)(i % 4),
                    new Vector2(i, i)));
            }
            Assert.IsFalse(buffer.Push(
                LocalGameplayInputEventKind.PrimaryClick, 0, Vector2.zero));

            ulong previous = 0;
            while (buffer.TryDequeue(out LocalGameplayInputEvent inputEvent))
            {
                Assert.Greater(inputEvent.LocalEventSequence, previous);
                previous = inputEvent.LocalEventSequence;
            }
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
