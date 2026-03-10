using UnityEngine;
using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using System;
using System.Collections.Generic;

public abstract class UnitCore : MonoBehaviour, IStateful, IDynamicObstacle
{
    #region 基础信息
    [SerializeField, LabelText("预制体ID"), ReadOnly]
    public int PrefabId;

    [SerializeField, LabelText("单位实例ID"), ReadOnly]
    private UnitUID unitId;
    public UnitUID UnitID => unitId;

    [SerializeField, LabelText("团队ID"), ReadOnly]
    private byte teamId;
    public byte TeamID => teamId;

    [SerializeField, LabelText("模型根节点")]
    private Transform modelRoot;

    [SerializeField, LabelText("单位大小半径")]
    public fp unitSizeRadius = 0.3m;

    [SerializeField, LabelText("单位定义配置")]
    public UnitDefinition definitionConfig;
    #endregion

    #region 单位状态
    protected UnitStats stats = new();
    public UnitStats Stats => stats;

    [SerializeField, LabelText("当前等级")]
    protected int level;

    [ShowInInspector, ReadOnly, LabelText("逻辑坐标")]
    protected fp3 logicPosition;// 实际只有xz有用，但是为了方便运算就使用fp3
    public fp3 LogicPosition
    {
        get => logicPosition;
        set => logicPosition = value;
    }

    [ShowInInspector, ReadOnly, LabelText("逻辑旋转")]
    protected fp2 logicRotation;// x代表四元数的y，y代表四元数的w
    public fp2 LogicRotation
    {
        get => logicRotation;
        set => logicRotation = value;
    }

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

    [SerializeField, LabelText("攻击投掷物")]
    protected AttackMissle attackMisslePrefab;
    [SerializeField, LabelText("基础攻击前摇时长")]
    private float baseAttackWindupDuration = 0.2f;
    [SerializeField, LabelText("基础攻击后摇时长")]
    private float baseAttackRecoveryDuration = 0.8f;
    private fp attackPrecastTimer;
    private fp attackRecoveryTimer;
    public bool IsInAttackRecovery => attackRecoveryTimer > 0;

    protected int reviveRecoveryTickRemaining;// 复活回血Tick次数
    protected fp reviveRecoveryTickInterval;// 复活回血Tick触发间隔
    protected fp reviveRecoveryTickIntervalTimer;// 回血Tick计时器
    protected fp reviveRecoveryHealAmountPerTick = 99999;// 复活每Tick回多少血
    protected fp reviveRecoveryManaRestorationPerTick = 99999;// 复活每Tick回多少蓝
    #endregion

    #region 行为限制
    public UnitCapability capability = UnitCapability.All;
    public bool IsInControlSiffness => CrowdControlHandler.IsInControlSiffness();
    public bool IsInAbilityPrecast => AbilityHandler.IsInAbilityPrecast();
    //public bool lockInput;// 禁用状态转移
    //public bool lockMove;// 禁用移动
    //public bool lockRotateion;// 禁用更新旋转
    //public bool lockAttack;// 禁用攻击(但可以追踪目标)
    //public bool lockCasting;// 禁用施法
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

