# Utils.Parser Roadmap

## Purpose

Utils.Parser is evolving toward a modern, ANTLR4-like parsing framework and tooling platform through conservative, incremental, and auditable steps.

This roadmap is authoritative for project direction and must be updated whenever meaningful architectural, runtime, metadata, or tooling changes are introduced.

## Long-term vision

The long-term target is a general parsing framework that can support:

- ANTLR4-like grammar support,
- grammar conversion and compilation,
- code generation,
- syntax highlighting,
- IDE/editor tooling,
- diagnostics tooling,
- parser runtime experimentation.

Long-term research possibilities may eventually include:

- shared lookahead execution,
- continuation-driven parsing,
- reduction of duplicated parsing work,
- forward-only parsing modes,
- potential parallel exploration of alternatives.

These are long-term possibilities only. They are not current runtime features.

## Current runtime philosophy

The current runtime prioritizes:

- determinism,
- auditability,
- compatibility,
- conservative execution,
- observable behavior preservation,
- parse-tree stability,
- diagnostic stability,
- incremental PR discipline.

At this stage, the runtime is intentionally not a speculative graph parser.

## Current runtime state

Current capabilities and responsibilities:

- `ParserEngine` remains the final parse authority.
- `AlternativeScheduler` is the sequential orchestration layer.
- `ScheduledAlternativeExecutor` performs local alternative execution.
- `ParserStateRegistry` manages visited states, completed invocation reuse, and metadata.
- `ActiveParseState` provides local descriptive branch state.
- `ParserLookaheadProbe` and `ParserLookaheadCache` provide conservative shallow lookahead.
- Runtime feature policies are present.
- Passive runtime observation hooks are available via policy configuration and remain non-authoritative.
- Semantic predicate evaluator abstraction is present.
- Parser action executor abstraction is present.
- Continuation metadata is present.
- Shared-prefix metadata is present.
- Parser feature capabilities metadata is present.
- ANTLR4 grammar bootstrap/conversion support is present.
- Runtime invariant documentation exists.
- Branch outcome documentation exists.
- Parser tests are organized to reflect runtime contracts.

Clarifications that must remain true:

- `ParserEngine` owns final parse acceptance.
- `ParserEngine` owns final diagnostics.
- `ParserEngine` owns final parse-tree outcome.
- `AlternativeScheduler` does not own global parse success/failure.
- `ActiveParseState` is not a runtime invocation frame.
- Continuations are metadata-only.
- Shared-prefix infrastructure is metadata-only.
- Runtime observers are descriptive only and do not control scheduling, pruning, parse acceptance, parse-tree outcomes, or diagnostics authority.

## Explicit non-goals

The following must not be introduced prematurely:

- no `ParserEngine2`,
- no public API break,
- no parse-tree shape break,
- no diagnostic format break,
- no speculative execution,
- no parser graph execution,
- no GLL or adaptive LL runtime,
- no continuation replay,
- no rollback,
- no semantic-state-aware memoization,
- no async runtime,
- no runtime parallelism,
- no action buffering,
- no hidden semantic state,
- no large unreviewable refactors.

## Architectural principles

- Preserve observable behavior.
- Document every new invariant.
- Add invariant tests for runtime changes.
- Prefer small, reviewable PRs.
- Separate documentation-only, test-only, refactor-only, and functional PRs.
- Never activate prepared metadata accidentally.
- Keep metadata-only infrastructure visibly metadata-only.
- Avoid wrappers and abstractions unless they remove real ambiguity.
- Do not introduce architecture layers without a clear invariant-driven reason.
- Optimize only after behavior is locked down.

## Roadmap phases

### Phase 0 — Stabilization and invariant documentation

Status: mostly complete, with ongoing maintenance required.

Scope of completed/recently consolidated work:

- runtime memoization invariants,
- runtime state ownership,
- lifecycle contracts,
- branch outcome model,
- parser test suite normalization.

Ongoing expectation:

- keep documentation and tests synchronized with invariant changes.

### Phase 1 — Alternative selection contract hardening

Goal: document and lock the current alternative selection model before changing it.

Scope:

- branch equivalence,
- branch selection,
- pruning eligibility,
- precedence interaction,
- completed-state comparison,
- local outcome prioritization,
- diagnostics around pruning and backtracking.

Current clarification status:

- local alternative comparison order is explicitly documented (longest match, then priority, then declaration order),
- pruning is explicitly documented as orchestration-only and non-syntax-authoritative,
- structural branch equivalence limits are explicitly documented as conservative and non-semantic-proof.
- passive runtime observation is available for tooling/audit visibility and is explicitly non-authoritative (no control over scheduling, pruning, parse acceptance, parse-tree outcomes, or diagnostics authority).

Allowed work:

- comments,
- documentation,
- tests,
- minimal helper extraction.

Forbidden work:

- changing selection algorithms,
- changing pruning rules,
- changing precedence behavior,
- introducing selection policies.

### Phase 2 — Diagnostics model consolidation

Goal: make diagnostics ownership and propagation more explicit.

Scope:

- local diagnostics,
- global diagnostics,
- diagnostics emitted during failed branches,
- diagnostics from pruning,
- diagnostics for unsupported ANTLR4 constructs,
- stability of diagnostic codes and format.

Allowed work:

- documentation,
- tests,
- small internal cleanup.

Forbidden work:

- changing diagnostic format,
- changing observable diagnostic behavior without an explicit bug-fix PR.

Current clarification status:

- diagnostics ownership and authority boundaries are explicitly documented,
- local vs global diagnostics lifecycle is explicitly documented,
- orchestration diagnostics (pruning/backtracking) are explicitly separated from engine-authoritative parse diagnostics,
- compatibility diagnostics are explicitly documented as independent from parse success/failure.
- unsupported ANTLR4 lexer-command constructs are now surfaced via explicit deterministic compatibility diagnostics.

### Phase 3 — Lookahead contract consolidation

Goal: keep lookahead shallow, conservative, and syntax-oriented while clarifying future options.

Scope:

- `ParserLookaheadProbe`,
- `ParserLookaheadCache`,
- negative shortcut rules,
- epsilon handling,
- parser rule references,
- predicates/actions exclusion,
- case-insensitive matching.

Allowed work:

- tests,
- documentation,
- internal cleanup.

Forbidden work:

- deep parser-rule speculative lookahead,
- evaluating predicates during lookahead,
- executing actions during lookahead.

Current clarification status:

- lookahead ownership boundaries are explicit (`ParserLookaheadProbe` / `ParserLookaheadCache` / engine authority),
- advisory vs authoritative lookahead outcomes are explicitly documented,
- fallback-to-parse behavior is documented for `RequiresParse`, `Unknown`, and ambiguous/epsilon-sensitive outcomes,
- cache semantics are explicit as metadata-only and non-authoritative,
- invariant tests cover conservative behavior for parser-rule references, predicates/actions, and ambiguous shallow outcomes.

### Phase 4 — Shared-prefix metadata maturity

Goal: improve shared-prefix metadata quality without activating execution.

Scope:

- detection,
- plans,
- formatting,
- validation,
- metadata scenarios,
- ambiguity documentation.

Allowed work:

- metadata tests,
- metadata validation,
- documentation.

Current clarification status:

- shared-prefix metadata is explicitly documented as descriptive and non-authoritative,
- metadata ownership and lifecycle boundaries are explicitly documented,
- non-authority guarantees are explicit (no replay, no continuation execution, no branch merging, no semantic-equivalence guarantee),
- future activation prerequisites are documented as boundaries only.

Forbidden work:

- shared-prefix execution,
- branch replay,
- continuation resume,
- action replay.

### Phase 5 — Continuation metadata maturity

Goal: clarify and mature continuation metadata as descriptive infrastructure only.

Scope:

- continuation identity,
- continuation formatting,
- ownership,
- relation to `ActiveParseState`,
- relation to registry metadata.

Allowed work:

- metadata tests,
- documentation,
- simplification.

Forbidden work:

- continuation replay,
- runtime resume,
- invocation frame model.

### Phase 6 — ANTLR4 compatibility expansion

Goal: progressively improve ANTLR4 grammar compatibility.

Scope may include:

- grammar parsing coverage,
- lexer commands,
- parser commands,
- labels,
- predicates,
- actions,
- parameters and returns,
- imports,
- `tokenVocab`,
- modes,
- options,
- compatibility matrix updates.

Important constraint:

- runtime support must stay clearly separated from metadata preservation.

Allowed work:

- converter support,
- diagnostics for unsupported constructs,
- compatibility matrix updates,
- tests.

Forbidden work:

- pretending unsupported runtime semantics are supported,
- silently ignoring meaningful constructs without diagnostics.

### Phase 7 — Code generation and tooling

Goal: move toward tooling capabilities once runtime behavior is stable.

Scope:

- grammar-derived metadata,
- code generation experiments,
- syntax highlighting,
- Visual Studio / IDE support,
- parse-tree tooling,
- diagnostics tooling.

Allowed work:

- tooling layers,
- non-runtime infrastructure,
- generated artifacts when isolated.

Forbidden work:

- destabilizing `ParserEngine`,
- tying IDE tooling to unstable runtime internals.

### Phase 8 — Future runtime research gates

Goal: define prerequisites before any major runtime evolution.

Potential future research areas:

- shared lookahead execution,
- duplicated-work reduction,
- continuation-driven parsing,
- forward-only parsing,
- parallel alternative exploration.

These are not approved runtime features today.

Before any such work, require:

- complete invariant documentation,
- explicit design document,
- dedicated tests,
- migration plan,
- rollback/side-effect policy,
- semantic predicate policy,
- parser action policy,
- diagnostic compatibility plan,
- parse-tree compatibility plan,
- small experimental branch or prototype when needed.

## Required update policy

This roadmap must be updated when any PR changes:

- runtime behavior,
- parser scheduling,
- memoization,
- diagnostics,
- parse-tree shape,
- metadata semantics,
- ANTLR4 compatibility,
- parser feature capabilities,
- runtime policies,
- test coverage strategy,
- tooling direction.

Roadmap updates should be in the same PR unless there is a clearly stated reason not to.

## Definition of done for future runtime PRs

Future runtime PRs should include:

- behavior summary,
- invariant impact,
- compatibility impact,
- diagnostics impact,
- parse-tree impact,
- tests,
- documentation updates,
- roadmap update when direction changes.

## Current safety summary

The runtime currently remains conservative and deterministic. Metadata-rich infrastructure exists, but it is not execution authority. No replay, rollback, semantic-state-aware memoization, graph execution, async parsing, or parallel parsing exists today.
