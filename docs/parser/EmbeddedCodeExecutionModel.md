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
- `@init` and `@after` are recognized and stored on the rule model, but are not executed.
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
- generated C# hooks are emitted for supported parser semantic predicates and inline parser actions;
- generated dispatchers implement `ISemanticPredicateEvaluator` and `IParserActionExecutor`;
- generated `CreateRuntimePolicy(...)` and `ParseWithEmbeddedCode(...)` helpers provide the explicit opt-in path;
- generated `Parse(...)` remains conservative and does not install generated embedded-code hooks.

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

`@header`, `@members`, `@parser::members`, `@lexer::members` remain metadata-only by default.

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
- runtime-index-aware hook dispatch for tested parser hook positions: single-item alternatives, sequence positions, quantified content, negation predicate probes, same-source hooks in distinct alternatives, and direct-left-recursive tail views because generated helpers resolve the generated definition before parsing with the generated policy;
- Roslyn diagnostic reporting and C# compilation errors for invalid embedded C# in the source-generator path.

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

No new diagnostics are introduced in this documentation PR.

Future implementation PRs should define shared diagnostics in `Utils.Parser.Diagnostics` so they can be used by:

- runtime ingestion;
- source generator Roslyn reporting;
- Visual Studio/tooling display.

Candidate future diagnostic improvements include:

- generator diagnostics for visible unsupported embedded-code constructs;
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
- rollback/replay semantics;
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
- generated dispatchers implement `ISemanticPredicateEvaluator` and `IParserActionExecutor` and are installed through `CreateRuntimePolicy(...)`;
- generated `ParseWithEmbeddedCode(...)` opts into those hooks, while generated `Parse(...)` keeps default conservative runtime behavior;
- supported predicate bodies include C# boolean expressions and block-bodied predicate statements with `return`, using `context`, `ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`, and `predicateCode`;
- supported action bodies include single-statement, multi-statement, and multi-line C# statement bodies using `context`, `ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`, and `actionCode`, including local variables and calls to user members in another partial class declaration;
- invalid embedded C# is intentionally reported by Roslyn as a compilation error; predicate blocks without a valid `bool` return and actions with invalid C# are not converted into custom parser diagnostics;
- future work may add generator diagnostics for unsupported constructs, controlled `@members` handling, and broader C# shape support without changing default parsing.

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

The expression-backed prepared registry consumes this shared discovery result before invoking its preparer. Unsupported constructs such as grammar actions, `@init`, `@after`, lexer actions/predicates, and non-inline parser actions remain non-executable, but they now carry explicit skip reasons. Invalid C# in a source-generator-supported hook remains a Roslyn compilation error rather than a custom parser diagnostic.

