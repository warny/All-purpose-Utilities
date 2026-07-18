# ANTLR4 Compatibility Matrix

> Rule arguments/returns plan: [`RuleArgumentsAndReturnsPlan.md`](./RuleArgumentsAndReturnsPlan.md) is the design reference for rule parameters, call arguments, returns, labels, rollback, and future generated-C# opt-in binding. The matrix rows below describe current behavior only: parameters and returns are metadata plus explicit helper state, raw call arguments are preserved/split syntactically, literal binding policies are explicit opt-in, labels store managed `ParserRuleCallResult` snapshots, and generated-C# automatic `callee[expr]` binding is not implemented.

## Introduction

`Utils.Parser` supports ANTLR4 grammar ingestion progressively, with a conservative runtime execution model.

Support levels are intentionally explicit:

- **supported**: parsed, resolved, and executed by the current runtime;
- **parsed but not executed**: syntax is recognized and represented, but execution semantics are intentionally disabled;
- **parsed but not fully resolved**: syntax is recognized, but runtime/value resolution is incomplete;
- **partially supported**: available in constrained scope with documented limits;
- **unsupported**: intentionally outside the current runtime model.

This document describes the current implementation status only. It does not define roadmap commitments.

## Compatibility level estimate

This project should not claim full ANTLR4 compatibility. For maintenance planning, the canonical compatibility reference estimates common grammar syntax and deterministic runtime tokenization/parsing at about 60-65% of practical ANTLR4 usage, strict ANTLR4 compatibility at about 40-45%, and embedded-code compatibility specifically at about 35-40%. These numbers are approximate guidance, not a formal conformance score; see `ANTLRCompatibility.md` for the rationale and unsupported boundaries.

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

`docs/parser/ANTLRCompatibility.md` is the canonical compatibility note. The matrix below summarizes default behavior versus the two opt-in paths. Embedded code is target-language code and is preserved unchanged by default. Any rewrite must go through `IParserEmbeddedCodeTransformer`; the no-op transformer is the default.

