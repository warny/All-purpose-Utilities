using System.Linq.Expressions;
using Utils.Expressions;
using Utils.Parser.Diagnostics.EmbeddedCode;
using Utils.Parser.EmbeddedCode;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace Utils.Parser.Expressions;

/// <summary>
/// Provides the supported runtime-inline compilation facade for embedded parser semantic predicates and inline actions.
/// It validates and transforms embedded source through the shared transformation pipeline, builds expressions that read
/// runtime symbols from the delegate context, delegates expression compilation to <see cref="IExpressionCompiler"/>,
/// and materializes specialized CLR lambdas in strongly typed prepared artifacts. Preparation does not execute the
/// embedded code or capture runtime values. This facade does not generate C# source, replace the source generator,
/// compile lexer hooks, or select the parser's overall execution strategy.
/// </summary>
public sealed class ExpressionEmbeddedCodePreparer : IEmbeddedCodePreparer<PreparedExpressionSemanticPredicate, PreparedExpressionParserAction>
{
    private readonly IExpressionCompiler _compiler;
    private readonly IParserEmbeddedCodeTransformer _transformer;

    /// <summary>
    /// Initializes the supported expression-backed runtime preparation facade.
    /// </summary>
    /// <param name="compiler">
    /// Required caller-supplied compiler that converts validated transformed text into an expression tree. It is not
    /// called for unsupported categories or non-runtime targets and is called exactly once for each supported preparation.
    /// </param>
    /// <param name="embeddedCodeTransformer">
    /// Optional transformer run through <see cref="EmbeddedCodeTransformationPipeline"/> before compiler invocation;
    /// <see cref="NoOpParserEmbeddedCodeTransformer.Instance"/> is used when no transformer is supplied.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="compiler"/> is <c>null</c>.</exception>
    public ExpressionEmbeddedCodePreparer(IExpressionCompiler compiler, IParserEmbeddedCodeTransformer? embeddedCodeTransformer = null)
    {
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _transformer = embeddedCodeTransformer ?? NoOpParserEmbeddedCodeTransformer.Instance;
    }

    /// <summary>
    /// Prepares a <see cref="EmbeddedCodeKind.SemanticPredicate"/> for the
    /// <see cref="EmbeddedCodeTarget.RuntimeInlineExpression"/> target without evaluating it.
    /// </summary>
    /// <param name="source">Raw semantic-predicate source and its parser metadata.</param>
    /// <param name="context">Preparation context that selects the runtime target and allowed contextual symbols.</param>
    /// <returns>
    /// <see cref="EmbeddedCodePreparationStatus.Unsupported"/> for another category,
    /// <see cref="EmbeddedCodePreparationStatus.PreservedNotCompiled"/> for another target,
    /// <see cref="EmbeddedCodePreparationStatus.CompilationFailed"/> after a transformation or compilation failure, or
    /// <see cref="EmbeddedCodePreparationStatus.Succeeded"/> with a <see cref="PreparedExpressionSemanticPredicate"/>
    /// containing a <see cref="Func{T, TResult}"/> over <see cref="SemanticPredicateEvaluationContext"/>.
    /// The shared pipeline transforms the source before runtime-bound symbol expressions are passed to the compiler;
    /// the resulting delegate is not invoked during preparation.
    /// </returns>
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
            TransformedEmbeddedCode transformedCode = TransformSource(source, context, ParserEmbeddedCodeLocation.SemanticPredicate);
            var runtimeContext = Expression.Parameter(typeof(SemanticPredicateEvaluationContext), "context");
            var expression = _compiler.Compile(transformedCode.Text, BuildSemanticPredicateSymbols(runtimeContext, context.SupportedSymbols));
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

