using UnityEngine;
using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using System;

public abstract class UnitCore : MonoBehaviour, IStateful, IDynamicObstacle
{
    #region 基础信息
    [SerializeField, LabelText("预制体ID"), ReadOnly]
    private int prefabId;
    public int PrefabId => prefabId;

    [SerializeField, LabelText("单位实例ID"), ReadOnly]
    private UnitUID unitId;
    public UnitUID UnitID => unitId;

    [SerializeField, LabelText("团队ID"), ReadOnly]
    private byte teamId;
    public byte TeamID => teamId;

    [SerializeField, LabelText("模型根节点")]
    private Transform modelRoot;
    #endregion

    #region 单位状态

    [SerializeField, LabelText("数值配置")]
    protected UnitPropertyConfig propertyConfig;

    protected UnitStats stats = new();
    public UnitStats Stats => stats;

    [SerializeField, LabelText("当前等级")]
    protected int level;

    [ShowInInspector, ReadOnly, LabelText("逻辑坐标")]
    protected fp3 logicPosition;
    public fp3 LogicPosition
    {
        get => logicPosition;
        set => logicPosition = value;
    }

    [ShowInInspector, ReadOnly, LabelText("逻辑旋转")]
    protected fp2 logicRotation;// x代表四元数的y，y代表四元数的w

    [ShowInInspector, ReadOnly, LabelText("状态")]
    protected UnitActionState currentActionState;
    public UnitActionState CurrentActionState => currentActionState;
    #endregion

    #region 行为数据
    [ShowInInspector, ReadOnly, LabelText("移动方向")]
    protected fp3 direction;
    protected PathFinder pathFinder;
    
    protected fp3? currentDestination;
    protected UnitCore currentTarget;

    private fp attackExcuteTimer;
    #endregion

    #region 行为限制
    public bool lockInput;// 禁用指令输入
    public bool lockMove;// 禁用移动
    public bool lockRotateion;// 禁用更新旋转
    public bool lockAttack;// 禁用攻击(但可以追踪目标)
    public bool lockCasting;// 禁用施法
    #endregion

    #region 回调事件
    public Action<DamageInfo> OnDamageHit;
    public Action<DamageInfo> OnGetDamage;
    public Action<DamageInfo> OnKill;
    public Action<DamageInfo> OnDeath;

    public Action OnReachDestination;
    public Action OnTrackCompleted;
    #endregion

    #region 额外功能

    protected UnitBaseHandler[] handlers;
    protected AbilityHandler abilityHandler;
    protected BuffHandler buffHandler;
    protected CrowdControlHandler crowdControlHandler;
    protected EquipmentHandler equipmentHandler;

    [ShowInInspector, ReadOnly]
    public AbilityHandler AbilityHandler => abilityHandler;
    [ShowInInspector, ReadOnly]
    public BuffHandler BuffHandler => buffHandler;
    [ShowInInspector, ReadOnly]
    public CrowdControlHandler CrowdControlHandler => crowdControlHandler;
    [ShowInInspector, ReadOnly]
    public EquipmentHandler EquipmentHandler => equipmentHandler;

    #endregion

    #region 生命周期
    protected virtual void Awake()
    {
        handlers = GetComponents<UnitBaseHandler>();
        abilityHandler = GetComponent<AbilityHandler>();
        buffHandler = GetComponent<BuffHandler>();
        crowdControlHandler = GetComponent<CrowdControlHandler>();
        equipmentHandler = GetComponent<EquipmentHandler>();
        pathFinder = GetComponent<PathFinder>();
    }

    public virtual void OnSpawn(UnitUID instanceUid, int startLevel = 1)
    {
        prefabId = instanceUid.PrefabId;
        unitId = instanceUid;
        teamId = instanceUid.TeamId;

        level = startLevel;
        stats.Init(propertyConfig);
        stats.SetLevel(level);

        RegisterRVOGenerator();
    }

    public virtual void OnDespawn()
    {
        stats.Clean();
        unitId = default;

        UnregisterRVOGenerator();
    }

    private void LateUpdate()
    {
        SyncTransform();
    }

    public void Tick(fp dt)
    {
        buffHandler.Tick(dt);
        abilityHandler.Tick(dt);
        crowdControlHandler.Tick(dt);
        equipmentHandler.Tick(dt);

        switch (currentActionState)
        {
            case UnitActionState.Idle:
                OnIdleTick(dt);
                break;
            case UnitActionState.Move:
                OnMoveTick(dt);   
                break;
            case UnitActionState.Track:
                OnTrackTick(dt);
                break;
            case UnitActionState.Attack:
                OnAttackTick(dt);
                break;
            case UnitActionState.Casting:
                OnCastingTick(dt);
                break;
        }
    }

    #endregion

    #region 状态机部分
    protected void ChangeActionState(UnitActionState nextState)
    {
        if (nextState == currentActionState) 
            return;

        OnStateExit(currentActionState);
        OnStateEnter(nextState);
        currentActionState = nextState;
    }

