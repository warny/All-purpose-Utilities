# Runtime Trace Analysis

This document defines a conservative, tooling-only analysis layer for runtime trace observations.

See also:

- [`RuntimeObservationAndExportContract.md`](./RuntimeObservationAndExportContract.md)
- [`DiagnosticsObservationCorrelation.md`](./DiagnosticsObservationCorrelation.md)
- [`RuntimeStateOwnership.md`](./RuntimeStateOwnership.md)

## Intent

Runtime trace analysis validates that external tooling can extract useful descriptive information from passive observations and deterministic exports.

The analysis layer consumes only:

- `AlternativeRuntimeObservation` sequences, or
- deterministic export strings (`RuntimeObservationTextWriter`, `RuntimeObservationJsonWriter`).

The analysis layer produces only:

- summaries,
- distributions,
- deterministic descriptive comparisons.

## Explicit non-authority boundaries

Runtime trace analysis does **not** imply:

- replay,
- runtime ownership,
- parser authority,
- diagnostics authority,
- parser execution control,
- scheduling control,
- runtime object navigation.

## Current abstractions

The current tooling abstractions are:

- `RuntimeTraceSummary`: deterministic counts and distributions.
- `RuntimeTraceAnalyzer`: summary and comparison entry points.
- `RuntimeTraceComparison`: deterministic descriptive comparison values.

These abstractions are read-only and do not access runtime internals.

## Typical outputs

Examples of allowed descriptive outputs:

- total observation count,
- event-kind distribution,
- status distribution,
- rule-name distribution,
- alternative-index distribution,
- deterministic export identity flags,
- deterministic event count deltas.

These outputs are diagnostic aids for tooling only.
