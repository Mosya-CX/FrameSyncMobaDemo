# FrameSyncMobaDemo — Frozen Architecture Decision Log

> This log records decisions that override older examples or superseded design text.
> Entries are normative unless their status changes.

## D-001 — Tick semantics


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

## D-025 - Buff cap and priority dispel

**Status:** Frozen

The repository owner confirmed `BuffHandler.MaxBuffs` with priority-based
dispel as a core gameplay rule on 2026-08-02.

```text
MaxBuffs
    byte cap on simultaneous BuffRuntime per unit (default 255 = no practical limit).

Priority
    0 = highest, 255 = lowest; only used for dispel arbitration.

Displacement
    checked only on first Apply of a new BuffConfigId;
    permanent buffs are never displaced;
    the candidate is the lowest-priority non-permanent buff
    (stable ConfigId order breaks ties, last wins);
    displacement happens only when the incoming Priority <= candidate Priority;
    otherwise the new buff is added beyond the cap (soft cap);
    the displaced buff follows the standard removal flow with
    RemovalReason.ManualRemove.
```

Formalized as section 13A of `BuffSystem_Design_v14_2_PermanentBuffRespawnPatch.md`.

## D-026 — Scene-split application flow (Bootstrap -> Lobby -> GameScene)

**Status:** Frozen (implementation complete 2026-08-04)

The repository owner confirmed on 2026-08-04 that the client and Dedicated
Server processes must not cram the whole flow into one bootstrap scene. The
scene responsibilities are:

```text
Client:
  ClientBootstrap  - startup and initialization (test account + UOS session)
  Lobby            - main menu, matchmaking and hero selection
  GameScene        - real game content (deterministic runtime, payload, HUD)

GameServer:
  ServerBootstrap  - UOS allocation, NGO server start, UOS Ready
  Lobby            - waiting for clients, hero selection barrier
  GameScene        - game load, authority ticks and frame sync
```

The flow remains a logical state machine (v10.2 sections 2-4); Unity scenes
are an implementation detail of that state machine, not a separate loop.
Scenes are not cycled repeatedly: the process moves Bootstrap -> Lobby ->
GameScene once per match and returns to Lobby after the result screen.

Implementation consequences:

- `GameSessionContext` is the cross-scene hand-off (role, flow mode, version
  handshake, pending start config, received payload, registered GameBootstrap
  and persistent bridge). It never enters Gameplay snapshots or checksums.
- The NGO root (NetworkManager + `FrameSyncNetworkBridge` +
  `LobbyNetworkBridge` + `ClientUiActionRouter`) is marked DontDestroyOnLoad by
  the bootstrap scene and survives into Lobby/GameScene.
- `LocalNgoEndpointDriver` moved to the Lobby scene and defers endpoint binding
  to the first Update so the persistent NGO root is stable after the scene
  transition; the network must start before the bridge registers NGO handlers
  because `CustomMessagingManager` does not exist before StartServer/StartClient.
- `LobbyNetworkBridge` no longer depends on a pre-loaded GameBootstrap: the
  Lobby schedules the start (stored in `GameSessionContext.PendingServerStart`),
  GameScene's GameBootstrap builds/applies/broadcasts the authoritative payload
  on the server and applies the received payload on the client.
- The shared `GameScene` supports both roles at runtime through
  `GameSessionContext.IsDedicatedServer`; a server process ignores the
  serialized PlayerInputController reference instead of failing.
- UOS login (`UosClientSession.InitializeAsync`) was first verified in the
  editor; the 2026-08-10 provider run subsequently verified allocation, Ready,
  matchmaking, two public NGO client connections, Lobby barriers and Gameplay.
- Connection-lifecycle ownership is exclusive per flow mode. In
  `FrameFlowMode.UosOnline`, `LobbyFlowController` owns matchmaking, client
  transport connection and identity. In `FrameFlowMode.LocalDirect`,
  `LocalNgoEndpointDriver` owns the client notification/wait path. This rule was
  made explicit after the first live clients exposed both owners running at
  once and `LobbyNetworkBridge` validation remains strict. Implementation note:
  the 2026-08-10 attempted fix only gates the driver's `Update()` polling;
  `OnClientConnected()` still calls `NotifyClientConnectedOnce()` in UOS mode.
  Conformance to this decision is therefore still open until that callback path
  and a callback-level behavior test are corrected.

## D-027 — Single source for UOS application configuration

**Status:** Frozen (implementation complete 2026-08-04)

The Matchmaking config ID and region ID are no longer duplicated on
`ClientBootstrap`/`GameBootstrap` scene components. The runtime reads one
source: the UOS Launcher environment settings exposed through
`Unity.UOS.Common.Settings.MatchmakingConfigID` (backed by
`Assets/Resources/UOSSettings.asset`, which is included in player builds).
Per-launch command-line arguments are explicit overrides only
(`-matchmakingConfigId=<id>`, `-uosRegionId=<id>`), and the online/local flow
mode is overridable with `-onlineFlow` / `-localFlow` without editing scenes.
Scene-serialized `uosMatchmakingConfigId`/`uosRegionId` fields were removed.

Implementation consequences:

- `UosApplicationConfig` resolves flow mode, config ID and region ID in one
  place with injectable test hooks; EditMode tests cover argument forms,
  precedence and reset behavior.
- `LobbyNetworkBridge.UnregisterHandlers` now tolerates a null
  `CustomMessagingManager` during scene teardown after the network has shut
  down (same guard that `FrameSyncNetworkBridge` already had); the repeated
  teardown NRE no longer pollutes the Console during PlayMode exits.
- Filling the config ID in the UOS Launcher environment configuration is now
  sufficient for the packaged client and server; no scene edit is required.
