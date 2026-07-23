# ExecPlan 0037 – Buff/Equipment Passive Rebuild on Respawn

> **Design authority**: `Docs/Design/BuffSystem_Design_v14_2_PermanentBuffRespawnPatch.md`, `Docs/Design/moba_equipment_shop_gold_system_design_v12.md`
> **Estimated code**: ~200 lines
> **Dependencies**: BuffHandler, EquipmentHandler, Unit.ClearForDeath/ClearForRespawn

## Rationale

BuffHandler.ClearForRespawn was incorrectly calling OnAdded on all buffs after respawn. Per Buff v14.2, only permanent buffs that survived death should have their life-stage handles rebuilt via effect.ClearForRespawn(). Similarly, BuffEffect had no ClearForDeath/ClearForRespawn entry points, so permanent buff effects were going through OnRemoved during death instead of properly releasing life-stage handles.

## Scope – Modified files

| File | Lines | Change |
|---|---|---|
| `Unit/Buff/BuffEffect.cs` | +40 | Add virtual ClearForDeath/ClearForRespawn; StatModifierBuffEffect and CombatModifierBuffEffect overrides |
| `Unit/Buff/BuffHandler.cs` | +30 | ClearForDeath: use effects[i].ClearForDeath() for permanent buffs; ClearForRespawn: only process permanent buffs, call effects[i].ClearForRespawn(); add helper methods |

## Key conformance

- Buff v14.2 §1.9.1: permanent buffs retain Runtime, release life-stage handles via ClearForDeath
- Buff v14.2 §1.9.2: only permanent buffs processed in ClearForRespawn; no Added/Reapplied/StackChanged/Removed
- Buff v14.2 §9.3: StatModifierEffect.ClearForRespawn rebuilds modifier if handle invalid
- Equipment v12: EquipmentHandler already had correct ClearForDeath/ClearForRespawn
