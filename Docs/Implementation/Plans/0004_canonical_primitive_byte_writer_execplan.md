# ExecPlan 0004 — Canonical Primitive Byte Writer

> Status: **Complete — implemented and Unity-verified on 2026-07-20.**  
> This plan adds only the lowest-level allocation-free canonical primitive writer required by later Command, checksum and snapshot slices.

## 1. Purpose

Provide one reusable deterministic byte-writing boundary for primitive Gameplay values so later systems do not choose platform-dependent layouts independently.

Observable result:

```text
The same primitive values always produce the same explicit byte sequence.
The caller supplies and owns the fixed-capacity buffer.
Writing performs no per-value allocation and never partially writes a value.
```

## 2. Progress

- [x] Re-read current design selection, architecture decisions, Roadmap and repository status.
- [x] Inspect the current deterministic assembly, tests and fixed-point package representation.
- [x] Search current Assets, Packages and Docs for an equivalent writer or checksum protocol.
- [x] Confirm the pre-change Unity Editor is idle.
- [x] Create this ExecPlan before changing the public contract.
- [x] Implement `CanonicalByteWriter` in `FrameSyncMoba.Deterministic`.
- [x] Add proportional EditMode golden-byte and failure-atomicity tests.
- [x] Refresh/compile and inspect Console through Unity MCP.
- [x] Run targeted and full EditMode tests and query the PlayMode baseline.
- [x] Review duplicates, dependency direction and task scope.
- [x] Update this plan and status documentation with actual results.

## 3. Surprises and discoveries

- FrameSync v10.2 requires complete canonical Command bytes and a shared Gameplay checksum, but it does not freeze primitive byte order or a writer API.
  Impact: this plan must make the byte layout explicit and test it with literal golden bytes; it does not claim that the design already named the helper.
- The approved fixed-point package exposes `fp.RawValue` as a signed `long` and reconstructs values with `fp.FromRaw(long)`.
  Impact: canonical fixed-point bytes are exactly the canonical signed 64-bit representation of `RawValue`; no float conversion is permitted.
- No current production or package-owned `CanonicalByteWriter`, `ChecksumWriter`, `WriteFp` or equivalent project protocol was found.
  Impact: adding one project-owned writer does not duplicate an existing contract.
- The current deterministic runtime assembly already has the required fixed-point dependency and no UnityEngine dependency.
  Impact: no asmdef or Package modification is needed.
- Unity MCP `console-clear-logs` could not clear its own `Temp/mcp-server/ai-editor-logs.txt` because another MCP process held the file. The retry produced the same tool-level error.
  Impact: compile evidence was isolated through the successful synchronous AssetDatabase refresh, idle Editor state and Console inspection. The only returned errors were this MCP file-lock report plus its domain-reload assertion; there was no C# compiler diagnostic.
- Unity MCP's assembly-filtered test summary reports the project discovery total separately from the filtered execution count.
  Impact: the targeted deterministic result is recorded from 35 returned passing cases; the following unfiltered EditMode run is the authoritative 40/40 project total.

## 4. Decision log

### D-0004-01 — Explicit canonical byte layout

- Multi-byte integers use little-endian order, independent of host architecture.
- Signed integers use their fixed-width two's-complement bit pattern.
- `bool` is one byte: `false = 0x00`, `true = 0x01`.
- `fp` is eight bytes obtained from `fp.RawValue` and written using the signed 64-bit rule.
- `byte` is written unchanged.

The implementation writes shifts explicitly and does not use host-endian `BitConverter` output.

### D-0004-02 — Caller-owned fixed-capacity storage

`CanonicalByteWriter` is a reusable sealed class constructed with a caller-provided `byte[]`. It never resizes or replaces the array. Construction is outside per-Tick writing; individual write operations allocate no managed objects.

### D-0004-03 — Read access without copying

The writer exposes `WrittenCount`, `Capacity`, `RemainingCapacity` and `GetWrittenSegment()`. The returned `ArraySegment<byte>` is a non-copying view over the caller-owned array. Consumers must use only its declared offset/count.

### D-0004-04 — Failure atomicity and reset

Every write validates complete remaining capacity before changing the cursor or buffer. Capacity failure throws `InvalidOperationException` with no partial value write. `Reset()` sets the cursor to zero but does not clear the caller-owned storage.

## 5. Current repository context

