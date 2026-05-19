# Parser Metadata and Runtime Limitations

## Purpose

This file is the canonical **limitations-first** reference for metadata-only semantics, unsupported runtime semantics, and future activation preconditions.

## How to read this document

Use this file for **what is not runtime-authoritative today** and what requires explicit future design.

- Canonical here: metadata-only semantics, unsupported semantics, compatibility boundaries, execution-sharing constraints, and activation preconditions.
- Canonical in companion file: parse authority, diagnostics authority, parse-tree authority, scheduling ownership, and registry authority boundaries.
- Companion: [`RuntimeStateOwnership.md`](./RuntimeStateOwnership.md).
- Process obligations remain in [`AGENT.md`](../../AGENT.md); roadmap direction remains in [`ROADMAP.md`](../../ROADMAP.md).

## Topic ownership map (canonical + companion)

| Topic | Canonical document | Companion summary |
|---|---|---|
| Runtime authority and component ownership | RuntimeStateOwnership.md | ParserMetadataAndRuntimeLimitations.md |
| Metadata-only limitations | ParserMetadataAndRuntimeLimitations.md | RuntimeStateOwnership.md |
| Diagnostics authority | RuntimeStateOwnership.md | ParserMetadataAndRuntimeLimitations.md |
| Lookahead fallback semantics | ParserMetadataAndRuntimeLimitations.md | RuntimeStateOwnership.md |
| Registry lifecycle ownership | RuntimeStateOwnership.md | ParserMetadataAndRuntimeLimitations.md |
| Duplicated-work/execution-sharing constraints | ParserMetadataAndRuntimeLimitations.md | RuntimeStateOwnership.md |
| Compatibility boundaries | ParserMetadataAndRuntimeLimitations.md | RuntimeStateOwnership.md |

## Compatibility and conservative fallback boundaries

### Supported runtime semantics (current compatibility guarantees)

Current runtime support remains conservative:

- deterministic syntax-oriented parser-authoritative execution;
- non-authoritative orchestration observability;
- advisory lookahead with parse-required fallback for non-reject outcomes;
- invocation-local metadata/reuse lifecycle and deterministic discardability;
- deterministic diagnostics ownership boundaries.

### Unsupported runtime semantics (no compatibility guarantee)

Current runtime does not support:

- semantic-aware memoization;
- adaptive parsing;
- parser replay;
- rollback parsing;
- branch merge execution;
- resumable continuation execution;
- semantic-aware pruning;
- speculative parser execution;
- execution sharing.

Unsupported semantics have no compatibility guarantee.
Implementation accident != supported contract.

### Parseable/observable boundary

- parseable or observable != supported compatibility contract.
- Unsupported semantics have no compatibility guarantee.
- Future support requires explicit runtime contracts, diagnostics expectations, tests, documentation, and roadmap updates.

## Lookahead limitations and fallback-to-parse contract

Lookahead remains metadata-only and advisory.

- lookahead is advisory;
- lookahead cannot independently finalize parse acceptance;
- lookahead cannot independently finalize diagnostics authority;
- lookahead cannot accept a branch;
- no adaptive parsing exists.

Fallback boundary:

- `ImmediateReject` may be used only as deterministic local reject evidence in allowed contexts;
- `RequiresParse`, `Unknown`, and `EpsilonPossible` require real parsing;
- ambiguous or parser-rule-dependent outcomes require real parsing;
- cached lookahead entries are advisory metadata only.

