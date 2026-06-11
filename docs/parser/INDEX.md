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

- [`EmbeddedCodeExecutionModel.md`](./EmbeddedCodeExecutionModel.md): Architecture boundary for ANTLR embedded code, including runtime-inline and generated-C# opt-in paths, the no-op default rule-call policy, the explicitly opt-in positional simple-literal binding policy, its narrow managed seed API, exact-arity validation, rollback/state-key integration, generated lifecycle hooks, and unsupported execution boundaries.
- [`EmbeddedCodeTransactionalState.md`](./EmbeddedCodeTransactionalState.md): Transactional-state audit covering managed backtracking rollback, state-aware memoization, generated lifecycle state, external-side-effect limits, deterministic hashing of supported positional literal parameter seeds, and volatile memoization bypass for arbitrary explicit seed objects.
- [`ANTLRCompatibility.md`](./ANTLRCompatibility.md): Canonical ANTLR compatibility reference, including conservative defaults, embedded-code opt-in paths, represented metadata, diagnostics, the explicitly opt-in exact-arity positional literal call policy, and its non-goals.
- [`Antlr4CompatibilityMatrix.md`](./Antlr4CompatibilityMatrix.md): Feature matrix for default, runtime-inline, and generated-C# behavior, including partial opt-in positional literal call binding, invocation-frame metadata, and state-aware memoization constraints.
- [`RuntimeTraceAnalysis.md`](./RuntimeTraceAnalysis.md): Tooling-oriented analysis model for runtime traces, focused on descriptive outputs rather than runtime control.

## Maintenance rule for contributors and agents

When adding, removing, or materially changing any file in `docs/parser/`, update this index in the same change so summaries stay accurate.

## Recent metadata note

`ANTLRCompatibility.md` is the primary compatibility source for embedded code. `EmbeddedCodeExecutionModel.md` and `EmbeddedCodeTransactionalState.md` complement it with architecture and flow details, including shared runtime indexing metadata, explicit unsupported reasons, fresh generated execution contexts for `ParseWithEmbeddedCode(string)`, explicit context-bound generated policies, the preparatory `ParserExecutionContextCopier<TContext>` helper, generated `Fork()` / `CopyFrom(...)` context helpers, state-aware completed-result memoization via `IParserExecutionStateManager.GetCurrentStateKey()` plus post-rule snapshot restoration, managed execution-state capture/restore for parser backtracking attempt boundaries, required-property compatibility guidance, passive parser rule invocation-frame descriptor infrastructure with preserved raw rule locals and exception metadata, explicit before/after rule-call policy callbacks with current-call-site metadata, generated C# opt-in `@init` / `@after` lifecycle support with missing-only untyped `null` allocation and explicit rule-local frame helper methods, generated C# parser `@header` / `@parser::header` source-file injection diagnostics (`UP1035`), generated C# parser `@footer` / `@parser::footer` trailing source injection diagnostics (`UP1036`), the top-level parse-rejection rollback boundary, `UP1030` predicate-options compatibility diagnostic, `UP1033`/`UP1034` rule options metadata diagnostics, `UP1031` parser-members injection diagnostics, and `UP1029` generator diagnostics for visible unsupported constructs used to keep runtime-inline preparation and source-generated hooks aligned without adding unsupported lexer execution.
