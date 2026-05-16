# Runtime State Ownership

This document consolidates runtime-state ownership and authority boundaries for `Utils.Parser`.
It is intentionally conservative and describes current behavior only.

## Runtime authority model

The parser runtime is authority-layered.

- `ParserEngine` is the final runtime authority for:
  - token consumption,
  - parse-tree construction,
  - diagnostic production,
  - final parse outcomes.
- Supporting runtime components provide orchestration, probing, caching, and metadata.
- Metadata components are descriptive and cannot decide parse outcomes on their own.

## Parsing authority

`ParserEngine` remains the authoritative parser execution component.

- It owns recursive rule parsing decisions.
- It owns branch acceptance/rejection outcomes.
- It owns final success/failure and trailing-token validation.
- It owns parse result materialization.

Non-engine components may assist the engine but do not replace these decisions.

## Scheduling authority

`AlternativeScheduler` is an orchestrator, not an autonomous parser executor.

- It orders and coordinates alternative attempts.
- It aggregates descriptive metadata for shared-prefix scenarios.
- It does not own semantic predicate truth.
- It does not own diagnostics authority.
- It does not implement replay, rollback, speculative execution, or parser-graph traversal.

`ScheduledAlternativeExecutor` performs local alternative attempts.

- It is bounded to one alternative attempt at one invocation origin.
- It does not provide global parse authority.
- It does not provide transactional isolation.
- It does not provide rollback or replay.

## State ownership

The runtime separates state by authority.

### Reusable runtime state (authoritative)

Owned by `ParserStateRegistry` for invocation-keyed reuse safety:

- visited parser state tracking,
- completed invocation outcomes,
- reusable success/failure outcomes.

### Metadata state (non-authoritative)

Stored or computed as descriptive structures only:

- continuation descriptors,
- shared-prefix plans,
- shared-prefix candidates,
- scheduling annotations.

### Preparatory state (non-authoritative)

Used to coordinate execution attempts:

- scheduler candidate sets,
- lookahead probe outcomes,
- lookahead cache entries.

### Descriptive state (non-authoritative)

`ActiveParseState` and related keys are structural runtime descriptors.

- `Continuation` is descriptive metadata only.
- `ParentStateKey` is lineage metadata, not an execution stack.
- `Depth` is descriptive lineage depth, not semantic frame depth.

## Metadata-only boundaries

The following remain metadata-only and non-executable:

- continuation descriptors,
- shared-prefix plans,
- lookahead metadata,
- scheduling metadata.

These structures cannot execute parsing, resume execution, or replay previous runtime steps.

## Explicit non-goals

The current runtime does **not** implement:

- replay,
- rollback,
- semantic runtime frames,
- parser graph execution,
- speculative execution,
- semantic-state-aware memoization.

The runtime remains deterministic, conservative, syntax-oriented, and execution-conservative.

## Runtime lifecycle contracts

`ActiveParseState` lifecycle values are local runtime contracts, not global parse outcomes.

- `Active`: exploratory branch-local state.
  - mutable transitions are represented only by creating new immutable state values.
  - not authoritative for global acceptance/rejection.
- `Completed`: local branch completion.
  - does **not** mean the full parse is accepted.
  - may still be rejected by higher-level orchestration in `ParserEngine`.
- `Failed`: local branch failure.
  - does **not** mean global parse failure.
  - may still participate in diagnostic context propagation.
- `Pruned`: orchestration-only elimination marker.
  - not a syntax-invalidity signal.
  - must not alter parse outcome, parse-tree shape, or syntax-error diagnostics.
  - may emit explicit pruning/ambiguity diagnostics where already supported.

### Diagnostics ownership contracts

- `ParserEngine` is final authority for emitted diagnostics and final parse outcome diagnostics.
- `ScheduledAlternativeExecutor` can return branch-level outcomes that carry diagnostic context,
  but it is not the final diagnostics authority.
- `AlternativeScheduler` coordinates branch attempts and may report ambiguity-pruning context,
  but it must not invent new syntax diagnostics as parse authority.
- `ActiveParseState` is descriptive-only local runtime data and does not own diagnostic decisions.

### Partial parse-node ownership contracts

- Partial nodes inside `ActiveParseState` are local scheduling/runtime artifacts.
- They support branch-local selection and metadata flow only.
- Final parse-tree authority remains in `ParserEngine` parse outcomes.

## Branch outcome model

Branch outcomes are intentionally split between **local** exploration and **global** parse authority.

- **Local success**
  - Means one branch completed for one invocation context.
  - Does **not** imply global parse acceptance.
  - Does **not** guarantee trailing-token consumption.
  - Does **not** guarantee final branch selection.
- **Global success**
  - Decided only by `ParserEngine`.
  - Requires an accepted final outcome and full-input consumption.
