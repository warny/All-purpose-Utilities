# Utils.IO — Current 2.0 backlog

Re-audited on 2026-08-16 against `master` `6bcb7aed0a0afa45b07b82f442511becc5036da4`.

The July audit text had become partially stale after PRs #526 and #528. This file contains only the residual work that is still reproducible in the current implementation. See `docs/releasing/TodoAudit-2026-08-16.md` for repository-wide classification, overlap analysis and PR sequencing.

## P1

### IO-02 — Base descriptor invariants are incomplete

Duplicate alphabet symbols currently fail incidentally through `ToDictionary`, while alphabet/separator/filler overlap and `FillerMod` consistency are not validated explicitly.

**Fix:** one deterministic descriptor validator covering uniqueness, separator/filler collisions and padding configuration.

### IO-03 — Extended numeric wire formats must honor the selected endianness

Primitive integer/floating converters honor `BigEndian`, but `BigInteger`, `Int128` and `UInt128` currently use fixed/framework layouts. `Guid` is not numeric and must not be controlled by the numeric endianness setting.

**Decision (2026-08-16):**

- `Int128` and `UInt128` must support both little-endian and big-endian and follow `BigEndian`, exactly like the other integer types. These APIs are new in 2.0, so there is no legacy 1.x wire format to preserve.
- `BigInteger` must support both little-endian and big-endian payloads so `RawReader`/`RawWriter` can interoperate with external systems. Use a documented signed two's-complement representation and make the byte order follow `BigEndian`.
- `Guid` is an identifier, not a number. Its binary representation must be explicitly standardized and platform-interoperable, and must remain independent of `BigEndian`. Reading and writing must use the same documented standard byte layout.

**Fix:** implement those contracts and add golden byte vectors for both endian modes and for the chosen standard GUID representation. Tests must assert bytes directly, not only writer→reader round-trips.

### IO-04 — Length-prefixed parsing is bounded only when callers opt in

PR #526 added `RawReader.MaximumLength` and negative/over-limit validation before allocation. The old finding claiming that lengths are passed directly to `ReadBytes` is therefore obsolete. The residual risk is that the default is `int.MaxValue` and aggregate/generated readers need one coherent budget policy.

**Fix:** define safe parsing options/budgets and propagate them consistently before allocation or count×element-size arithmetic.

### IO-06 — `StreamCopier` target validation is incomplete

`Add`/`Insert` reject null, but constructors copy entries without per-target validation and the indexer setter accepts null. No common insertion path defines whether non-writable targets are rejected at registration time.

**Fix:** central target validation for constructors, `Add`, `Insert` and indexer replacement; decide and document the `CanWrite` policy.

## P2

### IO-05 — Strict base decoding is intentionally non-transactional

`BaseDecoderStream` emits decoded bytes as input arrives and validates final padding/completeness in `Close()`. A malformed final quantum can therefore be reported after the destination has already changed.

**Decision (2026-08-16):** keep the classical streaming policy. `BaseDecoderStream` is non-transactional: already-emitted decoded bytes are not rolled back if a later or terminal validation error occurs. Do not introduce implicit whole-stream buffering or staging.

**Fix:** document this contract explicitly and add regression tests demonstrating that terminal validation remains strict while previously emitted bytes can remain in the destination. A separate transactional API should only be added in the future for a concrete requirement.

### IO-07 — `StreamCopier` post-dispose semantics are only partially coherent

Write and flush paths now throw after disposal and dispose is idempotent, but `CanWrite` remains `true` and list mutation/inspection does not consistently express the disposed lifetime.

**Fix:** define whether the target list remains inspectable/mutable after disposal and make capability/operational members consistent with that decision.

### IO-08 — Duplicate `StreamCopier` targets have no explicit contract

The same `Stream` reference can be registered more than once, causing repeated writes, flushes and owned disposal.

**Decision required:** reject duplicate references by identity or document weighted fan-out explicitly.

