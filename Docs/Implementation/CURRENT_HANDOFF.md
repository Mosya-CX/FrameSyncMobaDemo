# Current Handoff -- FrameSyncMobaDemo

> Corrected 2026-08-10 against baseline commit `ef080c9`, the current source,
> ExecPlan 0132's equipment worktree and the retained Local C/S/UOS logs. The repository
> owner confirms that both the local C/S flow and the complete UOS flow are
> runnable; the remaining findings are narrow startup reliability/diagnostic
> issues, not a failed end-to-end baseline. This document is intended to let a
> new Codex task continue without relying on previous chat history.

## 0. Latest source-only validation (2026-08-13)

- All eight current unit prefabs under `Assets/Resources/Prefab/Unit/` were
  audited through Unity. Aatrox was the only locomotion regression: the prior
  anti-jitter edit removed the persistent AnyState walk routes, but
  `AatroxIdle_Passive` and `AatroxIdle_ULT` had no direct route into their walk
  variants, and the walk states had no routes back out.
- `AatroxAnimator.controller` now uses an explicit six-state locomotion graph
  across normal, passive-ready and World Ender Idle/Walk variants. Every state
  has direct conditional routes to the other five; no locomotion state is
  entered from AnyState and no looping state can transition to itself. This
  preserves the current walk clip's normalized time instead of restarting it
  every render frame, while allowing passive/World Ender changes during movement.
- Minion and Varus controllers already use direct Idle/Move transitions and
  required no asset change. Tower controllers contain no movement states.
- Focused validation: `AatroxFormalContentTests` 10/10 EditMode,
  `UnitPrefabAnimatorTopologyTests` 1/1 EditMode and
  `AatroxPrefabPlayModeTests` 6/6 PlayMode. The runtime test proves passive walk
  time advances continuously, switches to World Ender and normal walk without
  stopping, then returns to Idle. Unity compilation is idle and the Console has
  zero errors. No package was built.

## 0.1 Previous source-only validation (2026-08-12)

- Aatrox animation now follows the formal presentation state-projection model,
  not animation-event Gameplay. `AttackHandler` locks empowered-attack state
  at BeginAttack; `UnitAnimationDriver` maps the read-only attack snapshot,
  fixed-passive readiness, World Ender Buff presence and active Ability stage
  into 14 Animator parameters/profile bindings.
- The formal Aatrox controller uses all 24 supplied clips: base/passive/World
  Ender locomotion, normal/passive attacks in both forms, Dash variants,
  Q1/Q2/Q3 variants, W variants and R activation. Unity domain-reload
  inspection confirms 24 states, 12 AnyState routes and 18 explicit action
  exits; the formal prefab points to the recreated controller asset.
- Focused validation: `AttackHandlerTests` 14/14, Aatrox EditMode 7/7 and
  Aatrox PlayMode 3/3. Compilation refresh passed. No package was built.
- Aatrox Q1/Q2/Q3/W authoring Gizmos now use `OnDrawGizmos`, so selection is
  not required. The hero-prefab component exposes `Off / All / Single`;
  Single selects one of Q1/Q2/Q3/W and All can be separated or overlaid.

- Tower red-line presentation now follows the current `AttackTarget` intent,
  switches directly to a successor, and hides when the target dies without a
  successor; it remains presentation-only and outside deterministic state.
- `UnitTargetFilter` and its masks now have complete Unity serialization
  contracts. Formal Varus E targets enemy/alive/targetable units including
  `Structure`, and AreaDamage queries with the caster's real team.
- Formal ability projectiles, Guinsoo On-Hit, W Blight application and Blight
  detonation are regression-covered against `UnitKind.Structure`; the Blight
  non-hero cap now includes structures.
- Unity MCP compilation is clean. FrameSync EditMode is 83/83; the Unit run is
  472 pass plus the same 12 pre-existing unrelated failures; the focused tower
  red-line PlayMode test is 1/1. Per user instruction, no package was built.

## 1. Authority and working rules

- Unity version: `2022.3.62f1c1`.
- Current formal designs are only those listed in
  `Docs/Architecture/DESIGN_INDEX.md`; files under `Docs/Design/` own their
  domains. Do not revive archived/superseded designs.
- The current workspace is the implementation baseline. Historical tracked
  deletions were explicitly accepted and must not be restored merely because
  Git reports them.
- Prefer Unity MCP for Unity state, assets, scenes, compilation, Console and
  tests. Do not edit Unity YAML when MCP/Unity APIs can perform the operation.
- A build request is sent exactly once. Do not poll Unity or trigger another
  build until the user reports that the build has ended.
- UOS/NGO belongs to `FrameSyncMoba.Bootstrap`; deterministic FrameSync and
  Gameplay assemblies must remain transport-independent.

## 2. Current playable state

The current project is substantially beyond the old 2026-07-28 handoff:

- Local packaged C/S (one Dedicated Server and two independently identified
  Clients) has been accepted by the repository owner for the current flow.
