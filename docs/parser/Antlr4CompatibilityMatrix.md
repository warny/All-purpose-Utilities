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
| Parser semantic predicates (`{ condition }?`) | Parsed and stored as predicate metadata. Default evaluation returns `NotEvaluated`; parsing conservatively accepts the branch and emits `UP1006` when applicable. | Supported for parser-model `ValidatingPredicate` entries through `ExpressionEmbeddedCodePreparer`, `EmbeddedCodeRuntimeDiscovery`, `PreparedExpressionEmbeddedCodeRegistryBuilder`, registry-backed adapters, and `PreparedExpressionRuntimePolicyBuilder`. Depends on the caller-supplied `IExpressionCompiler`; prepared adapters do not compile during `Evaluate()`. | Supported for generated parser grammars through generated C# hooks. Supports expression-bodied predicates and block-bodied predicates with `return`; Roslyn validates C#. Activated by `ParseWithEmbeddedCode(...)` or explicit-context `CreateRuntimePolicy(executionContext, basePolicy)`, not by generated `Parse(...)`. | Executable only when opted in. Runtime-compatible dispatch indexes cover single-item alternatives, sequences, quantifiers, negation probes, duplicate source text, direct-left-recursive base alternatives, and direct-left-recursive tails. |
| Inline parser actions (`{ code }`) | Parsed and stored as action metadata. Default execution returns `NotExecuted`; existing runtime diagnostics apply when applicable. | Supported for parser-model inline `EmbeddedAction` entries through the same prepared registry/policy path. Depends on the caller-supplied expression compiler and does not compile during `Execute()` in the prepared path. | Supported for generated parser grammars through generated C# hooks. Supports simple, multi-statement, and multi-line action bodies, local variables, and calls to instance members injected into or declared on the generated execution-context partial class. Activated by `ParseWithEmbeddedCode(...)` or explicit-context `CreateRuntimePolicy(executionContext, basePolicy)`, not by generated `Parse(...)`. | No action buffering, replay, or external side-effect rollback. Mutations of the generated execution context are covered by managed execution-state rollback in generated C# opt-in paths. Side-effectful actions should be treated cautiously, especially when they affect external state. |
| Rule actions (`@init`, `@after`) | Recognized/stored where supported by ingestion, but not executed. | Not supported; classified/skipped when visible to runtime discovery. | Supported for generated parser grammars through generated C# lifecycle hook methods. `@init` fires before the rule body; `@after` fires after rule success. Activated by `ParseWithEmbeddedCode(...)` or explicit-context `CreateRuntimePolicy(executionContext, basePolicy)`, not by generated `Parse(...)`. `ParserEngine` captures and restores execution-context snapshots through the generated `IParserExecutionStateManager` so failed alternatives, quantifier iterations, and negation probes do not commit lifecycle mutations. No longer emits `UP1029` for these hooks. | Generated policies always install a `GeneratedExecutionStateManager` for all generated parser execution contexts, so predicates, inline actions, and lifecycle hooks share the same state-aware memoization and rollback infrastructure. `GeneratedRuleLifecycleExecutor` is installed only when the grammar declares `@init` or `@after` hooks; otherwise `RuleLifecycleExecutor` remains the base no-op executor. |
| Grammar actions, `@header`, `@members`, and `@footer` | Preserved as metadata where visible, but not executed by default. | Not supported; classified/skipped when visible to runtime discovery. | Limited parser-header support for unscoped `@header` and `@parser::header`: injected verbatim near the top of the generated C# file, with warning `UP1035`. Limited parser-member support for unscoped `@members` and `@parser::members`: injected verbatim into `{ClassName}ExecutionContext`, with warning `UP1031`. Limited parser-footer support for unscoped `@footer` and `@parser::footer`: injected verbatim as trailing generated C# source near the end of the generated file after generated type declarations, with warning `UP1036`. `@lexer::header`, `@lexer::members`, `@lexer::footer`, lexer-grammar `@header`, lexer-grammar `@members`, lexer-grammar `@footer`, and other grammar actions remain unsupported and emit `UP1029`. | Parser headers, members, and footers are C# source-generator compatibility bridges, not full ANTLR target-language compatibility. Roslyn reports invalid C# and collisions. Parser footer is not a second header or documented `using` region. No lexer action/predicate execution is added. |
| Lexer predicates/actions | Not executed by parser runtime. | Not supported. | Not supported; visible constructs emit generator warning `UP1029`. | Requires a separate lexer-state design before execution. |
| Parser actions outside inline alternative positions | Not executed. | Not supported; classified/skipped when visible to runtime discovery. | Not supported. | Represented-only/out of scope for current executable paths. |

