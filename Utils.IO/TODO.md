# Utils.IO — Current 2.0 backlog

Re-audited on 2026-08-16 against `master` `6bcb7aed0a0afa45b07b82f442511becc5036da4`.

The July audit text had become partially stale after PRs #526 and #528. This file contains only the residual work that is still reproducible in the current implementation. See `docs/releasing/TodoAudit-2026-08-16.md` for repository-wide classification, overlap analysis and PR sequencing.

## P1

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

### IO-10 — Fixed through generic wire-codec infrastructure

DateTime now uses selectable codecs, with .NET binary as the default and built-in ticks, Unix seconds/milliseconds, OLE Automation and FILETIME representations. Codecs may be registered by exact type or overridden per serialized member. Generic framing is forward-only safe and prepares, but does not close, IO-04 reader-budget integration.

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

- IO-03 — fixed: `Int128` and `UInt128` now use fixed-width 16-byte representations following `BigEndian`; `BigInteger` keeps its `Int32` length prefix and uses a signed minimal two's-complement payload following `BigEndian`; `Guid` uses canonical RFC/network byte order independently of `BigEndian`. Golden wire vectors cover reader and writer behavior.
- IO-02 — base descriptor invariants: fixed by validating alphabet uniqueness, reserved-character collisions and padding quantum consistency before constructing lookup tables. Invalid custom descriptors now fail deterministically at construction time.
- IO-01 — base alphabet validation: fixed by requiring lengths from 2 through 256 to be exact powers of two; exhaustive regression coverage checks every length from 0 through 257 and verifies `BitsWidth` for valid alphabets.
- Old item 6 — unpadded incomplete final groups: fixed by PR #528; current `BaseDecoderStream.Close()` rejects invalid terminal quanta.
- Old item 9 — locking on externally visible `baseStream`: fixed by PR #526 with a shared per-base-stream `SemaphoreSlim`.
- Old item 10 — unsynchronized `Position`/`Seek`/`SetLength`: fixed for state transitions by PR #526; only the flush-policy residual remains as IO-09.
- Old item 14 — owned-target disposal stops on the first exception: current `StreamCopier` attempts every target and aggregates failures.

## Recommended implementation order

1. IO-03 + IO-04 + IO-10 + IO-11 — serialization/wire contract hardening using the decisions recorded above.
2. IO-06 + IO-07 + IO-08 + IO-09 + IO-12 + IO-13 — stream lifecycle/API cleanup.
3. IO-05 — documentation and regression tests for the intentionally non-transactional streaming contract; no transactional implementation is currently planned.