- The live UOS path has reached real Gameplay with two clients:
  UOS allocation -> server Ready -> matchmaking -> public NGO endpoint ->
  identity verification -> hero select/lock -> GameScene -> Loaded/Ready
  barrier -> StartTick 3 bootstrap -> authority/prediction Gameplay.
- The server continued deterministic simulation for several minutes. The
  supplied server log contains minion and hero deaths at increasing stable
  Ticks, proving this was not only a connection/object-creation smoke test.
- The runtime resource chain is `Assets/Config/Formal/`; packaged C/S does not
  use the removed legacy fixture/resource chains. See `MODULE_STATUS.md` and
  `REPOSITORY_MAP.md` for the broader module inventory.

This does not mean every startup reliability question is closed. The accepted
run exposed one confirmed transport-capacity warning, one client-side
notification exception in the retained package, and one timing observation,
listed in section 6. The current source must be checked before treating the
old package exception as a still-reachable code path.

## 3. UOS configuration that is known to be real

- UOS project/application console:
  `https://uos.unity.cn/services/58593889-dafc-4ac2-ae40-d7183954cb47/multiverse/profiles`
- Multiverse startup/profile ID (`moba-test`):
  `0fc730a2-ce02-4768-8a75-713ddb36c3b0`
- Matchmaking config ID:
  `f01c4e66-0023-43f6-af57-dcd8b73e7b90`

These two IDs are different contracts. The profile ID must never be written
into `MatchmakingConfigID`. That mistake previously produced:

```text
The config [0fc730a2-ce02-4768-8a75-713ddb36c3b0] is not found
```

The current local configuration was corrected through Unity MCP:

- `Assets/Editor/UOSEnvironments.asset` uses the real Matchmaking config ID.
- `Assets/Resources/UOSSettings.asset` was regenerated through the UOS
  Launcher environment manager.
- Runtime reflection verified
  `Unity.UOS.Common.Settings.MatchmakingConfigID` resolves to the real
  Matchmaking ID.

Do not print or copy UOS application/server secrets into documentation,
responses or test logs. The provider's allocation response may include
secret environment values; redact it before sharing.

## 4. UOS server image and resource findings

Build entry and output:

```text
FrameSyncMoba.EditorTools.LocalNgoBuildMenu.BuildServerLinux()
Builds/UosServer/FrameSyncMobaServer.x86_64
```

UOS image settings used for the successful test:

```text
Executable permission file: FrameSyncMobaServer.x86_64
Entry command: ./FrameSyncMobaServer.x86_64 -batchmode -nographics
Protocol/port: UDP 7777
Mounted files: none
Custom environment variables: none required
Timezone: not a Gameplay contract; the tested profile used Asia/Shanghai
```

The UOS platform injects allocation/match/Agones variables. Do not manually
copy values such as allocation UUID, room ID or SDK port into the profile.

Readiness results:

- The 1 CPU / 1536 MB tier reached Ready in roughly 10 seconds.
- The smaller coupled CPU/memory tier failed the provider readiness timeout.
- The earlier `Game server is not ready` result was therefore not proof that
  the Ready SDK call was wrong. The same image succeeded after increasing the
  resource tier.
- The Linux build currently has a relatively large startup asset footprint
  (for example `resources.assets` was about 330 MB during the audit), so memory
  pressure remains the leading explanation for the smaller tier failure.

## 5. Logs and verified UOS timeline

Server log supplied by the user:

```text
E:/EgdeDownLoad/59283afe-3235-48a8-9cca-5ed2c010cb11.log
```

Client logs from the successful two-client run:

```text
Logs/UosClient/UosClient1_20260810-173400.log
Logs/UosClient/UosClient2_20260810-173400.log
```

The clients were launched with separate `--TestAccountId` values and separate
`-logFile` paths. Preserve that discipline for all future multi-client tests.

Server-side timestamps establish the following sequence:

```text
17:34:49.544  allocation read; NGO server listening; Lobby loading
17:34:51.036  first NGO client connected
17:34:52.095  second NGO client connected
17:34:53.996  all identities verified
17:35:04.136  all heroes locked; GameScene loading
17:35:05.493  client 1 Loaded + Ready
17:35:05.515  client 2 Loaded + Ready; full barrier satisfied
17:35:05.517  StartTick 3 scheduled
17:35:05.623  bootstrap broadcast and applied on server
17:35:05.629  one UTP send-queue-full error
17:36:04.626  Tick 1625 combat event
```

The Tick 1625 timestamp is consistent with the server honoring the configured
five-second D-033 launch delay:

```text
(about 59 seconds since payload - 5 seconds launch wait) * 30 Tick/s
    ~= 1620 executed Ticks
```

Therefore the server did not begin simulation during its five-second wait.

Both clients logged:

- assignment received (`124.221.168.202:7287` in this allocation);
- NGO transport connected;
- identity accepted;
- hero locked;
- GameScene loaded;
- Loaded and Ready submitted;
- bootstrap applied at StartTick 3;
- ongoing authority, prediction, rollback/replay and Gameplay events.

