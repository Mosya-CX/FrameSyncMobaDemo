# 战斗 Modifier 集合

## 本次执行范围

本计划对应原编码 0021 的一次执行：战斗 Modifier 集合。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [战斗请求封存与因果波次](../../requirements/combat/REQ-FEAT-029_combat-causal-waves.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [伤害配方抗性与吸血](../../requirements/combat/REQ-FEAT-030_damage-resistance.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [濒死批次与公平击杀归属](../../requirements/combat/REQ-FEAT-034_death-attribution.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [死亡奖励与贡献窗口](../../requirements/combat/REQ-FEAT-035_death-rewards.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

收集强类型 Shield、Damage、Heal 请求；封存成因果波次后才分配最终 SequenceInTick；同目标基于批次开始时冻结状态结算。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Deterministic/Serialization/DeterministicHash32.cs`：`DeterministicHash32`。
- `Assets/Scripts/Gameplay/Modifiers/CombatModifierSet.cs`：`CombatModifierSet`、`RecordComparer`、`CombatFormulaAccumulator`、`CombatPolicyResolution`。
- `Assets/Scripts/Gameplay/Modifiers/CombatModifierHandle.cs`：`CombatModifierHandle`。
- `Assets/Scripts/Gameplay/Modifiers/CombatModifierId.cs`：`CombatModifierId`。
- `Assets/Scripts/Gameplay/Modifiers/CombatModifierRecord.cs`：`CombatModifierRecord`、`CombatModifierMatch`、`CombatFormulaPatch`、`CombatOperand`、`CombatOperandTerm`、`CombatValueRef`、`CombatPolicyPatch`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Deterministic/Serialization/DeterministicHash32.cs`：

```csharp
using System.Text;

namespace FrameSyncMoba.Deterministic
{
    public static class DeterministicHash32
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static uint Utf8(string key)
        {
            if (key == null)
            {
                return 0u;
            }

            uint hash = FnvOffsetBasis;
            byte[] bytes = Encoding.UTF8.GetBytes(key);
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= FnvPrime;
            }

            return hash;
        }

        public static uint Compute(byte[] data, int offset, int count)
        {
            uint hash = FnvOffsetBasis;
            int end = offset + count;
            for (int i = offset; i < end; i++)
            {
                hash ^= data[i];
                hash *= FnvPrime;
            }

            return hash;
        }
    }
}
```

`Assets/Scripts/Gameplay/Modifiers/CombatModifierSet.cs`：

```csharp
        public CombatModifierHandle Attach(CombatModifierRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            ValidateRecord(record);
            if (idToIndex.ContainsKey(record.Id))
            {
                throw new DeterministicSimulationException(
                    $"Duplicate CombatModifierRecord.Id {record.Id} on Unit {owner.UnitUid}.");
            }

            CombatModifierRecord stored = CloneRecord(record);
            int insertIndex = records.BinarySearch(
                stored,
                RecordComparer.Instance);
            if (insertIndex < 0)
                insertIndex = ~insertIndex;
            records.Insert(insertIndex, stored);
            RebuildIndicesFrom(insertIndex);

            return new CombatModifierHandle(owner.UnitUid, record.Id);
        }
```

### 输入输出与边界

**战斗请求封存与因果波次**

收集强类型 Shield、Damage、Heal 请求；封存成因果波次后才分配最终 SequenceInTick；同目标基于批次开始时冻结状态结算。

同批治疗封顶、盾参与吸收、总生命伤害一次提交；由结果产生的新反应进下一波；死亡反应产生的普通请求进下一 Tick。

**伤害配方抗性与吸血**

DamageRecipe/FormulaTerm 生成伤害，固定槽位 Modifier 合并后进入暴击、抗性、盾、生命、偷取及反应；超额伤害按定点权重分摊 ActualLifeDamage。

免疫、零伤害、纯盾伤害和纯过量不计击杀优势；非法请求仍报错；结构拒绝政策合法拒绝是成功空操作。

**濒死批次与公平击杀归属**

Combat 同步请求 UnitWorld 更新 Dying/Dead；致死批次按有效敌方英雄 ActualLifeDamage 总和取最大，纯中性分数处理最高伤害并列。

旧末次伤害者方案已被修订；队伍、Prefab、提交序列不决定平局；FormalDeathResult 唯一输出给统计和奖励。

**死亡奖励与贡献窗口**

DeathRewardContext 从正式死亡与贡献日志选收受者，整数稳定分配；经验立即结算，金币统一 RequestGoldIncome，批次摘要参与共享校验。

D-041 的生产者归属与既有复仇/击杀统计日志描述存在冲突，未确认部分不能静默改写；助攻窗口不受杀手修订而丢失。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Deterministic/Tests/DeterministicHash32Tests.cs`：EditMode，程序集 `FrameSyncMoba.Deterministic.Tests`，函数 `SameString_SameHash`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SameString_SameHash()
        {
            uint h1 = DeterministicHash32.Utf8("Ability.AatroxE.PassiveOmnivamp");
            uint h2 = DeterministicHash32.Utf8("Ability.AatroxE.PassiveOmnivamp");
            Assert.AreEqual(h1, h2);
        }
```
- `Assets/Scripts/Gameplay/Tests/CombatModifierSetTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `Attach_ReturnsValidHandle`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void Attach_ReturnsValidHandle()
        {
            var unit = CreateUnit();
            var set = new CombatModifierSet(unit);
            var record = CreateRecord(1000, "Buff.Berserk.DamageReduction");

            CombatModifierHandle handle = set.Attach(record);

            Assert.IsTrue(handle.IsValid);
            Assert.AreEqual(unit.UnitUid, handle.OwnerUnitUid);
            Assert.AreEqual(record.Id, handle.ModifierId);
            Assert.AreEqual(1, set.Count);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
