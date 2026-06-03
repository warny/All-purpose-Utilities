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

## Supported in current runtime/project compiler

The features below are operational in current runtime and project-compilation flows.

| Feature | Status | Notes |
|---|---|---|
| Parser grammars (`parser grammar`) | Supported as grammar input form | Rule loading, resolution, and parsing are supported within current runtime constraints. |
| Lexer grammars (`lexer grammar`) | Supported as grammar input form | Tokenization supports lexer rules, commands, and mode handling already implemented. |
| Combined grammars (`grammar`) | Supported as grammar input form | Combined parser/lexer grammars are supported as an input form. |
| Lexer modes (`mode`, `pushMode`, `popMode`) | Supported | Mode stack behavior is implemented by the lexer runtime. |
| Token channels (`channels { ... }` and `-> channel(...)`) | Supported (runtime command), metadata support available | Channel command execution is supported; declared channel metadata is recognized by the grammar pipeline. |
| Token vocabularies (`tokenVocab`) | Supported for project dependency loading | Used by grammar loading and dependency resolution in project compilation workflows. |
| Direct left recursion | Supported with safeguards | Runtime includes direct-left-recursion handling with diagnostics and safeguards. |
| Precedence handling | Supported with known limits | Left-recursive precedence exists, with explicit partial-parity diagnostic for complex ANTLR4 shapes. |
| `<assoc=right>` | Supported | Right-associative alternative metadata is parsed and applied in current precedence behavior. |
| Project-level grammar imports | Supported in project compilation | Multi-file import resolution is supported in `Antlr4GrammarProjectCompiler`. |
| Transitive imports | Supported in project compilation | Imported dependencies are resolved recursively at project-compilation level. |
| Import cycle detection | Supported in project compilation | Cycles are detected and surfaced with dedicated diagnostics. |
| Deterministic conflict resolution | Supported | Entry grammar definitions win deterministically over imported duplicates, with diagnostic traceability. |

## Embedded-code execution status

`docs/parser/ANTLRCompatibility.md` is the canonical compatibility note. The matrix below summarizes default behavior versus the two opt-in paths.

| Construct | Default runtime | Runtime-inline expression opt-in | Generated C# opt-in | Status / limitations |
|---|---|---|---|---|
| Parser semantic predicates (`{ condition }?`) | Parsed and stored as predicate metadata. Default evaluation returns `NotEvaluated`; parsing conservatively accepts the branch and emits `UP1006` when applicable. | Supported for parser-model `ValidatingPredicate` entries through `ExpressionEmbeddedCodePreparer`, `EmbeddedCodeRuntimeDiscovery`, `PreparedExpressionEmbeddedCodeRegistryBuilder`, registry-backed adapters, and `PreparedExpressionRuntimePolicyBuilder`. Depends on the caller-supplied `IExpressionCompiler`; prepared adapters do not compile during `Evaluate()`. | Supported for generated parser grammars through generated C# hooks. Supports expression-bodied predicates and block-bodied predicates with `return`; Roslyn validates C#. Activated by `CreateRuntimePolicy(...)` or `ParseWithEmbeddedCode(...)`, not by generated `Parse(...)`. | Executable only when opted in. Runtime-compatible dispatch indexes cover single-item alternatives, sequences, quantifiers, negation probes, duplicate source text, direct-left-recursive base alternatives, and direct-left-recursive tails. |
| Inline parser actions (`{ code }`) | Parsed and stored as action metadata. Default execution returns `NotExecuted`; existing runtime diagnostics apply when applicable. | Supported for parser-model inline `EmbeddedAction` entries through the same prepared registry/policy path. Depends on the caller-supplied expression compiler and does not compile during `Execute()` in the prepared path. | Supported for generated parser grammars through generated C# hooks. Supports simple, multi-statement, and multi-line action bodies, local variables, and calls to instance members injected into or declared on the generated execution-context partial class. Activated by `CreateRuntimePolicy(...)` or `ParseWithEmbeddedCode(...)`, not by generated `Parse(...)`. | No rollback/buffering or controlled context mutation model. Side-effectful actions should be treated cautiously, especially around backtracking or negation probes. |
| Rule actions (`@init`, `@after`) | Recognized/stored where supported by ingestion, but not executed. | Not supported; classified/skipped when visible to runtime discovery. | Not supported; visible constructs emit generator warning `UP1029`. | Represented-only; no lifecycle execution model. |
| Grammar actions and `@members` | Preserved as metadata where visible, but not executed by default. | Not supported; classified/skipped when visible to runtime discovery. | Limited support for unscoped `@members` and `@parser::members`: injected verbatim into `{ClassName}ExecutionContext`, with warning `UP1031`. `@lexer::members` and other grammar actions remain unsupported and emit `UP1029`. | Parser members are a C# source-generator compatibility bridge, not full ANTLR member/lifecycle support. Roslyn reports invalid C# and collisions. |
| Lexer predicates/actions | Not executed by parser runtime. | Not supported. | Not supported; visible constructs emit generator warning `UP1029`. | Requires a separate lexer-state design before execution. |
| Parser actions outside inline alternative positions | Not executed. | Not supported; classified/skipped when visible to runtime discovery. | Not supported. | Represented-only/out of scope for current executable paths. |