| Construct | Default runtime | Runtime-inline expression opt-in | Generated C# opt-in | Status / limitations |
|---|---|---|---|---|
| Parser semantic predicates (`{ condition }?`) | Parsed and stored as predicate metadata. Default evaluation returns `NotEvaluated`; parsing conservatively accepts the branch and emits `UP1006` when applicable. | Supported for parser-model `ValidatingPredicate` entries through `ExpressionEmbeddedCodePreparer`, `EmbeddedCodeRuntimeDiscovery`, `PreparedExpressionEmbeddedCodeRegistryBuilder`, registry-backed adapters, and `PreparedExpressionRuntimePolicyBuilder`. Depends on the caller-supplied `IExpressionCompiler`; prepared adapters do not compile during `Evaluate()`. | Supported for generated parser grammars through generated C# hooks. Supports expression-bodied predicates and block-bodied predicates with `return`; Roslyn validates C#. Activated by `ParseWithEmbeddedCode(...)` or explicit-context `CreateRuntimePolicy(executionContext, basePolicy)`, not by generated `Parse(...)`. | Executable only when opted in. Runtime-compatible dispatch indexes cover single-item alternatives, sequences, quantifiers, negation probes, duplicate source text, direct-left-recursive base alternatives, and direct-left-recursive tails. |
| Inline parser actions (`{ code }`) | Parsed and stored as action metadata. Default execution returns `NotExecuted`; existing runtime diagnostics apply when applicable. | Supported for parser-model inline `EmbeddedAction` entries through the same prepared registry/policy path. Depends on the caller-supplied expression compiler and does not compile during `Execute()` in the prepared path. | Supported for generated parser grammars through generated C# hooks. Supports simple, multi-statement, and multi-line action bodies, local variables, and calls to instance members injected into or declared on the generated execution-context partial class. Activated by `ParseWithEmbeddedCode(...)` or explicit-context `CreateRuntimePolicy(executionContext, basePolicy)`, not by generated `Parse(...)`. | No action buffering, replay, or external side-effect rollback. Mutations of the generated execution context are covered by managed execution-state rollback in generated C# opt-in paths. Side-effectful actions should be treated cautiously, especially when they affect external state. |
| Rule actions (`@init`, `@after`) | Recognized/stored where supported by ingestion, but not executed. | Not supported; classified/skipped when visible to runtime discovery. | Supported for generated parser grammars through generated C# lifecycle hook methods. `@init` fires before the rule body; `@after` fires after rule success. Activated by `ParseWithEmbeddedCode(...)` or explicit-context `CreateRuntimePolicy(executionContext, basePolicy)`, not by generated `Parse(...)`. `ParserEngine` captures and restores execution-context snapshots through the generated `IParserExecutionStateManager` so failed alternatives, quantifier iterations, and negation probes do not commit lifecycle mutations. No longer emits `UP1029` for these hooks. | Generated policies always install a `GeneratedExecutionStateManager` for all generated parser execution contexts, so predicates, inline actions, and lifecycle hooks share the same state-aware memoization and rollback infrastructure. `GeneratedRuleLifecycleExecutor` is installed only when the grammar declares `@init` or `@after` hooks; otherwise `RuleLifecycleExecutor` remains the base no-op executor. |
| Grammar actions, `@header`, `@members`, and `@footer` | Preserved as metadata where visible, but not executed by default. | Not supported; classified/skipped when visible to runtime discovery. | Limited parser-header support for unscoped `@header` and `@parser::header`: injected verbatim near the top of the generated C# file, with warning `UP1035`. Limited parser-member support for unscoped `@members` and `@parser::members`: injected verbatim into `{ClassName}ExecutionContext`, with warning `UP1031`. Limited parser-footer support for unscoped `@footer` and `@parser::footer`: injected verbatim as trailing generated C# source near the end of the generated file after generated type declarations, with warning `UP1036`. `@lexer::header`, `@lexer::members`, `@lexer::footer`, parser named actions in lexer grammars, lexer-grammar unscoped `@header`, `@members`, and `@footer`, unknown parser named-action names, unknown named-action scopes, and other grammar actions remain unsupported and emit deterministic `UP1029` diagnostics. | Parser headers, members, and footers are C# source-generator compatibility bridges, not full ANTLR target-language compatibility. Roslyn reports invalid C# and collisions. Parser footer is not a second header or documented `using` region. Simple lexer action/predicate execution is limited to generated-C# opt-in. |
| Embedded code preservation | Raw embedded parser code is stored as metadata and preserved. | Raw code is transformed first; with the no-op transformer, the compiler receives the same text. | Raw code is transformed first; with the no-op transformer, generated C# contains the same code text. | Supported by default. Normal generated wrapping/indentation may change whitespace, but the parser/generator does not rewrite target-language semantics. |
| Embedded-code transformation | Not applied by default parsing unless an explicit preparation path is used. | Supported through `RawEmbeddedCode` → `ParserEmbeddedCodeTransformationService.TransformOrThrow(...)` → `TransformedEmbeddedCode` before the existing compiler/preparer is invoked. | Supported through `RawEmbeddedCode` → `ParserEmbeddedCodeTransformationService.TransformOrThrow(...)` → `TransformedEmbeddedCode` before generated emission. | The transformer is not a compiler and must not introduce a parallel compiler abstraction. Error diagnostics stop safe preparation/emission. |
| ANTLR-style `$...` rewriting | Not core parser behavior. | Optional target-language transformer behavior only. | Optional C# compatibility transformer behavior only. | `$x.value`, `$xs.value`, `$rule.value`, `$param`, and `$local` are emitted/prepared unchanged with the default no-op transformer and may fail target-language compilation if invalid. Full ANTLR embedded action semantics are not supported. |
| Dynamic embedded-code compilation | Not automatic. | Through the existing caller-supplied compiler/preparer after optional transformation. | Not applicable; generated source is compiled by the consuming project. | No new compiler abstraction is introduced. |
| Lexer predicates/actions | Not executed by conservative `Parse(...)`. | Not supported. | Simple lexer inline actions and simple lexer predicates are supported only through generated-C# opt-in. Predicates are evaluated during lexer matching and reject only the current path; actions execute after token acceptance and before accepted lexer commands are applied. Tests cover rule references/fragments, simple quantifiers, duplicate hook positions, false-predicate/action ordering, `skip`, `channel(...)`, `type(...)`, `more`, `mode(...)`, and `pushMode(...)`/`popMode` mode transitions. | Lexer inline action reads are partially supported in generated-C# opt-in for `$text`, `$type`, `$channel`, `$mode`, `$line`, and `$pos`; simple lexer inline action writes are partially supported only for `$type = ...`, `$channel = ...`, and `$mode = ...` statements through `LexerActionExecutionResult`. Lexer predicate `$...`, runtime-inline lexer actions/predicates, a separate lexer runtime, public quantifier iteration indexes, rollback of external side effects, and full ANTLR lexer embedded-code semantics remain unsupported. |
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
| Rule parameters (`rule[int x]`) | Parsed with balanced-text preservation and stored as raw metadata text; each declaration split by top-level comma, name extracted lexically; raw declarations preserved. Generated C# execution contexts expose `GetRuleParameter`, `TryGetRuleParameter`, `GetRuleParameterDescriptors`, `SetNextRuleParameter`, and `ClearNextRuleParameters` helpers. | No ANTLR-compatible argument passing, typed binding, auto-binding, or generated parser signature changes; frames are not auto-populated; `$param` is not core parser behavior and `callee[expr]` is not evaluated or supported; explicit seeding via `SetNextRuleParameter` is rollback-safe. |
| Rule-call argument clause (`callee[...]`) | Recognized and preserved as raw text on `RuleRef.RawArguments` (outer brackets excluded). Both the runtime ANTLR converter and the source-generator G4 parser preserve the raw text. Reported with `UP1037 RuleCallArgumentsPreservedAsMetadata`. At runtime, raw argument text is also carried into `ParserRuleCallResult.RawArguments` on the parent frame's last completed child call result. Generated C# opt-in helpers `TryGetLastRuleCallRawArguments` and `GetLastRuleCallResult(context)?.RawArguments` expose the metadata explicitly. `SetNextRuleParameterFromRawArguments(context, ruleName, parameterName, rawArguments, map)` allows explicit user-controlled mapping into a future child seed via a caller-provided delegate. | Raw argument text is not evaluated, not parsed as C# expressions, and not bound to child rule parameters. `PendingChildSeeds`, `InvocationFrame.Parameters`, and frame behavior are unchanged. Generated `Parse(...)` and rule signatures are unchanged. Call-site metadata is rollback-safe and memoization-safe. `SetNextRuleParameterFromRawArguments` requires an explicit mapper and returns `false` for null raw arguments; mapper exceptions propagate. `SplitRawArgumentsTopLevel` and `TrySplitLastRuleCallRawArguments` split raw text into top-level slices; syntactic only, no evaluation. `SetNextRuleParametersFromRawArguments(context, ruleName, args, mappings)` maps positional slices to named seeds via `ParserRawArgumentParameterMapping`; requires explicit mapper per parameter; validates all indices before seeding; last mapping wins for duplicates. `SplitNamedRawArgumentsTopLevel` / `TrySplitLastRuleCallNamedRawArguments` split into named dictionaries (`value: 42` / `value = 42`; colon, equals, or both); `SetNextRuleParametersFromNamedRawArguments` maps named entries to seeds via `ParserRawNamedArgumentParameterMapping`; missing argument name returns false with no partial seed; last ParameterName wins for duplicates. All helpers syntactic-only; no automatic evaluation or binding. bare `$param` reads are not core parser behavior; an optional generated C# compatibility transformer may rewrite current-rule parameter reads, but argument clauses still do not evaluate `$param` expressions automatically. |
| Rule-reference labels (`x=child`, `xs+=child`) | Preserved as metadata end-to-end. `RuleRef.Label` (type `RuleLabel`) stores the label name and additive flag; `RuleRef.LabelName` and `RuleRef.LabelKind` (`ParserRuleReferenceLabelKind`: `None`, `Assignment`, `List`) derive from it. `ParserEngine` calls `AnnotateLastChildCallLabel` after every successful child rule completion so the call-site label is visible in `ParserRuleCallResult.LabelName` and `LabelKind` on the parent frame. Both the ANTLR converter and the source-generator G4 parser preserve labels; `GrammarEmitter` emits `Label: new RuleLabel(...)`. Labels compose with `callee[...]` raw arguments. Labels on non-rule-reference elements emit diagnostic `UP1022`. Label metadata is rollback-safe and memoization-safe. Generated C# opt-in code can inspect labels via `GetLastRuleCallResult(context)?.LabelName` and `?.LabelKind`. | Labels are metadata-only in parser core: no core `$x`, `$xs`, implicit label variables, typed label fields/properties, automatic parse-node storage, automatic return access, automatic binding, automatic argument evaluation, automatic parameter seeding, or generated parser method signatures are added. Generated C# can opt into the documented transformer-only `$c.value`/`$x.value` assignment-label reads and `$xs.value` list-label projection in inline parser actions and `@after`; the default/no-op transformer and conservative `Parse(...)` remain unchanged. No lexer label support. |
| Rule locals (`locals [int x]`) | Recognized, preserved as raw `Rule.Locals` metadata, exposed by passive descriptors, and emitted as explicit compatibility diagnostic `RuleLocalsIgnored` (`UP1008`). Generated C# execution contexts expose explicit lifecycle helper methods to read/write `context.InvocationFrame` locals and inspect descriptors. | Not executed, typed, or allocated from grammar metadata; passive invocation frames do not populate local stores automatically; no typed local fields/properties, implicit local variables, rule-parameter support, returns propagation, or exception execution are added. |
| Rule returns (`returns [int value]`) | Recognized, preserved as raw `Rule.Returns` metadata, exposed by passive descriptors (each declaration split by top-level comma, name extracted lexically — e.g. `value` for `int value`), and emitted as explicit compatibility diagnostic `RuleReturnsIgnored` (`UP1007`). Generated C# execution contexts expose explicit lifecycle helper methods to read/write `context.InvocationFrame` returns, inspect descriptors, and observe the last completed child call result via `GetLastRuleCallResult`/`TryGetLastRuleCallReturn`. | Not typed, auto-allocated, or automatically propagated to caller frames; passive invocation frames do not populate return stores automatically; no typed return fields/properties, implicit return variables, core `$rule.value`, automatic labeled rule-reference return access, or automatic parent assignment are added. Call results are rollback-safe (failed alternatives and memoization hits do not leak stale results). |

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