The Development clients write a stack trace after ordinary logs, creating
30+ MB log files. Avoid broad log dumps; extract exact markers or short line
ranges. Repetitive missing presentation assets currently include VFX IDs 1/2
and SFX ID 2. Those warnings are presentation/configuration follow-up and did
not stop Gameplay.

## 6. Open findings from the live run

### 6.1 Not fixed yet: UOS/LocalDirect connection-owner race

Observed on both clients immediately after NGO transport connection:

```text
InvalidOperationException: Lobby action requires a connected NGO client.
  LobbyNetworkBridge.RequireConnectedClient
  LobbyNetworkBridge.NotifyClientConnected
  LocalNgoEndpointDriver.NotifyClientConnectedOnce
```

Root cause:

- UOS matchmaking and connection are owned by `LobbyFlowController`.
- `LocalNgoEndpointDriver.Update()` nevertheless continued running its
  LocalDirect notification and 10-second localhost wait paths in UOS mode.
- It could observe `NetworkManager.IsConnectedClient` before
  `LobbyFlowController` had bound `LobbyNetworkBridge` as the client owner.
- The correct UOS owner then bound the bridge and sent identity successfully,
  so the exception polluted the log but did not block this match.

The retained UOS client package logged this exception, but the current source
has moved beyond the description originally written in this hand-off:

- `Assets/Scripts/Bootstrap/LocalNgoEndpointDriver.cs`
  gates the polling performed by `Update()` through
  `OwnsClientConnectionLifecycle(FrameFlowMode)`.
- `FrameFlowMode.LocalDirect` returns true.
- `FrameFlowMode.UosOnline` returns false for that polling path.
- `TryStartEndpoint()` returns immediately for `FrameFlowMode.UosOnline`.
- `OnClientConnected` is subscribed only inside `StartClient()`.
- `StartClient()` is reached only by the LocalDirect branch after the local
  start request. It is not unconditionally subscribed from `Start()`.
- `NotifyClientConnectedOnce()` itself still has no flow-mode guard. Adding
  that defensive guard and a callback-level behavior test would make the
  ownership invariant local and easier to audit, but the current source does
  not support the earlier claim that every UOS connection necessarily enters
  this callback.

Test added:

```text
FrameSyncMoba.Bootstrap.Tests.ApplicationFlowTests
  .ClientConnectionLifecycle_HasExactlyOneOwnerPerFlowMode
```

Validation and limitation:

- Unity MCP AssetDatabase refresh/compilation completed successfully.
- Focused EditMode result: 1/1 passed.
- That test only verifies the helper returns true for `LocalDirect` and false
  for `UosOnline`; it does not drive `OnClientConnectedCallback` or prove the
  helper guards every notification entry point. It is therefore insufficient
  behavioral coverage and its green result must not be treated as closure.
- Add a focused callback-level test before further ownership changes. Do not
  treat the old packaged exception as proof that commit `ef080c9` still has an
  unconditional UOS callback subscription.

### 6.2 Not fixed yet: startup UTP send-queue saturation

Immediately after the server broadcast the roughly 45 KB bootstrap to two
clients, it logged:

```text
Error sending message: Unable to queue packet in the transport.
Likely caused by send queue size ('Max Send Queue Size') being too small.
```

Current serialized `UnityTransport` setting in both endpoint scenes is:

```text
m_MaxPacketQueueSize: 128
m_MaxPayloadSize: 6144
```

Both clients ultimately received and applied the payload, so the log does not
prove bootstrap loss. It does prove the startup burst exceeded a transport
queue at least once, which can delay/drop an adjacent reliable message and
should be corrected and revalidated. Do not mark this resolved until the
scene transport settings are changed through Unity MCP/Unity APIs and the live
server log contains no queue-full error.

### 6.3 Not diagnosed yet: Loading closed with match time near 30 seconds

The operator observed the HUD appear when the displayed match time was already
near 30 seconds. Known facts:

- The HUD uses `Runtime.CurrentTick - MatchRule.RunningStartTick`, not the
  Lobby matchmaking timer.
- The server honored its configured five-second wait.
- The client received and applied a StartTick 3 payload.
- The client logs have no wall-clock timestamps around payload receipt and HUD
  opening.
- A later client input log shows local Tick 807 / synchronized Tick 802, but
  that was the first recorded input and is not proof that the HUD opened at
  Tick 807.

Possible causes still requiring evidence:

1. delayed/retried bootstrap or authority delivery after queue saturation;
2. endpoint absolute-clock offset under D-033's `LaunchUtcTicks` comparison;
3. client main-thread delay before `GameBootstrap.Update()` closes Loading;
4. another launch/HUD scheduling defect not visible in current logs.

Do not state that clock skew is the root cause yet. Do not revise the frozen
D-033 launch contract merely from the current logs. The next package should
log UTC, local monotonic time and current Tick at:

```text
server bootstrap broadcast
client bootstrap receive/apply
client launch barrier reached
client Loading close / HUD open
first accepted AuthorityFrame
```

That single rerun should distinguish message latency, clock offset and main
thread delay.

## 7. Other source/config corrections already included in the latest client

- `Assets/StreamingAssets/Lua/Core/UIFormat.lua` floors the total seconds
  before formatting with `%02d`. This fixed the packaged Lua exception
  `number has no integer representation` on the matchmaking timer.
- The UOS Matchmaking config ID in Launcher-backed settings is the real
  Matchmaking ID, not the Multiverse profile ID.
- Matchmaking clients must always receive different test account IDs; otherwise
  identity/ticket behavior is not a valid two-player test.

## 8. Exact next continuation

The next network-reliability task should continue in this order; unrelated
Gameplay/content slices may proceed independently:

1. Add callback-level LocalDirect/UOS ownership coverage. If the test exposes
   a reachable UOS notification path, enforce the rule inside
   `NotifyClientConnectedOnce()`; do not assume the old package stack trace
   describes the current subscription topology.
2. Through Unity MCP/Unity APIs, increase and verify endpoint UTP startup queue
   capacity using a value justified by the approximately 45 KB fragmented
   bootstrap sent to two clients. Do not edit scene YAML manually.
3. Add narrowly scoped launch timeline diagnostics listed in section 6.3.
   Avoid per-Tick logs.
4. Trigger Unity compilation once and inspect Console. Ignore only MCP's own
   asset-path inspection errors; do not ignore project compiler errors.
5. Run only focused EditMode tests unless the scene/transport change requires a
   focused PlayMode configuration test.
6. Build the UOS client once and wait for the user to report completion.
7. Reuse the already uploaded server image only if no server-side scene/code
   changed. If the server transport configuration changes, build/upload a new
   Linux image once before the rerun.
8. Launch two clients with unique test account IDs and separate timestamped log
   files, then let the user perform matchmaking.
9. After the user ends the run, check that:
   - the LocalNGO exception is absent;
   - the server send queue error is absent;
   - payload-apply and HUD-open timing explain the Loading observation;
   - both endpoints share the expected StartTick/checksum progression.
10. Only then revise D-033 if evidence proves the absolute UTC barrier is the
    design defect. If D-033 changes, update the wire/runtime tests and
    `MODULE_STATUS.md` in the same task.

## 9. Build and process discipline

- Build methods and paths are documented in `BUILD_GUIDE.md`.
- Local C/S procedure is documented in `C_S_TEST_GUIDE.md`.
- UOS client/server builds are Development builds with verbose logs; always use
  explicit `-logFile` paths.
- Never send a second build request because a build looks slow. Wait for the
  user's explicit “打包结束”.
- Do not operate Unity through MCP while a player build is in progress.

## 10. Remaining broader product limitations

These are unrelated to the current UOS startup correction and must not be
silently expanded into it:

- ExecPlan 0132 has landed in the worktree: GameScene references the formal
  five-item Guinsoo catalog and the Seething Strike Buff is registered in the
  formal Buff catalog. HeroTestDriver also bakes this catalog and composes the
  formal shop/gold runtimes for a local-Tick-only test flow with a 10000-gold
  initialization baseline. It submits canonical shop Commands to its local
  `SimulationTickPipeline` without any network transport; it must not regress
  to an empty database, fake HUD gold or direct UI mutation of
  `EquipmentHandler`. HUD and Shop read the same current balance, and the
  visual owned-cell overlay must remain non-raycast so purchased entries can
  still be selected for details and selling. A new schema-22 local C/S pair
  was built together through `LocalNgoBuildMenu.BuildBoth()` on 2026-08-10;
  both Server and Client reports returned Success. This new pair still needs
  a fresh multi-process runtime acceptance. No new UOS Linux package has been
  built for this task.
- Jungle camp/test monster content and full operator acceptance remain pending.
- Several HUD/presentation assets remain placeholders or missing.
- Complete result/return and remote settlement flow has not yet received the
  same live UOS acceptance as connection and Gameplay.
- `EquipmentTargetPolicy` remains underspecified by the current formal design;
  do not invent its values.

## 11. Git/workspace state at corrected hand-off

Before ExecPlan 0132 began, the repository was clean at:

```text
ef080c9 整合正式对局配置、Lua UI 与玩法系统重构
```

The old 655-entry dirty-worktree paragraph described a pre-commit state and is
not the current baseline. Continue to preserve user changes and avoid broad
reset/checkout operations, but do not report that historical status as the
present Git state.

## 12. 2026-08-11 cs26 Shop diagnosis and XP-range correction

The successful 2026-08-10 local pair was launched as cs26 (Server + two
Clients). The operator reached GameScene and observed that all five Shop
entries displayed price `0` and no purchase could be submitted. All three
processes were stopped before diagnosis.

