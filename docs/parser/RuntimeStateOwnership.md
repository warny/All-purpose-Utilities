# Runtime State Ownership

This document consolidates runtime-state ownership and authority boundaries for `Utils.Parser`.
It is intentionally conservative and describes current behavior only.

## Document role in parser documentation set

This file is the **authoritative ownership reference** for current runtime authority boundaries.

Use this document when the question is: *"which component owns which decision?"*

Companion roles:

- `docs/parser/ParserMetadataAndRuntimeLimitations.md`: conceptual limitations and preconditions overview.
- Runtime source comments (`Utils.Parser/Runtime/*`): implementation-local guardrails for maintainers.

To limit drift and repetition:

- keep ownership and authority contracts canonical here,
- keep conceptual narrative and limitations in the metadata/limitations document,
- keep source comments short and implementation-aligned, and refer to this document for canonical authority rules.

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
- It can use lookahead for conservative local shortcut rejection only.
- It cannot accept alternatives based on lookahead-only outcomes.

## Lookahead authority and ownership (current model)

Lookahead is intentionally split across three components with explicit boundaries:

- `ParserLookaheadProbe`
  - Computes shallow, first-token-oriented observations only.
  - Can authoritatively conclude `ImmediateReject` when deterministic mismatch is observed.
  - Cannot authoritatively conclude parse success.
- `ParserLookaheadCache`
  - Stores probe observations as runtime metadata for reuse in the same parse execution.
  - Does not own parse acceptance/rejection decisions.
  - Does not imply semantic equivalence between branches.
- `ParserEngine` (through scheduled execution)
  - Owns final parse acceptance and all branch confirmation.
  - Uses lookahead as advisory input only.

Authoritative parse acceptance always requires real parsing in engine-owned execution paths.

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


## Continuation metadata ownership and lifecycle (current runtime)

Continuation structures (`ContinuationKey`, `ParserContinuationKey`, `ParserContinuationDescriptor`, and `ActiveParseState.Continuation`) are metadata-only.

Ownership and authority boundaries:

- `ParserEngine` remains final authority for parse acceptance, diagnostics, and parse-tree outcomes.
- `ActiveParseState` can attach continuation metadata for branch-local observability only.
- `ParserStateRegistry` can transport continuation metadata grouped by invocation key.
- Neither attachment nor transport grants replay, resumability, frame restoration, rollback safety, or semantic equivalence.

Lifecycle (current behavior only):

1. **Production**: shallow continuation identities/descriptors may be produced from rule/alternative/cursor context.
2. **Attachment**: branch-local state may attach `ContinuationKey` in `ActiveParseState`.
3. **Transport**: registry may store/retrieve continuation keys per invocation identity.
4. **Observation/reuse**: callers may inspect metadata for diagnostics, formatting, or auditability.
5. **Discardability**: metadata can be removed or ignored without changing parse-authoritative outcomes.

Continuations are therefore independent from parse authority and independent from diagnostics authority.

## Runtime identity and equivalence model

Parser runtime identities are intentionally split by ownership purpose and must remain distinct.

- `ParserStateKey`:
  - visited-state tracking identity only.
- `RuleInvocationKey`:
  - invocation-local reusable completion identity only.
- `ContinuationKey`:
  - metadata transport identity only.
- `ActiveParseStateKey`:
  - local scheduling/deduplication identity only.
- `ActiveParseBranchEquivalenceKey`:
  - pruning/orchestration grouping identity only.

These identities are intentionally non-equivalent:

- none imply semantic equivalence;
- none imply replay safety;
- none imply rollback safety;
- none imply branch merge safety;
- none imply semantic-state equivalence.

Critical boundary comparisons:

- scheduling deduplication identity (`ActiveParseStateKey`) **!=** pruning equivalence identity (`ActiveParseBranchEquivalenceKey`);
- pruning equivalence **!=** reusable invocation result identity (`RuleInvocationKey`);
- reusable invocation identity **!=** parse acceptance identity (`ParserEngine` authority).

Why multiple identities exist:

- each runtime layer owns different decisions (visited-state tracking, invocation reuse, local scheduling, local pruning);
- collapsing identities would over-claim equivalence and could silently break conservative runtime guarantees.

Additional invariants:

- continuation metadata must not affect pruning grouping;
- continuation metadata must not affect reusable parse-result selection;
- reusable parse results are invocation-local reuse artifacts and are not final parse acceptance authority.

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


## Shared-prefix metadata ownership and lifecycle (current runtime)

Shared-prefix structures are intentionally metadata-only.

- Production: metadata is produced from shallow lookahead observations and scheduler orchestration context.
- Transport: metadata is transported through runtime result containers for visibility and auditability.
- Reuse: metadata may be reused for formatting/validation/analysis in the same conservative execution model.
- Discardability: metadata may be dropped without changing parse acceptance, branch selection, parse-tree shape, or diagnostics authority.
- Observability: metadata is observable as runtime information, not executable state.

Ownership boundaries:

- `ParserEngine` owns parse acceptance/rejection and final diagnostics.
- `AlternativeScheduler` owns deterministic orchestration and metadata aggregation.
- `ParserStateRegistry` owns invocation-local reusable tracking and metadata transport storage.
- Metadata structures (`ParserSharedPrefixPlan`, continuation descriptors, shared-prefix candidates) do not own execution authority.

Explicit non-authority guarantees:

- metadata does not authorize replay,
- metadata does not authorize continuation execution,
- metadata does not authorize branch merging,
- metadata does not establish semantic equivalence,
- metadata does not imply rollback safety,
- metadata does not override diagnostics ownership.

## Future activation preconditions (documentation boundary only)

Before any future activation work (such as replay, shared execution, continuation resume, or duplicated-work elimination), minimum architectural preconditions would need explicit designs and tests, including:

- explicit rollback model,
- semantic-state ownership model,
- side-effect safety model,
- deterministic replay semantics,
- diagnostics replay and ownership rules,
- parse-tree compatibility guarantees,
- branch identity guarantees,
- continuation ownership and execution authority model.

This section documents boundaries only. None of these systems are implemented by the current runtime.
