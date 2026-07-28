# Project Audit Report -- 2026-07-25 (Full Refresh)

> Scope: 16 design docs x 372 C# files x 24 asmdefs
> Method: Design contract → codebase cross-reference, every design doc scanned
> Previous P0 violations: All 4 resolved in 0095-0100
> Compilation: **0 errors** (verified via MCP console-get-logs after assets-refresh, 2026-07-25)

---

## Audit Summary

| Category | Count | Detail |
|----------|-------|--------|
| P0 Violations (must fix) | 0 | All previous P0 resolved |
| P1 Violations (should fix) | 1 | CombatSnapshot List<T> → T[] |
| Missing Types (Gameplay) | 9 | Non-deferred, design-mandated |
| Missing Types (Config) | 4 | SO/table types not yet created |
| API Minor Divergences | 2 | UnitWorld.AIController signature, naming |
| Placeholder/Incomplete | 2 | RuntimePlaceholderStageDef, EquipmentSlotView |
| Deferred Types | 6 | Phase 11/13/14 |

---

## P1: CombatSnapshot List<T> → T[] 

| File | Current | Required |
|------|---------|----------|
| CombatSnapshot.cs:8 | `List<DamageContributionTrackerSnapshot>` | `DamageContributionTrackerSnapshot[]` |
| CombatSnapshot.cs:9 | `List<DeferredCombatRequest>` | `DeferredCombatRequest[]` |
| CombatSnapshot.cs:19 | `List<DamageContributionRecordSnapshot>` | `DamageContributionRecordSnapshot[]` |

Per Snapshot Appendix v7.2 §7.1: all snapshot collections shall use T[] arrays.

---

## Missing Types (Gameplay Priority)

### GAP-1: DespawnUnit contract (Unit v27.3 §9.6.1) — HIGH

Design requires:
- `UnitDespawnRequest` struct (UnitUid, Reason, Mode)
- `UnitDespawnReason` enum (SummonExpired, OwnerRemoved, ScriptCleanup, MatchCleanup)
- `UnitDespawnMode` enum  
- `UnitWorld.DespawnUnit(in UnitDespawnRequest)` returning bool
- `UnitHandler.ClearForDespawn(reason)` — already referenced in BuffHandler design
- `BuffHandler.ClearForDespawn(reason, context)` — design mandates this

Current state: **None exist**. UnitWorld has CleanupNonHeroDeath and RemoveUnitForRollbackRestore but no unified non-death removal entry point.

### GAP-2: DeferredCombatRequestBuffer wrapper (Combat v13.2 §2.3) — MEDIUM

Design explicitly names `DeferredCombatRequestBuffer` as a formal wrapper type with `Records[]`. Current code has `DeferredCombatRequest` as individual struct used inline in `CombatSnapshot.DeferredRequests` (as `List<DeferredCombatRequest>`). The behavior is correct but the type name and wrapping structure diverge from the design appendix.

Also: `PendingDyingRecord` is named in design §1.2 step 6 but not found as a formal type in code. The combat settlement logic may handle this inline.

### GAP-3: AbilityAnimationPlan (Ability v15.2 §3.1 implied) — MEDIUM

Design defines per-ability animation plan structure parallel to AttackAnimationPlan. `AttackAnimationPlan` exists at `Assets/Scripts/Gameplay/Presentation/AttackAnimationPlan.cs`. `StageAnimationBinding` exists. But `AbilityAnimationPlan` is not yet implemented. Design expects per-ability animation configuration distinct from stage bindings.

### GAP-4: TowerTargetSelector (NonHero v5 §8) — LOW

Design names a dedicated `TowerTargetSelector` type for tower targeting priority logic. `TowerAttackHandler` (created in earlier plans) covers this functionally but doesn't match the design name. The design specifies: fixed priority targeting, no chase, tower projectile gating.

### GAP-5: BakedCastModelDef (Ability v15.2 §1.6 implied) — LOW

Design references SO-based baked cast model data. `AbilityInputProfileBaker` and `BakedPlayerAbilityInputProfile` exist at `Assets/Scripts/PlayerInput/`, but `BakedCastModelDef` as a ScriptableObject is not yet created. This would bridge authoring-time CastModelDef configuration to runtime baked data.

### GAP-6: AbilityIndicatorController (Ability v15.2 §1.5) — LOW

Design explicitly names `AbilityIndicatorController` as an independent local module separate from AbilityHandler. Current implementation has `SkillIndicatorDriver` at `Assets/Scripts/PlayerInput/SkillIndicatorDriver.cs` which handles direction/range/ground-target indicators. Functionality is covered but under a different name.

### GAP-7: UnitDespawnReason enum usage in ClearForDespawn — LOW

