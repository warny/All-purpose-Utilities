# Shared Prefix Metadata Pipeline (ParserEngine)

## 1. Current Status

The shared-prefix work in `Utils.Parser` is currently **metadata-only**.

The runtime currently:

- must not execute shared prefixes;
- must not replay continuations;
- must not traverse a parser graph runtime;
- must keep parse execution owned by `ParserEngine`.

Current runtime behavior remains unchanged:

- scheduler behavior is unchanged;
- parser behavior is unchanged;
- diagnostics behavior is unchanged.

This document describes what exists now for preparation, validation, and architecture control. It does not authorize execution changes.

## 2. High-Level Metadata Pipeline

### Runtime metadata production (active path)

Current scheduling-time metadata production flow:

```text
ParserLookaheadProbe
    -> ParserLookaheadSharedPrefixDetector
    -> ParserContinuationFactory
    -> ParserSharedPrefixPlanFactory
```

`AlternativeScheduler.BuildMetadata` uses this active path to produce plan metadata and return it as scheduling metadata output.

### Inspection and audit tooling (non-scheduling path)

Inspection flow over already-produced plans:

```text
ParserSharedPrefixPlan
    -> ParserSharedPrefixPlanValidator
ParserSharedPrefixPlan
    -> ParserSharedPrefixExecutionEligibilityAnalyzer
ParserSharedPrefixPlan
    -> ParserSharedPrefixPlanFormatter
```

The validator and eligibility analyzer are not active scheduling stages. The eligibility analyzer is currently consumed by inspection/formatting workflows, not by runtime scheduling decisions.

Stage intent:

1. `ParserLookaheadProbe` captures conservative lookahead observations.
2. `ParserLookaheadSharedPrefixDetector` identifies potential shared-prefix candidates from observations.
3. `ParserContinuationFactory` derives structural continuation descriptors for alternatives.
4. `ParserSharedPrefixPlanFactory` builds metadata plans describing boundaries and segments.
5. `ParserSharedPrefixPlanValidator` checks structural consistency of produced plans for audit and verification usage.
6. `ParserSharedPrefixExecutionEligibilityAnalyzer` classifies whether future execution experiments could be theoretically safe for analysis/debug views.
7. `ParserSharedPrefixPlanFormatter` produces dry-run/debug output for inspection.

All stages currently produce metadata artifacts only. They must not alter parse execution.

## 3. Component Responsibilities

### `ParserEngine`

- owns parser semantics;
- owns parse execution;
- remains authoritative for runtime behavior.

### `AlternativeScheduler`

- owns deterministic orchestration;
- owns scheduling order;
- produces metadata observations as part of orchestration context;
- does not execute shared prefixes.

### `ParserLookaheadProbe`

- shallow;
- conservative;
- observational only.

### `ParserContinuationFactory`

- creates structural continuation descriptors;
- does not create resumable runtime replay state.

### `ParserSharedPrefixPlanFactory`

- produces metadata plans only;
- does not schedule or execute parser work.

### `ParserSharedPrefixPlanValidator`

- validates structural consistency only;
- is not an execution authority.

### `ParserSharedPrefixExecutionEligibilityAnalyzer`

- classifies theoretical future safety envelopes;
- does not influence current runtime behavior;
- does not grant execution permission.

### `ParserSharedPrefixPlanFormatter`

- outputs debug/dry-run metadata views only;
- does not modify scheduling;
- does not modify parser execution.

## 4. Architectural Boundaries

The following boundaries must remain explicit and preserved:

- parser execution is owned by `ParserEngine`;
- scheduling is owned by `AlternativeScheduler`;
- validation is structural verification;
- eligibility is theoretical classification;
- formatting is observability;
- metadata is descriptive, not executable.

Required distinctions:

- validator **!=** eligibility analyzer;
- eligibility **!=** execution permission;
- formatter **!=** scheduler;
- continuations **!=** resumable execution.

No current component in this metadata pipeline may bypass these boundaries.

## 5. Current Invariants

The runtime must preserve all of the following invariants:

- deterministic scheduling;
- parse-tree stability;
- diagnostics stability;
- no additional speculative parsing introduced by shared-prefix metadata;
- existing parser backtracking semantics remain unchanged;
- no shared-prefix execution;
- no continuation replay;
- no parser graph traversal;
- no parallel parsing.

If a metadata conclusion conflicts with any invariant, runtime semantics must remain baseline and unchanged.

## 6. Current Limitations (Intentionally Unsupported)

The current architecture intentionally does not implement:

- recursive shared-prefix execution;
- nested continuation replay;
- parser DAG execution;
- adaptive LL behavior;
- GLL behavior;
- async parsing;
- parse forests;
- speculative replay.

These are outside the current metadata-only phase and must not be implied as active behavior.

## 7. Future Direction (Conservative Scope)

Any future execution-oriented work should remain conservative and phase-gated:

1. metadata stabilization;
2. explicit execution gating;
3. extremely limited shared-token experiments;
4. possible continuation replay research under strict equivalence checks.

Current state remains unchanged: nothing beyond metadata is currently active.

## References

- [Shared Prefix Execution Preconditions (ParserEngine)](./SharedPrefixExecutionPreconditions.md)
