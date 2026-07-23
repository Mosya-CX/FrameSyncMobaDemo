# Plan 0042: Presentation Bridge Foundation

> Status: Completed
> Created: 2026-07-22
> Design: `Docs/Design/moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md` §1–§11
> Predecessor: 0041 Pathfinding Infrastructure
> Lines target: ~550

## Scope

Build the presentation data pipeline: Gameplay systems submit pure-data VfxEvent/SfxEvent records during Tick execution; at Tick end, PresentationSyncManager consumes them and dispatches to stubbed VfxManager/AudioManager. Also build UnitPresentationHost + UnitAnimationDriver for per-unit animation driving.

### New: Unit/Presentation/ (pure data, no UnityEngine)

| # | File | Lines | Purpose |
|---|---|---|---|
| 1 | `PresentationEventId.cs` | ~45 | Stable identity: SourceLogicTick + SourceKind + SourceUid + EventSequence + EventKey |
| 2 | `VfxEvent.cs` | ~35 | VFX record: defId, world pos/dir, attach target, duration scale |
| 3 | `SfxEvent.cs` | ~35 | SFX record: defId, anchor type, attach target, pitch/volume scale |
| 4 | `VisualEventOutput.cs` | ~60 | Static buffers: SubmitVfx/SubmitSfx; ConsumeVfxEvents/ConsumeSfxEvents |
| 5 | `PresentationSourceKind.cs` | ~10 | Enum: Unit, Projectile |
| 6 | `SfxAnchor.cs` | ~10 | Enum: UnitRoot, Camera, World |

### New: FrameSync/ (MonoBehaviour, needs UnityEngine)

| # | File | Lines | Purpose |
|---|---|---|---|
| 7 | `UnitPresentationHost.cs` | ~65 | MB on Unit GO: holds AnimationDriver + SocketSet; registers on enable |
| 8 | `UnitPresentationRegistry.cs` | ~45 | Static Dictionary<UnitUid, UnitPresentationHost> for VFX/SFX lookup |
| 9 | `UnitAnimationDriver.cs` | ~90 | Reads LifeState/ActionStateView/AttackHandler/AbilityCastView → drives Animator |
| 10 | `PresentationSyncManager.cs` | ~55 | Tick-end: consumes Vfx/Sfx buffers, calls managers, updates animation drivers |
| 11 | `VfxManager.cs` | ~30 | Stub: logs VfxEvent details |
| 12 | `AudioManager.cs` | ~30 | Stub: logs SfxEvent details |

### Modified files

| # | File | Change |
|---|---|---|
| 13 | `Unit/Attack/AttackHandler.cs` | +15: After Commit → SubmitSfx |
| 14 | `Unit/Combat/CombatSystem.cs` | +10: On damage/death → SubmitVfx |
| 15 | `Unit/Buff/BuffHandler.cs` | +10: On Buff create/remove → SubmitVfx |
| 16 | `Unit/Ability/AbilityHandler.cs` | +10: On Stage entry → SubmitVfx/Sfx |
| 17 | `FrameSync/SimulationTickPipeline.cs` | +20: Tick end → PresentationSyncManager.ConsumeAllEvents |
| 18 | `Unit/Core/UnitWorld.cs` | +5: SpawnUnit → ensure UnitPresentationHost binding note
