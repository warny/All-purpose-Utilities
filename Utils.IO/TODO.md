# Utils.IO — Quality and security re-audit (2026-07-19)

This audit reviews the current `Utils.IO` implementation after the items recorded in `DONE-2026-07-17.md` were completed. The findings below are new residual issues, regressions, or contracts that remain insufficiently enforced. No production code is changed by this document.

## Critical priority

### 1. Base alphabet power-of-two validation accepts invalid alphabet sizes

`BaseDescriptorBase` repeatedly shifts the alphabet length until it reaches one and then checks whether the final value equals one. This accepts every length whose highest set bit eventually shifts to one, including 3, 5, 6, 7, 9, and many other non-powers of two.

`BitsWidth` is therefore computed as `floor(log2(length))`. The encoder mask can address only `2^BitsWidth` symbols, while the decoder can return larger symbol values from the reverse dictionary. The configured alphabet and the wire format no longer describe the same code.

**Fix:** validate with a true power-of-two test, for example `length > 1 && (length & (length - 1)) == 0`, or explicitly decide whether a one-symbol alphabet is meaningful. Add exhaustive constructor tests for lengths 0 through 257.

**Priority: P0 — wire-format correctness.**

## High priority

### 2. Length-prefixed reads trust unbounded, attacker-controlled lengths

`RawReader.ReadString` and `ReadBigInteger` read a signed 32-bit length and pass it directly to `ReadBytes`. Negative values produce incidental argument failures, while large positive values can allocate or buffer arbitrary amounts of memory. The same format is used by generated readers and by callers parsing untrusted binary files.

**Fix:** define configurable maximum lengths on the reader or per operation; reject negative values with a format-specific exception before allocation; use checked arithmetic for element-count × element-size operations; distinguish malformed input from ordinary end-of-stream.

**Priority: P1 — memory denial of service.**

### 3. `BigEndian` is ignored for `BigInteger`, `Int128`, `UInt128`, and `Guid`

The primitive integer delegates honor `BigEndian`, but `WriteBigInteger`/`ReadBigInteger`, `WriteInt128`/`ReadInt128`, `WriteUInt128`/`ReadUInt128`, and GUID serialization use fixed helper or framework byte layouts. Consumers cannot infer from the API which numeric types obey the configured byte order.

**Fix:** either make `BigEndian` apply consistently to every numeric representation, or explicitly define these values as fixed-format exceptions in names and documentation. Add cross-endian golden-vector tests rather than only same-implementation round trips.

**Priority: P1 — interoperability.**

### 4. Decoder validation occurs after bytes have already been committed

`BaseDecoderStream.Write` immediately writes decoded bytes to the target stream. Padding count, incomplete final groups, and non-zero trailing bits are validated only in `Close`. Malformed input can therefore modify the destination before throwing `FormatException`.

**Fix:** document the decoder as non-transactional and recommend decoding into a bounded staging buffer, or add a validation-first/transactional API for security-sensitive callers. When the target is seekable and truncatable, optionally restore both position and length on format failure.

**Priority: P1.**

### 5. Descriptor invariants do not prevent ambiguous alphabets

The descriptor constructor does not explicitly validate duplicate alphabet characters, overlap between alphabet and separator characters, overlap between alphabet and filler, filler/separator overlap, or the relationship between `Filler` and `FillerMod`. Duplicate alphabet entries currently fail through `ToDictionary` with an incidental exception; overlap with separators is worse because the decoder silently ignores a character that the encoder may emit as data.

**Fix:** validate all descriptor invariants explicitly and throw one deterministic `ArgumentException` identifying the conflict. Require a positive, format-compatible `FillerMod` whenever padding is configured and zero when it is not.

**Priority: P1 — canonical decoding.**

### 6. Strict decoding does not define completeness for unpadded encodings

Final completeness is primarily enforced through expected filler count. Encodings without a filler, such as the built-in base-16 descriptor, do not have an explicit legal-symbol-count rule. A trailing half-byte or another incomplete quantum must be rejected based on the encoding's byte alignment, independently of padding.

**Fix:** derive the legal final symbol counts from `BitsWidth` and the least common multiple of 8 and `BitsWidth`; reject incomplete terminal groups even when `Filler` is null. Add strict tests for every possible truncated suffix length for base 16, 32, and 64.

**Priority: P1.**

### 7. `StreamCopier` still permits invalid targets through constructors and the indexer

`Add` and `Insert` reject null, but the array constructor copies entries without validating each target and the public indexer accepts null. No insertion path verifies `CanWrite`. A null or read-only target therefore fails only during a later broadcast, after earlier targets may already have received data.

**Fix:** centralize target validation and apply it in constructors, `Add`, `Insert`, and the indexer setter. Decide whether disposed/non-writable targets are rejected at registration or treated as runtime fan-out failures.

**Priority: P1.**

### 8. `StreamCopier` remains operational after disposal

The overrides do not track a disposed state. When `closeAllTargetsOnDispose` is false, `Write`, `Flush`, collection mutation, and `CanWrite` continue to behave normally after `Dispose`. This violates normal `Stream` lifetime expectations and permits accidental reuse of an object whose ownership phase has ended.

**Fix:** track disposal, return `false` from capability properties after disposal where appropriate, and throw `ObjectDisposedException` from operational and mutating members. Define whether read-only inspection of the target list remains available.

**Priority: P1 — resource lifecycle.**

### 9. `PartialStream` locks on an externally visible object

