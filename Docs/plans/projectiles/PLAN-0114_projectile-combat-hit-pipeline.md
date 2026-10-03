# 投射物战斗命中管线

## 本次执行范围

本计划对应原编码 0114 的一次执行：投射物战斗命中管线。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [定义与强类型生成黑板](../../requirements/projectiles/REQ-FEAT-039_projectile-definitions.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [提交运动寿命与回收](../../requirements/projectiles/REQ-FEAT-040_projectile-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [命中过滤记忆与等距裁决](../../requirements/projectiles/REQ-FEAT-041_projectile-hit-order.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [战斗请求封存与因果波次](../../requirements/combat/REQ-FEAT-029_combat-causal-waves.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [伤害配方抗性与吸血](../../requirements/combat/REQ-FEAT-030_damage-resistance.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [濒死批次与公平击杀归属](../../requirements/combat/REQ-FEAT-034_death-attribution.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [死亡奖励与贡献窗口](../../requirements/combat/REQ-FEAT-035_death-rewards.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

ProjectileDef 配置逻辑、PrefabId、阶段模块和形状；SpawnBoard 是静态布局的强类型黑板，RequestSpawn 形成稳定 pending record。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/FrameSync/ProjectileHitResolver.cs`：`ProjectileHitResult`、`ProjectileHitResolver`。
- `Assets/Scripts/Gameplay/Projectile/ProjectileDef.cs`：`ProjectileDef`。
- `Assets/Scripts/Gameplay/Projectile/ProjectileEffectDispatcher.cs`：`ProjectileEffectDispatcher`、`ProjectileAoETarget`、`ProjectileAoETargetComparer`。
- `Assets/Scripts/Gameplay/Projectile/ProjectileUid.cs`：`ProjectileUid`。
- `Assets/Scripts/Gameplay/Projectile/ProjectileWorld.cs`：`PendingSpawnEntry`、`ProjectileWorld`。
- `Assets/Scripts/Gameplay/Unit/Core/Order.cs`：`OrderKind`、`Order`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitUid.cs`：`UnitUid`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/FrameSync/ProjectileHitResolver.cs`：

```csharp
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync
{
    public struct ProjectileHitResult
    {
        public ProjectileUid ProjectileUid;
        public UnitUid TargetUnitUid;
        public GameplayParticipantId TargetParticipantId;
        public fp2 HitPosition;
        public fp HitDistance;
        public ulong EqualDistanceTieScore;
        public int CandidateOrder;
        public int HitLogicTick;
    }

    public sealed class ProjectileHitResolver
    {
        private readonly PhysicsWorld physicsWorld;
        private readonly UnitWorld unitWorld;
        private readonly List<PhysicsEntity2D> candidates =
            new List<PhysicsEntity2D>();
        private readonly List<ProjectileHitResult> projectileHits =
            new List<ProjectileHitResult>();
        private readonly List<ProjectileHitResult> pendingHits =
            new List<ProjectileHitResult>();

        public ProjectileHitResolver(
            PhysicsWorld physicsWorld,
            UnitWorld unitWorld)
        {
            this.physicsWorld = physicsWorld ??
                throw new System.ArgumentNullException(
                    nameof(physicsWorld));
            this.unitWorld = unitWorld ??
                throw new System.ArgumentNullException(
                    nameof(unitWorld));
        }

        public IReadOnlyList<ProjectileHitResult>
            PendingHits => pendingHits;

        public void ResolveAllHits(
            ProjectileWorld projectileWorld)
        {
            pendingHits.Clear();
            if (projectileWorld == null) return;
            PhysicsSpatialGrid2D grid =
                physicsWorld.UnitFinalGrid;
            if (grid == null) return;

            IReadOnlyList<ProjectileRuntime> projectiles =
                projectileWorld.GetAllOrdered();
            int tick = SimulationTickContext.Current.Tick;
            for (int i = 0; i < projectiles.Count; i++)
            {
                ProjectileRuntime projectile =
                    projectiles[i];
                if (!projectile.ShouldQuery(tick))
                    continue;

                projectile.MarkQueried(tick);
                ResolveProjectileHits(
                    projectile,
                    grid,
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/Gameplay/Projectile/ProjectileDef.cs`：

```csharp
using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class ProjectileDef
    {
        public int DefId;
        public int RuntimeEntityPrefabId;
        public fp Speed;
        public fp Acceleration;
        /// <summary>
        /// Homing projectile (design v19: 跟踪弹体): when set, the projectile
        /// steers toward its locked TargetUnitUid each Tick and falls back to
        /// straight-line motion when the target is missing.
        /// </summary>
        public bool Homing;
        public int MaxLifetimeTicks;
        public fp HitRadius;
        public ProjectileTargetFilter TargetFilter =
            ProjectileTargetFilter.DefaultEnemy;
        public ProjectileHitPolicy HitPolicy =
            ProjectileHitPolicy.DefaultSingleHit;
        public ProjectileOnHitEffects OnHitEffects = ProjectileOnHitEffects.Empty;
        public ProjectileAoEConfig AoE = ProjectileAoEConfig.None;
        public ProjectileContainmentZone ContainmentZone;
        public bool IsValid =>
            DefId > 0 &&
            RuntimeEntityPrefabId > 0 &&
            MaxLifetimeTicks > 0 &&
            HitRadius >= fp.zero;

        public void ValidateOrThrow()
        {
            if (!IsValid)
                throw new InvalidOperationException(
                    $"ProjectileDef {DefId} has invalid identity, lifetime or radius.");
            TargetFilter.ValidateOrThrow();
            HitPolicy.ValidateOrThrow();
            OnHitEffects.ValidateOrThrow();
        }
    }
}
```

### 输入输出与边界

**定义与强类型生成黑板**

ProjectileDef 配置逻辑、PrefabId、阶段模块和形状；SpawnBoard 是静态布局的强类型黑板，RequestSpawn 形成稳定 pending record。

逻辑配置 ID 与 PrefabId 不合并；不使用任意 object 字典；OnHitDamageOverride、MaxLifetime 与动作来源均保存到所属快照。

**提交运动寿命与回收**

ProjectileWorld 拥有每 Tick spawn sequence；CommitSpawns 是唯一创建入口，随后 AdvanceMotion、UpdateLifecycle、ResolveHits、EmitEffects、FlushDestroy。

FrameSync 不维护第二个投射物 BeginTick 序列；Pending 与 Active 状态均可恢复；池化实体和逻辑实例各有释放 owner。

**命中过滤记忆与等距裁决**

ProjectileHitQueryService 使用 UnitFinalGrid、扫掠形状和正式 TargetFilter；ProjectileHitMemory 保存跨 Tick 命中事实，命中结果进入 Combat 或所属效果端口。

距离为主键；等距按动作/参与者纯哈希；记忆不能依赖候选枚举顺序；结构技能效果由中央准入兜底拒绝。

**战斗请求封存与因果波次**

收集强类型 Shield、Damage、Heal 请求；封存成因果波次后才分配最终 SequenceInTick；同目标基于批次开始时冻结状态结算。

同批治疗封顶、盾参与吸收、总生命伤害一次提交；由结果产生的新反应进下一波；死亡反应产生的普通请求进下一 Tick。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/UnitWorldTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `InternalRegistration_PublicLookupReturnsSameRuntime`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void InternalRegistration_PublicLookupReturnsSameRuntime()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(300, 9, 1), UnitKind.Hero, 0, TeamId.Neutral);

            world.RegisterUnit(unit);

            Assert.That(world.TryGetUnit(unit.UnitUid, out Unit resolved), Is.True);
            Assert.That(resolved, Is.SameAs(unit));
            Assert.That(world.TryGetUnit(new UnitUid(300, 9, 2), out _), Is.False);
        }
```
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
- `Assets/Scripts/Gameplay/Tests/UnitWorldIntegrationTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `SpawnMultipleKinds_GetByKind`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SpawnMultipleKinds_GetByKind()
        {
            world.SpawnUnit(heroProto, TeamId.Neutral, 1, 0m, 0m);
            world.SpawnUnit(heroProto, TeamId.Neutral, 1, 0m, 0m);
            world.SpawnUnit(minionProto, TeamId.Neutral, 1, 0m, 0m);

            var heroes = world.GetUnitsByKind(UnitKind.Hero);
            var minions = world.GetUnitsByKind(UnitKind.Minion);
            var all = world.GetAllUnits();

            Assert.AreEqual(2, heroes.Count);
            Assert.AreEqual(1, minions.Count);
            Assert.AreEqual(3, all.Count);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
