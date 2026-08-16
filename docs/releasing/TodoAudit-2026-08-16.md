# Repository-wide TODO audit — 2026-08-16

Reference commit: `6bcb7aed0a0afa45b07b82f442511becc5036da4` (`master`).

This audit was performed against the current code, not against the wording of historical audit files. It follows the 2.0 immutability/API work merged through PRs #533–#539.

## Scope

Nine structured TODO Markdown files were found, containing 127 numbered findings. The DONE inventory was also checked for misleading open-state markers. `Utils.Fonts/DONE-2026-07-10.md` was the only DONE file returned by an unchecked-checklist search; its intermediate prose still says some proposals are untreated, so all 32 numbered items in its two audit passes were re-read as well. Their later resolution sections confirm that all 32 are completed.

**Total structured findings reviewed: 159.**

TODO inventory at the reference commit:

- `Utils.IO/TODO.md` — 16 items.
- `Utils.Collections/TODO.md` — 14 items.
- `Utils.Collections/TODO-2026-07-11-pass2.md` — 11 items.
- `Utils.NumberToString/TODO.md` — 15 items (47–61).
- `Utils.NumberToString/TODO-2026-07-11-pass2.md` — 13 items (62–74).
- `Utils.NumberToString/TODO-2026-07-11-pass4.md` — 10 items (86–95).
- `Utils.Net/TODO-2026-07-17-pass5.md` — 14 items (30–43).
- `Utils.Net/TODO-2026-07-19-pass6.md` — 15 items (44–58).
- `Utils.Fonts/TODO-2026-07-19-pass2.md` — 19 items (21–39).

Inline `TODO`/`FIXME`/`HACK`/`XXX` searches were reviewed separately. Search hits are noisy (documentation, tests, historical audit text and ordinary identifiers); no independent inline marker was promoted into this backlog without a reproducible current-code defect.

## Fully completed historical TODOs

### Utils.Fonts pass 2

`Utils.Fonts/TODO-2026-07-19-pass2.md` already records that items 21–39 were addressed on 2026-08-04, including review follow-ups. It is historical completion material, not an active TODO.

### Utils.Fonts DONE-2026-07-10 consistency check

The file contains two audit passes (20 + 12 items). An intermediate heading says some proposals were still untreated, and an unchecked-checklist search therefore made the DONE status suspicious. Reading through the complete file resolves the apparent inconsistency: later per-item notes and final summaries mark all functional, debt and test items fixed, including the full `TrueTypeFont.WriteFont()` round-trip test and the defects that test exposed. No Fonts item is reopened.

### Utils.Net pass 5 and pass 6

Both Net audit files carry explicit per-item resolutions. The lifecycle, framing, ARP/DNS/NTP and server/client issues described there are no longer an active backlog. They are archived as DONE history on the audit branch.

### Utils.NumberToString items 47–61

The former main TODO was stale as an active backlog: all numbered findings 47–61 are resolved and the current code contains the corresponding fixes (transactional registration, presence-aware inheritance, variant validation/precedence, signed-boundary handling, regex timeout, etc.). Residual NumberToString work from the older pass-2/pass-4 files is consolidated into the refreshed `Utils.NumberToString/TODO.md`.

## Active findings