    /// <summary>
    /// Prepares an <see cref="EmbeddedCodeKind.ParserInlineAction"/> for the
    /// <see cref="EmbeddedCodeTarget.RuntimeInlineExpression"/> target without executing it.
    /// </summary>
    /// <param name="source">Raw inline parser-action source and its parser metadata.</param>
    /// <param name="context">Preparation context that selects the runtime target and allowed contextual symbols.</param>
    /// <returns>
    /// <see cref="EmbeddedCodePreparationStatus.Unsupported"/> for another category,
    /// <see cref="EmbeddedCodePreparationStatus.PreservedNotCompiled"/> for another target,
    /// <see cref="EmbeddedCodePreparationStatus.CompilationFailed"/> after a transformation or compilation failure, or
    /// <see cref="EmbeddedCodePreparationStatus.Succeeded"/> with a <see cref="PreparedExpressionParserAction"/>
    /// containing an <see cref="Action{T}"/> over <see cref="ParserActionExecutionContext"/>.
    /// The shared pipeline transforms the source before runtime-bound symbol expressions are passed to the compiler.
    /// A non-<see cref="Void"/> expression is normalized to an action whose value is ignored, and the resulting delegate
    /// is not invoked during preparation.
    /// </returns>
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
            TransformedEmbeddedCode transformedCode = TransformSource(source, context, ParserEmbeddedCodeLocation.InlineAction);
            var runtimeContext = Expression.Parameter(typeof(ParserActionExecutionContext), "context");
            var expression = _compiler.Compile(transformedCode.Text, BuildParserActionSymbols(runtimeContext, context.SupportedSymbols));
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
    /// Adapts the runtime facade to the shared transformation-and-validation pipeline without compiling source.
    /// It supplies <see cref="ParserEmbeddedCodeTransformationPath.RuntimeCompilation"/> failure metadata and converts
    /// the common transformation exception to the public Expressions-package compatibility exception.
    /// </summary>
    /// <param name="source">Original embedded-code source.</param>
    /// <param name="context">Runtime preparation context that supplies grammar metadata.</param>
    /// <param name="location">Embedded-code location represented by the source.</param>
    /// <returns>Validated transformed source to pass to the compiler.</returns>
    private TransformedEmbeddedCode TransformSource(EmbeddedCodeSource source, EmbeddedCodePreparationContext context, ParserEmbeddedCodeLocation location)
    {
        try
        {
            return EmbeddedCodeTransformationPipeline.TransformAndValidate(
                _transformer,
                source.RawCode,
                new ParserEmbeddedCodeTransformationContext
                {
                    Location = location,
                    GrammarName = context.GrammarName,
                    RuleName = source.RuleName
                },
                new ParserEmbeddedCodeTransformationFailureContext
                {
                    Path = ParserEmbeddedCodeTransformationPath.RuntimeCompilation,
                    Location = location,
                    GrammarName = context.GrammarName,
                    RuleName = source.RuleName
                });
        }
        catch (Utils.Parser.Diagnostics.EmbeddedCode.ParserEmbeddedCodeTransformationException exception)
        {
            throw new ParserEmbeddedCodeTransformationException(
                exception.Message,
                exception.DiagnosticCode,
                exception.DiagnosticMessage,
                exception.Path,
                exception.Location,
                exception.GrammarName,
                exception.RuleName,
                exception.Span,
                exception.InnerException);
        }
    }

    /// <summary>
    /// Builds symbol expressions that read semantic predicate values from the runtime context parameter at execution
    /// time rather than capturing values during preparation.
    /// </summary>
    /// <param name="runtimeContext">Runtime context parameter used by the compiled predicate delegate.</param>
    /// <param name="supportedSymbols">Contextual symbols that the preparation context allows this compiler invocation to expose.</param>
    /// <returns>Symbol expressions resolved by the expression compiler.</returns>
    private static IReadOnlyDictionary<string, Expression> BuildSemanticPredicateSymbols(
        ParameterExpression runtimeContext,
        IReadOnlySet<EmbeddedCodeContextSymbol> supportedSymbols) =>
        BuildRuntimeContextSymbols(runtimeContext, supportedSymbols);

    /// <summary>
    /// Builds symbol expressions that read parser action values from the runtime context parameter at execution time
    /// rather than capturing values during preparation.
    /// </summary>
    /// <param name="runtimeContext">Runtime context parameter used by the compiled action delegate.</param>
    /// <param name="supportedSymbols">Contextual symbols that the preparation context allows this compiler invocation to expose.</param>
    /// <returns>Symbol expressions resolved by the expression compiler.</returns>
    private static IReadOnlyDictionary<string, Expression> BuildParserActionSymbols(
        ParameterExpression runtimeContext,
        IReadOnlySet<EmbeddedCodeContextSymbol> supportedSymbols) =>
        BuildRuntimeContextSymbols(runtimeContext, supportedSymbols);

    /// <summary>
    /// Builds symbol expressions for <c>ruleName</c>, <c>inputPosition</c>, <c>alternativeIndex</c>, and
    /// <c>elementIndex</c> that read shared runtime values from a predicate or action context parameter. No runtime value
    /// is captured while the artifact is prepared.
    /// </summary>
    /// <param name="runtimeContext">Runtime context parameter used by the compiled artifact delegate.</param>
    /// <param name="supportedSymbols">Contextual symbols that the preparation context allows this compiler invocation to expose.</param>
    /// <returns>Symbol expressions resolved by the expression compiler.</returns>
    private static IReadOnlyDictionary<string, Expression> BuildRuntimeContextSymbols(
        ParameterExpression runtimeContext,
        IReadOnlySet<EmbeddedCodeContextSymbol> supportedSymbols)
    {
        var symbols = new Dictionary<string, Expression>(StringComparer.Ordinal);
        foreach (var symbol in supportedSymbols)
        {
            switch (symbol)
            {
                case EmbeddedCodeContextSymbol.RuleName:
                    symbols["ruleName"] = BuildRuleName(runtimeContext);
                    break;
                case EmbeddedCodeContextSymbol.InputPosition:
                    symbols["inputPosition"] = Expression.Property(runtimeContext, "InputPosition");
                    break;
                case EmbeddedCodeContextSymbol.AlternativeIndex:
                    symbols["alternativeIndex"] = Expression.Property(runtimeContext, "AlternativeIndex");
                    break;
                case EmbeddedCodeContextSymbol.ElementIndex:
                    symbols["elementIndex"] = Expression.Property(runtimeContext, "ElementIndex");
                    break;
            }
        }

        return symbols;
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

        return [constructName, context.Target.ToString()];
    }
}
