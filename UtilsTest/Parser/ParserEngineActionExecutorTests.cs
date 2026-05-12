using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Resolution;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies parser behavior with injected parser action executors.
/// </summary>
[TestClass]
public class ParserEngineActionExecutorTests
{
    [TestMethod]
    public void InlineAction_DefaultExecutor_PreservesCurrentBehavior()
    {
        var startRule = CreateStartRuleWithInlineAction("doSomething();");
        var tokenRuleA = CreateTokenRuleA();
        var definition = CreateDefinition(startRule, tokenRuleA);
        var parser = new ParserEngine(definition);
        var diagnostics = new DiagnosticBag();

        var result = parser.Parse([new Token(new SourceSpan(0, 1, 1, 1), "A", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")], diagnostics: diagnostics);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.InlineActionStoredNotExecuted.Code));
    }

    [TestMethod]
    public void InlineAction_CustomExecutor_IsInvoked()
    {
        var startRule = CreateStartRuleWithInlineAction("log();");
        var tokenRuleA = CreateTokenRuleA();
        var definition = CreateDefinition(startRule, tokenRuleA);
        var observer = new ObservingParserActionExecutor(ParserActionExecutionResult.NotExecuted);
        var parser = new ParserEngine(definition, new DefaultSemanticPredicateEvaluator(), observer);

        var result = parser.Parse([new Token(new SourceSpan(0, 1, 1, 1), "A", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")]);

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.AreEqual("log();", observer.LastContext?.ActionCode);
        Assert.AreEqual("start", observer.LastContext?.Rule.Name);
        Assert.AreEqual(0, observer.LastContext?.AlternativeIndex);
        Assert.AreEqual(0, observer.LastContext?.ElementIndex);
    }

    [TestMethod]
    public void InlineAction_CustomExecutor_DoesNotAlterParseBehavior()
    {
        var startRule = CreateStartRuleWithInlineAction("run();");
        var tokenRuleA = CreateTokenRuleA();
        var definition = CreateDefinition(startRule, tokenRuleA);
        var parser = new ParserEngine(definition, new DefaultSemanticPredicateEvaluator(), new ObservingParserActionExecutor(ParserActionExecutionResult.Executed));

        var result = parser.Parse([new Token(new SourceSpan(0, 1, 1, 1), "A", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")]);

        Assert.IsInstanceOfType<ParserNode>(result);
    }

    [TestMethod]
    public void CompileAndParse_FromAntlrText_UsesInjectedActionExecutor()
    {
        var observer = new ObservingParserActionExecutor(ParserActionExecutionResult.NotExecuted);
        var grammar = Antlr4GrammarConverter.Compile(
            """
            grammar P;
            start : { log(); } A ;
            A : 'a' ;
            """,
            new DefaultSemanticPredicateEvaluator(),
            observer);

        var result = grammar.Parse("a");

        Assert.IsInstanceOfType<ParserNode>(result);
        Assert.IsNotNull(observer.LastContext);
        Assert.AreEqual("log();", observer.LastContext.ActionCode.Trim());
    }

    private static Rule CreateTokenRuleA()
    {
        return new Rule("A", 0, true, new Alternation([new Alternative(0, Associativity.Left, new LiteralMatch("a"))]));
    }

    private static Rule CreateStartRuleWithInlineAction(string code)
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
                        new EmbeddedAction(code, ActionContext.Alternative, ActionPosition.Inline, []),
                        new RuleRef("A")
                    ]))
            ]));
    }

    private static ParserDefinition CreateDefinition(Rule startRule, Rule tokenRuleA)
    {
        return RuleResolver.Resolve(new ParserDefinition(
            Name: "G",
            Type: GrammarType.Combined,
            Options: null,
            Actions: [],
            Imports: [],
            Modes: [new LexerMode("DEFAULT_MODE", [tokenRuleA])],
            DeclaredTokens: new HashSet<string>(StringComparer.Ordinal),
            DeclaredChannels: new HashSet<string>(StringComparer.Ordinal) { "DEFAULT_CHANNEL", "HIDDEN" },
            ExtensionBindings: [],
            ParserRules: [startRule],
            RootRule: startRule));
    }

    /// <summary>
    /// Test executor that captures the latest action context for assertions.
    /// </summary>
    private sealed class ObservingParserActionExecutor : IParserActionExecutor
    {
        private readonly ParserActionExecutionResult _result;

        /// <summary>
        /// Initializes the executor.
        /// </summary>
        /// <param name="result">Result returned by <see cref="Execute"/>.</param>
        public ObservingParserActionExecutor(ParserActionExecutionResult result)
        {
            _result = result;
        }

        /// <summary>
        /// Gets the last context passed to <see cref="Execute"/>.
        /// </summary>
        public ParserActionExecutionContext? LastContext { get; private set; }

        /// <inheritdoc />
        public ParserActionExecutionResult Execute(ParserActionExecutionContext context)
        {
            LastContext = context;
            return _result;
        }
    }
}
