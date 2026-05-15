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

## Documentation requirements

For runtime-affecting PRs, agents must update relevant documentation, including:

- `ROADMAP.md`,
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
