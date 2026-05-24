using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Utils.Expressions;
using Utils.Parser.Diagnostics;
using Utils.Parser.Runtime;

namespace Utils.Parser.Expressions;

/// <summary>
/// Adapts an <see cref="IExpressionCompiler"/> to <see cref="ISemanticPredicateEvaluator"/> for runtime semantic predicate evaluation.
/// </summary>
public sealed class ExpressionSemanticPredicateEvaluator : ISemanticPredicateEvaluator
{
    private static readonly Regex ContextSymbolRegex = new(
        @"\b(ruleName|inputPosition|alternativeIndex|elementIndex)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<string, Func<SemanticPredicateEvaluationContext, Expression>> SymbolFactoryByName =
        new Dictionary<string, Func<SemanticPredicateEvaluationContext, Expression>>(StringComparer.Ordinal)
        {
            ["ruleName"] = static context => Expression.Constant(context.Rule.Name, typeof(string)),
            ["inputPosition"] = static context => Expression.Constant(context.InputPosition, typeof(int)),
            ["alternativeIndex"] = static context => Expression.Constant(context.AlternativeIndex, typeof(int)),
            ["elementIndex"] = static context => Expression.Constant(context.ElementIndex, typeof(int))
        };

    private readonly IExpressionCompiler _compiler;
    private readonly ConcurrentDictionary<string, SemanticPredicateEvaluationOutcome> _compiledPredicateByCode = new(StringComparer.Ordinal);

    public ExpressionSemanticPredicateEvaluator(IExpressionCompiler compiler)
    {
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
    }

    public SemanticPredicateEvaluationOutcome Evaluate(SemanticPredicateEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (UsesContextualSymbols(context.PredicateCode))
        {
            return CompilePredicate(context.PredicateCode, context);
        }

        return _compiledPredicateByCode.GetOrAdd(context.PredicateCode, code => CompilePredicate(code, context));
    }

    private SemanticPredicateEvaluationOutcome CompilePredicate(string predicateCode, SemanticPredicateEvaluationContext context)
    {
        try
        {
            var symbols = BuildSymbols(context);
            var expression = _compiler.Compile(predicateCode, symbols);
            if (expression.Type != typeof(bool))
            {
                return SemanticPredicateEvaluationOutcome.NotEvaluated(
                    ParserDiagnostics.EmbeddedCodeCompilationFailed,
                    null,
                    "semantic predicate",
                    $"Expected Boolean result, got {expression.Type.Name}.");
            }

            return Expression.Lambda<Func<bool>>(expression).Compile()()
                ? SemanticPredicateEvaluationOutcome.Satisfied
                : SemanticPredicateEvaluationOutcome.Rejected;
        }
        catch (Exception exception)
        {
            return SemanticPredicateEvaluationOutcome.NotEvaluated(
                ParserDiagnostics.EmbeddedCodeCompilationFailed,
                exception,
                "semantic predicate",
                exception.Message);
        }
    }

    private static bool UsesContextualSymbols(string predicateCode) => ContextSymbolRegex.IsMatch(predicateCode);

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
