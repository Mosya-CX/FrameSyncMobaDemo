# ExecPlan 0046 — Full design-conformance recovery

> Status: Completed 2026-07-22.  
> Approval source: the owner required all previously audited P0/P1 conformance findings to be corrected before later framework work. The owner subsequently canceled candidate-plan creation and requested immediate correction closure only.

## 1. Purpose

Bring the generic framework back into conformance with the current designs and accepted decisions. The recovery begins with prefab-authored MonoBehaviour Units/Handlers and closes the audited command, determinism, snapshot, lifecycle, module ownership, authority/checksum, configuration, composition-root, and test-baseline findings. It creates no production content.

## 2. Progress

- [x] Re-audit the current repository against `DESIGN_INDEX.md`, current designs, decisions, code, asmdefs, Unity assets, Console, and test baseline.
- [x] Record and implement the owner decision that Unit and all current Handlers use formal MonoBehaviour/prefab composition.
- [x] Gate A — Unit/Handler composition, formal spawn, prefab lookup, D-009 lifecycle ownership, and lossless Physics identity.
- [x] Gate B — canonical Command header/bytes, tagged Aim, stable collection, receipts/CommandSeq, and callback-to-later-processing PlayerInput boundary.
- [x] Gate C — identity-keyed aggregate GameplaySnapshot with separate Restore/Resolve/Rebuild and deterministic invalid-reference failure.
- [x] Gate D — global Combat settlement ordering/ownership/contributions and complete Projectile identity/lifecycle/restore.
- [x] Gate E — Ability/Buff/CC deterministic state, signals, handles, reactions, restore and lifecycle behavior.
- [x] Gate F — Equipment/Gold fixed-point Bake/runtime ownership/digest; Stats/XP and NonHero stable ownership/restore.
- [x] Gate G — continuous AuthorityFrame acceptance, recovery/rollback boundary, match state, and SharedGameplayChecksum coverage.
- [x] Gate H — composition root, runtime configuration assets, hard-coded runtime configuration removal, bootstrap scenes and Input Actions.
- [x] Gate I — Unity compilation/Console, focused EditMode/PlayMode correction and regression validation without weakening tests.
- [x] Update repository map, module status, handoff and this ExecPlan with current evidence and remaining non-blocking work.
- [x] Remove candidate creation from this round after the owner's explicit cancellation; no 0047 plan or candidate document was created or executed.

## 3. Surprises and discoveries

- The repository contained substantial generic code, but several aggregate contracts were smaller or differently owned than the formal designs; isolated passing tests did not prove correct module boundaries.
- Unit and seven Handlers were plain C# and forced an unwanted external Unit/GameObject map. Converting them to prefab-authored MonoBehaviours matched both the current design and the owner's configuration workflow.
- Several initial failures were stale fixture assumptions, but the final static/behavior audit also found real deterministic defects: Dictionary-dependent contributor ties, an incorrect inclusive expiry boundary, overkill/pre-shield contribution accounting, top-contributor substitution for the fatal source, and empty stable-reference resolution in shop/attack state.
- Runtime values were partly hidden behind convenience constructors and a hard-coded 30 Tick rate. All recovered production paths now receive explicit baked configuration.
- The old repository/status documents described an empty composition project, absent InputActionAsset, and pure-C# Unit variance. Those statements became false during this plan and have been replaced.
- `StatHandler.cs` contained legacy GBK comment bytes inside an otherwise ASCII file; it was normalized to UTF-8 before semantic edits.
- PlayerInput now consumes a baked profile contract, but the automatic `CastModelDef` → profile authoring Bake cannot be completed honestly until the formal generic Ability targeting/indicator authoring database exists. It remains non-blocking while no production Ability assets exist; no shortcut parallel protocol was invented.

## 4. Decision log

- Use only designs indexed as current by `DESIGN_INDEX.md`; do not edit designs to justify implementation shortcuts.
- Keep exactly one authoritative UID, Command, Snapshot, Aim, AbilitySignal, Checksum, FixedPoint and runtime DTO semantic contract.
- Use prefab-authored MonoBehaviour Unit/Handler composition; plain C# remains appropriate for deterministic values, collections, algorithms and services without authoring/lifecycle ownership.
- Inspector floats are authoring input only and convert once during Bake/initialization. Gameplay calculations and persisted authority use `fp`.
- Stable ordering is explicit at every output-affecting boundary. Dictionary/HashSet enumeration cannot determine Gameplay results.
- Restore, Resolve and Rebuild stay separate. Invalid deterministic references fail rather than being repaired or deleted.
- Use the low-overhead validation policy in `Docs/Implementation/AI_WORKFLOW.md`: compile and inspect Console at normal checkpoints; run tests for high-risk contracts, Unity lifecycle/assets, and final closure rather than repeatedly running full suites.
- The dirty historical Git baseline is neither a blocker nor a restoration source; current repository state and D-024 are authoritative.
- Candidate planning was removed from this plan when the owner requested correction-only closure.

## 5. Current repository context

The project dependency direction is RuntimeConfig/Deterministic/Physics → Unit → FrameSync → PlayerInput → Bootstrap, with Editor/PlayMode test assemblies pointing at their subjects. RuntimeConfig owns Unity configuration assets; Bootstrap owns application composition. Unit and Handlers are MonoBehaviours but deterministic authority is defined by stable UIDs, fixed-point state, canonical ordering and explicit snapshots rather than Unity identity or hierarchy order.

