# ExecPlan 0118: Presentation event history and shop view

> Status: Complete (2026-07-28).
> Parent: `0109_design_conformance_remediation_program_execplan.md`, Gate 9.

## Purpose

Prevent rollback/replay from duplicating consumed one-shot presentation events,
preserve distinct VFX/SFX semantics, and expose current spendable gold through
the formal read-only shop view.

## Design sources

- `Docs/Design/moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md`,
  section 8.
- `Docs/Design/MOBA_UI_Lua_System_Design_v9_1_GoldIncomeRuntime_Aligned.md`,
  sections 1.4-1.7, 5.7 and 5.13.
- `Docs/Design/moba_equipment_shop_gold_system_design_v12.md`, sections 7.6-7.7.

## Scope and result

- Deduplicate on the complete `PresentationEventId`.
- Keep separate bounded VFX and SFX consumption histories across rendered
  frames and replay.
- Give combat hit/death events stable keys and deterministic sequences; route
  death VFX and SFX through separate streams.
- Remove the unused cross-stream `PresentationSyncManager`.
- Add `IConfirmedGoldIncomeView`, `IEquipmentShopView` and a player-bound
  `EquipmentShopView`; derive current gold from confirmed income plus effective
  non-reverted shop delta.
- Keep all presentation/UI state out of Gameplay Snapshot and checksum.

## Validation

- Unity MCP compilation passed.
- EditMode: `PresentationEventDispatcherTests` 3/3,
  `EquipmentShopViewTests` 1/1 and `CombatSystemTests` 10/10.
- No PlayMode test was required because no scene, Input System, transform
  synchronization or live playback lifecycle was changed.
