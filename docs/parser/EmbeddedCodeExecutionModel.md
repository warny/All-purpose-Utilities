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

Current behavior is intentionally conservative.

- Semantic predicates are recognized and routed through `ISemanticPredicateEvaluator`.
- Inline parser actions are recognized and routed through `IParserActionExecutor`.
- `@init` and `@after` are recognized and stored on the rule model.
- Grammar actions are preserved as metadata only.
- No raw embedded ANTLR target-language code is executed automatically.

Important distinction:

- **source-generated model construction** is already implemented;
- **source-generated executable embedded code** is not implemented.

Current generator output preserves embedded code as model metadata strings (for example `ValidatingPredicate("...")` and `EmbeddedAction("...", ...)`) and does not compile those strings to executable delegates.

## 4. Two-path target architecture

The target architecture uses **two implementation paths** over **one shared parser model**:

1. Source-generation path (compile-time `.g4` ingestion, C# code generation).
2. Runtime-ingestion path (runtime `.g4` ingestion with explicit expression compilation adapters).

Shared expectations across both paths:

- same ANTLR construct classification;
- same model concepts;
- same deterministic diagnostic vocabulary;
- same runtime authority boundaries.

Adapters may differ by path, but model semantics must stay aligned.

## 5. Source generator C# path

Pipeline:

- `.g4` consumed as `AdditionalFiles` by `Utils.Parser.Generators`;
- grammar parsed by internal G4 tokenizer/parser;
- C# emitted by `GrammarEmitter`;
- final compilation performed by Roslyn.

Boundary rules:

- executable target-language support in this path is **C#-only** initially;
- language choice must be explicit or safely defaulted;
- non-C# embedded target code must be diagnosed or preserved as non-executable metadata;
- generator diagnostics must be Roslyn-visible;
- no implicit runtime interpretation of C# source;
- no silent execution capability.

Natural ownership: build-time C# embedded-code execution support belongs to `Utils.Parser.Generators`, not `ParserEngine`.

## 6. Runtime expression compiler path

Pipeline:

- `.g4` parsed at runtime;
- embedded ANTLR code preserved as raw text;
- explicit embedded-code compiler configured by policy;
- compiler delegates to `IExpressionCompiler`;
- expression/delegate executed through runtime policy interfaces.

Conceptual mapping:

- semantic predicates map to `ISemanticPredicateEvaluator`;
- parser inline actions map to `IParserActionExecutor`.

Strict rules:

- no raw target-language execution;
- no implicit language selection;
- `IExpressionCompiler` must be explicitly injected;
- `Utils.Parser` core must not reference `Utils.Expressions.CSyntax` or `Utils.Expressions.VBSyntax` directly.


Prototype status update:

- A first optional runtime adapter now exists to map `IExpressionCompiler` to `ISemanticPredicateEvaluator` for semantic predicates (`{ condition }?`).
- Default parser runtime behavior is unchanged (`NotEvaluated` with `UP1006` when applicable). Expression-backed semantic predicate evaluation now returns a structured outcome so compilation failures, delegate-shape adaptation failures, and runtime exceptions during compiled predicate execution can carry `UP1026` metadata, while `ParserEngine` remains the only component that emits diagnostics.
- Current adapter scope is limited to parser semantic predicates only.
- A first optional runtime parser action adapter now exists: `IExpressionCompiler` can be adapted to `IParserActionExecutor` for inline parser actions only.
- Default parser runtime behavior is unchanged; inline actions still do not control parse acceptance, parse-tree shape, or branch rejection.
- `Executed` and `NotExecuted` outcomes both continue parsing; no context mutation, no `ContextDelta`, and no lexer action/predicate or grammar-members execution support are introduced.
- The symbol model is intentionally minimal and read-only (`ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`).
- Predicate adapter cache: compilation-only and not parse-result memoization. Predicates that do not reference contextual symbols can be cached by predicate source; predicates referencing `ruleName`, `inputPosition`, `alternativeIndex`, or `elementIndex` are recompiled per evaluation to avoid context capture.
- Action adapter cache: compilation-only and not parse-result memoization. Non-contextual actions can be cached by action source; actions referencing `ruleName`, `inputPosition`, `alternativeIndex`, or `elementIndex` are recompiled per execution to avoid context capture.

## 7. Interface boundary

A future conceptual compiler boundary should be explicit and separated from runtime execution interfaces.

Example conceptual interface:

```csharp
public interface IEmbeddedCodeCompiler
{
    CompiledSemanticPredicate CompileSemanticPredicate(
        EmbeddedCodeSource source,
        EmbeddedCodeCompilationContext context);

    CompiledParserAction CompileParserAction(
        EmbeddedCodeSource source,
        EmbeddedCodeCompilationContext context);
}
```

Separation of concerns:

- compilation/preparation boundary: `IEmbeddedCodeCompiler` (future);
- runtime evaluation boundary: `ISemanticPredicateEvaluator`;
- runtime execution boundary: `IParserActionExecutor`.

## 8. Cache boundary

Allowed cache scope for future embedded-code compilation:

- key: raw embedded code + compilation context;
- value: compiled expression/delegate artifact.

Example conceptual key fields:

- source text;
- construct kind;
- expected result type;
- compiler identity/language;
- symbol model version.

Non-goal boundary:

- this compilation cache is **not** parse-result memoization;
- no `(input position + rule) -> parse result` semantic memoization changes.

## 9. Predicate vs action mapping

### Semantic predicate `{ condition }?`

Conceptual outcomes:

- compiled boolean expression;
- `true` -> `SemanticPredicateEvaluationResult.Satisfied`;
- `false` -> `SemanticPredicateEvaluationResult.Rejected`;
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
- `UP1026 EmbeddedCodeCompilationFailed`: currently used by expression-backed adapters for compile, delegate adaptation, and compiled-execution failures.
- `UP1027 EmbeddedCodePreservedNotCompiled`
- `UP1028 EmbeddedCodeExecutionDisabled`: reserved for explicit runtime policies that intentionally disable embedded-code execution. Current expression-backed adapters do not expose an `Enabled = false` policy and therefore do not emit this diagnostic.

These diagnostics define capability boundaries. They do not imply that every diagnostic is emitted by current adapters.

### `Utils.Parser.Generators`

Current responsibilities:

- Roslyn source generation (`netstandard2.0` analyzer/generator project);
- compile-time `.g4` ingestion via `AdditionalFiles`;
- internal G4 parsing and C# emission;
- preservation of embedded code as raw model metadata strings;
- Roslyn diagnostic reporting.

Future responsibilities (source-generation path):

- C#-only executable embedded-code hooks (explicit);
- language compatibility diagnostics;
- clear distinction between preserved raw metadata and executable generated hooks.

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

Future PRs should define shared diagnostics in `Utils.Parser.Diagnostics` so they can be used by:

- runtime ingestion;
- source generator Roslyn reporting;
- Visual Studio/tooling display.

Candidate shared diagnostics include:

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
- implementation changes in this PR.

## 13. Future PR plan

### PR 1 — Documentation/design (current PR)

- document model, boundaries, and project responsibilities;
- no behavior change.

### PR 2 — Diagnostics taxonomy

- add shared embedded-code diagnostics;
- no execution behavior added.

### PR 3 — Runtime predicate adapter prototype

- map `IExpressionCompiler` into `ISemanticPredicateEvaluator` for `{ condition }?`;
- outcomes:
  - `true` -> `Satisfied`
  - `false` -> `Rejected`
  - failed/unsupported -> `NotEvaluated` + diagnostic.

### PR 4 — Runtime parser inline action adapter

- map `IExpressionCompiler` into `IParserActionExecutor` for `{ code }`;
- define allowed side-effects, mutable context boundaries, idempotence expectations, and backtracking caveats.

### PR 5 — Source generator C# path

- add explicit C# embedded-code hook support in generator path;
- define language option handling;
- support C# only initially;
- report Roslyn diagnostics for unsupported/invalid embedded code;
- document generated hook shape.

### PR 6 — Lexer actions/predicates

- separate design and implementation;
- account for tokenization and lexer state impact.


Runtime policy architecture rule:

- Runtime policy contexts are immutable input snapshots.
- Runtime policy outcomes carry the decision and optional diagnostic metadata.
- Future outcomes may carry deterministic context transitions, but handlers must not mutate parser state directly.
- ParserEngine remains responsible for applying effects and emitting diagnostics.

Parser action execution now uses structured `ParserActionExecutionOutcome`, aligned with semantic predicate outcomes. Executors return a status plus optional diagnostic metadata. Inline parser actions still do not influence parse acceptance: `Executed` and `NotExecuted` both continue parsing.