Current Unity assets include `GlobalPrefabTable.asset`, `GlobalGameplayData.asset`, `Gameplay.inputactions`, `ClientBootstrap.unity` and `ServerBootstrap.unity`. Unity MCP verified the serialized global configuration fields and current compile/Console state.

## 6. Exact design sources

- `Docs/Architecture/DESIGN_INDEX.md` and each current system design it indexes.
- `Docs/Architecture/DECISION_LOG.md`, including D-009, D-022, D-023 and D-024.
- `Docs/Design/unit_behavior_framework_design_v27_3.md`.
- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`.
- `Docs/Design/MOBA_UnitPhysics_RangeQuery_Design_v13.1.md`.
- Current indexed Combat, Projectile, Ability, Buff, CrowdControl, Equipment/Gold, NonHero AI, Stats/XP, PlayerInput and snapshot appendix documents.
- `Docs/Implementation/AI_WORKFLOW.md`, synchronized from the owner's low-overhead handoff protocol.

## 7. Scope delivered

### In scope and completed

- Unit/Handler MonoBehaviour composition; stable prefab/factory spawn; Unit/Physics identity and D-009 lifecycle.
- Existing Command/Aim/Input contracts migrated in place to canonical formal boundaries.
- Aggregate snapshot topology, phase separation, stable identity/reference validation, random/physics/module/match state and checksum integration.
- Combat sequence, contribution, killer/assist, deferred ownership; Projectile UID, pending/active lifecycle and restore.
- Ability/Buff/CC state and lifecycle; Equipment/Gold fixed-point ownership/digest; Stats/XP and NonHero stable state.
- AuthorityFrame continuity, rollback/recovery boundaries, full shared checksum path.
- Runtime config/authoring assets, composition root, bootstrap scenes, Input Actions, required asmdef/test corrections.

### Explicitly out of scope

- P2-only cleanup, aesthetic refactors, unrelated optimization, new packages, final balance or production content.
- Specific heroes, named ability kits, production Buffs/equipment, map content, final audio/VFX/animation/UI.
- Future complete Ability authoring/targeting/indicator Bake, route execution, and presentation/UI systems.

## 8. Public contracts changed or confirmed

- `UnitHandler`, prefab-authored `Unit`, formal `UnitSpawnRequest`, `GlobalPrefabTable`, `GlobalGameplayData`.
- Existing `GameplayCommand`/`CommandHeader` canonical bytes and stable `CommandCollector` ordering.
- Existing tagged `AimSnapshot` and `AbilitySignal`; no parallel Aim/Signal schema.
- Aggregate `GameplaySnapshot` plus module snapshots and explicit restoration phases.
- Continuous AuthorityFrame/checksum/gold digest inputs and match state.
- Combat contribution/killer/assistant state and deterministic validation.
- Projectile stable UID/pending/active snapshot state.
- Equipment shop snapshot reference validation and explicit fixed-point configuration.

Repository-wide searches confirmed exactly one project authority for UnitUid, GameplayCommand, GameplaySnapshot, AimSnapshot, AbilitySignal, SharedGameplayChecksum, RuntimeUid adapter, and package `fp`. No competing public runtime DTO simple names were added.

## 9. Validation results

### Unity MCP

- AssetDatabase refresh and script compilation: passed after correcting the discovered Bootstrap PlayMode asmdef references and test API compatibility.
- Final Console query: no C# compiler or product-runtime Error. The only Error entries were MCP's own `ai-editor-logs.txt` file-lock diagnostics produced by the attempted log-clear operation.
- `GlobalGameplayData.asset` inspected through Unity serialization with TickRate 30; respawn 10/2 seconds; minion 30; jungle 5/3/60; sell 0.7; grid 10; and growth C/D fields present. Unity MCP performed the serialization save.

### Tests

- Deterministic EditMode: 51/51 passed, unchanged verified baseline.
- Physics EditMode: 71/71 passed.
- Physics PlayMode: 30/30 passed, unchanged verified baseline.
- FrameSync EditMode: 16/16 passed.
- PlayerInput EditMode: 3/3 passed.
- Unit PlayMode: 1/1 passed.
- Bootstrap PlayMode: 1/1 passed.
- Unit latest full run: 218 passed, 14 failed. Every failed owning class was corrected and focused-rerun green: Attack 11/11, Combat 10/10, contribution 3/3, stat calculation 11/11, stat snapshot 6/6, spawn 13/13, equipment snapshot 2/2.

The 14 corrections updated stale expectations to current formal semantics and fixed real production defects. No test was deleted, disabled, made non-assertive, or changed to accept incorrect behavior. Per the low-overhead policy, no redundant final full Unit suite was run after all owning focused suites passed.

## 10. Results

Completed. All audited P0 and implementation-blocking P1 findings within the approved recovery scope are corrected or, where dependent on a not-yet-existing broader formal authoring system, explicitly classified as non-blocking future capability without inventing a conflicting protocol.

Key verified invariants:

- Gameplay uses fixed-point authority, stable UIDs and explicit ordering; no Unity time/random/object identity or unordered enumeration determines output.
- Presentation/device input cannot write deterministic Gameplay state.
- Commands and snapshots have one canonical schema; restoration validates references and separates phases.
- D-009 ownership is preserved through death/respawn.
- Combat killer/assistant/contribution output is insertion-order independent and uses actual settled damage.
- Gold digest, match state, physics/random state and module state participate in aggregate recovery/checksum as owned.

Remaining limitations are future generic framework work only: Ability authoring/targeting/indicator Bake (including automatic player-input profile generation), route execution, presentation/UI integration, and production content. No next candidate or ExecPlan was created or executed in this round.