- The external dashboard/configuration gate was satisfied for the 2026-08-10
  live run. `UOSSettings.asset` and `UOSEnvironments.asset` now resolve the real
  Matchmaking config ID `f01c4e66-0023-43f6-af57-dcd8b73e7b90`. The Multiverse
  startup/profile ID `0fc730a2-ce02-4768-8a75-713ddb36c3b0` is a different
  provider contract and must not be placed in `MatchmakingConfigID`.
- These identifiers are configuration, not secrets. UOS application/server
  secrets and allocation-injected secret values must never be copied into this
  repository, documentation or shared logs.

## D-028 — Catalog-driven hero select and unrestricted hero choice

**Status:** Frozen (implementation complete 2026-08-04)

The hero select list is now driven by a dedicated `HeroDisplayTable`
(avatar/name mapping) instead of generated placeholder rows. The prefab table
keeps prefabs only; after a hero prefab is referenced by a `UnitKind.Hero`
prototype, the display table automatically gains a mapping row
(`UnitPrototypeId` + `HeroPrefabId` + display name), and content authors only
fill the avatar. `GlobalPrefabTable` also gains per-kind ID ranges
(design v10.2 17.5) with validation.

Implementation consequences:

- `HeroDisplayTable` is a `RuntimeConfig` ScriptableObject; the editor sync
  lives in the Unit assembly (`HeroDisplayTableSync`) and runs automatically
  from `UnitRuntimeCatalogAsset.OnValidate` plus a manual menu invocation.
- `GameFlowLuaBridge.BindHeroSelect` exposes the 1-based rows to Lua;
  `Select.lua` renders real heroes and `HeroCell.lua` sets the avatar image.
  The Select list is preloaded when the Lobby loads so cells exist before
  matchmaking opens the page.
- Hero choice is intentionally unrestricted (2026-08-04): any positive
  `HeroConfigId` is accepted from clients, duplicates across slots are
  allowed, and the locked value flows into `GameStartConfig` per slot. The
  previous "must equal the frozen fixture HeroConfigId" network checks were
  removed.
- Player Settings default to 1600x900 windowed with a resizable window so the
  packaged client is usable for acceptance testing.

## D-029 — Varus test-kit cast models: point/direction aim, hold-release indicator, W toggle

**Status:** Frozen (implementation complete 2026-08-05)

The Varus test-kit ability assets follow Ability v15.2 section 7.1/7.2 for
aim-carrying Commit and hold-release skills, and the test hero's W is a pure
toggle:

- VarusE: `CommitCastModelDef` + `AreaDamageStageDef`, `AimKind.Point`.
  The stage resolves its center from `AbilitySession.Aim.TargetPoint`.
- VarusR: `CommitCastModelDef` + `SpawnProjectileStageDef`,
  `AimKind.Direction`. The stage spawns the projectile toward the normalized
  `AbilitySession.Aim.Direction`.
- VarusQ: `HoldReleaseCastModelDef` (Hold -> Release), `AimKind.Direction`.
  The local indicator resolves to the Release stage and follows
  caster-position -> cursor direction every frame while the Focus session is
  active (`GameplayFocusing`).
- VarusW: `ToggleCastModelDef`, `AimKind.None`, no cooldown, no per-Tick
  resource drain, and `NotifyAbilityCastOnEnter = false` (design v15.2 7.8:
  W is not an active cast). A second Commit ends the active session without
  starting cooldown.

Implementation consequences:

- `AbilityHandler` now resolves stages for `ToggleCastModelDef`,
  `GroundTargetCastModelDef` and `VectorTargetCastModelDef` in addition to the
  original four models, and a second Commit on an active Toggle turns it off
  (session ends, cooldown not started).
- `PlayerInputController.UpdateIndicator` shows and follows the indicator for
  both `LocalAiming` (E/R aiming) and `GameplayFocusing` (Q hold) states; the
  previous code only handled `LocalAiming`, so hold-release indicators never
  rendered.
- The local indicator is now composed in `GameScene`: `SkillIndicatorDriver`
  is attached to the GameBootstrap object and wired to
  `GameBootstrap.indicatorDriver`, with placeholder
  `DirectionIndicator`/`RangeCircleIndicator`/`GroundTargetIndicator` prefabs
  under `Assets/Resources/Prefab/Indicators/` for acceptance testing. These
  are presentation-only placeholders; final art is out of scope.
- `AbilityAssetBakeValidator` gained explicit validation branches for
  Toggle/GroundTarget/VectorTarget authoring.

## D-030 — Varus Q charge mechanics and W blight toggle (2026-08-05)

**Status:** Frozen (implementation complete 2026-08-05)

The Varus test kit's Q and W are implemented as generic, data-driven
framework slices:

- **Q — Piercing Arrow**: `ChargeStageDef` (Hold) computes `ChargeRatio`
  into the session Blackboard (full charge at 1.5s/45 ticks, max hold 4s);
  `ChargeProjectileStageDef` (Release) linearly interpolates base damage,
  extra-AD ratio (80% -> 120%), range (925 -> 1625) and the W-empowered
  missing-health ratio, then spawns the projectile with a per-instance
  on-hit damage override and a cast-range lifetime. Piercing falloff is
  -15% per extra hit with a 33% floor, resolved per hit by
  `ProjectileEffectDispatcher`.
- **W — Blight Quiver**: stays a pure Toggle (D-029). Its active-ability
  passive (`OnHitBonusDamagePassiveEffectDef`) adds on-hit magic damage and
  applies one Blight stack; `AbilityHitStackDetonationBuffEffect` detonates
  all stacks when the caster lands Ability damage, deals
  MaxHealth-percent magic damage (per-stack, +AP ratio), caps each stack
  at 120 vs non-heroes, refunds 13% basic-ability cooldown per stack on
  hero targets, and never re-triggers from its own Buff-sourced damage.
  When W is toggled on, charging Q consumes the toggle and starts W's 40s
  cooldown (`ChargeStageDef.ConsumeToggleSlot`).

Implementation consequences:

