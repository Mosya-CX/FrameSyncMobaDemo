# ExecPlan 0123: Client control, presentation and UI composition

> Status: Implementation completed on 2026-07-29; visual prefab acceptance is
> intentionally left to the full-flow operator test.

## Purpose

Make one client role visibly control its assigned neutral unit through the
existing canonical command path while camera, presentation, HUD, shop and result
views consume read-only runtime state.

## Progress

- [x] Bind the local account/client ID to its authoritative player slot/unit.
- [x] Compose existing input, presentation and UI components in ClientBootstrap.
- [x] Replace local-only placeholder UI actions with application-facing calls.
- [x] Validate command routing and read-only presentation/UI bindings in
  focused tests; retain visible prefab acceptance for the operator test.

## Surprises and discoveries

- `BindLocalPlayer` has no production caller.
- Existing UI prefabs are not instantiated by Lobby or Client scenes.
- Result and Shop controllers create runtime fallback objects, while hero
  selection creates placeholder entries without application/network actions.

## Decision log

- Reuse the existing six UI prefabs and current controllers before requesting
  new visual assets.
- Keep all device/UI state outside GameplaySnapshot and checksum.
- Make scene composition fail visibly when a required production reference is
  missing instead of silently constructing an unstyled production UI.

## Current repository context

Existing prefabs include `GameplayHUD`, `HeroSelectCell`, `LoadingPanel`,
`LobbyPanel`, `SelectPanel` and `UIManager`. ClientBootstrap has input/camera
components but not a complete bridge, presentation or UI composition.

## Exact design sources

- `Docs/Design/MOBA_Player_Input_Command_Module_Design_v1_1.md`.
- `Docs/Design/moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md`.
- `Docs/Design/MOBA_UI_Lua_System_Design_v9_1_GoldIncomeRuntime_Aligned.md`.
- Current Ability, Attack and FrameSync designs for canonical command timing.

## Scope

In scope:

- Resolve local `ControllerClientId` to the applied player slot and spawned
  stable UnitUid, then call the existing `BindLocalPlayer`.
- Compose PlayerInput, camera, presentation dispatcher and existing UI views.
- Drive Move, Attack and one generic Ability through canonical future-Tick
  Commands.
- Bind HUD/minimap/cooldown/shop/result to read-only runtime views.
- Make result/return and selection actions call application services rather
  than mutate only local booleans.

Out of scope:

- Production hero kit, production UI art or final animation/audio.
- Lobby wire messages, UOS matchmaking and multi-process validation.
- Direct Gameplay writes from UI/Input callbacks.

## Affected assemblies and exact production types

Primarily `FrameSyncMoba.Bootstrap`, PlayerInput and Presentation composition.
Reuse `PlayerInputController`, `PlayerCommandRequester`,
`PresentationEventDispatcher`, current HUD controllers,
`ShopPageController`, `ResultPageController` and existing Lua bridge contracts.

Expected production-code change: 900-1,800 lines, plus prefab/scene reference
wiring.

## Public contracts, ownership and dependency direction

No new Command, Aim or AbilitySignal schema. Unity input callbacks write local
input events; later processing emits the existing Commands. UI and Presentation
read snapshots/events and never modify deterministic Gameplay state.

## Deterministic ordering

The controlled unit is selected only by applied player slot and stable UnitUid.
Input is converted once for a scheduled logic Tick. Presentation event order
uses the existing stable event history, never GameObject or callback order.

## Snapshot and serialization impact

Input-local, camera and UI state do not enter `GameplaySnapshot` or checksum.
The test must prove rollback/replay does not reread device input and produces
the same authoritative outcome.

## Implementation steps

1. Add an application-owned local-player binding coordinator after payload
   application and unit resolution.
2. Configure ClientBootstrap with required input/camera/presentation references
   and load the deterministic GameScene fixture.
3. Instantiate and inject the existing UI prefabs/controllers. Retain fallback
   UI only for explicit tests; fail visibly in the production client scene when
   required references are absent.
4. Route selection, result and return actions through application services
   without introducing lobby transport yet.
5. Exercise Move, Attack, generic Ability, shop command and result rendering.

## Tests

Focused EditMode tests cover slot binding, command timing, UI read models and
rollback input invariants. PlayMode tests cover Input System callbacks,
GameObject/prefab lifecycle, scene references, camera/presentation and the
visible neutral gameplay loop.

## Unity MCP validation

Use MCP for prefab/scene composition, compilation, Console and the focused
PlayMode path. Do not run the full suite unless shared protocol code changes.

## Failure conditions and completion criteria

Stop if client composition needs a second Gameplay Command schema or writes
Presentation state back to Gameplay. Complete when one client controls exactly
its assigned unit, commands settle deterministically, UI reflects state, and
base victory shows a result without production content.

## Production-content exclusion

Use only generic ability and primitive visual fixtures. No named hero, concrete
ability, Buff, equipment or final UI art is part of this plan.

## Results

ClientBootstrap now binds the assigned neutral Unit, routes UI actions through
application/Command entry points and keeps Presentation read-only. Existing UI
prefabs are retained; their final hierarchy and visual completeness remain
user-authored, while component binding and validation are code-owned.