BuffHandler design v14.2 explicitly references `ClearForDespawn(UnitDespawnReason reason, context)`. Since `UnitDespawnReason` doesn't exist yet (GAP-1), this signature can't be implemented.

### GAP-8: GameApplicationFlowManager (FrameSync v10.2 §2) — DEFERRED

Design requires client/server application flow state machines. Current bootstrap is simpler. Explicitly deferred to Phase 14.

### GAP-9: LobbySessionFlowNetwork + CommandDispatcher + AuthorityFrameReplicator + AuthorityRecovery (FrameSync v10.2 §3, §11, §12) — DEFERRED

Network-layer types. Deferred to Phase 11/14.

---

## Missing Types (Configuration)

### GAP-10: UnitDisposePolicyTable SO (Unit v27.3 §8.5) — MEDIUM

Design: SO with `List<UnitDisposePolicy>` records, each with Id/Type/DeathPresentationTicks/RuinUnitPrototypeId. Current: `UnitDisposePolicyConfig` struct exists in `Assets/Scripts/Gameplay/Unit/Prototype/UnitDisposePolicy.cs` with `UnitDisposePolicyKind` enum and `RuinPrototypeId`. But no SO-based lookup table.

### GAP-11: GlobalParamTable SO (Unit v27.3 §8.6) — MEDIUM

Design: SO holding StatGrowthC/D, armor/magic constants, move speed scale, arrive distances, attack sequence reset interval. Current: `StatGrowthC` and `StatGrowthD` are exposed as raw `fp` properties on `UnitWorld`. Other params are hardcoded or spread across multiple locations.

### GAP-12: UnitPoolConfig + UnitPoolRegistry (Unit v27.3 §8.7) — LOW

Design: Pool configuration per UnitSubKind, registry for Prewarm/Rent/Return. Current: not implemented. Code comment in `UnitPrototype.cs:11` acknowledges: "PhysicsProfile2D, UnitRespawnConfig, UnitPoolConfig) are deferred to".

### GAP-13: PlayerSlot formal struct (Equipment/Gold v12 §8) — LOW

Design mentions a formal `PlayerSlot` struct for player identity tracking. Current: player identity is tracked via `int ControlledByPlayerSlot` on Unit. No formal struct.

---

## API Minor Divergences

### DIV-1: UnitWorld AIController registration signatures

Design (NonHero v5 §2.2):
```
RegisterAIController(UnitUid ownerUnitUid, UnitAIController controller)
UnregisterAIController(UnitUid ownerUnitUid)
TryGetAIController(UnitUid ownerUnitUid, out UnitAIController controller)
```

Code (`UnitWorld.cs:43-56`):
```
RegisterAIController(UnitAIController controller)      // No UnitUid param
UnregisterAIController(UnitAIController controller)      // Uses controller reference, not UnitUid
// TryGetAIController not found
```

The code implementation uses sorted insertion by OwnerUnitUid internally. Functional but diverges from design signatures.

### DIV-2: SkillIndicatorDriver vs AbilityIndicatorController

Design (Ability v15.2 §1.5): `AbilityIndicatorController`
Code: `SkillIndicatorDriver`

Same functional role, different name. The design describes it as "独立的本地模块" (independent local module).

---

## Placeholder / Incomplete Implementations

### PLACEHOLDER-1: RuntimePlaceholderStageDef

Found at `Assets/Scripts/Gameplay/Ability/AbilityAsset.cs:210,218,247`. A fallback `StageDef` subclass used when baked stage data is unavailable. Indicates the ability authoring bake pipeline may not be fully wired for all stage types at runtime.

### PLACEHOLDER-2: EquipmentSlotView icon

`Assets/Scripts/Bootstrap/UI/EquipmentSlotView.cs:69` has comment `// Icon placeholder`. Incomplete UI rendering.

---

## Verified Conformance (no issues)

All previously identified strong-conformance items remain aligned:

