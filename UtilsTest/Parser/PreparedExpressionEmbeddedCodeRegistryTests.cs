using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.EmbeddedCode;
using Utils.Parser.Expressions;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Validates lookup of prepared expression embedded-code artifacts without compiling source at runtime.
/// </summary>
[TestClass]
public class PreparedExpressionEmbeddedCodeRegistryTests
{
    [TestMethod]
    public void TryAddSemanticPredicate_WhenArtifactIsRegistered_FindsArtifactByRuntimeContext()
    {
        var registry = new PreparedExpressionEmbeddedCodeRegistry();
        var artifact = CreatePredicateArtifact("start", "true", 0, 0, _ => true);

        var added = registry.TryAddSemanticPredicate(artifact);
        var found = registry.TryGetSemanticPredicate(CreatePredicateContext("start", "true", 0, 0), out var resolved);

        Assert.IsTrue(added);
        Assert.IsTrue(found);
        Assert.AreSame(artifact, resolved);
    }

    [TestMethod]
    public void TryAddParserAction_WhenArtifactIsRegistered_FindsArtifactByRuntimeContext()
    {
        var registry = new PreparedExpressionEmbeddedCodeRegistry();
        var artifact = CreateActionArtifact("start", "increment", 0, 0, _ => { });

        var added = registry.TryAddParserAction(artifact);
        var found = registry.TryGetParserAction(CreateActionContext("start", "increment", 0, 0), out var resolved);

        Assert.IsTrue(added);
        Assert.IsTrue(found);
        Assert.AreSame(artifact, resolved);
    }

    [TestMethod]
    public void TryGetSemanticPredicate_WhenSameTextExistsInDifferentRules_DoesNotCollide()
    {
        var registry = new PreparedExpressionEmbeddedCodeRegistry();
        var startArtifact = CreatePredicateArtifact("start", "inputPosition == 0", 0, 0, _ => true);
        var otherArtifact = CreatePredicateArtifact("other", "inputPosition == 0", 0, 0, _ => false);

        Assert.IsTrue(registry.TryAddSemanticPredicate(startArtifact));
        Assert.IsTrue(registry.TryAddSemanticPredicate(otherArtifact));

        Assert.IsTrue(registry.TryGetSemanticPredicate(CreatePredicateContext("start", "inputPosition == 0", 0, 0), out var resolvedStart));
        Assert.IsTrue(registry.TryGetSemanticPredicate(CreatePredicateContext("other", "inputPosition == 0", 0, 0), out var resolvedOther));
        Assert.AreSame(startArtifact, resolvedStart);
        Assert.AreSame(otherArtifact, resolvedOther);
    }

    [TestMethod]
    public void TryGetParserAction_WhenSameTextExistsAtDifferentIndexes_DoesNotCollide()
    {
        var registry = new PreparedExpressionEmbeddedCodeRegistry();
        var firstArtifact = CreateActionArtifact("start", "record", 0, 0, _ => { });
        var secondArtifact = CreateActionArtifact("start", "record", 1, 2, _ => { });

        Assert.IsTrue(registry.TryAddParserAction(firstArtifact));
        Assert.IsTrue(registry.TryAddParserAction(secondArtifact));

        Assert.IsTrue(registry.TryGetParserAction(CreateActionContext("start", "record", 0, 0), out var resolvedFirst));
        Assert.IsTrue(registry.TryGetParserAction(CreateActionContext("start", "record", 1, 2), out var resolvedSecond));
        Assert.AreSame(firstArtifact, resolvedFirst);
        Assert.AreSame(secondArtifact, resolvedSecond);
    }

    [TestMethod]
    public void Evaluate_WhenPreparedPredicateReturnsTrue_ReturnsSatisfied()
    {
        var registry = new PreparedExpressionEmbeddedCodeRegistry();
        Assert.IsTrue(registry.TryAddSemanticPredicate(CreatePredicateArtifact("start", "is-valid", 0, 0, _ => true)));
        var evaluator = new PreparedExpressionSemanticPredicateEvaluator(registry);

        var outcome = evaluator.Evaluate(CreatePredicateContext("start", "is-valid", 0, 0));

        Assert.AreEqual(SemanticPredicateEvaluationStatus.Satisfied, outcome.Status);
    }