| ID | Priority | Component | Category | Current finding | Proposed correction | Breaking/wire risk |
|---|---|---|---|---|---|---|
| IO-01 | P0 | BaseDescriptorBase | parsing/validation | Power-of-two validation accepts non-powers of two because it repeatedly shifts the length until one. | Validate `length > 1 && (length & (length - 1)) == 0`; test lengths 0–257. | Invalid custom descriptors start failing. |
| IO-02 | P1 | BaseDescriptorBase | parsing/validation | Alphabet/filler/separator invariants are not explicit; duplicate alphabet failures are incidental and separator/alphabet overlap can make encoded data undecodable. | Central invariant validation, deterministic argument errors, validated `FillerMod`. | Invalid custom descriptors start failing. |
| IO-03 | P1 | RawReader/RawWriter | serialization | `BigEndian` is ignored by `BigInteger`, `Int128`, `UInt128` and `Guid` payloads. | Define one wire contract and add cross-endian golden vectors. | **Yes: wire compatibility** if existing layouts change. |
| IO-04 | P1 | RawReader | security/robustness | Length-prefixed strings/BigInteger now use `MaximumLength`, but the default is `int.MaxValue`; safe limits remain opt-in and aggregate/generated readers need the same budget model. | Define a bounded parsing policy/options contract and apply it consistently before allocation. | Possible API/configuration change. |
| IO-05 | P2 | BaseDecoderStream | robustness | Strict validation is terminal and non-transactional: malformed input may have already modified the destination before `Close()` rejects it. | Document non-atomic semantics and/or add an explicit bounded staging API. | New API only if transactional mode is added. |
| IO-06 | P1 | StreamCopier | resources/validation | Constructor entries and indexer setter are not centrally validated; null/non-writable targets can fail after earlier targets received data. | One validation helper used by every insertion/replacement path; decide registration-time `CanWrite` policy. | Invalid registrations can fail earlier. |
| IO-07 | P2 | StreamCopier | resources/Dispose | Write/flush reject disposed instances, but `CanWrite` remains true and list mutation/inspection does not consistently express the disposed lifetime. | Define post-dispose list semantics and align capability/operational members. | Behavioral tightening. |
| IO-08 | P2 | StreamCopier | robustness | Duplicate target references are accepted and cause duplicate write/flush/dispose operations. | Reject by reference identity or document weighted fan-out. | Design decision / behavior change if rejected. |
| IO-09 | P2 | PartialStream | concurrency | Read/write/seek/position/length use the shared gate introduced by PR #526; `Flush`/`FlushAsync` remain outside that coordination policy. | Gate flush too, or document why it is intentionally outside slice-state synchronization. | None. |
| IO-10 | P2 | RawReader/RawWriter | serialization | `DateTime` writes ticks and reconstructs `Unspecified`, losing `Kind`. | Choose `ToBinary`/`FromBinary` or a documented canonical kind. | **Yes: wire behavior** if representation changes. |
| IO-11 | P2 | RawReader | parsing/validation | Boolean decoding accepts bytes 2–255 as false. | Strictly accept 0/1, or expose permissive mode explicitly. | Malformed inputs start failing. |
| IO-12 | P2 | BaseEncoderStream | API/validation | `maxDataWidth`/`indent` are not validated; exact final line width still emits a trailing separator. | Validate constructor values and define separator-between-lines formatting. | Text output can change at wrap boundaries. |
| IO-13 | P2 | BaseEncoderStream | API contract | Unsupported operations throw `InvalidOperationException` rather than `NotSupportedException`. | Align with `Stream` capability contract. | Exception-type behavior change. |
| COL-01 | P1 | SkipList<T> | bug/API contract | Comparer-equal duplicates are still inserted. | Introduce one traversal result / `TryAdd`; decide whether `ICollection<T>.Add` throws or is idempotent. | **Yes: collection semantics**. |
| COL-02 | P1 | SkipList<T> | concurrency | Lookups intentionally mutate upper levels, but concurrent readers can race while publishing links. | Prefer an explicit single-threaded contract unless synchronized adaptive promotion is required. | Documentation-only if single-threaded. |
| COL-03 | P1 | SkipList<T> | enumeration | Bottom-chain mutations do not version/invalidate enumerators. | Add content versioning; lookup-only upper-level promotion need not invalidate if proven safe. | Expected collection behavior tightening. |
| COL-04 | P1 | SkipList<T> | exception safety | Promotion can happen before a user comparer throws, leaving a failed lookup with hidden structural side effects. | Defer promotion until required comparisons succeed, or explicitly document/test best-effort maintenance. | Internal behavior. |
| COL-05 | P1 | SkipList<T> | tests/robustness | No invariant checker exists for reciprocal links, level ordering, boundaries, count and uniqueness. | Internal test-visible validator plus model/property tests. | None. |
| COL-06 | P1 | SkipListDictionary | validation | Runtime null keys are not rejected at public boundaries despite `K : notnull`. | Central runtime null guard for all key operations. | Null inputs fail deterministically. |
| COL-07 | P1 | SkipList/Dictionary/views | API contract | `CopyTo` writes before validating null/index/capacity, allowing incidental exceptions/partial copies. | Shared preflight validation helper. | Exception behavior becomes contract-compliant. |
| COL-08 | P2 | SkipListDictionary | performance | `Add` does `Contains` then `Add`, causing two adaptive traversals. | Reuse COL-01's `TryAdd` primitive. | None. |
| COL-09 | P2 | SkipList<T> | API/diagnostics | Threshold docs and `counter > threshold` semantics are ambiguous/off-by-one; constructor reports an unrelated density error. | Define exact threshold meaning, fix condition/docs/message and shape tests. | Potential shape/performance change. |
| COL-10 | P2 | SkipList/Dictionary | API completeness | Comparer is not exposed; `Keys`/`Values` allocate a wrapper per getter and live-view semantics are undocumented. | Expose comparer properties, cache wrappers, document live views. | New API only. |
| COL-11 | P2 | Utils.Collections project | nullability/package | Nullable analysis is not enabled; preview features and `LangVersion=Latest` are enabled without demonstrated need. | Enable nullable; pin language version; remove preview opt-in if unused. | Build/source warnings. |
| COL-12 | P2 | Utils.Collections tests/docs | tests/documentation | Tests need reproducible invariant/model coverage and docs still call the deterministic adaptive structure probabilistic. | Deterministic stress/model tests and contract-accurate README/XML docs. | None. |
| NTS-01 | P1 | NumberToString config loader | parsing/validation | Published XSD is not used by a schema-validating `XmlReader`; deserialization alone does not enforce the schema. | Secure XSD validation before semantic validation; line/position diagnostics. | Previously tolerated invalid XML can fail. |
| NTS-02 | P1 | NumberToString static initialization | availability | All built-in locale XML is initialized in one static constructor; one invalid resource can poison the type with `TypeInitializationException`. | Validate resources in CI and isolate/aggregate initialization failures through an explicit bootstrap/validated registry. | Runtime failure semantics may change. |
| NTS-03 | P2 | Composite number rendering | architecture | Historical evidence suggests some composite conversions can apply finalization at inconsistent layers, but adjacent fixes make the old prose insufficient proof today. | First add a deliberately non-idempotent-finalizer regression matrix; refactor only if reproduced. | Potential output change; prove first. |
| NTS-04 | P3 | Units/connectors/month forms | extensibility | Surrounding unit/connector text is not fully variant-aware; this is a linguistic-model limitation, not a generic correctness bug. | Extend only for a concrete supported-language requirement. | Likely configuration-model extension. |