        stats.Init(definitionConfig);
    }

    public virtual void OnSpawn(UnitUID instanceUid, int startLevel = 1)
    {
        unitId = instanceUid;
        teamId = instanceUid.TeamId;

        level = startLevel;
        stats.SetLevel(level);

        RegisterRVOGenerator();
    }

    public virtual void OnDespawn()
    {
        stats.Clean();
        unitId = default;

        UnregisterRVOGenerator();
    }

    private void Update()
    {
        UpdateAnimation();
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

        if (IsInAttackRecovery)
            attackRecoveryTimer -= dt;

        switch (currentActionState)
        {
            case UnitActionState.Idle:
                OnIdleTick(dt);
                break;
            case UnitActionState.Move:
                if (capability.HasFlag(UnitCapability.Move))
                    break;
                OnMoveTick(dt);   
                break;
            case UnitActionState.Track:
                if (capability.HasFlag(UnitCapability.Track))
                    break;
                OnTrackTick(dt);
                break;
            case UnitActionState.Attack:
                if (capability.HasFlag(UnitCapability.Attack))
                    break;
                OnAttackTick(dt);
                break;
            case UnitActionState.Revive:
                OnReviveTick(dt);
                break;
        }
    }

    #endregion

    #region 状态机部分
    protected void ChangeActionState(UnitActionState nextState)
    {
        if (nextState == currentActionState) 
            return;

        switch (nextState)
        {
            case UnitActionState.Move:
                if (capability.HasFlag(UnitCapability.Move))
                {
                    currentDestination = null;
                    return;
                }   
                break;
            case UnitActionState.Attack:
                if (capability.HasFlag(UnitCapability.Attack))
                {
                    currentTarget = null;
                    return;
                }
                break;
            case UnitActionState.Track:
                if (capability.HasFlag(UnitCapability.Track))
                {
                    currentTarget = null;
                    return;
                }
                break;
        }

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
            case UnitActionState.Dead:
                OnDeadExit();
                break;
            case UnitActionState.Revive:
                OnReviveExit();
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
            case UnitActionState.Dead:
                OnDeadEnter();
                break;
            case UnitActionState.Revive:
                OnReviveEnter();
                break;
            case UnitActionState.Siffness:
                OnSiffnessEnter();
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
            currentDestination = null;
            ChangeActionState(UnitActionState.Idle);
            return;
        }

        UpdateRotation();
    }
    protected virtual void OnMoveExit()
    {
        pathFinder.Stop();
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
        if (IsReach(currentTarget.logicPosition, stats.AttackDistance))
        {
            ChangeActionState(UnitActionState.Attack);
            return;
        }

        UpdateRotation();
    }

    protected virtual void OnTrackExit()
    {
        pathFinder.Stop();
    }
    #endregion
    
    #region Attack
    protected virtual void OnAttackEnter()
    {
        attackPrecastTimer = stats.AttackInterval * (fp)baseAttackWindupDuration;
    }

    protected virtual void OnAttackTick(fp dt)
    {
        if (currentTarget == null)
        {
            ChangeActionState(UnitActionState.Idle);
            return;
        }
        if (!IsReach(currentTarget.logicPosition, 0.1m))
        {
            ChangeActionState(UnitActionState.Track);
            return;
        }

        direction = fpmath.normalize(currentTarget.logicPosition - logicPosition);
        UpdateRotation();

        if (!IsInAttackRecovery)
        {
            attackPrecastTimer -= dt;
            if (attackPrecastTimer <= 0)
            {
                attackPrecastTimer = stats.AttackInterval * (fp)baseAttackWindupDuration;
                attackRecoveryTimer = stats.AttackInterval * (fp)baseAttackRecoveryDuration;
                ExecuteAttack();
            }
        }
    }

    protected virtual void OnAttackExit()
    {
        
    }
    #endregion

    #region Dead
    public virtual void CheckDead()
    {
        if (Stats.CurrentHealth == 0)
        {
            TriggerDamageCallback(UnitDamageCallbackType.OnDying, lastDamageInfoCache);
            if (currentActionState == UnitActionState.Revive)
                return;

            TriggerDamageCallback(UnitDamageCallbackType.OnDeath, lastDamageInfoCache);
            ChangeActionState(UnitActionState.Dead);
            lastDamageInfoCache.Source?.TriggerDamageCallback(UnitDamageCallbackType.OnKill, lastDamageInfoCache);
            // TODO向UnitManager申请死亡事件请求
        }
    }

    protected virtual void OnDeadEnter()
    {
        // TODO
        // 结束仍在激活的技能
        // 清除非永久性的Buff
        // 清除控制

        capability = UnitCapability.None;
    }

    protected virtual void OnDeadExit()
    {
        capability = UnitCapability.All;
        ResetStats();

        
    }
    #endregion

    #region Revive
    public void SetRevive(int recoveryTickCount, fp recoveryTickInterval, fp healAmountPerTick, fp manaRestorationPerTick)
    {
        if (stats.CurrentHealth > 0)
            return;

        reviveRecoveryTickRemaining = recoveryTickCount;
        reviveRecoveryTickInterval = recoveryTickInterval;
        reviveRecoveryTickIntervalTimer = 0;
        reviveRecoveryHealAmountPerTick = healAmountPerTick;
        reviveRecoveryManaRestorationPerTick = manaRestorationPerTick;

        ChangeActionState(UnitActionState.Revive);
    }

    protected virtual void OnReviveEnter()
    {
        // TODO
        // 结束仍在激活的技能
        // 清除非永久性的Buff
        // 清除控制

        capability = UnitCapability.None;
    }

    protected virtual void OnReviveTick(fp dt)
    {
        if (reviveRecoveryTickRemaining <= 0)
        {
            ChangeActionState(UnitActionState.Idle);
            reviveRecoveryTickRemaining = 0;
            return;
        }
        
        if (reviveRecoveryTickIntervalTimer > 0)
            reviveRecoveryTickIntervalTimer -= dt;
        else
        {
            reviveRecoveryTickIntervalTimer = reviveRecoveryTickInterval;
            stats.ModifyHealth(reviveRecoveryHealAmountPerTick);
            stats.ModifyMana(reviveRecoveryManaRestorationPerTick);
            reviveRecoveryTickRemaining--;
        }
    }

    protected virtual void OnReviveExit()
    {
        capability = UnitCapability.All;
    }
    #endregion

    #region Siffness
    protected virtual void OnSiffnessEnter()
    {
        currentDestination = null;
        currentTarget = null;
    }
    #endregion

    #endregion

    #region 寻路

    protected bool IsReach(fp3? targetDestination, fp reachThshold)
    {
        if (!targetDestination.HasValue) return true;
        return fpmath.distance(logicPosition, targetDestination.Value) < reachThshold;
    }

    public virtual void UpdateMoveDirection()
    {
        if (pathFinder != null)
            direction = pathFinder.GetDirection();
    }

    public virtual void UpdateAStarPath()
    {
        if (pathFinder != null)
            pathFinder.UpdatePath();
    }
    #endregion

    #region 攻击和受伤

    public virtual void ExecuteAttack()
    {
        if (attackMisslePrefab)
        {
            MissleManager.Instance.CreateNewMissleRequest(
                attackMisslePrefab.PrefabID,
                new TargetTrackMissleInitialData(this, currentTarget));
        }
        else
            DamageManager.Instance.CreateAttackDamageRequest(this, currentTarget);
    }

    public virtual void GetDamage(in DamageInfo info)
    {
        info.Source?.TriggerDamageCallback(UnitDamageCallbackType.OnDamageDealt, info);
        TriggerDamageCallback(UnitDamageCallbackType.OnDamageTaken, info);
        stats.ModifyHealth(-info.GetTotal());
        lastDamageInfoCache = info;
    }
    #endregion

    #region 快照和恢复

    public object CaptureState()
    {
        var handlerStates = new object[handlers.Length];
        for (int i = 0; i < handlers.Length; i++)
            handlerStates[i] = handlers[i].CaptureState();

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
            handlers[i].RestoreState(snap.HandlerStates[i]);
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

    public fp ObstacleSpeed => stats.MoveDistancePerSecond;

    public IDynamicObstacle Ingore => currentTarget;

    public void RegisterRVOGenerator() => RVOGenerator.Instance.Register(this);

    public void UnregisterRVOGenerator() => RVOGenerator.Instance.Unregister(this);

    #endregion

    #region 回调事件
    public DamageModifier DamageModifier;

    protected DamageInfo lastDamageInfoCache;
    private Dictionary<UnitDamageCallbackType, List<DamageCallback>> damageCallbacks = new Dictionary<UnitDamageCallbackType, List<DamageCallback>>
    {
        {UnitDamageCallbackType.OnDamageDealt, new()},
        {UnitDamageCallbackType.OnDamageTaken, new()},
        {UnitDamageCallbackType.OnKill, new()},
        {UnitDamageCallbackType.OnDeath, new()},
        {UnitDamageCallbackType.OnDying, new()},
    };

    public void RegisterDamageCallback(UnitDamageCallbackType type, DamageCallback callback)
    {
        if (damageCallbacks.TryGetValue(type, out var callbackList))
            callbackList.Add(callback);
    }

    public void UnregisterDamageCallback(UnitDamageCallbackType type, DamageCallback callback)
    {
        if (damageCallbacks.TryGetValue(type, out var callbackList))
            callbackList.Remove(callback);
    }

    public void UnregisterDamageCallback(DamageCallback callback)
    {
        foreach (var callbackList in damageCallbacks.Values)
            callbackList.Remove(callback);
    }

    protected void TriggerDamageCallback(UnitDamageCallbackType type, in DamageInfo info)
    {
        if (damageCallbacks.TryGetValue(type, out var callbackList))
            for (int i = 0; i < callbackList.Count; i++)
                callbackList[i].Invoke(info);

        buffHandler.OnDamageCallback(type, info);
        abilityHandler.OnDamageCallback(type, info);
        crowdControlHandler.OnDamageCallback(type, info);
        equipmentHandler.OnDamageCallback(type, info);
    }

    #endregion

    // TODO 自制动画播放器
    #region 动画机
    public void UpdateAnimation()
    {

    }
    #endregion

    public void SyncTransform()
    {
        transform.position = new Vector3((float)logicPosition.x, (float)logicPosition.y, (float)logicPosition.z);
        modelRoot.rotation = new Quaternion(0, (float)logicRotation.x, 0, (float)logicRotation.y);
    }

    public void ApplyMove(fp dt, fp3 modifier)
    {
        if (IsInControlSiffness || IsInAbilityPrecast) return;

        var moveDirection = fpmath.normalize(direction + modifier);
        logicPosition += dt * moveDirection * stats.MoveDistancePerSecond;
    }

    public void UpdateRotation()
    {
        if (IsInControlSiffness || IsInAbilityPrecast) return;

        fp angle = fpmath.atan2(direction.x, direction.z);

        // 绕 Y 轴旋转的四元数 (0, sin(θ/2), 0, cos(θ/2))
        fp halfAngle = angle / 2;
        fp sinHalf = fpmath.sin(halfAngle);
        fp cosHalf = fpmath.cos(halfAngle);

        var rot = new fp4(0, sinHalf, 0, cosHalf);
        logicRotation = new fp2(rot.y, rot.w);
    }

    public void ResetStats()
    {
        // TODO
        // 重置当前状态

    }
}

public readonly struct UnitUID : IEquatable<UnitUID>, IComparable<UnitUID>
{
    public readonly int PrefabId;
    public readonly uint Frame;
    public readonly byte TeamId;
    public readonly byte Sequence;

    public UnitUID(int prefabId, uint frame, byte teamId, byte sequence)
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

    public static bool operator ==(UnitUID left, UnitUID right) => left.Equals(right);
    public static bool operator !=(UnitUID left, UnitUID right) => !left.Equals(right);

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
    Dead,// 死亡状态
    Revive,// 复活状态
    Siffness,// 僵直状态
}

[Flags]
public enum UnitCapability
{
    None,
    Move = 1,
    Track = 2,
    Attack = 3,
    Cast = 4,
    Dash = 5,
    All = Move | Track | Attack | Cast | Dash,
}

public enum UnitDamageCallbackType : byte
{
    OnDamageDealt,
    OnDamageTaken,
    OnKill,
    OnDeath,
    OnDying,
}

