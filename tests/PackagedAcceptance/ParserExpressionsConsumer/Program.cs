using System.Linq.Expressions;
using Utils.Expressions;
using Utils.Parser.Expressions;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace PackagedAcceptance.ParserExpressions;

/// <summary>Exercises the packaged expression-backed semantic predicate adapter.</summary>
internal static class Program
{
    /// <summary>Evaluates true, false, contextual, and invalid expressions through the public adapter.</summary>
    private static void Main()
    {
        var evaluator = new ExpressionSemanticPredicateEvaluator(new AcceptanceExpressionCompiler());
        Require(evaluator.Evaluate(CreateContext("true")).Status == SemanticPredicateEvaluationStatus.Satisfied, "True predicate was not satisfied.");
        Require(evaluator.Evaluate(CreateContext("false")).Status == SemanticPredicateEvaluationStatus.Rejected, "False predicate was not rejected.");
        Require(evaluator.Evaluate(CreateContext("ruleName == \"start\"")).Status == SemanticPredicateEvaluationStatus.Satisfied, "Contextual predicate was not evaluated.");
        SemanticPredicateEvaluationOutcome invalid = evaluator.Evaluate(CreateContext("invalid"));
        Require(invalid.Status == SemanticPredicateEvaluationStatus.NotEvaluated && invalid.Exception is InvalidOperationException, "Invalid expression did not preserve its compilation failure.");
        Console.WriteLine("Parser.Expressions packaged predicate consumer passed.");
    }

    /// <summary>Creates the runtime context supplied to the expression evaluator.</summary>
    private static SemanticPredicateEvaluationContext CreateContext(string code)
    {
        var rule = new Rule("start", 0, false, new Alternation([new Alternative(0, Associativity.Left, new Sequence([]))]));
        return new SemanticPredicateEvaluationContext(rule, new ValidatingPredicate(code), code, 0, 0, 0);
    }

    /// <summary>Requires a packaged expression behavior.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>Provides a deterministic public-interface expression compiler for package integration.</summary>
    private sealed class AcceptanceExpressionCompiler : IExpressionCompiler
    {
        /// <inheritdoc />
        public Expression Compile(string content, IReadOnlyDictionary<string, Expression>? symbols = null) => content switch
        {
            "true" => Expression.Constant(true),
            "false" => Expression.Constant(false),
            "ruleName == \"start\"" => Expression.Equal(symbols!["ruleName"], Expression.Constant("start")),
            _ => throw new InvalidOperationException("Invalid acceptance expression.")
        };
    }
}
