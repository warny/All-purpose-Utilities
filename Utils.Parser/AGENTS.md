# Agent Instructions

## Scope

This file applies to all automated or assisted coding agents working on this repository, with special attention to `Utils.Parser` and its runtime-related documentation and tests.

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

## Roadmap phase status

Each phase in `ROADMAP.md` must carry an explicit status line immediately after its heading:

- `**Status: not started.**` — no work has begun.
- `**Status: in progress.**` — work is ongoing; optionally note what has been done.
- `**Status: complete.**` — all scope items are done and documented.
- `**Status: mostly complete. Ongoing maintenance required.**` — for phases with open-ended maintenance obligations (e.g. Phase 0).

When a PR completes the last remaining item of a phase, that PR must change the phase status to `complete`.
When a PR begins work on a phase that was `not started`, that PR must change the status to `in progress`.

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

## ANTLR4 compatibility reference

`docs/parser/ANTLRCompatibility.md` is the authoritative reference for ANTLR4 feature support in Utils.Parser.

Agents must:

- **consult `docs/parser/ANTLRCompatibility.md` before modifying any grammar-related component** (grammar converter, lexer engine, parser engine, model, resolution, diagnostics);
- **update `docs/parser/ANTLRCompatibility.md` after any change that adds, removes, or alters support for an ANTLR4 feature**, including moving a feature from "not supported" to "partially supported", or from "parsed but not executed" to "supported";
- document, in the relevant section, **how the feature works when its behaviour differs from standard ANTLR4** (usage examples, API hooks, known constraints).


## Parser documentation index

Before editing parser documentation, agents must read `docs/parser/INDEX.md` and update it in the same PR when any document under `docs/parser/` is added, removed, or materially changed.

## Documentation requirements

For runtime-affecting PRs, agents must update relevant documentation, including:

- `ROADMAP.md`,
- `docs/parser/ANTLRCompatibility.md`,
- `docs/parser/RuntimeStateOwnership.md`,
- `docs/parser/ParserMetadataAndRuntimeLimitations.md`,
- `docs/parser/Antlr4CompatibilityMatrix.md`,
- relevant test documentation/comments.

## Testing requirements

Agents must add or update tests when modifying:

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

Tests should be:

- focused,
- deterministic,
- audit-friendly,
- minimally coupled to internals unless explicitly shape-locking behavior.

## PR discipline

PRs must be:

- small,
- single-purpose,
- auditable,
- explicit about whether they are:
  - documentation-only,
  - test-only,
  - refactor-only,
  - behavior-changing.

Behavior-changing PRs must explicitly document:

- observable behavior changes,
- compatibility risks,
- diagnostics impact,
- parse-tree impact,
- roadmap impact.

## Metadata-only rule

The existence of metadata does not imply runtime support.

Continuation metadata, shared-prefix metadata, lookahead metadata, and feature capabilities must not be interpreted as execution authority.

Agents must not activate metadata execution paths accidentally.

## Conservative default

When uncertain, agents must prefer:

- documentation,
- tests,
- comments,
- small refactors,
- explicit invariants,

over introducing new runtime behavior.

## Final instruction

Before completing any PR, verify:

- `ROADMAP.md` is still accurate,
- `AGENT.md` rules were followed,
- relevant parser docs were updated,
- tests cover new or clarified invariants,
- no unsupported runtime feature was accidentally introduced.
