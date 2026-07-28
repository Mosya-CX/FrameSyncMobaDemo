# ExecPlan 0109: Design-conformance remediation program

> Status: Completed.
> Created: 2026-07-26.
> Design conformance: Strict 鈥?no deviation.
> Program rule: this document coordinates several bounded child slices. It must
> not be implemented as one repository-wide diff. Before coding a gate, create
> one child ExecPlan for that gate and keep its estimated code modification
> between 200 and 3000 lines.

## 1. Purpose

Restore the current generic framework from 鈥渃ompiles with substantial
scaffolding鈥?to a runnable and rollback-safe local Gameplay baseline that
matches the designs indexed by `Docs/Architecture/DESIGN_INDEX.md`.

The observable end state is:

- a Bootstrap scene can load frozen Inspector-authored configuration, create
  neutral geometric test Units through `UnitWorld`, and advance deterministic
  Logic Ticks;
- the same initial state and Command stream produce equal continuous and
  snapshot/restore/replay results;
- PlayerInput, AI, Attack, Ability, Projectile, Combat, Gold, movement and
  match flow use their existing authoritative contracts without parallel DTOs;
- the generic test fixtures prove the framework without introducing production
  heroes, abilities, Buffs, equipment, monsters, map content, VFX or audio;
- module status distinguishes verified behavior from partial, scaffold and
  deferred work.

## 2. Progress

- [x] Re-read the current plan standard, Roadmap, decision log, repository map,
  module status, handoff and current workflow.
- [x] Re-check the 2026-07-26 audit findings against current production code,
  Unity assets, scene composition, asmdefs and Unity MCP evidence.
- [x] Correct the audit finding about AI Intent: `Unit.Intent` now delegates to
  `BehaviorPlanner.SetIntent`, so that item is already resolved.
- [x] Define dependency-ordered remediation gates and bounded code estimates.
- [x] Gate 0 -- synchronize status/handoff with the verified baseline.
- [x] Gate 1 -- runnable composition root and neutral geometric fixtures
  (`0110_runnable_composition_root_and_neutral_test_fixtures_execplan.md`).
- [x] Gate 2 -- complete snapshot and shared-checksum coverage
  (`0111_snapshot_and_shared_checksum_completeness_execplan.md`).
- [x] Gate 3 -- correct Tick targeting, Ability signal transport, Gold order and
  authority-only match transitions
  (`0112_local_command_gold_and_authority_matchflow_execplan.md`).
- [x] Gate 4 -- recover the formal Attack/Combat-source contract
  (`0113_attack_and_combat_source_contract_recovery_execplan.md`).
- [x] Gate 5 -- route Projectile hits through Combat and formal hit filtering
  (`0114_projectile_combat_hit_pipeline_execplan.md`).
- [x] Gate 6 — complete generic Ability authoring and PlayerInput integration
  (`0115_generic_ability_authoring_and_player_input_execplan.md`).
- [x] Gate 7 — recover forced-move, Dash, RVO and radius-consistent movement
  (`0116_movement_forced_move_dash_and_rvo_conformance_execplan.md`).
- [x] Gate 8 — connect generic non-hero spawning and TeamBase match topology
  (`0117_generic_non_hero_and_match_topology_execplan.md`).
- [x] Gate 9 — correct rollback-aware Presentation and read-only UI derivation
  (`0118_presentation_event_history_and_shop_view_execplan.md`).
- [x] Gate 10 鈥?implement FrameSync authority/recovery and UOS application flow
  only after the local replay gate is green.
- [x] Run final integrated determinism validation and close this program.

## 3. Surprises and discoveries

- `0046_full_design_conformance_recovery_execplan.md`,
  `MODULE_STATUS.md` and `CURRENT_HANDOFF.md` claim that all P0/P1 findings are
  closed, but current code and assets do not support those claims.
- Compilation succeeds because many registries and scene references are valid
  when empty. Compilation therefore does not prove that a match can spawn Units
  or advance Gameplay.
- `Unit.Intent` is already an auto-synchronizing property. AI callers that assign
  it do reach `BehaviorPlanner`; the remaining behavior defect is the loss of
  `AbilityVerb` when a Cast Command becomes a persistent Intent/Action.
- The open `ClientBootstrap` scene is dirty and is not in Build Settings. Do not
  save or overwrite user scene changes without first inspecting the current
  serialized state through Unity MCP.
- `GlobalPrefabTable.asset` contains no entries, and no neutral Unit prefab or
  Inspector-authored Unit prototype asset exists.
- `FrameSyncGameRuntime.ExecuteOneTick` exists, but no production driver calls
  it. `GameBootstrap.Update` currently advances only shop presentation.
- Snapshot capture restores many Handler states, but omits future-affecting
  Planner/Intent/Action and Dash state. Shared checksum also omits existing
  CombatModifier and Locomotion state.
- Natural gold is not instantiated. Its current pipeline hook is nevertheless
  after `GoldIncomeRuntime.SealTick`, so direct activation would fail.