- `ProjectileSpawnRequest`/`ProjectileRuntime`/snapshots now carry a
  per-instance `ProjectileOnHitDamage` override and a max-lifetime override
  (both deterministic snapshot members); `ProjectileOnHitDamage` gained
  `MissingHpRatio`, `FalloffPerHitPercent` and `MinDamageRatio`.
- `DamageEventData` now carries the source `SourceDescriptor` so effects can
  distinguish Ability damage (detonates Blight) from Attack/AttackEffect
  damage (does not), without guessing.
- `AbilityPassiveListenerMask` gained `OnHitDealt`; `AbilityHandler` and
  `UnitEventBus` forward on-hit events to ability passives, and
  `AbilityPassiveRuntimeState.AbilityLevel` tracks the owning ability level.
- Buff definitions are registered through a new `BuffCatalogAsset` wired
  into `GameBootstrap` (the runtime registry was previously always empty);
  `BuffEffectConfig.Effect` is now `[SerializeReference]` and `BuffConfigId`
  is Unity-serializable so SO assets can configure buff effects.
- `ProjectileWorld`'s ObjectPool destroys pooled entities with
  `DestroyImmediate` outside play mode, fixing EditMode test teardown for
  all projectile tests.

## D-031 — Varus passive P, charge slow/refund, per-level cooldown (2026-08-06)

**Status:** Partially superseded by D-044 and D-045. The synchronized network
time launch barrier and stat
Dirty finalization remain frozen; carrying launch authorization inside
`GameBootstrapPayload` no longer applies.

Following the hero design document and Ability v15.2 review:

- **Cooldown is per ability level** (`AbilityDef.CooldownByLevel`,
  design v15.2 5.5) instead of a single int. Q/E/R are authored with
  level-scaled cooldowns (Q 16..12s, E 18..10s, R 100/80/60s). Special
  cooldowns stay out of `AbilityDef` (W's 40s post-Q cooldown is driven by
  `ChargeStageDef.ConsumeToggleCooldownTicks`).
- **Q charge self-slow**: `ChargeStageDef` now stores a
  `StatModifierHandle` in the ability Blackboard (new
  `AbilityBlackboardValueKind.StatModifierHandle`) and applies -20%
  MoveSpeed (FinalRatioAdd) for the hold duration, removed on exit.
- **Q timeout refund**: `HoldReleaseCastModelDef` gained
  `HoldTimeoutPolicy` (AutoRelease/Cancel) and
  `RefundCostPercentOnTimeout`; the handler cancels the hold on timeout and
  refunds half the already-paid cost (Varus Q = 50%).
- **Passive P (复仇之欲)**: fixed passive registration is now wired
  (`FixedPassiveDefinitionAsset` -> `AbilityRuntimeCatalogAsset` ->
  `AbilityLoadoutAsset.fixedPassiveAbilityId` -> `SetFixedPassive`).
  `ApplyBuffPassiveEffectDef` keeps the Revenge Buff applied (activate /
  respawn / kill); `KillStatGrowthBuffEffect` grants attack speed
  (10/15/20% at hero levels 1/7/13, 3x on hero kills) plus attack damage
  and ability power equal to 1100% (3300% hero) of the attack-speed bonus,
  refreshing 5/7/9/11s durations (levels 1/6/11/16), max one stack.
- Blight's per-stack damage cap applies to monsters only (design: 野怪),
  not to all non-heroes.

Checksum coverage: `AbilityPassiveRuntimeState.AbilityLevel`, projectile
`OnHitDamageOverride`, pending `MaxLifetimeTicksOverride`, and the new
Blackboard StatModifierHandle entries all participate in
`SharedGameplayChecksum`; `GameplaySnapshot.CurrentSchemaVersion` bumped to
16 and the snapshot appendix updated.

## D-032 — Revenge queue rule, buff icon, checksum diagnostics (2026-08-06)

**Status:** Frozen (implementation complete 2026-08-06)

- **Revenge normal-after-empowered rule**: when a non-hero is killed while
  the empowered Revenge Buff is active, the empowered Buff keeps its values
  and records a pending-normal flag; when the empowered Buff expires,
  exactly one normal Buff is re-applied (1x values, current-level duration).
  Implemented via `BuffEffect.OnRemovedComplete` (called after the runtime is
  removed from the store) and `BuffHandler.TryGetRuntime` for the successor
  lookup; the effect stores `IsEmpowered`/`PendingNormalAfterEmpowered`
  Blackboard bool slots.
- **Passive P buff icon**: the Revenge Buff and the fixed-passive skill use
  `Assets/Art/Icon/Ability/Varus/韦鲁斯被动.png` (presentation only).
- **Checksum divergence diagnostics**: `SharedGameplayChecksum` now exposes
  per-segment (Schema/Random/MatchRule, UnitWorld, Combat, Projectiles,
  EquipmentShop, Physics, GoldDigest) and per-unit per-handler hash segments.
  With `-checksumDetail` on the command line, the server logs its segments
  each tick and a client logs its predicted segments plus per-unit handler
  hashes when a replay mismatch occurs, so the diverging subsystem can be
  identified directly from the logs.
- **Order hardening**: `StatHandler.Capture` now sorts each entry's modifiers
  by `StatSeq` before checksum hashing (BuffStore and AbilityBook were
  already order-stable). This removes the only remaining insertion-order
  dependency in the checksum calculation.

## D-033 — Wall-clock launch barrier and stat Dirty finalization (2026-08-06)

**Status:** Frozen (implementation complete 2026-08-06)

- **Wall-clock launch barrier**: the match no longer starts the moment a
  client applies the bootstrap payload. The server computes an absolute
  `LaunchUtcTicks` (= UtcNow + `GameModeConfig.LaunchDelaySeconds`, default
  5s) and broadcasts it in `GameBootstrapPayload` (wire v2). Both server and
  clients hold the first simulation tick until the wall clock reaches that
  instant; clients keep the Loading page until then and only then open the
  battle HUD. This keeps endpoints' real-time offset as small as possible
  instead of aligning on a logic tick.
