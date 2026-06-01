# omy.Utils.Parser.Expressions

`omy.Utils.Parser.Expressions` provides optional runtime adapters that connect `IExpressionCompiler` to parser runtime policy interfaces. These adapters are the current integration surface, not the final embedded-code preparation architecture.

## Purpose

Use this package when you want to explicitly evaluate embedded parser code at runtime with a configured expression compiler.

Supported optional adapters:

- `ExpressionSemanticPredicateEvaluator` (`IExpressionCompiler` → `ISemanticPredicateEvaluator`) for semantic predicates (`{ condition }?`).
- `ExpressionParserActionExecutor` (`IExpressionCompiler` → `IParserActionExecutor`) for inline parser actions (`{ code }`).

- Default parser behavior remains unchanged.
- No compiler is selected automatically.
- Lexer actions, lexer predicates, and grammar members are not executed by this package.
- Current adapters compile opportunistically when evaluating/executing embedded code, with limited compilation caching.
- The intended target is preparation before parsing: compile or generate an executable artifact during parser model preparation/generation, then execute that prepared artifact during parsing.

## Usage

```csharp
var policy = ParserRuntimeFeaturePolicy.Default with
{
    SemanticPredicateEvaluator = new ExpressionSemanticPredicateEvaluator(expressionCompiler),
    ParserActionExecutor = new ExpressionParserActionExecutor(expressionCompiler)
};

var parser = new ParserEngine(definition, policy);
```

## Current adapter behavior vs target model

Current behavior:

- `ExpressionSemanticPredicateEvaluator` adapts `IExpressionCompiler` to `ISemanticPredicateEvaluator`.
- `ExpressionParserActionExecutor` adapts `IExpressionCompiler` to `IParserActionExecutor`.
- The adapters compile from source text at predicate/action invocation time when needed.
- Non-contextual expressions can reuse an opportunistically cached compiled delegate.
- Expressions that reference contextual symbols (`ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`) are currently recompiled per invocation to avoid capturing the wrong runtime context.

This behavior is intentionally preserved for now. It should not be described as the final target model. The target model is to compile or prepare expression-backed executable artifacts before parsing, during parser model generation/preparation, and then execute only the prepared artifact while parsing.

`ParserEngine` should remain language-neutral. It should execute policy outcomes and emit diagnostics, not select an expression language or compile embedded source code.

## Scope and limitations

### Semantic predicates

- Expects expression compilation to produce boolean-compatible expressions.
- Predicates that do not reference contextual symbols are cached by predicate source code in the current adapter.
- Predicates referencing `ruleName`, `inputPosition`, `alternativeIndex`, or `elementIndex` are currently recompiled per evaluation to avoid capturing runtime context.
- Compilation failures and delegate-shape adaptation failures are surfaced as structured `NotEvaluated` outcomes with `UP1026` metadata.

### Inline parser actions

- Scope is parser inline actions only (no lexer actions/predicates, no `@members`).
- Non-void expressions are evaluated and their result is discarded.
- Cache scope is compilation-only for non-contextual actions in the current adapter; contextual actions (`ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`) are currently recompiled per execution to avoid first-context capture.
- Compilation failures, delegate-shape adaptation failures, and runtime exceptions during compiled action execution return `NotExecuted` with `UP1026` metadata.

`ParserEngine` remains the diagnostic owner and emits diagnostics when a `DiagnosticBag` is provided.


`UP1028` is reserved for explicit runtime policies that intentionally disable embedded-code execution. The current expression-backed adapters do not use `UP1028` because they require an explicit compiler and do not expose an `Enabled = false` policy switch.