Parser semantic predicates and inline parser actions use shared runtime-indexing metadata for `ParserDefinition` discovery. This improves parity between prepared runtime-inline expression registries and source-generated C# hook dispatch. Parser `@header` / `@parser::header` are supported only as generated C# parser-header injection, parser `@members` / `@parser::members` are injected only into the generated execution context, and parser `@footer` / `@parser::footer` are injected only as trailing generated C# parser-footer source. Unsupported grammar actions, lexer actions, lexer predicates, `@lexer::header`, `@lexer::members`, and `@lexer::footer` remain out of scope, are classified with explicit reasons, and visible unsupported constructs in the source-generator model can produce `UP1029` warnings without changing behavior. Rule lifecycle actions (`@init`/`@after`) are now supported in the source-generator C# path and no longer emit `UP1029`; they do not add execution support to the runtime-inline expression path. Generated lifecycle hooks may explicitly use rule-local frame helpers in `ParseWithEmbeddedCode(...)`; before `@init`, declared names are allocated as missing-only untyped `null` frame entries. Generated `Parse(...)` remains conservative, and locals are neither typed nor exposed as implicit variables.


## Rule invocation descriptors

Parser rule invocation frames can carry passive `ParserRuleInvocationDescriptor` instances populated from metadata already exposed by the parser model, including preserved raw locals and preserved raw `throws`/`catch`/`finally` metadata. The descriptors are preparatory infrastructure only: rule parameters, returns, locals, throws/catch/finally metadata, and rule options remain metadata-only; ignored-metadata diagnostics such as `UP1007`, `UP1008`, `UP1023`, `UP1033`, and `UP1034` still apply; frame local stores are allocated from grammar metadata only by the generated C# opt-in lifecycle executor, using captured names, missing-only writes, and `null` values; catch/finally blocks are not executed; no ANTLR-compatible typed invocation semantics or generated parser method signature changes are implemented. Generated C# lifecycle hook bodies may explicitly bridge to the active frame using `GetRuleLocal`, `TryGetRuleLocal`, `SetRuleLocal`, and `GetRuleLocalDescriptors`; this bridge does not create typed defaults or typed members, and array-looking declarations remain `null`. `Parse(...)` remains conservative, and `ParseWithEmbeddedCode(...)` remains the opt-in path for currently supported generated C# hooks.