### IO-09 — `PartialStream.Flush` is outside the shared operation gate

PR #526 replaced `lock(baseStream)` with a `ConditionalWeakTable<Stream, SemaphoreSlim>` gate shared by slices and synchronized read/write/position/seek/length state. The old external-lock and unsynchronized-state findings are therefore largely fixed. `Flush`/`FlushAsync` still bypass that coordination policy.

**Fix:** either gate flush operations as well or document why they are intentionally outside slice-state synchronization.

### IO-10 — `DateTime` wire format must be selectable

`RawWriter.WriteDateTime` currently writes `Ticks`; `RawReader.ReadDateTime` reconstructs `new DateTime(ticks)`, which loses `DateTimeKind` and hard-codes one representation even though this library must read formats produced by heterogeneous external systems.

**Decision (2026-08-16):** make the `DateTime` wire representation a strategy rather than hard-coding one encoding. The default in 2.0 is the native .NET binary representation (`DateTime.ToBinary()` / `DateTime.FromBinary()`), because there are currently no released users whose existing wire data must be preserved.

Provide an extensible format interface/strategy and built-in implementations for the main interoperable representations, initially including:

- .NET binary (`ToBinary` / `FromBinary`) — default;
- .NET ticks;
- Unix epoch seconds;
- Unix epoch milliseconds;
- OLE Automation date;
- Windows FILETIME.

Each format must define its exact units/epoch, valid range, `DateTimeKind` semantics and how it uses the primitive reader/writer. Formats backed by integer/floating primitives should naturally inherit `BigEndian` through those primitive operations rather than reimplement byte-order handling.

**Fix:** introduce the strategy contract, select the .NET binary strategy by default, implement the principal formats above, and add external golden vectors plus round-trip/range/kind tests for each format.

### IO-11 — Boolean decoding accepts malformed bytes

`ReadBool` returns `ReadByte(reader) == 1`, so values 2–255 silently become false.

**Fix:** strict 0/1 validation by default; expose permissive semantics only if a real compatibility requirement exists.

### IO-12 — Encoder formatting arguments/output need an explicit contract

`BaseEncoderStream` does not validate `maxDataWidth`/`indent`, and reaching the exact line width writes the separator immediately, including after a full final line.

**Fix:** validate arguments and emit separators between lines rather than after final output if that is the chosen format contract.

### IO-13 — Unsupported `BaseEncoderStream` operations use the wrong exception family

`Position` setter, `Read`, `Seek` and `SetLength` throw `InvalidOperationException` even though `CanRead`/`CanSeek` advertise unsupported stream capabilities.

**Fix:** use `NotSupportedException` consistently.

## Closed since the July audit

- IO-01 — base alphabet validation: fixed by requiring lengths from 2 through 256 to be exact powers of two; exhaustive regression coverage checks every length from 0 through 257 and verifies `BitsWidth` for valid alphabets.
- Old item 6 — unpadded incomplete final groups: fixed by PR #528; current `BaseDecoderStream.Close()` rejects invalid terminal quanta.
- Old item 9 — locking on externally visible `baseStream`: fixed by PR #526 with a shared per-base-stream `SemaphoreSlim`.
- Old item 10 — unsynchronized `Position`/`Seek`/`SetLength`: fixed for state transitions by PR #526; only the flush-policy residual remains as IO-09.
- Old item 14 — owned-target disposal stops on the first exception: current `StreamCopier` attempts every target and aggregates failures.

## Recommended implementation order

1. IO-01 + IO-02 — descriptor correctness and validation.
2. IO-03 + IO-04 + IO-10 + IO-11 — serialization/wire contract hardening using the decisions recorded above.
3. IO-06 + IO-07 + IO-08 + IO-09 + IO-12 + IO-13 — stream lifecycle/API cleanup.
4. IO-05 — documentation and regression tests for the intentionally non-transactional streaming contract; no transactional implementation is currently planned.
