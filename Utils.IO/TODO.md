# Utils.IO — Current 2.0 backlog

Re-audited on 2026-08-16 against `master` `6bcb7aed0a0afa45b07b82f442511becc5036da4`.

The July audit text had become partially stale after PRs #526 and #528. This file contains only the residual work that is still reproducible in the current implementation. See `docs/releasing/TodoAudit-2026-08-16.md` for repository-wide classification, overlap analysis and PR sequencing.

## P0

### IO-01 — Base alphabet power-of-two validation is incorrect

`BaseDescriptorBase` repeatedly right-shifts the alphabet length until it reaches one. Non-powers of two such as 3, 5, 6 or 7 therefore pass and produce `BitsWidth = floor(log2(length))`.

**Fix:** validate a real power of two (`length > 1 && (length & (length - 1)) == 0`) and test all lengths 0–257.

## P1

### IO-02 — Base descriptor invariants are incomplete

Duplicate alphabet symbols currently fail incidentally through `ToDictionary`, while alphabet/separator/filler overlap and `FillerMod` consistency are not validated explicitly.

**Fix:** one deterministic descriptor validator covering uniqueness, separator/filler collisions and padding configuration.

### IO-03 — Extended numeric types do not honor `BigEndian`

Primitive integer/floating converters honor `BigEndian`, but `BigInteger`, `Int128`, `UInt128` and `Guid` currently use fixed/framework layouts.

**Fix:** first define the 2.0 wire contract, then add big/little-endian golden vectors. Changing existing representations is a wire-format break and must be documented.

### IO-04 — Length-prefixed parsing is bounded only when callers opt in

PR #526 added `RawReader.MaximumLength` and negative/over-limit validation before allocation. The old finding claiming that lengths are passed directly to `ReadBytes` is therefore obsolete. The residual risk is that the default is `int.MaxValue` and aggregate/generated readers need one coherent budget policy.

**Fix:** define safe parsing options/budgets and propagate them consistently before allocation or count×element-size arithmetic.

### IO-06 — `StreamCopier` target validation is incomplete

`Add`/`Insert` reject null, but constructors copy entries without per-target validation and the indexer setter accepts null. No common insertion path defines whether non-writable targets are rejected at registration time.

**Fix:** central target validation for constructors, `Add`, `Insert` and indexer replacement; decide and document the `CanWrite` policy.

## P2

### IO-05 — Strict base decoding is not transactional

`BaseDecoderStream` emits decoded bytes as input arrives and validates final padding/completeness in `Close()`. A malformed final quantum can therefore be reported after the destination has already changed.

This is not automatically a streaming-decoder bug. **Decision required:** document non-atomic semantics as the contract, or add a separate bounded staging/transactional API for callers that need atomic decode.

### IO-07 — `StreamCopier` post-dispose semantics are only partially coherent

Write and flush paths now throw after disposal and dispose is idempotent, but `CanWrite` remains `true` and list mutation/inspection does not consistently express the disposed lifetime.

**Fix:** define whether the target list remains inspectable/mutable after disposal and make capability/operational members consistent with that decision.

### IO-08 — Duplicate `StreamCopier` targets have no explicit contract

The same `Stream` reference can be registered more than once, causing repeated writes, flushes and owned disposal.

**Decision required:** reject duplicate references by identity or document weighted fan-out explicitly.

### IO-09 — `PartialStream.Flush` is outside the shared operation gate

PR #526 replaced `lock(baseStream)` with a `ConditionalWeakTable<Stream, SemaphoreSlim>` gate shared by slices and synchronized read/write/position/seek/length state. The old external-lock and unsynchronized-state findings are therefore largely fixed. `Flush`/`FlushAsync` still bypass that coordination policy.

**Fix:** either gate flush operations as well or document why they are intentionally outside slice-state synchronization.

### IO-10 — `DateTime` serialization loses `Kind`

`RawWriter.WriteDateTime` writes `Ticks`; `RawReader.ReadDateTime` calls `new DateTime(ticks)`, producing `DateTimeKind.Unspecified`.

**Fix:** choose `ToBinary`/`FromBinary` or a documented canonical kind. A representation change is a wire-format compatibility decision.

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

- Old item 6 — unpadded incomplete final groups: fixed by PR #528; current `BaseDecoderStream.Close()` rejects invalid terminal quanta.
- Old item 9 — locking on externally visible `baseStream`: fixed by PR #526 with a shared per-base-stream `SemaphoreSlim`.
- Old item 10 — unsynchronized `Position`/`Seek`/`SetLength`: fixed for state transitions by PR #526; only the flush-policy residual remains as IO-09.
- Old item 14 — owned-target disposal stops on the first exception: current `StreamCopier` attempts every target and aggregates failures.

## Recommended implementation order

1. IO-01 + IO-02 — descriptor correctness and validation.
2. IO-03 + IO-04 + IO-10 + IO-11 — serialization/wire contract hardening.
3. IO-06 + IO-07 + IO-08 + IO-09 + IO-12 + IO-13 — stream lifecycle/API cleanup.
4. IO-05 only after explicitly choosing whether a transactional decoder API is required.
