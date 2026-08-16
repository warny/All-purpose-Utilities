# Repository-wide TODO audit — 2026-08-16

Reference commit: `6bcb7aed0a0afa45b07b82f442511becc5036da4` (`master`).

This audit was performed against the current code, not against the wording of historical audit files. It follows the 2.0 immutability/API work merged through PRs #533–#539.

## Scope

Nine structured TODO Markdown files were found and 127 numbered findings were reviewed:

- `Utils.IO/TODO.md` — 16 items.
- `Utils.Collections/TODO.md` — 14 items.
- `Utils.Collections/TODO-2026-07-11-pass2.md` — 11 items.
- `Utils.NumberToString/TODO.md` — 15 items (47–61).
- `Utils.NumberToString/TODO-2026-07-11-pass2.md` — 13 items (62–74).
- `Utils.NumberToString/TODO-2026-07-11-pass4.md` — 10 items (86–95).
- `Utils.Net/TODO-2026-07-17-pass5.md` — 14 items (30–43).
- `Utils.Net/TODO-2026-07-19-pass6.md` — 15 items (44–58).
- `Utils.Fonts/TODO-2026-07-19-pass2.md` — 19 items (21–39).

Inline `TODO`/`FIXME`/`HACK`/`XXX` searches were reviewed separately. Search hits are noisy (documentation, tests, historical audit text, ordinary identifiers); no independent inline marker was promoted into this backlog without a reproducible current-code defect.

## Fully completed historical TODOs

### Utils.Fonts pass 2

`Utils.Fonts/TODO-2026-07-19-pass2.md` already records that items 21–39 were addressed on 2026-08-04, including review follow-ups. It is historical completion material, not an active TODO.

### Utils.Net pass 5 and pass 6

Both Net audit files carry explicit per-item resolutions. The lifecycle, framing, ARP/DNS/NTP and server/client issues described there are no longer an active backlog. They should be kept as DONE history rather than returned by TODO inventory.

### Utils.NumberToString items 47–61

The current main TODO is stale as an active filename: all numbered findings 47–61 are marked resolved and the current code contains the corresponding fixes (transactional registration, presence-aware inheritance, variant validation/precedence, signed-boundary handling, regex timeout, etc.). Remaining NumberToString work comes from the older pass-2/pass-4 files and is consolidated below.

## Active findings

