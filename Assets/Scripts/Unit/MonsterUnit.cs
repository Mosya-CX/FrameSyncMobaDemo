using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

/// <summary>
/// 野怪：
/// - 绑定营地与出生点
/// - 战斗时锁定敌方英雄/单位
/// - 脱战后回出生点
/// </summary>
public class MonsterUnit : CombatUnitBase, ITurretTargetInfo
{
    [SerializeField, ReadOnly] private MonsterCamp camp;
    [SerializeField, ReadOnly] private fp3 originPosition;
    [SerializeField, ReadOnly] private fp2 originRotation;

    [SerializeField] private bool isInBattle;
    [SerializeField] private fp battleSearchRadius = 8;
    [SerializeField] private fp leashDistance = 12;
    [SerializeField] private fp returnReachThreshold = 0.2m;

    public bool IsInBattle
    {
        get => isInBattle;
        set
        {
            if (isInBattle == value)
                return;

            isInBattle = value;

            if (isInBattle)
                OnBattleEnter();
            else
                OnBattleExit();
        }
    }

    public void SetBelongTo(MonsterCamp camp, fp3 position, fp2 rotation)
    {
        this.camp = camp;
        originPosition = position;
        originRotation = rotation;

        LogicPosition = originPosition;
        LogicRotation = originRotation;
    }

    protected override void TickNonCombat(fp dt)
    {
        // 先尝试找目标
        if (TryFindNearestEnemyHero(out var hero))
        {
            IsInBattle = true;
            BeginTrackTarget(hero);
            return;
        }

        // 不在出生点则回营地
        if (!IsReach(originPosition, returnReachThreshold))
        {
            BeginMoveTo(originPosition);
            return;
        }

        StopCurrentAction();
    }

    protected override void TickMoveToPoint(fp dt)
    {
        base.TickMoveToPoint(dt);

        if (!isInBattle && IsReach(originPosition, returnReachThreshold))
        {
            LogicRotation = originRotation;
            StopCurrentAction();
        }
    }

    protected override void TickTrackTarget(fp dt)
    {
        if (currentTarget == null || currentTarget.IsDead)
        {
            IsInBattle = false;
            StopCurrentAction();
            return;
        }

        if (fpmath.distance(LogicPosition, originPosition) > leashDistance)
        {
            IsInBattle = false;
            StopCurrentAction();
            return;
        }

        base.TickTrackTarget(dt);
    }

    protected override void TickAttackTarget(fp dt)
    {
        if (currentTarget == null || currentTarget.IsDead)
        {
            IsInBattle = false;
            StopCurrentAction();
            return;
        }

        if (fpmath.distance(LogicPosition, originPosition) > leashDistance)
        {
            IsInBattle = false;
            StopCurrentAction();
            return;
        }

        base.TickAttackTarget(dt);
    }

    protected virtual void OnBattleEnter()
    {
        if (currentTarget == null || currentTarget.IsDead)
            TryFindNearestEnemyHero(out currentTarget);

        if (currentTarget != null)
            BeginTrackTarget(currentTarget);
    }

    protected virtual void OnBattleExit()
    {
        currentTarget = null;
        BeginMoveTo(originPosition);
    }

    protected virtual bool TryFindNearestEnemyHero(out UnitCore nearest)
    {
        nearest = null;
        fp minDistSq = battleSearchRadius * battleSearchRadius;

        foreach (var kv in UnitManager.Instance.Spawns)
        {
            var unit = kv.Value;
            if (unit == null || unit == this || unit.IsDead)
                continue;

            if (unit.TeamID == TeamID)
                continue;

            // 这里只筛 HeroUnit；你也可以改成 camp 仇恨白名单
            if (unit is not HeroUnit)
                continue;

            fp distSq = fpmath.lengthsq(unit.LogicPosition - LogicPosition);
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                nearest = unit;
            }
        }

        return nearest != null;
    }

    public bool IsHero => false;
    public bool IsSummonedUnit => false;
    public bool IsSiegeOrSuperMinion => false;
    public bool IsLaneMinion => false;
    public bool IsMonster => true;

    public bool IsAttackingTarget(UnitCore target)
    {
        if (target == null)
            return false;

        return CurrentCombatMode == CombatMode.AttackTarget && CurrentTarget == target;
    }

    public override SimulationEntityType SimulationEntityType => SimulationEntityType.Monster;

    public override object CaptureState()
    {
        var baseState = (CombatUnitSnapshot)base.CaptureState();

        return new MonsterUnitSnapshot
        {
            Base = baseState,
            IsInBattle = isInBattle,
            OriginPosition = originPosition,
            OriginRotation = originRotation,
        };
    }

    public override void RestoreState(object state)
    {
        var snap = (MonsterUnitSnapshot)state;

        base.RestoreState(snap.Base);
        isInBattle = snap.IsInBattle;
        originPosition = snap.OriginPosition;
        originRotation = snap.OriginRotation;
    }

    [System.Serializable]
    public struct MonsterUnitSnapshot
    {
        public CombatUnitSnapshot Base;
        public bool IsInBattle;
        public fp3 OriginPosition;
        public fp2 OriginRotation;
    }
}
