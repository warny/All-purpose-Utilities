# Agent Instructions

## Scope

This file applies to all automated or assisted coding agents working on `Utils.Parser`, including runtime code, ANTLR grammar ingestion, diagnostics, tests, and parser documentation.

## Mandatory reading order

Before making any `Utils.Parser` change, read these documents in order:

1. `Utils.Parser/AGENTS.md`
2. `Utils.Parser/ROADMAP.md`
3. `docs/parser/INDEX.md`
4. `docs/parser/ANTLRCompatibility.md`
5. any document referenced by `docs/parser/INDEX.md` that is relevant to the change

Documentation is authoritative. Do not infer support status from code alone when the roadmap or compatibility reference states a boundary.

## Mandatory roadmap maintenance

Every meaningful change must include a check of whether `ROADMAP.md` needs to be updated.

`ROADMAP.md` must be updated in the same PR when changing:

- parser runtime behavior,
- scheduling,
- memoization,
- diagnostics,
- parse-tree shape,
- metadata semantics,
- ANTLR4 compatibility,
- parser capabilities,
- public API shape,
- runtime policies,
- tooling direction,
- test architecture.

If no roadmap update is required, the PR description must explicitly explain why.

Each roadmap phase must carry an explicit status line immediately after its heading:

- `**Status: not started.**`
- `**Status: in progress.**`
- `**Status: complete.**`
- `**Status: mostly complete. Ongoing maintenance required.**`

When a PR completes the last remaining item of a phase, update the phase status to `complete`. When a PR begins work on a phase that was `not started`, update it to `in progress`.

## ANTLR4 compatibility reference

`docs/parser/ANTLRCompatibility.md` is the authoritative reference for ANTLR4 feature support in `Utils.Parser`.

Agents must:

- consult `docs/parser/ANTLRCompatibility.md` before modifying grammar-related components: grammar converter, lexer engine, parser engine, model, resolution, diagnostics, generator metadata, or grammar tests;
- update it after any change that adds, removes, or alters support for an ANTLR4 feature;
- update it after any change that affects compatibility diagnostics, metadata semantics, runtime/generator parity, or intentional divergences;
- document how the feature works when behavior differs from standard ANTLR4.

If no compatibility-reference update is required, the PR description must explicitly explain why.

## Parser documentation index

Before editing parser documentation, read `docs/parser/INDEX.md`.

Update `docs/parser/INDEX.md` in the same PR when any document under `docs/parser/` is added, removed, moved, renamed, or materially changed.

## Documentation impact statement

Every PR description must include a documentation impact statement covering:

- `ROADMAP.md` updated, or why no update was required;
- `docs/parser/ANTLRCompatibility.md` updated, or why no update was required;
- `docs/parser/INDEX.md` updated when parser docs were added, moved, removed, renamed, or materially changed.

Before implementation, identify whether the change alters:

- parser behavior,
- diagnostics,
- ANTLR4 compatibility,
- runtime metadata,
- runtime policy,
- public API shape,
- test strategy,
- roadmap sequencing.


## Embedded parser code

Do not add target-language-specific parsing or rewriting logic to the parser/generator core. Embedded code is raw target-language code by default.

Any transformation of embedded code must go through `IParserEmbeddedCodeTransformer`. The default transformer is `NoOpParserEmbeddedCodeTransformer` and returns embedded code unchanged.

If ANTLR-style `$...` conveniences are needed, implement or use a target-language-specific transformer, for example a C# transformer. Keep it optional and isolated.

Do not add new `$...` semantics directly to `GrammarEmitter`, `ParserEngine`, runtime frame classes, or source generator core logic.

ANTLR-style local writes are implemented only in the optional C# transformer. Do not move this logic into parser/generator core.

ANTLR-style return writes are implemented only in the optional C# transformer. Runtime support must use parser-managed frame return state and must not add target-language rewrite logic to parser/generator core.

Parser named actions are parser-side only unless explicitly implemented for lexer code. Keep `@parser::members` emission inside the generated parser execution context/class. Do not move target-language rewriting into parser/generator core. ANTLR-style `$...` current-rule attribute rewriting applies to parser actions/lifecycle code only, not parser members/header/footer content.

Dynamic embedded code must be transformed before being passed to the existing compiler/preparer mechanism. Do not introduce a parallel compiler abstraction.

## Parser architecture boundaries

- Parser core parses grammar and builds the runtime model.
- Generator emits target code from the model.
- Runtime manages parser state, rollback, rule frames, parameters, locals, returns, and labeled call results.
- Embedded target-language code is external to parser semantics.
- External side effects from embedded code are not automatically rolled back.

## Embedded-code documentation rule

This is an ongoing maintenance rule, not a completed one-time audit task. Every future change to
embedded-code behavior or architectural boundaries must review and assess:

- `Utils.Parser/ROADMAP.md`
- `docs/parser/ANTLRCompatibility.md`
- `docs/parser/EmbeddedCodeExecutionModel.md`
- `docs/parser/EmbeddedCodeTransactionalState.md`
- `docs/parser/Antlr4CompatibilityMatrix.md`
- `Utils.Parser.Generators/README.md`