Rationale: execution of user code and semantic predicates is policy-controlled. The default runtime keeps deterministic conservative behavior, while opt-in policies may alter predicate outcomes or execute parser actions within documented runtime boundaries. The execution model is documented in `docs/parser/EmbeddedCodeExecutionModel.md`.

### Semantic predicate and precedence predicate audit table

| Construct | Parsed | Stored | Runtime evaluated | Default behavior | Diagnostic |
|---|---|---|---|---|---|
| `{ condition }?` | Yes | Yes (`ValidatingPredicate`) | Yes, via `ISemanticPredicateEvaluator` in `ParserRuntimeFeaturePolicy` | `NotEvaluated` is treated as accepted; parse continues conservatively | `UP1006` (`SemanticPredicateNotEnforced`) when result is `NotEvaluated` |
| `{ condition }=>` (if recognized) | Yes | Yes (`GatingPredicate`) | Yes, via `ISemanticPredicateEvaluator` in `ParserRuntimeFeaturePolicy` | `NotEvaluated` is treated as accepted; parse continues conservatively | `UP1006` (`SemanticPredicateNotEnforced`) when result is `NotEvaluated` |
| `precpred(_ctx, N)` | Yes | Yes (`PrecedencePredicate`) | No (not via semantic predicate evaluator) | Normalized into precedence checks (`CheckPrecedence`) | No `UP1006` emission for precedence predicates |

## Parsed but not fully resolved

The following constructs are recognized but not fully resolved into executable runtime semantics.

| Construct | Current behavior | Limitations |
|---|---|---|
| Rule parameters (`rule[int x]`) | Parsed with balanced-text preservation and stored as raw metadata text (including multiline and nested generic-like syntax). | No runtime invocation semantics exist (no argument passing, no typed binding, no invocation-frame model). |
| Rule locals (`locals [int x]`) | Recognized at rule prequel parsing stage and emitted as explicit compatibility diagnostic `RuleLocalsIgnored` (`UP1008`). | Not executed, not preserved as runtime state, and no invocation frame is created. |
| Rule returns (`returns [int value]`) | Recognized with balanced-text preservation and stored as raw metadata text. | Ignored by runtime return propagation semantics (no value extraction or runtime binding) and emits `RuleReturnsIgnored` (`UP1007`). |

These constructs are treated as syntax/metadata compatibility points, not as fully executable semantics.

Additional architectural context and explicit non-goals are documented in `docs/parser/ParserMetadataAndRuntimeLimitations.md`.

## Partially supported