- `FrameSyncMoba.Deterministic` is a no-engine, non-auto-referenced assembly depending only on `Unity.Mathematics` and `Unity.Mathematics.FixedPoint`.
- Completed plans 0001 and 0003 own the Tick context and sole deterministic random service.
- `FrameSyncMoba.Deterministic.Tests` contains 30 passing EditMode cases before this plan.
- `FrameSyncMoba.Unit.Tests` contains 5 passing EditMode cases; full baseline is 35/35.
- Unity 2022.3.62f1c1 was idle before modification (`IsPlaying=false`, `IsCompiling=false`, `IsUpdating=false`).
- Pre-task Git history is not part of this execution; only the files listed below are in scope.

Expected files:

```text
Assets/Scripts/FrameSyncMoba/Deterministic/CanonicalByteWriter.cs
Assets/Tests/EditMode/Deterministic/CanonicalByteWriterTests.cs
Docs/Implementation/Plans/0004_canonical_primitive_byte_writer_execplan.md
Docs/Implementation/MODULE_STATUS.md
Docs/Architecture/REPOSITORY_MAP.md only if repository structure evidence changes
```

## 6. Design sources

- `Docs/Architecture/DESIGN_INDEX.md`
  - selects FrameSync v10.2 and Snapshot Appendix v7.2.
- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`
  - 10.3 requires stable Command ordering and authoritative canonical bytes;
  - 10.4–10.5 carry canonical Command byte sequences;
  - 12.1–12.3 require complete canonical Command comparison and `SharedGameplayChecksum` over deterministic state.
- `Docs/Design/FrameSync_Snapshot_Contents_Appendix_v7_2.md`
  - fixes later snapshot/checksum participation but does not define this primitive writer's API.
- `Docs/Implementation/ROADMAP.md`
  - Phase 1 requires canonical serialization helpers and byte-identical output.
- `Docs/Architecture/DECISION_LOG.md`
  - D-002 requires complete canonical Command bytes;
  - D-022 fixes `Unity.Mathematics.FixedPoint.fp` and its deterministic raw-state boundary;
  - D-023 requires proportional automated tests.

## 7. Scope

### In scope

- one `CanonicalByteWriter` in `FrameSyncMoba.Deterministic`;
- byte, bool, signed/unsigned 32-bit, signed/unsigned 64-bit and `fp` writes;
- explicit little-endian layout and fixed-width signed representation;
- caller-owned fixed-capacity buffer, cursor/reset and non-copying written segment;
- capacity validation before mutation;
- focused EditMode tests and Unity MCP validation;
- plan/status synchronization.

### Out of scope

- canonical reader/deserialization;
- checksum/hash algorithm or `SharedGameplayChecksum` type;
- Command, Snapshot, UID, Aim, AbilitySignal or other aggregate serializers;
- strings, variable-length integers, collections, type tags or reflection serialization;
- buffer pooling, growth, streams or network transport;
- Unit/UnitWorld, PhysicsEntity2D or random geometry implementation;
- asmdefs, Packages, scenes, prefabs, ScriptableObjects, Input Actions or production content.

## 8. Affected assemblies

```text
FrameSyncMoba.Deterministic
    modified public surface; dependency set unchanged

FrameSyncMoba.Deterministic.Tests
    new Editor-only focused fixture; dependency set unchanged
```

Dependency direction remains:

```text
future Gameplay assemblies -> FrameSyncMoba.Deterministic
FrameSyncMoba.Deterministic.Tests -> FrameSyncMoba.Deterministic
```

## 9. Exact production types and public contracts

New public type:

```csharp
public sealed class CanonicalByteWriter
{
    public CanonicalByteWriter(byte[] buffer);

    public int Capacity { get; }
    public int WrittenCount { get; }
    public int RemainingCapacity { get; }

    public ArraySegment<byte> GetWrittenSegment();
    public void Reset();

