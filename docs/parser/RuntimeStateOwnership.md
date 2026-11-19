# Runtime State Ownership

This document is the canonical ownership and authority reference for the current runtime.
It is conservative, documentation-only, and describes current contracts only.

## How to read this document

Use this file for **authority boundaries**: which component owns which decision.

- Canonical here: parse authority, diagnostics authority, parse-tree authority, scheduling ownership, registry ownership, and lifecycle ownership boundaries.
- Canonical in companion file: metadata-only limitations, unsupported semantics, duplicated-work/execution-sharing constraints, and future activation preconditions.
- Companion: [`ParserMetadataAndRuntimeLimitations.md`](./ParserMetadataAndRuntimeLimitations.md).
- Contribution/process rules remain in [`Utils.Parser/AGENTS.md`](../../Utils.Parser/AGENTS.md); roadmap direction remains in [`Utils.Parser/ROADMAP.md`](../../Utils.Parser/ROADMAP.md).

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

## Runtime authority model (current contract)

- `ParserEngine` owns final parse acceptance.
- `ParserEngine` owns final diagnostics.
- `ParserEngine` owns final parse-tree outcome.
- `AlternativeScheduler` is orchestration-only and does not own global parse success/failure.
- `ScheduledAlternativeExecutor` is local and non-authoritative.
- `ParserStateRegistry` is parse-lifecycle-scoped storage and does not own final parse acceptance.
- `ActiveParseState` is descriptive state and is not a runtime invocation frame.

## Supported runtime ownership semantics

Current supported ownership contracts are:

- deterministic syntax-oriented parsing with `ParserEngine` as final authority;
- non-authoritative orchestration and metadata transport;
- advisory lookahead plus parser-authoritative fallback;
- invocation-local registry lifecycle and deterministic reuse boundaries;
- metadata-only continuation/shared-prefix/lookahead observability.