| Area | Current support boundary |
|---|---|
| `import` usage | Fully resolved when grammars are compiled as a project input set; isolated single-file compilation may emit `ImportParsedButNotResolved` when no resolver context is provided. |
| `tokenVocab` | Dependency loading is supported; effective behavior depends on available resolver inputs and vocabulary source availability in the compilation context. |
| `superClass` | Parsed, preserved, normalized into `EffectiveGrammarOptions`, and exposed through `GrammarExtensionBinding` metadata. It is **not** interpreted as parser/lexer runtime inheritance execution. |
| Labels on non-rule-reference elements | Parsed and recognized, then ignored with deterministic compatibility diagnostic UP1022. |
| Other grammar `options` entries | Parsed and preserved as metadata; unsupported options are reported explicitly with `UnsupportedAntlrOptionIgnored`. |
| Left-recursive precedence parity | Implemented for current runtime model, but not equivalent to all ANTLR4 precedence scenarios; the runtime can emit `LeftRecursivePrecedencePartiallySupported` where applicable. |
| Lexer command set | Supported commands are `skip`, `more`, `channel`, `type`, `pushMode`, `popMode`, `mode`. Any other command is rejected deterministically with `UnsupportedLexerCommand`. |

## Runtime metadata boundary

Continuation metadata descriptors are internal runtime metadata.
They are prepared after grammar resolution.
They are not ANTLR grammar constructs.
They are preserved/normalized as descriptive metadata only.
They are never executed, replayed, or resumed.

## Unsupported

The following capabilities are currently unsupported by design:

- adaptive LL prediction;
- GLL parsing;
- speculative parsing and continuation replay;
- runtime continuation execution/resume;
- parser graph execution;
- parse-forest generation;
- parallel parsing;
- async parsing;
- automatic or arbitrary target-language action execution engines beyond the explicit parser predicate/action opt-in paths summarized above;
- contextual lexer dispatch beyond current deterministic lexer/mode behavior.

These are intentionally outside the current runtime envelope.

## Diagnostics interpretation for compatibility

Compatibility diagnostics are intended to document capability boundaries, not to redefine parse authority:

- unsupported/ignored ANTLR4 feature diagnostics are compatibility-oriented and may appear even when parse succeeds;
- orchestration diagnostics (such as pruning/backtracking) remain separate from compatibility diagnostics;
- engine-authoritative parse diagnostics (parse failure, trailing tokens) remain owned by `ParserEngine`.

## Architectural notes

Current parser architecture includes explicit metadata infrastructure for future-safe analysis, while preserving deterministic execution boundaries.

- Shared-prefix infrastructure is **metadata-only**.
- Continuation metadata support is recognized/preserved/normalized as structural metadata; execution support is intentionally disabled.
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

## Capability descriptor model (code-facing)

The repository now also exposes a code-facing capability descriptor model (`ParserFeatureCapabilities`) that centralizes feature support levels (Supported, SupportedWithLimits, RuntimeOptional, MetadataOnly, ParsedOnly, Unsupported).

This model is **descriptive only** and does not change parser behavior, diagnostics emission, parse-tree shape, or runtime execution policies.


## Memoization and policy assumptions


Custom predicate/action policies can influence branch acceptance and therefore parse outcomes.
However, invocation memoization remains keyed by `(rule, input position, precedence)` and does not model semantic runtime state.

Parser invocation reuse is currently keyed by rule, input position, and precedence.
This model currently assumes policy handlers are deterministic for equivalent invocations and does not include evaluator/executor external state in the memoization key.
Custom policies should therefore avoid invocation-count-dependent decisions and externally observable mutable semantic state.


> Embedded-code execution paths use or document their diagnostics against the shared taxonomy in `EmbeddedCodeExecutionModel.md`; generated C# hook syntax errors surface as Roslyn compilation errors.

## Embedded-code runtime indexing metadata

Parser semantic predicates and inline parser actions use shared runtime-indexing metadata for `ParserDefinition` discovery. This improves parity between prepared runtime-inline expression registries and source-generated C# hook dispatch without adding support for grammar actions, lexer actions, lexer predicates, `@init`, or `@after` execution. Unsupported constructs remain out of scope, are classified with explicit reasons, and visible unsupported constructs in the source-generator model can now produce `UP1029` warnings without changing behavior.

