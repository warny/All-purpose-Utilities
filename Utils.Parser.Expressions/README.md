# omy.Utils.Parser.Expressions

`omy.Utils.Parser.Expressions` provides optional expression-backed integration for `omy.Utils.Parser`.
It connects an explicit `IExpressionCompiler` to parser embedded-code surfaces without selecting a language automatically.

## Purpose

Use this package when you want to work with embedded parser code through a caller-selected expression compiler.

Available surfaces:

- `ExpressionEmbeddedCodePreparer` prepares expression-backed artifacts for semantic predicates (`{ condition }?`) and inline parser actions (`{ code }`).
- `PreparedExpressionSemanticPredicate` stores a compiled predicate delegate and can produce `SemanticPredicateEvaluationOutcome` values without recompiling source text.
- `PreparedExpressionParserAction` stores a compiled action delegate and can produce `ParserActionExecutionOutcome` values without recompiling source text.
- `PreparedExpressionEmbeddedCodeKey` identifies prepared artifacts by embedded-code kind, owning rule, source text, alternative index, and element index.
- `PreparedExpressionEmbeddedCodeRegistry` stores prepared semantic predicates separately from prepared parser inline actions.
- `PreparedExpressionEmbeddedCodeRegistryBuilder` explicitly scans `ParserDefinition` models and fills a registry from validating predicates and inline parser actions.
- `PreparedExpressionSemanticPredicateEvaluator` executes registered `PreparedExpressionSemanticPredicate` artifacts through `ISemanticPredicateEvaluator` without compiling source text.
- `PreparedExpressionParserActionExecutor` executes registered `PreparedExpressionParserAction` artifacts through `IParserActionExecutor` without compiling source text.
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

Contextual symbols (`ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`) are filtered through `EmbeddedCodePreparationContext.SupportedSymbols` before compilation. Supported symbols are represented as reads from the runtime context parameter when compiling prepared artifacts, so they are not captured as fixed preparation-time constants. Expressions that reference a symbol excluded from `SupportedSymbols` fail through the configured compiler and are returned as `CompilationFailed` results.

## Runtime status

Prepared artifacts can now be prepared from a parser model, stored in a registry, and consumed through runtime adapters explicitly.
Default parser behavior is unchanged, and `ParserEngine` is not modified by this package.
Callers that want the prepared-artifact path must create a preparer, build the registry outside the parser, and pass the adapters through `ParserRuntimeFeaturePolicy`:

```csharp
IExpressionCompiler compiler = GetExpressionCompiler();
var preparer = new ExpressionEmbeddedCodePreparer(compiler);

var buildResult = PreparedExpressionEmbeddedCodeRegistryBuilder.Build(
    definition,
    preparer,
    new PreparedExpressionEmbeddedCodeRegistryBuilderOptions
    {
        GrammarName = definition.Name,
        LanguageOrCompilerIdentity = "custom-expression-language"
    });

var policy = ParserRuntimeFeaturePolicy.Default with
{
    SemanticPredicateEvaluator = new PreparedExpressionSemanticPredicateEvaluator(buildResult.Registry),
    ParserActionExecutor = new PreparedExpressionParserActionExecutor(buildResult.Registry)
};

var parser = new ParserEngine(definition, policy);
```

The builder is never used automatically. It scans `ParserDefinition.ParserRules`, prepares `ValidatingPredicate` nodes and inline `EmbeddedAction` nodes (`ActionContext.Alternative` plus `ActionPosition.Inline`), records non-success preparation results, records duplicate registry keys, and skips grammar-level actions plus rule lifecycle actions (`@init` / `@after`). It depends only on `IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction>`, not on a concrete expression language package.

The registry lookup key is intentionally audit-friendly: it includes the embedded-code kind, owning rule name, raw source text, alternative index, and element index. The builder uses the parser runtime's local priority-ordered alternative index and sequence element index strategy. Runtime contexts use `-1` for unavailable indexes; registry keys normalize those unavailable values to `null`. This is the safest key currently available without changing `ParserEngine`, but it still depends on prepared artifacts carrying metadata that matches the runtime model.

The existing adapters remain available as the opportunistic compilation path:

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
- `PreparedExpressionEmbeddedCodeRegistryBuilder` can invoke that preparer for validating predicates and inline parser actions found in an already-built `ParserDefinition`.
- `PreparedExpressionSemanticPredicate` and `PreparedExpressionParserAction` execute already-compiled delegates.
- `PreparedExpressionSemanticPredicateEvaluator` and `PreparedExpressionParserActionExecutor` look up those artifacts in `PreparedExpressionEmbeddedCodeRegistry` and execute them without depending on `IExpressionCompiler`.
- A missing registry entry returns `NotEvaluated` or `NotExecuted`, allowing parsing to continue under the existing `ParserEngine` outcome handling.
- The prepared-artifact path does not change parser scheduling, memoization, diagnostics emission, parse-tree shape, or default runtime policy.

The target model remains to prepare executable artifacts before parsing and then execute only those artifacts while parsing. This package now provides the explicit runtime adapters and an explicit registry builder for that consumption step, but it still does not automatically prepare a parser model. `ParserEngine` should remain language-neutral: it should execute policy outcomes and emit diagnostics, not select an expression language or compile embedded source code.

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
