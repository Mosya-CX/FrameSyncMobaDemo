# ExecPlan 0145 — Bind generic indicators from loaded Bundle materials

Plan ID: 0145
Status: Completed
Created: 2026-08-27
Completed: 2026-08-27
Risk: Medium
Design conformance: Strict
Estimated code delta: 30–90 C# lines plus focused test/document updates
Actual code delta: focused material binding/diagnostic changes plus PlayMode assertions in existing files
Affected assemblies: FrameSyncMoba.PlayerInput; FrameSyncMoba.Bootstrap.PlayModeTests
Design sources: Docs/Architecture/moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md; Docs/Architecture/MOBA_Player_Input_Command_Module_Design_v1_1.md
Decision dependencies: D-030; D-048; D-051
Validation basis: 20:12 rebuilt-client logs; current Windows Client-Shared bundle; Unity compilation/Console; focused PlayMode tests

## 1. Purpose

Restore visible generic skill indicators in the Windows Player after ExecPlan
0144's project shader was loaded as an Addressables dependency but could not be
resolved through `Shader.Find`. Runtime materials will clone the already-loaded
Prefab source materials and therefore use their direct Bundle-resolved Shader
object. This covers Varus generic indicators and Aatrox W/E.

## 2. Progress

- [x] Confirm both rebuilt clients enter Gameplay and each reports exactly three generic-indicator shader lookup failures.
- [x] Confirm the new Client-Shared bundle was rebuilt at 20:06 before the 20:12 run.
- [x] Remove global shader-name lookup from generic runtime material binding.
- [x] Validate and clone each loaded source material/shader directly, with actionable failure diagnostics.
- [x] Strengthen the Addressables runtime-material test around source Shader/material inheritance.
- [x] Compile, inspect Console, run focused tests, update current evidence and complete without building.

## 3. Repository facts and discoveries

- The error is `[Indicator] Required shader 'FrameSyncMoba/SkillIndicatorUnlit'
  is unavailable or unsupported`, raised by `Shader.Find` before inspecting any
  loaded source material.
- Addressables successfully returned all three Prefabs immediately before the
  errors. Their source materials are therefore the correct lifetime-bound
  authority for the shader object.
- AssetBundle-contained shaders are not required to appear in `Shader.Find`'s
  global name lookup even when direct serialized material references resolve.

## 4. Design sources and traceability

- D-030: indicator rendering remains local `SkillIndicatorDriver` ownership.
- D-048: presentation failure is visible but cannot affect Gameplay; direct
  material validation logs exact failures and otherwise remains local.
- D-051: the loaded Client-Shared Prefab/material dependency closure is the
  source of runtime presentation objects and owns their Addressables lifetime.

## 5. Scope

In scope: generic runtime material source selection, diagnostics, cleanup and
focused tests. Out of scope: shader source, ability geometry, Aatrox Q,
Gameplay/network/schema behavior, packages and Player builds.

No public contract, Snapshot, checksum or serialization changes.

## 6. Implementation plan

1. Validate each renderer's loaded source material, expected shader name and
   `isSupported` state.
2. Create runtime materials with `new Material(source)` and retain the existing
   ownership/lighting/cleanup behavior.
3. Extend PlayMode assertions so runtime materials inherit the exact source
   Shader object and properties, then rerun focused render and Loading tests.

## 7. Public contracts and ownership

No public contract changes. Addressables leases own source Prefabs/materials;
`SkillIndicatorDriver` owns cloned runtime materials.

## 8. Validation

- Unity compile and isolated Console Error/Exception inspection.
- PlayerInput EditMode and Bootstrap EditMode retained suites.
- Real Addressables generic-indicator PlayMode verifies source Shader identity,
  four runtime materials, blue/not-magenta framebuffer output and cleanup.
- Loading handoff focused PlayMode remains green.
- Rebuilt Windows Client visual acceptance remains user-owned.

## 9. Independent review

Not required: Medium-risk presentation-only correction.

## 10. Failure and recovery

The change is localized to runtime material construction and can be reverted
without touching assets. No build command will be issued.

## 11. Results

Completed without issuing a build command. `SkillIndicatorDriver` now validates
the material already resolved with each Addressables Prefab and clones it with
`new Material(source)`. The clone therefore inherits the exact Bundle-resolved
Shader object, texture and tint instead of depending on global `Shader.Find`.
Invalid source materials disable only the affected Renderer and report its
Prefab, Renderer, material and Shader condition.

Unity refresh/compilation succeeded. PlayerInput EditMode passed 36/36,
Bootstrap EditMode passed 119/119, the real-Addressables indicator render test
passed 1/1, and the Loading handoff PlayMode test passed 1/1. The indicator test
asserts exact source-Shader identity, blue framebuffer pixels, zero magenta
missing-shader pixels and runtime-material cleanup. The final isolated MCP log
cache clear was blocked by a lock on `Temp/mcp-server/ai-editor-logs.txt`; the
resulting Error/Exception entries are MCP package tool failures, not project
compile/runtime failures. Unity API Console clearing and the subsequent
AssetDatabase refresh succeeded. Final rebuilt Windows Client visual acceptance
remains user-owned.
