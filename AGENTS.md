<!-- UNITY CODE ASSIST INSTRUCTIONS START -->

- Project name: FrameSyncMobaDemo
- Unity version: Unity 2022.3.62f1c1
  <!-- UNITY CODE ASSIST INSTRUCTIONS END -->

# FrameSyncMobaDemo Development Instructions

## Project overview

This repository contains a deterministic frame-synchronized Unity MOBA.

The authoritative design documents are listed in:

- `Docs/Architecture/DESIGN_INDEX.md`

Before implementing a system, read the current design documents listed for that system.

Do not use archived or superseded design versions as implementation references.

## Requirement priority

When requirements conflict, use this order:

1. The current user task.
2. `Docs/Architecture/DECISION_LOG.md`.
3. `Docs/Architecture/DESIGN_INDEX.md`.
4. Current versioned system design documents.
5. Existing implementation.
6. Code comments and examples.

Do not silently resolve a conflict between current design documents.

When a real public-contract conflict exists:

1. Stop implementation of the conflicting contract.
2. Report the exact documents and sections.
3. Explain the implementation consequences.
4. Continue all unaffected work.

Do not stop for ordinary implementation details that can be resolved without changing architecture.

## Working method

Before modifying code:

1. Read the relevant design documents.
2. Search the repository for existing equivalent types.
3. Inspect the current assembly definitions and dependency direction.
4. Check the current Unity compilation state through MCP.
5. Identify the smallest complete and testable implementation slice.
6. Update the current ExecPlan when the task spans several assemblies or public contracts.

After modifying code:

1. Trigger Unity script compilation through MCP.
2. Read all relevant Console errors and warnings.
3. Run relevant EditMode tests.
4. Run PlayMode tests when Unity lifecycle, scenes, input or presentation is involved.
5. Review the diff against the design documents.
6. Update module implementation status.

Do not only provide pseudocode when the task asks for implementation.

## Unity MCP

Use the connected Unity MCP for operations it supports.

Prefer MCP for:

- Inspecting the Unity version and installed packages.
- Inspecting scenes, prefabs and ScriptableObjects.
- Inspecting project settings and assembly definitions.
- Triggering script compilation.
- Reading Unity Console output.
- Running EditMode and PlayMode tests.
- Creating or modifying Unity assets.
- Verifying serialized references.

Do not manually edit scene, prefab or ScriptableObject YAML unless MCP cannot perform the operation and the task explicitly requires it.

After changing C# scripts, always trigger compilation and inspect the Console.

## Design-document discipline

Before implementation, verify the current document version through:

- `Docs/Architecture/DESIGN_INDEX.md`

Do not:

- Reference an older design because its filename appears in another old document.
- Copy example code without checking the current formal interface.
- Modify a design document merely to justify an implementation shortcut.
- invent missing public contracts without first checking related current designs.

Examples in design documents are explanatory unless explicitly marked as formal contracts.

Formal data structures, lifecycle rules, snapshot membership and stable ordering rules take precedence over illustrative code.

## Deterministic simulation

Gameplay simulation must not depend on:

- `float` or `double` for authoritative Gameplay calculations.
- `UnityEngine.Random`.
- `Time.time`, `Time.deltaTime` or render-frame duration.
- `GetInstanceID()`.
- Unity object creation order.
- Unity physics as Gameplay authority.
- `Dictionary` or `HashSet` enumeration order.
- Scene hierarchy order.
- Component registration order.
- Presentation state.
- Device input during rollback or replay.

Use:

- The project fixed-point type.
- Stable UID types.
- The deterministic random service.
- Explicit stable sorting keys.
- Explicit deterministic iteration order.

Any collection iteration that affects Gameplay output must define its ordering in code.

Do not hide deterministic ordering inside an undocumented helper.

## Assembly boundaries

Prefer explicit assembly definitions with one-way dependencies.

Deterministic Gameplay assemblies must not reference:

- Unity UI.
- Presentation implementations.
- Audio implementations.
- Visual effects implementations.
- Unity Input System device state.
- Networking transport implementations unless the design explicitly places a contract there.

Public contracts should live in the lowest-level assembly that owns their semantics.

Avoid circular assembly references.

Before creating a new type, search for an existing authoritative:

- UID.
- Command.
- Snapshot.
- Aim structure.
- Ability signal.
- Runtime view.
- DTO.
- Event ID.
- Fixed-point value type.

Do not create a second version of an existing protocol type for convenience.

## Frame synchronization

Maintain the following invariants:

- `ServerTick` is the next authoritative Tick the server will execute.
- `LatestAuthorityFrameTick` is the latest continuously accepted authority Tick.
- `LocalSimulationTick` is the next client Gameplay Tick.
- `SnapshotTick` is the next Tick to execute after restore.
- Ordinary rollback must not cross `LatestAuthorityFrameTick + 1`.
- AuthorityFrames are processed continuously and one Tick at a time.
- `SharedGameplayChecksum` is required.
- `GoldIncomeBatchDigest[T]` participates in `SharedGameplayChecksum(T)`.
- AuthorityRecovery only restores missing AuthorityFrames according to the current design.
- Device input is converted to Gameplay Commands once.
- Rollback and replay never reread Unity Input System.

Use complete canonical Command bytes for authority comparison.

Do not create a second network Command schema inside Gameplay or input modules.

## Snapshot and rollback

Snapshot interval is one Tick unless the current design is explicitly revised.

Restore uses separate phases:

1. `Restore`
2. `Resolve`
3. `Rebuild`

Do not combine their responsibilities.

Tick-local transient state must not enter Tick-end snapshots unless the current snapshot appendix explicitly says it is cross-Tick state.

Snapshot members must match the current snapshot appendix.

Restore must not silently repair invalid deterministic references.

Invalid stable references should produce a deterministic restoration error according to the relevant system design.

Every snapshot implementation requires:

- Capture and restore tests.
- Restore/resolve/rebuild tests.
- Round-trip equality tests.
- Rollback and replay equivalence tests.

## Unit and lifecycle rules

Use the formal lifecycle APIs:

- `UnitWorld.RequestEnterDying`
- `UnitWorld.RequestRecoverFromDying`
- `UnitWorld.ConfirmUnitDeath`

Formal death is written synchronously through UnitWorld during Combat settlement.

Normal death must not globally clear:

- `StatHandler`
- `CombatModifiers`

Each source system removes only the handles it owns.

Death and respawn call handlers in a fixed stable order.

Use:

- `ClearForDeath`
- `ClearForRespawn`

Cross-death Buff, equipment passive and ability passive runtimes rebuild their current-life-stage handles during the respawn lifecycle.

Do not save `FirstActiveLogicTick` or `FirstAITickLogicTick` as independent runtime or snapshot state when it can be derived from `SpawnLogicTick`.

## Combat

Follow the current Combat design for settlement and deferred requests.

Maintain these rules:

- `UnitDying` and ordinary damage/heal reactions may continue in the current Tick where specified.
- New ordinary Combat requests produced by `UnitDeath` or `UnitKill` follow the current deferred-request rule.
- `CombatSystemSnapshot` only stores the cross-Tick state listed in the snapshot appendix.
- Tick-local Combat queues must satisfy Capture assertions.
- Deferred request ordering must be stable.
- Legal deferred sequence gaps remain legal and must not be renumbered.
- Invalid restored Unit references must not be silently deleted.

`MatchStatisticsRuntime` must run on every simulation endpoint, not only the Dedicated Server.

## Gold and equipment

`GoldIncomeRuntime` is the sole match-runtime owner of:

- Unconfirmed gold income batches.
- Gold income batch digests.
- Confirmed earned gold totals.
- Confirmed income progress.

Do not create another predicted gold batch cache or confirmed gold ledger in FrameSync.

Account identity runtime stores no match gold total.

Gold confirmation:

- Advances confirmed income.
- Does not scan later shop Commands.
- Does not create a gold-specific dirty Tick.
- Does not actively replay later predicted shop Commands.
- Does not retroactively create a Command that failed local RequestCheck.

`CurrentAvailableGold` is derived and read-only.

## Player input

UI input is handled directly by Unity Input System UI integration.

The Gameplay player-input module handles:

- Move.
- Normal attack.
- Q/W/E/R ability slots.
- Local non-smart-cast aiming.
- Hold-release ability input.

Unity InputAction callbacks only write local input events.

Gameplay requests are processed later by the player-input module.

Do not access or modify deterministic Gameplay directly inside InputAction callbacks.

Player input mode is derived offline from the current `CastModelDef`.

Do not duplicate these values in input configuration:

- Minimum focus time.
- Maximum focus time.
- Ability range.
- Ability damage.
- Ability stage durations.
- Cooldown.
- Charge curves.

For hold-release abilities:

- Key press maps to the existing `Focus` signal.
- Key release maps to the existing `Commit` signal.
- Primary click also maps to the same `Commit` signal.
- The first successful Commit request suppresses duplicate Commit input.
- Right click does not cancel an already activated hold-release ability.
- Right click may still generate Move or Attack.
- Skill timing is calculated from deterministic Focus and Commit execution Ticks.

Input-local state:

- Does not enter GameplaySnapshot.
- Does not enter SharedGameplayChecksum.
- Is not restored during rollback.
- Must not be used as Gameplay authority.

## AI ability usage

AI does not:

- Simulate Unity keyboard or mouse input.
- Use the player-input module.
- Generate player network Commands.
- Read player-input profiles.

AI directly reads the existing Ability definitions and runtime state and produces the existing AbilityAction and AbilitySignal language.

Do not introduce a generic AI input protocol or Ability control layer unless a later formal design requires it.

## Presentation

Presentation must not affect deterministic Gameplay state.

Continue using the current `PresentationEventId` structure.

Attack Commit audio must use the current presentation/audio event entry point.

Do not call `AudioSource.Play()` directly from deterministic Attack or Ability code.

The entity root Unity Transform must only be written from the currently designated presentation synchronization point.

## Code quality

Use explicit access modifiers.

Prefer immutable or readonly value types where appropriate.

Avoid in per-Tick paths:

- LINQ.
- Unnecessary managed allocation.
- Closures.
- Boxing.
- Runtime reflection.
- String-based dispatch.
- Uncached component searches.

Validate static configuration during Editor validation or Bake.

Invalid deterministic configuration should fail early and visibly.

Do not catch and ignore deterministic simulation errors.

Do not leave:

- Placeholder success returns.
- Empty implementations presented as complete.
- TODO comments instead of required behavior.
- Disabled tests used to hide failures.

## Testing requirements

Every implementation task must add or update tests.

Prefer EditMode or pure deterministic tests for:

- Stable ordering.
- Command serialization.
- UID generation.
- Combat settlement.
- Ability stages.
- Snapshot round trips.
- Rollback replay.
- Checksums.
- Gold batches.
- Player-input state machines.

Use PlayMode tests when required for:

- Unity Input System callbacks.
- Scenes.
- GameObject lifecycle.
- Prefabs.
- Presentation.
- UI pointer blocking.

For deterministic systems, test that:

1. The same initial state and Command sequence executed twice produce identical results.
2. Continuous execution and Snapshot/Restore/Replay produce identical results.
3. Stable collection insertion order does not change canonical output.
4. Invalid configuration fails deterministically.

## Scope control

Implement the smallest complete vertical slice requested by the task.

Do not implement unrelated modules.

Do not make public-interface changes for stylistic preference.

Do not add third-party packages without explicit approval.

Do not perform a large refactor unless required by the current design and task.

When a task reveals unrelated problems:

- Record them.
- Report them.
- Do not silently expand the current task.

## Completion requirements

A coding task is complete only when:

1. The implementation matches the current design documents.
2. Unity compilation has no new errors.
3. Relevant EditMode tests pass.
4. Relevant PlayMode tests pass when required.
5. The final diff has been reviewed.
6. No duplicate protocol types were introduced.
7. No scope-external refactor was included.
8. Implementation status and ExecPlan were updated when applicable.

The final report must include:

- Files changed.
- Public contracts added or changed.
- Tests added or changed.
- Unity compilation result.
- EditMode and PlayMode test results.
- Design requirements verified.
- Remaining limitations.
- Assumptions or unresolved design conflicts.

## Framework versus game content

The current implementation goal is to build reusable systems and authoring pipelines, not final hero or gameplay content.

Implement generic framework capabilities such as:

- Unit, Combat, Ability, Attack, Buff, CrowdControl and Projectile runtimes.
- Generic cast models and stages.
- Generic hold-release ability support.
- Generic targeting and indicator support.
- Generic Buff and Modifier ownership.
- Generic equipment and gold systems.
- Deterministic configuration Bake and validation.
- Test fixtures that prove the framework behavior.

Do not implement specific production content unless the current task explicitly requests it.

This includes:

- Specific heroes such as Aatrox or Varus.
- Complete production ability kits.
- Production Buffs, equipment passives or map-object effects.
- Final balance values.
- Final visual, audio or animation assets.
- Champion-specific subclasses or hard-coded branches.
- Types named after a specific champion solely to implement an example.

When a design document mentions a named hero, ability or mechanic, treat it as:

```text
A behavioral example.
An acceptance scenario.
A test case for a generic capability.
```

Do not treat it as a production-content implementation request.

For example:

```text
“Varus Q” means:
    prove that the generic Ability system supports
    Focus on press,
    Commit on release or primary click,
    deterministic charge duration,
    projectile creation,
    session completion,
    and cooldown transition.

It does not mean:
    create a Varus hero,
    create final Varus Q data,
    import Varus assets,
    or hard-code Varus-specific logic.
```

Use neutral test names such as:

- `TestHoldReleaseAbility`
- `TestThreeStageAbility`
- `TestPermanentBuff`
- `TestDeathTriggeredProjectile`
- `TestEquipmentPassive`

Do not use production hero names in runtime framework types.

Framework code must remain data-driven so future production content can be added through configuration and existing extension points without modifying core simulation code.