## Items already fixed or superseded during this audit

- `Utils.IO` old item 6 (strict incomplete unpadded terminal groups): fixed by PR #528 and present in `BaseDecoderStream.Close`.
- `Utils.IO` old item 9 (locking on externally visible base stream): superseded by the shared `ConditionalWeakTable<Stream, SemaphoreSlim>` gate introduced by PR #526.
- `Utils.IO` old item 10 is largely fixed by the same shared gate; only flush-policy consistency remains as IO-09.
- `Utils.IO` old item 14 (dispose stops after first owned target failure): current `StreamCopier` attempts every target and aggregates failures.
- `Utils.Collections` duplicate findings from both passes are represented once as COL-01; concurrent-read duplicates as COL-02; enumeration duplicates as COL-03.
- Mutable comparer-relevant key state is the normal restriction of sorted/indexed collections; document it, do not attempt to snapshot arbitrary user objects.
- NumberToString pass-4 item 92 is superseded: presence-aware `LanguageDefinition`/`Optional<T>` models distinguish absent values from explicit defaults during `baseOn` inheritance.
- NumberToString items 47–64 and 66–73 are resolved or intentionally documented in current history.
- The suspicious Fonts DONE file was fully re-read; its later resolution sections close all 32 items, so no item is reopened from it.

## Intentional behavior / false positives worth retaining

- Lookup-driven upper-level promotion in `SkipList<T>` is intentional. The defect is the missing concurrency/enumeration contract, not the optimization itself.
- Deterministic adaptive skip indexing is intentional; do not introduce random levels just to match textbook terminology.
- `SkipListDictionary.Keys`/`Values` are intended live views. Cache/document the wrappers; do not turn them into snapshots.
- `PartialStream` intentionally leaves ownership of the base stream to the caller.
- `StreamCopier` fan-out is explicitly non-transactional. Do not claim all-or-nothing writes.
- NumberToString sub-second truncation is documented behavior, not an open bug.
- Historical language-agreement limitations are model limitations, not regressions to fix without a concrete supported-language requirement.

## Proposed PR sequence

1. **Fix base descriptor invariants and strict base-format construction** — IO-01, IO-02. Independent and highest-risk correctness; exhaustive descriptor construction tests.
2. **Harden Utils.IO serialization contracts** — IO-03, IO-04, IO-10, IO-11. Golden vectors, bounds and malformed-input tests. Decide wire compatibility first and update migration/API allowlist only for actual public breaks.
3. **Finish stream lifecycle and validation contracts** — IO-06, IO-07, IO-08, IO-09, IO-12, IO-13. Keep IO-05 as a documented design decision unless a transactional API is explicitly chosen.
4. **Enforce SkipList key uniqueness with one insertion traversal** — COL-01, COL-06, COL-08. Add `TryAdd`, comparer-equivalent duplicate and null-key tests.
5. **Make SkipList mutation/enumeration invariants explicit** — COL-02, COL-03, COL-04, COL-05. Add invariant checker first, then versioning and the chosen concurrency contract.
6. **Complete collection contracts and package documentation** — COL-07, COL-09, COL-10, COL-11, COL-12. CopyTo preflight, threshold semantics, comparer exposure, nullable/project settings, deterministic tests and README.
7. **Validate NumberToString XML before construction** — NTS-01. Secure schema validation + semantic validation + all built-in resources in CI.
8. **Isolate NumberToString built-in initialization failures** — NTS-02. Depends on PR 7 for trustworthy diagnostics.
9. **NumberToString composite phrase proof/refactor** — NTS-03, and NTS-04 only with a concrete language requirement. Begin with tests that prove a defect; avoid speculative refactoring.

## Acceptance policy for follow-up PRs

Each correction PR must contain behavioral regression tests, update the relevant TODO entry, keep unrelated cleanup out of scope, and explicitly call out wire/API changes. For 2.0 breaks, update `eng/api-breaking-changes/2.0.0.json` only when ApiCompat reports an intentional public break, and add matching migration text.
