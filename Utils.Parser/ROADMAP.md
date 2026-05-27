# Utils.Parser Roadmap

## Purpose

Utils.Parser is evolving toward a modern, ANTLR4-like parsing framework and tooling platform through conservative, incremental, and auditable steps.

This roadmap is authoritative for project direction and must be updated whenever meaningful architectural, runtime, metadata, tooling, or public API changes are introduced.

## Public API maturity policy

`Utils.Parser` is currently considered pre-release.

Until an explicit API stabilization milestone is declared:

- public API changes are allowed;
- compatibility preservation is preferred but not mandatory;
- reducing API debt is preferred over preserving accidental contracts;
- API changes must remain explicit, documented, and reviewable.

Public API evolution must not be used to justify changes to runtime authority, parse-tree compatibility, diagnostics format, or unsupported execution semantics.

## Explicit non-goals

The following must not be introduced prematurely:

- no `ParserEngine2`,
- no undocumented public API break,
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

## Public API change rules

Public API changes are allowed only when at least one of the following applies:

- remove architectural debt;
- clarify ownership or responsibility boundaries;
- simplify usage patterns;
- eliminate temporary abstractions;
- prepare future stabilized APIs.

Every API-changing PR must include:

- explicit API surface summary;
- migration notes when applicable;
- compatibility impact assessment;
- documentation impact statement;
- tests for public behavior.

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
- public API shape,
- runtime policies,
- test coverage strategy,
- tooling direction.

## Current safety summary

The runtime currently remains conservative and deterministic. Metadata-rich infrastructure exists, but it is not execution authority. Public APIs may evolve while the project remains pre-release; runtime execution guarantees remain conservative.