# ANTLR4 Compatibility Matrix

## Introduction

`Utils.Parser` supports ANTLR4 grammar ingestion progressively, with a conservative runtime execution model.

Support levels are intentionally explicit:

- **supported**: parsed, resolved, and executed by the current runtime;
- **parsed but not executed**: syntax is recognized and represented, but execution semantics are intentionally disabled;
- **parsed but not fully resolved**: syntax is recognized, but runtime/value resolution is incomplete;
- **partially supported**: available in constrained scope with documented limits;
- **unsupported**: intentionally outside the current runtime model.

This document describes the current implementation status only. It does not define roadmap commitments.

## Fully supported

The features below are operational in current runtime and project-compilation flows.

| Feature | Status | Notes |
|---|---|---|
| Parser grammars (`parser grammar`) | Fully supported | Rule loading, resolution, and parsing are supported within current runtime constraints. |
| Lexer grammars (`lexer grammar`) | Fully supported | Tokenization supports lexer rules, commands, and mode handling already implemented. |
| Combined grammars (`grammar`) | Fully supported | Combined parser/lexer grammars are supported as an input form. |
| Lexer modes (`mode`, `pushMode`, `popMode`) | Fully supported | Mode stack behavior is implemented by the lexer runtime. |
| Token channels (`channels { ... }` and `-> channel(...)`) | Fully supported (runtime command), metadata support available | Channel command execution is supported; declared channel metadata is recognized by the grammar pipeline. |
| Token vocabularies (`tokenVocab`) | Fully supported for dependency loading | Used by grammar loading and dependency resolution in project compilation workflows. |
| Direct left recursion | Fully supported | Runtime includes direct-left-recursion handling with diagnostics and safeguards. |
| Precedence handling | Supported with known limits | Left-recursive precedence exists, with explicit partial-parity diagnostic for complex ANTLR4 shapes. |
| `<assoc=right>` | Fully supported | Right-associative alternative metadata is parsed and applied in current precedence behavior. |
| Project-level grammar imports | Fully supported in project compilation | Multi-file import resolution is supported in `Antlr4GrammarProjectCompiler`. |
| Transitive imports | Fully supported in project compilation | Imported dependencies are resolved recursively at project-compilation level. |
| Import cycle detection | Fully supported in project compilation | Cycles are detected and surfaced with dedicated diagnostics. |
| Deterministic conflict resolution | Fully supported | Entry grammar definitions win deterministically over imported duplicates, with diagnostic traceability. |

## Parsed but not executed

The following constructs are parsed and stored, but runtime semantic execution is intentionally disabled.

| Construct | Current behavior | Diagnostics |
|---|---|---|
| Semantic predicates (`{ condition }?`) | Parsed and represented in grammar model; runtime does not evaluate predicate semantics. | `SemanticPredicateNotEnforced` |
| Inline actions (`{ code }`) | Parsed and stored; runtime does not execute target-language code blocks. | `InlineActionStoredNotExecuted` |
| Rule actions (`@init`, `@after`) | Parsed as action metadata where present; no execution hook is active in runtime parsing. | `ActionIgnored` (pipeline-dependent) |

Rationale: execution of user code and semantic predicates requires explicit policy boundaries and execution abstractions that are intentionally not part of the current deterministic runtime.

## Parsed but not fully resolved

The following constructs are recognized but not fully resolved into executable runtime semantics.

| Construct | Current behavior | Limitations |
|---|---|---|
| Rule parameters (`rule[int x]`) | Bracketed parameter syntax is partially recognized by bootstrap parsing. | No runtime invocation-frame/value binding model is implemented. |
| Rule returns (`returns [int value]`) | Returns metadata can be parsed and preserved as raw text. | No runtime value propagation/extraction contract exists for parser execution. |

These constructs are treated as syntax/metadata compatibility points, not as fully executable semantics.

## Partially supported

| Area | Current support boundary |
|---|---|
| `import` usage | Fully resolved when grammars are compiled as a project input set; isolated single-file compilation may emit `ImportParsedButNotResolved` when no resolver context is provided. |
| `tokenVocab` | Dependency loading is supported; effective behavior depends on available resolver inputs and vocabulary source availability in the compilation context. |
| Left-recursive precedence parity | Implemented for current runtime model, but not equivalent to all ANTLR4 precedence scenarios; the runtime can emit `LeftRecursivePrecedencePartiallySupported` where applicable. |

## Unsupported

The following capabilities are currently unsupported by design:

- adaptive LL prediction;
- GLL parsing;
- speculative parsing and continuation replay;
- parser graph execution;
- parse-forest generation;
- parallel parsing;
- async parsing;
- semantic action execution engines;
- contextual lexer dispatch beyond current deterministic lexer/mode behavior.

These are intentionally outside the current runtime envelope.

## Architectural notes

Current parser architecture includes explicit metadata infrastructure for future-safe analysis, while preserving deterministic execution boundaries.

- Shared-prefix infrastructure is **metadata-only**.
- Continuation descriptors are structural metadata, not runtime replay state.
- No shared-prefix execution pipeline is active.
- `AlternativeScheduler` provides explicit deterministic orchestration.
- `ParserStateRegistry` centralizes parser state guards.
- `ActiveParseState` provides explicit state normalization for branch handling.

The above architecture supports auditability and controlled evolution without changing current parser semantics.

## Design philosophy

`Utils.Parser` follows conservative compatibility evolution:

- deterministic runtime behavior first;
- explicit diagnostics when semantics are parsed but not executed;
- compatibility through explicit modeling before execution support;
- audit-friendly boundaries between metadata, resolution, and runtime execution.

This policy avoids speculative runtime complexity while still enabling progressive ANTLR4 syntax coverage.
