# Runtime Observation and Export Contract

This document defines the contract boundaries for parser runtime observation in `Utils.Parser`.

It intentionally separates:

1. the **runtime observation contract** (what the parser runtime emits), and
2. the **export contract** (how tooling can render/export those observations).

Observation and export remain passive, descriptive, and non-authoritative.

See also:

- [`Utils.Parser/ROADMAP.md`](../../Utils.Parser/ROADMAP.md)
- [`docs/parser/RuntimeStateOwnership.md`](./RuntimeStateOwnership.md)
- [`docs/parser/ParserMetadataAndRuntimeLimitations.md`](./ParserMetadataAndRuntimeLimitations.md)
- [`docs/parser/DiagnosticsObservationCorrelation.md`](./DiagnosticsObservationCorrelation.md)

## 1) Runtime observation contract

Applicable types:

- `AlternativeRuntimeObservation`
- `ParserRuntimeObservationKind`
- `ParserRuntimeObservationStatus`
- `IParserRuntimeObserver`

### Guarantees

- Observations are emitted in the deterministic scheduling order of a deterministic parser run.
- Observation payloads are immutable (`AlternativeRuntimeObservation` record values).
- Observation callbacks are passive and non-authoritative.
- Observer callback exceptions are isolated and must not alter parser execution semantics.
- `Kind` describes **which scheduler event was observed**.
- `Status` describes **the observed runtime status at that moment**.
- `Kind` and `Status` are distinct dimensions and must not be collapsed into a single semantic channel.
- Runtime observation does not grant replay, control, or execution authority.

### Non-guarantees

- No guarantee of exhaustive internal scheduler state exposure.
- No exposure of `ActiveParseState` internals via observation payloads.
- No proof of semantic equivalence from observations alone.
- No branch replay/resume contract.
- No commitment that every future runtime internal detail will be surfaced in observations.
- No commitment that observation payload fields model complete runtime internals.

## 2) Export contract

Applicable types:

- `RuntimeObservationTextWriter`
- `RuntimeObservationJsonWriter`

Exports are tooling-oriented diagnostics. They are not parser execution contracts.

### Shared guarantees

- Export functions are deterministic for the same input observation sequence.
- Exports are descriptive only and not authoritative parse state.
- Exports are not replay formats.
- Exports do not expose internal runtime structures such as `ActiveParseState`.

### Text export contract

`RuntimeObservationTextWriter.Write` outputs one line per observation with:

- invariant token labels,
- stable field order,
- no trailing newline (lines separated by `\n`).

Intended use:

- deterministic diagnostics,
- snapshot-like tooling comparisons.

### JSON export contract

`RuntimeObservationJsonWriter.Write` outputs a JSON array with one object per observation.

Current object field names are:

- `Kind`
- `Status`
- `Rule`
- `Alternative`
- `CurrentInputPosition`
- `OriginInputPosition`
- `Priority`

Additional expectations:

- enum values are emitted as enum names (`ToString()` representation),
- object property order is stable for the current implementation and covered by tests,
- output is intentionally minimal and descriptive.

## 3) Stability boundary

Within the current major line, this contract is intended to be stable for tooling-oriented comparisons, while staying explicitly non-authoritative for runtime execution.

If future runtime internals evolve, observation/export additions should preserve these passive boundaries and avoid implying execution or replay semantics.
