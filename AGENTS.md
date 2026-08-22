<!-- UNITY CODE ASSIST INSTRUCTIONS START -->

- Project name: FrameSyncMobaDemo
- Unity version: Unity 2022.3.62f1c1

<!-- UNITY CODE ASSIST INSTRUCTIONS END -->

# FrameSyncMobaDemo Development Constitution

## 1. Project and authority

This repository contains a deterministic frame-synchronized Unity MOBA.

The single index of current formal designs is:

- `Docs/Architecture/DESIGN_INDEX.md`

When requirements conflict, use this order:

1. The current user request.
2. `Docs/Architecture/DECISION_LOG.md`.
3. `Docs/Architecture/DESIGN_INDEX.md`.
4. Current formal designs listed by the index.
5. Existing implementation.
6. Code comments and examples.

Only documents marked Current by `DESIGN_INDEX.md` are implementation
authorities. Archived audits, old prompts, completed plans and superseded
designs are historical evidence, not current requirements.

Do not silently choose between conflicting current public contracts. Report the
exact documents and sections, stop the affected contract work, and continue all
unaffected work.

## 2. Document responsibilities and reading policy

- `AGENTS.md`: stable project constitution and document router.
- `Docs/Implementation/AI_WORKFLOW.md`: current direct-request execution flow.
- `.agents/PLANS.md`: ExecPlan triggers, format and lifecycle.
- `Docs/Implementation/CURRENT_HANDOFF.md`: replace-on-update current save state.
- `Docs/Implementation/MODULE_STATUS.md`: current module capability and evidence.
- `Docs/Architecture/DESIGN_INDEX.md`: current formal design index.
- `Docs/Architecture/DECISION_LOG.md`: frozen architecture decisions.
- `Docs/Implementation/Plans/INDEX.md`: active-plan locator and plan history rules.
- `Docs/Implementation/BUILD_GUIDE.md`: packaging instructions.
- `Docs/Implementation/C_S_TEST_GUIDE.md`: local client/server test instructions.
- `Docs/Archive/`: historical material; never a default source of current facts.

At task intake, read only the current state and authority indexes needed to
understand the request. Search `DECISION_LOG.md` for the relevant domain or
decision instead of reading it in full. Read `.agents/PLANS.md` only when the
task triggers an ExecPlan. Read operational guides only when their procedures
are in scope.

The project no longer uses an A/B/C candidate loop. The user supplies a design
and a concrete request; Codex scopes, implements and verifies that request
directly.

## 3. Direct-request working model

Before modifying code:

1. Resolve the current formal design and relevant frozen decisions.
2. Search for existing authoritative types and equivalent implementations.
3. Inspect affected asmdefs and dependency direction.
4. Check the current Unity compilation/Console state through Unity MCP.
5. Select the smallest complete, testable slice that satisfies the request.
6. Create or update an ExecPlan when `.agents/PLANS.md` requires one.

After modifying code:

1. Trigger Unity script compilation through MCP.
2. Inspect relevant Console errors and warnings.
3. Run focused EditMode tests.
4. Run PlayMode tests when scenes, lifecycle, input, assets or presentation are
   involved.
5. Review the diff against the exact design sections and scope.
6. Run an independent read-only review for High-risk changes, using a separate
   review sub-agent when collaboration tools are available.
7. Update only the affected plan, module-status rows and current handoff state.

The following require user approval before the affected work continues:

- a formal design deviation;
- a new third-party package;
- a major public-protocol or data-ownership change not already requested;
- Snapshot schema semantics or restore-boundary changes not already requested;
- merging/removing architecture layers required by a current design;
- a real conflict between current formal designs;
- large-scale deletion of existing implementation.

Private implementation details, focused test organization and ordinary helper
functions do not require repeated approval.

## 4. Deterministic Gameplay invariants

Authoritative Gameplay must not depend on:

- `float` or `double` calculations;
- `UnityEngine.Random`;
- `Time.time`, `Time.deltaTime` or render duration;
- `GetInstanceID()`;
- Unity object creation, scene hierarchy or component registration order;
- Unity physics as Gameplay authority;
- `Dictionary` or `HashSet` enumeration order;
- presentation state;
- device input during rollback or replay.

Use the project fixed-point type, stable UIDs, deterministic random service,
canonical serialization and explicit stable ordering keys. Any collection
iteration that affects Gameplay output must define its ordering in code.

