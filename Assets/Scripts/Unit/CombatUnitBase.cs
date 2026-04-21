using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

/// <summary>
/// 所有可战斗单位的共享基类。
/// 负责：
/// 目标点移动
/// 目标单位追踪
/// 基础普攻前摇/后摇
/// 普攻发射物 / 直接伤害
/// </summary>
public abstract class CombatUnitBase : UnitCore
{
    #region 战斗运行时
    [SerializeField, LabelText("攻击投掷物")]
    protected AttackMissle attackMisslePrefab;

    [SerializeField, LabelText("基础攻击前摇时长占比")]
    protected float baseAttackWindupDuration = 0.2f;

    [SerializeField, LabelText("基础攻击后摇时长占比")]
    protected float baseAttackRecoveryDuration = 0.8f;

    protected fp3? currentDestination;
    protected UnitCore currentTarget;

    protected fp attackPrecastTimer;
    protected fp attackRecoveryTimer;

    protected CombatMode combatMode = CombatMode.None;
    public CombatMode CurrentCombatMode => combatMode;

    public bool IsInAttackRecovery => attackRecoveryTimer > 0;
    public bool HasDestination => currentDestination.HasValue;
    public bool HasTarget => currentTarget != null;
    public UnitCore CurrentTarget => currentTarget;
    public fp3? CurrentDestination => currentDestination;

    protected DashMotor dashMotor;
    protected SkillExecutionController skillExecutionController;
    protected SkillBook skillBook;
    protected UnitAnimationController animationController;

    public DashMotor DashMotor => dashMotor;
    public SkillExecutionController SkillExecutionController => skillExecutionController;
    public SkillBook SkillBook => skillBook;
    public UnitAnimationController AnimationController => animationController;

    #endregion

    protected override void Awake()
    {
        base.Awake();

        dashMotor = new DashMotor(this);
        skillExecutionController = GetComponent<SkillExecutionController>();
        skillBook = GetComponent<SkillBook>();
        animationController = GetComponent<UnitAnimationController>();
    }

    public override void Tick(fp dt, uint currentTick)
    {
        base.Tick(dt, currentTick);

        animationController?.Tick(dt);
        if (IsDead)
            return;

        if (attackRecoveryTimer > 0)
        {
            attackRecoveryTimer -= dt;
            if (attackRecoveryTimer < 0)
                attackRecoveryTimer = 0;
        }

        TickCombat(dt);
        dashMotor?.Tick(dt);
        skillExecutionController?.Tick(dt, currentTick);
    }

    protected virtual void TickCombat(fp dt)
    {
        switch (combatMode)
        {
            case CombatMode.None:
                TickNonCombat(dt);
                break;

            case CombatMode.MoveToPoint:
                TickMoveToPoint(dt);
                break;

            case CombatMode.TrackTarget:
                TickTrackTarget(dt);
                break;

            case CombatMode.AttackTarget:
                TickAttackTarget(dt);
                break;
        }
    }

    protected virtual void TickNonCombat(fp dt)
    {
    }

    protected virtual void TickMoveToPoint(fp dt)
    {
        if (!currentDestination.HasValue)
        {
            StopCurrentAction();
            return;
        }

        if (!CanStartMove())
        {
            return;
        }

        if (IsReach(currentDestination.Value, 0.05m))
        {
            currentDestination = null;
            StopCurrentAction();
            return;
        }

        SetLocomotionState(UnitLocomotionState.Move);
        UpdateRotation();
    }

    protected virtual void TickTrackTarget(fp dt)
    {
        if (currentTarget == null || currentTarget.IsDead)
        {
            StopCurrentAction();
            return;
        }

        if (!CanStartTrack())
            return;

        var targetPos = currentTarget.LogicPosition;
        var attackDistance = Stats.AttackDistance;

        direction = fpmath.normalize(targetPos - LogicPosition);

        if (IsReach(targetPos, attackDistance))
        {
            BeginAttackTarget(currentTarget);
            return;
        }

        SetLocomotionState(UnitLocomotionState.Move);
        UpdateRotation();
    }

    protected virtual void TickAttackTarget(fp dt)
    {
        if (currentTarget == null || currentTarget.IsDead)
        {
            StopCurrentAction();
            return;
        }

        if (!CanStartAttack())
            return;

        var targetPos = currentTarget.LogicPosition;
        direction = fpmath.normalize(targetPos - LogicPosition);
        UpdateRotation();

        if (!IsReach(targetPos, Stats.AttackDistance))
        {
            BeginTrackTarget(currentTarget);
            return;
        }

        if (IsInAttackRecovery)
            return;

        attackPrecastTimer -= dt;
        if (attackPrecastTimer <= 0)
        {
            attackPrecastTimer = Stats.AttackInterval * (fp)baseAttackWindupDuration;
            attackRecoveryTimer = Stats.AttackInterval * (fp)baseAttackRecoveryDuration;
            ExecuteAttack();
        }
    }

    protected override void OnDeadEnter()
    {
        base.OnDeadEnter();
        DashMotor?.Cancel();
    }

    #region 公开行为入口
    public virtual void BeginMoveTo(fp3 destination)
    {
        if (!CanStartMove())
            return;

        currentTarget = null;
        currentDestination = destination;
        combatMode = CombatMode.MoveToPoint;

        pathFinder?.SetDestination(destination);
        SetLocomotionState(UnitLocomotionState.Move);
    }

