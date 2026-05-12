# Parser Metadata and Runtime Limitations

## Purpose

This document consolidates parser documentation that explains what is currently implemented as metadata, what is intentionally not executed at runtime, and which architectural constraints protect deterministic parser behavior.

It is factual and conservative. It does not define a roadmap commitment.

## Consolidation note

This document intentionally consolidates previous shared-prefix architecture documents to keep invariants and runtime-limitation guidance in one place:

- `SharedPrefixExecutionPreconditions.md`
- `SharedPrefixMetadataPipeline.md`

It also replaces narrower invocation-frame-only wording with a broader metadata/runtime limitations scope.

## 1) Parameters and `returns`: parsed and preserved as metadata

The grammar ingestion pipeline supports ANTLR4-style rule signatures such as:

- `rule[int x]`
- `rule returns [int value]`
- `rule[int x] returns [int value]`

Current behavior:

- parameter and `returns` blocks are parsed;
- bracket content is preserved with balanced-text handling;
- multiline and nested generic-like text is preserved;
- metadata is stored as raw text for compatibility and traceability.

Example preserved as raw metadata text:

```antlr
rule[Dictionary<string, List<int>> map]
```

### Runtime semantics intentionally not implemented

`ParserEngine` currently does **not** provide:

- argument passing to rule invocations;
- typed parameter binding;
- invocation-frame lifecycle;
- return-value propagation;
- parameter/return runtime scopes;
- semantic type resolution for parameters/returns.

No parameter evaluation and no return extraction occurs at runtime.

## 2) Shared-prefix infrastructure: metadata-only

The shared-prefix pipeline exists for analysis, validation, and auditability. It does not execute shared prefixes.

Active metadata production path:

```text
ParserLookaheadProbe
    -> ParserLookaheadSharedPrefixDetector
    -> ParserContinuationFactory
    -> ParserSharedPrefixPlanFactory
```

Inspection/audit path over produced plans:

```text
ParserSharedPrefixPlan
    -> ParserSharedPrefixPlanValidator
ParserSharedPrefixPlan
    -> ParserSharedPrefixExecutionEligibilityAnalyzer
ParserSharedPrefixPlan
    -> ParserSharedPrefixPlanFormatter
```

Boundary rules:

- `ParserEngine` owns parse semantics and execution;
- `AlternativeScheduler` owns deterministic orchestration;
- validators/analyzers/formatters are descriptive tools, not runtime authorities;
- continuation metadata is structural, not resumable runtime state.

## 3) Invariants that must remain true

The current runtime must preserve:

- parser determinism;
- parse-tree stability;
- diagnostics stability (content/position/order);
- scheduling and alternative-order equivalence;
- conservative correctness-first behavior.

If metadata conclusions conflict with these invariants, baseline runtime behavior must remain unchanged.

## 4) Unsupported runtime capabilities (current state)

The following are intentionally unsupported in current runtime behavior:

- shared-prefix execution;
- continuation replay;
- parser graph/DAG execution;
- adaptive LL and GLL behavior;
- speculative replay;
- async/parallel parsing;
- parse-forest generation;
- runtime parameter/returns invocation semantics.

## 5) Why runtime invocation support is non-trivial

Any future runtime support for parameters/returns would require careful architectural work. It would require or may require:

- explicit invocation-frame modeling;
- deterministic value propagation under backtracking;
- rollback-safe value handling;
- memoization rules for frame/value-sensitive results;
- parser-state interaction rules (`ParserStateRegistry` and related state normalization);
- diagnostics propagation rules for value-binding failures.

These topics are architecture considerations, not implementation commitments.

## 6) Explicit non-goals of current implementation

Current implementation does not provide and this document does not propose:

- runtime parameter support;
- semantic parsing/splitting of parameter lists;
- parameter/return type checking;
- return propagation APIs;
- symbol tables for invocation values;
- parser behavior or diagnostics changes.

## 7) Compatibility matrix reference

For a feature-by-feature status view (supported / parsed-only / unsupported), see:

- [ANTLR4 Compatibility Matrix](./Antlr4CompatibilityMatrix.md)

## 8) Preconditions philosophy for any future execution experiments

No shared-prefix execution experiment should be considered unless all targeted-scenario preconditions are demonstrated, including:

- deterministic scheduling equivalence;
- stable continuation anchors;
- no diagnostics divergence;
- no parse-tree divergence;
- no observable side-effect reordering;
- no unsafe predicate/action influence in the shared segment.

If any precondition is not proven, baseline duplicated work per alternative should remain in place.

## 9) Unsupported cases to reject by default

Execution-oriented work should reject, by default:

- recursive shared-prefix replay;
- nested shared-prefix execution/replay;
- fallback-boundary execution;
- divergent continuation layouts;
- speculative replay semantics;
- parser graph traversal semantics.

## 10) Review gate language for future execution-oriented PRs

Any future PR proposing execution changes would need to demonstrate, at minimum:

1. which invariants are preserved and how equivalence was validated;
2. which unsupported cases remain rejected;
3. why parser determinism, diagnostics ordering/content, and parse-tree shape remain equivalent;
4. a rollback path to baseline behavior.

Without this evidence, execution-oriented changes should not be merged.

## Capability model note

A centralized parser capability descriptor model is available in code (`ParserFeatureCapabilities`) to make support status queryable and auditable.

It is intentionally descriptive metadata and is **not** used to gate parsing, alter diagnostics, or introduce new runtime behavior.


## Runtime feature policy

Parser runtime optional behaviors are centralized through `ParserRuntimeFeaturePolicy` (`Utils.Parser.Runtime`).
The default policy remains conservative and unchanged:
- semantic predicates use `DefaultSemanticPredicateEvaluator` and are reported as not enforced;
- inline parser actions use `DefaultParserActionExecutor` and are reported as not executed.

Existing constructors that accept `ISemanticPredicateEvaluator` and/or `IParserActionExecutor` are still supported and internally normalized to the runtime policy object.
