using System.Linq.Expressions;
using Utils.Expressions;
using Utils.Parser.EmbeddedCode;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace Utils.Parser.Expressions;

/// <summary>
/// Prepares runtime-inline embedded parser code by compiling expression-backed artifacts with an explicit expression compiler.
/// </summary>
public sealed class ExpressionEmbeddedCodePreparer : IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction>
{
    private readonly IExpressionCompiler _compiler;

    /// <summary>
    /// Initializes a new expression-backed embedded-code preparer.
    /// </summary>
    /// <param name="compiler">Expression compiler selected by the caller.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="compiler"/> is <c>null</c>.</exception>
    public ExpressionEmbeddedCodePreparer(IExpressionCompiler compiler)
    {
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
    }

    /// <inheritdoc />
    public EmbeddedCodePreparationResult<PreparedExpressionSemanticPredicate> PrepareSemanticPredicate(
        EmbeddedCodeSource source,
        EmbeddedCodePreparationContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        if (source.Kind != EmbeddedCodeKind.SemanticPredicate)
        {
            return EmbeddedCodePreparationResult<PreparedExpressionSemanticPredicate>.Unsupported(CreateDiagnosticArguments(source, context));
        }

        if (context.Target != EmbeddedCodeTarget.RuntimeInlineExpression)
        {
            return EmbeddedCodePreparationResult<PreparedExpressionSemanticPredicate>.PreservedNotCompiled(CreateDiagnosticArguments(source, context));
        }

        try
        {
            var runtimeContext = Expression.Parameter(typeof(SemanticPredicateEvaluationContext), "context");
            var expression = _compiler.Compile(source.SourceText, BuildSemanticPredicateSymbols(runtimeContext, context.SupportedSymbols));
            if (expression.Type != typeof(bool))
            {
                return EmbeddedCodePreparationResult<PreparedExpressionSemanticPredicate>.CompilationFailed(
                    new InvalidOperationException($"Expected Boolean result, got {expression.Type.Name}."),
                    CreateDiagnosticArguments(source, context));
            }

            var predicate = Expression.Lambda<Func<SemanticPredicateEvaluationContext, bool>>(expression, runtimeContext).Compile();
            var artifact = new PreparedExpressionSemanticPredicate(source, context, predicate);
            return EmbeddedCodePreparationResult<PreparedExpressionSemanticPredicate>.Success(artifact);
        }
        catch (Exception exception)
        {
            return EmbeddedCodePreparationResult<PreparedExpressionSemanticPredicate>.CompilationFailed(
                exception,
                CreateDiagnosticArguments(source, context));
        }
    }

    /// <inheritdoc />
    public EmbeddedCodePreparationResult<PreparedExpressionParserAction> PrepareParserAction(
        EmbeddedCodeSource source,
        EmbeddedCodePreparationContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        if (source.Kind != EmbeddedCodeKind.ParserInlineAction)
        {
            return EmbeddedCodePreparationResult<PreparedExpressionParserAction>.Unsupported(CreateDiagnosticArguments(source, context));
        }

        if (context.Target != EmbeddedCodeTarget.RuntimeInlineExpression)
        {
            return EmbeddedCodePreparationResult<PreparedExpressionParserAction>.PreservedNotCompiled(CreateDiagnosticArguments(source, context));
        }

        try
        {
            var runtimeContext = Expression.Parameter(typeof(ParserActionExecutionContext), "context");
            var expression = _compiler.Compile(source.SourceText, BuildParserActionSymbols(runtimeContext, context.SupportedSymbols));
            var executableExpression = expression.Type == typeof(void)
                ? expression
                : Expression.Block(expression, Expression.Empty());
            var action = Expression.Lambda<Action<ParserActionExecutionContext>>(executableExpression, runtimeContext).Compile();
            var artifact = new PreparedExpressionParserAction(source, context, action);
            return EmbeddedCodePreparationResult<PreparedExpressionParserAction>.Success(artifact);
        }
        catch (Exception exception)
        {
            return EmbeddedCodePreparationResult<PreparedExpressionParserAction>.CompilationFailed(
                exception,
                CreateDiagnosticArguments(source, context));
        }
    }

    /// <summary>
    /// Builds symbol expressions that read semantic predicate values from the runtime context parameter.
    /// </summary>
    /// <param name="runtimeContext">Runtime context parameter used by the compiled predicate delegate.</param>
    /// <param name="supportedSymbols">Contextual symbols that the preparation context allows this compiler invocation to expose.</param>
    /// <returns>Symbol expressions resolved by the expression compiler.</returns>
    private static IReadOnlyDictionary<string, Expression> BuildSemanticPredicateSymbols(
        ParameterExpression runtimeContext,
        IReadOnlySet<EmbeddedCodeContextSymbol> supportedSymbols)
    {
        var symbols = new Dictionary<string, Expression>(StringComparer.Ordinal);
        foreach (var symbol in supportedSymbols)
        {
            AddSemanticPredicateSymbol(symbols, symbol, runtimeContext);
        }

        return symbols;
    }

