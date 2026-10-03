# 表现事件历史与商店视图

## 本次执行范围

本计划对应原编码 0118 的一次执行：表现事件历史与商店视图。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [单位视图绑定与语义挂点](../../requirements/presentation-ui/REQ-FEAT-072_unit-view-binding.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [攻击技能动画与插值采样](../../requirements/presentation-ui/REQ-FEAT-073_animation-sampling.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [特效音效与回滚账本](../../requirements/presentation-ui/REQ-FEAT-074_presentation-ledger.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [商店商品详情与余额刷新](../../requirements/presentation-ui/REQ-FEAT-077_shop-view.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [购买合成与 Command 二次校验](../../requirements/equipment-shop/REQ-FEAT-055_shop-purchase-validation.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [卖出撤销与交易失效](../../requirements/equipment-shop/REQ-FEAT-056_shop-undo.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UnitPresentationHost/Registry 和 PresentationSocketSet 只读逻辑；SocketProfile 定义挂点，缺失挂点按可见校验策略处理。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Presentation/PresentationEventId.cs`：`PresentationSourceKind`、`SfxAnchor`、`PresentationEventKeys`、`PresentationEventId`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Presentation/PresentationEventId.cs`：

```csharp
using System;

namespace FrameSyncMoba.Unit
{
    public enum PresentationSourceKind
    {
        Unit,
        Projectile,
    }

    public enum SfxAnchor
    {
        UnitRoot,
        Camera,
        World,
    }

    public static class PresentationEventKeys
    {
        public const int CombatHit = 1;
        public const int CombatDeath = 2;
        public const int AbilityCast = 3;
        public const int BuffApplied = 4;
        public const int BuffDetonated = 5;
    }

    public struct PresentationEventId : IEquatable<PresentationEventId>
    {
        public int SourceLogicTick;
        public PresentationSourceKind SourceKind;
        public UnitUid SourceRuntimeUid;
        public int EventSequence;
        public int EventKey;

        public bool Equals(PresentationEventId other)
        {
            return SourceLogicTick == other.SourceLogicTick
                && SourceKind == other.SourceKind
                && SourceRuntimeUid.Equals(other.SourceRuntimeUid)
                && EventSequence == other.EventSequence
                && EventKey == other.EventKey;
        }

        public override bool Equals(object obj)
            => obj is PresentationEventId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SourceLogicTick;
                hash = (hash * 397) ^ (int)SourceKind;
                hash = (hash * 397) ^ SourceRuntimeUid.GetHashCode();
                hash = (hash * 397) ^ EventSequence;
                hash = (hash * 397) ^ EventKey;
                return hash;
            }
        }

        public static bool operator ==(PresentationEventId a, PresentationEventId b) => a.Equals(b);
        public static bool operator !=(PresentationEventId a, PresentationEventId b) => !a.Equals(b);
    }
}
```

### 输入输出与边界

**单位视图绑定与语义挂点**

UnitPresentationHost/Registry 和 PresentationSocketSet 只读逻辑；SocketProfile 定义挂点，缺失挂点按可见校验策略处理。

视图不能反写 Gameplay；异步加载不能复用旧生命；逻辑空间所有者不随模型层级变化。

**攻击技能动画与插值采样**

UnitAnimationDriver 读取 Attack 锁定时间与 AbilityCastView；客户端默认 20 Hz 插值，Bootstrap 发布按 UnitWorld 拥有的连续逻辑时间投影。

不得跨未 Commit 的 Impact 或 Ready；loop 相位由逻辑 epoch 和实时倍率重建；未知 TickRate 不硬回退 30 Hz；独立采样不新增 Gameplay Tick。

**特效音效与回滚账本**

VfxManager、AudioManager 分别管理定义、池和回滚账本；PresentationEventId=SourceLogicTick+SourceKind+SourceRuntimeUid+EventSequence+EventKey，支持 OneShotNoReplay、DurationCorrectable、LoopState。

Gameplay 不直接 Play 音效；每 manager 独立账本；事件序号归源运行时所有，表现不新增第二序号。

**商店商品详情与余额刷新**

Shop.lua 直接调用 IEquipmentShopView 与 Request 接口；CurrentAvailableGold 来自 GoldIncomeRuntime 确认累计加商店增量。

预测收入不提前可用；普通回滚和 AuthorityRecovery 后修订通知重刷；界面不暴露 ProcessCommand 或写交易状态。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Bootstrap/Tests/EditMode/PresentationEventDispatcherTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `Replay_DoesNotDispatchCompletedEventAgain`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void Replay_DoesNotDispatchCompletedEventAgain()
        {
            PresentationEventId id =
                CreateId(10, 1, 20);

            SubmitVfx(id);
            dispatcher.DispatchCurrentFrame();
            SubmitVfx(id);
            dispatcher.DispatchCurrentFrame();

            Assert.That(vfx.Count, Is.EqualTo(1));
        }
```
- `Assets/Scripts/FrameSync/Tests/EquipmentShopViewTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `CurrentAvailableGold_UsesConfirmedIncomeAndEffectiveShopDelta`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void CurrentAvailableGold_UsesConfirmedIncomeAndEffectiveShopDelta()
        {
            var income = new GoldIncomeRuntime();
            income.Initialize(1, 1000);
            var shop = new EquipmentShopRuntime();
            shop.Initialize(
                1,
                new EquipmentDatabase(),
                Unity.Mathematics.FixedPoint.fp.one,
                new UnitWorld());
            ShopTraderRuntime trader =
                shop.GetOrCreateTrader(
                    0,
                    new UnitUid(1, 1, 1));
            trader.OperationLog.Add(
                new ShopOperationRecord
                {
                    GoldDelta = -200,
                    Reverted = false,
                });
            trader.OperationLog.Add(
                new ShopOperationRecord
                {
                    GoldDelta = 50,
                    Reverted = true,
                });
            IEquipmentShopView view =
                new EquipmentShopView(
                    shop,
                    income,
                    0);

            Assert.That(
                view.GetCurrentAvailableGold(),
                Is.EqualTo(800));
            Assert.That(
                income
                    .GetConfirmedEarnedGoldTotal(0),
                Is.EqualTo(1000));

            income.BeginTick(0);
            income.RequestGoldIncome(
                0,
                100,
                GoldIncomeReason.NaturalIncome);
            income.SealTick(0);
            Assert.That(
                view.GetCurrentAvailableGold(),
                Is.EqualTo(800),
                "Unconfirmed Gameplay income is not spendable.");
// 方法后续请阅读上述真实源码；这里是节选。
```
- `Assets/Scripts/Gameplay/Tests/CombatSystemTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `SubmitDamage_ValidRequest_ReducesHealth`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SubmitDamage_ValidRequest_ReducesHealth()
        {
            BeginTick(1);
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            var target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            fp initialHealth = target.StatHandler.CurrentHealth;

            _combat.BeginTick();
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                attacker.UnitUid, target.UnitUid, (fp)100));
            _combat.SettleActiveRequests();
            _combat.EndTick();

            fp finalHealth = target.StatHandler.CurrentHealth;
            Assert.Less(finalHealth, initialHealth);
            Assert.Greater(finalHealth, fp.zero);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
