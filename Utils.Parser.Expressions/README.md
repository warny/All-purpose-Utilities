# omy.Utils.Parser.Expressions

`omy.Utils.Parser.Expressions` provides optional expression-backed integration for `omy.Utils.Parser`.
It connects an explicit `IExpressionCompiler` to parser embedded-code surfaces without selecting a language automatically.

## Purpose

Use this package when you want to work with embedded parser code through a caller-selected expression compiler.

Available surfaces:

- `ExpressionEmbeddedCodePreparer` prepares expression-backed artifacts for semantic predicates (`{ condition }?`) and inline parser actions (`{ code }`).
- `PreparedExpressionSemanticPredicate` stores a compiled predicate delegate and can produce `SemanticPredicateEvaluationOutcome` values without recompiling source text.
- `PreparedExpressionParserAction` stores a compiled action delegate and can produce `ParserActionExecutionOutcome` values without recompiling source text.
- `ExpressionSemanticPredicateEvaluator` remains the current runtime adapter from `IExpressionCompiler` to `ISemanticPredicateEvaluator`.
- `ExpressionParserActionExecutor` remains the current runtime adapter from `IExpressionCompiler` to `IParserActionExecutor`.

## Preparing artifacts

```csharp
IExpressionCompiler compiler = GetExpressionCompiler();
var preparer = new ExpressionEmbeddedCodePreparer(compiler);

var source = new EmbeddedCodeSource(
    "inputPosition > 0",
    EmbeddedCodeKind.SemanticPredicate,
    ruleName: "value");

var context = new EmbeddedCodePreparationContext(
    "ExampleGrammar",
    EmbeddedCodeTarget.RuntimeInlineExpression,
    ruleName: "value",
    languageOrCompilerIdentity: "custom-expression-language");

var result = preparer.PrepareSemanticPredicate(source, context);
```

The preparer:

- supports `EmbeddedCodeKind.SemanticPredicate` and `EmbeddedCodeKind.ParserInlineAction`;
- uses only the supplied `IExpressionCompiler` contract;
- does not reference `Utils.Expressions.CSyntax` or `Utils.Expressions.VBSyntax` directly;
- returns `Unsupported` for `RuleInitAction`, `RuleAfterAction`, and `GrammarAction`;
- returns `PreservedNotCompiled` for `EmbeddedCodeTarget.SourceGeneratorCSharp`, because C# source-generation hooks belong to `Utils.Parser.Generators`;
- never executes predicates or actions during preparation.

Contextual symbols (`ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`) are represented as reads from the runtime context parameter when compiling prepared artifacts. They are not captured as fixed preparation-time constants.

## Runtime status

The prepared artifacts are not wired automatically into `ParserEngine` or `ParserRuntimeFeaturePolicy` yet.
Default parser behavior is unchanged.

The existing adapters remain available and remain the current runtime/intermediate path:

```csharp
var policy = ParserRuntimeFeaturePolicy.Default with
{
    SemanticPredicateEvaluator = new ExpressionSemanticPredicateEvaluator(expressionCompiler),
    ParserActionExecutor = new ExpressionParserActionExecutor(expressionCompiler)
};

var parser = new ParserEngine(definition, policy);
```

## Current adapter behavior vs target model

Current adapter behavior:

- `ExpressionSemanticPredicateEvaluator` adapts `IExpressionCompiler` to `ISemanticPredicateEvaluator`.
- `ExpressionParserActionExecutor` adapts `IExpressionCompiler` to `IParserActionExecutor`.
- The adapters compile from source text at predicate/action invocation time when needed.
- Non-contextual expressions can reuse an opportunistically cached compiled delegate.
- Expressions that reference contextual symbols (`ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`) are currently recompiled per invocation to avoid capturing the wrong runtime context.

Prepared artifact behavior:

- `ExpressionEmbeddedCodePreparer` compiles before parsing when invoked explicitly by a caller.
- `PreparedExpressionSemanticPredicate` and `PreparedExpressionParserAction` execute already-compiled delegates.
- The preparer does not change parser scheduling, memoization, diagnostics emission, parse-tree shape, or default runtime policy.

The target model remains to prepare executable artifacts before parsing and then execute only those artifacts while parsing. `ParserEngine` should remain language-neutral: it should execute policy outcomes and emit diagnostics, not select an expression language or compile embedded source code.

## Scope and limitations

### Semantic predicates

- Predicate expressions must compile to `bool`.
- Non-boolean predicate expressions produce a `CompilationFailed` preparation result with `UP1026` metadata.
- Preparation compilation failures preserve the exception in the preparation result.

### Inline parser actions

- `void` expressions are accepted.
- Non-void expressions are accepted when executable; their result is ignored, matching the existing adapter behavior.
- Preparation does not execute the action.
- Runtime exceptions from a prepared action are converted to `ParserActionExecutionOutcome.NotExecuted` with `UP1026` metadata when the artifact is executed explicitly.

Lexer actions, lexer predicates, grammar members, `@init`, and `@after` execution are not implemented by this package.