- Projectile damage directly changes HP and Projectile Buff lookup is an
  explicit null implementation. This bypasses formal Combat settlement.
- Existing Attack tests encode sequence advancement at Begin, which conflicts
  with Attack v6.2 and Combat v13.2.
- NGO and UOS packages are installed, but no authoritative transport/application
  composition exists. This remains a later gate, not a prerequisite for local
  deterministic repair.
- Gate 2 confirmed that `ActionRuntimeSet` is dormant: no production
  `IActionRuntime` implementation or `Add` caller exists. The real action state
  remains in Handlers, so capture now rejects a future non-empty set instead of
  serializing unreconstructable interface references.
- Forced-movement persistence is already owned by CrowdControl; Dash is the
  missing Movement-owned cross-Tick state. `CombatModifierSet.Detach` also had a
  shifted-index bug discovered while canonicalizing its snapshot.
- Gate 3 found the Ability verb/Aim loss in both local Command dispatch and the
  AI/script Order path. Both now carry the existing canonical contracts rather
  than introducing an AI-specific protocol.
- Gate 8 found that the earlier Minion AI still bypassed the semantic Order
  chain and duplicated its current attack target in AI Snapshot. The final
  implementation resolves lane/camp destinations through existing Orders,
  keeps current targets in Unit Intent, and snapshots only AI-owned timing and
  state.
- Gate 9 found a Tick-plus-UID hash cache that was cleared every render frame
  and shared by VFX/SFX. It is replaced by complete-ID, bounded per-stream
  histories; current gold now comes from the formal bound shop view.
- Future Commands did not need a new wire schema: the existing `TargetTick`
  field was correct, while collector consumption and lifecycle were not.
- Gate 10 validation exposed two fixture defects rather than Gameplay defects:
  the smoke test omitted the required Ability catalog, and the two Ability
  ScriptableObject classes lived in a non-matching source filename. Splitting
  them into matching files restored stable Unity serialization.
- Unity-MCP 0.84.3 can report a misleading `No tests found` when its five-second
  test-list retrieval times out. Its filter validation also ORs supplied
  filters while Unity execution applies its own filter combination. Exact,
  fully-qualified `testMethod` is the reliable focused-test workaround.

## 4. Decision log

- Use only current designs listed by `DESIGN_INDEX.md`; do not revise designs to
  legalize current shortcuts.
- Treat current repository code and Unity assets as the implementation baseline,
  but not as authority over the current designs.
- Keep Unit and current Handlers as prefab-authored MonoBehaviours. Plain C#
  remains appropriate for deterministic values, services, algorithms and
  runtime collections.
- Use primitive Unity geometry to create neutral test prefabs. These assets are
  framework fixtures and visual placeholders, not production content.
- Inspector-facing authoring may use `float`; Bake/initialization converts once
  to `fp`. Gameplay state, calculations, snapshots and checksums remain fixed
  point.
- Repair existing public contracts in place. Do not create a second UID,
  Command, Snapshot, Aim, AbilitySignal, FixedPoint, Checksum, source descriptor
  or runtime DTO.
- Snapshot schema changes and checksum changes happen in the same child slice so
  restore and verification cannot temporarily disagree.
- Until a concrete restorable `IActionRuntime` contract exists, a non-empty
  `ActionRuntimeSet` is a deterministic snapshot-boundary failure, not a new
  parallel snapshot protocol.
- `GameplaySnapshot` schema 5 writes Intent, Dash, Locomotion and
  CombatModifier state; Combat modifiers use strictly increasing `ModifierId`.
- Formal Combat source semantics use the existing design-owned
  `CombatRequestHeader.SourceDescriptor`. Do not retain Attack animation sequence
  as a DamageRequest/on-hit discriminator.
- Local deterministic correctness precedes network authority integration.
- Use focused tests during each slice. Do not run the full EditMode/PlayMode
  suite at every checkpoint.
- No design deviation is approved or required by this program. If a child slice
  discovers a real conflict between current designs, stop only that slice and
  record the exact sections.

## 5. Current repository context

### Unity and assets

- Unity version: `2022.3.62f1c1`.
- Current Unity MCP compile/Console baseline: no compiler Error and no Warning
  after synchronous refresh on 2026-07-26.
- Open scene: `Assets/Scenes/ClientBootstrap.unity`; loaded, dirty, build index
  `-1`.
- `ProjectSettings/EditorBuildSettings.asset` contains only disabled
  `Assets/Scenes/SampleScene.unity`.
- Runtime assets currently present:
  - `Assets/Config/Runtime/GlobalGameplayData.asset`
  - `Assets/Config/Runtime/GlobalPrefabTable.asset`
  - `Assets/Input/Gameplay.inputactions`
- `GlobalPrefabTable.asset` has an empty `prefabGroups` collection.
- Client Bootstrap has PlayerInput and Camera references, but presentation,
  indicator, Lua/UI and Jungle configuration references are unassigned.