    private void OnStateExit(UnitActionState current)
    {
        switch (currentActionState)
        {
            case UnitActionState.Idle:
                OnIdleExit();
                break;
            case UnitActionState.Move:
                OnMoveExit();
                break;
            case UnitActionState.Track:
                OnTrackExit();
                break;
            case UnitActionState.Attack:
                OnAttackExit();
                break;
            case UnitActionState.Casting:
                OnCastingExit();
                break;
        }
    }

    private void OnStateEnter(UnitActionState next)
    {
        switch (currentActionState)
        {
            case UnitActionState.Idle:
                OnIdleEnter();
                break;
            case UnitActionState.Move:
                OnMoveEnter();
                break;
            case UnitActionState.Track:
                OnTrackEnter();
                break;
            case UnitActionState.Attack:
                OnAttackEnter();
                break;
            case UnitActionState.Casting:
                OnCastingEnter();
                break;
        }
    }

    #region Idle
    protected virtual void OnIdleEnter()
    {

    }
    protected virtual void OnIdleTick(fp dt)
    {

    }
    protected virtual void OnIdleExit()
    {

    }
    #endregion

    #region Move
    protected virtual void OnMoveEnter()
    {

    }
    protected virtual void OnMoveTick(fp dt)
    {
        if (!currentDestination.HasValue)
        {
            ChangeActionState(UnitActionState.Idle);
            return;
        }
        if (IsReach(currentDestination.Value, 0.01m))
        {
            OnReachDestination?.Invoke();
            currentDestination = null;
            ChangeActionState(UnitActionState.Idle);
            return;
        }

        ApplyRotateByDir();
    }
    protected virtual void OnMoveExit()
    {
        
    }
    #endregion

    #region Track
    protected virtual void OnTrackEnter()
    {

    }
    protected virtual void OnTrackTick(fp dt)
    {
        if (currentTarget == null)
        {
            ChangeActionState(UnitActionState.Idle);
            return;
        }
        if (IsReach(currentTarget.logicPosition, stats.RealAttackDistance))
        {
            OnTrackCompleted?.Invoke();
            ChangeActionState(UnitActionState.Attack);
            return;
        }

        ApplyRotateByDir();
    }

    protected virtual void OnTrackExit()
    {
        
    }
    #endregion
    
    #region Attack
    protected virtual void OnAttackEnter()
    {
        attackExcuteTimer = 0;
    }

    protected virtual void OnAttackTick(fp dt)
    {
        if (currentTarget == null)
        {
            ChangeActionState(UnitActionState.Idle);
            return;
        }
        if (IsReach(currentTarget.logicPosition, 0.1m))
        {
            ChangeActionState(UnitActionState.Track);
            return;
        }

        direction = fpmath.normalize(currentTarget.logicPosition - logicPosition);
        ApplyRotateByDir();

        attackExcuteTimer += dt;
        if (attackExcuteTimer > stats.AttackInterval)
        {
            ExcuteAttack();
            attackExcuteTimer -= stats.AttackInterval;
        }
    }

    protected virtual void OnAttackExit()
    {

    }
    #endregion

    #region Casting
    protected virtual void OnCastingEnter()
    {

    }
    protected virtual void OnCastingTick(fp dt)
    {

    }
    protected virtual void OnCastingExit()
    {

    }
    #endregion

    #endregion

    #region 寻路

    protected bool IsReach(fp3? targetDestination, fp reachThshold)
    {
        if (!targetDestination.HasValue) return true;
        return fpmath.distance(logicPosition, targetDestination.Value) < reachThshold;
    }

    public abstract void UpdateAStarPath();
    public abstract void UpdateMoveDirection();

    public void ApplyMove(fp dt, fp3 modifier)
    {
        if (lockMove) 
            return;

        if (currentActionState != UnitActionState.Move && currentActionState != UnitActionState.Track)
            return;

        var moveDirection = fpmath.normalize(direction + modifier);

        logicPosition += dt * new fp3(moveDirection.x, 0, moveDirection.y) * stats.RealMoveSpeed;
    }
    #endregion

    #region 攻击和受伤
    public void ExcuteAttack()
    {

    }

    public void ApplyGetDamage(DamageInfo info)
    {
        
    }
    #endregion

    #region 快照和恢复

    public object CaptureState()
    {
        var handlerStates = new object[handlers.Length];
        for (int i = 0; i < handlers.Length; i++)
        {
            if (handlers[i] is IHandlerStateful stateful)
                handlerStates[i] = stateful.CaptureHandlerState();
        }

        return new UnitCoreSnapshot
        {
            Position = logicPosition,
            Rotation = logicRotation,
            Level = level,
            StatsSnapshot = stats.Capture(),  
            HandlerStates = handlerStates
        };
    }