## Explicit parser rule-call policy

| Capability | Default runtime | Generated C# opt-in | ANTLR compatibility boundary |
|---|---|---|---|
| `BeforeRuleCall(...)` / `AfterRuleCall(...)` observation | `NullParserRuleCallExecutionPolicy` is a no-op; parsing is unchanged. | A custom policy can be preserved through generated `CreateRuntimePolicy(executionContext, basePolicy)` or the three-argument `ParseWithEmbeddedCode(input, executionContext, basePolicy)` overload. | Explicit extension point only; not ANTLR argument execution. |
| Rule-call context metadata | Exposes rule name, raw arguments, labels, passive target descriptor, optional syntactic splits, success/failure, and a tracked completed call result when available. | Same runtime context; successful results are annotated before the after callback, including memoized calls. | `callee[...]` stays metadata-only; no evaluation, binding, automatic seed, `$param`, `$x`, `$x.value`, or `$rule.value`. |
| Policy rollback | No policy side effects occur under the default no-op policy. | External custom-policy side effects are not automatically rolled back. | Only separately managed rollback-aware parser state participates in capture/restore; call-site result metadata remains rollback/memoization safe. |

## Opt-in positional literal call policy

Parser-rule call arguments remain **represented-only by default**. When a caller explicitly installs `PositionalLiteralRuleCallExecutionPolicy`, exact-arity positional arguments can bind to declared parser-rule parameter names. Supported values are `null`, lowercase Booleans, signed decimal `int`/`long`, finite invariant decimal or exponent `double`, double-quoted strings, and single-quoted characters with the limited escapes `\\`, `\"`, `\'`, `\n`, `\r`, `\t`, and `\0`.

This is partial compatibility, not full ANTLR argument semantics: declared C# types are not checked; named/mixed arguments and arbitrary expressions are rejected; Roslyn is not invoked; `Parse(...)` stays conservative; and lexer arguments, `$param`, `$x`, `$x.value`, `$rule.value`, return binding, and label-backed storage inside rule-call argument clauses remain unsupported. This does not contradict the separate generated-C# embedded-action transformer sugar for `$c.value`/`$x.value` and `$xs.value` in inline parser actions and `@after`. Seeds participate in managed rollback, and generated memoization keys deterministically distinguish the supported bound literal values.


## Opt-in named literal call policy

`NamedLiteralRuleCallExecutionPolicy` is a second, separate opt-in policy. The default remains represented-only, generated `Parse(...)` remains conservative, and positional binding is not combined automatically. The policy consumes the existing syntactic `NamedRawArguments` dictionary for both `name: literal` and `name = literal`, matches exact ordinal declared names independent of argument order, and requires exact parameter-name coverage. Missing, extra, case-mismatched, blank, or duplicate declared names reject the whole binding; optional/default parameters, partial binding, and mixed syntax are unsupported. Duplicate raw names retain `ParserRawNamedArgumentSplitter` last-wins behavior.