The cs26 logs contained no Shop request/action entries. They did contain the
separate known server-side UTP send-queue saturation warning, but no exception
that explained zero prices. Source tracing found the exact Shop deadlock:

```text
Lua price -> LocalEquipmentShopView.CalculatePurchasePrice
           -> EquipmentShopRuntime.GetTrader == null -> 0

Lua purchase -> EquipmentShopRuntime.RequestPurchase
             -> TryResolveHandler requires Trader -> ControlledUnitNotFound

Shop Command dispatch would create Trader, but RequestCheck could never submit
the first Command.
```

The correction follows Equipment/Gold v12 instead of pre-creating Trader
state. While Trader is absent, read-only price/RequestCheck resolution scans
the formal `ControlledByPlayerSlot` mapping; after a Purchase/Sell plan passes,
the deterministic command pipeline creates Trader and records the successful
transaction. Purchase/Sell/Undo Lua actions now emit one `[ShopRequest]` line
with the request inputs and result. There is no per-Tick log spam.

The requested minion experience audit also found a real units bug:
`MinionRewardShareRadius` was squared directly as `800 * 800` against logic
positions. It now first multiplies 800 by
`UnitWorld.StatDistanceToLogicDistanceScale`; the current 0.01 configuration
therefore produces an approximately 8-unit logic radius.

Post-fix verification:

```text
Unity compilation: passed
EquipmentShopRequestTests: 9/9
EquipmentShopTransactionTests: 3/3
MatchRewardDistanceTests: 1/1
FrameSync EditMode assembly: 77/77
GameBootstrapPlayModeTests: 1/1
HeroTestSceneEquipmentPlayModeTests: 2/2
```

`LocalNgoBuildMenu.BuildBoth()` was requested exactly once and completed on
2026-08-11. Both Server and Client Unity reports ended with
`Build Finished, Result: Success`; their managed gameplay assemblies were
written at 01:10 and 01:12 respectively. The next action is a fresh C/S run
that confirms non-zero base prices, successful first purchase, and useful
`[ShopRequest]` evidence if any action still rejects.

## 13. 2026-08-11 cs27 purchase-command composition correction

The rebuilt pair was launched as cs27. All equipment prices displayed with
their expected non-zero values, closing the cs26 lazy-Trader pricing issue.
Clicking Purchase failed on both clients with this exact Lua/C# exception:

```text
EquipmentShopRuntime requires a command submitter before RequestPurchase.
  at EquipmentShopRuntime.RequestPurchase
  at GameBootstrap.BindGameFlowLuaBridge
  at UI.Shop
```

The formal `PlayerCommandRequester` already implements
`IEquipmentShopCommandSubmitter` and creates the existing canonical Shop
`GameplayCommand`; no protocol was missing. The composition root attempted to
inject it during `GameBootstrap.Awake()`, before local-player binding had
created the requester, so the conditional silently did nothing. All cs27
Server and Client processes were stopped before diagnosis.

The correction removes that ineffective early check and calls
`Runtime.ConfigureShopCommandSubmitter(requester)` immediately after
`PlayerInputController.Initialize(...)` inside `BindLocalPlayer()`. The
formal Bootstrap PlayMode test now binds a real local hero, requests the first
catalog purchase, and asserts that exactly one canonical
EquipmentShop/Purchase command with the selected equipment ID enters the
shared `CommandCollector`.

Post-correction evidence:

```text
Unity compilation: passed; Console errors: 0
GameBootstrapPlayModeTests.ClientComposition_InitializesFromProjectAssets: 1/1
PlayerCommandRequesterTests.ShopRequests_UseCanonicalCommandsAndSharedSequence: 1/1
```

No public contract, command schema or deterministic snapshot changed. A new
matching C/S package pair and live purchase/sell/undo acceptance are still
required; the existing cs27 binaries do not contain this source correction.

## 14. 2026-08-11 cs28 acceptance and integer kill-reward correction

The cs28 Server + two-Client logs are healthy: no Lua/C# exception, checksum
mismatch, rejected Shop command or transport disconnect was found. Purchase,
Sell and Undo actions all reached the deterministic pipeline with
`allowed=True`. The repeated `authorityEndsMatch=False` observations were
followed by successful rollback comparisons (`match=True`), not replay
divergence. All three processes exited cleanly. The remaining noisy warning is
the known missing VFX prefab for VFX id 1.

The operator's apparent missing minion gold was not a missing producer in the
cs28 binary. The logs contain CreepKill results for the local slot-0 hero. At
the third client-0 purchase the displayed balance was exactly explained by:

```text
500 initial + 596 confirmed natural income
+ 48 minion last-hit income (14 + 14 + old melee value 20)
- 700 previous purchases
= 444 current available gold
```

The UI path is also live: `TickCompleted -> PushUiSnapshot` writes
`EquipmentShopView.GetCurrentAvailableGold()` into `LuaDataCache`, and the HUD
Lua host refreshes `GameFlow.GetHudGold()` every rendered frame. The old build
had no income-confirmation diagnostic, so natural income obscured each kill
increment. Source now logs only confirmed UnitKill records as:

