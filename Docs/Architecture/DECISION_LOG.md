# FrameSyncMobaDemo — Frozen Architecture Decision Log

> This log records decisions that override older examples or superseded design text.  
> Entries are normative unless their status changes.

## D-001 — Tick semantics

**Status:** Frozen

```text
ServerTick
    Next authoritative Tick the server will execute.

LatestAuthorityFrameTick
    Latest continuous AuthorityFrame fully accepted.

LocalSimulationTick
    Next client Gameplay Tick.

SnapshotTick
    Next Gameplay Tick to execute after restore.
```

Ordinary rollback starts at or after:

```text
LatestAuthorityFrameTick + 1
```

## D-002 — AuthorityFrame verification

**Status:** Frozen

`AuthorityFrame.SharedGameplayChecksum` is required.

AuthorityFrame comparison uses complete canonical Command bytes.

The checksum includes `GoldIncomeBatchDigest[T]` and all shared cross-Tick Gameplay state required by the current designs.

The client stores `LocalFrameVerificationRecord` for unconfirmed Ticks.

## D-003 — AuthorityRecovery scope

**Status:** Frozen

AuthorityRecovery only retransmits missing AuthorityFrames.

It does not provide BaseSnapshot, mid-match join, process-restart recovery, or external gold state.

If the client no longer has the local recovery snapshot, its current match connection terminates.

## D-004 — Snapshot frequency and restore phases

**Status:** Frozen

Snapshot interval is one Tick.

Restore phases are:

```text
Restore
Resolve
Rebuild
```

Tick-local transient state is not saved at Tick-end capture unless explicitly defined as cross-Tick state.

## D-005 — Gold runtime ownership

**Status:** Frozen

`GoldIncomeRuntime` is the unique match-runtime owner of:

```text
Current batch builder
Unconfirmed income batches
Gold income batch digests
Confirmed earned totals
Confirmed income progress
```

FrameSync does not create a second predicted batch cache or confirmed ledger.

Account identity runtime stores no match gold total.

## D-006 — Gold confirmation does not replay later prediction

**Status:** Frozen

Confirming Tick `T` income:

```text
Advances confirmed earned gold.
Does not scan later Purchase or Undo Commands.
Does not create a gold-specific Dirty Tick.
Does not actively replay later predicted Ticks.
Does not retroactively create a locally rejected Command.
```

A conservative remote shop prediction is corrected only when that Command Tick's AuthorityFrame is processed.

## D-007 — CurrentAvailableGold

**Status:** Frozen

```text
CurrentAvailableGold =
    GoldIncomeRuntime.GetConfirmedEarnedGoldTotal(player)
    + EffectiveShopGoldDelta
```

It is derived, read-only, not synchronized as state, and not stored in GameplaySnapshot.

## D-008 — Unit active timing

**Status:** Frozen

A spawned unit exists and can participate passively during its spawn Tick.

Active AI/order/planner/action/movement/attack/active-ability work begins only when:

```text
CurrentTick > UnitUid.SpawnLogicTick
```

No separate FirstActive or FirstAI Tick state is stored.

## D-009 — Formal death and modifier ownership

**Status:** Frozen

Combat writes Dying/Dead synchronously through UnitWorld.

Formal APIs:

```text
RequestEnterDying
RequestRecoverFromDying
ConfirmUnitDeath
```

Normal death does not globally clear StatHandler or CombatModifiers.

Each source system removes only its own handles.

Death and respawn call handlers in fixed stable order using `ClearForDeath` and `ClearForRespawn`.

## D-010 — UnitDeath / UnitKill reaction requests

**Status:** Frozen

UnitDeath and UnitKill callbacks execute immediately in Tick `T`.

New ordinary Shield, Damage, and Heal requests created by those callbacks are stored as deferred Combat requests for Tick `T + 1`.

Legal deferred sequence gaps are allowed and never renumbered.

## D-011 — Combat snapshot

**Status:** Frozen

Combat Tick-end snapshot stores only:

```text
DamageContributionTrackerSnapshot[]
DeferredCombatRequestSnapshot[]
```

The exact schema and capture assertions are owned by Combat v13.2 and Snapshot Appendix v7.2.

## D-012 — Projectile snapshot and sequence ownership

**Status:** Frozen

Projectile Tick-end snapshot stores:

```text
PendingSpawnRecordSnapshot[]
ProjectileSnapshot[]
```

ProjectileWorld owns its per-Tick spawn sequence reset. FrameSync does not require an external ProjectileWorld BeginTick call.

## D-013 — Match statistics

**Status:** Frozen

`MatchStatisticsRuntime` consumes formal death results on every simulation endpoint, not only Dedicated Server.

## D-014 — Presentation identity

**Status:** Frozen

`PresentationEventId` remains:

```text
SourceLogicTick
SourceKind
SourceRuntimeUid
EventSequence
EventKey
```

Current sources are Unit and Projectile.

Deterministic Attack or Ability code never directly calls `AudioSource.Play()`.