    public void WriteByte(byte value);
    public void WriteBoolean(bool value);
    public void WriteInt32(int value);
    public void WriteUInt32(uint value);
    public void WriteInt64(long value);
    public void WriteUInt64(ulong value);
    public void WriteFp(fp value);
}
```

No existing public signature changes. No new UID, Command, Snapshot, Aim, AbilitySignal, Checksum, FixedPoint or Runtime DTO is added.

## 10. Ownership, ordering, snapshot and serialization impact

- The writer owns only its cursor; the caller owns storage lifetime and content after writing.
- Call order is byte order. The writer performs no sorting and accepts no unordered collections.
- This is a serialization helper, not a schema. Aggregate owners must explicitly define field order in later plans.
- No snapshot member or version changes in this slice.
- No checksum algorithm or checksum value type is introduced.

## 11. Implementation plan

1. Add the writer to the existing deterministic runtime folder.
2. Implement up-front capacity validation with overflow-safe remaining-space comparison.
3. Implement literal little-endian shifts for all multi-byte values.
4. Write `fp.RawValue` through the signed 64-bit path.
5. Add a focused fixture with literal golden bytes, signed boundaries, fixed-point raw identity, reset reuse and capacity failure atomicity.
6. Clear Console, refresh/compile with Unity MCP and inspect all diagnostics.
7. Run targeted deterministic tests, all EditMode tests and query PlayMode.
8. Review task-relevant changes for duplicate protocols, allocation, host-endian calls and scope.
9. Complete Results and synchronize module/repository status.

## 12. Validation

### EditMode tests

- a mixed primitive sequence matches literal golden little-endian bytes;
- signed minimum/negative values retain exact two's-complement bits;
- `fp` output equals its exact `RawValue` bytes;
- reset reuses the same caller buffer and starts writing at offset zero;
- insufficient capacity throws before cursor or buffer mutation;
- null construction is rejected.

### PlayMode tests

Not required. The type has no Unity lifecycle, GameObject, scene, input or presentation behavior. Query the existing PlayMode baseline without adding an artificial fixture.

### Unity MCP

Clear Console, refresh the AssetDatabase, wait for idle compilation, read Console, run targeted and full EditMode suites, query PlayMode, and confirm the open scene remains clean.

## 13. Failure conditions and recovery

Stop this plan if a current formal design or existing production type freezes a conflicting primitive layout, requires an out-of-scope public protocol change, or requires a new Package/asmdef dependency.

Otherwise failures are local to the new runtime and test files. Fix them in place; do not weaken or disable tests, restore historical files, or modify unrelated assets.

## 14. Completion criteria

- the exact public contract in section 9 compiles in the existing deterministic assembly;
- every primitive has a literal, platform-independent byte layout;
- `fp` never converts through float/double;
- writes allocate no per-value managed object and do not partially mutate on failure;
- targeted and full EditMode tests pass;
- PlayMode remains correctly not applicable;
- Console has no new production diagnostic;
- no duplicate protocol, asmdef/Package/asset/content change or scope-external refactor is introduced;
- this ExecPlan and status documents record actual results.

## 15. Production-content exclusion

This slice contains no hero, ability, Buff, equipment, unit, projectile, map or balance content. Any future domain serializer will consume this generic helper through a separately reviewed plan.

## 16. Results

```text
Production file added:
    Assets/Scripts/FrameSyncMoba/Deterministic/CanonicalByteWriter.cs

Public contract added:
    CanonicalByteWriter(byte[] buffer)
    Capacity / WrittenCount / RemainingCapacity
    GetWrittenSegment()
    Reset()
    WriteByte / WriteBoolean
    WriteInt32 / WriteUInt32
    WriteInt64 / WriteUInt64
    WriteFp

Canonical layout verified:
    explicit little-endian integers
    signed two's-complement bit preservation
    bool false=0x00 and true=0x01
    fp serialized directly through signed 64-bit RawValue

Tests added:
    Assets/Tests/EditMode/Deterministic/CanonicalByteWriterTests.cs
    5 focused cases covering golden bytes, signed boundaries, reset,
    capacity failure atomicity and null storage.

Unity compilation:
    ForceSynchronousImport AssetDatabase refresh succeeded.
    IsCompiling=false and IsUpdating=false after refresh.
    No C# compiler diagnostic was returned.
    Console clearing was blocked by an MCP log-file lock; Console inspection
    returned only that MCP self-error and a domain-reload assertion.

Targeted EditMode:
    FrameSyncMoba.Deterministic.Tests: 35 returned cases passed,
    including all 5 new writer cases; 0 failed and 0 skipped.

Full EditMode:
    40 passed, 0 failed, 0 skipped.

PlayMode:
    No tests found; not applicable to this no-engine helper.

Scene:
    GameScene remained loaded and IsDirty=false with four roots.

Duplicate/dependency review:
    No equivalent canonical/checksum writer was present before this slice.
    No UID, Command, Snapshot, Aim, AbilitySignal, Checksum, FixedPoint or
    Runtime DTO type was added.
    FrameSyncMoba.Deterministic dependencies and asmdef flags are unchanged.

Remaining limitations:
    This is a primitive writer, not an aggregate schema, reader or checksum.
    UnitUid/Command/Snapshot serializers and SharedGameplayChecksum remain
    future separately reviewed slices.

Scope-external changes:
    None. No asmdef, Package, scene, prefab, ScriptableObject, Input Actions,
    ProjectSettings, production content or non-deterministic module changed.
```