```text
[GoldIncomeConfirmed] tick=... slot=... amount=21 total=... sequence=... reason=UnitKill
```

Requested formal values are now authored and regression-tested: initial gold
1500, melee minion 21, ranged minion 14 and hero 300. Hero rewards use integer
3/5 and 2/5 shares (300 with two assistants -> 180/60/60; no assistants ->
300 to the killer). `GoldAllocation.GoldAmount` was corrected from `fp` to
`int`, eliminating fixed-point truncation from currency. HeroTest keeps its
explicit 10000 local test baseline.

The shop/equipment audit against Equipment/Gold v12 found the implemented
vertical slice conformant for the global formal catalog, global-table-driven
enumeration, static icons and English text, dynamic recipe discounts,
component consumption, Basic/Advanced repetition, Finished-item uniqueness,
six inventory slots, owned-item selection, Sell, Undo, lazy Trader creation,
canonical Shop Commands, OperationLog accounting and the sole
`GoldIncomeRuntime` confirmed-income ledger. Remaining limits are active-item
targeting and the documented producer-contract conflict between Equipment v12
6.5-6.6 and Combat v13.2 1.5/11.10. The implementation currently follows the
v12 MatchStatistics producer path and resolves ReceiverHeroUid to PlayerSlot in
the Tick pipeline; that ownership contract was not silently refactored.

Verification after the correction:

```text
Unity compilation / Console errors: passed / 0
FrameSync EditMode assembly: 82/82
FormalGoldRewardConfigTests: 1/1
GameBootstrapPlayModeTests: 1/1
HeroTestSceneEquipmentPlayModeTests: 2/2
```

The broad Unit EditMode run still reports the same 12 unrelated existing
failures (movement, Dash, assembly boundary and other legacy fixtures); no
reward or equipment test failed. `GameBootstrapPlayModeTests` was also fixed
to use the formal `UnitWorld.SpawnUnit(UnitSpawnRequest)` path: its former call
to the `UnitTestFactory` extension replaced the shared formal PrefabTable and
contaminated later HeroTest runs.

## 15. 2026-08-11 Aatrox formal hero vertical slice

Aatrox is now the second formal hero in the shared configuration pipeline.
The combined `FormalHeroAbilityRuntimeCatalog` contains both Varus and Aatrox;
per-loadout active-ability overrides keep the common Q/W/E/R physical slots
without selecting another hero's ability. GameScene and HeroTestScene both
reference this combined catalog. Stable content IDs are prototype/prefab
`1002/1102`, passive/Q/W/E/R `10020-10024`, Buffs
`12021/12022/12024`, W projectiles `109/2105` and `110/2106`, Q VFX
`3101-3103`, and
KnockUp `113`.

The implemented mechanics are data-driven: Q has three exact rectangle,
trapezoid and offset-circle impacts with sweet spots, delayed 0.9-second
settlement, 30-Tick minimum recast delays and 120-Tick windows; its cast
direction is locked while the impact origin samples the caster after an E
displacement. W applies rank slow to any valid non-Structure first target,
doubles first-hit damage to minions, creates a hero-only 1.5-second tether by
spawning a stationary area projectile, and settles repeat damage plus a 9-Tick
pull on expiry. The area projectile owns the supplied
`Resources/Prefab/Missle/InfernalChainsArea.prefab`; leaving the area, death or
Buff removal ends it through `ProjectileWorld`. Large-monster tethering remains
deferred by user decision. E supplies rank omnivamp, resets basic-attack timing
and uses a 300-unit dash with deterministic terrain-landing extension up to
500. R fears enemy Minions/Monsters within 200 units for 45 Ticks, then owns
the ten-second attack-damage, healing, decaying move-speed, ghosting and
kill/assist refresh Buff. The fixed passive owns ready-state attack range,
target-max-health magic damage, actual-damage healing and deterministic
cooldown reduction.

Parameter debugging is editor visualization, not client debug UI. The
`AatroxHeroRuntime.prefab` gizmo can draw Q1/Q2/Q3 primary and sweet-spot zones plus the
W tether trapezoid through `AatroxAbilityZoneAuthoringGizmo`; it reads the
formal Q Ability values and W area-projectile authoring component and contributes no runtime state,
snapshot or checksum bytes. `Off`, `All` and `Single` are user-controlled
authoring preferences; automated tests do not prescribe or overwrite them.

The Aatrox runtime resource layout was corrected after review. The supplied
`AatroxSpellWMissle.prefab` and `InfernalChainsArea.prefab` now directly carry
their Physics/projectile authoring under `Assets/Resources/Prefab/Missle/` and
are registered as `2105/2106`. The copied `AatroxWProjectileRuntime`, pure-VFX
tether wrapper and unused `Unit_Hero_Aatrox` prefabs were reference-checked and
deleted. `Assets/Config/Formal/Prefabs/` now retains only the finished
`AatroxHeroRuntime.prefab` for this hero. `AatroxAnimator.controller` now matches
the `VarusRuntime` presentation contract with locomotion, attack and formal
ability-state transitions.

