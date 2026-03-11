using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

/// <summary>
/// 兵线小兵：
/// - 默认跟随兵线/流场前进
/// - 发现敌人后进入战斗
/// - 战斗结束后恢复推线
/// </summary>
public class MinionUnit : CombatUnitBase, ITurretTargetInfo
{
    [SerializeField] private bool isInBattle;
    [SerializeField] private fp battleSearchRadius = 6;
    [SerializeField] private fp leashDistance = 12;
    [SerializeField] private MinionType type = MinionType.Melee;

    [SerializeField, ReadOnly] private LaneId laneId;
    [SerializeField, ReadOnly] private byte sideTeamId;

    public LaneId Lane => laneId;
    public byte SideTeamId => sideTeamId;


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

    protected virtual void Start()
    {
        pathFinder?.SetFlowFieldMode();
    }

    protected override void TickNonCombat(fp dt)
    {
        if (TryFindNearestEnemy(out var enemy))
        {
            IsInBattle = true;
            BeginTrackTarget(enemy);
            return;
        }

        // 推线状态：保持流场驱动
        if (LocomotionState != UnitLocomotionState.Move)
            SetLocomotionState(UnitLocomotionState.Move);
    }

    protected override void TickTrackTarget(fp dt)
    {
        if (currentTarget == null || currentTarget.IsDead)
        {
            IsInBattle = false;
            StopCurrentAction();
            return;
        }

        // 脱战距离
        if (fpmath.distance(LogicPosition, currentTarget.LogicPosition) > leashDistance)
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

        if (fpmath.distance(LogicPosition, currentTarget.LogicPosition) > leashDistance)
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
            TryFindNearestEnemy(out currentTarget);

        if (currentTarget != null)
            BeginTrackTarget(currentTarget);
    }

    protected virtual void OnBattleExit()
    {
        currentTarget = null;
        currentDestination = null;
        combatMode = CombatMode.None;

        pathFinder?.Stop();
        pathFinder?.SetFlowFieldMode();
        SetLocomotionState(UnitLocomotionState.Move);
    }

    protected virtual bool TryFindNearestEnemy(out UnitCore nearest)
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

            fp distSq = fpmath.lengthsq(unit.LogicPosition - LogicPosition);
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                nearest = unit;
            }
        }

        return nearest != null;
    }

    public override void UpdateAStarPath()
    {
        if (combatMode == CombatMode.TrackTarget && currentTarget != null)
        {
            base.UpdateAStarPath();
            return;
        }
    }

    public void SetLane(LaneId lane)
    {
        laneId = lane;
    }

    public void SetSide(byte teamId)
    {
        sideTeamId = teamId;
    }

    public bool IsHero => false;
    public bool IsSummonedUnit => false;
    public bool IsSiegeOrSuperMinion => type == MinionType.Super || type == MinionType.Siege;
    public bool IsLaneMinion => true;
    public bool IsMonster => false;

    public bool IsAttackingTarget(UnitCore target)
    {
        if (target == null)
            return false;

        return CurrentCombatMode == CombatMode.AttackTarget && CurrentTarget == target;
    }

    public override SimulationEntityType SimulationEntityType => SimulationEntityType.Minion;
}

public enum MinionType
{
    Melee,
    Ranged,
    Siege,
    Super,
}