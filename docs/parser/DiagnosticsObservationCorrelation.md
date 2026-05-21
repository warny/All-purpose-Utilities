# Diagnostics / Observation Correlation Contract

This document defines conservative boundaries for correlating parser diagnostics with runtime observations in `Utils.Parser`.

It extends the current passive runtime observation model without changing runtime behavior, parser authority, or export authority.

See also:

- [`docs/parser/RuntimeStateOwnership.md`](./RuntimeStateOwnership.md)
- [`docs/parser/RuntimeObservationAndExportContract.md`](./RuntimeObservationAndExportContract.md)
- [`docs/parser/ParserMetadataAndRuntimeLimitations.md`](./ParserMetadataAndRuntimeLimitations.md)

## 1) Contract intent

Correlation is allowed only as descriptive metadata for tooling.

The preferred conservative model is one-way:

- `Diagnostic -> RuntimeObservationId?`

This means a diagnostic may optionally carry an immutable descriptive identifier that points to an observed runtime event in the same parser run.

No reverse ownership link is implied.

## 2) Authority and ownership boundaries

These boundaries are mandatory:

- Diagnostics remain parser-authoritative.
- Runtime observations remain passive and descriptive.
- Correlation identifiers do not transfer ownership between diagnostics and observations.
- Correlation identifiers do not change parser scheduling, parse acceptance, parse-tree shape, or diagnostic authority.

A correlation identifier is not an execution handle.

## 3) Why direct object references are forbidden

Direct references such as `Diagnostic -> Observation object` or `Observation -> Diagnostic[]` are intentionally forbidden because they would:

- blur ownership boundaries,
- create graph-navigation pressure,
- risk accidental replay expectations,
- increase coupling to mutable runtime internals,
- make export contracts harder to keep deterministic and non-authoritative.

To preserve determinism and ownership boundaries, correlation must stay identifier-based and immutable.

## 4) Correlation identifier semantics

Any future correlation identifier must be interpreted as:

- descriptive only,
- scoped to a single observed parser run unless explicitly documented otherwise,
- optional (`null` / missing is valid),
- non-authoritative,
- non-navigational.

A correlation identifier must not imply:

- runtime state reachability,
- runtime object access,
- replay capability,
- scheduler authority,
- parser-control authority,
- cross-run stability by default.

## 5) Non-goals

This contract does not introduce:

- runtime graph construction,
- bidirectional diagnostic/observation links,
- mutable correlation registries,
- ID-based runtime lookup APIs,
- trace replay,
- diagnostic-driven parsing,
- observation-driven parsing,
- parser execution control.

## 6) Export-facing expectations

If correlation appears in future text or JSON exports, the field must remain descriptive and optional.

Export constraints remain:

- exports are deterministic for the same input sequence,
- exports are not replay formats,
- exports must not expose mutable runtime internals,
- absence of correlation data must be valid and expected.

## 7) Recommended future shape (documentation-only)

If a runtime identifier type is added in future code, keep it minimal and immutable (for example a small readonly value type).

Any adoption should remain conservative and behavior-neutral:

- no global mutable ID registry,
- no runtime lookup service,
- no ownership transfer,
- no parser-control surface.