Rationale: execution of user code and semantic predicates is policy-controlled. The default runtime keeps deterministic conservative behavior, while opt-in policies may alter predicate outcomes or execute parser actions within documented runtime boundaries. The execution model is documented in `docs/parser/EmbeddedCodeExecutionModel.md`.

### Semantic predicate and precedence predicate audit table

| Construct | Parsed | Stored | Runtime evaluated | Default behavior | Diagnostic |
|---|---|---|---|---|---|
| `{ condition }?` | Yes | Yes (`ValidatingPredicate`) | Yes, via `ISemanticPredicateEvaluator` in `ParserRuntimeFeaturePolicy` | `NotEvaluated` is treated as accepted; parse continues conservatively | `UP1006` (`SemanticPredicateNotEnforced`) when result is `NotEvaluated` |
| `{ condition }=>` (if recognized) | Yes | Yes (`GatingPredicate`) | Yes, via `ISemanticPredicateEvaluator` in `ParserRuntimeFeaturePolicy` | `NotEvaluated` is treated as accepted; parse continues conservatively | `UP1006` (`SemanticPredicateNotEnforced`) when result is `NotEvaluated` |
| Predicate options (`{ condition }?<fail=...>`) | Yes | No — predicate options content is recognized but dropped | No | Predicate is converted normally as `ValidatingPredicate`; options content is not stored or executed | `UP1030` (`PredicateOptionsIgnored`) emitted when options are present |
| `precpred(_ctx, N)` | Yes | Yes (`PrecedencePredicate`) | No (not via semantic predicate evaluator) | Normalized into precedence checks (`CheckPrecedence`) | No `UP1006` emission for precedence predicates |

## Parsed but not fully resolved

The following constructs are recognized but not fully resolved into executable runtime semantics.

| Construct | Current behavior | Limitations |
|---|---|---|
| Rule parameters (`rule[int x]`) | Parsed with balanced-text preservation and stored as raw metadata text where exposed by the parser model. | Passive invocation-frame descriptors can surface represented parameter metadata, but no ANTLR-compatible invocation semantics exist (no argument passing, typed binding, or generated parser signature changes). |
| Rule locals (`locals [int x]`) | Recognized, preserved as raw `Rule.Locals` metadata, exposed by passive descriptors, and emitted as explicit compatibility diagnostic `RuleLocalsIgnored` (`UP1008`). Generated C# execution contexts expose explicit lifecycle helper methods to read/write `context.InvocationFrame` locals and inspect descriptors. | Not executed, typed, or allocated from grammar metadata; passive invocation frames do not populate local stores automatically; no typed local fields/properties, implicit local variables, rule-parameter support, returns propagation, or exception execution are added. |
| Rule returns (`returns [int value]`) | Recognized with balanced-text preservation and stored as raw metadata text. | Ignored by runtime return propagation semantics (no value extraction, propagation, or runtime binding) and emits `RuleReturnsIgnored` (`UP1007`); passive invocation frames do not populate returns automatically. |

