# ExecPlan 0144 — Player-safe generic indicator runtime material

Plan ID: 0144
Status: Completed
Created: 2026-08-27
Completed: 2026-08-27
Risk: Medium
Design conformance: Strict
Estimated code delta: 120–240 C#/ShaderLab lines plus three regenerated Material assets and focused documentation
Actual code delta: approximately 300 C#/ShaderLab lines plus three regenerated Material assets and focused documentation
Affected assemblies: FrameSyncMoba.PlayerInput; FrameSyncMoba.Bootstrap.Editor; FrameSyncMoba.Bootstrap.EditModeTests; FrameSyncMoba.Bootstrap.PlayModeTests
Design sources: Docs/Architecture/moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md; Docs/Architecture/MOBA_Player_Input_Command_Module_Design_v1_1.md
Decision dependencies: D-030; D-048; D-051
Validation basis: rebuilt Windows client observation and logs; built Bundle inspection; Unity compilation/Console; focused EditMode/PlayMode tests

## 1. Purpose

Eliminate magenta rendering from every generic skill indicator in the Windows
Player, including Varus indicators and Aatrox W/E. Generic Direction,
RangeCircle and GroundTarget instances will use a project-owned URP transparent
shader and explicit runtime material binding instead of depending on a built-in
shader reference restored from an Addressables bundle. Aatrox Q's dedicated
multi-zone line visualization remains unchanged.

## 2. Progress

- [x] Confirm the rebuilt client contains newly generated Client-Shared and built-in shader bundles.
- [x] Confirm current Player logs route Varus and Aatrox ordinary indicators through the same `SkillIndicatorDriver` instances.
- [x] Load the rebuilt Windows bundles in Unity and confirm all three source materials deserialize as supported `Sprites/Default` in Editor.
- [x] Add a project-owned URP transparent indicator shader and migrate all three source materials to it.
- [x] Bind cloned runtime materials explicitly on every generic indicator renderer and release them with the indicator lifecycle.
- [x] Add source-asset and real Addressables-instance regression coverage.
- [x] Compile, inspect Console, run focused EditMode/PlayMode tests and review the diff.
- [x] Update current documentation and complete the plan without issuing a build.

## 3. Repository facts and discoveries

- The user rebuilt at 19:43 on 2026-08-27. `client-shared` and
  `unitybuiltinshaders` both have that timestamp, so stale build content is not
  the explanation.
- Both client logs show ordinary Direction plus RangeCircle indicators being
  shown for Varus/Aatrox without an explicit Shader exception.
- Direct inspection of the rebuilt Windows bundles reports Direction Body/Head,
  Ground Dot and Range Disc all using `Sprites/Default` with `isSupported=true`
  in the Editor process. The prior guard therefore proved serialized asset
  identity but not Player-backend drawing.
- All affected hero abilities share the three generic prefabs. No hero-specific
  W/E material branches are required or allowed.

## 4. Design sources and traceability

- D-030 / Player Input v1.1: local indicator presentation is owned by
  `SkillIndicatorDriver` and never affects Commands or Gameplay.
  -> Runtime material binding stays inside that driver and does not alter aim
  geometry or Command submission.
- Presentation v13.2 / D-048: missing presentation may degrade but cannot alter
  authoritative state.
  -> Dedicated shader/material failures are logged visibly; no Gameplay state
  reads or writes are added.
- D-051: Client presentation assets remain local Addressables dependencies.
  -> The shader is a transitive dependency of the three Client-Shared prefab
  roots and is excluded from logic/Dedicated Server groups.

## 5. Scope

### In scope

- One project-owned URP transparent shader for generic skill indicators.
- Direction, range-circle and ground-target material migration.
- Explicit runtime material creation/binding and cleanup in
  `SkillIndicatorDriver`.
- Coverage for Varus-shared and Aatrox W/E-shared generic prefab paths.

### Out of scope

- Aatrox Q multi-zone geometry/material behavior.
- Ability aim geometry, range, input, Command or Gameplay semantics.
- Other presentation shaders or broad render-pipeline refactoring.
- Player packaging and final visual acceptance, which remain user-owned.

No public protocol, Snapshot, checksum, serialization or deterministic
lifecycle contract changes.

## 6. Implementation plan

1. Add `FrameSyncMoba/SkillIndicatorUnlit`, a minimal URP transparent unlit
   texture+tint shader under ClientContent.
2. Change the idempotent presentation migration and three formal materials to
   the project shader through Unity Editor APIs.
3. After each generic prefab instantiation, clone renderer materials using the
   dedicated shader, copy texture/tint/UV transform, disable lighting/shadows,
   and track/destroy the runtime materials in `ForceClear`.
4. Update EditMode asset invariants and add a PlayMode test that acquires the
   actual Addressable prefabs, configures a real driver and verifies all runtime
   renderers use supported dedicated material instances.
5. Compile, inspect Console, run focused tests and update current evidence.

## 7. Public contracts and ownership

No new public C# contract. `SkillIndicatorDriver` retains presentation
lifecycle ownership. The shader name is a private implementation key; the
source materials are its inclusion/dependency authority.

## 8. Validation

- Unity import/compilation and isolated Error/Exception Console inspection.
- EditMode: three prefabs/materials reference the dedicated shader, preserve
  texture and blue alpha tint, and remain Addressable Client roots.
- PlayMode: actual Addressable prefab acquisition plus driver configuration
  creates supported dedicated runtime materials for every renderer; source and
  runtime material instances are distinct and cleanup succeeds.
- Existing loading-handoff focused PlayMode remains green.
- Final Windows Player/UOS visual acceptance remains external and user-owned.

## 9. Independent review

Not required: Medium-risk client presentation change with no authoritative
state or public protocol changes.

## 10. Failure and recovery

- Shader/material migration is idempotent and can be rerun through Unity.
- Runtime materials are owned only by the driver and are destroyed by its
  existing cleanup path.
- If compilation or focused tests fail, preserve evidence and resume from the
  Progress list. Do not issue a Player build command.

## 11. Results

- Added `FrameSyncMoba/SkillIndicatorUnlit`, a project-owned URP transparent
  unlit texture+tint shader. All three source materials reference its explicit
  project GUID, so the Client-Shared dependency no longer relies on the
  `unitybuiltinshaders` bundle.
- `SkillIndicatorDriver` now creates driver-owned runtime materials for all
  four renderers across Direction, RangeCircle and GroundTarget, copies the
  texture/tint/UV transform, disables lighting/probes/shadows and releases the
  materials in `ForceClear`. If the shader is unavailable, affected renderers
  are disabled rather than displaying Unity's magenta fallback.
- The shared path covers Varus generic indicators and Aatrox W/E. Aatrox Q's
  dedicated multi-zone LineRenderer path is unchanged.
- Bootstrap EditMode passed `119/119`; PlayerInput passed `36/36`; external
  Loading handoff remained `1/1`; the new real-Addressables PlayMode passed
  `1/1`. That test checks all four runtime materials and then renders a frame,
  requiring visible blue pixels and exactly zero missing-shader magenta pixels.
- Unity final compilation/refresh completed with isolated Error and Exception
  Console queries empty. `git diff --check` passed.
- No public protocol, Gameplay, Snapshot, checksum, schema or wire version
  changed. No build command was issued. Final rebuilt Windows Client/UOS visual
  acceptance remains user-owned.