    [TestMethod]
    public void Evaluate_WhenPreparedPredicateReturnsFalse_ReturnsRejected()
    {
        var registry = new PreparedExpressionEmbeddedCodeRegistry();
        Assert.IsTrue(registry.TryAddSemanticPredicate(CreatePredicateArtifact("start", "is-valid", 0, 0, _ => false)));
        var evaluator = new PreparedExpressionSemanticPredicateEvaluator(registry);

        var outcome = evaluator.Evaluate(CreatePredicateContext("start", "is-valid", 0, 0));

        Assert.AreEqual(SemanticPredicateEvaluationStatus.Rejected, outcome.Status);
    }

    [TestMethod]
    public void Evaluate_WhenNoPreparedPredicateExists_ReturnsNotEvaluated()
    {
        var evaluator = new PreparedExpressionSemanticPredicateEvaluator(new PreparedExpressionEmbeddedCodeRegistry());

        var outcome = evaluator.Evaluate(CreatePredicateContext("start", "missing", 0, 0));

        Assert.AreEqual(SemanticPredicateEvaluationStatus.NotEvaluated, outcome.Status);
        Assert.IsNull(outcome.Diagnostic);
    }

    [TestMethod]
    public void Execute_WhenPreparedActionExists_ReturnsExecutedAndRunsDelegate()
    {
        var registry = new PreparedExpressionEmbeddedCodeRegistry();
        var counter = 0;
        Assert.IsTrue(registry.TryAddParserAction(CreateActionArtifact("start", "increment", 0, 0, _ => counter++)));
        var executor = new PreparedExpressionParserActionExecutor(registry);

        var outcome = executor.Execute(CreateActionContext("start", "increment", 0, 0));

        Assert.AreEqual(ParserActionExecutionStatus.Executed, outcome.Status);
        Assert.AreEqual(1, counter);
    }

    [TestMethod]
    public void Execute_WhenNoPreparedActionExists_ReturnsNotExecuted()
    {
        var executor = new PreparedExpressionParserActionExecutor(new PreparedExpressionEmbeddedCodeRegistry());

        var outcome = executor.Execute(CreateActionContext("start", "missing", 0, 0));

        Assert.AreEqual(ParserActionExecutionStatus.NotExecuted, outcome.Status);
        Assert.IsNull(outcome.Diagnostic);
    }

    [TestMethod]
    public void EvaluateAndExecute_WhenUsingPreparedAdapters_DoNotCompileSource()
    {
        var registry = new PreparedExpressionEmbeddedCodeRegistry();
        var predicateCalls = 0;
        var actionCalls = 0;
        Assert.IsTrue(registry.TryAddSemanticPredicate(CreatePredicateArtifact("start", "prepared-predicate", 0, 0, _ =>
        {
            predicateCalls++;
            return true;
        })));
        Assert.IsTrue(registry.TryAddParserAction(CreateActionArtifact("start", "prepared-action", 0, 1, _ => actionCalls++)));
        var evaluator = new PreparedExpressionSemanticPredicateEvaluator(registry);
        var executor = new PreparedExpressionParserActionExecutor(registry);

        var predicateOutcome = evaluator.Evaluate(CreatePredicateContext("start", "prepared-predicate", 0, 0));
        var actionOutcome = executor.Execute(CreateActionContext("start", "prepared-action", 0, 1));

        Assert.AreEqual(SemanticPredicateEvaluationStatus.Satisfied, predicateOutcome.Status);
        Assert.AreEqual(ParserActionExecutionStatus.Executed, actionOutcome.Status);
        Assert.AreEqual(1, predicateCalls);
        Assert.AreEqual(1, actionCalls);
    }