| Rule exception metadata (`throws`, `catch`, `finally`) | Recognized, preserved as raw `Rule.ExceptionMetadata`, exposed by passive descriptors, and emitted as explicit compatibility diagnostic `RuleExceptionMetadataIgnored` (`UP1023`). | `throws` does not change parser exception behavior; `catch`/`finally` are not executed; no typed exception declarations or handlers are generated. |

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
| Element options other than `assoc` (`<type=...>`, etc.) | Parsed; `assoc=right` applied; all other options emitted as `UP1032` (`ElementOptionIgnored`) and ignored. |
| Lexer rule options block (`TOKEN options { ... } : ...`) | Parsed; options stored as `Rule.Options` metadata (`RuleOptions`); emits `UP1033` (`LexerRuleOptionsIgnored`); not applied to runtime behavior. |
| Parser rule options block (`rule options { ... } : ...`) | Parsed; options stored as `Rule.Options` metadata (`RuleOptions`); emits `UP1034` (`ParserRuleOptionsIgnored`); not applied to runtime behavior. Passive invocation-frame descriptors can surface represented options as metadata only. |

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
Invocation memoization is keyed by `(rule, input position, precedence, execution-state key)`.

Parser invocation reuse includes the current `IParserExecutionStateManager.GetCurrentStateKey()` value.
The no-op manager returns `ParserExecutionStateKey.Stateless`, so stateless policies keep the same effective behavior as the older rule/position/precedence key.
Stateful managers must return different keys when two semantic states can make parsing differ.
Custom policies should therefore avoid invocation-count-dependent decisions unless those decisions are represented in the execution-state key.


> Embedded-code execution paths use or document their diagnostics against the shared taxonomy in `EmbeddedCodeExecutionModel.md`; generated C# hook syntax errors surface as Roslyn compilation errors.

## Embedded-code runtime indexing metadata

Parser semantic predicates and inline parser actions use shared runtime-indexing metadata for `ParserDefinition` discovery. This improves parity between prepared runtime-inline expression registries and source-generated C# hook dispatch. Parser `@header` / `@parser::header` are supported only as generated C# parser-header injection, parser `@members` / `@parser::members` are injected only into the generated execution context, and parser `@footer` / `@parser::footer` are injected only as trailing generated C# parser-footer source. Unsupported grammar actions, lexer actions, lexer predicates, `@lexer::header`, `@lexer::members`, and `@lexer::footer` remain out of scope, are classified with explicit reasons, and visible unsupported constructs in the source-generator model can produce `UP1029` warnings without changing behavior. Rule lifecycle actions (`@init`/`@after`) are now supported in the source-generator C# path and no longer emit `UP1029`; they do not add execution support to the runtime-inline expression path. Generated lifecycle hooks may explicitly use rule-local frame helpers in `ParseWithEmbeddedCode(...)`, while generated `Parse(...)` remains conservative and rule locals remain metadata-only unless helper calls explicitly store frame values.


## Rule invocation descriptors

Parser rule invocation frames can carry passive `ParserRuleInvocationDescriptor` instances populated from metadata already exposed by the parser model, including preserved raw locals and preserved raw `throws`/`catch`/`finally` metadata. The descriptors are preparatory infrastructure only: rule parameters, returns, locals, throws/catch/finally metadata, and rule options remain metadata-only; ignored-metadata diagnostics such as `UP1007`, `UP1008`, `UP1023`, `UP1033`, and `UP1034` still apply; frame local stores are not allocated from grammar metadata; catch/finally blocks are not executed; no ANTLR-compatible typed invocation semantics or generated parser method signature changes are implemented. Generated C# lifecycle hook bodies may explicitly bridge to the active frame using `GetRuleLocal`, `TryGetRuleLocal`, `SetRuleLocal`, and `GetRuleLocalDescriptors`; this bridge does not create default values or typed members. `Parse(...)` remains conservative, and `ParseWithEmbeddedCode(...)` remains the opt-in path for currently supported generated C# hooks.