### Assemblies and dependency direction

The project has 22 project asmdefs plus two vendor asmdefs under Assets.
The intended project direction remains:

```text
RuntimeConfig / Deterministic / Physics
    -> Unit
    -> FrameSync
    -> PlayerInput / LuaBridge
    -> Bootstrap
```

No project assembly cycle was found. Child slices must preserve this direction.
Deterministic Gameplay assemblies must not depend on Bootstrap, Presentation,
Input devices, NGO transport or UOS implementations.

### Primary current code

- Composition: `Assets/Scripts/Bootstrap/GameBootstrap.cs`
- Runtime: `Assets/Scripts/FrameSync/FrameSyncGameRuntime.cs`
- Tick pipeline: `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`
- Rollback/checksum:
  - `Assets/Scripts/FrameSync/GameplaySnapshot.cs`
  - `Assets/Scripts/FrameSync/SharedGameplayChecksum.cs`
  - `Assets/Scripts/FrameSync/PredictionRollbackCoordinator.cs`
- Unit/behavior:
  - `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`
  - `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`
  - `Assets/Scripts/Gameplay/Unit/Core/BehaviorPlanner.cs`
  - `Assets/Scripts/Gameplay/Unit/Core/ActionRuntimeSet.cs`
- Movement:
  - `Assets/Scripts/Gameplay/Movement/MovementHandler.cs`
  - `Assets/Scripts/Gameplay/Pathfinding/RvoOrchestrator.cs`
  - `Assets/Scripts/Gameplay/Pathfinding/DeterministicRVOSystem.cs`
- Attack/Combat:
  - `Assets/Scripts/Gameplay/Attack/AttackHandler.cs`
  - `Assets/Scripts/Gameplay/Attack/AttackSnapshot.cs`
  - `Assets/Scripts/Gameplay/Combat/DamageRequest.cs`
  - `Assets/Scripts/Gameplay/Combat/CombatSystem.cs`
- Ability/Input:
  - `Assets/Scripts/Gameplay/Ability/AbilityAsset.cs`
  - `Assets/Scripts/Gameplay/Ability/AbilityHandler.cs`
  - `Assets/Scripts/Gameplay/Ability/AbilityRuntime.cs`
  - `Assets/Scripts/PlayerInput/PlayerCommandRequester.cs`
  - `Assets/Scripts/PlayerInput/AbilityInputProfileProvider.cs`
- Projectile:
  - `Assets/Scripts/Gameplay/Projectile/ProjectileWorld.cs`
  - `Assets/Scripts/Gameplay/Projectile/ProjectileEffectDispatcher.cs`
  - `Assets/Scripts/FrameSync/ProjectileHitResolver.cs`
- Gold/match:
  - `Assets/Scripts/FrameSync/GoldIncomeRuntime.cs`
  - `Assets/Scripts/FrameSync/NaturalGoldIncomeSystem.cs`
  - `Assets/Scripts/FrameSync/MatchRuleRuntime.cs`
  - `Assets/Scripts/FrameSync/MatchFlowStateMachine.cs`
- Presentation/UI:
  - `Assets/Scripts/Bootstrap/PresentationEventDispatcher.cs`
  - `Assets/Scripts/LuaBridge/UiSnapshotDto.cs`

## 6. Exact design sources

- `Docs/Architecture/DESIGN_INDEX.md`
- `Docs/Architecture/DECISION_LOG.md`
  - D-001 through D-004: Tick, authority, recovery and snapshot phases
  - D-005 through D-007: Gold ownership and derived available gold
  - D-008 through D-013: Unit lifecycle, Combat and Projectile snapshots
  - D-014 through D-018: Presentation, PlayerInput and AI boundaries
  - D-019 through D-024: prefab kinds, framework/content, fixed point, tests and
    accepted repository baseline
- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`
  - 5: GoldIncome ordering and confirmation
  - 8: Tick ownership, accumulator and prediction lead
  - 9: Command TargetTick
  - 12: AuthorityFrame, recovery and SharedGameplayChecksum
  - 14: full per-Tick execution order
- `Docs/Design/FrameSync_Snapshot_Contents_Appendix_v7_2.md`
  - 5: Unit aggregate state including Behavior/Intent/Action
  - 6: restore/resolve/rebuild and checksum equivalence
- `Docs/Design/unit_behavior_framework_design_v27_3.md`
  - 1.6: UnitPrototype
  - 3: Intent, Planner, Action and ownership
  - 7: UnitWorld and lifecycle
  - 8: frozen global tables and prefab lookup
- `Docs/Design/moba_attack_module_design_v6_2.md`
  - 2鈥?: Attack state, Begin, Commit and output
  - successful-Commit sequence advancement and lazy idle reset
- `Docs/Design/moba_combat_system_design_v13_2.md`
  - 3鈥?: SourceDescriptor and Attack source semantics
  - 7: DamageRequest and settlement
  - snapshot/deferred request appendices
- `Docs/Design/MOBA_FrameSync_Unity_Projectile_System_Design_v19.md`
  - ProjectileDef/TargetFilter
  - hit candidate ordering and hit policy
  - HitModule to Combat request boundary
  - snapshot lifecycle
- `Docs/Design/moba_ability_system_design_v15_2.md`
  - AbilityDef, CastConditions and AbilityCostPlan
  - Session/Stage/signal transitions
  - Bake validation and indicator-stage ownership
- `Docs/Design/MOBA_Player_Input_Command_Module_Design_v1_1.md`
  - CastModelDef-derived input profiles
  - Focus/Commit duplicate suppression
  - no duplicate timing/range/damage configuration
- `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md`
  - Locomotion priority
  - RvoGrid neighbor selection
  - DashRuntime and ForcedMoveRuntime
  - Radius/RadiusClass consistency
- Current indexed Buff, CrowdControl, Equipment/Gold, Physics, NonHero,
  Presentation and UI/Lua designs for their owning gates.

## 7. Scope

### In scope

- Correct every confirmed P0/P1 implementation mismatch recorded in this plan.
- Add Inspector-backed generic authoring assets needed to populate existing
  runtime tables without hard-coded production registration.
- Create neutral geometric test fixtures and a smoke scene through Unity MCP.
- Add the minimum automated behavior tests for each repaired feature.
- Correct snapshot schema, canonical serialization and shared checksum where
  required by current designs.
- Correct scene/build composition required to run the generic framework.
- Update `MODULE_STATUS.md`, `CURRENT_HANDOFF.md`, and only structurally affected
  `REPOSITORY_MAP.md` entries as each child closes.

### Out of scope

- Named or production heroes, ability kits, Buffs, equipment, minions, monsters,
  map objects or balance values.
- Final art, animation, audio, VFX, UI styling or production map layout.
- Fog of war.
- New packages.
- Host mode, offline Gameplay, mid-match join, client/server process restart
  recovery.
- P2-only cleanup unless required to make a touched P0/P1 implementation correct.
- Full-suite test execution at every child checkpoint.

## 8. Dependency-ordered implementation plan

Each gate below becomes one child ExecPlan immediately before implementation.
The child must list exact types, line estimate, tests and Unity MCP validation.
Do not start the next gate until the current gate compiles and its focused tests
pass.

### Gate 0 鈥?status and baseline synchronization

Estimated code modification: 0 lines; documentation-only start of the first
implementation session.

1. Correct `MODULE_STATUS.md` classifications using the 2026-07-26 evidence.
2. Replace stale P0/P1 counts and mark the old 528/529 test result as historical
   until revalidated.
3. Update `CURRENT_HANDOFF.md` with this program and the current dirty-scene
   constraint.
4. Mark unfinished or duplicate historical plans as historical without deleting
   evidence.

### Gate 1 鈥?runnable composition root and neutral geometric fixtures

Estimated modification: 700鈥?200 code lines plus Unity assets.

Observable result:

- a dedicated smoke scene loads frozen configuration;
- one or more neutral Capsule/Cube/Sphere-based Unit prefabs spawn through the
  formal `UnitSpawnRequest -> GlobalUnitPrototypeTable -> GlobalPrefabTable`
  chain;
- a bounded Unity-frame accumulator advances Logic Ticks according to FrameSync
  v10.2 and `MaxLogicTicksPerUnityFrame`;
- no test-only object map or parallel Unit runtime is introduced.

Work:

1. Add Inspector-backed authoring/bake assets for the existing runtime
   UnitPrototype and StatDefinition tables. Reuse the existing runtime table
   types rather than replacing them.
2. Add required serialized references to the appropriate global configuration
   or Bootstrap surface and fail visibly when required tables are absent.
3. Populate `GlobalPrefabTable.asset` with stable neutral fixture entries.
4. Through Unity MCP, create a root Unit prefab with primitive geometry and the
   required `Unit`, `PhysicsEntity2D`, Stat, Movement, Attack, Ability, Buff, CC
   and Equipment components.
5. Add a minimal deterministic Tick driver at the Bootstrap/application layer.
6. Add a neutral smoke scene or PlayMode fixture; do not auto-spawn test content
   in a production match scene.
7. Put the intended Bootstrap scenes into Build Settings after inspecting the
   current dirty scene and preserving user changes.

Focused validation:

- EditMode: table Bake, duplicate/invalid IDs, missing prefab and stat failure.
- PlayMode: prefab composition, Unit spawn, stable UID/query identity, several
  Logic Ticks, presentation Transform remains non-authoritative.

### Gate 2 鈥?aggregate snapshot and checksum completeness

Estimated modification: 800鈥?400 lines.

Observable result:

- continuous execution and snapshot/restore/replay preserve Unit Intent,
  action-reservation state, Dash/forced-move state, Combat modifiers and
  locomotion;
- any difference in those shared states changes SharedGameplayChecksum.

Work:

1. Add the formal serializable Behavior/Intent/Action aggregate to
   `UnitSnapshot`. Do not snapshot derived presentation state.
2. Either make current ActionRuntime state real and snapshot-capable or remove
   unused runtime reservation from future-affecting behavior in the same formal
   slice. Follow Unit v27.3; do not keep a half-owned state path.
3. Move Dash/ForcedMove future state into formal Movement snapshot members.
4. Write CombatModifier and Locomotion state into the canonical checksum.
5. Ensure every new array/list is captured in explicit stable order.
6. Increment `GameplaySnapshot.SchemaVersion`; reject old incompatible snapshots
   deterministically rather than silently repairing them.

Focused validation:

- round-trip equality;
- restore/resolve/rebuild;
- continuous versus replay;
- checksum sensitivity for each added state;
- insertion-order independence;
- missing stable references fail deterministically.

### Gate 3 鈥?FrameSync local command, gold and match-flow correctness

Estimated modification: 550鈥?50 lines.

Observable result:

- local Commands target a legal future Tick;
- CastAbility preserves its original `AbilitySignalVerb`;
- natural gold is generated inside the open batch and replays identically;
- predicted client ticks cannot finalize an authority-only match result.

Work:

1. Preserve `AbilityVerb` in the existing Intent/Action language. If the current
   Intent cannot represent it, extend that formal existing contract and its
   snapshot; do not add a second ability-control DTO.
2. Introduce the design-owned TargetTick resolver:
   `max(LocalSimulationTick + 1,
   LatestSynchronizedServerTick + MinCommandLeadTicks)`.
3. Inject the resolver and actual build Tick into `PlayerCommandRequester`.
4. Instantiate `NaturalGoldIncomeSystem` from baked global configuration and run
   it immediately after `GoldIncomeRuntime.BeginTick`.
5. Derive natural-income scheduling from deterministic Tick/phase or snapshot
   its minimal future-affecting state.
6. Ensure `SealTick` remains near Tick end before digest/checksum creation.
7. Remove Bootstrap's unqualified authority evaluation. Authority-only match
   transitions remain inside the execution-mode-aware pipeline.

Focused validation:

- Focus/Commit verb preservation;
- minimum Command lead;
- natural-income interval and replay equivalence;
- Gold batch digest equality;
- client prediction cannot enter Ending from an unconfirmed base death.

### Gate 4 鈥?Attack and Combat source-contract recovery

Estimated modification: 550鈥?50 lines.

Observable result:

- attack sequence advances only after successful Gameplay Commit;
- canceled/failed attacks consume no sequence;
- first and wrapped sequence values still produce correct Attack-source on-hit
  behavior;
- rollback during windup/recovery produces the same result and presentation
  progress.

Work:

1. Align `AttackSnapshot` with Attack v6.2, including the last successful Commit
   Tick and all future-affecting resolved attack timing/state.
2. Read the reset threshold from frozen global static configuration.
3. Apply lazy idle reset immediately before the next Begin.
4. Increment the byte sequence only after a successful direct DamageRequest or
   Projectile spawn request.
5. Remove `AttackSequenceIndex` from `DamageRequest`, Combat events and checksum
   payloads where it is being used as Gameplay source identity.
6. Use the formal `CombatRequestHeader.SourceDescriptor` with
   `SourceType=Attack` and the appropriate stable source ID/Recipe ID.
7. Fix recovery animation progress without allowing presentation to write
   Gameplay.

Focused validation:

- Begin/cancel/fail/Commit sequence semantics;
- byte wrap to zero;
- direct and Projectile attacks derive on-hit eligibility from SourceDescriptor;
- snapshot during windup and recovery;
- SFX `PresentationEventId.EventSequence` uses the committed attack sequence.

### Gate 5 鈥?Projectile to Combat integration

Estimated modification: 900鈥?500 lines.

Observable result:

- a neutral Projectile prefab moves deterministically, selects legal targets in
  canonical order, submits formal Combat requests and obeys its hit policy;
- Projectile damage can trigger shields, modifiers, contribution, Dying/Death
  and on-hit exactly through CombatSystem;
- configured generic Buff/CC hit effects resolve through their owning registries.

Work:

1. Replace direct HP mutation with formal DamageRequest submission.
2. Resolve Buff definitions from the existing authoritative Buff registry; fail
   invalid static configuration during Bake instead of silently doing nothing.
3. Add the minimum formal `ProjectileTargetFilter` and hit-policy fields required
   by the current slice.
4. Use target Physics shape/radius for narrow phase.
5. Sort candidate results by hit distance and stable UID.
6. Stop processing after `DestroyOnFirstHit` or exhausted hit/pierce policy.
7. Align advance, lifecycle, hit emission and destroy flush order with
   Projectile v19.
8. Bind neutral projectile prefabs through `GlobalPrefabTable` and
   `PhysicsEntity2D`.

Focused validation:

- friendly/hostile/targetability filters;
- equal-distance UID tie;
- no extra same-Tick hit after destruction;
- shield/modifier/death path;
- Projectile snapshot and replay equivalence.

### Gate 6 鈥?generic Ability authoring and PlayerInput integration

Estimated modification: 900鈥?600 lines.

Observable result:

- neutral Commit, local-aim and hold-release Ability assets Bake in the Inspector
  and execute through the existing Ability signal/session/stage language;
- invalid assets fail visibly;
- PlayerInput derives profiles and indicators from the baked formal definitions.

Work:

1. Add the missing current-design AbilityCostPlan semantics: level values,
   optional health cost and formal CostTiming.
2. Add generic start checks and current-design CastConditions/target checks.
3. Perform checks and Stage resolution before cost consumption; apply cost only
   at the configured formal timing.
4. Remove `RuntimePlaceholderStageDef` success fallback. Missing StageDef is a
   Bake error.
5. Stop `AbilityDefinitionRegistry.TryRegisterFromAsset` from swallowing
   deterministic configuration exceptions.
6. Populate Ability registries from Inspector-authored assets at Bootstrap.
7. Use `AbilityInputProfileProvider.CreateFromAbilityHandler` and provide one
   real `ILocalAbilityRuntimeView`.
8. Remove or migrate `BakedCastModelDef` fields that duplicate Gameplay timing
   or guess AimKind. Indicator stage and shape are resolved from formal
   CastModelDef/StageDef/AbilityRuntime data.

Focused validation:

- resource/health cost timing;
- failed start consumes nothing;
- invalid/missing stage fails Bake;
- Focus then release/primary Commit;
- duplicate Commit suppression;
- right click does not cancel activated hold-release;
- rollback uses execution Ticks.

### Gate 7 鈥?movement, forced movement and RVO conformance

Estimated modification: 750鈥?300 lines.

Observable result:

- every Unit uses its configured Physics radius/radius class consistently for
  pathfinding, movement, wall correction and RVO;
- forced movement, Dash and route movement have one deterministic priority;
- idle agents participate in RVO with zero desired velocity.

Work:

1. Make `MovementHandler` consume the Unit's baked Physics shape/radius rather
   than a hard-coded `0.5`.
2. Execute exactly one of ForcedMove, Dash, Route or Idle per Tick in the formal
   priority.
3. Have CrowdControl own the control instance while Movement owns and snapshots
   the resolved ForcedMove runtime.
4. Validate positive Dash duration and remove unused timing state.
5. Use `PhysicsWorld.RvoGrid` movement-before positions for neighbor candidates.
6. Include idle agents and sort neighbors by the design-owned stable keys.
7. Reuse buffers to avoid obvious per-Tick allocation in the touched path.

Focused validation:

- forced move does not also route-move;
- Dash/forced state round trip;
- small/medium/large wall behavior;
- idle obstacle avoidance;
- insertion-order-independent RVO output.

### Gate 8 鈥?generic non-hero and match topology

Estimated modification: 750鈥?400 lines.

Observable result:

- neutral lane fixtures spawn configured waves through UnitWorld;
- neutral jungle fixtures create, register, die, reset and respawn through formal
  lifecycle ownership;
- two generic TeamBase Units can drive the existing authority-confirmed victory
  condition.

Work:

1. Add Inspector-authored neutral MinionWave and JungleCamp fixture assets.
2. Connect `ProcessWave` to explicit lane/spawn topology; do not infer topology
   from scene hierarchy order.
3. Spawn camp members from stable prototype IDs and register their origin/member
   state.
4. Connect AI controllers through the existing Planner/Ability contracts.
5. Add generic TeamBase registration using existing UnitKind/prototype data.
6. Register bases during map/bootstrap initialization in stable team order.

Focused validation:

- wave/camp spawn ordering;
- AI active gate;
- death unregister and respawn;
- snapshot/replay of manager state;
- only an authority-confirmed base death finalizes a match.

### Gate 9 鈥?Presentation and UI derivation

Estimated modification: 450鈥?50 lines.

Observable result:

- rollback/replay does not duplicate already-consumed VFX/SFX;
- distinct events from the same source and Tick remain distinct;
- UI displays derived current gold without writing Gameplay.

Work:

1. Deduplicate with the complete `PresentationEventId`.
2. Keep a bounded rollback-aware consumption history instead of clearing the
   cache every rendered frame.
3. Keep VFX and SFX semantics distinct while preserving one event identity
   contract.
4. Derive `CurrentAvailableGold` from confirmed income plus effective predicted
   shop delta; retain `ConfirmedGold` separately.
5. Wire neutral presentation/UI fixture references in the smoke scene without
   making Presentation a Gameplay dependency.

Focused validation:

- replayed event suppression;
- two events with different EventKey/sequence both dispatch;
- UI gold derivation after purchase/sell/undo and authority confirmation;
- no Presentation-to-Gameplay write path.

### Gate 10 鈥?authority, recovery and UOS application flow

Estimated modification: 1400鈥?600 lines. This gate may be split into
`authority/recovery` and `UOS application flow` if implementation evidence
exceeds 3000 lines.

Precondition:

- Gates 1鈥? compile;
- continuous versus local replay checks are green;
- no unresolved P0/P1 remains in shared Gameplay state.

Observable result:

- client and Dedicated Server exchange the existing canonical Commands and
  AuthorityFrames continuously;
- missing AuthorityFrames use the formal recovery protocol;
- prediction lead and predicted match-end pauses work;
- UOS matchmaking/application flow owns transport/session lifecycle but not
  deterministic Gameplay semantics.

Work:

1. Add the design-owned GameplayCommandBundle, AcceptedCommandRelay,
   AuthorityFrameReplicator and recovery application services.
2. Implement continuous one-Tick authority acceptance and canonical byte/checksum
   comparison.
3. Apply configured recovery retry/attempt limits and request-sequence checks.
4. Enforce prediction-lead and match-end candidate pauses.
5. Add Dedicated Server Tick ownership and MatchStartPayload.
6. Integrate installed NGO/UOS packages only at Bootstrap/application assemblies.

Focused validation:

- command/checksum mismatch rollback;
- missing-frame recovery;
- no rollback across authority boundary;
- disconnect when recovery anchor is unavailable;
- client/server checksum equality;
- no transport dependency in deterministic Gameplay assemblies.

## 9. Public contracts and ownership

Anticipated in-place public-contract changes:

- `UnitSnapshot` gains formal Behavior/Intent/Action state.
- Movement snapshot gains formal Dash/ForcedMove state.
- `GameplaySnapshot.SchemaVersion` increments with deterministic old-schema
  rejection.
- SharedGameplayChecksum writes every shared future-affecting state.
- Existing UnitIntent/CastAction representation preserves AbilitySignalVerb.
- `DamageRequest` is aligned to formal `CombatRequestHeader`,
  `SourceDescriptor`, Recipe and BaseValue semantics; Attack animation sequence
  is removed from Combat identity.
- AbilityCostPlan and Ability authoring gain the current formal cost/condition
  fields.
- ProjectileDef gains only the current slice's formal TargetFilter/hit-policy
  fields.
- Inspector authoring assets Bake into existing runtime tables and registries.

Ownership remains:

```text
Bootstrap/application
    Unity frame scheduling, scenes, UOS/NGO, presentation and asset composition

