using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq.Expressions;
using Utils.Expressions;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Expressions;
using Utils.Parser.Model;
using Utils.Parser.Resolution;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Validates runtime inline parser action execution through <see cref="ExpressionParserActionExecutor"/>.
/// </summary>
[TestClass]
public class ExpressionParserActionExecutorTests
{
    [TestMethod]
    public void Execute_WhenVoidExpressionExecutes_ReturnsExecuted()
    {
        var compiler = new FakeExpressionCompiler();
        var executor = new ExpressionParserActionExecutor(compiler);

        var result = executor.Execute(CreateContext("increment"));

        Assert.AreEqual(ParserActionExecutionStatus.Executed, result.Status);
        Assert.AreEqual(1, compiler.Counter);
    }

    [TestMethod]
    public void Execute_WhenNonVoidExpressionExecutes_ReturnsExecutedAndDiscardsResult()
    {
        var compiler = new FakeExpressionCompiler();
        var executor = new ExpressionParserActionExecutor(compiler);

        var result = executor.Execute(CreateContext("next"));

        Assert.AreEqual(ParserActionExecutionStatus.Executed, result.Status);
        Assert.AreEqual(1, compiler.Counter);
    }

    [TestMethod]
    public void Execute_WhenCompilationThrows_ReturnsNotExecutedWithUp1026()
    {
        var executor = new ExpressionParserActionExecutor(new FakeExpressionCompiler());

        var result = executor.Execute(CreateContext("throw-compile"));

        Assert.AreEqual(ParserActionExecutionStatus.NotExecuted, result.Status);
        Assert.AreEqual(ParserDiagnostics.EmbeddedCodeCompilationFailed, result.Diagnostic);
        Assert.IsNotNull(result.Exception);
        CollectionAssert.AreEqual(new object?[] { "parser action", "boom" }, result.DiagnosticArguments.ToArray());
    }

    [TestMethod]
    public void Execute_WhenExecutionThrows_ReturnsNotExecutedWithUp1026()
    {
        var executor = new ExpressionParserActionExecutor(new FakeExpressionCompiler());

        var result = executor.Execute(CreateContext("throw-execute"));

        Assert.AreEqual(ParserActionExecutionStatus.NotExecuted, result.Status);
        Assert.AreEqual(ParserDiagnostics.EmbeddedCodeCompilationFailed, result.Diagnostic);
        Assert.IsNotNull(result.Exception);
        CollectionAssert.AreEqual(new object?[] { "parser action", "execution boom" }, result.DiagnosticArguments.ToArray());
    }

    [TestMethod]
    public void Execute_WhenContextSymbolIsUsed_ProvidesRuleName()
    {
        var compiler = new FakeExpressionCompiler();
        var executor = new ExpressionParserActionExecutor(compiler);

        var result = executor.Execute(CreateContext("record-rule ruleName", "start"));

        Assert.AreEqual(ParserActionExecutionStatus.Executed, result.Status);
        CollectionAssert.AreEqual(new List<string> { "start" }, compiler.RecordedRules);
    }

    [TestMethod]
    public void Execute_WhenActionIsNonContextual_ReusesCompilationAndReExecutes()
    {
        var compiler = new FakeExpressionCompiler();
        var executor = new ExpressionParserActionExecutor(compiler);

        _ = executor.Execute(CreateContext("increment"));
        _ = executor.Execute(CreateContext("increment"));

        Assert.AreEqual(1, compiler.CompileCount);
        Assert.AreEqual(2, compiler.Counter);
    }

    [TestMethod]
    public void Execute_WhenActionUsesContextSymbol_DoesNotReuseCapturedFirstContext()
    {
        var compiler = new FakeExpressionCompiler();
        var executor = new ExpressionParserActionExecutor(compiler);

        _ = executor.Execute(CreateContext("record-rule ruleName", "start"));
        _ = executor.Execute(CreateContext("record-rule ruleName", "other"));

        CollectionAssert.AreEqual(new List<string> { "start", "other" }, compiler.RecordedRules);
    }

    [TestMethod]
    public void Parse_WhenExecutorReturnsDetailedNotExecuted_EmitsUp1026WithoutUp1005()
    {
        var compiler = new FakeExpressionCompiler();
        var executor = new ExpressionParserActionExecutor(compiler);
        var definition = CreateDefinition(CreateStartRuleWithInlineAction("throw-compile"), CreateTokenRuleA());
        var parser = new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default with { ParserActionExecutor = executor });
        var diagnostics = new DiagnosticBag();

        _ = parser.Parse([new Token(new SourceSpan(0, 1, 1, 1), "A", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")], diagnostics: diagnostics);

        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.EmbeddedCodeCompilationFailed.Code));
        Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.InlineActionStoredNotExecuted.Code));
    }

    [TestMethod]
    public void Parse_WhenActionExecutes_DoesNotEmitUp1005()
    {
        var compiler = new FakeExpressionCompiler();
        var executor = new ExpressionParserActionExecutor(compiler);
        var definition = CreateDefinition(CreateStartRuleWithInlineAction("increment"), CreateTokenRuleA());
        var parser = new ParserEngine(definition, ParserRuntimeFeaturePolicy.Default with { ParserActionExecutor = executor });
        var diagnostics = new DiagnosticBag();

        _ = parser.Parse([new Token(new SourceSpan(0, 1, 1, 1), "A", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a")], diagnostics: diagnostics);

        Assert.AreEqual(1, compiler.Counter);
        Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.InlineActionStoredNotExecuted.Code));
    }

    private static ParserActionExecutionContext CreateContext(string actionCode, string ruleName = "start")
    {
        var rule = new Rule(
            ruleName,
            0,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new Sequence([]))
            ]));

        return new ParserActionExecutionContext(
            Rule: rule,
            Action: new EmbeddedAction(actionCode, ActionContext.Alternative, ActionPosition.Inline, []),
            ActionCode: actionCode,
            InputPosition: 0,
            AlternativeIndex: 0,
            ElementIndex: 0);
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

    private sealed class FakeExpressionCompiler : IExpressionCompiler
    {
        public int CompileCount { get; private set; }

        public int Counter { get; private set; }

        public List<string> RecordedRules { get; } = [];

        public Expression Compile(string content, IReadOnlyDictionary<string, Expression>? symbols = null)
        {
            CompileCount++;
            return content switch
            {
                "increment" => Expression.Call(Expression.Constant(this), nameof(Increment), Type.EmptyTypes),
                "next" => Expression.Call(Expression.Constant(this), nameof(NextValue), Type.EmptyTypes),
                "record-rule" or "record-rule ruleName" => Expression.Call(Expression.Constant(this), nameof(RecordRule), Type.EmptyTypes, symbols!["ruleName"]),
                "throw-compile" => throw new InvalidOperationException("boom"),
                "throw-execute" => Expression.Call(Expression.Constant(this), nameof(ThrowAtExecution), Type.EmptyTypes),
                _ => Expression.Empty()
            };
        }

        public void Increment() => Counter++;

        public int NextValue()
        {
            Counter++;
            return Counter;
        }

        public void RecordRule(string ruleName) => RecordedRules.Add(ruleName);

        public void ThrowAtExecution() => throw new InvalidOperationException("execution boom");
    }
}
