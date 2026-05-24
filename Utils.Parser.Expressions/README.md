# omy.Utils.Parser.Expressions

`omy.Utils.Parser.Expressions` provides optional runtime adapters that connect `IExpressionCompiler` to parser runtime policy interfaces.

## Purpose

Use this package when you want to explicitly evaluate embedded parser code at runtime with a configured expression compiler.

Supported optional adapters:

- `ExpressionSemanticPredicateEvaluator` (`IExpressionCompiler` → `ISemanticPredicateEvaluator`) for semantic predicates (`{ condition }?`).
- `ExpressionParserActionExecutor` (`IExpressionCompiler` → `IParserActionExecutor`) for inline parser actions (`{ code }`).

- Default parser behavior remains unchanged.
- No compiler is selected automatically.
- Lexer actions, lexer predicates, and grammar members are not executed by this package.

## Usage

```csharp
var policy = ParserRuntimeFeaturePolicy.Default with
{
    SemanticPredicateEvaluator = new ExpressionSemanticPredicateEvaluator(expressionCompiler),
    ParserActionExecutor = new ExpressionParserActionExecutor(expressionCompiler)
};

var parser = new ParserEngine(definition, policy);
```

## Scope and limitations

### Semantic predicates

- Expects expression compilation to produce boolean-compatible expressions.
- Predicates that do not reference contextual symbols are cached by predicate source code.
- Predicates referencing `ruleName`, `inputPosition`, `alternativeIndex`, or `elementIndex` are recompiled per evaluation to avoid capturing runtime context.
- Compilation failures and non-boolean expression results are surfaced as structured `NotEvaluated` outcomes with `UP1026` metadata.

### Inline parser actions

- Scope is parser inline actions only (no lexer actions/predicates, no `@members`).
- Non-void expressions are evaluated and their result is discarded.
- Cache scope is compilation-only for non-contextual actions; contextual actions (`ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`) are recompiled per execution to avoid first-context capture.
- Compilation failures, incompatible executable expressions, and runtime execution exceptions return `NotExecuted` with `UP1026` metadata.

`ParserEngine` remains the diagnostic owner and emits diagnostics when a `DiagnosticBag` is provided.