For authority ownership, see [`RuntimeStateOwnership.md#lookahead-authority-boundaries-ownership-view`](./RuntimeStateOwnership.md#lookahead-authority-boundaries-ownership-view).

## Memoization and registry limitations view

Authority remains in [`RuntimeStateOwnership.md`](./RuntimeStateOwnership.md#parserstateregistry-lifecycle-ownership). This section captures limitations:

- registry state is parse-lifecycle scoped;
- `Clear()` resets registry-owned state;
- reusable results are invocation-local completion artifacts;
- reusable result selection is not final parse acceptance;
- invocation reuse != execution reuse;
- current memoization is syntax-oriented and non-semantic;
- semantic evaluator external state is not modeled by memoization keys;
- parser action side effects are not modeled by memoization keys.

Registry cleanup is discardability-oriented, with no semantic rollback model.

## Diagnostics limitations view

Diagnostics authority is owned by `ParserEngine` (canonical in [`RuntimeStateOwnership.md`](./RuntimeStateOwnership.md#diagnostics-authority-boundaries-canonical)).

Limitation boundaries:

- pruning != syntax failure;
- backtracking observation != syntax failure;
- metadata transport != diagnostics authority;
- branch equivalence != parse rejection;
- trailing-token validation is parser-authoritative.

Orchestration/pruning/backtracking observations are non-authoritative even when observable.


## Runtime policy boundaries (semantic predicates and actions)

`ParserEngine` delegates semantic predicates and embedded actions to `ParserRuntimeFeaturePolicy`.

- Default policy remains conservative (`NotEvaluated` / `NotExecuted`).
- Custom policies may evaluate predicates or execute actions.

Runtime limitation boundaries remain explicit:

- actions may execute in branches that are later rejected or pruned;
- no rollback is provided for external action side effects;
- no exactly-once guarantee exists across backtracked attempts;
- no transactional isolation exists across competing alternatives.

## ANTLR parameters/returns: parsed metadata vs runtime support

ANTLR4-style parameter and `returns` signatures are parsed and preserved as metadata text.

Current runtime does **not** provide:

- invocation-frame parameter binding,
- return-value propagation,
- runtime parameter/return scopes,
- semantic type-resolution for parameter/return runtime semantics.

So: parsed/preserved metadata != supported runtime invocation semantics.

## Shared-prefix pipeline (metadata production only)

Shared-prefix infrastructure is analysis/observability metadata only.
The current metadata production path is:

`ParserLookaheadProbe -> ParserLookaheadSharedPrefixDetector -> ParserContinuationFactory -> ParserSharedPrefixPlanFactory`

Validation/inspection components remain descriptive and non-authoritative.

## Capability model note

`ParserFeatureCapabilities` is descriptive metadata for support visibility.
It is not used to alter parse authority, parsing behavior, or diagnostics authority.


## Continuation metadata model (current, metadata-only)

Continuation metadata is metadata-only and non-authoritative.

- continuation metadata does not authorize replay;
- continuation metadata does not authorize continuation execution;
- continuation metadata does not authorize resumable parsing;
- continuation metadata does not authorize branch merging;
- continuation metadata does not establish semantic equivalence;
- continuation metadata does not imply rollback safety;
- continuation metadata is discardable.

No continuation execution runtime is implemented.

## Shared-prefix metadata authority, limitations, and lifecycle

Shared-prefix infrastructure remains metadata-only.

- shared-prefix grouping != execution sharing;
- grouping != semantic equivalence;
- grouping != branch merge permission;
- shared-prefix metadata is discardable;
- no shared-prefix execution exists.

No parser-graph execution, replay, or rollback model is provided by shared-prefix metadata.

## Duplicated-work-reduction constraint model (limitations view)

Current metadata may support future analysis/preparation only.
It does not activate execution-sharing semantics.

- duplicated-work reduction is analysis/preparation only;
- no replay;
- no rollback;
- no execution sharing;
- no branch merge execution;
- no resumable continuation execution;
- no semantic-aware memoization;
- no semantic-state ownership model is currently provided.

## Execution-sharing safety boundaries (limitations view)

Invalid assumptions in current runtime:

- metadata grouping != execution sharing,
- reusable completion != branch replay,
- invocation reuse != execution reuse,
- continuation transport != resumable execution,
- deterministic observability != semantic equivalence.

### Future activation preconditions

Any future activation requires explicit future design and dedicated tests, including:

- semantic-state ownership,
- replay safety,
- rollback guarantees,
- merge semantics,
- diagnostics ownership resolution,
- parse-tree authority preservation,
- explicit compatibility contracts and documentation.

These are boundary-only preconditions, not active capabilities.

### Future execution-oriented PR review gates

Any future execution-oriented PR must show, at minimum:

1. which invariants are preserved and how equivalence was validated;
2. why diagnostics content/ordering remain equivalent;
3. why parse-tree shape remains equivalent;
4. which unsupported cases remain rejected by default;
5. a rollback path to baseline behavior.

## Compatibility matrix reference

For feature-level support status, see:

- [ANTLR4 Compatibility Matrix](./Antlr4CompatibilityMatrix.md)