Values are limited to `ParserSimpleLiteralParser`; declared C# types are not validated or converted. The complete call is parsed before one all-or-none rollback-managed seed batch. Matching existing seeds are overwritten, unrelated seeds are preserved, `null` stays present, and state-aware memoization distinguishes supported values. Arbitrary expressions, `$param`, `$x`, `$x.value`, `$rule.value`, returns, labels, and lexer rule arguments remain unsupported inside named argument clauses; generated-C# embedded-action label-return sugar is documented separately below.

## Typed literal call-policy detail

| Capability | Default / existing untyped policies | Explicit typed policies |
|---|---|---|
| Declared type enforcement | No; metadata only | Yes, for the closed built-in allowlist |
| Installation | Default is no-op; positional/named untyped policies are explicit | `TypedPositionalLiteralRuleCallExecutionPolicy` or `TypedNamedLiteralRuleCallExecutionPolicy` must be installed explicitly |
| Target types | Not inspected | `bool`, integer aliases, `float`, `double`, `decimal`, `char`, `string`, `object`, their exact `System.*` names, and one nullable suffix |
| Numeric conversion | None; parsed literal runtime value is retained | Checked integral range conversion; exact-preserving integral-to-floating and double-to-float; no floating-to-integral; integral-to-decimal only |
| Text conversion | None | Exact string/char, `char` to `string`, one-character `string` to `char`; never string-to-number or string-to-Boolean |
| Null | Retained without declared-type checks | Reference types and nullable value types only |
| Defaults | Ignored; exact arity/coverage remains required | Passive `RawDefaultValue` metadata may satisfy omitted parameters only when the default is a supported simple literal convertible to the declared type |
| Omission | Unsupported | Positional omission is trailing-only; named omission is order-independent when every omitted parameter has a valid default |
| Mutation | One complete seed batch | All explicit values and required defaults are resolved before one seed batch |
| Rollback/memoization | Managed generated state | Same managed state; keys contain converted runtime values and runtime types |

Explicit arguments override defaults, and an invalid default is not parsed or converted when its parameter is supplied explicitly. Default text is never treated as general C#: parameter references, constants, enum members, member access, calls, arithmetic, casts, `default`, `default(T)`, `nameof`, interpolation, arrays, and collections remain unsupported. The typed policies do not resolve arbitrary C# types and do not support user-defined types, enums, arrays, generics, collections, tuples, delegates, return/label binding, `$param` forms, or lexer execution. Generated `Parse(...)` remains conservative; only an explicit base policy used by generated opt-in APIs enables typed binding.

## Labeled parser-rule result matrix

| Capability | Default runtime | Generated C# opt-in | Limits |
|---|---|---|---|
| Child return snapshot in `ParserRuleCallResult` | Available when a managed stack frame manager is installed | Available | Captured after successful child `@after`; immutable; absent and present-null differ |
| `x=child` retention | Managed parent-frame state | `TryGetLabeledRuleCallResult` / `TryGetLabeledRuleCallReturn` | Last successful result wins; failures do not overwrite |
| `xs+=child` retention | Managed parent-frame state | `GetLabeledRuleCallResults` / `GetLabeledRuleCallReturns` | Successful results append in order; missing requested returns are skipped |
| Backtracking/memoization | Rollback-aware with managed execution state | Rollback-aware | Memoized returns may be reused, but current call-site label metadata is reapplied |
| ANTLR attribute syntax | Unsupported in runtime-inline execution | Limited in generated C# | Declared current-rule bare `$returnName` reads/writes, current-rule parameters/locals as documented; assignment-label `$c.value`/`$x.value` reads and list-label `$xs.value` projections are supported only by the optional generated-C# transformer in inline parser actions and `@after`; `$child.value`, `$rule.value`, `$ctx`, `$c.ctx`, `$xs.ctx`, bare labels, writes to label returns, `@init`/predicate label-return reads, token/lexer attributes, implicit fields, and automatic binding remain unsupported |
| Lexer labels/returns | Unsupported | Unsupported | No lexer invocation frames are added |

### Limited parser return attribute rewrite

| Generated-C# form | Status | Boundary |
|---|---|---|
| `$value = ...` for a declared current-rule return | Supported in inline parser actions and `@after` | Rewrites to typed `SetRequiredRuleReturn<T>(context, "value", ...)` and stores in the current invocation frame |
| `$value` for a declared current-rule return | Supported in inline parser actions and `@after` | Rewrites to typed `GetRequiredRuleReturn<T>(context, "value")`; missing runtime values preserve required-helper failure semantics |
| `$child.value`, `$c.value`, `$x.value`, `$xs.value`, `$rule.value` | Unsupported | No child, labeled, list-labeled, or dotted current-rule return convenience is generated |
| Token attributes, bare undeclared attributes, writes to undeclared returns, chains, list indexing syntax, lexer attributes | Unsupported | `UP0014`; no general ANTLR list-label or lexer compatibility |

This is not general ANTLR attribute compatibility. No typed variables, implicit fields, conversions, special `$xs[i]`/`$xs.value[i]` syntax, flow-sensitive definite assignment, or execution in conservative `Parse(...)` are introduced.

