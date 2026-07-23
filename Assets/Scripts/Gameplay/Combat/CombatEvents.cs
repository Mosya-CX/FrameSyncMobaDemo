using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Combat-related UnitEventBus events (Combat v13.2 section 1.6).
    /// Dual-role: global static bus for systems without per-Unit access,
    /// AND auto-forwarder to per-Unit EventBus when TryResolveUnit is set (Unit v27.3 §6).
    /// </summary>
    public static class CombatEvents
    {
        /// <summary>Optional resolver to forward events to per-Unit EventBus.</summary>
        public static System.Func<UnitUid, Unit> TryResolveUnit;
        /// <summary>Called when a player participates in combat (used for shop undo invalidation).</summary>
        public static System.Action<UnitUid, UnitUid, CombatParticipationFlags> OnCombatParticipationUnit;
        public static System.Action<int, CombatParticipationFlags> OnCombatParticipation;

        public delegate void UnitDeathEventHandler(UnitUid victimUid, UnitUid killerUid);
        private static UnitDeathEventHandler _onUnitDeath;
        private static UnitDeathEventHandler _onUnitKill;
        private static UnitDeathEventHandler _onUnitAssist;

        public static event UnitDeathEventHandler OnUnitDeath
        { add { _onUnitDeath += value; } remove { _onUnitDeath -= value; } }
        public static event UnitDeathEventHandler OnUnitKill
        { add { _onUnitKill += value; } remove { _onUnitKill -= value; } }
        public static event UnitDeathEventHandler OnUnitAssist
        { add { _onUnitAssist += value; } remove { _onUnitAssist -= value; } }

        public delegate void DamageEventHandler(DamageEventData data);
        public delegate void HealEventHandler(HealEventData data);
        public delegate void ShieldEventHandler(ShieldEventData data);

        private static DamageEventHandler _onDamageTaken;
        private static DamageEventHandler _onDamageDealt;
        private static HealEventHandler _onHealTaken;
        private static HealEventHandler _onHealDealt;
        private static ShieldEventHandler _onShieldApplied;

        public static event DamageEventHandler OnDamageTaken
        { add { _onDamageTaken += value; } remove { _onDamageTaken -= value; } }
        public static event DamageEventHandler OnDamageDealt
        { add { _onDamageDealt += value; } remove { _onDamageDealt -= value; } }
        public static event HealEventHandler OnHealTaken
        { add { _onHealTaken += value; } remove { _onHealTaken -= value; } }
        public static event HealEventHandler OnHealDealt
        { add { _onHealDealt += value; } remove { _onHealDealt -= value; } }
        public static event ShieldEventHandler OnShieldApplied
        { add { _onShieldApplied += value; } remove { _onShieldApplied -= value; } }

        private static void TryForwardToTarget(UnitUid uid, System.Action<Unit> publish)
        {
            var resolver = TryResolveUnit;
            if (resolver == null) return;
            var unit = resolver(uid);
            if (unit != null) publish(unit);
        }

        internal static void RaiseDamageTaken(DamageEventData data)
        {
            _onDamageTaken?.Invoke(data);
            TryForwardToTarget(data.TargetUid, u => u.EventBus?.PublishDamageTaken(data));
        }

        internal static void RaiseDamageDealt(DamageEventData data)
        {
            _onDamageDealt?.Invoke(data);
            TryForwardToTarget(data.SourceUid, u => u.EventBus?.PublishDamageDealt(data));
        }

        internal static void RaiseHealTaken(HealEventData data)
        {
            _onHealTaken?.Invoke(data);
            TryForwardToTarget(data.TargetUid, u => u.EventBus?.PublishHealTaken(data));
        }

        internal static void RaiseHealDealt(HealEventData data)
        {
            _onHealDealt?.Invoke(data);
            TryForwardToTarget(data.SourceUid, u => u.EventBus?.PublishHealDealt(data));
        }

        internal static void RaiseShieldApplied(ShieldEventData data)
        {
            _onShieldApplied?.Invoke(data);
            TryForwardToTarget(data.TargetUid, u => u.EventBus?.PublishShieldApplied(data));
        }

        internal static void RaiseUnitDeath(UnitUid victim, UnitUid killer)
        {
            _onUnitDeath?.Invoke(victim, killer);
            TryForwardToTarget(victim, u => u.EventBus?.PublishUnitDeath(u));
        }

        internal static void RaiseUnitKill(UnitUid killer, UnitUid victim)
        {
            _onUnitKill?.Invoke(killer, victim);
            Unit victimUnit = TryResolveUnit?.Invoke(victim);
            TryForwardToTarget(killer, u => u.EventBus?.PublishUnitKill(victimUnit));
        }

        internal static void RaiseUnitAssist(UnitUid assistant, UnitUid victim)
        {
            _onUnitAssist?.Invoke(assistant, victim);
        }

        internal static void RaiseLevelUp(UnitUid unitUid, int previousLevel, int newLevel)
        {
            TryForwardToTarget(unitUid, u => u.EventBus?.PublishLevelUp(previousLevel, newLevel));
        }

        public static void Clear()
        {
            _onDamageTaken = null;
            _onDamageDealt = null;
            _onHealTaken = null;
            _onHealDealt = null;
            _onShieldApplied = null;
            TryResolveUnit = null;
            OnCombatParticipation = null;
            OnCombatParticipationUnit = null;
            _onUnitDeath = null;
            _onUnitKill = null;
            _onUnitAssist = null;
        }
    }

    public struct DamageEventData
    {
        public UnitUid SourceUid;
        public UnitUid TargetUid;
        public fp RawDamage;
        public fp MitigatedDamage;
        public fp ActualDamage;
        public DamageType DamageType;
    }

    public struct HealEventData
    {
        public UnitUid SourceUid;
        public UnitUid TargetUid;
        public fp RawHeal;
        public fp EffectiveHeal;
    }

    public struct ShieldEventData
    {
        public UnitUid SourceUid;
        public UnitUid TargetUid;
        public fp ShieldAmount;
        public ShieldType ShieldType;
    }
}