    public void RestoreState(object state)
    {
        var snap = (UnitCoreSnapshot)state;

        // 恢复位置
        logicPosition = snap.Position;
        logicRotation = snap.Rotation;

        // 恢复等级
        level = snap.Level;
        stats.SetLevel(level);  

        // 恢复 stats
        stats.Restore(snap.StatsSnapshot);

        // 恢复每个 handler 的状态
        for (int i = 0; i < handlers.Length; i++)
        {
            if (handlers[i] is IHandlerStateful stateful && snap.HandlerStates[i] != null)
                stateful.RestoreHandlerState(snap.HandlerStates[i]);
        }
    }

    [System.Serializable]
    public struct UnitCoreSnapshot
    {
        public fp3 Position;
        public fp2 Rotation;
        public fp3 Destination;
        public UnitUID TargetID;
        public UnitActionState ActionState;
        public int Level;
        public UnitStats.UnitStatsSnapshot StatsSnapshot; 
        public object[] HandlerStates;
    }
    #endregion

    #region 重要状态属性快捷读取
    public fp CurrentHealth => stats.CurrentHealth;
    public fp MaxHealth => stats.Get(UnitStatType.MaxHealth);
    public fp CurrentMana => stats.CurrentMana;
    public fp MaxMana => stats.Get(UnitStatType.MaxMana);
    public fp AttackDamage => stats.Get(UnitStatType.AttackDamage);
    public fp AbilityPower => stats.Get(UnitStatType.AbilityPower);
    public fp Armor => stats.Get(UnitStatType.Armor);
    public fp MagicResist => stats.Get(UnitStatType.MagicResist);
    public fp CritChance => stats.Get(UnitStatType.CritChance);
    public fp MoveSpeed => stats.Get(UnitStatType.MoveSpeed);
    public fp PhysicalReduction => stats.PhysicalDamageReduction;
    public fp MagicReduction => stats.MagicDamageReduction;
    #endregion

    #region IDynamicObstacle接口实现
    public fp3 ObstaclePosition => logicPosition;

    public fp3 ObstacleDirection => direction;

    public fp3 ObstacleSpeed => stats.RealMoveSpeed;

    public IDynamicObstacle Ingore => currentTarget;

    fp3 IDynamicObstacle.ObstaclePosition => throw new NotImplementedException();

    fp3 IDynamicObstacle.ObstacleDirection => throw new NotImplementedException();

    fp3 IDynamicObstacle.ObstacleSpeed => throw new NotImplementedException();

    IDynamicObstacle IDynamicObstacle.Ingore => throw new NotImplementedException();

    public void RegisterRVOGenerator() => RVOGenerator.Instance.Register(this);

    public void UnregisterRVOGenerator() => RVOGenerator.Instance.Unregister(this);

    #endregion

    public void SyncTransform()
    {
        transform.position = new Vector3((float)logicPosition.x, (float)logicPosition.y, (float)logicPosition.z);
        modelRoot.rotation = new Quaternion(0, (float)logicRotation.x, 0, (float)logicRotation.y);
    }

    public void ApplyRotateByDir()
    {
        if (lockRotateion)
            return;
        fp angle = fpmath.atan2(direction.x, direction.z);

        // 绕 Y 轴旋转的四元数 (0, sin(θ/2), 0, cos(θ/2))
        fp halfAngle = angle / 2;
        fp sinHalf = fpmath.sin(halfAngle);
        fp cosHalf = fpmath.cos(halfAngle);

        var rot = new fp4(0, sinHalf, 0, cosHalf);
        logicRotation = new fp2(rot.y, rot.w);
    }


}

public readonly struct UnitUID : IEquatable<UnitUID>, IComparable<UnitUID>
{
    public readonly int PrefabId;
    public readonly ulong Frame;
    public readonly byte TeamId;
    public readonly byte Sequence;

    public UnitUID(int prefabId, ulong frame, byte teamId, byte sequence)
    {
        PrefabId = prefabId;
        Frame = frame;
        TeamId = teamId;
        Sequence = sequence;
    }

    public bool Equals(UnitUID other) =>
        PrefabId == other.PrefabId &&
        Frame == other.Frame &&
        TeamId == other.TeamId &&
        Sequence == other.Sequence;

    public override bool Equals(object obj) => obj is UnitUID other && Equals(other);

    public override int GetHashCode()
    {
        return HashCode.Combine(PrefabId, Frame, TeamId, Sequence);
    }

    public int CompareTo(UnitUID other)
    {
        int cmp = PrefabId.CompareTo(other.PrefabId);
        if (cmp != 0) return cmp;
        cmp = Frame.CompareTo(other.Frame);
        if (cmp != 0) return cmp;
        cmp = TeamId.CompareTo(other.TeamId);
        if (cmp != 0) return cmp;
        return Sequence.CompareTo(other.Sequence);
    }

    public override string ToString() => $"{PrefabId}:{Frame}:{TeamId}:{Sequence}";
}

public enum UnitActionState : byte
{
    None,
    Idle,// 待机
    Move,// 正常移动
    Track,// 追踪
    Attack,// 攻击
    Casting,// 施法引导中(不能移动和攻击)
}