Read and write operations use `lock (baseStream)`. External code can lock the same stream in another order, causing lock inversion or deadlock. The original audit recommended a private synchronization object, but the current implementation still uses the base stream as the monitor.

**Fix:** use a private gate shared by all slices that coordinate on the same base stream, or require exclusive ownership and remove the thread-safety claim. A per-instance gate alone is insufficient when several `PartialStream` instances wrap the same stream.

**Priority: P1 — concurrency.**

### 10. `PartialStream` state changes are not consistently synchronized

`Read` and `Write` update `partialPosition` while holding the base-stream monitor, but `Position`, `Seek`, and `SetLength` access the same fields without that lock. Concurrent operations can race, lose position updates, or validate against a stale length. `Flush` is also outside the stated concurrency discipline.

**Fix:** define the class as non-thread-safe, or protect every state transition with one coherent synchronization policy. Add stress tests combining read/write/seek/resize operations.

**Priority: P1.**

## Medium priority

### 11. `DateTime` serialization loses `Kind`

Only `DateTime.Ticks` is written, and the reader constructs `new DateTime(ticks)`, which returns `DateTimeKind.Unspecified`. UTC and local values therefore do not round-trip semantically even though their clock ticks do.

**Fix:** use `DateTime.ToBinary`/`FromBinary`, or document a canonical UTC/unspecified wire contract and enforce it. Add tests for `Utc`, `Local`, ambiguous local times, and `Unspecified`.

**Priority: P2.**

### 12. Boolean decoding silently normalizes every non-one byte to false

`ReadBool` returns true only for byte `1`; values `2` through `255` are silently accepted as false. Corrupt input is therefore normalized rather than rejected.

**Fix:** accept only `0` and `1` in the strict binary format. If permissive C-style non-zero semantics are needed, expose them explicitly as a separate option.

**Priority: P2.**

### 13. Base encoder configuration is not validated and line wrapping emits trailing formatting

`BaseEncoderStream` accepts `maxDataWidth` values below `-1` and negative indentation. A width of zero or a negative value other than `-1` has surprising behavior. The separator is emitted immediately after reaching the configured width, including when that symbol is the final symbol, producing a trailing separator and indentation.

**Fix:** validate `maxDataWidth == -1 || maxDataWidth > 0` and `indent >= 0`. Decide whether wrapping is a between-lines operation; if so, delay separator emission until the next data symbol rather than appending formatting after the last line.

**Priority: P2.**

### 14. `StreamCopier.Dispose` does not attempt every owned target

When target ownership is enabled, disposal iterates directly without exception aggregation. The first failing target prevents later targets from being disposed and prevents `_targets.Clear()`. This differs from the class's documented best-effort behavior for `Write` and `Flush`.

**Fix:** attempt every target, aggregate failures, clear internal references in `finally`, and make repeated disposal idempotent. Define how duplicate target references are handled to avoid disposing the same stream more than once.

**Priority: P2.**

### 15. Duplicate fan-out targets are accepted without an explicit contract

The same stream can be registered multiple times. Every write, flush, and optionally dispose is then repeated against that same object. This can duplicate bytes, corrupt stateful encoders, and cause repeated disposal.

**Fix:** either reject duplicate references using reference identity, or document duplicates as intentional weighted fan-out. Add tests covering constructor, indexer replacement, and mutation paths.

**Priority: P2.**

### 16. Unsupported stream operations use inconsistent exception types

`BaseEncoderStream.Position` setter, `Read`, `Seek`, and `SetLength` throw `InvalidOperationException`, whereas the standard `Stream` contract uses `NotSupportedException` for unsupported capabilities. This makes generic stream consumers handle the class differently from ordinary write-only streams.

**Fix:** use `NotSupportedException` consistently and ensure `CanRead`/`CanSeek` accurately predict the behavior of every related method.

**Priority: P2.**

## Required regression tests

- Reject every non-power-of-two alphabet size and all alphabet/filler/separator collisions.
- Verify strict terminal-group validation for every truncated base-16/base-32/base-64 suffix.
- Verify malformed encoded input cannot be mistaken for an atomic decode.
- Reject negative and over-limit string/BigInteger/array lengths before allocation.
- Compare big- and little-endian golden vectors for every supported numeric type.
- Round-trip all `DateTimeKind` values and reject invalid boolean bytes.
- Exercise every `StreamCopier` insertion path with null, non-writable, duplicate, and disposed targets.
- Verify `StreamCopier` behavior after disposal and aggregate failures from target disposal.
- Stress concurrent `PartialStream` read/write/seek/resize operations and multiple slices over one base stream.
- Validate encoder width/indent arguments and exact line-wrapping output with and without a full final line.

## Recommended order

| Priority | Action |
|---|---|
| P0 | Correct base alphabet power-of-two validation |
| P1 | Bound all length-prefixed allocations |
| P1 | Define consistent endianness for extended numeric types |
| P1 | Harden strict decoder completeness and descriptor invariants |
| P1 | Clarify transactional decode behavior |
| P1 | Enforce `StreamCopier` target and disposal contracts |
| P1 | Define and implement one `PartialStream` concurrency policy |
| P2 | Preserve `DateTime` semantics and reject malformed booleans |
| P2 | Validate encoder formatting options and standardize exceptions |

## Deployment warning

Until items 1, 2, 4, 5, and 6 are fixed, do not treat custom base descriptors or length-prefixed binary data from untrusted sources as safely validated. Until items 7 through 10 are fixed, do not rely on `StreamCopier` or `PartialStream` as thread-safe or lifecycle-safe coordination primitives.