| ID | Priority | Component | Category | Current finding | Proposed correction | Breaking/wire risk |
|---|---|---|---|---|---|---|
| IO-01 | P0 | BaseDescriptorBase | parsing/validation | Power-of-two validation accepts non-powers of two because it repeatedly shifts the length until one. | Validate `length > 1 && (length & (length - 1)) == 0`; test lengths 0–257. | Constructor behavior only; invalid descriptors start failing. |
| IO-02 | P1 | BaseDescriptorBase | parsing/validation | Alphabet/filler/separator invariants are not explicit; duplicate alphabet failures are incidental and separator/alphabet overlap can make encoded data undecodable. | Central invariant validation, deterministic `ArgumentException`, validated `FillerMod`. | Invalid custom descriptors start failing. |
| IO-03 | P1 | RawReader/RawWriter | serialization | `BigEndian` is ignored by `BigInteger`, `Int128`, `UInt128` and `Guid` payloads. | Define one wire contract and add cross-endian golden vectors. | **Yes: wire compatibility** if existing layouts change. |
| IO-04 | P1 | RawReader | security/robustness | Length-prefixed strings/BigInteger now use `MaximumLength`, but the default is `int.MaxValue`; safe limits remain opt-in and generated/aggregate reads need the same budget model. | Define a bounded parsing policy/options contract and apply it consistently before allocation. | Possible API/configuration change. |
| IO-05 | P2 | BaseDecoderStream | robustness | Strict validation is terminal and non-transactional: malformed input may have already modified the destination before `Close()` rejects it. | Document non-atomic semantics and/or add an explicit validation/staging API; do not pretend ordinary streaming decode is transactional. | New API only if transactional mode is added. |
| IO-06 | P1 | StreamCopier | resources/validation | Constructor entries and indexer setter are not centrally validated; null/non-writable targets can fail after earlier targets have received data. | One validation helper used by every insertion/replacement path; decide registration-time `CanWrite` policy. | Invalid target registrations start failing earlier. |
| IO-07 | P2 | StreamCopier | resources/Dispose | Write/flush now reject disposed instances, but `CanWrite` remains true and list inspection/mutation does not consistently enforce the disposed lifetime. | Define post-dispose list semantics; make capabilities and operational mutations coherent. | Behavioral tightening. |
| IO-08 | P2 | StreamCopier | robustness | Duplicate target references are accepted and cause duplicate write/flush/dispose operations. | Explicitly reject by reference identity or document weighted fan-out. | Design decision / behavior change if rejected. |
| IO-09 | P2 | PartialStream | concurrency | Read/write/seek/position/length now share a per-base-stream gate, fixing the old external-lock defect; `Flush`/`FlushAsync` remain outside that coordination policy. | Either include flush in the shared gate or explicitly document it as outside slice-state coordination. | No public signature change. |
| IO-10 | P2 | RawReader/RawWriter | serialization | `DateTime` writes ticks and reconstructs `Unspecified`, losing `Kind`. | Choose `ToBinary`/`FromBinary` or a documented canonical kind. | **Yes: wire behavior** if representation changes. |
| IO-11 | P2 | RawReader | parsing/validation | Boolean decoding accepts bytes 2–255 as false. | Strictly accept 0/1, or expose permissive mode explicitly. | Malformed inputs start failing. |
| IO-12 | P2 | BaseEncoderStream | API/validation | `maxDataWidth`/`indent` are not validated; wrapping still emits a separator immediately after a full final line. | Validate constructor values and emit separators between lines, not after final output. | Text output can change at exact wrap boundaries. |
| IO-13 | P2 | BaseEncoderStream | API contract | Unsupported operations still throw `InvalidOperationException` instead of `NotSupportedException`. | Align with `Stream` contract and capability properties. | Exception-type behavior change. |
| COL-01 | P1 | SkipList<T> | bug/API contract | Comparer-equal duplicates are still inserted. | Introduce one traversal result / `TryAdd`; decide whether `ICollection<T>.Add` throws or is idempotent. | **Yes: collection semantics**. |
| COL-02 | P1 | SkipList<T> | concurrency | Lookups intentionally mutate upper levels, but concurrent readers can race while publishing links. | Prefer explicit single-threaded contract unless synchronized adaptive promotion is required. | Documentation-only if single-threaded; implementation cost otherwise. |
| COL-03 | P1 | SkipList<T> | enumeration | Bottom-chain mutations do not version/invalidate enumerators. | Add content versioning; upper-level lookup promotion should not invalidate if proven not to alter bottom enumeration. | Expected .NET collection behavior tightening. |
| COL-04 | P1 | SkipList<T> | exception safety | Promotion can happen before a user comparer throws, leaving a failed lookup with hidden structural side effects. | Defer promotion until required comparisons succeed, or explicitly document/test best-effort maintenance. | Internal behavior. |
| COL-05 | P1 | SkipList<T> | tests/robustness | No invariant checker exists for reciprocal links, level ordering, boundaries, count and uniqueness. | Internal validator exposed to tests; model/property tests after every operation. | None. |
| COL-06 | P1 | SkipListDictionary | validation | Runtime null keys are not rejected at public boundaries despite `K : notnull`. | Central runtime null guard for all key operations. | Null inputs fail deterministically. |
| COL-07 | P1 | SkipList/Dictionary/views | API contract | `CopyTo` methods write before validating null/index/capacity, allowing incidental exceptions/partial copies. | Shared preflight validation helper. | Exception types become contract-compliant. |
| COL-08 | P2 | SkipListDictionary | performance | `Add` does `Contains` then `Add`, causing two adaptive traversals. | Reuse the `TryAdd` primitive from COL-01. | None. |
| COL-09 | P2 | SkipList<T> | API/diagnostics | Threshold docs and `counter > threshold` semantics are ambiguous/off-by-one; constructor still reports an unrelated density error. | Define exact threshold meaning, fix condition/docs/message, shape tests for small thresholds. | Potential shape/performance change. |
| COL-10 | P2 | SkipList/Dictionary | API completeness | Comparer is not exposed; dictionary `Keys`/`Values` allocate a wrapper per getter and live-view semantics are undocumented. | Expose comparer properties; cache wrappers; document live views. | New API only. |
| COL-11 | P2 | Utils.Collections project | nullability/package | Nullable analysis is not enabled; preview features and `LangVersion=Latest` are enabled without a demonstrated need. | Enable nullable with warning cleanup; pin language version and remove preview opt-in if unused. | Build/source warnings; package metadata may change. |
| COL-12 | P2 | Utils.Collections tests/docs | tests/documentation | Tests still need deterministic seeds/invariant coverage and README still describes a probabilistic skip list / incomplete dictionary contract. | Deterministic model/stress tests and documentation aligned with the adaptive deterministic design. | None. |
| NTS-01 | P1 | NumberToString config loader | parsing/validation | Published XSD is not used by a schema-validating `XmlReader`; deserialization alone does not enforce the schema. | Secure XSD validation before semantic model validation; line/position diagnostics. | Previously tolerated invalid XML can fail. |
| NTS-02 | P1 | NumberToString static initialization | availability | All built-in locale XML is initialized in one static constructor; one invalid resource can poison the type with `TypeInitializationException`. | Validate resources in CI and isolate/aggregate initialization failures through an explicit bootstrap/validated registry. | May add API; runtime failure semantics change. |
| NTS-03 | P2 | Composite number rendering | architecture | Composite conversions still have a historical risk of finalizing already-finalized subparts or applying finalization inconsistently across assembled phrases. | Before refactoring, add a non-idempotent-finalizer regression matrix; if reproduced, separate raw fragment generation from one final phrase render. | Potential output changes; prove with tests first. |
| NTS-04 | P3 | Units/connectors/month forms | extensibility | Surrounding unit/connector text is not fully variant-aware; this is a linguistic-model limitation rather than a generic correctness bug. | Treat as an explicit design feature only if a target language requires it; do not refactor pre-emptively. | Likely public configuration-model extension. |

