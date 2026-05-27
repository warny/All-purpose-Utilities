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
- test strategy,
- roadmap sequencing.

## Runtime safety rules

Agents must not introduce:

- `ParserEngine2`,
- speculative execution,
- graph parsing,
- adaptive LL / GLL,
- continuation replay,
- rollback,
- semantic runtime state,
- semantic-state-aware memoization,
- async parser runtime,
- runtime parallelism,
- public API breaks,
- parse-tree shape breaks,
- diagnostic format breaks.

Any exception requires a future roadmap phase to explicitly allow it and a dedicated design in the PR.

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
- parse-tree shape.

## PR discipline

PRs must be small, single-purpose, auditable, and explicit about whether they are documentation-only, test-only, refactor-only, or behavior-changing.

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
- no unsupported runtime feature was accidentally introduced.
