# Current Handoff -- FrameSyncMobaDemo

> Last updated: 2026-08-10 after the first live UOS two-client match, its log
> audit, and a final source audit that found the attempted LocalNGO owner-race
> correction incomplete. This document is intended to let a new Codex task
> continue without relying on the previous chat history.

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

This does not mean the live UOS acceptance is completely closed. The first
successful run exposed two startup defects/risks and one timing observation,
listed in section 6.

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

An attempted source correction is present, but the final hand-off audit found
it incomplete:

- `Assets/Scripts/Bootstrap/LocalNgoEndpointDriver.cs`
  gates the polling performed by `Update()` through
  `OwnsClientConnectionLifecycle(FrameFlowMode)`.
- `FrameFlowMode.LocalDirect` returns true.
- `FrameFlowMode.UosOnline` returns false for that polling path.
- However, `Start()` subscribes `OnClientConnected` to
  `NetworkManager.OnClientConnectedCallback` for both flow modes, and
  `OnClientConnected()` still calls `NotifyClientConnectedOnce()` without the
  flow-mode guard. A real UOS transport connection can therefore enter the same
  invalid notification path through the callback even though `Update()` is
  gated.
- The robust minimum correction is to enforce ownership inside
  `NotifyClientConnectedOnce()` itself (or guard every caller, including the
  callback), while keeping `LobbyNetworkBridge` strict and without swallowing
  the exception.

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
- Fix the callback path, add a focused test proving a UOS connection event
  cannot call `LobbyNetworkBridge.NotifyClientConnected` through the local
  driver, then compile and live-retest with a new client package.

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

The next task should continue in this order:

1. Complete the LocalNGO ownership correction. The existing `Update()` guard is
   insufficient because `OnClientConnected()` remains ungated. Enforce the
   rule at the notification method/callback boundary and add a behavior test,
   not another helper-return-only assertion.
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

- The formal Equipment Shop catalog/content is still empty in the real scene.
- Jungle camp/test monster content and full operator acceptance remain pending.
- Several HUD/presentation assets remain placeholders or missing.
- Complete result/return and remote settlement flow has not yet received the
  same live UOS acceptance as connection and Gameplay.
- `EquipmentTargetPolicy` remains underspecified by the current formal design;
  do not invent its values.

## 11. Git/workspace state at hand-off

The repository is intentionally very dirty and the working tree is the current
implementation baseline. At the 2026-08-10 hand-off audit,
`git status --porcelain=v1` reported 655 entries after the hand-off documents
were synchronized:

```text
211 modified tracked paths
166 deleted tracked paths
278 untracked paths
```

Those numbers are evidence of accumulated project work, not an instruction to
restore or clean anything. The repository owner previously accepted historical
tracked deletions and explicitly said the current local state outranks stale
Git-era audit records. A new task must inspect a file's current role before
changing it and must not use `git checkout`, `git reset`, broad deletion or a
historical commit to reconstruct an older architecture.

Relevant current status entries for the immediate UOS continuation are:

```text
modified  Assets/Editor/UOSEnvironments.asset
modified  Assets/Resources/UOSSettings.asset
modified  Assets/Scripts/Bootstrap/LocalNgoEndpointDriver.cs
modified  Assets/Scripts/Bootstrap/Tests/EditMode/ApplicationFlowTests.cs
untracked Assets/StreamingAssets/Lua/Core/UIFormat.lua
modified  Docs/Architecture/DECISION_LOG.md
modified  Docs/Architecture/REPOSITORY_MAP.md
modified  Docs/Implementation/CURRENT_HANDOFF.md
modified  Docs/Implementation/MODULE_STATUS.md
modified  Docs/Implementation/Plans/0124_lobby_ngo_uos_end_to_end_composition_execplan.md
modified  Docs/Implementation/ROADMAP.md
modified  Docs/Implementation/TEST_PLAN.md
modified  Docs/Implementation/TEST_PREPARATION.md
untracked Docs/Implementation/BUILD_GUIDE.md
```

The `LocalNgoEndpointDriver.cs` and `ApplicationFlowTests.cs` diffs also contain
earlier scene-split/lobby work; do not assume every line in their Git diff was
introduced by the final owner-race attempt. That attempt added the `Update()`
flow-mode gate and a helper-return test, but it did not gate the NGO connection
callback and is not complete. No commit was created by this documentation pass.