- **Stat Dirty finalization**: the packaged-client Tick 3 checksum divergence
  was reproduced in-editor (`BootstrapDeterminismProbeTests`) and localized
  to `StatHandler` `Dirty` flags: the client restore path marks all entries
  Dirty while the server spawn path does not, and nothing recomputed them at
  tick end. `SimulationTickPipeline.ExecuteTick` now calls
  `StatHandler.FinalizeTick()` on every unit at tick end (Unit v27.3 5.5.1),
  so server-authority and client-prediction first ticks produce identical
  checksums. The probe test passes with the fix.

Implementation consequences:

- `FrameSyncGameRuntime` exposes `LaunchUtcTicks` /
  `IsLaunchTimeReached` / `SetLaunchUtcTicks`; `GameBootstrap` gates
  `AdvanceSimulationByElapsedSeconds` on it and defers HUD opening until the
  barrier is reached.
- `GameBootstrapPayload` gains `LaunchUtcTicks`; `BootstrapPayloadWireCodec`
  wire version bumped to 2.
- `GlobalGameplayData` gains `GameModeConfig.LaunchDelaySeconds`
  (baked into `BakedGlobalGameplayData.LaunchDelaySeconds`).

2026-08-10 live validation note (does not revise this frozen decision):

- The server applied the bootstrap at 17:35:05.623 and later emitted a Tick
  1625 combat event at 17:36:04.626. At 30 Tick/s, subtracting the configured
  five-second launch delay from that wall-clock interval predicts about 1620
  executed Ticks. This is strong evidence that the server honored the barrier
  and did not simulate during its five-second wait.
- The operator saw a client leave Loading with displayed match time near 30
  seconds. Existing packaged client logs do not timestamp payload receive,
  barrier reach, HUD open or first accepted AuthorityFrame, so message delay,
  endpoint clock offset, main-thread delay and a scheduling defect remain
  indistinguishable.
- Do not replace the absolute UTC contract from that observation alone. The
  next live package must add narrow UTC + monotonic-time + current-Tick markers
  at server broadcast, client payload receive/apply, launch-barrier reach, HUD
  open and first accepted authority. Revise D-033 only if that evidence proves
  the contract itself is defective.

The 2026-08-14 timestamped UOS logs resolved that observation: client Tick 3
and server Tick 3 began at essentially the same wall-clock instant, but the
client then executed through roughly Tick 1080 in about 2.6 seconds before
returning to 30 Hz. This was a prediction-start runaway, not 30 seconds of
network RTT or a server-side delay.

## D-034 — Assist event chain and assist-driven Revenge buff (2026-08-06)

**Status:** Frozen (implementation complete 2026-08-06)

Combat v13.2 7.14/14.6 assist support now reaches gameplay effects:

- `CombatEvents.RaiseUnitAssist` forwards to the assistant unit's
  `UnitEventBus.PublishUnitAssist`, which dispatches to
  `AbilityHandler.OnUnitAssist` and `BuffHandler.OnUnitAssist`.
- `AbilityPassiveListenerMask.UnitAssist` +
  `AbilityPassiveEffectDefBase.OnUnitAssist` and
  `BuffEffect.OnUnitAssist` were added, with handler dispatch.
- `KillStatGrowthBuffEffect.OnUnitAssist` treats a hero assist the same as a
  hero kill: it applies the empowered Revenge buff (3x values, refreshed
  duration). The hero-kill/assist branch is shared via
  `ApplyHeroEmpowered`.
- The assist event was raised twice per assistant (CombatSystem settlement
  and DeathEffectDispatcher.FireOnKillEvents); the duplicate in
  `DeathEffectDispatcher` was removed.

Covered by `AssistEventIntegrationTests` (damage contributions -> stable
AssistantHeroUids -> single assist event -> empowered Revenge buff) and
`PassivePAbilityTests.AssistHero_AppliesEmpoweredBonusToAssistant`.

## D-035 — Combat contribution event log and last-hit killer (2026-08-06)

**Status:** Frozen (implementation complete 2026-08-06)

The aggregated `DamageContributionTracker` is replaced by a per-victim
`CombatContributionEventLog` (Combat v13.2 搂7.14, snapshot appendix v7.2):

- Every effective Damage / Shield / Heal interaction is stored as one event
  ordered by (LogicTick, SequenceInTick); capacity 256 per victim and an
  expiry window of 150 ticks (~5 s at 30) bound the log.
- The killer is the contributor of the last Damage event
  (`LastHitContributorUid`), not the highest accumulated contribution.
- Assistants are the distinct Damage contributors inside the window,
  excluding the killer, filtered to valid enemy heroes and sorted by
  UnitUid ascending.
- `CombatSnapshot` carries `ContributionEventLogs`;
  `SharedGameplayChecksum` hashes LastHit, Kind, Amount, LogicTick and
  SequenceInTick for every event. GameplaySnapshot schema is bumped to 17.

Implementation consequences:

- `DamageContributionTracker` and its snapshot type are deleted; no
  reference remains in Combat, checksum or test code.
- `CombatSystem.RecordEvent` is the single write point for Damage / Shield /
  Heal events, and `ResolveDying` uses `log.ResolveKiller` /
  `log.ResolveAssistants`.
- Death settlement no longer needs a frozen killer map: the killer and
  assistants are resolved from the snapshot member at settlement time.

Covered by `CombatContributionEventLogTests` (5) and
`AssistEventIntegrationTests.Killer_IsLastDamageContributor_NotHighestTotal`;
FrameSync checksum/rollback regression and the combat-focused suites pass.

## D-036 — Crowd Control v6.2 module-architecture conformance (2026-08-06)

**Status:** Frozen (runtime + integration implemented 2026-08-06)

