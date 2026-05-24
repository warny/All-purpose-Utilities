using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Utils.Expressions;
using Utils.Parser.Diagnostics;
using Utils.Parser.Runtime;

namespace Utils.Parser.Expressions;

/// <summary>
/// Adapts an <see cref="IExpressionCompiler"/> to <see cref="IParserActionExecutor"/> for runtime inline parser action execution.
/// </summary>
public sealed class ExpressionParserActionExecutor : IParserActionExecutor
{
    private static readonly Regex ContextSymbolRegex = new(
        @"\b(ruleName|inputPosition|alternativeIndex|elementIndex)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<string, Func<ParserActionExecutionContext, Expression>> SymbolFactoryByName =
        new Dictionary<string, Func<ParserActionExecutionContext, Expression>>(StringComparer.Ordinal)
        {
            ["ruleName"] = static context => Expression.Constant(context.Rule.Name, typeof(string)),
            ["inputPosition"] = static context => Expression.Constant(context.InputPosition, typeof(int)),
            ["alternativeIndex"] = static context => Expression.Constant(context.AlternativeIndex, typeof(int)),
            ["elementIndex"] = static context => Expression.Constant(context.ElementIndex, typeof(int))
        };

    private readonly IExpressionCompiler _compiler;
    private readonly ConcurrentDictionary<string, Func<ParserActionExecutionOutcome>> _compiledActionByCode = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new action executor that compiles parser action source code with the provided compiler.
    /// </summary>
    /// <param name="compiler">Expression compiler used to compile action source code.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="compiler"/> is <c>null</c>.</exception>
    public ExpressionParserActionExecutor(IExpressionCompiler compiler)
    {
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
    }

    /// <inheritdoc />
    public ParserActionExecutionOutcome Execute(ParserActionExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (UsesContextualSymbols(context.ActionCode))
        {
            return CompileAction(context.ActionCode, context)();
        }

        // ConcurrentDictionary.GetOrAdd may invoke the value factory more than once under contention.
        // This cache is therefore opportunistic: duplicate concurrent compilation is acceptable,
        // but only the compiled delegate/failure is cached. Execution results are never cached.
        var executor = _compiledActionByCode.GetOrAdd(context.ActionCode, code => CompileAction(code, context));
        return executor();
    }

    private Func<ParserActionExecutionOutcome> CompileAction(string actionCode, ParserActionExecutionContext context)
    {
        try
        {
            var symbols = BuildSymbols(context);
            var expression = _compiler.Compile(actionCode, symbols);
            var action = BuildActionDelegate(expression);

            return () =>
            {
                try
                {
                    action();
                    return ParserActionExecutionOutcome.Executed;
                }
                catch (Exception exception)
                {
                    return ParserActionExecutionOutcome.NotExecuted(
                        ParserDiagnostics.EmbeddedCodeCompilationFailed,
                        exception,
                        "parser action",
                        exception.Message);
                }
            };
        }
        catch (Exception exception)
        {
            return () => ParserActionExecutionOutcome.NotExecuted(
                ParserDiagnostics.EmbeddedCodeCompilationFailed,
                exception,
                "parser action",
                exception.Message);
        }
    }

    private static Action BuildActionDelegate(Expression expression)
    {
        try
        {
            var executableExpression = expression.Type == typeof(void)
                ? expression
                : Expression.Block(expression, Expression.Empty());
            return Expression.Lambda<Action>(executableExpression).Compile();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Expected executable expression.", exception);
        }
    }

    private static bool UsesContextualSymbols(string actionCode) => ContextSymbolRegex.IsMatch(actionCode);

    private static IReadOnlyDictionary<string, Expression> BuildSymbols(ParserActionExecutionContext context)
    {
        var symbols = new Dictionary<string, Expression>(SymbolFactoryByName.Count, StringComparer.Ordinal);
        foreach (var symbolEntry in SymbolFactoryByName)
        {
            symbols[symbolEntry.Key] = symbolEntry.Value(context);
        }

        return symbols;
    }
}