`AbilityLevelValue` was made Unity-serializable without adding a public
mutation API. This is necessary for fixed-point rank arrays embedded directly
inside Buff managed-reference effects. Q/W/E and W/R Buff rank-1 values are
covered after an actual domain reload, preventing an in-memory-only asset
repair from passing unnoticed.

HeroTestScene now uses the finished Aatrox formal content: its serialized
`heroPrototypeId` is `1002`, target dummies remain `1001`, and the standalone
driver reads `FormalHeroAbilityRuntimeCatalog` instead of the Varus-only
catalog. Test controls are Q/W key-to-indicator plus primary-click Commit, E
immediate cursor-direction Commit, and R self-cast. The actual scene was entered through Unity MCP and
reported prototype `1002` with Q/W/E/R `10021/10022/10023/10024`; no runtime
Console error occurred.

Verification:

```text
Unity MCP compilation / Console errors: passed / 0
AatroxFormalContentTests (focused EditMode): 7/7
InfernalChains projectile pipeline integration (EditMode): 1/1
AatroxPrefabPlayModeTests (PlayMode): 2/2
HeroTestSceneEquipmentPlayModeTests (PlayMode): 2/2
```

The focused legacy `StageDefBakeTests` class still reports its two known Dash
fixture failures (default authoring lacks a StageKey; a worldless Dash expects
Running). The new Aatrox tests and all final Console checks pass. No C/S or UOS
package was requested or built for this slice.

## 16. 2026-08-12 Aatrox input and presentation follow-up

Q and W now use the same formal local-aim contract in both the C/S input path
and the standalone Tick-only HeroTest driver: the skill key opens the indicator
and primary click sends the first deterministic Commit. Q's runtime indicator
reads the current sequential-recast StageDef and renders the exact Q1 rectangle,
Q2 trapezoid or Q3 offset circle, including the authored sweet spot. W retains
the standard direction bar used by Varus R. The Q recast minimum-delay window is
projected through the existing HUD cooldown channel without changing Gameplay
cooldown or snapshot state.

All three normal and three World Ender Q clips are exactly one second. Their
formal impact delays and impact stages are 30 Ticks (1.0 second), matching the
animation duration exactly. `AatroxDeath.anim` is routed from AnyState
by `LifeState`, and living routes are guarded from overriding death.

KnockUp and KnockBack now receive a generic presentation-only parabolic model Y
offset derived from control duration. KnockBack uses half the corresponding
KnockUp peak because it also has authoritative planar motion. World Ender Buff
`12024` controls the four wing/banner bone roots on the Aatrox prefab, keeping
them hidden outside R. Neither feature writes the authoritative unit root or
enters snapshot/checksum state.

Verification after this follow-up:

```text
Unity MCP compilation / project Console errors: passed / 0
AatroxFormalContentTests (EditMode): 8/8
AbilityInputMappingTests (EditMode): 16/16
CooldownPipelineTests (EditMode): 6/6
AatroxPrefabPlayModeTests (PlayMode): 3/3
```

The editor range-gizmo display mode remains the user's explicit `Off` setting.
The test validates only that the component and its Q/W authoring references are
valid; it does not require `All` or a particular `Single` selection.

## 17. 2026-08-12 Aatrox passive UI, E facing, Q icons and VFX direction

HeroTest's passive cooldown bridge no longer returns constant zero. It reads the
same fixed-passive `NextReadyLogicTick` and level-scaled total cooldown used by
the formal bootstrap, so consuming Aatrox's empowered attack activates the HUD
cooldown mask and countdown.

Sequential-recast authoring assigns the next impact icon to each waiting
window. The UI remains compliant with Ability v15.2 by reading the current
`CastStage.IconOverride`: after Q1 the current window displays Q2, and after Q2
the current window displays Q3, rather than waiting for the next Commit.

Dash movement now checks for an already active movement-locking cast. It still
applies its deterministic planar displacement, but preserves the locked cast
direction instead of turning the Unit toward the dash. This lets E reposition
during Q without changing the Q direction. The state is derived from existing
Ability sessions, so snapshot schema 22 and checksum membership are unchanged.

`VfxEvent.WorldDirection` is kept in world space for attached effects. Attached
Q VFX continue following the caster position, but a later Unit turn no longer
rotates the effect a second time. This lock is a rebuildable presentation
component and never feeds back into Gameplay.

```text
Unity MCP compilation / final Console errors: passed / 0
AatroxFormalContentTests (EditMode): 8/8
MovementHandlerTests (EditMode): 19/19
HeroTestSceneEquipmentPlayModeTests (PlayMode): 2/2
AatroxPrefabPlayModeTests (PlayMode): 4/4
```

No C/S or UOS package was requested or built.

## 18. 2026-08-12 Aatrox W timing and complete outline coverage

