# Parser Invocation Metadata Limitations

## Purpose

This document clarifies the current status of ANTLR4-style rule parameters and returns support in `Utils.Parser`.

It is intentionally limited to current implementation facts and conservative architectural considerations. It does not define commitments, timelines, or implementation plans.

## Current supported behavior

The grammar ingestion pipeline currently supports the following syntax forms:

- `rule[int x]`
- `rule returns [int value]`
- `rule[int x] returns [int value]`

When these forms are present:

- parameter and returns blocks are parsed;
- block content is captured with balanced bracket handling;
- multiline content is preserved;
- nested generic-like text is preserved.

Examples such as:

```antlr
rule[Dictionary<string, List<int>> map]
```

are preserved as raw metadata text exactly for compatibility and traceability.

## Current unsupported behavior

The current runtime intentionally does not implement invocation semantics for this metadata.

Specifically, runtime does not currently provide:

- argument passing to parser rule invocations;
- typed parameter models;
- invocation-frame creation or lifecycle;
- return-value propagation;
- parameter binding into runtime scopes;
- semantic type resolution;
- runtime variable scopes related to parameter/return values;
- generated invocation APIs for parameterized rules.

No evaluation of parameter expressions occurs, and no return-value extraction occurs during parser execution.

## Why runtime support is non-trivial

Future runtime support would require coordinated changes across parser architecture boundaries.

At a high level, this would require or may require:

- an explicit invocation-frame model with deterministic lifecycle boundaries;
- deterministic value propagation rules that remain safe under backtracking;
- defined interactions with parser state management (`ParserStateRegistry` and related state normalization);
- memoization semantics describing whether cached results include invocation values;
- rollback semantics for frame-local values when alternatives fail;
- diagnostics propagation rules when value-binding steps fail or are unsupported.

These concerns are architecturally sensitive because parser execution must remain deterministic, auditable, and rollback-safe.

## Potential future architecture topics (non-committal)

If runtime invocation semantics are considered in the future, work would likely need to evaluate topics such as:

- invocation-frame structures;
- immutable or rollback-safe value contexts;
- parser-local state scopes;
- frame-aware memoization boundaries;
- interaction policies with backtracking and continuation-style metadata.

This list is informational only and should not be interpreted as a committed implementation plan.

## Explicit non-goals of current implementation

The current codebase does not provide runtime parameter/returns semantics, and this document does not propose code changes to add them.

Current non-goals include:

- runtime parameter support;
- parameter list semantic parsing;
- type checking of parameter/return declarations;
- runtime return propagation;
- symbol table introduction for invocation values;
- parser behavior or diagnostics changes related to execution semantics.

Parser runtime behavior remains unchanged.