- **UnitWorld** formal death APIs: RequestEnterDying, RequestRecoverFromDying, ConfirmUnitDeath — present
- **MovementHandler.TickUpdate()** reads SimulationTickContext.Current.DeltaTick internally — fixed
- **UnitLocomotionAgent.Position** reads from PhysicsEntity2D — verified
- **PhysicsEntity2D.LateUpdate** exists — verified
- **CombatSystem** deferred request ordering: DeferredSequenceInSourceTick, legal gaps, no renumbering — verified in code
- **CombatSnapshot** stores ContributionTrackers + DeferredRequests — verified (except List→Array)
- **AuthorityFrame.SharedGameplayChecksum** required — present
- **GoldIncomeRuntime** sole ownership — verified
- **Presentation** is read-only — verified
- **Deterministic foundation** (Tick context, random, geometry, serialization, UID) — verified
- **Physics / spatial grid** (shapes, queries, grid, stable ordering, Transform sync) — verified
- **Unit / prefab composition** (Unit + Handlers as prefab-authored MonoBehaviours) — verified
- **Stats / XP** (StatHandler consolidated, XpRewardTable, LevelExperienceConfig) — verified
- **Attack** (fixed-point timing, TickRate injection, TowerAttackHandler) — verified
- **Buff** (runtime store, reactions, ownership, restore, respawn lifecycle) — verified
- **Crowd control** (stable handles, immunity, unstoppable, forced movement) — verified
- **Projectile** (stable UID, spawn lifecycle, deterministic hit order) — verified
- **Equipment / shop / gold** (fixed-point Bake/runtime, sole-owner GoldIncomeRuntime) — verified
- **Snapshot / rollback** (aggregate tree, Restore/Resolve/Rebuild) — verified
- **FrameSync / match flow** (canonical commands, AuthorityFrame, MatchFlowStateMachine) — verified
- **Player input** (callbacks enqueue local events, processing resolves aim) — verified
- **Non-hero AI** (Minion lane AI, Monster idle/chase/return AI, Tower AI) — verified
- **Movement / pathfinding** (A*, FlowField, RVO, WallPenetration, Radius Clearance) — verified
- **Bootstrap / composition root** (Client/server bootstrap, GameBootstrap) — verified
- **Presentation bridge** (EventDispatcher, AttackSfxHandler, HitReactionPresenter, DeathPresenter) — verified
- **Ability indicator** (SkillIndicatorDriver with direction/range/ground-target) — verified
- **UI / Lua** (Shop, Scoreboard, Minimap, Result, HeroSelect pages) — verified
- **Unit behavior chain** (Intent, Planner, Arbiter, RuntimeSet, Orders) — verified
- **Unit lifecycle** (RespawnConfig, DisposePolicy wired, HandlerLoadout, LocomotionProfile, PhysicsProfile2D) — verified

---

## Design Documents Fully Covered

| Design Document | Version | Implementation Status |
|---|---|---|
| FrameSync / flow / match runtime | v10.2 | Core verified; Network/Authority layer deferred |
| Snapshot / rollback schema | v7.2 | Verified (P1: List→Array) |
| Unit behavior framework | v27.3 | Verified (GAP: DespawnUnit, config SOs) |
| Combat | v13.2 | Verified (GAP: named wrappers) |
| Projectile | v19 | Verified |
| Ability | v15.2 | Partial (GAP: AnimationPlan, BakedCastModelDef) |
| Attack | v6.2 | Verified |
| Buff | v14.2 | Verified (GAP: ClearForDespawn depends on DespawnUnit) |
| Crowd control | v6.2 | Verified |
| Equipment / shop / gold | v12 | Verified (GAP: PlayerSlot) |
| Unit physics / range query | v13.1 | Verified |
| Pathfinding | v13.1 | Verified |
| Non-hero units | v5 | Verified (GAP: TowerTargetSelector naming) |
| Presentation | v13.2 | Verified |
| UI / Lua | v9.1 | Partial (placeholders in UI) |
| Player input | v1.1 | Verified |

---

## Recommended Action Order

1. **GAP-1** (DespawnUnit) — Core lifecycle gap, ~200 lines, HIGH
2. **P1** (CombatSnapshot List→Array) — Simple type change, ~50 lines
3. **GAP-2** (DeferredCombatRequestBuffer + PendingDyingRecord) — Named types, ~100 lines
4. **GAP-10** (UnitDisposePolicyTable SO) — Configuration table, ~120 lines
5. **GAP-11** (GlobalParamTable SO) — Configuration table, ~100 lines
6. **GAP-3** (AbilityAnimationPlan) — Missing type, ~100 lines
7. **GAP-4** (TowerTargetSelector) — Rename/alias, ~30 lines
8. **GAP-5** (BakedCastModelDef) — Bake pipeline, ~150 lines
9. **GAP-13** (PlayerSlot) — Minor typing, ~50 lines
10. **GAP-12** (UnitPoolConfig + UnitPoolRegistry) — Pool lifecycle, ~200 lines
11. **DIV-1** (UnitWorld AIController API adjustment) — Signature update, ~30 lines
12. **GAP-6** (AbilityIndicatorController rename) — Naming, ~20 lines
13. **PLACEHOLDER-1** (RuntimePlaceholderStageDef audit) — Verify bake pipeline, investigation
14. **PLACEHOLDER-2** (EquipmentSlotView icon) — UI polish, ~50 lines
