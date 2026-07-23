# FrameSyncMobaDemo — Authoritative Design Index

> Purpose: This file is the single index of current formal design documents.  
> Codex must not infer the active version from filenames elsewhere in the repository.

## Source-of-truth order

When requirements conflict:

1. Current user task.
2. `Docs/Architecture/DECISION_LOG.md`.
3. This index.
4. Current formal design documents listed below.
5. Existing code.
6. Comments and examples.

A document not listed as **Current** here is not an implementation authority.

## Current formal designs

| Domain | Current design | Status | Notes |
|---|---|---|---|
| FrameSync / flow / match runtime | `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md` | Current | Also owns application flow, AuthorityFrame, recovery, prediction and match result boundaries |
| Snapshot / rollback schema | `Docs/Design/FrameSync_Snapshot_Contents_Appendix_v7_2.md` | Current | Exact snapshot membership and restore phases |
| Unit behavior framework | `Docs/Design/unit_behavior_framework_design_v27_3.md` | Current | UnitWorld, Handler ownership, AI controller lifecycle, action framework |
| Combat | `Docs/Design/moba_combat_system_design_v13_2.md` | Current | Settlement, formal death, contribution tracking, deferred death/kill reaction requests |
| Projectile | `Docs/Design/MOBA_FrameSync_Unity_Projectile_System_Design_v19.md` | Current | Projectile UID, world lifecycle, snapshot and hit pipeline |
| Ability | `Docs/Design/moba_ability_system_design_v15_2.md` | Current | Ability Runtime, Session, Stage, signal language and indicator-stage source |
| Attack | `Docs/Design/moba_attack_module_design_v6_2.md` | Current | Attack action/session/commit and presentation audio integration |
| Buff | `Docs/Design/BuffSystem_Design_v14_2_PermanentBuffRespawnPatch.md` | Current | Handle ownership and death/respawn lifecycle |
| Crowd control | `Docs/Design/moba_crowd_control_system_design_v6_2.md` | Current | CC state, immunity/unstoppable handles and lifecycle |
| Equipment / shop / gold | `Docs/Design/moba_equipment_shop_gold_system_design_v12.md` | Current | Equipment runtime, OperationLog, undo and unique GoldIncomeRuntime |
| Unit physics / range query | `Docs/Design/MOBA_UnitPhysics_RangeQuery_Design_v13.1.md` | Current | Deterministic 2D movement, collision/query and transform boundary |
| Pathfinding | `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md` | Current | Deterministic pathfinding integration |
| Non-hero units | `Docs/Design/moba_non_hero_unit_modules_design_v5.md` | Current | Minion and jungle ownership, AI registration and death cleanup |
| Presentation | `Docs/Design/moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md` | Current | Visual snapshots, event identity, attack SFX and presentation rollback |
| UI / Lua | `Docs/Design/MOBA_UI_Lua_System_Design_v9_1_GoldIncomeRuntime_Aligned.md` | Current | UI/Lua bridge and read-only gold display |
| Player input | `Docs/Design/MOBA_Player_Input_Command_Module_Design_v1_1.md` | Current | Move, Attack, QWER, non-smart cast and hold-release physical input mapping |

## Cross-document authority

Where a topic appears in multiple documents, use this ownership table:

| Contract | Owning design |
|---|---|
| Tick meanings, AuthorityFrame, recovery and rollback boundary | FrameSync v10.2 |
| Exact GameplaySnapshot membership | Snapshot Appendix v7.2 |
| Unit lifecycle API and Handler ownership | Unit Framework v27.3 |
| Damage/heal/shield settlement and death/kill deferred requests | Combat v13.2 |
| Projectile UID and ProjectileWorld snapshot | Projectile v19 |
| Ability signals, sessions, stages and Gameplay timing | Ability v15.2 |
| Physical player input mapping | Player Input v1.1 |
| AI ability decision path | Unit Framework + Ability; AI does not use Player Input |
| Gold batch ownership and CurrentAvailableGold | Equipment / Gold v12 |
| Visual and audio event identity | Presentation v13.2 |
| Physics query and logical position | Physics v13.1 |

## Archived documents

Old versions should be moved under:

```text
Docs/Archive/
```

Do not keep older versions beside active designs unless there is a strong reason.

If old files must remain, their first heading should state:

```text
SUPERSEDED — DO NOT USE FOR IMPLEMENTATION
```

## Updating this index

When a formal design is revised:

1. Add the new file.
2. Change the Current entry here.
3. Move the old file to `Docs/Archive/`.
4. Add the decision to `DECISION_LOG.md`.
5. Update affected ExecPlans and module status.
