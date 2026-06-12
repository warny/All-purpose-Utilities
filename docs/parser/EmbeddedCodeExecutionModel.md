# Embedded ANTLR Code Execution Model

## 1. Introduction

This document defines the architecture boundary for embedded ANTLR code in `Utils.Parser`.

It is a **design and documentation** reference only. It does not introduce execution behavior changes.

The goal is to maximize ANTLR compatibility while preserving runtime determinism, parser authority, and language-neutral core responsibilities.

## 2. ANTLR standard behavior

In standard ANTLR, embedded code is interpreted as target-language code and injected into generated lexer/parser code.

Typical constructs:

- semantic predicates: `{ condition }?`
- inline parser actions: `{ code }`
- rule actions: `@init { code }`, `@after { code }`
- grammar actions: `@header`, `@members`, `@parser::members`, `@lexer::members`
- lexer predicates and lexer actions

Standard ANTLR semantics:

- `{ code }` is emitted as target-language executable code.
- `{ condition }?` is emitted as target-language conditional logic.
- `@members` adds members to the generated target-language class.

## 3. Current `Utils.Parser` behavior

Current behavior is intentionally conservative by default. The canonical compatibility status is maintained in [`ANTLRCompatibility.md`](./ANTLRCompatibility.md); this document focuses on execution architecture.

- Semantic predicates are recognized and routed through `ISemanticPredicateEvaluator`.
- Inline parser actions are recognized and routed through `IParserActionExecutor`.
- `@init` and `@after` are recognized and stored on the rule model; they are not executed by default, but the source-generator C# path now generates and executes lifecycle hook methods for them through `ParseWithEmbeddedCode(...)` or an explicit-context `CreateRuntimePolicy(executionContext, basePolicy)` result, with missing declared local names allocated as untyped `null` frame entries before `@init` and explicit helper methods for lifecycle hook code to read/write only that store. Existing entries are preserved; no typed fields/properties or implicit local variables are generated.
- Grammar actions and `@members` are preserved as metadata only when visible to ingestion.
- Lexer predicates and lexer actions are outside the current executable embedded-code scope.
- No raw embedded ANTLR target-language code is executed automatically.

Two explicit opt-in paths exist for parser semantic predicates and inline parser actions:

1. a runtime-inline prepared expression path assembled by callers through `Utils.Parser.Expressions`;
2. a source-generator C# path emitted by `Utils.Parser.Generators` and activated through generated helpers.

Both paths install runtime policy handlers explicitly. `ParserEngine` remains language-neutral and default parsing remains conservative.

## 4. Two-path target architecture

The target architecture uses **two distinct implementation paths** over **one shared parser model**:

1. Source-generation path (compile-time `.g4` ingestion, C# source generation).
2. Runtime-inline path (runtime `.g4` ingestion with an explicitly provided `IExpressionCompiler`).

These paths must not be conflated. They share the same intent:

```text
compile/generate during parser model generation or source generation
execute during parsing
```

They must not converge on this weaker model as the architectural target:

```text
store source text
compile opportunistically during predicate/action evaluation
```

Shared expectations across both paths:

- same ANTLR construct classification;
- same model concepts;
- same deterministic diagnostic vocabulary;
- same runtime authority boundaries;
- prepared executable output before parsing begins when executable embedded code is enabled.

The prepared output is intentionally path-specific:

- `Utils.Parser.Generators` produces C# source hooks compiled by Roslyn in the consuming project for supported parser predicates/actions;
- runtime-inline ingestion produces compiled expression artifacts through the configured `IExpressionCompiler` when callers run the prepared registry/policy builder.

Adapters may differ by path, but model semantics and runtime dispatch indexes must stay aligned.

## 4.1 Minimal preparation boundary

The repository now contains a minimal preparation boundary in `Utils.Parser` for embedded-code preparation work. These contracts are public so optional packages such as `Utils.Parser.Expressions` can produce artifacts without duplicating the embedded-code model. They model:

- raw embedded source text and construct kind (`EmbeddedCodeSource`, `EmbeddedCodeKind`);
- explicit target path metadata (`EmbeddedCodePreparationContext`, `EmbeddedCodeTarget`);
- the contextual symbol model (`ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`);
- preparation outcomes (`EmbeddedCodePreparationResult<TArtifact>`, `EmbeddedCodePreparationStatus`);
- one narrow preparation interface for semantic predicates and inline parser actions (`IEmbeddedCodePreparer<TPredicateArtifact, TActionArtifact>`).

This boundary is intentionally metadata/preparation-only. It is not wired into `ParserEngine`, does not change scheduling or memoization, and does not activate embedded code by default. The neutral preserving preparer returns explicit preserved/unsupported metadata and never compiles or executes embedded source. Making the boundary public is an API exposure for pre-release preparation/tooling integration only; it is not runtime activation.

## 5. Source generator C# path

Pipeline:

- `.g4` consumed as `AdditionalFiles` by `Utils.Parser.Generators`;
- grammar parsed by internal G4 tokenizer/parser;
- C# emitted by `GrammarEmitter`;
- final compilation performed by Roslyn.

Current state:

- generated model construction is implemented;
- embedded predicates and actions continue to be preserved as metadata strings such as `ValidatingPredicate("...")` and `EmbeddedAction("...", ...)`;
- a generated execution context class (`{ClassName}ExecutionContext`) owns generated C# hooks, any injected parser `@members` blocks, and generated `Fork()` / `CopyFrom(...)` copy helpers;
- generated C# hooks are emitted as instance methods on that context for supported parser semantic predicates and inline parser actions;
- generated dispatchers implement `ISemanticPredicateEvaluator` and `IParserActionExecutor` and are bound to one execution-context instance;
- generated `ParseWithEmbeddedCode(...)` helpers provide the fresh-context opt-in path, and generated `CreateRuntimePolicy(executionContext, basePolicy)` binds a policy to a caller-supplied execution context;
- generated `ParseWithEmbeddedCode(string input)` creates a fresh execution context for that parse, while the overload accepting `{ClassName}ExecutionContext` lets advanced callers supply and observe a context explicitly;
- generated `Fork()` returns a copied execution context through `ParserExecutionContextCopier<TContext>.Copy(...)`, preserving `ICloneable` precedence when a user partial context implements it;
- generated `CopyFrom(source)` validates `source` and copies source state into the current context through `ParserExecutionContextCopier<TContext>.CopyTo(source, this)`;
- generated `Parse(...)` remains conservative and does not install generated embedded-code hooks.

Context-copy preparation:

- `Utils.Parser.Runtime.ParserExecutionContextCopier<TContext>` is available as a public runtime helper for future generated-context snapshot/fork/commit designs and is exposed by generated execution contexts through `Fork()` and `CopyFrom(...)`;
- the helper inspects each closed context type once, builds a compiled `Action<TContext, TContext>` field-copy delegate, and caches that delegate through the closed generic type;
- `Copy(source, factory)` first uses `source.Clone()` when `source` implements `ICloneable`; the clone result must be non-null and assignable to the context type;
- the semantics of `ICloneable.Clone()` belong to the user context type, and the caller-provided factory is used only when the source does not implement `ICloneable`;
- `Copy(source, factory)` creates the target instance through the caller-supplied factory for field-copy contexts so generated code can copy internal or non-public-constructor contexts from the consuming assembly;
- `CopyTo(source, target)` copies into an existing context through the field-copy delegate and intentionally does not use `ICloneable`, making it suitable for future commit/restore experiments;
- field-copy behavior is a shallow structural copy, not a universal deep copy: value fields, strings, nullable values, enums, and unrecognized references are assigned directly;
- known containers are recreated with explicit copy expressions (`T[]`, `List<T>`, `Dictionary<TKey,TValue>`, and `HashSet<T>`), but their elements remain shallow-copied references or values;
- unknown `IEnumerable<T>` collection fields may also be recreated when they expose a compatible public copy constructor, or a public parameterless constructor plus `AddRange(IEnumerable<T>)`, or a public parameterless constructor plus `Add(T)`;
- unknown collections without one of those safe reconstruction strategies are copied by reference instead of failing or producing a partial copy;
- null known or reconstructable containers remain null, static fields are not copied, field-like event backing fields are skipped, and readonly instance fields cause an explicit configuration exception instead of being ignored silently;
- compiler-generated auto-property backing fields are treated as context state and are copied unless they are readonly;
- generated `Fork()` and `CopyFrom(...)` are helpers for snapshot/fork/commit work; generated policies expose them through `IParserExecutionStateManager`, and `ParserEngine` now calls the manager around parser backtracking attempt boundaries: ordinary parser alternatives, left-recursive extensions, quantifier attempts, and negation probes. This rollback is limited to managed parser execution state: it does not add complete ANTLR transactional semantics, action buffering, external side-effect rollback, lexer actions, or lexer predicates. Parser `@init` and `@after` hooks participate only in the source-generator C# opt-in path.

Execution boundary:

- embedded ANTLR code selected for execution in this path is treated as C# source;
- the generator emits explicit C# hook code;
- Roslyn compiles that generated C# as part of the consuming project;
- parsing invokes the already-generated hook rather than asking `ParserEngine` to compile source text;
- the generator performs light body normalization only and leaves C# validation to Roslyn.

Boundary rules:

- invalid embedded C# is a C# compilation problem, not a parser-runtime evaluation problem;
- this path is C#-specific and belongs to `Utils.Parser.Generators`;
- this path does not imply runtime support for arbitrary target-language code;
- generated hook execution remains opt-in through generated policy helpers.

## 6. Runtime-inline expression compiler path

Pipeline target:

- `.g4` parsed at runtime;
- embedded ANTLR code classified and preserved as raw text metadata;
- an explicit expression compiler is selected by configuration, not by `ParserEngine`;
- preparation/generation compiles embedded code through `IExpressionCompiler`;
- the prepared expression/delegate/function is stored in the parsing model or in an adjacent executable artifact;
- parsing executes that prepared artifact through runtime policy interfaces.

Conceptual mapping:

- semantic predicates execute through `ISemanticPredicateEvaluator`;
- parser inline actions execute through `IParserActionExecutor`.

`ISemanticPredicateEvaluator` and `IParserActionExecutor` are runtime execution interfaces. They are not, by themselves, the complete generation/preparation boundary for embedded code. The explicit preparation boundary now exists separately and can produce path-specific artifacts before parsing. Optional runtime adapters can consume prepared expression artifacts through `ParserRuntimeFeaturePolicy`, and `Utils.Parser.Expressions` now provides an explicit convenience builder that assembles that prepared policy. These artifacts are still not prepared automatically and are not invoked by default by `ParserEngine`.

Strict rules:

- no raw target-language execution;
- no implicit language selection;
- `IExpressionCompiler` must be explicitly injected or otherwise explicitly selected by the caller;
- `ParserEngine` must not know whether the embedded source was C#, VB-like syntax, or another expression language;
- `ParserEngine` must not become responsible for compiling embedded source text;
- `Utils.Parser` core must not reference `Utils.Expressions.CSyntax` or `Utils.Expressions.VBSyntax` directly.

Current intermediate status:

- `ExpressionEmbeddedCodePreparer` in `Utils.Parser.Expressions` can prepare runtime-inline semantic predicate and inline parser action artifacts through an explicitly supplied `IExpressionCompiler`.
- Prepared expression artifacts only expose contextual symbols allowed by `EmbeddedCodePreparationContext.SupportedSymbols`. Exposed symbols (`ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`) are resolved from the runtime context parameter at execution time, avoiding capture of preparation-time values.
- The expression-backed preparer returns `PreservedNotCompiled` for the source-generator C# target because that path belongs to `Utils.Parser.Generators`, not to runtime-inline expression preparation.
- The preparer is not connected to `ParserEngine`; therefore it does not change default runtime behavior.
- `PreparedExpressionEmbeddedCodeRegistry` in `Utils.Parser.Expressions` can store prepared semantic predicates separately from prepared parser inline actions. Its key uses the embedded-code kind, owning rule name, raw source text, alternative index, and element index, which is the safest audit-friendly identity currently available from preparation metadata and runtime contexts without modifying `ParserEngine`.
- `PreparedExpressionEmbeddedCodeRegistryBuilder` in `Utils.Parser.Expressions` can explicitly scan an already-built `ParserDefinition`, prepare `ValidatingPredicate` and inline parser `EmbeddedAction` nodes, populate a registry, and return build entries for successes, non-success preparation results, duplicate keys, and skipped unsupported actions.
- The registry builder uses the same local index strategy exposed by runtime contexts: alternatives are considered in scheduler priority order, sequence items use zero-based element indexes, quantifier and negation inner probes use the active runtime direct-inner element index, direct-left-recursive recursive alternatives are prepared from the runtime tail after leading self-reference removal, and unavailable indexes remain absent rather than invented.
- `PreparedExpressionSemanticPredicateEvaluator` maps registered `PreparedExpressionSemanticPredicate` artifacts to `ISemanticPredicateEvaluator` without depending on `IExpressionCompiler` or compiling source text during evaluation.
- `PreparedExpressionParserActionExecutor` maps registered `PreparedExpressionParserAction` artifacts to `IParserActionExecutor` without depending on `IExpressionCompiler` or compiling source text during execution.
- `PreparedExpressionRuntimePolicyBuilder` assembles the full opt-in path: caller-supplied `IExpressionCompiler`, `ExpressionEmbeddedCodePreparer`, `PreparedExpressionEmbeddedCodeRegistryBuilder`, registry-backed evaluator/executor, and a `ParserRuntimeFeaturePolicy`. The build result exposes the policy, registry, registry build result, and `HasFailures` audit summary.
- `PreparedExpressionRuntimePolicyBuilderOptions.BasePolicy` lets callers preserve unrelated policy settings while replacing only `SemanticPredicateEvaluator` and `ParserActionExecutor`.
- Missing prepared artifacts return conservative `NotEvaluated` / `NotExecuted` outcomes, so the existing parser fallback diagnostics and continuation behavior remain owned by `ParserEngine`.
- Automatic model-wide preparation from `ParserEngine` is not implemented; callers may invoke the explicit runtime policy builder or assemble the registry builder and adapters manually. Skips remain limited to constructs outside this runtime-inline path, such as grammar-level actions, rule lifecycle actions, and non-inline actions.
- `ExpressionSemanticPredicateEvaluator` maps `IExpressionCompiler` to `ISemanticPredicateEvaluator` for semantic predicates (`{ condition }?`).
- `ExpressionParserActionExecutor` maps `IExpressionCompiler` to `IParserActionExecutor` for inline parser actions (`{ code }`).
- The expression-compiler adapters are useful explicit runtime integration points, but they are an intermediate step rather than the final architectural boundary because they may compile opportunistically during predicate/action invocation.
- Default parser runtime behavior is unchanged (`NotEvaluated` with `UP1006` when applicable for predicates, `NotExecuted`/`UP1005` default behavior for actions).
- Expression-backed semantic predicate evaluation returns a structured outcome so compilation failures and delegate-shape adaptation failures can carry `UP1026` metadata, while `ParserEngine` remains the only component that emits diagnostics.
- Inline actions still do not control parse acceptance, parse-tree shape, or branch rejection.
- `Executed` and `NotExecuted` outcomes both continue parsing; no context mutation, no `ContextDelta`, and no lexer action/predicate or grammar-members execution support are introduced.
- The symbol model is intentionally minimal and read-only (`ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`).
- Predicate adapter cache: compilation-only and not parse-result memoization. Predicates that do not reference contextual symbols can be cached by predicate source; predicates referencing `ruleName`, `inputPosition`, `alternativeIndex`, or `elementIndex` are currently recompiled per evaluation to avoid context capture.
- Action adapter cache: compilation-only and not parse-result memoization. Non-contextual actions can be cached by action source; actions referencing `ruleName`, `inputPosition`, `alternativeIndex`, or `elementIndex` are currently recompiled per execution to avoid context capture.

The last two cache bullets describe the opportunistic-compilation adapters, not the prepared-artifact path. The prepared runtime-inline model prepares executable artifacts before parsing and executes those artifacts during parsing without opportunistic source compilation on predicate/action invocation. The prepared expression registry builder and runtime policy builder provide that explicit opt-in assembly step; automatic model preparation from `ParserEngine` remains out of scope.

## 7. Interface boundary

The preparation boundary is explicit and separated from runtime execution interfaces. The current interface is `IEmbeddedCodePreparer<TPredicateArtifact, TActionArtifact>`, with path-specific artifact types supplied by the implementation package.

Separation of concerns:

- preparation/generation boundary: receives embedded source plus compilation context and produces a path-specific executable artifact before parsing;
- runtime evaluation boundary: `ISemanticPredicateEvaluator`;
- runtime execution boundary: `IParserActionExecutor`.

The execution interfaces consume runtime context and return runtime outcomes. They should not become responsible for selecting the embedded language, compiling raw source text as part of parsing, or owning parser diagnostics.

## 8. Cache boundary

Allowed cache scope for future embedded-code preparation:

- key: raw embedded code + compilation context;
- value: path-specific executable artifact, such as generated C# source/hook metadata for the generator path or a compiled expression/delegate/function for the runtime-inline path.

Example conceptual key fields:

- source text;
- construct kind;
- expected result type;
- compiler identity/language;
- symbol model version.

Non-goal boundary:

- this preparation cache is **not** parse-result memoization;
- no `(input position + rule) -> parse result` semantic memoization changes;
- future caching should avoid recompiling source text during predicate/action invocation.

## 9. Predicate vs action mapping

### Semantic predicate `{ condition }?`

Conceptual outcomes:

- compiled boolean expression;
- `true` -> `SemanticPredicateEvaluationOutcome.Satisfied`;
- `false` -> `SemanticPredicateEvaluationOutcome.Rejected`;
- unsupported/failed -> `NotEvaluated` + diagnostic.

### Parser inline action `{ code }`

Conceptual outcomes:

- compiled action/effect expression;
- executable path -> `ParserActionExecutionOutcome.Executed`;
- unavailable path -> `ParserActionExecutionOutcome.NotExecuted` + diagnostic.

### Rule actions `@init` / `@after`

Current status:

- recognized;
- stored on `Rule.InitAction` / `Rule.AfterAction`;
- not automatically executed.

Future possibilities (separate PRs):

- source generator C# hook path;
- runtime explicit compiler + explicit runtime policy.

### Grammar actions

`@header` and `@lexer::members` remain metadata-only by default. In the source-generator C# path only, unscoped `@members` and `@parser::members` are injected into the generated execution context; the runtime-inline prepared expression path still treats them as non-executable metadata.

Future source generator mapping may provide C#-specific explicit hooks. Runtime ingestion must not execute raw grammar members.

### Lexer actions and predicates

Lexer embedded semantics are a separate, higher-risk domain because they can affect tokenization, mode transitions, channel/type behavior, and stateful lexing.

They must be handled in dedicated future design/implementation work and not conflated with parser inline action support.

## 10. Project responsibilities

### `Utils.Parser`

Responsible for:

- runtime grammar ingestion;
- embedded-code recognition/classification;
- raw source preservation in model metadata;
- routing via runtime policy abstractions;
- internal preparation boundary contracts for future executable or generable artifacts;
- deterministic diagnostics;
- preserving `ParserEngine` authority.

Not responsible for:

- interpreting C# or VB source;
- implicit language selection;
- direct target-language compilation/execution;
- hidden semantic state ownership.

Current preparatory helpers:

- `ParserRuleInvocationFrame`, `ParserRuleInvocationDescriptor`, `IParserRuleInvocationFrameManager`, `NullParserRuleInvocationFrameManager`, and `StackParserRuleInvocationFrameManager` provide passive per-rule invocation-frame infrastructure with explicit call-stack semantics. `ParserEngine` enters a frame for actual rule execution, passes a descriptor built from currently available parser-rule metadata, and exits it with a success flag in a `try/finally` block; `ParserRuleLifecycleContext` can expose that frame to lifecycle hooks. Frames now expose `Parent` (the caller's frame, or `null` for root-level rules) and `Depth` (zero-based call-stack depth). `StackParserRuleInvocationFrameManager` is the stack-aware implementation: `Enter(...)` creates a child of the current frame and makes it current; `Exit(frame, succeeded)` pops the matching frame and restores the parent as current; a mismatched exit throws `InvalidOperationException`. Generated C# opt-in runtime policies (`CreateRuntimePolicy`) now install `StackParserRuleInvocationFrameManager` automatically; lifecycle hooks in `ParseWithEmbeddedCode(...)` can observe `context.InvocationFrame.Parent` and `context.InvocationFrame.Depth`. The frame stack is implicitly rollback-aware: the engine's `try/finally` structure ensures every rule entry's frame is exited regardless of success, so failed alternatives, quantifier iterations, negation probes, and memoization hits cannot leave stale frames. The conservative `Parse(...)` policy continues to use `NullParserRuleInvocationFrameManager.Instance`. This call-stack model is preparatory infrastructure for future rule return and argument support only: rule parameters, returns, throws/catch/finally metadata, and rule options remain metadata-only and are not bound, typed, propagated, applied, or executed automatically. Rule locals also remain untyped and unbound; only the generated C# opt-in lifecycle executor allocates captured local names as missing-only `null` frame entries before `@init`. Rule locals are preserved as raw `Rule.Locals` metadata when available, and rule exception metadata is preserved as raw `Rule.ExceptionMetadata` for `throws`, `catch`, and `finally` when available. Descriptors can expose that preserved metadata through `RawLocals`, `Locals`, `Returns`, and `Exceptions`; they do not parse C# or ANTLR target-language declarations semantically, and they do not invent locals, return, or exception metadata that the runtime model does not expose. Return descriptor names are extracted lexically (e.g. `value` for `int value`) using the same top-level comma split strategy as locals; raw declarations are preserved verbatim. `ParserRuleCallResult` is available as an immutable snapshot of return values from a successfully completed child rule invocation; it is captured by `StackParserRuleInvocationFrameManager.PrepareCallResultForSnapshot` (called by `ParserEngine` before the post-rule execution-state snapshot) and stored on the parent frame's `LastCompletedChildCall`; the managed execution-state snapshot includes the call result via callback so rollback-safe memoization and alternative backtracking work correctly; failed alternatives clear stale call results on the parent frame and memoization hits restore the correct child result. Generated C# lifecycle hook bodies may explicitly call the frame-local helpers (`GetRuleLocal`, `TryGetRuleLocal`, `SetRuleLocal`, `GetRuleLocalDescriptors`), the frame-return helpers (`GetRuleReturn`, `TryGetRuleReturn`, `SetRuleReturn`, `GetRuleReturnDescriptors`), the frame-parameter helpers (`GetRuleParameter`, `TryGetRuleParameter`, `GetRuleParameterDescriptors`), and the parameter-seeding helpers (`SetNextRuleParameter`, `ClearNextRuleParameters`) in `ParseWithEmbeddedCode(...)`; parameter names are extracted lexically (e.g. `value` for `int value`); frames are not auto-populated with parameter values by default; rule call arguments are not evaluated as arbitrary expressions, while the explicitly installed positional literal policy can seed its limited supported values; `$param` is not supported; `SetNextRuleParameter` seeds an untyped value for the next invocation of the named child rule — seeds are consumed by `StackParserRuleInvocationFrameManager.Enter`, copied into the matching child frame, and are rollback-safe (included in managed execution-state snapshots so failed alternatives do not leak seeds); `callee[expr]` is not evaluated and generated parser signatures are unchanged; inline-action overloads of `SetNextRuleParameter` and `ClearNextRuleParameters` accepting `ParserActionExecutionContext` are also available in `ParseWithEmbeddedCode(...)`, routing through the instance `_frameManager` field on the generated execution context with identical rollback-safe semantics; for locals: existing entries are not overwritten, array-looking declarations remain `null`, and no typed default values, typed local members, or implicit action variables are generated; for returns: return entries are not auto-allocated, returns are not propagated to caller frames, no typed return fields/properties are generated, and `$rule.value` and labeled rule-reference return access are not supported. `throws` does not change parser exception behavior, catch/finally blocks remain non-executable, no typed ANTLR-compatible rule invocation semantics are implemented, no `$rule.value` or argument passing is added, and generated `Parse(...)` remains conservative.
- `ParserExecutionContextCopier<TContext>` provides a reusable runtime copy primitive for parser execution contexts. Fields marked with `[ParserExecutionStateIgnored]` are excluded from copying and hashing; this attribute is applied to infrastructure fields on generated contexts (such as `_frameManager`) that must not participate in execution-state snapshots. Generated execution contexts expose this primitive through `Fork()` and `CopyFrom(...)`. Generated runtime policies also expose an `IParserExecutionStateManager` through `ParserRuntimeFeaturePolicy.ExecutionStateManager`; the generated manager captures with `Fork()` and restores with `CopyFrom(...)`. The default runtime policy uses `NullParserExecutionStateManager.Instance`. ParserEngine now captures and restores managed parser execution state around parser backtracking attempt boundaries, and memoized rule results carry post-rule execution-state snapshots that are restored on memoization hits without replaying actions. This does not provide complete ANTLR transactional semantics.

API compatibility note: `ParserRuntimeFeaturePolicy.ExecutionStateManager` and `ParserRuntimeFeaturePolicy.RuleInvocationFrameManager` are required. Prefer `ParserRuntimeFeaturePolicy.Default with { ... }` when customizing a policy so the no-op default manager is preserved automatically. Direct `new ParserRuntimeFeaturePolicy { ... }` initializers must now set `ExecutionStateManager = NullParserExecutionStateManager.Instance` and `RuleInvocationFrameManager = NullParserRuleInvocationFrameManager.Instance` to keep conservative no-op behavior. This requirement enables managed parser execution-state rollback for parser backtracking attempt boundaries when a stateful manager is supplied, but it does not enable action buffering, replay, lifecycle hooks, external side-effect rollback, lexer embedded-code execution, typed rule invocation semantics, generated parser method signature changes, return propagation, local allocation, rule option semantics, or exception metadata execution.

```csharp
var policy = ParserRuntimeFeaturePolicy.Default with
{
    SemanticPredicateEvaluator = customEvaluator
};

var directPolicy = new ParserRuntimeFeaturePolicy
{
    SemanticPredicateEvaluator = new DefaultSemanticPredicateEvaluator(),
    ParserActionExecutor = new DefaultParserActionExecutor(),
    ExecutionStateManager = NullParserExecutionStateManager.Instance,
    RuleInvocationFrameManager = NullParserRuleInvocationFrameManager.Instance
};
```

### `Utils.Parser.Diagnostics`

Responsible for shared diagnostic descriptors across runtime and generator/tooling surfaces.

Future shared embedded-code diagnostics should live here when shared by runtime and generator paths.

The shared embedded-code diagnostic taxonomy is:

- `UP1024 EmbeddedCodeLanguageUnsupported`
- `UP1025 EmbeddedCodeCompilerNotConfigured`
- `UP1026 EmbeddedCodeCompilationFailed`: currently used by expression-backed adapters for compile and delegate adaptation failures, plus compiled-action execution failures.
- `UP1027 EmbeddedCodePreservedNotCompiled`
- `UP1028 EmbeddedCodeExecutionDisabled`: reserved for explicit runtime policies that intentionally disable embedded-code execution. Current expression-backed adapters do not expose an `Enabled = false` policy and therefore do not emit this diagnostic.

These diagnostics define capability boundaries. They do not imply that every diagnostic is emitted by current adapters.

### `Utils.Parser.Generators`

Current responsibilities:

- Roslyn source generation (`netstandard2.0` analyzer/generator project);
- compile-time `.g4` ingestion via `AdditionalFiles`;
- internal G4 parsing and C# emission;
- preservation of embedded code as raw model metadata strings;
- C#-only executable hooks for parser semantic predicates and inline parser actions in generated grammars;
- generated `ISemanticPredicateEvaluator`, `IParserActionExecutor`, and `ParserRuntimeFeaturePolicy` wiring;
- generated `ParseWithEmbeddedCode(...)` helper for explicit opt-in execution while generated `Parse(...)` keeps the default conservative policy;
- generated execution context classes that own instance hooks and injected unscoped `@members` / `@parser::members` C# members;
- runtime-index-aware hook dispatch for tested parser hook positions: single-item alternatives, sequence positions, quantified content, negation predicate probes, same-source hooks in distinct alternatives, and direct-left-recursive tail views because generated helpers resolve the generated definition before parsing with the generated policy;
- Roslyn diagnostic reporting and C# compilation errors for invalid embedded C# in the source-generator path, including invalid injected members or member-name collisions;
- generator warning `UP1031 EmbeddedMembersInjectedByGenerator` for unscoped `@members` and `@parser::members` injected into the generated execution context;
- generator warning `UP1029 EmbeddedCodeConstructNotExecutedByGenerator` for visible embedded-code constructs that are not executable generated hooks, including lexer actions/predicates, unsupported grammar actions, and `@lexer::members`; parser `@init` and `@after` hooks are generated C# opt-in lifecycle hooks and do not produce `UP1029`.

Future responsibilities (source-generation path):

- additional source-generator C# hook shapes beyond parser predicate expressions, parser predicate blocks with `return`, and inline parser action statement bodies;
- clear distinction between preserved raw metadata and executable generated hooks;
- lexer embedded-code hooks only after dedicated design work;
- deterministic semantics for parser actions inside negation probes only after dedicated design work and tests.

### `Utils.Parser.VisualStudio`

Responsible for tooling and integration surfaces (diagnostic/grammar information presentation), not execution of embedded grammar code.

### `Utils.Parser.VisualStudio.Worker`

Responsible for isolated tooling worker scenarios and future diagnostics surfacing, not arbitrary embedded-code execution without explicit future policy.

### `Utils.Expressions.*`

Role:

- optional expression compiler providers;
- shared contract via `IExpressionCompiler`;
- examples: `Utils.Expressions.CSyntax`, `Utils.Expressions.VBSyntax`.

Usage rules:

- injected explicitly by consumers/adapters;
- used through contracts;
- no direct dependency from `Utils.Parser` core.

## 11. Diagnostics strategy

Source-generator diagnostics include `UP1029 EmbeddedCodeConstructNotExecutedByGenerator`, a warning for visible unsupported embedded-code constructs in `.g4` files. The diagnostic is emitted only for constructs that are not promoted to generated C# hooks or injected parser members; it must not be emitted for supported parser semantic predicates, supported inline parser actions, unscoped `@members`, or `@parser::members`, even when their C# is invalid. Invalid C# remains owned by Roslyn. Source-generator diagnostics also include `UP1031 EmbeddedMembersInjectedByGenerator`, a compatibility warning that states unscoped `@members` or `@parser::members` was injected into the generated execution context as C# source.

Diagnostics should continue to be defined in `Utils.Parser.Diagnostics` so they can be used by:

- runtime ingestion;
- source generator Roslyn reporting;
- Visual Studio/tooling display.

Candidate future diagnostic improvements include:

- embedded code language unsupported;
- embedded code compiler not configured;
- embedded code compilation failed;
- embedded code preserved but not compiled;
- embedded code execution disabled by policy.

## 12. Non-goals

This model explicitly excludes:

- runtime C# compilation in `Utils.Parser` core;
- implicit embedded-code language inference;
- automatic execution of raw ANTLR target code;
- parser scheduler changes;
- `ParserEngine` authority transfer;
- complete rollback/replay semantics;
- use of `IParserExecutionStateManager`, generated execution-context `Fork()` / `CopyFrom(...)`, or `ParserExecutionContextCopier<TContext>` as speculative-execution authority beyond managed parser execution-state rollback at parser backtracking attempt boundaries;
- hidden semantic runtime state;
- parse-tree shape changes;
- direct `Utils.Parser` dependency on `Utils.Expressions.CSyntax` or `Utils.Expressions.VBSyntax`;
- runtime behavior changes from the current preparation-boundary contracts.

## 13. Future PR plan

### Completed — Documentation/design realignment

- clarify the two execution paths and the prepare-before-parse target;
- document current expression-backed adapters as an intermediate runtime integration step;
- no behavior change.

### Completed — Explicit preparation boundary

- introduce an internal minimal boundary that represents embedded-code source, preparation context, target path, preparation status, and path-specific artifacts;
- keep the boundary disconnected from automatic `ParserEngine` activation;
- preserve `ParserEngine` as an execution coordinator rather than a language compiler.

### Runtime-inline prepared expression path

- preparation contracts, the expression-backed preparer, prepared registry, registry builder, registry-backed adapters, and prepared runtime policy builder are available;
- callers opt in before parsing by building a policy with a supplied `IExpressionCompiler`;
- prepared adapters execute artifacts during parsing without compiling source text during predicate/action invocation;
- automatic activation from `ParserEngine` remains out of scope.

### Source generator C# path

- initial explicit C# embedded-code hook support is implemented for parser semantic predicates and inline parser actions;
- generated hooks are private C# methods compiled by Roslyn with the consuming project;
- generated dispatchers implement `ISemanticPredicateEvaluator` and `IParserActionExecutor`, are bound to one caller-supplied generated execution-context instance, and are installed through `CreateRuntimePolicy(executionContext, basePolicy)`; reusing that policy reuses the same context state;
- generated `ParseWithEmbeddedCode(string)` opts into those hooks with a fresh execution context for that parse, `ParseWithEmbeddedCode(string, context)` intentionally reuses the supplied context, and generated `Parse(...)` keeps default conservative runtime behavior;
- supported predicate bodies include C# boolean expressions and block-bodied predicate statements with `return`, using `context`, `ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`, and `predicateCode`;
- supported action bodies include single-statement, multi-statement, and multi-line C# statement bodies using `context`, `ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`, and `actionCode`, including local variables and calls to user members in another partial class declaration;
- invalid embedded C# is intentionally reported by Roslyn as a compilation error; predicate blocks without a valid `bool` return and actions with invalid C# are not converted into custom parser diagnostics;
- unscoped `@members` and `@parser::members` are injected into the generated execution context and produce generator warning `UP1031`;
- visible unsupported embedded-code constructs produce generator warning `UP1029` without changing behavior: `@lexer::members`, lexer actions/predicates, and other unsupported grammar actions are not executed; parser semantic predicates, inline parser actions, and parser `@init` / `@after` hooks are generated as executable opt-in hooks;
- future work may add broader C# shape support without changing default parsing.

### Rule-call argument syntax `callee[...]`

- `callee[...]` argument clauses are **metadata-only**: parsed and preserved as `RuleRef.RawArguments` (raw text, outer brackets excluded);
- reported with `UP1037 RuleCallArgumentsPreservedAsMetadata`;
- at runtime, the raw argument text is also carried into `ParserRuleCallResult.RawArguments` on the parent frame's last completed child call result (via `StackParserRuleInvocationFrameManager.AnnotateLastChildCallRawArguments`, called by `ParserEngine.TryParseRuleRef` after each successful child rule parse, whether fresh or memoized);
- generated C# opt-in code can inspect it explicitly via `GetLastRuleCallResult(context)?.RawArguments` or `TryGetLastRuleCallRawArguments(context, ruleName, out rawArgs)` in parent lifecycle and inline-action hooks;
- `SetNextRuleParameterFromRawArguments(context, ruleName, parameterName, rawArguments, map)` allows explicit user-controlled mapping of raw text into a future child seed via a caller-supplied delegate; requires an explicit mapper; null `rawArguments` returns `false`; mapper exceptions propagate; seeds the **next** invocation of the named rule;
- `SplitRawArgumentsTopLevel(rawArguments)` and `TrySplitLastRuleCallRawArguments(context, ruleName, out args)` split raw argument text into top-level slices at commas while respecting nested `()`, `[]`, `{}`, and quoted strings; syntactic only — no argument is evaluated, no parameter is bound, no seed is set automatically; backed by `Utils.Parser.Runtime.ParserRawArgumentSplitter.SplitTopLevel`;
- `SetNextRuleParametersFromRawArguments(context, ruleName, rawArgs, params mappings)` maps multiple positional slices to named child seeds in a single call using `ParserRawArgumentParameterMapping` entries (ParameterName, Index, Map); validates all mappings before applying any seed (out-of-range index returns false with no partial seeding); duplicate parameter names: last mapping wins; mapper exceptions propagate; null mapped values allowed; both lifecycle and inline-action overloads available;
- `ParserRawNamedArgumentSplitter.SplitNamedTopLevel` parses named key–value forms (`value: 42`, `value = 42`) from top-level slices; throws `FormatException` on missing separator or empty key; duplicate keys: last wins; `TrySplitLastRuleCallNamedRawArguments` wraps this in a Try… helper; `SetNextRuleParametersFromNamedRawArguments(context, ruleName, named, params mappings)` maps named entries to seeds using `ParserRawNamedArgumentParameterMapping` (ParameterName, ArgumentName, Map); validates all mappings before seeding (missing ArgumentName returns false, no partial seeding); lifecycle and inline-action overloads available; syntactic only — no evaluation;
- call-site metadata is rollback-safe (execution-state snapshots include `_lastChildCallResult.RawArguments`) and memoization-safe (annotation always reflects the current call site, not the cached snapshot);
- raw argument text is not evaluated, not parsed as C# expressions, and not bound to child rule parameters;
- `PendingChildSeeds`, `InvocationFrame.Parameters`, and frame behavior are unchanged;
- generated `Parse(...)` and generated rule method signatures are unchanged;
- use `SetNextRuleParameter(...)` for explicit parameter seeding from lifecycle hook code;
- `$param` is not supported.

### Rule-reference label metadata (`x=child`, `xs+=child`)

Rule-reference labels are preserved as passive metadata end-to-end:

- `x=child` and `xs+=child` are parsed by both the ANTLR converter and the source-generator G4 parser;
- label metadata is stored on `RuleRef.Label` (`RuleLabel` record: label name, rule name, additive flag), with `RuleRef.LabelName` and `RuleRef.LabelKind` (`ParserRuleReferenceLabelKind`: `None`, `Assignment`, `List`) as computed properties;
- `GrammarEmitter` emits `Label: new RuleLabel(...)` in generated `BuildDefinition()` when a label is present;
- at runtime, `ParserEngine.TryParseRuleRef` calls `IParserRuleInvocationFrameManager.AnnotateLastChildCallLabel(labelName, labelKind)` after each successful child rule completion so the call-site label is visible in `ParserRuleCallResult.LabelName` and `ParserRuleCallResult.LabelKind` on the parent frame;
- labels compose with `callee[...]` raw arguments: both metadata fields are set independently and can coexist;
- label metadata is rollback-safe (included in `ParserRuleCallResult.GetParserExecutionStateHash()`, tracked alongside raw arguments in execution-state snapshots) and memoization-safe (annotation always reflects the current call site, not the cached snapshot);
- generated C# opt-in code can inspect label metadata explicitly via `GetLastRuleCallResult(context)?.LabelName` and `GetLastRuleCallResult(context)?.LabelKind` in parent lifecycle and inline-action hooks;
- labels on non-rule-reference elements (literals, character classes, groups) are recognized and ignored with diagnostic `UP1022 LabelOnNonRuleReferenceIgnored`;
- labels are metadata-only: no `$x`, `$x.value`, `$xs`, implicit label variables, typed label fields/properties, automatic parse-node storage, automatic return access, automatic binding, automatic argument evaluation, automatic parameter seeding, or generated parser method signatures are added;
- conservative `Parse(...)` remains conservative; lifecycle hooks do not execute and label metadata is not exposed to code.

### Future PR — Lexer actions/predicates

- separate design and implementation;
- account for tokenization and lexer state impact.


Runtime policy architecture rule:

- Runtime policy contexts are immutable input snapshots.
- Runtime policy outcomes carry the decision and optional diagnostic metadata.
- Future outcomes may carry deterministic context transitions, but handlers must not mutate parser state directly.
- ParserEngine remains responsible for applying effects and emitting diagnostics.

Parser action execution now uses structured `ParserActionExecutionOutcome`, aligned with semantic predicate outcomes. Executors return a status plus optional diagnostic metadata. Inline parser actions still do not influence parse acceptance: `Executed` and `NotExecuted` both continue parsing.
### Shared runtime indexing metadata

Parser embedded-code discovery now has a shared metadata model in `Utils.Parser.EmbeddedCode`. `EmbeddedCodeRuntimeDiscovery` walks a `ParserDefinition` and emits `EmbeddedCodeRuntimeEntry` values with the raw source, `EmbeddedCodeKind`, owning rule name, runtime-compatible alternative and element indexes, a runtime key for executable entries, and an explicit `EmbeddedCodeUnsupportedReason` for skipped entries. The metadata mirrors the existing parser runtime indexing rules for priority-ordered alternatives, single-item alternatives, sequences, quantifier inner parsing, negation probes, and direct-left-recursive base/tail alternatives. It is metadata only: it does not compile source, generate C#, execute actions, or change `ParserEngine` behavior.

The expression-backed prepared registry consumes this shared discovery result before invoking its preparer. Unsupported constructs such as grammar actions, lexer actions/predicates, lexer lifecycle/member actions, and non-inline parser actions remain non-executable, but they now carry explicit skip reasons. Parser `@init` and `@after` hooks are supported only by the source-generator C# opt-in path and remain outside runtime-inline expression preparation. The source-generator path reports `UP1029` when these constructs are visible in its grammar model; this warning is metadata/conservative-only and does not add execution. Invalid C# in a source-generator-supported hook remains a Roslyn compilation error rather than a custom parser diagnostic.


## Explicit parser rule-call execution policy

`ParserRuntimeFeaturePolicy.RuleCallExecutionPolicy` is an explicit opt-in extension point around parser rule references. `ParserEngine` creates a passive `ParserRuleCallExecutionContext`, calls `BeforeRuleCall(...)`, invokes the child `ParseRule(...)`, annotates a successful `ParserRuleCallResult` with the current call site's raw arguments and label, and then calls `AfterRuleCall(...)`. The after callback reports `Succeeded` and exposes the annotated `CompletedCallResult` when stack-aware invocation-frame tracking is active. Context metadata also includes the target rule name and descriptor, caller frame when available, raw argument text, label name/kind, positional top-level slices, and named top-level slices when syntactically valid.

The default `NullParserRuleCallExecutionPolicy` performs no work, so default parsing behavior is unchanged. This policy does not make `callee[...]` executable: there is no automatic expression evaluation, positional or named binding, parameter seed, generated parser signature, typed parameter/return variable, `$param`, `$x`, `$x.value`, or `$rule.value`. Generated C# opt-in code preserves a custom rule-call policy supplied through the `basePolicy` passed to `CreateRuntimePolicy(...)`; the generated three-argument `ParseWithEmbeddedCode(input, executionContext, basePolicy)` overload provides the same explicit path while existing overloads remain unchanged. Generated `Parse(...)` remains conservative.

Policy method calls are ordinary external callbacks. Their external side effects are not buffered, replayed, or automatically rolled back. Only mutations performed through separately documented rollback-aware parser state participate in parser rollback. Call-site raw arguments and labels remain rollback- and memoization-safe because successful child results are annotated after every child call, including memoization hits, before `AfterRuleCall(...)` observes them. Policy implementations must not retain mutable invocation frames beyond the callback.

## Concrete positional literal rule-call policy

`PositionalLiteralRuleCallExecutionPolicy` provides a narrow, caller-installed bridge from parser call-site metadata to pending child parameters. `BeforeRuleCall(...)` reads the target descriptor and syntactically split positional arguments, requires exact arity and usable unique parameter names, parses every supported literal into temporary storage, and only then calls `ParserRuleCallExecutionContext.TrySetParameterSeeds(...)` once with the complete binding set. The context API fixes the target to the current `RuleName` and delegates to the frame manager's all-or-none batch contract; it does not expose the frame manager or directly mutate a child frame. The stack manager applies the batch through one immutable pending-seed-store replacement. The method returns `false` when the configured invocation-frame manager cannot retain the complete batch (including the conservative no-op manager), so ignore mode leaves the call unbound and throw mode reports a binding failure instead of claiming success. Policy seeds overwrite same-parameter pending seeds while unrelated seeds remain intact. `AfterRuleCall(...)` is intentionally a no-op.

Supported values are limited to `null`, lowercase Booleans, signed decimal `int`/`long`, finite invariant decimal/exponent `double`, double-quoted strings, and single-quoted characters with `\\`, `\"`, `\'`, `\n`, `\r`, `\t`, and `\0`. No declared-type validation, named binding, expression execution, reflection-based identifier resolution, or Roslyn compilation occurs. The default policy remains no-op and generated `Parse(...)` remains conservative. Generated callers opt in by passing a `basePolicy` containing this policy to `ParseWithEmbeddedCode(...)`.

## Named literal rule-call policy

`NamedLiteralRuleCallExecutionPolicy` complements, but does not compose automatically with, the positional policy. It is explicitly supplied through `basePolicy`; defaults and generated `Parse(...)` remain metadata-only. It reads only `ParserRuleCallExecutionContext.NamedRawArguments`, whose existing splitter accepts top-level `name: literal` and `name = literal`, ignores separators inside nesting or quotes, and resolves duplicate raw names with last-wins semantics. Exact ordinal parameter-name coverage is required, while call-site order may differ from declaration order. Missing, extra, case-mismatched, blank, or duplicate declared names, mixed syntax, optional/default parameters, and partial binding are rejected conservatively.

Each raw value is parsed solely by `ParserSimpleLiteralParser`; declared C# types are passive metadata and do not drive validation or conversion. Only the documented simple literals are supported, not arbitrary C# expressions. All validation precedes one atomic pending-child seed batch, so rollback and state-aware memoization use the same managed guarantees as positional binding. The policy does not bind returns or labels and does not add `$param`, `$x`, `$x.value`, `$rule.value`, or lexer execution.

## Typed simple-literal call policies

Typed rule-call binding is a separate explicit execution strategy, not a change to grammar parsing or the conservative default. `TypedPositionalLiteralRuleCallExecutionPolicy` and `TypedNamedLiteralRuleCallExecutionPolicy` consume only values accepted by `ParserSimpleLiteralParser`, inspect the descriptor's conservatively preserved `RawType`, and delegate conversion to `ParserLiteralTypeConverter`. Existing positional and named policies remain untyped. Parameter descriptors also preserve a conservatively split `RawDefaultValue` as passive metadata. Only typed policies consume it: positional calls may omit trailing parameters, while named calls may omit any parameter whose declaration supplies a usable default. Explicit values win and prevent unused invalid defaults from being evaluated.

Every explicit value and every required default is parsed and converted before exactly one `TrySetParameterSeeds(...)` call. The resulting complete effective state follows the existing managed rollback and memoization paths. No general C# default-expression execution, parameter reference, constant/enum resolution, return/local/label binding, `$param` support, or lexer execution is introduced; generated `Parse(...)` remains conservative.

The converter recognizes only the exact aliases `bool`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`, `char`, `string`, `object` and their canonical `System.*` equivalents. One nullable suffix is supported; `string?` and `object?` do not enforce nullable-reference annotations. It performs checked integral conversion, exact-preserving integral-to-floating and double-to-float conversion, exact integral-to-decimal conversion, and the limited `char`/`string` conversions. It never parses strings into numbers or Booleans and never converts floating-point values to integral values.

The execution order is deliberately transactional: validate call syntax and exact coverage, validate every descriptor name and supported type, parse every literal, convert every value, build the complete dictionary, then invoke the managed batch writer once. Unsupported types or values produce no mutation in `IgnoreCall` mode or a deterministic `ParserRuleCallBindingException` in `Throw` mode. There is no arbitrary type resolution, assembly loading, Roslyn conversion, expression evaluation, generated typed variable/signature support, `$param`/`$x`/`$x.value`/`$rule.value`, return or label binding, or lexer support.

## Explicit labeled child-call result access

A successful child invocation is finalized in this order: child execution, child `@after`, immutable return capture, current-site raw-argument annotation, current-site label annotation, parent `LastCompletedChildCall` update, parent labeled-store binding, then `AfterRuleCall(...)`. Assignment labels overwrite only after success; list labels append only after success. The immutable parent-frame store is core managed call behavior, not another argument policy, while access remains explicit through the generated generic helpers.

Lifecycle and inline-action helper overloads support assignment result lookup, ordered list result lookup, assignment return lookup, and ordered list return projection. Return names use ordinal matching with no conversion. A present-null return is included and reported present; an absent return is not. List return projection skips absent keys and preserves the order of calls containing the key. There is no fallback to `LastCompletedChildCall`, rule-name lookup, generated label-specific member, ANTLR attribute rewriting, automatic return propagation, or lexer equivalent. Conservative `Parse(...)` does not opt into embedded-code access.
