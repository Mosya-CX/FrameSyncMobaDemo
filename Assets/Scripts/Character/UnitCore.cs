using UnityEngine;
using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using System;

public abstract class UnitCore : MonoBehaviour
{
    [SerializeField, LabelText("预制体ID"), ReadOnly]
    private int prefabId;
    public int PrefabId => prefabId;

    [SerializeField, LabelText("单位实例ID"), ReadOnly]
    private UnitUID unitId;
    public UnitUID UnitID => unitId;

    [SerializeField, LabelText("团队ID"), ReadOnly]
    private byte teamId;
    public byte TeamID => teamId;

    [SerializeField, LabelText("数值配置")]
    protected UnitPropertyConfig propertyConfig;

    protected UnitStats stats = new();
    public UnitStats Stats => stats;

    [SerializeField, LabelText("当前等级")]
    private int level;

    private AbilityHandler abilityHandler;
    private BuffHandler buffHandler;

    public AbilityHandler AbilityHandler => abilityHandler;
    public BuffHandler BuffHandler => buffHandler;

    private void Awake()
    {
        abilityHandler ??= GetComponent<AbilityHandler>();
        buffHandler ??= GetComponent<BuffHandler>();
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

    public virtual void Tick(fp deltaTime)
    {
        abilityHandler?.Tick(deltaTime);
    }

    public virtual void OnDespawn()
    {
        stats.Clean();
        unitId = default;
    }

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
}

public readonly struct UnitUID : IEquatable<UnitUID>
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

    public override string ToString() => $"{PrefabId}:{Frame}:{TeamId}:{Sequence}";
}

