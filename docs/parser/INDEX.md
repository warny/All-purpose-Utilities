# Utils.Parser Documentation Index

This index consolidates the parser documentation set and gives a short summary of each file.

## Core contracts and boundaries

- [`RuntimeStateOwnership.md`](./RuntimeStateOwnership.md): Canonical authority map for runtime responsibilities (parse decisions, diagnostics, parse-tree ownership, scheduler/registry boundaries).
- [`ParserMetadataAndRuntimeLimitations.md`](./ParserMetadataAndRuntimeLimitations.md): Limitations-first reference that clarifies metadata-only semantics, unsupported runtime semantics, and activation preconditions.
- [`RuntimeObservationAndExportContract.md`](./RuntimeObservationAndExportContract.md): Defines what runtime observations are allowed to emit and how exporters must remain passive and non-authoritative.
- [`DiagnosticsObservationCorrelation.md`](./DiagnosticsObservationCorrelation.md): Conservative rules for linking diagnostics to runtime observations without changing runtime authority.

## Metadata and preparation documents

- [`ContinuationMetadata.md`](./ContinuationMetadata.md): Describes continuation metadata lifecycle and explicitly states that metadata does not grant execution/resume authority.
- [`SharedLookAheadPreparation.md`](./SharedLookAheadPreparation.md): Documents shared-prefix/look-ahead preparation as deterministic advisory metadata only.
- [`RuntimeArchitecture.md`](./RuntimeArchitecture.md): Canonical pipeline and ownership map from preparation through analysis, including metadata authority boundaries.

## Compatibility and analysis

- [`EmbeddedCodeExecutionModel.md`](./EmbeddedCodeExecutionModel.md): Architecture boundary for ANTLR embedded code, including current behavior, the target two-path model (C# source generation vs runtime-inline `IExpressionCompiler` preparation), current semantic-predicate and parser-action runtime adapters as intermediate opportunistic-compilation implementations, cache limits (predicate vs action), the internal minimal preparation boundary, and multi-project responsibilities.
- [`ANTLRCompatibility.md`](./ANTLRCompatibility.md): Canonical ANTLR compatibility reference including feature behavior, shared prequel metadata/diagnostic boundaries, runtime-generator parity notes, and usage guidance.
- [`Antlr4CompatibilityMatrix.md`](./Antlr4CompatibilityMatrix.md): Current ANTLR4 feature support matrix with explicit levels (supported, partial, parsed-only, unsupported), including semantic-predicate vs precedence-predicate runtime-policy distinctions and continuation metadata support boundaries.
- [`RuntimeTraceAnalysis.md`](./RuntimeTraceAnalysis.md): Tooling-oriented analysis model for runtime traces, focused on descriptive outputs rather than runtime control.

## Maintenance rule for contributors and agents

When adding, removing, or materially changing any file in `docs/parser/`, update this index in the same change so summaries stay accurate.