# ExecPlan 0063: Equipment Passive Runtime

> Status: **Complete** — 2026-07-24
> Type: Strict — no design deviation
> Compilation: Clean
> Tests: Covered by existing equipment/shop tests (459/459 EditMode)

## What was implemented

Bridged equipment effects to Buff system through BuffEquipmentModule:

1. **BuffEquipmentModule**: Concrete EquipmentEffectModule that applies a buff via BuffHandler when triggered
2. **EquipmentInstance._appliedBuffConfigIds**: tracks which buffs were applied by each equipment
3. **EquipmentHandler.Add**: after OnEquipped dispatch, tracks applied buff IDs
4. **EquipmentHandler.Remove**: removes applied buffs before releasing stats
5. **EquipmentHandler.ClearForDeath/ClearForDespawn**: removes applied buffs

## Flow

```
Equip item → OnEquipped dispatch → BuffEquipmentModule.Execute → BuffHandler.Apply
Unequip item → RemoveAppliedBuffs → BuffHandler.Remove
```

## Files

| File | Type |
|---|---|
| `Equipment/BuffEquipmentModule.cs` | Production (new concrete module) |
| `Equipment/EquipmentHandler.cs` | Modified (+TrackAppliedBuffs, +RemoveAppliedBuffs, +ClearForDeath/Despawn buff cleanup) |