    /// <summary>
    /// Builds symbol expressions that read parser action values from the runtime context parameter.
    /// </summary>
    /// <param name="runtimeContext">Runtime context parameter used by the compiled action delegate.</param>
    /// <param name="supportedSymbols">Contextual symbols that the preparation context allows this compiler invocation to expose.</param>
    /// <returns>Symbol expressions resolved by the expression compiler.</returns>
    private static IReadOnlyDictionary<string, Expression> BuildParserActionSymbols(
        ParameterExpression runtimeContext,
        IReadOnlySet<EmbeddedCodeContextSymbol> supportedSymbols)
    {
        var symbols = new Dictionary<string, Expression>(StringComparer.Ordinal);
        foreach (var symbol in supportedSymbols)
        {
            AddParserActionSymbol(symbols, symbol, runtimeContext);
        }

        return symbols;
    }

    /// <summary>
    /// Adds a semantic predicate symbol when the preparation context exposes it.
    /// </summary>
    /// <param name="symbols">Mutable symbol dictionary to update.</param>
    /// <param name="symbol">Context symbol selected by the preparation context.</param>
    /// <param name="runtimeContext">Runtime context parameter used by the compiled predicate delegate.</param>
    private static void AddSemanticPredicateSymbol(
        Dictionary<string, Expression> symbols,
        EmbeddedCodeContextSymbol symbol,
        ParameterExpression runtimeContext)
    {
        switch (symbol)
        {
            case EmbeddedCodeContextSymbol.RuleName:
                symbols["ruleName"] = BuildRuleName(runtimeContext);
                break;
            case EmbeddedCodeContextSymbol.InputPosition:
                symbols["inputPosition"] = Expression.Property(runtimeContext, nameof(SemanticPredicateEvaluationContext.InputPosition));
                break;
            case EmbeddedCodeContextSymbol.AlternativeIndex:
                symbols["alternativeIndex"] = Expression.Property(runtimeContext, nameof(SemanticPredicateEvaluationContext.AlternativeIndex));
                break;
            case EmbeddedCodeContextSymbol.ElementIndex:
                symbols["elementIndex"] = Expression.Property(runtimeContext, nameof(SemanticPredicateEvaluationContext.ElementIndex));
                break;
        }
    }

    /// <summary>
    /// Adds a parser action symbol when the preparation context exposes it.
    /// </summary>
    /// <param name="symbols">Mutable symbol dictionary to update.</param>
    /// <param name="symbol">Context symbol selected by the preparation context.</param>
    /// <param name="runtimeContext">Runtime context parameter used by the compiled action delegate.</param>
    private static void AddParserActionSymbol(
        Dictionary<string, Expression> symbols,
        EmbeddedCodeContextSymbol symbol,
        ParameterExpression runtimeContext)
    {
        switch (symbol)
        {
            case EmbeddedCodeContextSymbol.RuleName:
                symbols["ruleName"] = BuildRuleName(runtimeContext);
                break;
            case EmbeddedCodeContextSymbol.InputPosition:
                symbols["inputPosition"] = Expression.Property(runtimeContext, nameof(ParserActionExecutionContext.InputPosition));
                break;
            case EmbeddedCodeContextSymbol.AlternativeIndex:
                symbols["alternativeIndex"] = Expression.Property(runtimeContext, nameof(ParserActionExecutionContext.AlternativeIndex));
                break;
            case EmbeddedCodeContextSymbol.ElementIndex:
                symbols["elementIndex"] = Expression.Property(runtimeContext, nameof(ParserActionExecutionContext.ElementIndex));
                break;
        }
    }

    /// <summary>
    /// Builds an expression that reads the current rule name from a semantic predicate or parser action context.
    /// </summary>
    /// <param name="runtimeContext">Runtime context parameter that exposes a <see cref="Rule"/> property.</param>
    /// <returns>An expression that resolves the current rule name at artifact execution time.</returns>
    private static Expression BuildRuleName(ParameterExpression runtimeContext)
    {
        var rule = Expression.Property(runtimeContext, "Rule");
        return Expression.Property(rule, nameof(Rule.Name));
    }

    /// <summary>
    /// Creates shared diagnostic arguments that identify the embedded-code construct and preparation target.
    /// </summary>
    /// <param name="source">Embedded source metadata used to describe the construct.</param>
    /// <param name="context">Preparation context used to describe the target path.</param>
    /// <returns>Diagnostic arguments suitable for embedded-code preparation results.</returns>
    private static IReadOnlyList<object?> CreateDiagnosticArguments(
        EmbeddedCodeSource source,
        EmbeddedCodePreparationContext context)
    {
        var constructName = source.RuleName is null
            ? source.Kind.ToString()
            : $"{source.RuleName}:{source.Kind}";

        return new object?[] { constructName, context.Target.ToString() };
    }
}