Aatrox W now uses a 14-Tick deterministic Commit stage, equal to 0.4667 seconds
at the formal 30 Hz simulation rate. Both normal and World Ender W animation
clips and the projectile spawn delay use the same 14-Tick duration, so Gameplay
release and presentation windup finish together.

`ClientUnitOutline` previously built one outline material slot for Aatrox's
single `SkinnedMeshRenderer`, but that renderer contains five submeshes. Unity
therefore rendered the inverted hull only for the first Wings submesh. The
outline renderer now repeats the same local material instance for every source
submesh, covering Wings, Body, Sword, Shoulder and Banner without creating
additional material instances or affecting deterministic state.

```text
Unity MCP compilation: passed
AatroxFormalContentTests (EditMode): 8/8
AatroxPrefabPlayModeTests (PlayMode): 5/5
```

The user requested a local C/S package after this verification. Trigger it once
through `LocalNgoBuildMenu.BuildBoth()` and perform no further Unity operation
until the user reports that packaging has finished.

## 19. 2026-08-13 exact cast timing and locked-camera movement stability

Aatrox Q1/Q2/Q3 impact stages and their authored damage delays now use 30 Ticks,
exactly matching the six 1.0-second normal/World-Ender clips. Aatrox W uses 14
Ticks for its Commit stage and projectile spawn delay; both W clips were retimed
to 14/30 seconds because the deterministic 30 Hz simulation cannot represent
0.45 seconds exactly.

The right-click movement path and the HeroTest diagnostic move path both submit
the same deterministic Move Command. Runtime sampling found no model-local drift
or persistent Animator restart. The visible shake came from the locked camera
smoothing a 30 Hz stepped unit root before/against `PhysicsEntity2D.LateUpdate`.
`CameraController` now follows real `PhysicsEntity2D` targets exactly in a later
`LateUpdate`, keeping camera and controlled-unit projection on one render
timeline. Continuous non-Gameplay debug rigs retain their authored damping.

HeroTestScene remains configured for Aatrox prototype `1002`. No package was
requested for this follow-up.

```text
Unity MCP compilation / Console errors: passed / 0
AatroxFormalContentTests (EditMode): 10/10
UnitPrefabAnimatorTopologyTests (EditMode): 1/1
CameraControllerPlayModeTests (PlayMode): 1/1
AatroxPrefabPlayModeTests (PlayMode): 6/6
PlayerInputSimulationPlayModeTests (PlayMode): 3/3
```

## 20. 2026-08-13 two-client shop rollback and live client feedback

The retained UOS logs were correlated using the user's exact order: client 2's
hero died first, client 1's hero died second, then client 2 bought two items.
Client 1 accepted the first remote purchase at Tick 5845 and reached Tick 5881
with identical canonical command bytes but a different checksum. Its dump
already contained both correct equipment definitions and stat handles.

The correction path was executing non-hero `Resolve/Rebuild` twice:
`SimulationTickPipeline.RestoreFromSnapshot` already owns the complete formal
three-phase restore, while `PredictionRollbackCoordinator` called the same
non-hero phases again. The duplicate coordinator calls are removed. Shop-command
Ticks now emit checksum segments on authority/replay endpoints, and a persistent
mismatch writes the local segment set beside the world dump.

The same slice fixes Aatrox attack presentation by detecting attack starts from
the snapshotted start Tick and mapping the deterministic sequence onto the
finite authored variants. Attack range now subtracts only the target collision
radius. Match status/time, staged loading progress/status and a configurable
0.5-second presentation-only ping echo are live-updated.

Focused equipment restore/replay, authority replication, attack range,
animation mapping, ping and GameBootstrap PlayMode tests pass. The full
EditMode baseline remains 860/870 with the same ten pre-existing failures. No
package was built; a rebuilt two-client UOS reproduction is still required for
live acceptance.

## 21. 2026-08-13 bounded asynchronous UOS diagnostics

The former global `List<string> + Task.Run(AppendAllLines)` diagnostic helper
has been replaced by a compile-selectable, bounded asynchronous transport.
Enabled clients and servers mirror Unity logs to an owned file; explicit
FrameSync diagnostics also go to stdout for UOS Dedicated Server collection.
Normal and priority queues are bounded, and producers only perform non-waiting
enqueue operations. Saturation increments a visible dropped count rather than
blocking the simulation thread. Writer failures are no longer swallowed.

`FrameSyncMoba/Build Diagnostics/Include Async Diagnostics` is checked by
default. Unchecking it removes `FRAME_SYNC_MOBA_DIAGNOSTICS` from all existing
Local/UOS BuildPlayerOptions, so Conditional calls disappear and no worker or
Unity-log subscription is created in the package. Enabled packages also accept
`-disableFrameSyncDiagnostics` as an emergency runtime override.

Focused diagnostics tests pass 4/4, build-option composition passes 1/1, and
the existing client/server bootstrap PlayMode tests both produced and cleanly
flushed endpoint files. No package was built.