For unsupported semantics and compatibility interpretation (including parseable/observable boundaries), see [`ParserMetadataAndRuntimeLimitations.md`](./ParserMetadataAndRuntimeLimitations.md#compatibility-and-conservative-fallback-boundaries).

## Parsing, scheduling, and local execution boundaries

### Parsing authority

`ParserEngine` remains authoritative for:

- recursive rule parsing decisions,
- branch acceptance/rejection,
- trailing-token validation,
- final parse materialization.

### Scheduling authority

`AlternativeScheduler`:

- coordinates deterministic ordering and local candidate comparison,
- can aggregate observability metadata,
- does not own global parse success/failure,
- does not own diagnostics authority.

`ScheduledAlternativeExecutor`:

- performs local attempt execution,
- can use lookahead for deterministic local reject evidence only,
- cannot accept a branch from lookahead-only outcomes,
- cannot finalize parse acceptance.

## Lookahead authority boundaries (ownership view)

Lookahead is advisory and non-authoritative.

- lookahead cannot independently finalize parse acceptance;
- lookahead cannot independently finalize diagnostics authority;
- lookahead cannot accept a branch;
- non-reject lookahead outcomes require real parsing;
- no adaptive parsing exists in the current runtime.

`ImmediateReject` may be used only as deterministic local reject evidence in allowed contexts.
`RequiresParse`, `Unknown`, and `EpsilonPossible` require parser-authoritative execution.

For conceptual fallback lists and limitations detail, see [`ParserMetadataAndRuntimeLimitations.md`](./ParserMetadataAndRuntimeLimitations.md#lookahead-limitations-and-fallback-to-parse-contract).

## ParserStateRegistry lifecycle ownership

`ParserStateRegistry` owns parse-lifecycle-scoped runtime storage for:

- visited parser-state tracking,
- invocation-local continuation transport metadata,
- invocation-local completed results,
- deterministic reusable completion selection inputs.

Boundaries:

- `Clear()` resets registry-owned state for a new parse lifecycle; `ParserEngine.Parse` clears it both when a parse starts and when it ends (successfully, with an `ErrorNode`, or with an exception), so no parse state outlives its parse (S-06);
- every insertion of a new visited state, continuation, completed result or look-ahead entry is charged to the running parse's `MaxRetainedStates` budget before it is stored; an exhausted budget refuses the whole parse instead of dropping or evicting an entry;
- reusable results are invocation-local completion artifacts;
- reusable result selection is not final parse acceptance;
- invocation reuse != execution reuse;
- current memoization is syntax-oriented and non-semantic;
- semantic evaluator external state is not modeled by memoization keys;
- parser action side effects are not modeled by memoization keys.

## Operation ownership and concurrent use (issue #709)

A `CompiledGrammar` separates what one grammar shares from what one operation owns. Every `Tokenize(string)` and
`Parse(string)` call creates its own `LexerEngine` and `ParserEngine`; the grammar-invariant tables those engines need
are built once by the `CompiledGrammar` constructor (internal `LexerEngine.Preparation` and
`ParserEngine.Preparation`) and are never mutated afterwards.

| State | Owner | Classification | Shared between operations? |
|---|---|---|---|
| `ParserDefinition` (rules, modes, options; immutable arrays and init-only records) | grammar | immutable grammar data | yes |
| `ParserResourceLimits` | grammar | immutable configuration | yes (each operation builds its own budget from it) |
| lexer keyword tries (`KeywordLookup` per mode), candidate rules per mode, case sensitivity | `LexerEngine.Preparation` | precomputed execution metadata, read-only after construction | yes |
| parser scheduling shapes (stable priority order and structural descriptors per alternative list, keyed by list reference) | `ParserEngine.Preparation` | precomputed execution metadata, read-only after construction | yes |
| lexer `_modeStack`, `_moreTextBuilder`, `_moreStartPos`, `_activeRuleRefs` | `LexerEngine` | mutable per-operation state | no: one engine per call |
| lexer resource budget (`ParserResourceBudget`) | tokenization enumeration | mutable per-operation state | no |
| parser `_activeRuleFrames`, `ParserStateRegistry` (visited states, continuations, completed results), `ParserLookaheadCache` | `ParserEngine` | mutable per-operation state | no: one engine per call |
| `AlternativeScheduler`, `ScheduledAlternativeExecutor`, `SchedulingPreparation`, `ParserLookaheadProbe` | `ParserEngine` | per-engine helpers bound to that engine's registry and cache | no |
| parse budget and token position (`ParseContext`) | parse operation | mutable per-operation state | no |
| `LexerEngine.LastResourceBudget`, `ParserEngine.LastResourceUsage`, internal `CompiledGrammar.Lexer` / `Parser` | engine / grammar | diagnostic-only, last operation that started | written by every call; meaningful only in sequential use |
| `ParserRuntimeFeaturePolicy` components | caller | external policy state | yes: the same instances serve every call |

Before #709 a `CompiledGrammar` owned one engine pair, and every call reset the shared engine state: `ParserEngine.Parse`
cleared `_activeRuleFrames`, the state registry and the look-ahead cache at its start and end, and
`LexerEngine.Tokenize` reset the mode stack and pending `more` text. Concurrent calls therefore cleared or reused each
other's state: a memoized completed result computed for another input could be selected as a reusable result (silent
wrong tree or wrong branch selection), visited-state and frame guards could reject or accept alternatives for another
input (wrong acceptance), a mode switch of one tokenization could apply to another (wrong tokens), and concurrent writes
to the non-thread-safe collections could corrupt them permanently (exceptions and wrong results even after the
concurrent phase).

Contract:

- `CompiledGrammar.Tokenize` and `CompiledGrammar.Parse` may be called concurrently on one shared instance, and
  re-entrantly (for example from a lexer action or a predicate of another call on the same grammar), when the runtime
  policy is `ParserRuntimeFeaturePolicy.Default` or another policy whose components are stateless or thread-safe;
- the components of `ParserRuntimeFeaturePolicy.Default` keep no instance state;
- policy components are never cloned; a custom component with mutable state (for example
  `StackParserRuleInvocationFrameManager`, a recording observer, or a policy bound to a generated execution context) is
  shared by every call of the grammar, so concurrent use of such a grammar is supported only when the caller guarantees
  the component is thread-safe; otherwise one `CompiledGrammar` per concurrent operation is required;
- a directly constructed `LexerEngine` or `ParserEngine` remains non-thread-safe and non-re-entrant;
- the CSyntax/VBSyntax front ends inherit the guarantee: `CSyntaxTokenParser`, `VBSyntaxTokenParser`,
  `CSyntaxExpressionCompiler` and `VBSyntaxExpressionCompiler` keep no mutable instance state (a compilation builds its
  own `ParseTreeCompiler` and compilation context), so one instance may serve concurrent calls; an
  `ExpressionCompilerContext` or `VBSyntaxCompilerContext` passed explicitly to `CompileExpression` is read and mutated by
  that compilation and must not be shared by concurrent compilations;
- no lock, thread-local engine or pool is used; resource budgets stay owned by each operation (S-06).

## Continuation and shared-prefix ownership boundaries

Ownership view only (limitations remain canonical in companion file):

- continuation metadata is metadata-only;
- shared-prefix metadata is metadata-only;
- lookahead metadata is metadata-only;
- metadata transport is non-authoritative and discardable.

Mandatory boundaries:

- metadata does not authorize replay;
- metadata does not authorize continuation execution;
- metadata does not authorize branch merging;
- metadata does not establish semantic equivalence;
- metadata does not imply rollback safety;
- metadata does not override diagnostics ownership.

For lifecycle details, anti-feature boundaries, and future activation preconditions, see:

- [`ParserMetadataAndRuntimeLimitations.md#continuation-metadata-model-current-metadata-only`](./ParserMetadataAndRuntimeLimitations.md#continuation-metadata-model-current-metadata-only)
- [`ParserMetadataAndRuntimeLimitations.md#shared-prefix-metadata-authority-limitations-and-lifecycle`](./ParserMetadataAndRuntimeLimitations.md#shared-prefix-metadata-authority-limitations-and-lifecycle)
- [`ParserMetadataAndRuntimeLimitations.md#execution-sharing-safety-boundaries-limitations-view`](./ParserMetadataAndRuntimeLimitations.md#execution-sharing-safety-boundaries-limitations-view)

## Diagnostics authority boundaries (canonical)

- `ParserEngine` is final diagnostics authority.
- Orchestration diagnostics are non-authoritative.
- pruning != syntax failure.
- backtracking observation != syntax failure.
- metadata transport != diagnostics authority.
- branch equivalence != parse rejection.
- trailing-token validation is parser-authoritative.

Observability can exist without authority transfer.

## Deterministic observability boundary

Observable runtime artifacts may include scheduler metadata, lookahead observations, continuation/shared-prefix metadata, and branch-state collections.
These are testable for auditability but remain non-authoritative.

Incidental internal ordering/layout remains non-contractual unless explicitly documented.

## Explicit non-goals (current runtime)

No support for:

- replay,
- rollback,
- execution sharing,
- branch-merge execution,
- resumable continuation execution,
- semantic-aware memoization.

These boundaries are current-contract statements only.


## Runtime observation

The optional `IParserRuntimeObserver` infrastructure is passive and descriptive only. Observer callbacks do not return control decisions and cannot influence scheduling, pruning, parse acceptance, parse-tree shape, or diagnostics authority.

Additional observation contract clarifications:

- runtime observation ordering is deterministic for a given deterministic parser run;
- event payloads are immutable descriptive snapshots with explicit event kind and status, not execution handles;
- observer callback exceptions are isolated by the scheduler and do not alter parser runtime outcomes.
