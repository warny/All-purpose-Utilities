using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Model;
using Utils.Parser.Runtime;
using Utils.Parser.Resolution;
using Utils.Parser.Diagnostics;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies parser behavior with injected semantic predicate evaluators.
/// </summary>
[TestClass]
public class ParserEngineSemanticPredicateEvaluatorTests
{
    [TestMethod]
    public void Parse_WhenEvaluatorRejectsPredicate_ReturnsErrorNode()
    {
        var startRule = CreateStartRuleWithPredicate(new ValidatingPredicate("canProceed"));
        var definition = CreateDefinition(startRule);
        var parser = new ParserEngine(definition, new ConstantSemanticPredicateEvaluator(SemanticPredicateEvaluationResult.Rejected));

        var result = parser.Parse([]);

        Assert.IsInstanceOfType<ErrorNode>(result);
    }

    [TestMethod]
    public void Parse_WhenEvaluatorSatisfiesPredicate_ParsesSuccessfully()
    {
        var startRule = CreateStartRuleWithPredicate(new GatingPredicate("canProceed"));
        var definition = CreateDefinition(startRule);
        var parser = new ParserEngine(definition, new ConstantSemanticPredicateEvaluator(SemanticPredicateEvaluationResult.Satisfied));

        var result = parser.Parse([]);

        Assert.IsInstanceOfType<ParserNode>(result);
    }

    [TestMethod]
    public void Parse_WhenEvaluatorReturnsNotEvaluated_EmitsLegacyDiagnostic()
    {
        var startRule = CreateStartRuleWithPredicate(new ValidatingPredicate("canProceed"));
        var definition = CreateDefinition(startRule);
        var parser = new ParserEngine(definition, new ConstantSemanticPredicateEvaluator(SemanticPredicateEvaluationResult.NotEvaluated));
        var diagnostics = new DiagnosticBag();

        parser.Parse([], diagnostics: diagnostics);

        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.SemanticPredicateNotEnforced.Code));
    }

    private static Rule CreateStartRuleWithPredicate(RuleContent predicate)
    {
        return new Rule(
            "start",
            0,
            false,
            new Alternation([
                new Alternative(
                    0,
                    Associativity.Left,
                    new Sequence([
                        predicate
                    ]))
            ]));
    }

    private static ParserDefinition CreateDefinition(Rule startRule)
    {
        return RuleResolver.Resolve(new ParserDefinition(
            Name: "G",
            Type: GrammarType.Parser,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [startRule],
            RootRule: startRule));
    }

    /// <summary>
    /// Test evaluator returning a deterministic preconfigured result.
    /// </summary>
    private sealed class ConstantSemanticPredicateEvaluator : ISemanticPredicateEvaluator
    {
        private readonly SemanticPredicateEvaluationResult _result;

        /// <summary>
        /// Initializes the evaluator.
        /// </summary>
        /// <param name="result">Result returned by <see cref="Evaluate"/>.</param>
        public ConstantSemanticPredicateEvaluator(SemanticPredicateEvaluationResult result)
        {
            _result = result;
        }

        /// <inheritdoc />
        public SemanticPredicateEvaluationResult Evaluate(SemanticPredicateEvaluationContext context)
        {
            return _result;
        }
    }
}