    /// <summary>
    /// Creates a prepared semantic predicate artifact around an already-available delegate.
    /// </summary>
    /// <param name="ruleName">Owning rule name.</param>
    /// <param name="sourceText">Raw predicate source text.</param>
    /// <param name="alternativeIndex">Owning alternative index.</param>
    /// <param name="elementIndex">Owning element index.</param>
    /// <param name="predicate">Prepared predicate delegate.</param>
    /// <returns>A prepared semantic predicate artifact.</returns>
    private static PreparedExpressionSemanticPredicate CreatePredicateArtifact(
        string ruleName,
        string sourceText,
        int alternativeIndex,
        int elementIndex,
        Func<SemanticPredicateEvaluationContext, bool> predicate)
    {
        return new PreparedExpressionSemanticPredicate(
            new EmbeddedCodeSource(sourceText, EmbeddedCodeKind.SemanticPredicate, ruleName, alternativeIndex, elementIndex),
            CreatePreparationContext(ruleName),
            predicate);
    }

    /// <summary>
    /// Creates a prepared parser action artifact around an already-available delegate.
    /// </summary>
    /// <param name="ruleName">Owning rule name.</param>
    /// <param name="sourceText">Raw action source text.</param>
    /// <param name="alternativeIndex">Owning alternative index.</param>
    /// <param name="elementIndex">Owning element index.</param>
    /// <param name="action">Prepared action delegate.</param>
    /// <returns>A prepared parser action artifact.</returns>
    private static PreparedExpressionParserAction CreateActionArtifact(
        string ruleName,
        string sourceText,
        int alternativeIndex,
        int elementIndex,
        Action<ParserActionExecutionContext> action)
    {
        return new PreparedExpressionParserAction(
            new EmbeddedCodeSource(sourceText, EmbeddedCodeKind.ParserInlineAction, ruleName, alternativeIndex, elementIndex),
            CreatePreparationContext(ruleName),
            action);
    }

    /// <summary>
    /// Creates a runtime semantic predicate context matching registry key metadata.
    /// </summary>
    /// <param name="ruleName">Current rule name.</param>
    /// <param name="sourceText">Runtime predicate source text.</param>
    /// <param name="alternativeIndex">Runtime alternative index.</param>
    /// <param name="elementIndex">Runtime element index.</param>
    /// <returns>A semantic predicate evaluation context.</returns>
    private static SemanticPredicateEvaluationContext CreatePredicateContext(
        string ruleName,
        string sourceText,
        int alternativeIndex,
        int elementIndex)
    {
        return new SemanticPredicateEvaluationContext(
            CreateRule(ruleName),
            new ValidatingPredicate(sourceText),
            sourceText,
            InputPosition: 0,
            AlternativeIndex: alternativeIndex,
            ElementIndex: elementIndex);
    }

    /// <summary>
    /// Creates a runtime parser action context matching registry key metadata.
    /// </summary>
    /// <param name="ruleName">Current rule name.</param>
    /// <param name="sourceText">Runtime action source text.</param>
    /// <param name="alternativeIndex">Runtime alternative index.</param>
    /// <param name="elementIndex">Runtime element index.</param>
    /// <returns>A parser action execution context.</returns>
    private static ParserActionExecutionContext CreateActionContext(
        string ruleName,
        string sourceText,
        int alternativeIndex,
        int elementIndex)
    {
        return new ParserActionExecutionContext(
            CreateRule(ruleName),
            new EmbeddedAction(sourceText, ActionContext.Alternative, ActionPosition.Inline, []),
            sourceText,
            InputPosition: 0,
            AlternativeIndex: alternativeIndex,
            ElementIndex: elementIndex);
    }

    /// <summary>
    /// Creates a minimal parser rule for runtime contexts.
    /// </summary>
    /// <param name="ruleName">Rule name.</param>
    /// <returns>A parser rule with an empty alternative.</returns>
    private static Rule CreateRule(string ruleName)
    {
        return new Rule(
            ruleName,
            0,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new Sequence([]))
            ]));
    }

    /// <summary>
    /// Creates a preparation context for a prepared artifact.
    /// </summary>
    /// <param name="ruleName">Owning rule name.</param>
    /// <returns>An embedded-code preparation context.</returns>
    private static EmbeddedCodePreparationContext CreatePreparationContext(string ruleName)
    {
        return new EmbeddedCodePreparationContext(
            "G",
            EmbeddedCodeTarget.RuntimeInlineExpression,
            ruleName,
            languageOrCompilerIdentity: "prepared-test");
    }
}
