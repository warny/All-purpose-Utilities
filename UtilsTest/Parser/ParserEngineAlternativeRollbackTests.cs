using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Resolution;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies transactional parser execution-state rollback around ordinary parser alternatives.
/// </summary>
[TestClass]
public class ParserEngineAlternativeRollbackTests
{
    [TestMethod]
    public void FailedAlternativeActionRollback_RestoresStateForFollowingPredicate()
    {
        var manager = new CounterExecutionStateManager();
        var parser = CreateParser(
            CreateStartRule([
                Alternative(0, Sequence([RuleRef("A"), Action("bump"), RuleRef("B")])),
                Alternative(1, Sequence([Predicate("stateIsZero"), RuleRef("A")]))
            ]),
            manager);

        var result = parser.Parse([Token("A", "a")]);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual(0, manager.Value);
        CollectionAssert.Contains(manager.PredicateValues, 0);
    }

    [TestMethod]
    public void FailedAlternativeRollback_RestoresInputPositionForNextAlternative()
    {
        var parser = CreateParser(
            CreateStartRule([
                Alternative(0, Sequence([RuleRef("A"), RuleRef("B")])),
                Alternative(1, RuleRef("A"))
            ]),
            new CounterExecutionStateManager());

        var result = parser.Parse([Token("A", "a")]);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual(0, result.Span.Position);
        Assert.AreEqual(1, result.Span.Length);
    }

    [TestMethod]
    public void FollowingAlternative_SeesStateRestoredAfterFailedActionAlternative()
    {
        var manager = new CounterExecutionStateManager();
        var parser = CreateParser(
            CreateStartRule([
                Alternative(0, Sequence([RuleRef("A"), Action("bump"), RuleRef("B")])),
                Alternative(1, Sequence([Predicate("stateIsZero"), RuleRef("A")]))
            ]),
            manager);

        var result = parser.Parse([Token("A", "a")]);

        Assert.IsInstanceOfType<ParserNode>(result);
        CollectionAssert.AreEqual(new List<int> { 0 }, manager.PredicateValues);
    }

    [TestMethod]
    public void SuccessfulAlternativeRollback_DoesNotDiscardWinningMutation()
    {
        var manager = new CounterExecutionStateManager();
        var parser = CreateParser(
            CreateStartRule([
                Alternative(0, Sequence([RuleRef("A"), Action("bump")])),
                Alternative(1, RuleRef("A"))
            ]),
            manager);

        var result = parser.Parse([Token("A", "a")]);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual(1, manager.Value);
    }

    [TestMethod]
    public void NullExecutionStateManager_PreservesNoOpSideEffectBehavior()
    {
        var counter = 0;
        var executor = new DelegatingActionExecutor(_ =>
        {
            counter++;
            return ParserActionExecutionOutcome.Executed;
        });
        var parser = CreateParser(
            CreateStartRule([
                Alternative(0, Sequence([RuleRef("A"), Action("bump"), RuleRef("B")])),
                Alternative(1, RuleRef("A"))
            ]),
            NullParserExecutionStateManager.Instance,
            executor,
            new DefaultSemanticPredicateEvaluator());

        var result = parser.Parse([Token("A", "a")]);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual(1, counter);
    }

