using UnityEngine;
using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using System;

public abstract class UnitCore : MonoBehaviour, IStateful
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
    protected fp2 moveDirection;
    protected PathFinder pathFinder;
    
    protected fp3? destination;
    protected UnitCore currentTarget;
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
    }

    public virtual void OnDespawn()
    {
        stats.Clean();
        unitId = default;
    }

    private void LateUpdate()
    {
        SyncTransform();
    }

    public void SyncTransform()
    {
        transform.position = new Vector3((float)logicPosition.x, (float)logicPosition.y, (float)logicPosition.z);
        transform.rotation = new Quaternion(0, (float)logicRotation.x, 0, (float)logicRotation.y);
    }

    protected void Tick(fp dt)
    {
        buffHandler.Tick(dt);
        abilityHandler.Tick(dt);
        crowdControlHandler.Tick(dt);
        equipmentHandler.Tick(dt);

        switch (currentActionState)
        {
            case UnitActionState.Move:
                UpdateMoveDirection(dt);
                ApplyRotateByDir(moveDirection);
                break;
            case UnitActionState.Track:
                UpdateMoveDirection(dt);
                ApplyRotateByDir(moveDirection);
                break;
            case UnitActionState.Attack:
                var dir = (currentTarget.transform.position - transform.position).normalized;
                ApplyRotateByDir(new fp2((fp)dir.x, (fp)dir.z));
                break;
            case UnitActionState.Casting:
                // 暂时什么都做不了
                break;
        }
        ApplyMove(dt);
    }

    #region 状态切换
    protected void ChangeActionState(UnitActionState nextState)
    {
        OnStateExit(currentActionState);
        OnStateEnter(nextState);
        currentActionState = nextState;
    }

    private void OnStateExit(UnitActionState current)
    {

    }

    private void OnStateEnter(UnitActionState next)
    {

    }
    #endregion

    #region 寻路
    private void UpdateMoveDirection(fp dt)
    {
        // TODO通过PathFind更新moveDirection
        // 根据当前状态选择不同的寻路方式
        
    }

    public void ApplyRotateByDir(fp2 xzDir)
    {
        if (lockRotateion)
            return;

        var rot = TurnXZDirectionToRotation(xzDir);
        logicRotation = new fp2(rot.y, rot.w);
    }

    public void ApplyMove(fp dt)
    {
        if (lockMove) 
            return;

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

    private fp4 TurnXZDirectionToRotation(fp2 dirXZ)
    {
        fp angle = fpmath.atan2(dirXZ.x, dirXZ.y); 

        // 绕 Y 轴旋转的四元数 (0, sin(θ/2), 0, cos(θ/2))
        fp halfAngle = angle / 2;
        fp sinHalf = fpmath.sin(halfAngle);
        fp cosHalf = fpmath.cos(halfAngle);

        return new fp4(0, sinHalf, 0, cosHalf);
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