FrameSync
    Tick orchestration, Command history, snapshot aggregation, checksum,
    authority/recovery and GoldIncomeRuntime

Unit/Gameplay
    Unit, Handler, Combat, Attack, Ability, Buff, CC, Projectile, movement,
    non-hero and match-domain runtime semantics

Physics
    deterministic spatial state, shape, grids and query geometry

PlayerInput
    local device-event processing and one-time Gameplay Command creation

Presentation/UI/Lua
    read-only consumers of Gameplay outputs
```

No child may reverse these dependencies.

## 10. Deterministic ordering, snapshot and serialization rules

- Unit iteration: ascending UnitUid.
- Commands: existing canonical CommandCollector order and complete canonical
  bytes.
- Behavior/Action state: explicit stable slot/kind/sequence keys; never component
  registration order.
- Projectile candidates: HitDistance then stable runtime UID.
- RVO: stable UnitUid and design-owned neighbor/tie ordering.
- Gold requests: formal fixed source order and ascending PlayerSlot where owned.
- Match topology: stable TeamId/lane/camp IDs.
- No authoritative Dictionary/HashSet enumeration.
- No Unity object identity, hierarchy order, Transform or physics authority.
- No `float`/`double` in authoritative calculations.
- Restore remains three separate phases: Restore, Resolve, Rebuild.
- Invalid stable references fail deterministically.

## 11. Validation

### Normal child-slice checkpoint

1. Use Unity MCP to refresh assets and trigger script compilation.
2. Wait until Unity is neither compiling nor updating.
3. Read Console Error, Exception, Assert and relevant Warning entries.
4. Run the smallest relevant EditMode test assembly/class/method.
5. Run PlayMode tests only for GameObject, scene, prefab, Input System,
   presentation or UI behavior.
6. Review only the child diff against its exact design sections.
7. Update the child ExecPlan, affected status row and handoff.

### Required deterministic checks by owning child

- same initial state and Command sequence executed twice;
- continuous versus snapshot/restore/replay;
- reversed insertion order produces the same canonical output;
- invalid configuration/reference fails deterministically;
- checksum changes when shared future state changes.

### Program closure

After all local gates and authority integration:

- Unity compilation and Console;
- full relevant EditMode suites;
- required PlayMode suites;
- long deterministic simulation;
- randomized canonical Command stream;
- client/server checksum equality;
- no duplicate protocol type search;
- asmdef dependency/cycle check;
- final scene/config serialized-reference inspection through Unity MCP.

Do not save the currently dirty ClientBootstrap scene merely to run tests. First
inspect and preserve the user's changes; use a dedicated neutral smoke scene when
practical.

## 12. Failure and recovery

- Each gate must finish at a compileable checkpoint and can be resumed from its
  child Progress section.
- Do not execute two gates in the same diff merely because they touch the same
  file.
- If a gate exceeds 3000 estimated changed code lines, close at the nearest
  compileable point and split the remainder into another child.
- If a current-design public-contract conflict is found, record both exact
  sections and stop only the affected gate. Continue documentation or unaffected
  validation work.
- Do not keep compatibility shims that preserve a known incorrect deterministic
  contract.
- Do not delete or weaken tests to pass.
- Do not add packages.
- Unity assets are created/modified through Unity MCP. Manual YAML editing is a
  fallback only when MCP cannot perform the explicit required operation.

## 13. Completion criteria

This program is complete only when:

- the generic local smoke scene can spawn and advance deterministic Units;
- every confirmed P0/P1 in this plan is fixed or explicitly reclassified with
  new repository evidence;
- snapshot/restore/replay and shared checksum cover all integrated shared state;
- Attack, Ability and Projectile use formal Combat/source semantics;
- movement priorities and radii are consistent;
- input callbacks remain non-authoritative and signals survive Command routing;
- Presentation/UI remain read-only;
- network/authority is either implemented and verified or still honestly marked
  Deferred without calling FrameSync Verified;
- no production content was introduced;
- status, repository map and handoff match the actual repository;
- actual code modification totals are reported for every child.

## 14. Results

Execution began on 2026-07-26.

- Gate 0 corrected `MODULE_STATUS.md` and `CURRENT_HANDOFF.md`: prior
  `Verified`, P0/P1=0 and 528/529 claims are now historical where the current
  repository does not demonstrate them.
- Gate 1 is owned by
  `0110_runnable_composition_root_and_neutral_test_fixtures_execplan.md`.
  It implemented and MCP-validated the frozen Unit/stat catalog, neutral prefab,
  stable Tick-0 spawn and bounded Tick driver. Focused Test Runner execution is
  pending only because the open ClientBootstrap scene is dirty.
- Gate 2 is owned by
  `0111_snapshot_and_shared_checksum_completeness_execplan.md`. It implemented
  schema 5 Intent/Dash/Locomotion/CombatModifier capture, strict restore
  validation and checksum coverage, fixed shifted CombatModifier indices, and
  added focused deterministic tests. Unity MCP compilation and a real-asset
  two-Unit snapshot round trip passed; removing Intent changed the checksum.
- Gate 3 is owned by
  `0112_local_command_gold_and_authority_matchflow_execplan.md`. It implemented
  formal local TargetTick resolution, exact-Tick future-command consumption,
  end-to-end Ability verb/Aim preservation, Tick-derived natural gold inside the
  open batch, and authority-only match mutation. GameplaySnapshot is now schema
  6. Unity MCP compilation and direct behavior validation passed; the existing
  dirty scene prevented focused Test Runner execution without risking user
  changes.
- Gate 4 implemented the formal Attack lifecycle and Combat source header.
  Unity compiled; seven focused MCP behavior checks passed. The dirty scene was
  not saved or discarded.
- Gate 5 implemented deterministic Projectile motion/filtering/hit policy,
  formal Combat/Buff/CC effect routing, prefab pooling and schema-8 rollback
  state. Unity compiled and six focused MCP checks passed.
- Gate 6 implemented strict Ability authoring/catalog/loadout composition,
  formal cost timing and aim checks, hold-release state, read-only PlayerInput
  profiles and schema-9 snapshot/checksum coverage. Unity compiled; eight
  focused checks and asset reload validation passed.
- Gate 7 made PhysicsEntity2D the sole pose owner, implemented deterministic
  ForcedMove/Dash/Route priority and stable idle-aware RVO, and reduced
  MovementSnapshot to its formal Dash/ForcedMove members (schema 11). Unity
  compiled; 18 MovementHandler, 7 conformance and 4 snapshot/checksum tests
  passed.
- Gates 8 and 9 implemented stable non-hero topology, TeamBase match ownership,
  rollback-aware presentation history and read-only UI derivation.
- Gate 10A (`0119`) implemented canonical command bundle/relay,
  AuthorityFrame production/archive, recovery and prediction limits; its
  focused suite passed 8/8.
- Gate 10B (`0120`) implemented Bootstrap-owned UOS/NGO application flow,
  lobby/start/result contracts and the NGO FrameSync bridge. Bootstrap EditMode
  passed 36/36.
- Final touched-path repairs completed component-aware Equipment Shop
  transactions/undo, formal Combat modifier formula/policy evaluation, Tower
  target single ownership and canonical shop UI Commands.
- The real `FrameworkSmoke` scene now binds a valid, persistable Ability runtime
  catalog. Its EditMode bootstrap check passed 1/1 and scene PlayMode check
  passed 1/1.
- Final Unity state is idle after successful compilation. Live external UOS
  service and multi-process NGO validation remain outside the local framework
  remediation.


