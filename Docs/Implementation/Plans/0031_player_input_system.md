# ExecPlan 0031 — Player Input System

> **Design authority**: `Docs/Design/MOBA_Player_Input_Command_Module_Design_v1_1.md`
> **Estimated code**: ~550–750 lines
> **New assembly**: `FrameSyncMoba.PlayerInput` (engine-aware, refs Unity.InputSystem)
> **Dependencies**: Command ✓ / Ability ✓ / Attack ✓ / FrameSync ✓

## Rationale

All Core Gameplay systems are implemented (Combat/Ability/Attack/Buff/CC/Projectile/Equipment). The project is a collection of deterministic modules with no way to interact. Player Input bridges Unity Input System to deterministic Commands, making the game playable.

## Scope — New files (new assembly)

| File | Lines | Description |
|---|---|---|
| `PlayerInput/FrameSyncMoba.PlayerInput.asmdef` | — | Ref: FrameSyncMoba.Unit, FrameSyncMoba.Deterministic, Unity.InputSystem; noEngineReferences=false |
| `PlayerInput/PlayerInputController.cs` | ~100 | MonoBehaviour. Subscribes to InputAction callbacks (Move/Attack/QWER/LeftClick/RightClick). Writes LocalGameplayInputEvent to buffer. Enable/disable ActionMap. |
| `PlayerInput/LocalInputEventBuffer.cs` | ~60 | Struct LocalGameplayInputEvent (Kind/Tick/Uid/Position/TargetUid). Ring buffer with MaxEventsPerTick=16. |
| `PlayerInput/GameplayInputGate.cs` | ~70 | IGameplayInputGate: checks CC/Capability block. HoldRelease keys force ReceiveRelease. Right-click during hold-release bypasses cancel. |
| `PlayerInput/MouseWorldResolver.cs` | ~80 | Screen→world: ground click→fp2, unit click→UnitUid. Uses PhysicsWorld for raycast. Local prediction tolerance. |
| `PlayerInput/PlayerCommandRequester.cs` | ~140 | Translate buffered events→GameplayCommand: Move/Attack/CastAbility/CancelAbility. HoldRelease FSM (Idle→Focus→Commit). AimSnapshot filling. First Commit suppresses duplicate. |

## Scope — Modified files

| File | Lines | Change |
|---|---|---|
| `FrameSync/GameplayCommand.cs` | +40 | Add AbilitySlot (byte), AbilitySignal field, AimSnapshot. Factory methods: CreateCastAbility, CreateCancelAbility. |
| `FrameSync/GameplayCommandKind.cs` | 0 | Already has CastAbility=3, CancelAbility=4 — no change needed |
| `FrameSync/CommandCollector.cs` | +30 | Accept GameplayCommandRequest (typed); return Receipt (Accepted/Blocked/Queued). |
| `Unit/Core/Unit.cs` | +10 | Add ControlledByPlayerSlot (int, -1=AI) |

## Key conformance

- InputAction callbacks only write local events — never touch deterministic Gameplay directly
- LocalInputEventBuffer does NOT enter GameplaySnapshot, SharedGameplayChecksum, or rollback
- HoldRelease: Q-press→Focus signal, Q-release→Commit signal, Left-click→Commit signal
- First successful Commit suppresses duplicate Commit input (same tick)
- Right-click during hold-release does NOT cancel ability; may still generate Move/Attack
- Skill timing calculated from deterministic Focus/Commit Tick execution
- AI does NOT use player input module (design §14)
- All Gameplay values (range, damage, cooldown) read from Ability/CastModelDef — never duplicated in input config