| Bare `$name` current-rule parameter/local read | Unsupported | Unsupported | Supported only for current-rule parameters/locals with raw declaration types; emits typed helper reads, performs no conversion, remains read-only, rejects predicates/writes/chains, and does not affect conservative `Parse(...)`. |

## Embedded-code transformation matrix

| Capability | Status | Notes |
|---|---|---|
| Embedded code preservation | Supported | Default no-op transformation keeps target-language code unchanged. |
| ANTLR-style attribute rewriting | Optional transformer | `$...` rewriting is not core parser/generator behavior. |
| Dynamic embedded-code compilation | Existing compiler path after transformation | No new compiler abstraction is introduced. |


### Embedded C# local writes

Default generation preserves `$local = ...` unchanged. Generated C# can opt into `CSharpAntlrStyleParserEmbeddedCodeTransformer` for current-rule local writes; the transformer rewrites supported assignment operators and statement-only increment/decrement to typed local getter/setter helpers. Parameters, labels, list-label projections, lexer attributes, `ref`/`out`, semantic-predicate writes, and increment/decrement expression values remain unsupported. Compound assignment uses getter/operator/setter and does not emulate C# special conversions.

### Embedded C# current-rule return writes

Default generation preserves `$returnName = ...` unchanged. Generated C# can opt into `CSharpAntlrStyleParserEmbeddedCodeTransformer` for a narrow current-rule return write convenience syntax in inline parser actions and rule `@after` code. Supported bare return-attribute assignments, compound assignments, and standalone increment/decrement statements are rewritten to typed `SetRequiredRuleReturn<T>` / `GetRequiredRuleReturn<T>` helper calls. The generated-C# opt-in path executes these writes through parser-managed frame return state, captures successful child returns in `ParserRuleCallResult`, keeps present-null distinct from missing, rolls back rejected alternatives, and restores memoized successful return snapshots without replaying actions. Parameters and labels remain read-only, list-label projections remain read-only, semantic predicates and `@init` reject return writes, `ref`/`out` is unsupported, and dotted current-rule return writes such as `$rule.value = ...` are unsupported.

> Current-rule return writes: with the optional C# ANTLR-style transformer, bare declared returns such as `$value = ...`, `$value += ...`, and standalone increments are supported in `@after` and inline parser actions. The default no-op transformer preserves the syntax unchanged. Runtime writes use parser-managed invocation-frame return state, are rollback-safe, are captured into successful `ParserRuleCallResult` snapshots for explicit helper APIs, distinguish present-null from missing, and do not auto-initialize returns. Dotted writes (`$rule.value = ...`), predicates, `@init`, parameters, labels, list projections, tokens, lexer attributes, and `ref`/`out` writes remain unsupported.


Parser and lexer grammar-level named-action support is source-generator C# only. In parser or combined grammars, unscoped `@header` / `@members` / `@footer` are treated as parser compatibility blocks, and scoped `@parser::header` / `@parser::members` / `@parser::footer` are equivalent parser compatibility blocks. They emit parser header code, generated execution-context members, or deterministic trailing parser source in grammar source order, and they still produce compatibility warnings (`UP1035`, `UP1031`, or `UP1036`) because invalid C# remains a Roslyn responsibility. Scoped lexer named actions (`@lexer::header`, `@lexer::members`, `@lexer::footer`) mirror the same limited injection model in combined or lexer grammars only with dedicated lexer markers; parser-only grammars keep them unsupported because no lexer is generated; lexer members are emitted into the existing generated execution context and do not create a separate ANTLR lexer runtime type. Parser named actions in lexer grammars are invalid for this generator, and unscoped `@header`, `@members`, and `@footer` are not parser compatibility blocks in lexer grammars. Unsupported named actions, unknown lexer/parser action names such as `@lexer::custom` or `@parser::custom`, and unknown scopes such as `@tree::members` produce deterministic `UP1029` diagnostics and are not silently injected. The default/no-op transformer preserves named-action content unchanged; optional transformer behavior remains opt-in, and `$...` current-rule attribute rewriting is intentionally limited to parser actions/lifecycle code, not parser or lexer header/member/footer content. Parser and lexer members can be called from generated inline parser actions and supported `@init`/`@after` lifecycle hooks, simple lexer inline actions and simple lexer predicates are supported only in the explicit generated-C# opt-in path.


## Lexer named actions

The generated-C# path supports grammar-level `@lexer::header`, `@lexer::members`, and `@lexer::footer` in combined or lexer grammars in the same limited compatibility model as parser named actions; parser-only grammars keep scoped lexer actions unsupported because no lexer is generated. Lexer headers are emitted after parser headers and before generated types, lexer members are emitted into the generated execution context after parser members, and lexer footers are emitted after parser footers with dedicated lexer markers. This supports the optional generated-C# lexer inline action read rewrite for `$text`, `$type`, `$channel`, `$mode`, `$line`, and `$pos`, plus the bounded simple `$type = ...`, `$channel = ...`, and `$mode = ...` write subset through `LexerActionExecutionResult`; it does not support `$text`/`$line`/`$pos` writes or complex `$mode` writes, lexer predicate attributes, a separate generated ANTLR lexer type, or full lexer target-language compatibility.


