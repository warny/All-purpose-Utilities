using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq.Expressions;
using Utils.Expressions;
using Utils.Parser.Expressions;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Validates runtime semantic predicate evaluation through <see cref="ExpressionSemanticPredicateEvaluator"/>.
/// </summary>
[TestClass]
public class ExpressionSemanticPredicateEvaluatorTests
{
    [TestMethod]
    public void Evaluate_WhenPredicateIsTrue_ReturnsSatisfied()
    {
        var evaluator = new ExpressionSemanticPredicateEvaluator(new FakeExpressionCompiler());

        var result = evaluator.Evaluate(CreateContext("true"));

        Assert.AreEqual(SemanticPredicateEvaluationStatus.Satisfied, result.Status);
    }

    [TestMethod]
    public void Evaluate_WhenPredicateIsFalse_ReturnsRejected()
    {
        var evaluator = new ExpressionSemanticPredicateEvaluator(new FakeExpressionCompiler());

        var result = evaluator.Evaluate(CreateContext("false"));

        Assert.AreEqual(SemanticPredicateEvaluationStatus.Rejected, result.Status);
    }

    [TestMethod]
    public void Evaluate_WhenContextSymbolIsUsed_ReturnsSatisfied()
    {
        var evaluator = new ExpressionSemanticPredicateEvaluator(new FakeExpressionCompiler());

        var result = evaluator.Evaluate(CreateContext("ruleName == \"start\""));

        Assert.AreEqual(SemanticPredicateEvaluationStatus.Satisfied, result.Status);
    }

    [TestMethod]
    public void Evaluate_WhenCompilationThrows_ReturnsNotEvaluated()
    {
        var evaluator = new ExpressionSemanticPredicateEvaluator(new FakeExpressionCompiler());

        var result = evaluator.Evaluate(CreateContext("throw"));

        Assert.AreEqual(SemanticPredicateEvaluationStatus.NotEvaluated, result.Status);
    }

    [TestMethod]
    public void Evaluate_WhenExpressionIsNotBoolean_ReturnsNotEvaluated()
    {
        var evaluator = new ExpressionSemanticPredicateEvaluator(new FakeExpressionCompiler());

        var result = evaluator.Evaluate(CreateContext("42"));

        Assert.AreEqual(SemanticPredicateEvaluationStatus.NotEvaluated, result.Status);
    }

    [TestMethod]
    public void Evaluate_WhenPredicateIsReused_UsesCompilationCache()
    {
        var compiler = new FakeExpressionCompiler();
        var evaluator = new ExpressionSemanticPredicateEvaluator(compiler);

        _ = evaluator.Evaluate(CreateContext("true"));
        _ = evaluator.Evaluate(CreateContext("true"));

        Assert.AreEqual(1, compiler.CompileCount);
    }

    [TestMethod]
    public void Evaluate_WhenSamePredicateCodeUsesDifferentRuleContext_DoesNotReuseCapturedSymbols()
    {
        var evaluator = new ExpressionSemanticPredicateEvaluator(new FakeExpressionCompiler());

        var firstResult = evaluator.Evaluate(CreateContext("ruleName == \"start\"", "start"));
        var secondResult = evaluator.Evaluate(CreateContext("ruleName == \"start\"", "other"));

        Assert.AreEqual(SemanticPredicateEvaluationOutcome.Satisfied, firstResult);
        Assert.AreEqual(SemanticPredicateEvaluationOutcome.Rejected, secondResult);
    }

    private static SemanticPredicateEvaluationContext CreateContext(string predicateCode, string ruleName = "start")
    {
        var rule = new Rule(
            ruleName,
            0,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new Sequence([]))
            ]));

        return new SemanticPredicateEvaluationContext(
            Rule: rule,
            Predicate: new ValidatingPredicate(predicateCode),
            PredicateCode: predicateCode,
            InputPosition: 0,
            AlternativeIndex: 0,
            ElementIndex: 0);
    }

    /// <summary>
    /// Minimal expression compiler used to drive deterministic test behavior.
    /// </summary>
    private sealed class FakeExpressionCompiler : IExpressionCompiler
    {
        /// <summary>
        /// Gets the number of times <see cref="Compile"/> was called.
        /// </summary>
        public int CompileCount { get; private set; }

        /// <inheritdoc />
        public Expression Compile(string content, IReadOnlyDictionary<string, Expression>? symbols = null)
        {
            CompileCount++;

            return content switch
            {
                "true" => Expression.Constant(true),
                "false" => Expression.Constant(false),
                "ruleName == \"start\"" => Expression.Equal(
                    symbols!["ruleName"],
                    Expression.Constant("start", typeof(string))),
                "inputPosition == 0" => Expression.Equal(
                    symbols!["inputPosition"],
                    Expression.Constant(0, typeof(int))),
                "42" => Expression.Constant(42),
                "throw" => throw new InvalidOperationException("boom"),
                _ => Expression.Constant(true)
            };
        }
    }
}
