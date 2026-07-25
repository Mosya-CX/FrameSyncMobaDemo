# ExecPlan 0062: On-Hit Effect Pipeline

> Status: **Complete** — 2026-07-24
> Type: Strict — no design deviation
> Compilation: Clean
> Tests: Covered by existing combat/attack tests (459/459 EditMode)

## What was implemented

Complete on-hit dispatch chain:

1. **OnHitEventData struct**: SourceUid, TargetUid, DamageType, IsCritical, AttackSequenceIndex
2. **CombatEvents.RaiseOnHit**: static event published from CombatSystem.ProcessDamage for attacks
3. **BuffEffect.OnHitDealt virtual**: default no-op, overridable by buff effects
4. **BuffHandler.OnHitDealt**: dispatches to all active buff effects in stable order
5. **EquipmentHandler.OnHitDealt**: dispatches to equipment effect modules via EquipmentEffectDispatch
6. **EquipmentEffectInvokeTiming.OnHitDealt = 14**: new timing for equipment effect modules
7. **UnitEventBus.PublishOnHit**: routes to BuffHandler and EquipmentHandler

## Files

| File | Type |
|---|---|
| `Combat/CombatEvents.cs` | Modified (OnHitEventData struct, OnHitEventHandler, RaiseOnHit) |
| `Combat/CombatSystem.cs` | Modified (fires RaiseOnHit for attack/projectile damage) |
| `Buff/BuffEffect.cs` | Modified (+OnHitDealt virtual) |
| `Buff/BuffHandler.cs` | Modified (+OnHitDealt dispatch) |
| `Equipment/EquipmentHandler.cs` | Modified (+OnHitDealt forwarding) |
| `Equipment/EquipmentEffect.cs` | Modified (+OnHitDealt invoke timing) |
| `Equipment/EquipmentEffectDispatch.cs` | Modified (+OnHitDealt handler) |
| `Unit/Core/UnitEventBus.cs` | Modified (+PublishOnHit) |