The legacy Kind-branch crowd-control handler is replaced by the
`moba_crowd_control_system_design_v6_2.md` architecture:

- `CrowdControlDefinition` (SO) with authoring fields plus baked hidden
  runtime fields (ParamLayout, OnAdd/Collect/Signal/OnRemove ops,
  SignalMask); `CrowdControlCatalogAsset` + `CrowdControlDefinitionRegistry`
  registered per world at bootstrap (mapped from the design's GameplayConfig
  singleton to the project's catalog pattern).
- Handler creates independent `CrowdControlInstance`s (no merge/source
  metadata), resolves definitions per call, arbitrates the unique
  ForcedMove, and implements immunity (tag query + BlockCount + Priority),
  unstoppable, cleanse, lightweight signals (2-tick retention) and Tenacity.
- Module executor table with BlockActions / MaxMoveSlow /
  MaxAttackSpeedSlow / MinVisionScale / BasicAttackMiss / ForcedBehavior /
  ForcedMoveOnAdd / RemoveOnSignal / AddControlOnNaturalExpire; no per-Kind
  branches anywhere.
- Key params: explicit `ControlParamKeys` constants (project has no
  StableStringId32) + a project-owned `FixedBytes64` param block (no
  Unity.Collections dependency).
- Unit-framework integration follows `unit_behavior_framework_design_v27_3`:
  `Unit.RefreshCapabilityState()` folds `CrowdControlStateView.BlockedActions`
  into coarse capability; `BehaviorPlanner` reads
  `TryGetBehaviorOverride` before intent and maps it to existing Move/Attack
  requests; `ActionArbiter` reads `State` in Submit and runs
  `EvaluateCurrentRuntimes` to interrupt no-longer-allowed runtimes;
  forced move bypasses Planner/Arbiter via MovementHandler.
- Snapshot/checksum carry instances (ControlId/StartTick/ExpireTick/params),
  immunity, unstoppable, ids, active forced-move handle and signals;
  GameplaySnapshot schema bumped to 18.

Recorded mappings: `Suppression` Intensity is High (not cleansable/immunable,
matching the pre-refactor semantics); `AbilityBlackboard` gained a
`CrowdControlHandle` value kind so stages like Pull keep their handle across
ticks.

Covered by `CrowdControlHandlerTests` (10), migrated `MovementConformanceTests`
and FrameSync/Bootstrap regression.

## D-037 — Minion initial buffs, tower mechanics and two-config split (2026-08-07)

**Status:** Frozen (implemented and regression-tested 2026-08-07)

Non-hero content decisions for towers and lane minions:

- **Built-in buffs are data-driven, not hard-coded.** `UnitPrototype` gains
  `InitialBuffConfigIds`; `BuffHandler` exposes
  `SetInitialBuffConfigs`/`ApplyInitialBuffs` and `UnitWorld` applies them at
  spawn. Infinite buffs survive death via the permanent-buff respawn
  lifecycle, so any unit can carry built-in buffs by configuration alone.
- **Minion special mechanics ride CombatModifier Buffs.** `CombatModifierMatch`
  gains target-`UnitKind` filtering and `CombatValueRefKind.TargetCurrentHealth`
  is added, so buffs express "extra damage vs minions" and "reduced damage vs
  towers" through the existing formula pipeline (no per-minion
  EquipmentHandler). Test config: `Buff_MinionMuncher` (melee +2% target
  current health vs minions), `Buff_MinionPincushion` (ranged +3.5%),
  `Buff_TowerPillow` (towers take x0.6). Minion level growth is intentionally
  dropped (user decision).
- **Tower mechanics** (`TowerAttackHandler : AttackHandler`, NonHero v5 搂9):
  hero damage ramps 180 -> x1.5 per hit on the same hero, capped at 600
  (injected through `ProjectileSpawnRequest.OnHitDamageOverride`); minion hits
  stay flat base damage. In-flight projectile locking:
  `HasUnresolvedProjectile` keeps `TowerAIController` from re-targeting
  (v5 8.5). Ramp/lock state lives in `AttackSnapshot` (rollback-safe) and is
  checksummed. `TowerTargetLinePresenter` draws the red line presentation-only.
  Per the 2026-08-11 user clarification, the line follows the tower's current
  `AttackTarget` intent rather than the last projectile lock: intent replacement
  switches the endpoint immediately, while a dead/invalid target with no
  successor disables rendering. The presenter never writes Gameplay state and
  does not enter snapshots or checksums.
  Both test and formal tower prefabs were migrated from AttackHandler to
  TowerAttackHandler.
- **Single runtime chain (superseded by D-038).** Resource layout was
  consolidated on 2026-08-10 into `Assets/Config/Formal/` as the only formal
  chain; see D-038.

Covered by `MinionInitialBuffTests` (4), `TowerAttackHandlerTests` (3) plus
FrameSync (71) and Bootstrap EditMode (58) regression.

## D-038 — Single-source formal resource layout (2026-08-10)

The packaged C/S build is the single source of truth for runtime resources;
test scenes and tests reference the same chain.

```text
Assets/Config/Formal/   the one formal chain (packaged)
    GlobalGameplayData.asset -> GlobalPrefabTable.asset
    HeroDisplayTable / AudioLibrary / UnitOutlineRim
    CrowdControl/ (12 CC definitions + catalog)
    Abilities/ (Varus Q/W/E/R + loadout + catalog)
    Buffs/ (blight, corruption vines, minion built-ins, revenge, ...)
    FlowFields/ (Team 1/2 x Small/Medium/Large)
    Animation/ (TestHero.controller, minion controllers, profile)
    Prefabs/ (TestHeroRuntime = Varus, melee/caster minions, towers)
    FullMatch* catalogs (units, projectiles, VFX, map, minion wave, dispose)
Assets/Config/Tests/    test-only configs (HeroTestMapConfig)
Assets/Resources/       C/S-used UI / missiles / indicators / VFX / materials
```

