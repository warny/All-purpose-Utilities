using System.Collections.Concurrent;
using System.Linq.Expressions;
using Utils.Expressions;
using Utils.Parser.Runtime;

namespace Utils.Parser.Expressions;

/// <summary>
/// Adapts an <see cref="IExpressionCompiler"/> to <see cref="ISemanticPredicateEvaluator"/> for runtime semantic predicate evaluation.
/// </summary>
public sealed class ExpressionSemanticPredicateEvaluator : ISemanticPredicateEvaluator
{
    private static readonly IReadOnlyDictionary<string, Func<SemanticPredicateEvaluationContext, Expression>> SymbolFactoryByName =
        new Dictionary<string, Func<SemanticPredicateEvaluationContext, Expression>>(StringComparer.Ordinal)
        {
            ["ruleName"] = static context => Expression.Constant(context.Rule.Name, typeof(string)),
            ["inputPosition"] = static context => Expression.Constant(context.InputPosition, typeof(int)),
            ["alternativeIndex"] = static context => Expression.Constant(context.AlternativeIndex, typeof(int)),
            ["elementIndex"] = static context => Expression.Constant(context.ElementIndex, typeof(int))
        };

    private readonly IExpressionCompiler _compiler;
    private readonly ConcurrentDictionary<string, Func<bool>?> _compiledPredicateByCode = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new evaluator that compiles semantic predicate source code with the provided compiler.
    /// </summary>
    /// <param name="compiler">Expression compiler used to compile predicate source code.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="compiler"/> is <c>null</c>.</exception>
    public ExpressionSemanticPredicateEvaluator(IExpressionCompiler compiler)
    {
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
    }

    /// <inheritdoc />
    public SemanticPredicateEvaluationResult Evaluate(SemanticPredicateEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var compiledPredicate = _compiledPredicateByCode.GetOrAdd(context.PredicateCode, code => CompilePredicate(code, context));

        if (compiledPredicate is null)
        {
            return SemanticPredicateEvaluationResult.NotEvaluated;
        }

        return compiledPredicate() ? SemanticPredicateEvaluationResult.Satisfied : SemanticPredicateEvaluationResult.Rejected;
    }

    private Func<bool>? CompilePredicate(string predicateCode, SemanticPredicateEvaluationContext context)
    {
        try
        {
            var symbols = BuildSymbols(context);
            var expression = _compiler.Compile(predicateCode, symbols);

            if (expression.Type != typeof(bool))
            {
                return null;
            }

            return Expression.Lambda<Func<bool>>(expression).Compile();
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<string, Expression> BuildSymbols(SemanticPredicateEvaluationContext context)
    {
        var symbols = new Dictionary<string, Expression>(SymbolFactoryByName.Count, StringComparer.Ordinal);

        foreach (var symbolEntry in SymbolFactoryByName)
        {
            symbols[symbolEntry.Key] = symbolEntry.Value(context);
        }

        return symbols;
    }
}
