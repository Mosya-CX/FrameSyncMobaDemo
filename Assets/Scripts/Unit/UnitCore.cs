using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public abstract class UnitCore : MonoBehaviour, IStateful, IDynamicObstacle, IDamageModifierProvider, IHealModifierProvider, IUnitContactListener
{
    #region 基础信息
    [SerializeField, LabelText("预制体ID")]
    public int PrefabId;

    [SerializeField, LabelText("单位实例ID"), ReadOnly]
    private UnitUID unitId;
    public UnitUID UnitID => unitId;

    [SerializeField, LabelText("团队ID"), ReadOnly]
    private byte teamId;
    public byte TeamID => teamId;

    [SerializeField, LabelText("模型根节点")]
    protected Transform modelRoot;

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
    public int Level => level;

    [ShowInInspector, ReadOnly, LabelText("逻辑坐标")]
    protected fp3 logicPosition;
    public fp3 LogicPosition
    {
        get => logicPosition;
        set => logicPosition = value;
    }

    [ShowInInspector, ReadOnly, LabelText("逻辑旋转")]
    protected fp2 logicRotation;
    public fp2 LogicRotation
    {
        get => logicRotation;
        set => logicRotation = value;
    }

    [ShowInInspector, ReadOnly, LabelText("基础运动状态")]
    protected UnitLocomotionState locomotionState = UnitLocomotionState.Idle;
    public UnitLocomotionState LocomotionState => locomotionState;

    public bool IsDead => locomotionState == UnitLocomotionState.Dead;
    #endregion

    #region 运动基础
    [ShowInInspector, ReadOnly, LabelText("移动方向")]
    protected fp3 direction;
    public fp3 Direction => direction;

    protected PathFinder pathFinder;
    #endregion

    #region 额外功能
    protected UnitBaseHandler[] handlers;
    protected BuffHandler buffHandler;
    protected CrowdControlHandler crowdControlHandler;
    protected EquipmentHandler equipmentHandler;

    public BuffHandler BuffHandler => buffHandler;
    public CrowdControlHandler CrowdControlHandler => crowdControlHandler;
    public EquipmentHandler EquipmentHandler => equipmentHandler;
    #endregion

    #region 生命周期
    protected virtual void Awake()
    {
        handlers = GetComponents<UnitBaseHandler>();
        buffHandler = GetComponent<BuffHandler>();
        crowdControlHandler = GetComponent<CrowdControlHandler>();
        equipmentHandler = GetComponent<EquipmentHandler>();
        
        pathFinder = GetComponent<PathFinder>();

        stats.Init(definitionConfig);
        DamageDealt += (damageInfo) =>
        {
            var healAmount = stats.Get(UnitStatType.SpellVamp) * damageInfo.Result.TotalDamage;
            if (damageInfo.Result.Tags.Contains(DamageTagConst.FromAttack))
                healAmount += stats.Get(UnitStatType.LifeSteal) * damageInfo.Result.TotalDamage;
            HealManager.Instance.CreateHealRequest(this, this, healAmount);
        };
    }

    public virtual void OnSpawn(UnitUID instanceUid, int startLevel = 1)
    {
        unitId = instanceUid;
        teamId = instanceUid.TeamId;

        level = startLevel;
        stats.SetLevel(level);

        RegisterRVOGenerator();
        SetLocomotionState(UnitLocomotionState.Idle);
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

    public virtual void Tick(fp dt, uint currentTick)
    {
        buffHandler?.Tick(dt);
        crowdControlHandler?.Tick(dt);
        equipmentHandler?.Tick(dt);

        TickLocomotion(dt);
    }
    #endregion

    #region 基础运动状态
    protected void SetLocomotionState(UnitLocomotionState next)
    {
        if (locomotionState == next)
            return;

        OnLocomotionExit(locomotionState);
        locomotionState = next;
        OnLocomotionEnter(next);
    }

    protected virtual void TickLocomotion(fp dt)
    {
        switch (locomotionState)
        {
            case UnitLocomotionState.Idle:
                OnIdleTick(dt);
                break;

            case UnitLocomotionState.Move:
                OnMoveTick(dt);
                break;

            case UnitLocomotionState.Dead:
                OnDeadTick(dt);
                break;
        }
    }

    private void OnLocomotionEnter(UnitLocomotionState state)
    {
        switch (state)
        {
            case UnitLocomotionState.Idle: OnIdleEnter(); break;
            case UnitLocomotionState.Move: OnMoveEnter(); break;
            case UnitLocomotionState.Dead: OnDeadEnter(); break;
        }
    }

    private void OnLocomotionExit(UnitLocomotionState state)
    {
        switch (state)
        {
            case UnitLocomotionState.Idle: OnIdleExit(); break;
            case UnitLocomotionState.Move: OnMoveExit(); break;
            case UnitLocomotionState.Dead: OnDeadExit(); break;
        }
    }

    protected virtual void OnIdleEnter() { }
    protected virtual void OnIdleTick(fp dt) { }
    protected virtual void OnIdleExit() { }

    protected virtual void OnMoveEnter() { }
    protected virtual void OnMoveTick(fp dt)
    {
        UpdateRotation();
    }
    protected virtual void OnMoveExit()
    {
        pathFinder?.Stop();
    }

    protected virtual void OnDeadEnter() { }
    protected virtual void OnDeadTick(fp dt) { }
    protected virtual void OnDeadExit() { }

    #endregion

    #region 动作通道查询入口
    protected ActionLockSnapshot BuildExternalActionLockSnapshot()
    {
        var snapshot = ActionLockSnapshot.Default;

        for (int i = 0; i < handlers.Length; i++)
        {
            if (handlers[i] is not IActionLockProvider provider)
                continue;

            var lockSnap = provider.BuildActionLockSnapshot();
            snapshot.OccupiedChannels |= lockSnap.OccupiedChannels;
            snapshot.BlockedChannels |= lockSnap.BlockedChannels;
        }

        return snapshot;
    }

    public virtual bool IsActionChannelBlocked(ActionChannelMask channel)
    {
        if (crowdControlHandler != null && crowdControlHandler.CurrentSnapshot.IsChannelBlocked(channel))
            return true;

        var externalLocks = BuildExternalActionLockSnapshot();

        if ((externalLocks.OccupiedChannels & channel) != 0)
            return true;

        return externalLocks.IsBlocked(channel);
    }

    public virtual bool CanStartMove() => !IsActionChannelBlocked(ActionChannelMask.Move);
    public virtual bool CanStartTrack() => !IsActionChannelBlocked(ActionChannelMask.Track);
    public virtual bool CanStartAttack() => !IsActionChannelBlocked(ActionChannelMask.Attack);
    public virtual bool CanStartCast() => !IsActionChannelBlocked(ActionChannelMask.Cast);
    public virtual bool CanStartDash() => !IsActionChannelBlocked(ActionChannelMask.Dash);
    public virtual bool CanRotate() => !IsActionChannelBlocked(ActionChannelMask.Rotate);
    #endregion

    #region 移动基础能力
    public bool IsReach(fp3? targetDestination, fp reachThreshold)
    {
        if (!targetDestination.HasValue)
            return true;

        return fpmath.distance(logicPosition, targetDestination.Value) < reachThreshold;
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

    public virtual void ApplyMove(fp dt, fp3 modifier)
    {
        if (!CanStartMove())
            return;

        var raw = direction + modifier;
        if (fpmath.lengthsq(raw) <= 0)
            return;

        var moveDirection = fpmath.normalize(raw);
        logicPosition += dt * moveDirection * stats.MoveDistancePerSecond;
    }

    public virtual void UpdateRotation()
    {
        if (!CanRotate())
            return;

        if (fpmath.lengthsq(direction) <= 0)
            return;

        fp angle = fpmath.atan2(direction.x, direction.z);
        fp halfAngle = angle / 2;
        fp sinHalf = fpmath.sin(halfAngle);
        fp cosHalf = fpmath.cos(halfAngle);

        var rot = new fp4(0, sinHalf, 0, cosHalf);
        logicRotation = new fp2(rot.y, rot.w);
    }
    #endregion

    #region 战斗相关
    [Serializable]
    public struct RecentAttackerInfo
    {
        public UnitUID AttackerUid;
        public uint LastDamageTick;
        public fp AccumulatedDamage;
    }

    [SerializeField, LabelText("攻击者缓存Tick数")]
    private uint assistRecordDurationTick = 600;
    protected readonly Dictionary<UnitUID, RecentAttackerInfo> recentAttackers = new();
    protected DamageResult lastDamageResultCache;

    public void RecordRecentAttacker(UnitCore attacker, fp damage, uint currentTick)
    {
        if (attacker == null || attacker == this)
            return;

        if (recentAttackers.TryGetValue(attacker.UnitID, out var info))
        {
            info.LastDamageTick = currentTick;
            info.AccumulatedDamage += damage;
            recentAttackers[attacker.UnitID] = info;
        }
        else
        {
            recentAttackers[attacker.UnitID] = new RecentAttackerInfo
            {
                AttackerUid = attacker.UnitID,
                LastDamageTick = currentTick,
                AccumulatedDamage = damage,
            };
        }
    }

    public void CleanupExpiredRecentAttackers(uint currentTick)
    {
        var toRemove = ListPool<UnitUID>.Get();

        foreach (var pair in recentAttackers)
        {
            if (currentTick - pair.Value.LastDamageTick > assistRecordDurationTick)
                toRemove.Add(pair.Key);
        }

        for (int i = 0; i < toRemove.Count; i++)
            recentAttackers.Remove(toRemove[i]);

        ListPool<UnitUID>.Release(toRemove);
    }

    public List<UnitCore> GetCurrentAssistContributors(UnitCore killer, uint currentTick)
    {
        CleanupExpiredRecentAttackers(currentTick);

        var result = new List<UnitCore>();

        foreach (var pair in recentAttackers)
        {
            if (killer != null && pair.Key == killer.UnitID)
                continue;

            if (UnitManager.Instance.Spawns.TryGetValue(pair.Key, out var unit) && unit != null && !unit.IsDead)
                result.Add(unit);
        }

        return result;
    }

    public virtual void ApplyDamageResult(DamageResult result, uint currentTick)
    {
        if (result.Source != null && result.TotalDamage > 0)
            RecordRecentAttacker(result.Source, result.TotalDamage, currentTick);

        stats.ModifyHealth(-result.TotalDamage);
        lastDamageResultCache = result;

        CheckDead(currentTick);
    }

    public virtual void ApplyHealResult(HealResult result, uint currentTick)
    {
        if (result.FinalHeal <= 0)
            return;

        stats.ModifyHealth(result.FinalHeal);
    }

    public virtual void CheckDead(uint currentTick = 0)
    {
        if (Stats.CurrentHealth != 0)
            return;

        if (IsDead)
            return;

        var killer = lastDamageResultCache.Source;
        var assisters = GetCurrentAssistContributors(killer, currentTick);

        OnDying(killer);

        SetLocomotionState(UnitLocomotionState.Dead);

        if (killer != null)
            killer.OnKillPerformed(this);

        for (int i = 0; i < assisters.Count; i++)
            assisters[i].OnAssistPerformed(this, killer);

        OnDeath(killer, assisters);
    }

    public virtual void ModifyOutgoingDamage(DamageContext context)
    {
        if (context == null)
            return;

        for (int i = 0; i < handlers.Length; i++)
        {
            if (handlers[i] is IDamageModifierProvider provider)
                provider.ModifyOutgoingDamage(context);
        }
    }

    public virtual void ModifyIncomingDamage(DamageContext context)
    {
        if (context == null)
            return;

        for (int i = 0; i < handlers.Length; i++)
        {
            if (handlers[i] is IDamageModifierProvider provider)
                provider.ModifyIncomingDamage(context);
        }
    }

    public virtual void ModifyOutgoingHeal(HealContext context)
    {
        if (context == null)
            return;

        for (int i = 0; i < handlers.Length; i++)
        {
            if (handlers[i] is IHealModifierProvider provider)
                provider.ModifyOutgoingHeal(context);
        }
    }

    public virtual void ModifyIncomingHeal(HealContext context)
    {
        if (context == null)
            return;

        for (int i = 0; i < handlers.Length; i++)
        {
            if (handlers[i] is IHealModifierProvider provider)
                provider.ModifyIncomingHeal(context);
        }
    }
    #endregion

    #region 快照和恢复
    public virtual object CaptureState()
    {
        var handlerStates = new object[handlers.Length];
        for (int i = 0; i < handlers.Length; i++)
            handlerStates[i] = handlers[i].CaptureState();

        return new UnitCoreSnapshot
        {
            Position = logicPosition,
            Rotation = logicRotation,
            Level = level,
            LocomotionState = locomotionState,
            Direction = direction,
            StatsSnapshot = stats.Capture(),
            HandlerStates = handlerStates,
            RecentAttackersState = CaptureRecentAttackersState(),
        };
    }

    public virtual void RestoreState(object state)
    {
        var snap = (UnitCoreSnapshot)state;

        logicPosition = snap.Position;
        logicRotation = snap.Rotation;
        SyncTransform();
        level = snap.Level;
        locomotionState = snap.LocomotionState;
        direction = snap.Direction;

        stats.SetLevel(level);
        stats.Restore(snap.StatsSnapshot);

        for (int i = 0; i < handlers.Length; i++)
            handlers[i].RestoreState(snap.HandlerStates[i]);

        RestoreRecentAttackersState(snap.RecentAttackersState);
    }

    [System.Serializable]
    public struct UnitCoreSnapshot
    {
        public fp3 Position;
        public fp2 Rotation;
        public fp3 Direction;
        public UnitLocomotionState LocomotionState;
        public int Level;
        public UnitStats.UnitStatsSnapshot StatsSnapshot;
        public object[] HandlerStates;
        public object RecentAttackersState;
    }

    [System.Serializable]
    public struct RecentAttackerInfoSnapshot
    {
        public UnitUID AttackerUid;
        public uint LastDamageTick;
        public fp AccumulatedDamage;
    }

    protected object CaptureRecentAttackersState()
    {
        var list = new List<RecentAttackerInfoSnapshot>(recentAttackers.Count);
        foreach (var pair in recentAttackers)
        {
            list.Add(new RecentAttackerInfoSnapshot
            {
                AttackerUid = pair.Value.AttackerUid,
                LastDamageTick = pair.Value.LastDamageTick,
                AccumulatedDamage = pair.Value.AccumulatedDamage,
            });
        }
        return list.ToArray();
    }

    protected void RestoreRecentAttackersState(object state)
    {
        recentAttackers.Clear();

        if (state is not RecentAttackerInfoSnapshot[] snaps)
            return;

        for (int i = 0; i < snaps.Length; i++)
        {
            var s = snaps[i];
            recentAttackers[s.AttackerUid] = new RecentAttackerInfo
            {
                AttackerUid = s.AttackerUid,
                LastDamageTick = s.LastDamageTick,
                AccumulatedDamage = s.AccumulatedDamage,
            };
        }
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

    #region IDynamicObstacle
    public fp3 ObstaclePosition => logicPosition;
    public fp3 ObstacleDirection => direction;
    public fp ObstacleSpeed => stats.MoveDistancePerSecond;
    public IDynamicObstacle Ingore => null;

    public void RegisterRVOGenerator() => RVOGenerator.Instance.Register(this);
    public void UnregisterRVOGenerator() => RVOGenerator.Instance.Unregister(this);
    #endregion

    #region 回调事件
    public event Action<DamageDealtEvent> DamageDealt;
    public event Action<DamageTakenEvent> DamageTaken;
    public event Action<HealDealtEvent> HealDealt;
    public event Action<HealTakenEvent> HealTaken;
    public event Action<AttackEvent> AttackPerformed;
    public event Action<AbilityCastStageEvent> AbilityCastStagePerformed;
    public event Action<KillEvent> KillPerformed;
    public event Action<AssistEvent> AssistPerformed;
    public event Action<DyingEvent> Dying;
    public event Action<DeathEvent> Death;
    public event Action<ContactEvent> Contact;

    public virtual void OnDamageDealt(DamageResult result)
    {
        DamageDealt?.Invoke(new DamageDealtEvent(result.Source, result.Target, result));
    }

    public virtual void OnDamageTaken(DamageResult result)
    {
        DamageTaken?.Invoke(new DamageTakenEvent(result.Source, result.Target, result));
    }

    public virtual void OnHealDealt(HealResult result)
    {
        HealDealt?.Invoke(new HealDealtEvent(result.Source, result.Target, result));
    }

    public virtual void OnHealTaken(HealResult result)
    {
        HealTaken?.Invoke(new HealTakenEvent(result.Source, result.Target, result));
    }

    public virtual void OnAttackPerformed(UnitCore target)
    {
        AttackPerformed?.Invoke(new AttackEvent(this, target));
    }

    public virtual void OnKillPerformed(UnitCore victim)
    {
        KillPerformed?.Invoke(new KillEvent(this, victim));
    }

    public virtual void OnAssistPerformed(UnitCore victim, UnitCore killer)
    {
        AssistPerformed?.Invoke(new AssistEvent(this, victim, killer));
    }

    public virtual void OnDying(UnitCore killer)
    {
        Dying?.Invoke(new DyingEvent(this, killer));
    }

    public virtual void OnDeath(UnitCore killer, IReadOnlyList<UnitCore> assisters)
    {
        Death?.Invoke(new DeathEvent(this, killer, assisters));
    }

    public void OnUnitContact(UnitContactEventType eventType, UnitCore other)
    {
        Contact?.Invoke(new ContactEvent(this, other, eventType));
    }
    #endregion

    #region 表现

    public virtual void SyncTransform()
    {
        transform.position = new Vector3((float)logicPosition.x, (float)logicPosition.y, (float)logicPosition.z);

        if (modelRoot != null)
            modelRoot.rotation = new Quaternion(0, (float)logicRotation.x, 0, (float)logicRotation.y);
    }
    #endregion

    public virtual void ResetStats()
    {
    }  

    public virtual SimulationEntityType SimulationEntityType => SimulationEntityType.None;

    public SimulationTeamMask SimulationTeamMask
    {
        get
        {
            return TeamID switch
            {
                1 => SimulationTeamMask.Neutral,
                2 => SimulationTeamMask.Blue,
                3 => SimulationTeamMask.Red,
                _ => SimulationTeamMask.None,
            };
        }
    }
}

public enum UnitLocomotionState : byte
{
    None,
    Idle,
    Move,
    Dead,
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

    public override int GetHashCode() => HashCode.Combine(PrefabId, Frame, TeamId, Sequence);

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

public enum UnitDamageCallbackType : byte
{
    OnDamageDealt,
    OnDamageTaken,
    OnKill,
    OnDeath,
    OnDying,
}