## D-015 — Player input and UI

**Status:** Frozen

UI uses Unity Input System UI integration directly.

The player Gameplay input module handles Move, Attack, and Q/W/E/R.

InputAction callbacks only enqueue local events. They do not modify deterministic Gameplay.

Rollback never rereads device input.

## D-016 — Player ability input profile

**Status:** Frozen

The physical player input mode is derived offline from `CastModelDef`.

Input configuration does not duplicate Gameplay timing, range, damage, cooldown, stage duration, or charge curves.

Current baked modes:

```text
PressCommit
LocalAimPrimaryCommit
PressFocusReleaseOrPrimaryCommit
```

## D-017 — Hold-release input

**Status:** Frozen

For an activated hold-release ability:

```text
Key press -> Focus
Key release -> Commit
Primary click -> same Commit
First successful Commit request suppresses duplicate Commit input
Right click does not Cancel
Right click may still create Move or Attack
```

Focus and Commit may execute in the same TargetTick if CommandSeq preserves Focus before Commit.

Ability timing uses deterministic execution Ticks.

## D-018 — AI ability usage

**Status:** Frozen

AI does not simulate physical input and does not generate player network Commands.

AI reads existing Ability definitions/runtime and produces existing `AbilityAction` / `AbilitySignal` semantics directly.

No generic AI input-control layer is introduced.

## D-019 — Prefab kinds

**Status:** Frozen

`PrefabKind` is code-defined:

```text
Unit
Projectile
ParticleVfx
AudioEmitter
Misc
```

Editor tooling can manage ID ranges and entries, but cannot invent runtime PrefabKind enum values.

## D-020 — Framework implementation versus production content

**Status:** Frozen

The current implementation phase builds reusable deterministic systems and authoring pipelines.

Named heroes, abilities, Buffs, equipment effects and other production-content examples in design documents are acceptance scenarios, not implementation backlog items.

For example, “Varus Q support” requires the generic framework to support:

```text
Focus
Deterministic hold duration
Commit from key release or primary click
Direction Aim
Projectile production
Session completion
Cooldown transition
```

It does not require a production Varus hero or Varus-specific runtime code.

Core systems must not contain champion-specific branches.

Specific production content is implemented only when an explicit task requests that content.

## D-021 — Design files and naming corrections

**Status:** Frozen

The 16 files listed as Current in `Docs/Architecture/DESIGN_INDEX.md` under `Docs/Design/` are the implementation authority.

When an older index entry disagrees with the actual selected design file's title or version, the selected file under `Docs/Design/` wins and the index must be corrected to match it.

Pure path, directory-name and filename-reference mistakes may be corrected directly after checking all repository references. A Unity asset rename must still use Unity-aware tooling so its GUID and serialized references remain intact.

## D-022 — Authoring float and authoritative fixed point

**Status:** Frozen

The authoritative Gameplay numeric type is `Unity.Mathematics.FixedPoint.fp` from:

```text
com.danielmansson.mathematics.fixedpoint
```

Inspector-facing authored values may use `float` for display and editing. They must be validated and converted once at the Bake or deterministic runtime-initialization boundary.

After conversion:

```text
Authoritative Gameplay calculations use fp.
Runtime deterministic configuration stores fp.
Snapshot and checksum inputs use deterministic fp state.
Per-Tick Gameplay does not convert back to float for authority.
Presentation may derive float values from read-only Gameplay output.
```

Do not introduce a second project fixed-point number type. Canonical byte layout and conversion rounding must follow the package representation and the owning serialization design when implemented.

## D-023 — Core runtime with proportional feature tests

**Status:** Active

The next implementation emphasis is the smallest production-quality generic core Gameplay vertical slice whose logic compiles and runs end to end.

A standalone test-harness-first slice is not the current priority. Every implemented feature must nevertheless add the smallest focused automated test that proves that feature's required behavior.

Prefer pure C# or EditMode tests for deterministic logic. Use PlayMode only when the feature depends on scenes, GameObjects, Unity lifecycle, Input System callbacks, presentation or UI. Snapshot/rollback features require their corresponding focused equivalence or round-trip test when those features are implemented.

Tests should remain proportional to the slice rather than becoming an unrelated comprehensive framework. Every slice also requires Unity compilation, Console inspection and the smallest relevant runtime smoke validation. Missing or failing tests must be reported honestly.

The final implementation objective is to build the generic production systems specified by the 16 Current design files, not merely to document or prototype them.

## D-024 — Accepted clean implementation baseline

**Status:** Frozen

The repository owner confirms that all 616 tracked deletions observed on 2026-07-19 are intentional.

The current working tree is the new implementation baseline. Deleted legacy Gameplay, RVO2, hero-specific and related resource files must not be restored or treated as current implementation evidence.

Future implementation starts from the files currently present and follows the Current design files. Historical deleted files may be inspected read-only only when explicitly useful; they do not own current contracts or implementation direction.