## Items already fixed or superseded during this audit

- `Utils.IO` old item 6 (strict incomplete unpadded terminal groups): fixed by PR #528 and present in `BaseDecoderStream.Close`.
- `Utils.IO` old item 9 (locking on externally visible base stream): superseded by the shared `ConditionalWeakTable<Stream, SemaphoreSlim>` gate introduced by PR #526.
- `Utils.IO` old item 14 (dispose stops after first owned target failure): current `StreamCopier` attempts every target and aggregates failures.
- `Utils.Collections` first-pass duplicate/second-pass duplicate findings are the same root issue and are represented once as COL-01.
- `Utils.Collections` concurrent-read findings across both passes are represented once as COL-02.
- `Utils.Collections` enumeration findings across both passes are represented once as COL-03.
- Mutable comparer-relevant key state is the normal restriction of sorted/indexed collections; document it, do not attempt to snapshot arbitrary user objects.
- NumberToString pass-4 item 92 is superseded: presence-aware `LanguageDefinition`/`Optional<T>` models now distinguish absent values from explicit defaults during `baseOn` inheritance.
- NumberToString items 47–64 and 66–73 are resolved or explicitly documented in the current TODO history.

## Intentional behavior / false positives worth retaining

- Lookup-driven upper-level promotion in `SkipList<T>` is intentional. The defect is the missing concurrency/enumeration contract, not the fact that a lookup can optimize the index.
- Deterministic adaptive skip indexing is intentional; do not replace it with randomized levels merely to match textbook terminology.
- `SkipListDictionary.Keys`/`Values` are intended to be live views. The improvement is to cache read-only wrappers and document the live contract, not convert them to snapshots.
- `PartialStream` intentionally leaves ownership of the base stream to the caller.
- `StreamCopier` fan-out is explicitly non-transactional. Do not claim all-or-nothing write semantics.
- NumberToString sub-second truncation is documented behavior, not an open correctness bug.
- Historical Zulu/Arabic/Greek/Slavic agreement limitations are language-model limitations; they are not regressions to fix without a concrete supported-language requirement.

## Proposed PR sequence

1. **Fix base descriptor invariants and strict base-format construction** — IO-01, IO-02. Independent, highest-risk wire-format correctness; exhaustive descriptor construction tests.
2. **Harden Utils.IO serialization contracts** — IO-03, IO-04, IO-10, IO-11. Golden vectors, bounds and malformed-input tests. Decide wire compatibility up front and update `MigrationTo2.0.md` / API allowlist only for actual public API breaks.
3. **Finish stream lifecycle and validation contracts** — IO-06, IO-07, IO-08, IO-09, IO-12, IO-13; keep IO-05 as a documented design decision unless a transactional API is explicitly chosen.
4. **Enforce SkipList key uniqueness with one insertion traversal** — COL-01, COL-06, COL-08. Add `TryAdd`, duplicate/comparer-equivalent/null-key tests. This is the foundation for later invariants.
5. **Make SkipList mutation/enumeration invariants explicit** — COL-02, COL-03, COL-04, COL-05. Add internal invariant checker first, then versioning and the chosen single-thread/concurrent-read policy.
6. **Complete collection contracts and package documentation** — COL-07, COL-09, COL-10, COL-11, COL-12. CopyTo preflight, threshold semantics, comparer exposure, nullable/project settings, deterministic tests and README.
7. **Validate NumberToString XML before construction** — NTS-01. Secure schema validation + semantic validation, built-in resource CI tests.
8. **Isolate NumberToString built-in initialization failures** — NTS-02. Depends on PR 7 so initialization diagnostics are trustworthy.
9. **NumberToString composite phrase proof/refactor** — NTS-03, and NTS-04 only if a concrete language requirement justifies the model extension. Start with tests that prove a defect; avoid speculative refactoring.

## Acceptance policy for follow-up PRs

Each correction PR must contain behavioral regression tests, update the relevant TODO entry, keep unrelated cleanup out of scope, and explicitly call out wire/API changes. For 2.0 breaks, update `eng/api-breaking-changes/2.0.0.json` only when ApiCompat reports an intentional public break, and add the matching migration text.
