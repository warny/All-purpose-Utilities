# Shared Look-Ahead Preparation

## Purpose

This document defines the current **shared-prefix identification metadata** prepared during grammar/runtime metadata preparation.

The intent is to prepare future duplicated-work reduction research while preserving the current parser runtime contract.

> Shared-prefix metadata does not change parser execution.

## Scope and boundaries

Current shared-prefix preparation is:

- deterministic;
- conservative;
- structural;
- advisory metadata only.

Current shared-prefix preparation is **not**:

- shared-prefix execution;
- parser graph execution;
- continuation replay;
- scheduler control;
- parse-tree control;
- diagnostics authority.

## Metadata semantics

Shared-prefix metadata means only:

- participating alternatives begin with structurally similar token/reference prefixes;
- prefix depth can be described in ordered structural token form.

Shared-prefix metadata does **not** mean:

- alternatives are semantically equivalent;
- alternatives are interchangeable;
- one alternative can be executed on behalf of another.

## Structural token preparation

Structural token sequences are computed by `AlternativeStructuralPrefixExtractor` during grammar-level preparation,
**before** scheduling begins.

The extractor inspects grammar model objects (`RuleContent`, `Sequence`, `RuleRef`, `LiteralMatch`) and produces
lightweight `AlternativeStructuralDescriptor` records carrying only token name strings.
These descriptors are forwarded to `ParserSharedPrefixPlanFactory`, which aggregates them without accessing
grammar model objects.

`AlternativeScheduler` receives pre-computed descriptors and forwards them; it does not inspect rule content,
traverse grammar structures, or extract tokens itself.

## Conservative detection model

Detection is intentionally under-approximated.

It reports shared prefixes when they are structurally obvious from rule metadata (for example, identical leading `RuleRef` and `LiteralMatch` sequences).

It does not attempt semantic equivalence or deep inference over complex nested constructs.

## Determinism and non-authority

For a given grammar snapshot, produced shared-prefix metadata is deterministic.

Produced metadata remains non-authoritative:

- parser acceptance remains owned by `ParserEngine`;
- scheduling remains sequential and deterministic;
- diagnostics ownership remains unchanged.

## Future intent

This metadata enables future, explicitly gated design exploration for duplicated-work reduction.

Any future execution activation requires dedicated design, tests, and roadmap updates, and is out of scope for this preparation step.