- **Local failure**
  - Means one branch attempt failed in current exploration.
  - Does **not** imply global parse failure.
  - May still contribute local diagnostic context.
  - May be stored as reusable invocation-local outcome.
- **Global failure**
  - Decided only by `ParserEngine`.
  - Occurs when no acceptable final outcome remains, or when trailing-token validation rejects the parse.
- **Pruned outcome**
  - Pruning is an orchestration optimization in scheduling.
  - Pruning is **not** syntax invalidity.
  - Pruning is **not** final parse failure.
  - Pruning diagnostics (when emitted) are separate from syntax-failure diagnostics.

### Diagnostic propagation model

Diagnostics can be observed at multiple runtime layers, but final authority remains centralized.

- **Local diagnostics** are exploratory descriptions generated during branch attempts.
- **Transported diagnostics** may flow through local result containers without ownership transfer.
- **Pruning diagnostics** describe orchestration decisions, not syntax invalidity.
- **Global diagnostics** are the emitted parse diagnostics selected by `ParserEngine` for the final outcome.

### Observable diagnostics categories (current runtime)

Current observable categories are implementation-aligned and intentionally flat:

- **Syntax/parse-failure diagnostics**: parse failure when no acceptable root outcome exists.
- **Trailing-token diagnostics**: engine-authoritative rejection when input remains after local success.
- **Pruning diagnostics**: orchestration ambiguity/pruning information produced by scheduling flow.
- **Backtracking diagnostics**: orchestration-only information about branch rewinds during exploration.
- **Unsupported-feature diagnostics**: compatibility-oriented diagnostics for parsed-but-not-executed or unsupported ANTLR4 features.
- **Informational diagnostics**: runtime traceability diagnostics such as entering/leaving rule and default behavior applied.

These categories describe current behavior and ownership boundaries; they are not a new runtime hierarchy.

### Diagnostics lifecycle (current behavior)

Diagnostics follow a conservative lifecycle:

1. **Creation** in the component currently executing logic (`ParserEngine`, scheduler/executor, resolver/converter pipelines).
2. **Propagation** via shared `DiagnosticBag` references passed through runtime calls.
3. **Transport** through local branch outcomes where needed for orchestration visibility.
4. **Global visibility decision** through engine-owned final parse acceptance/rejection gates.

Important lifecycle boundary:

- branch-local diagnostics are descriptive and may be present even when that branch is later rejected;
- successful global parse can still keep orchestration diagnostics (for example pruning/backtracking);
- rejected local branch outcomes do not automatically become authoritative parse-failure diagnostics;
- trailing-token diagnostics are emitted only by final engine acceptance checks.

### Partial parse outcomes

Partial outcomes are descriptive runtime artifacts:

- partial nodes in local branch states,
- completed local branch outcomes,
- rejected local branch outcomes.

They support deterministic orchestration and reuse, but they are not authoritative parse acceptance decisions.

## Alternative selection and pruning contracts (current behavior)

This section documents current observable behavior only. It is not a forward design proposal.

- Final selected parse outcome is owned by `ParserEngine`.
- `AlternativeScheduler` computes a best local candidate among locally completed states.
- `ScheduledAlternativeExecutor` only transports local branch outcomes; it does not own final selection.

### Local candidate eligibility

- A branch is a local completion candidate only when an `ActiveParseState` is completed.
- Failed local branches remain useful for diagnostics/backtracking context, but are not completion candidates.
- Branches may complete locally and still be globally rejected later (for example, trailing tokens).

### Deterministic local comparison order

Current local comparison uses the following stable ordering:

1. longest consumed input (`CurrentInputPosition`),
2. lower `Alternative.Priority` value,
3. lower `AlternativeIndex`.

This ordering is used in scheduler deduplication and best-candidate selection, and is intentionally deterministic.

### Branch equivalence and pruning eligibility

- Equivalence keys are structural/runtime-orchestration keys (rule, origin, end/current position, cursor kind/index).
- Pruning is allowed only when branch semantics are not considered distinct by the current runtime check.
- Equivalence here is conservative and structural; it is **not** a complete semantic-equivalence proof.
- Equivalent branches can still each have locally successful outcomes before pruning.

### Pruning semantics and diagnostics

- Pruning is orchestration-only elimination.
- Pruning is not syntax invalidity.
- Pruning is not parse failure.
- Pruning does not own final parse acceptance.
- Pruning diagnostics are ambiguity/orchestration diagnostics and remain separate from syntax-failure diagnostics.

### Completed state versus final selected outcome

- A completed local state means one branch reached a local completion point.
- Multiple local completions may coexist.
- Only one deterministic local winner is selected by scheduling.
- Final parse acceptance still depends on engine-level gates (including full-input consumption).
