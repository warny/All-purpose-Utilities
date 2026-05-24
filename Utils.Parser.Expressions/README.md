# omy.Utils.Parser.Expressions

`omy.Utils.Parser.Expressions` provides an optional runtime adapter that connects `IExpressionCompiler` to `ISemanticPredicateEvaluator`.

## Purpose

Use this package when you want to explicitly evaluate ANTLR semantic predicates (`{ condition }?`) at runtime with a configured expression compiler.

- Default parser behavior remains unchanged.
- No compiler is selected automatically.
- Inline parser actions are not executed by this package.

## Usage

```csharp
var evaluator = new ExpressionSemanticPredicateEvaluator(expressionCompiler);
var policy = ParserRuntimeFeaturePolicy.Default with
{
    SemanticPredicateEvaluator = evaluator
};
var parser = new ParserEngine(definition, policy);
```

## Scope and limitations

- Supports semantic predicates only.
- Expects expression compilation to produce boolean-compatible expressions.
- Predicates that do not reference contextual symbols are cached by predicate source code.
- Predicates referencing `ruleName`, `inputPosition`, `alternativeIndex`, or `elementIndex` are recompiled per evaluation to avoid capturing runtime context.
- Does not evaluate lexer predicates or lexer actions.
