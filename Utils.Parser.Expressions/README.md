# omy.Utils.Parser.Expressions

`omy.Utils.Parser.Expressions` provides optional expression-backed integration for `omy.Utils.Parser`.
It connects an explicit `IExpressionCompiler` to parser embedded-code surfaces without selecting a language automatically.

## Purpose

Use this package when you want to work with embedded parser code through a caller-selected expression compiler. It is the runtime-inline prepared expression path documented in [`docs/parser/ANTLRCompatibility.md`](https://github.com/warny/All-purpose-Utilities/blob/master/docs/parser/ANTLRCompatibility.md); it is not the generated C# path.

Available surfaces:

- `ExpressionEmbeddedCodePreparer` prepares expression-backed artifacts for semantic predicates (`{ condition }?`) and inline parser actions (`{ code }`).
- `PreparedExpressionSemanticPredicate` stores a compiled predicate delegate and can produce `SemanticPredicateEvaluationOutcome` values without recompiling source text.
- `PreparedExpressionParserAction` stores a compiled action delegate and can produce `ParserActionExecutionOutcome` values without recompiling source text.
- `PreparedExpressionEmbeddedCodeKey` identifies prepared artifacts by embedded-code kind, owning rule, source text, alternative index, and element index.
- `PreparedExpressionEmbeddedCodeRegistry` stores prepared semantic predicates separately from prepared parser inline actions.
- `PreparedExpressionEmbeddedCodeRegistryBuilder` explicitly scans `ParserDefinition` models and fills a registry from validating predicates and inline parser actions.
- `PreparedExpressionRuntimePolicyBuilder` assembles the full opt-in prepared runtime path from a `ParserDefinition` and an `IExpressionCompiler`.
- `PreparedExpressionRuntimePolicyBuildResult` exposes the configured `ParserRuntimeFeaturePolicy`, registry, registry build result, and failure summary.
- `PreparedExpressionRuntimePolicyBuilderOptions` configures grammar/compiler identity, supported symbols, and an optional base runtime policy.
- `PreparedExpressionSemanticPredicateEvaluator` executes registered `PreparedExpressionSemanticPredicate` artifacts through `ISemanticPredicateEvaluator` without compiling source text.
- `PreparedExpressionParserActionExecutor` executes registered `PreparedExpressionParserAction` artifacts through `IParserActionExecutor` without compiling source text.
- `ExpressionSemanticPredicateEvaluator` remains the current runtime adapter from `IExpressionCompiler` to `ISemanticPredicateEvaluator`.
- `ExpressionParserActionExecutor` remains the current runtime adapter from `IExpressionCompiler` to `IParserActionExecutor`.

## Supported runtime compilation facade

`ExpressionEmbeddedCodePreparer` is the single supported `omy.Utils.Parser.Expressions` entry point for
transforming, compiling, and materializing executable parser embedded code before runtime execution. It
implements `IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction>`,
coordinates a caller-supplied `IExpressionCompiler`, and optionally coordinates a caller-supplied
`IParserEmbeddedCodeTransformer` (the no-op transformer is the default). It does not replace the compiler:
it directs that compiler within the supported preparation pipeline.

The complete preparation path is:

```text
EmbeddedCodeSource
→ validation of kind and target
→ EmbeddedCodeTransformationPipeline
→ TransformedEmbeddedCode
→ runtime-bound symbol expressions
→ IExpressionCompiler
→ specialized CLR lambda
→ prepared artifact
```

| Source | Runtime context | Delegate | Prepared artifact |
| --- | --- | --- | --- |
| `SemanticPredicate` | `SemanticPredicateEvaluationContext` | `Func<SemanticPredicateEvaluationContext, bool>` | `PreparedExpressionSemanticPredicate` |
| `ParserInlineAction` | `ParserActionExecutionContext` | `Action<ParserActionExecutionContext>` | `PreparedExpressionParserAction` |

The facade normalizes outcomes as follows:

| Condition | Preparation status |
| --- | --- |
| The method receives the wrong embedded-code category | `Unsupported` |
| The target is `SourceGeneratorCSharp` rather than `RuntimeInlineExpression` | `PreservedNotCompiled` |
| Transformation, expression compilation, or delegate materialization fails | `CompilationFailed` |
| A specialized prepared artifact is produced | `Success` (`EmbeddedCodePreparationStatus.Succeeded`) |

Only parser semantic predicates and inline parser actions are supported. Lexer predicates, lexer actions,
grammar actions, `@init`, `@after`, non-inline parser actions, and every other category that is not compatible
with runtime-inline preparation remain unsupported. The facade does not generate C# source, replace the source
generator, execute embedded code during preparation, capture preparation-time runtime values, or decide the
parser's global execution strategy. Generated C# remains a separate `Utils.Parser.Generators` path.

### Minimal preparation example

The following uses the real public preparation contracts; `GetExpressionCompiler()` represents the caller's
selection of an existing `IExpressionCompiler` implementation.

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

EmbeddedCodePreparationResult<PreparedExpressionSemanticPredicate> result =
    preparer.PrepareSemanticPredicate(source, context);
```

The preparer:

- supports `EmbeddedCodeKind.SemanticPredicate` and `EmbeddedCodeKind.ParserInlineAction`;
- uses only the supplied `IExpressionCompiler` contract;
- can be paired by consumers with expression compiler packages such as `Utils.Expressions.CSyntax` or `Utils.Expressions.VBSyntax`;
- does not reference those compiler packages directly;
- returns `Unsupported` for `RuleInitAction`, `RuleAfterAction`, `GrammarAction`, lexer actions, lexer predicates, and non-inline parser actions;
- returns `PreservedNotCompiled` for `EmbeddedCodeTarget.SourceGeneratorCSharp`, because C# source-generation hooks belong to `Utils.Parser.Generators`;
- never executes predicates or actions during preparation.

Internally, preparation first crosses the same transformation-and-validation boundary as generated C# and receives a validated `TransformedEmbeddedCode`. Only then does this package build runtime symbol expressions and the specialized lambda and invoke the supplied `IExpressionCompiler`. Lexer hooks are not supported by this runtime path and are not synthesized by the shared boundary.

Contextual symbols (`ruleName`, `inputPosition`, `alternativeIndex`, `elementIndex`) are filtered through `EmbeddedCodePreparationContext.SupportedSymbols` before compilation. Supported symbols are represented as reads from the runtime context parameter when compiling prepared artifacts, so they are not captured as fixed preparation-time constants. Expressions that reference a symbol excluded from `SupportedSymbols` fail through the configured compiler and are returned as `CompilationFailed` results.

## Runtime status

Prepared artifacts can be prepared from a parser model, stored in a registry, and consumed through runtime adapters explicitly.
Default parser behavior is unchanged, and `ParserEngine` is not modified by this package. The package does not execute lexer embedded code, grammar actions, `@members`, `@init`, `@after`, non-inline parser actions, action rollback/buffering, context mutation, or arbitrary parser state mutation.
The recommended full prepared runtime flow is to build an opt-in policy outside the parser and pass that policy explicitly:

```csharp
IExpressionCompiler compiler = GetExpressionCompiler();

var integration = PreparedExpressionRuntimePolicyBuilder.Build(
    definition,
    compiler,
    new PreparedExpressionRuntimePolicyBuilderOptions
    {
        GrammarName = definition.Name,
        LanguageOrCompilerIdentity = "custom-expression-language",
        BasePolicy = existingPolicy
    });

if (integration.HasFailures)
{
    foreach (var entry in integration.RegistryBuildResult.NonSuccessEntries)
    {
        Console.WriteLine($"Embedded code preparation failed: {entry.Source.SourceText}");
    }
}

var parser = new ParserEngine(definition, integration.Policy);
```

`PreparedExpressionRuntimePolicyBuilder.Build` creates the `ExpressionEmbeddedCodePreparer`, runs `PreparedExpressionEmbeddedCodeRegistryBuilder`, creates `PreparedExpressionSemanticPredicateEvaluator` and `PreparedExpressionParserActionExecutor`, and returns a policy configured with those adapters. When `BasePolicy` is supplied, only `SemanticPredicateEvaluator` and `ParserActionExecutor` are replaced; other policy features, such as the passive runtime observer, are preserved. Preparation failures remain visible through `RegistryBuildResult` and `HasFailures`; the builder does not throw for compiler failures unless the compiler itself fails outside the preparation result flow.

Consumers that need lower-level control can still assemble the same components manually:

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

The builder is never used automatically. It scans `ParserDefinition.ParserRules`, prepares `ValidatingPredicate` nodes and inline `EmbeddedAction` nodes (`ActionContext.Alternative` plus `ActionPosition.Inline`), records non-success preparation results, records duplicate registry keys, and skips grammar-level actions plus rule lifecycle actions (`@init` / `@after`). It does not skip executable predicates or inline actions merely because they are nested in runtime-executable structures such as quantifiers, negations, nested alternations, or left-recursive tails. It depends only on `IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction>`, not on a concrete expression language package.

The registry lookup key is intentionally audit-friendly: it includes the embedded-code kind, owning rule name, raw source text, alternative index, and element index. The builder mirrors the parser runtime's active indexing strategy: alternatives are ordered by runtime priority, sequences use zero-based item indexes, quantifier and negation inner probes use the runtime alternative index as the direct inner element index, and direct-left-recursive recursive alternatives are prepared from the effective tail after the leading self-reference is removed. Runtime contexts use `-1` for unavailable indexes; registry keys normalize those unavailable values to `null`. This is the safest key currently available without changing `ParserEngine`, but it still depends on prepared artifacts carrying metadata that matches the runtime model.

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
- `PreparedExpressionRuntimePolicyBuilder` is the convenience integration API for building the registry and prepared runtime policy in one explicit opt-in call.
- `PreparedExpressionSemanticPredicate` and `PreparedExpressionParserAction` execute already-compiled delegates.
- `PreparedExpressionSemanticPredicateEvaluator` and `PreparedExpressionParserActionExecutor` look up those artifacts in `PreparedExpressionEmbeddedCodeRegistry` and execute them without depending on `IExpressionCompiler`.
- A missing registry entry returns `NotEvaluated` or `NotExecuted`, allowing parsing to continue under the existing `ParserEngine` outcome handling.
- The prepared-artifact path does not change parser scheduling, memoization, diagnostics emission, parse-tree shape, or default runtime policy.

The prepared path compiles executable artifacts before parsing and then executes only those artifacts while parsing. This package provides the explicit runtime adapters, an explicit registry builder, and a convenience policy builder for that opt-in consumption step, but it still does not automatically prepare a parser model. `ParserEngine` remains language-neutral: it executes policy outcomes and emits diagnostics, but does not select an expression language or compile embedded source code.

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

Lexer actions, lexer predicates, grammar members/`@members`, `@init`, `@after`, non-inline parser actions, rollback/buffering, and parser-state mutation are not implemented by this package. See [`docs/parser/ANTLRCompatibility.md`](https://github.com/warny/All-purpose-Utilities/blob/master/docs/parser/ANTLRCompatibility.md) for the canonical compatibility status.

### Shared runtime indexing metadata

Parser embedded-code discovery now has a shared metadata model in `Utils.Parser.EmbeddedCode`. `EmbeddedCodeRuntimeDiscovery` walks a `ParserDefinition` and emits `EmbeddedCodeRuntimeEntry` values with the raw source, `EmbeddedCodeKind`, owning rule name, runtime-compatible alternative and element indexes, a runtime key for executable entries, and an explicit `EmbeddedCodeUnsupportedReason` for skipped entries. The metadata mirrors the existing parser runtime indexing rules for priority-ordered alternatives, single-item alternatives, sequences, quantifier inner parsing, negation probes, and direct-left-recursive base/tail alternatives. It is metadata only: it does not compile source, generate C#, execute actions, or change `ParserEngine` behavior.

The expression-backed prepared registry consumes this shared discovery result before invoking its preparer. Unsupported constructs such as grammar actions, `@init`, `@after`, lexer actions/predicates, and non-inline parser actions remain non-executable, but they now carry explicit skip reasons. Invalid C# in a source-generator-supported hook remains a Roslyn compilation error rather than a custom parser diagnostic.

## API documentation

See the [versioned API documentation for `omy.Utils.Parser.Expressions` 0.1.0](https://warny.github.io/All-purpose-Utilities/v0.1.0/).
