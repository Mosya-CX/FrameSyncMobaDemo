using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class Turret : UnitCore
{
    [SerializeField, LabelText("攻击投掷物")]
    private AttackMissle attackMisslePrefab;

    [SerializeField, LabelText("搜敌半径")]
    private fp searchRadius = 10;

    [SerializeField, LabelText("攻击间隔覆盖(<=0则使用Stats.AttackInterval)")]
    private float overrideAttackInterval = 0f;

    private UnitCore currentTarget;
    private fp attackIntervalTimer;

    public UnitCore CurrentTarget => currentTarget;
    public bool HasTarget => currentTarget != null;

    private fp AttackInterval =>
        overrideAttackInterval > 0 ? (fp)overrideAttackInterval : Stats.AttackInterval;

    public override void OnSpawn(UnitUID instanceUid, int startLevel = 1)
    {
        base.OnSpawn(instanceUid, startLevel);
        attackIntervalTimer = 0;
    }

    public override void Tick(fp dt, uint currentTick)
    {
        base.Tick(dt, currentTick);

        if (IsDead)
            return;

        TickTurretAttack(dt);
    }

    protected virtual void TickTurretAttack(fp dt)
    {
        if (!CanStartAttack())
            return;

        // 只有当前目标失效时，才重新选目标
        if (!IsValidLockedTarget(currentTarget))
            currentTarget = FindBestTargetByPriority();

        if (currentTarget == null)
            return;

        direction = fpmath.normalize(currentTarget.LogicPosition - LogicPosition);
        UpdateRotation();

        fp effectiveRange = Stats.AttackDistance > 0 ? Stats.AttackDistance : searchRadius;
        if (!IsInRange(currentTarget.LogicPosition, effectiveRange))
        {
            currentTarget = null;
            return;
        }

        attackIntervalTimer -= dt;
        if (attackIntervalTimer > 0)
            return;

        attackIntervalTimer = AttackInterval;
        ExecuteAttack();
    }

    /// <summary>
    /// 当前锁定目标是否合法。
    /// 合法就持续攻击，不重新选。
    /// </summary>
    protected virtual bool IsValidLockedTarget(UnitCore unit)
    {
        if (unit == null || unit == this || unit.IsDead)
            return false;

        if (unit.TeamID == TeamID)
            return false;

        if (unit is ITurretTargetInfo info && info.IsMonster)
            return false;

        fp effectiveRange = Stats.AttackDistance > 0 ? Stats.AttackDistance : searchRadius;
        return IsInRange(unit.LogicPosition, effectiveRange);
    }

    protected virtual UnitCore FindBestTargetByPriority()
    {
        UnitCore highestPriorityHero = null;
        fp highestPriorityHeroDistSq = fp.max_value;

        UnitCore summonedUnit = null;
        fp summonedUnitDistSq = fp.max_value;

        UnitCore siegeOrSuperMinion = null;
        fp siegeOrSuperMinionDistSq = fp.max_value;

        UnitCore nearestMinion = null;
        fp nearestMinionDistSq = fp.max_value;

        fp radiusSq = searchRadius * searchRadius;

        foreach (var kv in UnitManager.Instance.Spawns)
        {
            var unit = kv.Value;
            if (!IsCandidateTarget(unit, radiusSq))
                continue;

            fp distSq = fpmath.lengthsq(unit.LogicPosition - LogicPosition);

            if (unit is ITurretTargetInfo info)
            {
                // 最高优先级：正在攻击己方英雄的敌方英雄
                if (info.IsHero && IsAttackingAlliedHero(unit))
                {
                    if (distSq < highestPriorityHeroDistSq)
                    {
                        highestPriorityHeroDistSq = distSq;
                        highestPriorityHero = unit;
                    }
                    continue;
                }

                // 第二优先级：敌方召唤物
                if (info.IsSummonedUnit)
                {
                    if (distSq < summonedUnitDistSq)
                    {
                        summonedUnitDistSq = distSq;
                        summonedUnit = unit;
                    }
                    continue;
                }

                // 第三优先级：攻城车 / 超级兵
                if (info.IsSiegeOrSuperMinion)
                {
                    if (distSq < siegeOrSuperMinionDistSq)
                    {
                        siegeOrSuperMinionDistSq = distSq;
                        siegeOrSuperMinion = unit;
                    }
                    continue;
                }

                // 默认：最近小兵
                if (info.IsLaneMinion)
                {
                    if (distSq < nearestMinionDistSq)
                    {
                        nearestMinionDistSq = distSq;
                        nearestMinion = unit;
                    }
                    continue;
                }
            }
        }

        return highestPriorityHero
            ?? summonedUnit
            ?? siegeOrSuperMinion
            ?? nearestMinion;
    }

    protected virtual bool IsCandidateTarget(UnitCore unit, fp radiusSq)
    {
        if (unit == null || unit == this || unit.IsDead)
            return false;

        if (unit.TeamID == TeamID)
            return false;

        if (fpmath.lengthsq(unit.LogicPosition - LogicPosition) > radiusSq)
            return false;

        if (unit is not ITurretTargetInfo info)
            return false;

        // 野怪永远不吸引塔仇恨
        if (info.IsMonster)
            return false;

        // 塔不会主动打非英雄/非召唤物/非兵线单位
        if (!info.IsHero && !info.IsSummonedUnit && !info.IsSiegeOrSuperMinion && !info.IsLaneMinion)
            return false;

        return true;
    }

    protected virtual bool IsAttackingAlliedHero(UnitCore attacker)
    {
        if (attacker is not ITurretTargetInfo info)
            return false;

        foreach (var kv in UnitManager.Instance.Spawns)
        {
            var unit = kv.Value;
            if (unit == null || unit.IsDead)
                continue;

            if (unit.TeamID != TeamID)
                continue;

            if (unit is HeroUnit && info.IsAttackingTarget(unit))
                return true;
        }

        return false;
    }

    protected bool IsInRange(fp3 targetPos, fp range)
    {
        var delta = targetPos - LogicPosition;
        return fpmath.lengthsq(delta) <= range * range;
    }

    public virtual void ExecuteAttack()
    {
        if (currentTarget == null || currentTarget.IsDead)
            return;

        if (attackMisslePrefab != null)
        {
            MissleManager.Instance.SpawnNow<AttackMissle>(
            attackMisslePrefab.PrefabID, new TargetTrackMissleInitialData(this, currentTarget));
        }
        else
        {
            DamageManager.Instance.CreateAttackDamageRequest(this, currentTarget);
        }
    }

    protected override void OnDeadEnter()
    {
        base.OnDeadEnter();
        currentTarget = null;
    }

    public override SimulationEntityType SimulationEntityType => SimulationEntityType.Turret;

    public override object CaptureState()
    {
        var core = (UnitCoreSnapshot)base.CaptureState();

        return new TurretSnapshot
        {
            Core = core,
            HasTarget = currentTarget != null,
            TargetId = currentTarget != null ? currentTarget.UnitID : default,
            AttackIntervalTimer = attackIntervalTimer,
        };
    }

    public override void RestoreState(object state)
    {
        var snap = (TurretSnapshot)state;

        base.RestoreState(snap.Core);
        attackIntervalTimer = snap.AttackIntervalTimer;

        if (snap.HasTarget &&
            UnitManager.Instance.Spawns.TryGetValue(snap.TargetId, out var target))
        {
            currentTarget = target;
        }
        else
        {
            currentTarget = null;
        }
    }

    [System.Serializable]
    public struct TurretSnapshot
    {
        public UnitCoreSnapshot Core;
        public bool HasTarget;
        public UnitUID TargetId;
        public fp AttackIntervalTimer;
    }
}

public interface ITurretTargetInfo
{
    bool IsHero { get; }
    bool IsSummonedUnit { get; }
    bool IsSiegeOrSuperMinion { get; }
    bool IsLaneMinion { get; }
    bool IsMonster { get; }

    /// <summary>
    /// 当前是否正在攻击某个目标
    /// </summary>
    bool IsAttackingTarget(UnitCore target);
}