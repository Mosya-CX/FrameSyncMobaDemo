# Jungle Camp Config Bake -- SO-based Camp Authoring

> ExecPlan 0088 | 2026-07-24
> Design: `moba_non_hero_unit_modules_design_v5.md` section 4
> Predecessor: 0032 (Non-Hero units), 0086 (Minion Wave Config Bake pattern)
> Conformance: Strict

## Goal

Create a data-driven `JungleCampConfig` ScriptableObject pipeline so camp composition, respawn timing, and monster definitions can be authored in the Editor and baked into runtime data. Follow the established pattern from 0086 (`MinionWaveConfig`).

## What already exists

- `JungleCampSystem` with `CreateCamp()`, `JungleCamp`, `JungleCampTiming` -- fully functional runtime.
- `JungleCampState` (Idle, Combat, Reset, Dead) with capture/restore.
- `BakedGlobalGameplayData` already provides Jungle timing values from GlobalGameplayData.
- `MinionWaveConfig` + `MinionWaveConfigValidator` + `MinionWaveBakeMenuItem` -- established pattern to follow.

## New types and files

### Production code (~250 lines)

| # | File | Assembly | Lines | Purpose |
|---|---:|---|---|---|
| 1 | `RuntimeConfig/JungleCampConfig.cs` | `FrameSyncMoba.RuntimeConfig` | ~100 | ScriptableObject with camp entries: campId, prototypeIds[], respawnDelay, gold/XP rewards |
| 2 | `RuntimeConfig/Editor/JungleCampConfigValidator.cs` | `FrameSyncMoba.RuntimeConfig.Editor` | ~80 | Editor-time validation: no duplicate campIds, valid prototype refs, non-negative timings |
| 3 | `RuntimeConfig/Editor/JungleCampBakeMenuItem.cs` | `FrameSyncMoba.RuntimeConfig.Editor` | ~40 | Editor menu item "Tools > FrameSync > Bake Jungle Camp Config" |
| 4 | `FrameSyncGameRuntime.cs` (modify) | `FrameSyncMoba.FrameSync` | +30 | Accept JungleCampConfig, wire into JungleCampSystem initialization |

## Public contract impact

- `JungleCampConfig` -- new public ScriptableObject.
- `JungleCampEntry` -- new serializable struct.
- No changes to runtime JungleCampSystem public contract.

## Snapshot / Checksum impact

None. JungleCampSystem snapshot topology (campId, member slots) is unchanged. Config is read-only after bake.

## Design conformance checklist

- [x] Data-driven camp composition (Design section 4)
- [x] Config validation at bake time, not runtime
- [x] Follows MinionWaveConfig bake pattern (Editor validator + menu item)
- [x] Camp topology stays in JungleCampSystem snapshot

## Tests

- `JungleCampConfigValidationTest` -- Editor test: valid config passes, duplicate IDs fail, invalid protos fail
