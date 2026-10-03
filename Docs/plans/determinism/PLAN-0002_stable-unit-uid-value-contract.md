# 稳定单位 UID 值合同

## 本次执行范围

本计划对应原编码 0002 的一次执行：稳定单位 UID 值合同。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [稳定 UID 与参与者身份](../../requirements/determinism/REQ-FEAT-015_stable-unit-identity.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UnitUid、ProjectileUid 使用生成 Tick 与归属系统序列；GameplayParticipantId 从稳定出生来源建立，OriginActionId 由参与者、来源类型/ID、逻辑 Tick 和本地动作序列组成。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/UnitUid.cs`：`UnitUid`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Unit/Core/UnitUid.cs`：

```csharp
        public int CompareTo(UnitUid other)
        {
            int tickComparison = SpawnLogicTick.CompareTo(other.SpawnLogicTick);
            if (tickComparison != 0)
            {
                return tickComparison;
            }

            int prefabComparison = RuntimeEntityPrefabId.CompareTo(other.RuntimeEntityPrefabId);
            if (prefabComparison != 0)
            {
                return prefabComparison;
            }

            return SpawnSequenceInTick.CompareTo(other.SpawnSequenceInTick);
        }
```

### 输入输出与边界

**稳定 UID 与参与者身份**

UnitUid、ProjectileUid 使用生成 Tick 与归属系统序列；GameplayParticipantId 从稳定出生来源建立，OriginActionId 由参与者、来源类型/ID、逻辑 Tick 和本地动作序列组成。

参与者缺失或重复可见失败；不得用 PrefabId、对象注册顺序、实例 ID 或队伍侧生成中性随机身份。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/UnitUidTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `SameComponents_ProduceEqualIdentity`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SameComponents_ProduceEqualIdentity()
        {
            var first = new UnitUid(1200, 1001, 7);
            var second = new UnitUid(1200, 1001, 7);

            Assert.That(first.SpawnLogicTick, Is.EqualTo(1200));
            Assert.That(first.RuntimeEntityPrefabId, Is.EqualTo(1001));
            Assert.That(first.SpawnSequenceInTick, Is.EqualTo(7));
            Assert.That(first.Equals(second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }
```
- `Assets/Scripts/Gameplay/Tests/UnitAssemblyBoundaryTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `RuntimeAssembly_HasNoForbiddenDirectDependency`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void RuntimeAssembly_HasNoForbiddenDirectDependency()
        {
            string[] references = typeof(UnitUid)
                .Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            foreach (string forbiddenPrefix in ForbiddenDirectReferences)
            {
                Assert.That(
                    references.Any(reference => reference.StartsWith(forbiddenPrefix, StringComparison.Ordinal)),
                    Is.False,
                    $"FrameSyncMoba.Unit directly references forbidden assembly prefix '{forbiddenPrefix}'.");
            }
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
