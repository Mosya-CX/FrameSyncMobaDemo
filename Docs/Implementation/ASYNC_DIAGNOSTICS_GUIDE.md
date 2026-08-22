# Asynchronous diagnostics guide

> Document class: Operational Guide
> Default read: only when diagnostics or diagnostic build switches are in scope

## Build-time switch

Use the checked Unity menu item:

```text
FrameSyncMoba/Build Diagnostics/Include Async Diagnostics
```

- Checked (default): the next Local C/S, UOS Client and UOS Server build adds
  `FRAME_SYNC_MOBA_DIAGNOSTICS` and includes the bounded asynchronous logger.
- Unchecked: the define is absent; diagnostic Conditional call sites are
  compiled out and the Player creates no worker or Unity-log subscription.
- Every build prints `[Build] ... async diagnostics included/fully compiled
  out` before `BuildPipeline.BuildPlayer`, so the selected mode is auditable.

The switch is stored in Editor preferences and applies to all four existing
build entries. It does not mutate global PlayerSettings scripting symbols.

## Runtime output

The main thread only performs bounded, non-waiting enqueue operations. A
below-normal dedicated worker batches formatting and IO every 250 ms.

- Packaged client with `-logFile`: writes
  `<UnityLogPath>.diagnostics.log`.
- Client without an explicit path: writes under
  `Application.persistentDataPath/FrameSyncDiagnostics`.
- Dedicated Server: writes its owned file under the same persistent-data
  folder and mirrors explicit FrameSync diagnostics to stdout for UOS.
- A checksum mismatch writes `.worlddump.txt` beside the owned diagnostic log.

Every entry includes UTC time, endpoint, MatchId, PlayerSlot, sequence and
severity. Unity logs are mirrored into the owned file. Both normal and priority
queues are bounded; saturation drops entries, never blocks Gameplay, and emits
a visible `[Diagnostics]` dropped-count warning. Writer failures go to stderr
and are surfaced back through a Unity error.

## Runtime arguments

An enabled package supports:

```text
-disableFrameSyncDiagnostics
-frameSyncDiagnosticsPath=<absolute-path>
```

The first is an emergency runtime disable. Use the build menu switch when a
package must compile out the diagnostic runtime completely.

`-checksumDetail` remains the independent switch for expanded checksum details.
Do not put UOS secrets, allocation credentials or authentication tokens in
diagnostic messages.