    [TestMethod]
    public void Memoization_UsesRestoredExecutionStateKeyAfterFailedAlternative()
    {
        var manager = new CounterExecutionStateManager();
        var childRule = ParserRule("child", 1, [Alternative(0, Sequence([Predicate("stateIsZero"), RuleRef("A")]))]);
        var startRule = CreateStartRule([
            Alternative(0, Sequence([RuleRef("child"), Action("bump"), RuleRef("B")])),
            Alternative(1, RuleRef("child"))
        ]);
        var parser = CreateParser(startRule, manager, parserRules: [startRule, childRule]);
        var diagnostics = new DiagnosticBag();

        var result = parser.Parse([Token("A", "a")], diagnostics: diagnostics);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual(0, manager.Value);
        Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.ParseMemoHit.Code));
    }

    [TestMethod]
    public void FailedAlternativeAction_DoesNotPolluteLaterAlternatives()
    {
        var manager = new CounterExecutionStateManager();
        var parser = CreateParser(
            CreateStartRule([
                Alternative(0, Sequence([Action("bump"), RuleRef("B")])),
                Alternative(1, Sequence([Predicate("stateIsZero"), RuleRef("A")]))
            ]),
            manager);

        var result = parser.Parse([Token("A", "a")]);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual(0, manager.Value);
        CollectionAssert.AreEqual(new List<int> { 0 }, manager.PredicateValues);
    }

    [TestMethod]
    public void FailedActionAlternativeThenPredicateAlternative_RestoresInitialStateAndSucceeds()
    {
        var manager = new CounterExecutionStateManager();
        var parser = CreateParser(
            CreateStartRule([
                Alternative(0, Sequence([RuleRef("A"), Action("bump"), RuleRef("B")])),
                Alternative(1, Sequence([Predicate("stateIsZero"), RuleRef("A")]))
            ]),
            manager);

        var result = parser.Parse([Token("A", "a")]);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual(0, manager.Value);
        CollectionAssert.AreEqual(new List<int> { 0 }, manager.PredicateValues);
    }

    /// <summary>
    /// Creates a parser engine with a counter-backed execution-state policy.
    /// </summary>
    private static ParserEngine CreateParser(Rule startRule, CounterExecutionStateManager manager, IReadOnlyList<Rule>? parserRules = null)
    {
        return CreateParser(
            startRule,
            manager,
            new CounterActionExecutor(manager),
            new CounterPredicateEvaluator(manager),
            parserRules);
    }

    /// <summary>
    /// Creates a parser engine with explicit runtime policy components.
    /// </summary>
    private static ParserEngine CreateParser(
        Rule startRule,
        IParserExecutionStateManager manager,
        IParserActionExecutor executor,
        ISemanticPredicateEvaluator evaluator,
        IReadOnlyList<Rule>? parserRules = null)
    {
        var allParserRules = parserRules ?? [startRule];
        var definition = RuleResolver.Resolve(new ParserDefinition(
            Name: "RollbackGrammar",
            Type: GrammarType.Combined,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [LexerRule("A", "a"), LexerRule("B", "b")])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: allParserRules,
            RootRule: startRule));
        return new ParserEngine(
            definition,
            ParserRuntimeFeaturePolicy.Default with
            {
                ExecutionStateManager = manager,
                ParserActionExecutor = executor,
                SemanticPredicateEvaluator = evaluator
            });
    }

    /// <summary>
    /// Creates the start parser rule.
    /// </summary>
    private static Rule CreateStartRule(IReadOnlyList<Alternative> alternatives)
    {
        return ParserRule("start", 0, alternatives);
    }

    /// <summary>
    /// Creates a parser rule with the supplied alternatives.
    /// </summary>
    private static Rule ParserRule(string name, int declarationOrder, IReadOnlyList<Alternative> alternatives)
    {
        return new Rule(name, declarationOrder, false, new Alternation(alternatives));
    }

    /// <summary>
    /// Creates a lexer rule that matches one literal token text.
    /// </summary>
    private static Rule LexerRule(string name, string text)
    {
        return new Rule(name, 0, true, new Alternation([Alternative(0, new LiteralMatch(text))]));
    }

    /// <summary>
    /// Creates an alternative with left associativity.
    /// </summary>
    private static Alternative Alternative(int priority, RuleContent content)
    {
        return new Alternative(priority, Associativity.Left, content);
    }

    /// <summary>
    /// Creates a rule reference.
    /// </summary>
    private static RuleRef RuleRef(string ruleName)
    {
        return new RuleRef(ruleName);
    }

    /// <summary>
    /// Creates a parser sequence.
    /// </summary>
    private static Sequence Sequence(IReadOnlyList<RuleContent> items)
    {
        return new Sequence(items);
    }

    /// <summary>
    /// Creates an inline parser action.
    /// </summary>
    private static EmbeddedAction Action(string code)
    {
        return new EmbeddedAction(code, ActionContext.Alternative, ActionPosition.Inline, []);
    }

    /// <summary>
    /// Creates a validating semantic predicate.
    /// </summary>
    private static ValidatingPredicate Predicate(string code)
    {
        return new ValidatingPredicate(code);
    }

    /// <summary>
    /// Creates a default-channel token for parser-engine tests.
    /// </summary>
    private static Token Token(string ruleName, string text)
    {
        return new Token(new SourceSpan(0, text.Length, 1, 1), ruleName, "DEFAULT_MODE", "DEFAULT_CHANNEL", text);
    }

    /// <summary>
    /// Mutable counter state used as parser execution state.
    /// </summary>
    private sealed class CounterExecutionStateManager : IParserExecutionStateManager
    {
        /// <summary>
        /// Gets or sets the mutable semantic state value.
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// Gets the semantic-state values observed by predicates.
        /// </summary>
        public List<int> PredicateValues { get; } = [];

        /// <inheritdoc />
        public object Capture()
        {
            return Value;
        }

        /// <inheritdoc />
        public void Restore(object snapshot)
        {
            Value = (int)snapshot;
        }

        /// <inheritdoc />
        public ParserExecutionStateKey GetCurrentStateKey()
        {
            return new ParserExecutionStateKey((ulong)Value);
        }
    }

    /// <summary>
    /// Action executor that increments the counter for <c>bump</c> actions.
    /// </summary>
    private sealed class CounterActionExecutor : IParserActionExecutor
    {
        private readonly CounterExecutionStateManager _manager;

        /// <summary>
        /// Initializes the action executor.
        /// </summary>
        public CounterActionExecutor(CounterExecutionStateManager manager)
        {
            _manager = manager;
        }

        /// <inheritdoc />
        public ParserActionExecutionOutcome Execute(ParserActionExecutionContext context)
        {
            if (string.Equals(context.ActionCode.Trim(), "bump", StringComparison.Ordinal))
            {
                _manager.Value++;
            }

            return ParserActionExecutionOutcome.Executed;
        }
    }

    /// <summary>
    /// Predicate evaluator that accepts only when the counter state matches the predicate text.
    /// </summary>
    private sealed class CounterPredicateEvaluator : ISemanticPredicateEvaluator
    {
        private readonly CounterExecutionStateManager _manager;

        /// <summary>
        /// Initializes the predicate evaluator.
        /// </summary>
        public CounterPredicateEvaluator(CounterExecutionStateManager manager)
        {
            _manager = manager;
        }

        /// <inheritdoc />
        public SemanticPredicateEvaluationOutcome Evaluate(SemanticPredicateEvaluationContext context)
        {
            _manager.PredicateValues.Add(_manager.Value);
            return context.PredicateCode.Trim() switch
            {
                "stateIsZero" => _manager.Value == 0 ? SemanticPredicateEvaluationOutcome.Satisfied : SemanticPredicateEvaluationOutcome.Rejected,
                "stateIsOne" => _manager.Value == 1 ? SemanticPredicateEvaluationOutcome.Satisfied : SemanticPredicateEvaluationOutcome.Rejected,
                _ => SemanticPredicateEvaluationOutcome.Rejected
            };
        }
    }

    /// <summary>
    /// Delegates parser action execution to a test callback.
    /// </summary>
    private sealed class DelegatingActionExecutor : IParserActionExecutor
    {
        private readonly Func<ParserActionExecutionContext, ParserActionExecutionOutcome> _execute;

        /// <summary>
        /// Initializes the delegating action executor.
        /// </summary>
        public DelegatingActionExecutor(Func<ParserActionExecutionContext, ParserActionExecutionOutcome> execute)
        {
            _execute = execute;
        }

        /// <inheritdoc />
        public ParserActionExecutionOutcome Execute(ParserActionExecutionContext context)
        {
            return _execute(context);
        }
    }
}