Do not create duplicate protocol types for UID, Command, Snapshot, Aim,
AbilitySignal, checksum, PlayerSlot or fixed-point values. Public contracts live
in the lowest-level assembly that owns their semantics.

Deterministic Gameplay assemblies must not reference UI, presentation/audio/VFX
implementations, Unity Input System device state or NGO/UOS transport
implementations. Bootstrap owns scenes, Unity scheduling and networking.

Maintain the frozen cross-system rules in `DECISION_LOG.md`, including:

- Tick meanings and ordinary rollback boundary;
- complete canonical Command-byte authority comparison;
- one-Tick Snapshot interval and separate Restore/Resolve/Rebuild phases;
- exact Snapshot membership from the current appendix;
- synchronous formal death through UnitWorld lifecycle APIs;
- stable Combat settlement and deferred death/kill requests;
- unique GoldIncomeRuntime ownership and read-only derived available gold;
- input events converted to Commands once, with replay never rereading devices;
- presentation never writing authoritative Gameplay state.

Invalid restored deterministic references must fail visibly according to the
owning design. Restore must not silently repair or delete them.

## 5. Assembly and code quality

Prefer explicit asmdefs and one-way dependencies. Before adding a type, search
for the existing authoritative UID, DTO, command, snapshot, runtime view, event
ID or value type.

Use explicit access modifiers and immutable/readonly value types where useful.
Avoid in per-Tick paths:

- LINQ;
- unnecessary managed allocation;
- closures, boxing and runtime reflection;
- string-based dispatch;
- uncached component searches.

Validate static configuration during Editor validation or Bake. Do not catch
and ignore deterministic errors. Do not leave placeholder success, disabled
tests, empty implementations or TODOs in place of required behavior.

Do not modify a formal design merely to justify an implementation shortcut.
Examples are explanatory unless explicitly marked as formal contracts.

## 6. Unity MCP and asset discipline

Use the connected Unity MCP for supported Unity operations, especially:

- version/package/project-state inspection;
- scene, prefab, ScriptableObject and serialized-reference inspection;
- AssetDatabase refresh and script compilation;
- Console inspection;
- EditMode and PlayMode tests;
- Unity asset creation or modification.

Do not manually edit scene, prefab, controller, InputAction or ScriptableObject
YAML when MCP/Unity APIs can perform the operation. If MCP fails, record the
operation, failure, fallback, risk and required final Unity verification.

After changing C# scripts, always compile through Unity and inspect the Console.
A source-only compiler is not sufficient Unity validation.

## 7. Testing and completion

Every implementation task adds or updates tests proportionate to its behavior.
Prefer pure/EditMode deterministic tests for ordering, serialization, UIDs,
Combat, abilities, snapshots, rollback, checksums, gold and input state
machines. Use PlayMode for Input System callbacks, scenes, GameObject lifecycle,
prefabs, presentation and UI pointer behavior.

For deterministic systems, cover where relevant:

1. repeated execution equivalence;
2. continuous versus Snapshot/Restore/Replay equivalence;
3. insertion-order independence;
4. deterministic failure of invalid configuration.

A task is complete only when implementation matches the current design, Unity
has no new compile errors, focused tests pass, required PlayMode/integration
checks pass, the diff is reviewed, and status evidence is recorded. Compilation
alone is never behavior verification.

Final reports include changed files, public contracts, tests, Unity compilation,
EditMode/PlayMode results, design requirements verified, remaining limitations
and unresolved conflicts or assumptions.

## 8. Scope and content

Implement the smallest complete vertical slice requested. Do not perform
unrelated refactors or add packages without approval.

Framework systems remain generic and data-driven. Named heroes, equipment or
mechanics in formal designs are examples unless the current user explicitly
requests that concrete production content. User-requested concrete content may
be implemented through existing generic authoring and extension points; do not
hard-code content branches into core deterministic systems.

Report unrelated problems without silently expanding the task.

## 9. Packaging and builds

Build entry points and procedures are defined in:

- `Docs/Implementation/BUILD_GUIDE.md`
- `Docs/Implementation/C_S_TEST_GUIDE.md`

Build commands are sent exactly once. After sending a build request, stop all
Unity operations and wait for the user to report that the build has ended.

- Local C/S: `LocalNgoBuildMenu.BuildBoth()`.
- UOS Linux server image: `BuildServerLinux()`.