> Lexer inline actions and predicates: simple source-generator C# lexer inline actions and simple lexer predicates are now supported only through the explicit opt-in generated path. `Parse(...)` remains conservative. Predicates are evaluated during lexer matching and can reject only the current token path; actions execute after token acceptance and do not run when an earlier predicate rejects that path. Fragments, lexer rule references, simple quantifiers, duplicate source text at distinct positions, and already-supported commands such as `skip` are covered by regression tests. `AlternativeIndex` and `ElementIndex` identify the source hook location rather than a quantified runtime iteration. Lexer `$...` rewriting is limited to generated-C# opt-in inline action reads (`$text`, `$type`, `$channel`, `$mode`, `$line`, `$pos`) and simple `$type = ...`/`$channel = ...`/`$mode = ...` statement writes; lexer predicate attributes, runtime-inline lexer execution, a separate runtime lexer, generalized action buffering/replay, general lexer rollback, and external side-effect rollback remain unsupported.


## Limited lexer attribute rewrite status

| Feature | Default/no-op | Runtime-inline | Generated C# with optional C# transformer |
|---|---|---|---|
| Lexer inline action reads (`$text`, `$type`, `$channel`, `$mode`, `$line`, `$pos`) | Preserved as raw target code | Unsupported | Partially supported. Rewritten to generated execution-context helpers. `$text` reads `LexerActionExecutionContext.Text`; `$type` reads `TokenType`; `$channel` reads `Channel`; `$mode` reads `Mode`; `$line` reads `Line`; `$pos` reads `Column`. Values are accepted-token/chunk context metadata before lexer commands apply, and `$pos` is this runtime's 1-based source column rather than full ANTLR `charPositionInLine` compatibility. |
| Lexer inline action writes (`$type = ...`, `$channel = ...`, `$mode = ...`) | Preserved as raw target code | Unsupported | Partially supported for simple identifier or string statement assignments only. Rewritten to `SetLexerType(result, "...")`, `SetLexerChannel(result, "...")`, or `SetLexerMode(result, "...")`; generated hooks write `LexerActionExecutionResult.TokenType`, `Channel`, or `Mode`, and `LexerEngine` applies the result before lexer commands run. `$mode = ...` replaces the current mode like `mode(...)` and does not push or pop the mode stack. |
| Other lexer action writes (`$text = ...`, `$line = ...`, `$pos = ...`, compound/coalescing/increment writes including `$mode += ...`/`$mode++`, dotted/chained writes, expression writes) | Preserved as raw target code | Unsupported | Deterministic transformer error |
| Lexer predicate `$...` attributes | Preserved as raw target code | Unsupported | Deterministic transformer error; lexer attributes remain action-only in the generated-C# opt-in path |
| Runtime-inline lexer actions/predicates | Not executed | Unsupported | Unsupported |
| Full ANTLR lexer embedded-code semantics | Unsupported | Unsupported | Unsupported |

The generated-C# helper values are passive lexer-action metadata, and simple writes cross the generated hook/runtime boundary only through `LexerActionExecutionResult`; they do not add a separate lexer runtime or general target-language logic to the runtime engine. `$type`/`$channel`/`$mode` writes are covered by edge-case tests for last-write-wins, same-action type/channel/mode writes, fragments, lexer rule references, quantifiers, rejected alternatives, `more`, command override, and deterministic diagnostics for complex unsupported forms.


### Generated-C# explicit simple positional rule-call binding

Generated parsers can explicitly install a generated-C#-only rule-call policy for `ParseWithEmbeddedCode(...)` when generation enables simple positional rule-argument binding. When a parser rule call supplies raw positional arguments, the generated policy first requires the raw positional argument count to exactly match the declared target-rule parameter count, including zero-parameter target rules; an explicit empty argument list such as `child[]` is therefore valid only when the target declares zero parameters. This generated-C# automatic boundary is stricter than the reusable typed runtime policy: declared parameter defaults are not consumed to satisfy omitted generated-C# call-site arguments. After exact arity passes, the generated policy converts supported simple literals and submits one atomic managed seed batch to the existing invocation-frame parameter store. The conservative generated `Parse(...)` path remains unchanged and does not execute this binding path.

Supported automatic generated-C# argument forms are intentionally narrow: exact-arity simple positional literals that the typed literal binding policy can convert to the declared parameter type, including decimal integer literals for `int` parameters. Named arguments and arbitrary C# expressions remain unsupported and are rejected deterministically in the generated-C# explicit binding path before child lifecycle hooks can observe partially seeded state. Full ANTLR-compatible generated rule signatures such as `child(int value)` are still not emitted; generated hooks should continue to read parameters through frame helpers such as `GetRequiredRuleParameter<T>(context, "name")`, and the optional C# ANTLR-style transformer may rewrite `$name` to those helpers. Explicit runtime policies such as `TypedPositionalLiteralRuleCallExecutionPolicy` may still support simple typed defaults separately when callers install them directly.

