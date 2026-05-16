# Parser Metadata and Runtime Limitations

## Purpose

This file is the **conceptual overview** for metadata scope and runtime limitations.

Use this document when the question is: *"what is metadata-only today, and what structural limits/preconditions exist?"*

Companion roles:

- `docs/parser/RuntimeStateOwnership.md`: authoritative ownership and parse-authority reference.
- Runtime source comments (`Utils.Parser/Runtime/*`): implementation-local reminders and invariants near code.

To reduce repetition, this document avoids redefining detailed ownership tables when already covered in `RuntimeStateOwnership.md`.

This document consolidates parser documentation that explains what is currently implemented as metadata, what is policy-controlled at runtime, and which architectural constraints protect deterministic parser behavior.

It is factual and conservative. It does not define a roadmap commitment.

## Consolidation note

This document intentionally consolidates previous shared-prefix architecture documents to keep invariants and runtime-limitation guidance in one place:

- `SharedPrefixExecutionPreconditions.md`
- `SharedPrefixMetadataPipeline.md`

It also replaces narrower invocation-frame-only wording with a broader metadata/runtime limitations scope.


## Runtime policy boundaries (semantic predicates and actions)

`ParserEngine` delegates semantic predicates and embedded actions to `ParserRuntimeFeaturePolicy`:

- Default policy is conservative:
  - semantic predicates return `NotEvaluated`;
  - parser actions return `NotExecuted`.
- Custom policies may:
  - reject branches (`SemanticPredicateEvaluationResult.Rejected`);
  - accept branches (`Satisfied`);
  - execute parser actions (`ParserActionExecutionResult.Executed`).

This means runtime behavior can depend on injected evaluators/executors, even though default behavior preserves legacy conservative semantics.

## Memoization assumptions and limits

Invocation reuse currently keys completed results by `(rule, input position, precedence)`.
The memoization layer does **not** currently model:

- runtime feature policy state;
- semantic evaluator external state;
- parser action side-effect state;
- rollback-safe mutable semantic frames.

As a result, custom policies are expected to remain deterministic for equivalent invocations, avoid invocation-count-dependent behavior, and avoid externally observable mutable semantic state.
Future runtime work may require broader memoization keys and rollback-aware semantic-state modeling.


## Backtracking and observable action execution

Backtracking can execute embedded parser actions in branches that are later rejected or pruned.
Current runtime guarantees are intentionally conservative:

- no rollback is applied to external action side effects;
- no exactly-once guarantee is provided for actions across backtracked attempts;
- no transactional isolation exists between competing alternatives;
- memoization remains syntax-oriented and is not semantic-state-aware.

Custom runtime policies should therefore remain deterministic and conservative, and should avoid dependence on mutable external semantic state.

## Diagnostics meaning for policy-controlled features

- `SemanticPredicateNotEnforced` is emitted when a predicate is encountered and the active evaluator returns `NotEvaluated`.
- `InlineActionStoredNotExecuted` is emitted when an embedded action is encountered and the active executor returns `NotExecuted`.

With custom policies, these diagnostics may be reduced or suppressed when predicates/actions are actively handled.

## Diagnostics ownership and lifecycle boundaries

Current diagnostics behavior is intentionally conservative:

- `ParserEngine` owns final observable parse outcome diagnostics (including parse failure and trailing tokens).
- `AlternativeScheduler` and `ScheduledAlternativeExecutor` may emit orchestration diagnostics (for example pruning/backtracking) but do not own final parse acceptance.
- `ParserLookaheadProbe` and `ParserLookaheadCache` provide shallow transport/probing metadata and do not establish parse-authoritative diagnostics by themselves.

Local branch diagnostics are descriptive runtime context and do not automatically become global parse failure diagnostics.
Compatibility diagnostics (unsupported or parsed-only ANTLR4 capabilities) are independent from branch success/failure and may coexist with successful parse outcomes.

## Lookahead limitations and fallback-to-parse contract

Current lookahead behavior is intentionally conservative and shallow.

- Lookahead probing is first-token-oriented and does not perform deep parser-rule simulation.
- Parser-rule references in lookahead remain advisory and may return `Unknown`.
- Epsilon-sensitive shapes may produce `Unknown`/`EpsilonPossible` to avoid over-claiming acceptance.
- Predicates are never evaluated by lookahead as parse-authoritative evidence.
- Parser actions are never executed by lookahead as parse-authoritative evidence.
- Lookahead is semantic-state-agnostic and does not model external mutable runtime state.

Fallback semantics:

- `ImmediateReject` can be used as deterministic local reject evidence in allowed scheduler/executor contexts.
- `RequiresParse`, `Unknown`, and `EpsilonPossible` are parse-required outcomes.
- Ambiguous or parser-rule-dependent outcomes must defer to real parsing.
- Cached lookahead entries are reusable advisory metadata only and do not replace parse execution.

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


## Shared-prefix metadata authority, limitations, and lifecycle

Current shared-prefix infrastructure is descriptive-only.

Authority clarifications:

- Shared-prefix metadata does not own parse acceptance or rejection.
- Shared-prefix metadata does not own branch selection.
- Shared-prefix metadata does not authorize replay, continuation execution, or branch merging.
- Shared-prefix metadata does not establish semantic equivalence between alternatives.
- Shared-prefix metadata does not imply rollback safety, side-effect isolation, or speculative safety.

Current structural limitations (explicit):

- no shared execution frames,
- no continuation replay,
- no parser-graph execution,
- no action replay safety model,
- no semantic-runtime-aware equivalence model,
- no rollback guarantees,
- no side-effect isolation model,
- no shared semantic state,
- no speculative execution.

Lifecycle clarifications:

- production: metadata is produced from shallow lookahead and scheduling context,
- transport: metadata is carried through scheduler output and registry-attached structures,
- reuse: metadata is reused for validation/formatting/analysis,
- discardability: metadata can be discarded without changing parse-authoritative outcomes,
- observability: metadata is observable for audit/documentation purposes only.

Metadata lifecycle remains independent from parse authority and diagnostics authority.

## Conservative preconditions before any future activation work

Any future work that attempts replay/shared execution/continuation resume must first define and validate, at minimum:

- explicit rollback semantics,
- semantic-state ownership and isolation,
- deterministic replay rules,
- diagnostics replay/ownership rules,
- parse-tree compatibility/equivalence proofs,
- branch identity and merge-safety contracts,
- continuation ownership and execution authority contracts.

These are prerequisite boundaries, not current capabilities.


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