- Removed: `Assets/Fixtures/` (at the time this removed the only equipment
  catalog and left `equipmentCatalog` null; D-039 later introduced the formal
  packaged catalog),
  the dead `Config/Formal/` pair (`FormalUnitRuntimeCatalog` /
  `FormalGlobalPrefabTable`), `Config/Runtime/` + `Config/FullMatchTest/`
  (merged into `Config/Formal/`), and `Assets/Resources/Prefab/Unit/`
  (formal unit prefabs that only the dead pair referenced).
- `GlobalPrefabTable` dropped the placeholder 1001/2001 entries that pointed
  at `Fixtures/Framework/Prefabs` ellipsoid/sphere prefabs.
- All test scenes (HeroTestScene, MinionTowerLongRunTest, FrameworkSmoke,
  ClientFrameworkSmoke) and their drivers/tests reference the Formal chain.
- Hero attack animation restored: `TestHero.controller` gained Attack1 /
  Attack2 states (AnyState entry on `AttackStart`, alternating by
  `AttackSequenceIndex`, exit on `IsAttacking == false`). Note Unity
  2022.3.62 `AnimatorConditionMode`: Equals=6, NotEqual=7.

## D-039 — First formal equipment catalog and repeat-safe On-Hit (2026-08-10)

**Status:** Implemented and focused-tested by ExecPlan 0132.

- The first packaged equipment catalog lives under
  `Assets/Config/Formal/Equipment/` and contains Dagger (31001), Amplifying
  Tome (31002), Pickaxe (31003), Recurve Bow (31004) and Guinsoo's Rageblade
  (31005). GameScene references this catalog; Seething Strike uses the formal
  Buff catalog with BuffConfigId 31901.
- HeroTestScene is driven by `HeroTestDriver`, not GameBootstrap. Its
  local-Tick shop must bake the same formal equipment catalog, initialize the
  formal `GoldIncomeRuntime` with a 10000-gold baseline, and submit the same
  canonical shop Commands into its local `SimulationTickPipeline` without a
  network transport. Constructing an empty `EquipmentDatabase` or mutating the
  `EquipmentHandler` directly from UI is a stale pre-D-039 fallback and is
  covered by dedicated PlayMode regressions.
- Equipment modules receive a tick-local execution context and their exact
  `EquipmentEffectModuleRuntimeState` by `ref`. Persistent counters and
  internal cooldowns therefore cannot be lost through struct copies.
- A repeated On-Hit is the existing On-Hit event with `IsRepeated = true`, not
  a second Attack or a second Command. It routes through existing Ability,
  Buff and Equipment handlers; the stack grant and repeat generator ignore
  repeats, preventing recursion. The attack that reaches four stacks counts
  as the first full-stack hit.
- The runtime module member formerly named `TimerTicks` is the formal
  `TriggerCount`; it is snapshotted and checksummed. GameplaySnapshot schema
  advances from 21 to 22, so client and server packages must be rebuilt as a
  matching pair.

## D-040 — Lazy Shop Trader bootstrap and minion reward distance conversion (2026-08-11)

**Status:** Frozen clarification of Equipment/Gold v12 and Combat v13.2.

- `ShopTraderRuntime` remains absent until the first successful Purchase or
  Sell transaction. UI price queries and local RequestCheck are read-only and
  must not create Trader state.
- Before Trader creation, `EquipmentShopRuntime` resolves the controlled hero
  from the existing stable `Unit.ControlledByPlayerSlot` mapping. Once Trader
  exists, its snapshotted `ControlledUnitUid` remains the authoritative shop
  binding. Multiple Units carrying the same PlayerSlot are a deterministic
  configuration error.
- The command pipeline creates Trader state only after Purchase/Sell planning
  succeeds, then applies the transaction and OperationLog record. Undo never
  creates an initial Trader.
- `MatchStatisticsRuntime.MinionRewardShareRadius` is authored in the same
  stat-distance domain as attack ranges. It is converted exactly once through
  `UnitWorld.StatDistanceToLogicDistanceScale` before comparing squared logic
  positions. At the current 0.01 scale, radius 800 is approximately 8 logic
  units.

## D-041 -- Integer kill rewards and formal test gold (2026-08-11)

**Status:** Implemented for the current reward pipeline; one cross-document
producer-contract conflict remains unresolved.

- All gold values and allocations are non-negative integers. In particular,
  `GoldAllocation.GoldAmount` is `int`; kill allocation and
  `GoldIncomeRuntime.RequestGoldIncome` no longer cross an `fp` boundary.
- Formal C/S match initialization starts every player at 1500 earned gold.
  HeroTest remains a separate local-Tick fixture with its explicit 10000-gold
  test baseline.
- Formal base kill values are melee minion 21, ranged minion 14 and hero 300.
  Per the current user rule, minion gold belongs only to the last-hitting hero;
  nearby enemy heroes continue to share minion experience within the converted
  reward radius.
- A hero killer receives `floor(BaseGold * 3 / 5)` and valid assistants split
  the remaining `2 / 5` in stable order. Thus a 300-gold kill with two assists
  is 180/60/60. With no assistants the killer receives all 300.
- Confirmed UnitKill income emits one `[GoldIncomeConfirmed]` diagnostic with
  Tick, PlayerSlot, integer amount and the resulting confirmed total. Natural
  income does not emit this diagnostic.

The current documents disagree on allocation ownership: Equipment/Gold v12
sections 6.5-6.6 place allocation generation in `MatchStatisticsRuntime`, while
Combat v13.2 sections 1.5 and 11.10 require `CombatSystem` to emit
`GoldIncomeAllocation` with PlayerSlot and Reason. The existing implementation
follows v12 and still allocates by ReceiverHeroUid before the pipeline resolves
PlayerSlot. This task does not silently choose a new owner or migrate that
public producer contract; only the shared integer Amount contract and requested
balance behavior are frozen here.

