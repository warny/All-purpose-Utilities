using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Resolution;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Locks runtime invariants around memoization, backtracking side effects, shared-prefix metadata, and lookahead conservatism.
/// </summary>
[TestClass]
public class ParserRuntimeInvariantTests
{
    [TestMethod]
    public void Memoization_IntraParse_ReusesSubRuleInvocationWithoutReevaluatingPredicate()
    {
        var subRule = new Rule(
            "sub",
            1,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new Sequence([
                    new ValidatingPredicate("allow"),
                    new RuleRef("A")
                ]))
            ]));
        var startRule = new Rule(
            "start",
            0,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new Sequence([
                    new RuleRef("sub"),
                    new RuleRef("B")
                ])),
                new Alternative(1, Associativity.Left, new RuleRef("sub"))
            ]));
        var definition = CreateDefinition(startRule, [subRule], LexerRule("A", "a"), LexerRule("B", "b"));
        var evaluator = new CountingPredicateEvaluator(SemanticPredicateEvaluationResult.Satisfied);
        var parser = new ParserEngine(definition, evaluator, new CountingActionExecutor(ParserActionExecutionResult.NotExecuted));

        var result = parser.Parse([Token("A", "a")]);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual(1, evaluator.EvaluationCount);
    }

    [TestMethod]
    public void Backtracking_ActionCanExecuteOnRejectedBranch_WithoutRollback()
    {
        var startRule = new Rule(
            "start",
            0,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new Sequence([
                    new EmbeddedAction("branch0();", ActionContext.Alternative, ActionPosition.Inline, []),
                    new RuleRef("A"),
                    new RuleRef("B")
                ])),
                new Alternative(1, Associativity.Left, new RuleRef("A"))
            ]));
        var definition = CreateDefinition(startRule, LexerRule("A", "a"), LexerRule("B", "b"));
        var actions = new CountingActionExecutor(ParserActionExecutionResult.Executed);
        var parser = new ParserEngine(definition, new DefaultSemanticPredicateEvaluator(), actions);

        var result = parser.Parse([Token("A", "a")]);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual(1, actions.ExecutionCount);
    }

    [TestMethod]
    public void PredicateDeterministicPolicy_Satisfied_UsesPredicateBranchAndConsumesAllTokens()
    {
        var startRule = new Rule(
            "start",
            0,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new Sequence([
                    new ValidatingPredicate("onlyWhenAllowed"),
                    new RuleRef("A"),
                    new RuleRef("B")
                ])),
                new Alternative(1, Associativity.Left, new RuleRef("A"))
            ]));
        var definition = CreateDefinition(startRule, LexerRule("A", "a"), LexerRule("B", "b"));
        var evaluator = new CountingPredicateEvaluator(SemanticPredicateEvaluationResult.Satisfied);
        var parser = new ParserEngine(definition, evaluator, new CountingActionExecutor(ParserActionExecutionResult.NotExecuted));
        var diagnostics = new DiagnosticBag();

        var result = parser.Parse([Token("A", "a"), Token("B", "b")], diagnostics: diagnostics);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual(1, evaluator.EvaluationCount);
        Assert.IsFalse(result is ErrorNode);
        Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.TrailingTokensAfterParse.Code));
    }

    [TestMethod]
    public void PredicateDeterministicPolicy_Rejected_FallsBackAndLeavesTrailingToken()
    {
        var startRule = new Rule(
            "start",
            0,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new Sequence([
                    new ValidatingPredicate("onlyWhenAllowed"),
                    new RuleRef("A"),
                    new RuleRef("B")
                ])),
                new Alternative(1, Associativity.Left, new RuleRef("A"))
            ]));
        var definition = CreateDefinition(startRule, LexerRule("A", "a"), LexerRule("B", "b"));
        var evaluator = new CountingPredicateEvaluator(SemanticPredicateEvaluationResult.Rejected);
        var parser = new ParserEngine(definition, evaluator, new CountingActionExecutor(ParserActionExecutionResult.NotExecuted));
        var diagnostics = new DiagnosticBag();

        var result = parser.Parse([Token("A", "a"), Token("B", "b")], diagnostics: diagnostics);

        Assert.IsInstanceOfType<ErrorNode>(result);
        Assert.AreEqual(1, evaluator.EvaluationCount);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.TrailingTokensAfterParse.Code));
    }

    [TestMethod]
    public void ActiveParseState_ToBranch_PreservesDescriptiveContinuationMetadata()
    {
        var first = new ActiveParseState
        {
            Rule = new Rule("expr", 0, false, new Alternation([])),
            Alternative = new Alternative(0, Associativity.Left, new LiteralMatch("x")),
            OriginInputPosition = 0,
            CurrentInputPosition = 1,
            AlternativeIndex = 0,
            Cursor = new RuleContentCursor { Index = 0, Kind = ScheduledAlternativeCursorKinds.AlternativeRoot },
            PartialNode = new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE", "metadata", null),
            Status = ActiveParseStateStatus.Completed,
            EndPosition = 1,
            ParentStateKey = null,
            Depth = 0,
            Continuation = new ContinuationKey("expr", 0, 0, 1, 0)
        };

        var branch = first.ToBranch();

        Assert.AreEqual(1, branch.EndPosition);
        Assert.AreEqual(0, branch.Cursor.Index);
        Assert.AreEqual(ScheduledAlternativeCursorKinds.AlternativeRoot, branch.Cursor.Kind);
    }


    [TestMethod]
    public void LocalCompletedAlternative_DoesNotGuaranteeGlobalParseAcceptance_WhenTrailingTokensRemain()
    {
        var startRule = new Rule(
            "start",
            0,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new RuleRef("A"))
            ]));
        var definition = CreateDefinition(startRule, LexerRule("A", "a"), LexerRule("B", "b"));
        var parser = new ParserEngine(definition);
        var diagnostics = new DiagnosticBag();

        var result = parser.Parse([Token("A", "a"), Token("B", "b")], diagnostics: diagnostics);

        Assert.IsInstanceOfType<ErrorNode>(result);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.TrailingTokensAfterParse.Code));
    }

    [TestMethod]
    public void FailedAlternative_DoesNotForceGlobalParseFailure_WhenAnotherAlternativeSucceeds()
    {
        var startRule = new Rule(
            "start",
            0,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new Sequence([
                    new RuleRef("A"),
                    new RuleRef("B")
                ])),
                new Alternative(1, Associativity.Left, new RuleRef("A"))
            ]));
        var definition = CreateDefinition(startRule, LexerRule("A", "a"), LexerRule("B", "b"));
        var parser = new ParserEngine(definition);

        var result = parser.Parse([Token("A", "a")]);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.IsFalse(result is ErrorNode);
    }

    [TestMethod]
    public void Pruning_EmitsOnlyAmbiguityPruningDiagnostic_ForSchedulerLevelDeduplication()
    {
        var scheduler = new AlternativeScheduler();
        var rule = new Rule("r", 0, false, new Alternation([
            new Alternative(0, Associativity.Left, new LiteralMatch("a"), "X"),
            new Alternative(1, Associativity.Left, new LiteralMatch("a"), "X")
        ]));
        var state = new ActiveParseState
        {
            Rule = rule,
            Alternative = rule.Content.Alternatives[0],
            OriginInputPosition = 0,
            CurrentInputPosition = 1,
            AlternativeIndex = 0,
            Cursor = new RuleContentCursor { Index = 0, Kind = ScheduledAlternativeCursorKinds.AlternativeRoot },
            PartialNode = new ParserNode(new SourceSpan(0, 1), "DEFAULT_MODE", rule, []),
            EndPosition = 1,
            Status = ActiveParseStateStatus.Completed,
            ParentStateKey = null,
            Depth = 0,
            Continuation = null
        };
        var diagnostics = new DiagnosticBag();

        _ = scheduler.Run(rule, rule.Content.Alternatives, 0, 0, diagnostics, (_, i) =>
            new ScheduledAlternativeExecutionResult(state with { Alternative = rule.Content.Alternatives[i], AlternativeIndex = i }, new ParserLookaheadProbeResult(ParserLookaheadProbeKind.RequiresParse, "A", "a", ["A"])));

        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.AmbiguousAlternativesPruned.Code));
        Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.ParseFailure.Code));
        Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.TrailingTokensAfterParse.Code));
    }

    [TestMethod]
    public void LookaheadProbe_DoesNotEvaluatePredicatesOrExecuteActions()
    {
        var probe = new ParserLookaheadProbe();
        var alternative = new Alternative(0, Associativity.Left, new Sequence([
            new EmbeddedAction("sideEffect();", ActionContext.Alternative, ActionPosition.Inline, []),
            new ValidatingPredicate("gate"),
            new LiteralMatch("a")
        ]));

        var result = probe.Probe(alternative, Token("A", "a"), static _ => null, false);

        Assert.AreEqual(ParserLookaheadProbeKind.Unknown, result.Kind);
    }

    [TestMethod]
    public void ParserEngine_RuntimeParseOutcome_RemainsAuthoritative_WhenAlternativesSharePrefix()
    {
        var startRule = new Rule(
            "start",
            0,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new Sequence([
                    new RuleRef("A"),
                    new RuleRef("B")
                ])),
                new Alternative(1, Associativity.Left, new Sequence([
                    new RuleRef("A"),
                    new RuleRef("C")
                ]))
            ]));
        var definition = CreateDefinition(startRule, LexerRule("A", "a"), LexerRule("B", "b"), LexerRule("C", "c"));
        var parser = new ParserEngine(definition);

        var result = parser.Parse([Token("A", "a"), Token("B", "b")]);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual("start", result.Rule?.Name);
        Assert.IsFalse(result is ErrorNode);
    }

    [TestMethod]
    public void LookaheadProbe_RemainsConservative_ForParserRuleReference()
    {
        var parserRule = new Rule(
            "expr",
            1,
            false,
            new Alternation([new Alternative(0, Associativity.Left, new RuleRef("A"))]));
        var startRule = new Rule(
            "start",
            0,
            false,
            new Alternation([new Alternative(0, Associativity.Left, new RuleRef("expr"))]));
        var definition = CreateDefinition(startRule, [parserRule], LexerRule("A", "a"));
        var probe = new ParserLookaheadProbe();

        var result = probe.Probe(
            new Alternative(0, Associativity.Left, new RuleRef("expr")),
            Token("A", "a"),
            name => definition.ParserRules.FirstOrDefault(rule => string.Equals(rule.Name, name, StringComparison.Ordinal)),
            false);

        Assert.AreEqual(ParserLookaheadProbeKind.Unknown, result.Kind);
    }

    [TestMethod]
    public void ContinuationMetadata_StoredInRegistry_DoesNotCreateReusableParseOutcome()
    {
        var registry = new ParserStateRegistry();
        var invocation = new RuleInvocationKey("start", 0, 0);

        Assert.IsTrue(registry.AddContinuation(invocation, new ContinuationKey("start", 0, 0, 1, 0)));
        Assert.IsFalse(registry.TryGetReusableResult(invocation, out _));

        var startRule = new Rule(
            "start",
            0,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new RuleRef("A"))
            ]));
        var parser = new ParserEngine(CreateDefinition(startRule, LexerRule("A", "a")));

        var parseResult = parser.Parse([Token("A", "a")]);

        Assert.IsInstanceOfType<ParserNode>(parseResult);
    }

    [TestMethod]
    public void ContinuationMetadata_DoesNotAuthorizeReplayOrResumability()
    {
        var invocation = new RuleInvocationKey("expr", 0, 0);
        var continuation = new ContinuationKey("expr", 0, 0, 1, 0);
        var registry = new ParserStateRegistry();

        Assert.IsTrue(registry.AddContinuation(invocation, continuation));
        Assert.IsFalse(registry.TryGetReusableResult(invocation, out _));

        var continuations = registry.GetContinuations(invocation);
        Assert.AreEqual(1, continuations.Count);
        Assert.AreEqual(continuation, continuations[0]);
    }

    [TestMethod]
    public void ContinuationIdentity_DoesNotImplySemanticRuntimeEquivalence()
    {
        var continuation = new ContinuationKey("expr", 0, 1, 4, 0);
        var firstResult = new ParserRuleResult(new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE", "first"), 4, false);
        var secondResult = new ParserRuleResult(new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE", "second"), 4, false);

        Assert.AreEqual(continuation, new ContinuationKey("expr", 0, 1, 4, 0));
        Assert.AreNotEqual(((ErrorNode)firstResult.Node!).Message, ((ErrorNode)secondResult.Node!).Message);
    }

    [TestMethod]
    public void ActiveParseState_ContinuationMetadata_IsDescriptiveOnly()
    {
        var state = new ActiveParseState
        {
            Rule = new Rule("expr", 0, false, new Alternation([])),
            Alternative = new Alternative(0, Associativity.Left, new LiteralMatch("x")),
            OriginInputPosition = 0,
            CurrentInputPosition = 0,
            AlternativeIndex = 0,
            Cursor = new RuleContentCursor { Index = 0, Kind = ScheduledAlternativeCursorKinds.AlternativeRoot },
            PartialNode = new ErrorNode(new SourceSpan(0, 0), "DEFAULT_MODE", "metadata", null),
            EndPosition = null,
            Status = ActiveParseStateStatus.Active,
            ParentStateKey = null,
            Depth = 0,
            Continuation = new ContinuationKey("expr", 0, 0, 0, 0)
        };

        var advanced = state.Advance(1, new RuleContentCursor { Index = 1, Kind = ScheduledAlternativeCursorKinds.AlternativeRoot });

        Assert.AreEqual(0, state.CurrentInputPosition);
        Assert.AreEqual(1, advanced.CurrentInputPosition);
        Assert.AreEqual(0, state.Continuation?.ResumePosition);
        Assert.AreEqual(0, advanced.Continuation?.ResumePosition);
    }

    private static ParserDefinition CreateDefinition(Rule startRule, Rule[] parserRules, params Rule[] lexerRules)
    {
        return RuleResolver.Resolve(new ParserDefinition(
            Name: "G",
            Type: GrammarType.Combined,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [.. lexerRules])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [startRule, .. parserRules],
            RootRule: startRule));
    }

    private static ParserDefinition CreateDefinition(Rule startRule, params Rule[] lexerRules)
    {
        return CreateDefinition(startRule, [], lexerRules);
    }

    private static Rule LexerRule(string name, string literal)
    {
        return new Rule(name, 0, true, new Alternation([new Alternative(0, Associativity.Left, new LiteralMatch(literal))]));
    }

    private static Token Token(string ruleName, string text)
    {
        return new Token(new SourceSpan(0, text.Length), ruleName, "DEFAULT_MODE", "DEFAULT_CHANNEL", text);
    }

    private sealed class CountingPredicateEvaluator : ISemanticPredicateEvaluator
    {
        private readonly SemanticPredicateEvaluationResult _result;

        public CountingPredicateEvaluator(SemanticPredicateEvaluationResult result)
        {
            _result = result;
        }

        public int EvaluationCount { get; private set; }

        public SemanticPredicateEvaluationResult Evaluate(SemanticPredicateEvaluationContext context)
        {
            EvaluationCount++;
            return _result;
        }
    }

    private sealed class CountingActionExecutor : IParserActionExecutor
    {
        private readonly ParserActionExecutionResult _result;

        public CountingActionExecutor(ParserActionExecutionResult result)
        {
            _result = result;
        }

        public int ExecutionCount { get; private set; }

        public ParserActionExecutionResult Execute(ParserActionExecutionContext context)
        {
            ExecutionCount++;
            return _result;
        }
    }
}