Each affected PR must update the documents impacted by the change, explicitly identify those
updates, and justify why each other reviewed document did not require modification.

## Runtime safety rules

Agents must not introduce, outside the explicitly documented parser-managed mechanisms:

- a new parser engine (`ParserEngine2`);
- speculative execution;
- graph parsing;
- adaptive LL / GLL;
- continuation replay;
- new rollback semantics;
- new semantic runtime state ownership;
- new semantic-state-aware memoization rules;
- async parser runtime;
- runtime parallelism;
- unreviewed or undocumented public API breaks;
- parse-tree shape breaks;
- diagnostic format breaks.

Existing parser-managed rollback, execution-state snapshots, generated execution-context state keys, and state-aware memoization are allowed only within the boundaries documented in the parser compatibility and embedded-code documents. Extending those mechanisms requires a dedicated roadmap entry or design discussion in the PR.

External side effects performed by embedded actions remain outside parser-managed rollback and must not be presented as rollback-safe.

Public API changes are allowed while `Utils.Parser` remains pre-release and has no committed external compatibility contract, provided they follow the public API change policy below.

## Public API change policy

Public API changes are allowed when they improve architecture, remove misleading contracts, reduce pre-release API debt, or make ownership boundaries clearer.

Public API changes must remain:

- small,
- single-purpose,
- explicitly documented in the PR description,
- covered by deterministic tests when behavior or public usage changes,
- accompanied by migration notes when a caller-visible replacement exists.

Public API changes must not be used as a reason to introduce unsupported runtime semantics, speculative execution, metadata execution authority, parse-tree shape breaks, or diagnostic format breaks.

## Metadata-only rule

The existence of metadata does not imply runtime support.

Continuation metadata, shared-prefix metadata, lookahead metadata, feature capabilities, ANTLR prequel metadata, and neutral validation facts must not be interpreted as execution authority.

Agents must not activate metadata execution paths accidentally.

## Testing requirements

Agents must add or update deterministic, audit-friendly tests when modifying:

- `ParserEngine`,
- `AlternativeScheduler`,
- `ScheduledAlternativeExecutor`,
- `ParserStateRegistry`,
- `ParserLookaheadProbe`,
- `ParserLookaheadCache`,
- `ActiveParseState`,
- semantic predicate behavior,
- parser action behavior,
- ANTLR4 conversion,
- diagnostics,
- parse-tree shape,
- public API shape or usage contracts.

## PR discipline

PRs must be small, single-purpose, auditable, and explicit about whether they are documentation-only, test-only, refactor-only, API-changing, or behavior-changing.

API-changing PRs must explicitly document the public surface changed, the reason for the change, compatibility impact, migration guidance, and why the change should happen before API stabilization.

Behavior-changing PRs must explicitly document observable behavior changes, compatibility risks, diagnostics impact, parse-tree impact, and roadmap impact.

## Conservative default

When uncertain, prefer documentation, tests, comments, small refactors, and explicit invariants over new runtime behavior.

## Final checklist

Before completing any PR, verify:

- `ROADMAP.md` is still accurate and updated or explicitly justified;
- `docs/parser/ANTLRCompatibility.md` is still accurate and updated or explicitly justified;
- `docs/parser/INDEX.md` is updated when parser documentation changed;
- relevant parser docs were updated;
- tests cover new or clarified invariants;
- public API changes are documented when present;
- no unsupported runtime feature was accidentally introduced.

## Parser named actions

- Grammar-level named-action classification must go through the shared internal helper. Do not duplicate `@header`/`@members`/`@footer` parser/lexer support rules in `GrammarEmitter` and `Antlr4GrammarGenerator`.
- Parser header/member/footer are source-generator C# compatibility only.
- Lexer grammar-level named actions are limited to `@lexer::header`, `@lexer::members`, and `@lexer::footer` source-generator C# injection. Simple lexer inline actions and simple lexer predicates are supported only in the explicit generated-C# opt-in path; lexer predicates remain separate from parser predicates and lexer actions.
- `@parser::members` and `@lexer::members` are emitted into the generated execution context only.
- `$...` current-rule rewriting is rule-bound and must not run for parser header/member/footer content.


## Lexer embedded-code attribute rewriting

Lexer `$...` conveniences must follow the same optional-transformer model as parser attributes. Keep lexer attribute rewriting in the C# embedded-code transformer and dedicated rewriter code, not in `LexerEngine`, `ParserEngine`, or `ParserRuntimeFeaturePolicy`. Supported generated-C# lexer action reads are intentionally narrow (`$text`, `$type`, `$channel`, `$mode`, `$line`, `$pos`); `$text` must continue to mean `LexerActionExecutionContext.Text` read through generated helpers, not a guaranteed fragment-local slice, and `$pos` must remain documented as 1-based `SourceSpan.Column` rather than full ANTLR `charPositionInLine` compatibility. Writes and lexer predicate attributes must remain deterministic diagnostics unless a documented runtime-neutral action-result model is added.
