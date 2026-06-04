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

- [`EmbeddedCodeExecutionModel.md`](./EmbeddedCodeExecutionModel.md): Architecture boundary for ANTLR embedded code, including the two opt-in execution paths (runtime-inline prepared expressions vs source-generated C# hooks), language-neutral runtime core, prepared registry/policy-builder responsibilities, generated policy helpers, runtime-index alignment, the execution-context copier, the execution-state manager, managed execution-state rollback for ordinary alternatives, left-recursive extensions, quantifier attempts, and negation probes, state-aware memoization key with post-rule snapshot restoration, its required-policy API compatibility note, default conservative behavior, source-generator unsupported-construct diagnostics, and execution limits.
- [`EmbeddedCodeTransactionalState.md`](./EmbeddedCodeTransactionalState.md): Audit and implementation plan for transactional embedded-code state, including implemented managed execution-state rollback for ordinary alternatives, left-recursive extensions, quantifier attempts, and negation probes, implemented execution-state key, required-policy API compatibility guidance, state-aware memoization constraints with post-rule snapshot restoration, and the required sequence before `@init` / `@after` execution.
- [`ANTLRCompatibility.md`](./ANTLRCompatibility.md): Canonical ANTLR compatibility reference and primary embedded-code status note, including default runtime behavior, runtime-inline expression opt-in, generated C# opt-in, `IParserExecutionStateManager` policy exposure with required-property compatibility guidance, a no-op default, and generated managers backed by `Fork()` / `CopyFrom(...)` and state keys, supported executable parser constructs, predicate options (`UP1030`), lexer/parser rule options metadata (`UP1033`/`UP1034`), represented-only constructs, unsupported constructs, diagnostics such as `UP1029`, limitations, and next steps.
- [`Antlr4CompatibilityMatrix.md`](./Antlr4CompatibilityMatrix.md): Current ANTLR4 feature support matrix with explicit levels and default/runtime-inline/generated-C# embedded-code columns aligned to the canonical compatibility note, including generator warnings for visible unsupported embedded-code constructs and state-aware invocation memoization notes.
- [`RuntimeTraceAnalysis.md`](./RuntimeTraceAnalysis.md): Tooling-oriented analysis model for runtime traces, focused on descriptive outputs rather than runtime control.

## Maintenance rule for contributors and agents

When adding, removing, or materially changing any file in `docs/parser/`, update this index in the same change so summaries stay accurate.

## Recent metadata note

`ANTLRCompatibility.md` is the primary compatibility source for embedded code. `EmbeddedCodeExecutionModel.md` and `EmbeddedCodeTransactionalState.md` complement it with architecture and flow details, including shared runtime indexing metadata, explicit unsupported reasons, fresh generated execution contexts for `ParseWithEmbeddedCode(string)`, explicit context-bound generated policies, the preparatory `ParserExecutionContextCopier<TContext>` helper, generated `Fork()` / `CopyFrom(...)` context helpers, state-aware completed-result memoization via `IParserExecutionStateManager.GetCurrentStateKey()` plus post-rule snapshot restoration, managed execution-state capture/restore for parser backtracking attempt boundaries, required-property compatibility guidance, the remaining transactional-state roadmap before `@init` / `@after`, `UP1030` predicate-options compatibility diagnostic, `UP1033`/`UP1034` rule options metadata diagnostics, `UP1031` parser-members injection diagnostics, and `UP1029` generator diagnostics for visible unsupported constructs used to keep runtime-inline preparation and source-generated hooks aligned without adding unsupported execution.