## D-042 -- Sequential-recast UI projection and cast-facing preservation (2026-08-12)

**Status:** Implemented and focused-tested by ExecPlan 0133.

- A sequential-recast model's first and second waiting-window `CastStage`
  receive the following impact stage's authored icon override. The UI therefore
  shows the next legal cast (Q2, then Q3) as soon as the previous impact enters
  its recast window, while continuing to obey Ability v15.2's rule that UI
  reads the current `CastStage.IconOverride`.
- Dash movement may translate a Unit while another active `CastStage` owns
  `LockMovement`, but preserves that locked cast's deterministic facing instead
  of replacing it with the dash direction. No hero-ID branch and no new
  snapshot member are introduced; the active Ability session already owns the
  stage and aim.
- An attached VFX with non-zero `VfxEvent.WorldDirection` follows its host's
  position while retaining that direction in world space. Later host rotation
  must not rotate the effect a second time. This remains presentation-only.
- HeroTest binds the fixed-passive cooldown state to the HUD bridge instead of
  returning constant zero values.

## D-043 -- Compile-selectable bounded asynchronous diagnostics (2026-08-13)

**Status:** Implemented and focused-tested by ExecPlan 0134.

- Diagnostics are presentation/operations output only. They never participate
  in Gameplay state, Commands, snapshots, deterministic ordering or checksums.
- Enabled Player builds define `FRAME_SYNC_MOBA_DIAGNOSTICS`. Calls marked with
  that conditional symbol enqueue bounded work; disabled builds omit the call
  sites and do not create a worker, subscribe to Unity logging or perform IO.
- Producers never wait for file/stdout IO. A below-normal dedicated background
  thread owns batching, formatting, directory creation, file writes, stdout
  mirroring and mismatch-artifact construction.
- Normal and priority queues are both bounded. Saturation drops entries and
  increments a visible counter instead of blocking the simulation thread.
- Client and server Unity logs are mirrored into an owned diagnostic file.
  Explicit FrameSync diagnostics are also mirrored to process stdout so UOS
  Dedicated Server log collection receives them. A packaged client places its
  diagnostic file beside the explicit Unity `-logFile`; otherwise it uses
  `Application.persistentDataPath/FrameSyncDiagnostics`.
- Writer failures are sent to stderr and exposed to the Unity host, which emits
  a visible Unity error. Empty catch-and-ignore is not the failure contract.
- Normal Gameplay shutdown may wait only for the configured bounded exit flush;
  Gameplay execution never waits for the diagnostic worker.

## D-044 -- Two-phase bootstrap acknowledgement and launch commit (2026-08-14)

**Status:** Partially superseded by D-045. The two-phase acknowledgement
barrier remains; its UTC clock domain and wire-v2 compatibility slot do not.

- `GameBootstrapPayload` restores the authoritative initial snapshot and frozen
  player mapping but never authorizes simulation. Its legacy wire-v2
  `LaunchUtcTicks` slot is retained as zero to avoid a second snapshot codec
  migration.
- A client sends `BootstrapAppliedConfirmation(MatchId, StartTick)` only after
  snapshot Restore/Resolve/Rebuild and local controlled-unit binding complete.
  The server tracks the frozen roster in PlayerSlot order; identical duplicate
  confirmations are idempotent and invalid senders or identities fail visibly.
- Only after every assigned client confirms does the server compute
  `MatchLaunchCommit.LaunchUtcTicks = UtcNow + LaunchDelaySeconds` and broadcast
  the commit. The server waits until that absolute instant. A client may begin
  `MaxPredictionLeadTicks - 1` Ticks early, which automatically subtracts both
  real message transit time and the configured prediction lead from its wait.
- Client prediction is independently bounded by an absolute wall-clock Tick
  ceiling advancing at `TickRate`, in addition to the authority-frame
  prediction window. A startup bookkeeping defect can therefore no longer
  execute approximately 30 seconds of Gameplay in a few render seconds.
- External flow mode exclusively selects UOS or LocalDirect; serialized
  LocalDirect defaults cannot leak into UOS. `LoadingGame -> InGame` occurs at
  LaunchCommit, while actual Tick execution still waits for the endpoint's
  launch threshold.
- `GameplayDataVersion` advances from 1 to 2 so mixed old/new client and server
  packages are rejected during the lobby version handshake.

## D-045 -- Monotonic launch scheduling and millisecond authoring (2026-08-20)

**Status:** Implemented in source; 20/30/60 Hz and rebuilt two-client UOS
acceptance pending.

- `GameBootstrapPayload` contains no launch timestamp. Bootstrap wire version
  advances from 2 to 3 and old payloads are rejected rather than retaining a
  zero-valued `LaunchUtcTicks` compatibility slot.
- `MatchLaunchCommit` carries `LaunchServerTimeMilliseconds` in NGO's
  synchronized `NetworkManager.ServerTime` domain. The server and clients use
  that same clock only to cross the launch threshold; local calendar UTC never
  authorizes or paces simulation.
- Once an endpoint crosses its threshold, launch pacing is anchored to a local
  monotonic millisecond clock. A client may execute only the greater of its
  monotonic launch allowance and the real continuously received AuthorityFrame
  backlog; timestamp lateness alone never fabricates a backlog.
- Loading progress, matchmaking presentation, Ping scheduling and render-loop
  simulation accumulation use integer milliseconds. Calendar UTC remains
  diagnostic metadata only (asynchronous log timestamps/file names).
- Offline Inspector content time is integer milliseconds with an explicit
  `Ceil`, `Nearest` or `Floor` Bake policy. At Bake, the selected fixed
  `TickRate` converts milliseconds to runtime Tick state using integer
  arithmetic. Runtime snapshots, Commands, cooldown state and checksums remain
  Tick-based.