The implementation uses existing parser-managed pending seeds, invocation frames, execution-state snapshots, rollback, and memoization boundaries. No target-language expression evaluator was added to `ParserEngine`.

### Generated-C# returns/labels boundary and named-action strategy

The rule-return and labeled rule-call boundary follows the existing parser named-action architecture rather than a parallel implementation path. Classification of grammar-level named actions is centralized in `EmbeddedMembersSupport`: `@members` and `@parser::members` are parser compatibility blocks injected into the generated execution context, `@header` and `@parser::header` are injected near the top of generated C# source, and `@footer` and `@parser::footer` are injected as trailing generated C# source. Unsupported parser-scoped actions such as `@parser::init` and parser named actions inside lexer grammars remain deterministic diagnostics and are not generated-source injection points.

Parser embedded code must continue to pass through `IParserEmbeddedCodeTransformer` via `TransformEmbeddedCode(...)`. The default path preserves target-language code, and generated-C# embedded-code paths remain opt-in. Metadata is not execution authority: rule-return declarations may be present in grammar metadata, and labeled rule-call storage may be present in parser-managed frame state, but metadata/storage alone does not imply automatic runtime support, ANTLR-compatible label access, public typed parser contexts, `$label.ctx`, `$ctx`, or public ANTLR-style rule methods. Conservative `Parse(...)` remains unchanged, and `ParserEngine` remains target-language-neutral.

Future simple generated-C# return assignment/access should reuse generated execution-context helpers and optional transformer rewriting. Future labeled rule-call return access should build on existing labeled result storage where available. Any `$...` syntax support must be implemented through the parser embedded-code transformer, not the runtime parser core. No full ANTLR parser context model is promised by the current generated-C# compatibility bridge.


## Generated-C# explicit labeled return helper boundary

Generated-C# opt-in supports explicit helper access to parser-rule labels plus narrow assignment-label `$c.value` sugar for parser-rule return reads in inline parser actions and `@after`. Assignment labels can be inspected with `TryGetLabeledRuleCallResult` and `GetRequiredLabeledRuleCallReturn`; list labels can be inspected with `GetLabeledRuleCallResults` and `GetLabeledRuleCallReturns`. Present-null values are distinct from missing return names, absent list labels project as empty lists, failed alternatives do not leak label state, and memoized child returns preserve values while using the successful call-site label.

The assignment-label syntax `$c.value`/`$x.value` rewrites to required helper access only in inline parser actions and `@after`. The list-label syntax `$xs.value` is also supported in those same generated-C# action locations and rewrites to the list-return projection helper. `$child.value`, `$rule.value`, `$ctx`, `$c.ctx`, `$xs.ctx`, bare labels, label-return writes, `@init` label-return reads, semantic-predicate label-return reads, token attributes, and lexer attributes remain unsupported and should produce deterministic transformer diagnostics rather than rewrites. Generated `Parse(...)` stays conservative, and `ParserEngine` does not gain target-language-specific C# logic.


Generated-C# list-label return sugar is intentionally narrow: `$xs.value` is available only in inline parser actions and `@after` when `xs` is a visible parser-rule list label from `xs+=child` and every referenced child rule declares `value`. The transformer rewrites only the `$xs.value` root/projection to `GetLabeledRuleCallReturns(context, "xs", "value")`; any following C# member access, for example `.Count`, is ordinary C#. The projection is read-only, reads only successful child calls, preserves order and present-null values, and follows the same rollback semantics as the explicit helper. It is unavailable in `@init`, semantic predicates, parser/lexer members, headers, footers, and lexer actions. `$child.value`, `$rule.value`, `$xs.ctx`, `$ctx`, typed parser contexts, public ANTLR-style parser methods, label writes, token attributes, and lexer attributes remain unsupported. Conservative `Parse(...)` remains unchanged and `ParserEngine` remains target-language-neutral.

### Generated-C# parser return convenience boundary

Supported generated-C# opt-in convenience forms are deliberately narrow:

- bare `$value` reads/writes a declared return of the current rule;
- `$c.value` / `$x.value` read a declared child return through an assignment label such as `c=child`;
- `$xs.value` reads a list-label projection through `xs+=child` and returns the generated helper list.

The following remain unsupported and must produce deterministic transformer diagnostics rather than new runtime syntax: `$child.value`, `$rule.value`, `$ctx`, `$c.ctx`, `$xs.ctx`, bare `$c` / `$xs` label objects, writes to `$c.value` or `$xs.value`, label-return reads in `@init`, label-return reads in semantic predicates, token attributes such as `$t.text`, lexer attributes, typed parser contexts, public ANTLR-style parser rule methods, and general ANTLR attribute compatibility.

These forms are optional `IParserEmbeddedCodeTransformer` rewrites for generated C# only. The default/no-op transformer leaves `$...` text unchanged, conservative `Parse(...)` remains unchanged, and `ParserEngine` remains target-language-neutral. Parser-managed return and label state follows the existing rollback semantics; no rollback of external side effects is implied.