    public virtual void BeginTrackTarget(UnitCore target)
    {
        if (target == null || target.IsDead)
            return;

        if (!CanStartTrack())
            return;

        currentDestination = null;
        currentTarget = target;
        combatMode = CombatMode.TrackTarget;

        pathFinder?.SetTarget(target);
        SetLocomotionState(UnitLocomotionState.Move);
    }

    public virtual void BeginAttackTarget(UnitCore target)
    {
        if (target == null || target.IsDead)
            return;

        if (!CanStartAttack())
            return;

        currentDestination = null;
        currentTarget = target;
        combatMode = CombatMode.AttackTarget;

        attackPrecastTimer = Stats.AttackInterval * (fp)baseAttackWindupDuration;
        SetLocomotionState(UnitLocomotionState.Idle);
    }

    public virtual void StopCurrentAction()
    {
        currentDestination = null;
        currentTarget = null;
        combatMode = CombatMode.None;

        pathFinder?.Stop();

        if (!IsDead)
            SetLocomotionState(UnitLocomotionState.Idle);
    }
    #endregion

    #region 基础攻击
    public virtual void ExecuteAttack()
    {
        if (currentTarget == null || currentTarget.IsDead)
            return;

        OnAttackPerformed(currentTarget);

        if (attackMisslePrefab != null)
        {
            MissleManager.Instance.SpawnNow<AttackMissle>(
                attackMisslePrefab.PrefabID,
                new TargetTrackMissleInitialData(this, currentTarget));
        }
        else
        {
            DamageManager.Instance.CreateAttackDamageRequest(this, currentTarget);
        }
    }
    #endregion

    #region 移动状态表现
    protected override void OnMoveTick(fp dt)
    {
        if (DashBlocked())
            return;

        base.OnMoveTick(dt);
    }

    protected virtual bool DashBlocked()
    {
        return DashMotor.IsDashing;
    }
    #endregion

    #region 快照和回滚
    public override object CaptureState()
    {
        var coreState = (UnitCoreSnapshot)base.CaptureState();

        return new CombatUnitSnapshot
        {
            Core = coreState,
            CombatMode = combatMode,
            Destination = currentDestination,
            TargetId = currentTarget != null ? currentTarget.UnitID : default,
            HasTarget = currentTarget != null,
            AttackPrecastTimer = attackPrecastTimer,
            AttackRecoveryTimer = attackRecoveryTimer,
            DashState = DashMotor.CaptureState(),
            SkillExecutionControllerState = skillExecutionController != null ? skillExecutionController.CaptureState() : null,
            SkillBookState = skillBook != null ? skillBook.CaptureState() : null,
        };
    }

    public override void RestoreState(object state)
    {
        var snap = (CombatUnitSnapshot)state;

        base.RestoreState(snap.Core);

        combatMode = snap.CombatMode;
        currentDestination = snap.Destination;
        attackPrecastTimer = snap.AttackPrecastTimer;
        attackRecoveryTimer = snap.AttackRecoveryTimer;

        if (snap.HasTarget && UnitManager.Instance.Spawns.TryGetValue(snap.TargetId, out var target))
            currentTarget = target;
        else
            currentTarget = null;

        DashMotor.RestoreState(snap.DashState);

        if (skillExecutionController != null && snap.SkillExecutionControllerState != null)
            skillExecutionController.RestoreState(snap.SkillExecutionControllerState);

        if (skillBook != null && snap.SkillBookState != null)
            skillBook.RestoreState(snap.SkillBookState);

        RebuildCombatNavigationState();
    }

    [System.Serializable]
    public struct CombatUnitSnapshot
    {
        public UnitCoreSnapshot Core;
        public CombatMode CombatMode;
        public fp3? Destination;
        public UnitUID TargetId;
        public bool HasTarget;
        public fp AttackPrecastTimer;
        public fp AttackRecoveryTimer;

        public object DashState;
        public object SkillExecutionControllerState;
        public object SkillBookState;
    }

    protected virtual void RebuildCombatNavigationState()
    {
        switch (combatMode)
        {
            case CombatMode.None:
                pathFinder?.Stop();
                if (!IsDead)
                    SetLocomotionState(UnitLocomotionState.Idle);
                break;

            case CombatMode.MoveToPoint:
                if (currentDestination.HasValue)
                {
                    pathFinder?.SetDestination(currentDestination.Value);
                    if (!IsDead)
                        SetLocomotionState(UnitLocomotionState.Move);
                }
                else
                {
                    StopCurrentAction();
                }
                break;

            case CombatMode.TrackTarget:
                if (currentTarget != null && !currentTarget.IsDead)
                {
                    pathFinder?.SetTarget(currentTarget);
                    if (!IsDead)
                        SetLocomotionState(UnitLocomotionState.Move);
                }
                else
                {
                    StopCurrentAction();
                }
                break;

            case CombatMode.AttackTarget:
                if (currentTarget == null || currentTarget.IsDead)
                {
                    StopCurrentAction();
                }
                else
                {
                    if (!IsDead)
                        SetLocomotionState(UnitLocomotionState.Idle);
                }
                break;
        }
    }
    #endregion

    #region 动画接口实现
    public virtual bool ShouldPlayAttackAnimation()
    {
        return combatMode == CombatMode.AttackTarget && attackPrecastTimer > 0;
    }
    #endregion
}

public enum CombatMode : byte
{
    None,
    MoveToPoint,
    TrackTarget,
    AttackTarget,
}