- Supported offline rates are 10 through 120 Hz in multiples of 5. Formal
  migration preserves the old 30 Hz content duration by mapping positive
  `legacyTicks` to `floor(legacyTicks * 1000 / 30)` milliseconds. The old values
  and exact 30 Hz durations are retained in
  `Docs/Implementation/LEGACY_30HZ_TIME_AUTHORING_INVENTORY.md`.
- Gameplay protocol data version advances from 2 to 3, and launch/bootstrap
  wire versions advance independently, so mixed packages fail visibly.

## D-046 — Hero spawn slots are bound from lobby selection (original decision 2026-08-12; ID corrected 2026-08-22)

**Status:** Frozen. This decision was originally recorded under a duplicate
`D-039` heading. It was renumbered to `D-046` without changing semantics.
Existing `D-039` references continue to mean the formal equipment/On-Hit
decision.

Player-controlled initial spawns no longer hardcode the hero prototype in the
scene composition (`initialUnitSpawns`). The spawn slot (spawn point + team)
is the deterministic topology; the hero prototype is taken from
`PlayerSlotConfig.HeroConfigId` at payload build time
(`GameBootstrap.BindSelectedHeroesToPlayerSpawns` +
`SimulationTickPipeline.OverrideInitialSpawnPrototype`), identical on every
endpoint because the authoritative payload carries the resulting snapshot.
Adding a hero therefore only requires the prefab table, unit catalog and hero
display table — never a scene edit. The authored `UnitPrototypeId` on a
player-controlled spawn is now only a placeholder/fallback.

## D-047 — Structured unit arbitration and fixed Main/Base Runtime ownership (2026-08-22)

**Status:** Frozen and implemented by ExecPlan 0137.

- The ordinary action chain remains
  `Intent -> Planner -> ActionRequest -> Arbiter -> ActionRuntime -> Handler`.
  Planner may replace/clear Intent and propose one request, but never starts,
  cancels or resets Handler state.
- Arbiter policy is structural: capability, aggregated control blocks,
  `ActionStartSpec`, resource conflicts and interruptibility. Numeric
  `ActionKind` priority and named hero/ability branches are not policy.
- `ActionRuntimeSet` has exactly one Main and one Base slot. Ordinary casts and
  attack windup use Main; route movement and ability Dash use Base. Handlers
  retain timing and mechanism authority.
- A movement-locking ordinary cast owns Facing and blocks voluntary Move, but
  does not reserve Movement against an authored ability Dash. Thus Aatrox Q
  impact can coexist with E Dash while retaining Q facing. A movable Hold cast
  does not own Facing or Movement, so Varus Q Hold can coexist with route Move;
  Release preempts that Move when its Stage locks movement/facing.
- Sequential-recast waiting windows retain AbilitySession but release
  MainRuntime. Only a real legal Commit entering the next impact reacquires it.
- Pure Toggles retain their AbilitySession/state without owning Main/Base
  ActionRuntime resources. Toggle activation/deactivation never preempts or is
  blocked by another Runtime; the existing AbilityCast control block and
  Handler legality still gate the signal (D-029).
- Handler-owned automatic or signal-driven Stage transitions are reconciled in
  the same Tick. Arbiter re-describes the active Stage, updates resources and
  migrates the same AbilitySession between Main/Base without self-cancel;
  illegal conflicts with an uninterruptible Runtime fail visibly.
- Forced-behavior Move/Attack bypass only the voluntary Capability veto. Their
  AbilityMask, target/mechanism checks and fine `ControlMove`/`ControlAttack`
  blocks remain authoritative; control Attack windup is not canceled by a
  `VoluntaryAttack` block alone. Both forced behaviors use the `Forced`
  interrupt level and bypass an ordinary cast's voluntary movement lock.
- Unit Snapshot contains fixed Main/Base ActionRuntime slot state exactly as
  listed in Unit Framework v27.4 amendment section 6. Restore never replays
  start callbacks, Resolve fails visibly on missing Handler/target/ability
  authority, and checksum includes every member. GameplaySnapshot schema
  advances from 22 to 23 and bootstrap payload wire version advances from 3
  to 4; mixed packages must fail at the header/handshake and be rebuilt as a
  matching pair.

## D-048 — Local Addressables client views and Dedicated Server presentation exclusion (2026-08-23)

**Status:** Frozen for implementation by ExecPlan 0138.

- `GlobalPrefabTable` remains the only formal `PrefabKind + PrefabId` registry.
  Each entry owns a direct reference to its synchronous logical prefab and may
  additionally carry a stable client-view Addressables address. A second view
  registry or duplicate PrefabId table is forbidden.
- Gameplay, snapshots, checksums, restore and logical object pools resolve only
  the direct logical prefab. Addressables never participates in deterministic
  spawn order or Gameplay state and introduces no wire- or Snapshot-schema
  change.
- Client views are local, asynchronous and reconstructible presentation. They
  may appear after their logical Unit, must tolerate despawn/rollback/pool reuse
  while loading, and may only read the bound logical state. A failed view load
  is visible but cannot invalidate or replace the logical entity.
- The shipped catalog and bundles are local installation content only. Remote
  catalogs, content downloads, CDN paths, runtime catalog updates and hot
  update behavior are out of scope and must remain disabled.
- Every Addressables initialization, asset handle and instantiated view has one
  explicit client-side owner and exactly one matching release path. Synchronous
  `WaitForCompletion` loading is forbidden.
- `UNITY_SERVER` builds neither initialize the client content service nor retain
  Addressables catalogs/bundles, models, AnimatorControllers, animation clips,
  render materials/shaders, VFX, audio or UI assets. A build-time dependency
  audit must fail visibly when a formal server scene or logical prefab reaches
  those presentation assets.
- Baseline and post-migration dependency manifests are generated by stable,
  path-sorted `AssetDatabase` dependency traversal. They record source asset,
  dependency path, GUID, direct/transitive relationship and ownership class;
  runtime reflection scanning is not part of this contract.
