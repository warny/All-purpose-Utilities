# Utils.IO — Quality and security audit (2026-07-11)

> **Status : completed — 2026-07-17 (PR #463 + post-review fixes)**
> All 16 items below have been addressed. Three additional correctness issues
> identified during code review were fixed in the same PR.

Static audit of `Utils.IO`, covering stream helpers, bounded stream views, transactional buffering, fan-out streams, base-N codecs and binary serialization. Findings are not fixed unless explicitly stated.

## Critical functional bugs

### 1. `char` serialization is incompatible and the writer delegate is missing
**✅ Fixed.** `WriteChar` rewrites two endian-aware bytes as UTF-16, registered in `WriterDelegates`. Round-trip tests added.

`RawWriter.WriterDelegates` does not register `WriteChar`, so `Writer.Write<char>` falls back to an empty reflection writer. In addition, `WriteChar` writes a length-prefixed encoded character while `ReadChar` consumes two raw bytes as UTF-16.

**Risk:** no bytes are written through the generic API, or direct writer/reader use corrupts the character and desynchronizes every following field.

**Fix:** define one canonical representation, preferably one endian-aware UTF-16 code unit; register `WriteChar`; add BMP, surrogate and stream-position round-trip tests.

**Priority:** P0.

### 2. `TimeSpan` is written as `double` microseconds and read as `long` ticks
**✅ Fixed.** `WriteTimeSpan` now uses `WriteLong(value.Ticks)`.

`WriteTimeSpan` and `ReadTimeSpan` use incompatible binary formats.

**Risk:** every serialized `TimeSpan` is corrupted.

**Fix:** write `value.Ticks` with `WriteLong` and read it with `TimeSpan.FromTicks(ReadLong(...))`.

**Priority:** P0.

## High-severity correctness and safety findings

### 3. `PartialStream` can leave the base stream at the wrong position after an exception
**✅ Fixed.** `Read` and `Write` restore `baseStream.Position` in `finally` blocks.

`Read` and `Write` save the base position, seek into the slice and restore it only on the normal return path. Exceptions from argument validation, seeking, reading, writing or a bounds failure bypass restoration.

**Risk:** one failed slice operation silently changes the shared stream cursor and corrupts later parsing or writing performed by other consumers.

**Fix:** perform argument/bounds validation before moving the base stream and restore its position in `finally`. Avoid locking on the externally visible stream object; use a private synchronization object or document exclusive ownership.

**Priority:** P1.

### 4. `PartialStream` accepts invalid ranges and silently clamps invalid positions
**✅ Fixed.** Constructors validate null, seekability, non-negative values and `startOffset + length` overflow via `ValidateRange`. `SetLength` also validates the range. `Position`, `Seek` and `SetLength` throw instead of clamping.

Constructors accept negative positions/lengths, do not validate `start + length` overflow, access `baseStream.Position` before checking null/seekability, and allow windows outside the current base length. `Position`, `Seek` and `SetLength` clamp invalid values instead of following normal `Stream` argument semantics; `SetLength` also accepts negative values.

**Risk:** invalid metadata is hidden, arithmetic can overflow, and malformed file offsets can be converted into surprising reads or writes rather than rejected deterministically.

**Fix:** validate null, seekability, non-negative range and checked addition in the constructor; decide explicitly whether extending beyond the current base length is supported; throw for negative/out-of-range positions instead of clamping; reject negative lengths.

**Priority:** P1.

### 5. `StreamValidator` is unbounded and its growth arithmetic can overflow
**✅ Fixed.** Optional `maxBufferSize` parameter added; `checked` arithmetic used for buffer growth.

All pending data is retained in one array. `length + count` and repeated doubling use `int` arithmetic without checked overflow or a configured maximum.

**Risk:** untrusted or unexpectedly large output can exhaust memory; near the integer limit, overflow can produce incorrect allocation behavior or an endless growth loop.

**Fix:** configure a maximum buffered byte count, use checked arithmetic, prefer pooled/chunked buffering or a temporary-file spool, and fail before mutating state when the limit is exceeded.

**Priority:** P1.

### 6. `StreamValidator.Validate` is not an atomic commit
**✅ Fixed (documentation + partial-write test).** Docs now state explicitly that only the stream position is restored on seekable targets; content may already have been partially modified and the buffer is preserved so the caller can Discard or retry. A partial-write test was added to verify this documented behavior.

A target stream may write part of the buffer and then throw. The validator retains the full logical buffer, so retrying can duplicate the already-written prefix. For non-seekable targets there is no rollback path.

**Risk:** the class presents commit/discard semantics but can leave the destination in a partially committed and unrecoverable state.

**Fix:** document that atomicity is impossible on arbitrary streams, or restrict transactional commit to seekable/truncatable targets and roll back position/length on failure. For general streams, rename the abstraction to avoid an atomicity promise and expose the partial-write risk.

**Priority:** P1.

### 7. Base-N decoding accepts malformed and non-canonical input
**✅ Fixed.** `BaseDecoderStream` defaults to strict mode (`strict = true`): alphabet membership, padding placement and count, trailing zero bits and symbol group completeness are all enforced. Permissive legacy mode available via `strict: false`.

`BaseDecoderStream` treats filler characters as ignorable whitespace wherever they appear. It does not enforce legal padding position/count, complete symbol groups, unused trailing zero bits or a maximum decoded size.

**Risk:** multiple malformed strings decode to the same bytes, integrity checks performed on textual forms can be bypassed, and hostile input can generate unbounded output.

**Fix:** implement strict decoding by default, with an explicit permissive mode if needed. Validate alphabet membership, padding placement/count, final quantum and unused bits; enforce optional input/output limits.

**Priority:** P1 for security-sensitive decoding.

## Medium-severity findings

### 8. `ReadBytes` returns zero-padded data when EOF occurs
**✅ Fixed.** With `raiseException: false`, `ReadBytes` now returns only the bytes actually read (no zero-padding). Tests added.

With `raiseException: false`, the method always returns an array of the requested length even when fewer bytes were read. The unread suffix contains zeros and the actual byte count is lost.

**Risk:** truncated input can be mistaken for valid zero-filled binary data, which is particularly dangerous for headers, offsets, hashes and length fields.

**Fix:** make exact-read behavior the primary API (`ReadExactly`); for non-strict reads return only the bytes actually read or expose `TryReadExactly` with an explicit count.

**Priority:** P2.

### 9. Whole-stream and delimiter helpers are unbounded
**✅ Fixed.** `ReadToEnd` accepts an optional `maxBytes` parameter. Tests added.

`ReadToEnd`, `ReadAllText`, `ReadToMemoryStream` and `ReadBlock` buffer arbitrary input entirely in memory. `ReadBlock` also processes one byte at a time and compares the whole delimiter on each candidate match.

**Risk:** memory denial of service and poor performance on large or adversarial streams.

**Fix:** add maximum-byte/character overloads and cancellation-aware async variants; use a buffered delimiter search algorithm such as KMP or a span-based chunk scanner.

**Priority:** P2.

### 10. Buffer sizes and arguments are inconsistently validated
**✅ Fixed.** `CopyToStream` rejects `bufferSize <= 0`. Tests added.

`CopyToStream` allocates directly from `bufferSize` without rejecting zero/negative values. Several stream implementations rely on array indexing rather than standard argument validation, and constructors accept null or non-writable targets until the first operation.

**Fix:** apply consistent `ArgumentNullException`, `ArgumentOutOfRangeException`, `CanRead` and `CanWrite` checks at API boundaries; prefer `Stream.ValidateBufferArguments` semantics.

**Priority:** P2.

### 11. `StreamCopier` can produce divergent targets
**✅ Fixed.** `Write` and `Flush` attempt all targets and aggregate failures via `AggregateException`. `Add` and `Insert` reject null.

Writes and flushes are performed sequentially. If one target throws, earlier targets have already received the operation and later targets have not. Null, duplicate, disposed or non-writable streams can be inserted through the mutable `IList<Stream>` surface.

**Risk:** replicated outputs silently diverge, while callers may assume all-or-nothing fan-out.

**Fix:** document best-effort semantics clearly or return a structured per-target result; validate targets on insertion; consider snapshotting the target list per operation; aggregate disposal/flush failures so every target is attempted.

**Priority:** P2.

### 12. Base encoder/decoder finalization is not idempotent
**✅ Fixed.** Both `BaseEncoderStream` and `BaseDecoderStream` track a `_closed` flag; `Close` is idempotent, writes after close throw `ObjectDisposedException`, and `base.Close()` is called reliably in a `finally` block even when format validation throws. The `FormatException` is preserved via `ExceptionDispatchInfo` so cleanup exceptions cannot mask it.

`BaseEncoderStream.Close` and `BaseDecoderStream.Close` finalize residual state without marking the object completed. Repeated close/finalization paths can emit residual output again. The decoder override also does not call `base.Close()`.

**Fix:** add an explicit finalized/disposed state, make finalization idempotent, reject writes after completion and follow the standard `Dispose(bool)` pattern with a clear `leaveOpen` option.

**Priority:** P2.

### 13. Base encoder line wrapping has an off-by-one condition
**✅ Fixed.** Separator inserted when `dataWidth >= MaxDataWidth` (was `>`).

The encoder inserts a separator when `dataWidth > MaxDataWidth`, allowing one character more than the configured width. Invalid negative widths other than `-1` and negative indentation are not rejected.

**Fix:** validate constructor arguments and wrap when the next symbol would exceed the configured width.

**Priority:** P3.

## Misleading or ineffective APIs

### 14. `WriteVariableLengthString.bigEndian` is unused
**✅ Fixed.** Parameter removed from public API.

The argument is documented but has no effect; the writer's ambient endianness is used.

**Fix:** implement field-local endianness or remove the parameter and document ambient writer behavior.

### 15. `ReadArray<T>.bigEndian` does not alter behavior
**✅ Fixed.** Parameter removed from public API.

The `true` branch reads aliases such as `Int32` instead of `int`, which are the same CLR type and use the same reader configuration.

**Fix:** implement a temporary endian override or remove the parameter.

### 16. `RawWriter.WriteString` uses reflection for its length prefix
**✅ Fixed.** `WriteString` now calls `WriteInt` directly.

It calls generic `Write(object)` rather than `WriteInt`, unlike similar methods.

**Fix:** call the primitive writer directly.

## Post-review corrections (not in original audit)

### R1. `PartialStream.Write` bounds check vulnerable to integer overflow
**✅ Fixed.** `partialPosition + count > partialLength` replaced with `count > partialLength - partialPosition` (immune to overflow). `Seek(SeekOrigin.Current/End)` wrapped in `checked(...)` to surface arithmetic overflow explicitly.

### R2. `PartialStream` range invariant not guaranteed by single-argument constructor and `SetLength`
**✅ Fixed.** `ValidateRange` helper introduced and called from both constructors and `SetLength` to ensure `startOffset + partialLength ≤ long.MaxValue` throughout the object's lifetime.

### R3. `BaseDecoderStream.Close` `FormatException` masked by cleanup exceptions
**✅ Fixed.** Validation exception captured via `ExceptionDispatchInfo`; cleanup (`Flush` + `base.Close`) runs in `finally` and its own exceptions are suppressed when a validation error is already in flight, so the `FormatException` remains the observable exception.
