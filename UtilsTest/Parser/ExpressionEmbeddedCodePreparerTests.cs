using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq.Expressions;
using Utils.Expressions;
using Utils.Parser.Diagnostics.EmbeddedCode;
using Utils.Parser.Diagnostics;
using Utils.Parser.EmbeddedCode;
using Utils.Parser.Expressions;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Validates expression-backed embedded-code preparation without parser runtime integration.
/// </summary>
[TestClass]
public class ExpressionEmbeddedCodePreparerTests
{
    [TestMethod]
    public void Constructor_WhenCompilerIsNull_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new ExpressionEmbeddedCodePreparer(null!));
    }

    [TestMethod]
    public void PrepareSemanticPredicate_WhenPredicateIsBoolean_Succeeds()
    {
        var preparer = new ExpressionEmbeddedCodePreparer(new FakeExpressionCompiler());

        var result = preparer.PrepareSemanticPredicate(
            CreateSource("true", EmbeddedCodeKind.SemanticPredicate),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression));

        Assert.AreEqual(EmbeddedCodePreparationStatus.Succeeded, result.Status);
        Assert.IsNotNull(result.Artifact);
        Assert.AreEqual(SemanticPredicateEvaluationStatus.Satisfied, result.Artifact.Evaluate(CreatePredicateContext("true")).Status);
    }

    [TestMethod]
    public void PrepareSemanticPredicate_WhenPredicateIsNotBoolean_ReturnsCompilationFailed()
    {
        var preparer = new ExpressionEmbeddedCodePreparer(new FakeExpressionCompiler());

        var result = preparer.PrepareSemanticPredicate(
            CreateSource("42", EmbeddedCodeKind.SemanticPredicate),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression));

        Assert.AreEqual(EmbeddedCodePreparationStatus.CompilationFailed, result.Status);
        Assert.AreSame(ParserDiagnostics.EmbeddedCodeCompilationFailed, result.DiagnosticDescriptor);
        Assert.IsNotNull(result.Exception);
        StringAssert.Contains(result.Exception.Message, "Expected Boolean result");
    }


    [TestMethod]
    public void PrepareParserAction_WhenTransformerProvided_CompilerReceivesTransformedCode()
    {
        var compiler = new FakeExpressionCompiler();
        var preparer = new ExpressionEmbeddedCodePreparer(compiler, new ReplaceRuntimeCodeTransformer());

        var result = preparer.PrepareParserAction(
            CreateSource("__TOKEN__", EmbeddedCodeKind.ParserInlineAction),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression));

        Assert.AreEqual(EmbeddedCodePreparationStatus.Succeeded, result.Status);
        Assert.AreEqual("increment", compiler.LastContent);
    }

    [TestMethod]
    public void PrepareParserAction_WhenTransformerReportsError_DoesNotInvokeCompiler()
    {
        var compiler = new FakeExpressionCompiler();
        var preparer = new ExpressionEmbeddedCodePreparer(compiler, new ErrorRuntimeCodeTransformer());

        var result = preparer.PrepareParserAction(
            CreateSource("increment", EmbeddedCodeKind.ParserInlineAction),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression));

        Assert.AreEqual(EmbeddedCodePreparationStatus.CompilationFailed, result.Status);
        Assert.AreEqual(0, compiler.CompileCount);
        Assert.IsInstanceOfType(result.Exception, typeof(ParserEmbeddedCodeTransformationException));
    }

    [TestMethod]
    public void PrepareParserAction_WhenActionIsPrepared_DoesNotExecuteDuringPreparation()
    {
        var compiler = new FakeExpressionCompiler();
        var preparer = new ExpressionEmbeddedCodePreparer(compiler);

        var result = preparer.PrepareParserAction(
            CreateSource("increment", EmbeddedCodeKind.ParserInlineAction),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression));

        Assert.AreEqual(EmbeddedCodePreparationStatus.Succeeded, result.Status);
        Assert.AreEqual(0, compiler.Counter);
        Assert.IsNotNull(result.Artifact);

        var executionResult = result.Artifact.Execute(CreateActionContext("increment"));

        Assert.AreEqual(ParserActionExecutionStatus.Executed, executionResult.Status);
        Assert.AreEqual(1, compiler.Counter);
    }

    [DataTestMethod]
    [DataRow(EmbeddedCodeKind.RuleInitAction)]
    [DataRow(EmbeddedCodeKind.RuleAfterAction)]
    [DataRow(EmbeddedCodeKind.GrammarAction)]
    public void PrepareSemanticPredicate_WhenKindIsUnsupported_ReturnsUnsupported(EmbeddedCodeKind kind)
    {
        var preparer = new ExpressionEmbeddedCodePreparer(new FakeExpressionCompiler());

        var result = preparer.PrepareSemanticPredicate(
            CreateSource("true", kind),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression));

        Assert.AreEqual(EmbeddedCodePreparationStatus.Unsupported, result.Status);
        Assert.AreSame(ParserDiagnostics.EmbeddedCodeLanguageUnsupported, result.DiagnosticDescriptor);
    }

    [DataTestMethod]
    [DataRow(EmbeddedCodeKind.RuleInitAction)]
    [DataRow(EmbeddedCodeKind.RuleAfterAction)]
    [DataRow(EmbeddedCodeKind.GrammarAction)]
    public void PrepareParserAction_WhenKindIsUnsupported_ReturnsUnsupported(EmbeddedCodeKind kind)
    {
        var preparer = new ExpressionEmbeddedCodePreparer(new FakeExpressionCompiler());

        var result = preparer.PrepareParserAction(
            CreateSource("increment", kind),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression));

        Assert.AreEqual(EmbeddedCodePreparationStatus.Unsupported, result.Status);
        Assert.AreSame(ParserDiagnostics.EmbeddedCodeLanguageUnsupported, result.DiagnosticDescriptor);
    }

    [TestMethod]
    public void PrepareSemanticPredicate_WhenTargetIsSourceGeneratorCSharp_ReturnsPreservedNotCompiled()
    {
        var preparer = new ExpressionEmbeddedCodePreparer(new FakeExpressionCompiler());

        var result = preparer.PrepareSemanticPredicate(
            CreateSource("true", EmbeddedCodeKind.SemanticPredicate),
            CreateContext(EmbeddedCodeTarget.SourceGeneratorCSharp));

        Assert.AreEqual(EmbeddedCodePreparationStatus.PreservedNotCompiled, result.Status);
        Assert.AreSame(ParserDiagnostics.EmbeddedCodePreservedNotCompiled, result.DiagnosticDescriptor);
    }

    [TestMethod]
    public void PrepareParserAction_WhenTargetIsSourceGeneratorCSharp_ReturnsPreservedNotCompiled()
    {
        var preparer = new ExpressionEmbeddedCodePreparer(new FakeExpressionCompiler());

        var result = preparer.PrepareParserAction(
            CreateSource("increment", EmbeddedCodeKind.ParserInlineAction),
            CreateContext(EmbeddedCodeTarget.SourceGeneratorCSharp));

        Assert.AreEqual(EmbeddedCodePreparationStatus.PreservedNotCompiled, result.Status);
        Assert.AreSame(ParserDiagnostics.EmbeddedCodePreservedNotCompiled, result.DiagnosticDescriptor);
    }

    [TestMethod]
    public void PrepareSemanticPredicate_WhenCompilationThrows_ReturnsCompilationFailedWithException()
    {
        var preparer = new ExpressionEmbeddedCodePreparer(new FakeExpressionCompiler());

        var result = preparer.PrepareSemanticPredicate(
            CreateSource("throw-compile", EmbeddedCodeKind.SemanticPredicate),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression));

        Assert.AreEqual(EmbeddedCodePreparationStatus.CompilationFailed, result.Status);
        Assert.AreSame(ParserDiagnostics.EmbeddedCodeCompilationFailed, result.DiagnosticDescriptor);
        Assert.IsInstanceOfType(result.Exception, typeof(InvalidOperationException));
    }

    [TestMethod]
    public void PreparedSemanticPredicate_WhenContextSymbolsAreUsed_ReadsCurrentRuntimeContext()
    {
        var preparer = new ExpressionEmbeddedCodePreparer(new FakeExpressionCompiler());

        var result = preparer.PrepareSemanticPredicate(
            CreateSource("ruleName == target", EmbeddedCodeKind.SemanticPredicate),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression));

        Assert.AreEqual(EmbeddedCodePreparationStatus.Succeeded, result.Status);
        Assert.IsNotNull(result.Artifact);

        var firstResult = result.Artifact.Evaluate(CreatePredicateContext("ruleName == target", "start"));
        var secondResult = result.Artifact.Evaluate(CreatePredicateContext("ruleName == target", "other"));

        Assert.AreEqual(SemanticPredicateEvaluationStatus.Satisfied, firstResult.Status);
        Assert.AreEqual(SemanticPredicateEvaluationStatus.Rejected, secondResult.Status);
    }

    [TestMethod]
    public void PreparedParserAction_WhenContextSymbolsAreUsed_ReadsCurrentRuntimeContext()
    {
        var compiler = new FakeExpressionCompiler();
        var preparer = new ExpressionEmbeddedCodePreparer(compiler);

        var result = preparer.PrepareParserAction(
            CreateSource("record-rule ruleName", EmbeddedCodeKind.ParserInlineAction),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression));

        Assert.AreEqual(EmbeddedCodePreparationStatus.Succeeded, result.Status);
        Assert.IsNotNull(result.Artifact);

        _ = result.Artifact.Execute(CreateActionContext("record-rule ruleName", "start"));
        _ = result.Artifact.Execute(CreateActionContext("record-rule ruleName", "other"));

        CollectionAssert.AreEqual(new List<string> { "start", "other" }, compiler.RecordedRules);
    }

    [TestMethod]
    public void PrepareSemanticPredicate_WhenOnlyInputPositionIsSupported_AllowsInputPositionExpression()
    {
        var preparer = new ExpressionEmbeddedCodePreparer(new FakeExpressionCompiler());

        var result = preparer.PrepareSemanticPredicate(
            CreateSource("inputPosition >= 0", EmbeddedCodeKind.SemanticPredicate),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression, new HashSet<EmbeddedCodeContextSymbol> { EmbeddedCodeContextSymbol.InputPosition }));

        Assert.AreEqual(EmbeddedCodePreparationStatus.Succeeded, result.Status);
        Assert.IsNotNull(result.Artifact);
        Assert.AreEqual(SemanticPredicateEvaluationStatus.Satisfied, result.Artifact.Evaluate(CreatePredicateContext("inputPosition >= 0", inputPosition: 3)).Status);
    }

    [TestMethod]
    public void PrepareSemanticPredicate_WhenRuleNameIsNotSupported_ReturnsCompilationFailed()
    {
        var preparer = new ExpressionEmbeddedCodePreparer(new FakeExpressionCompiler());

        var result = preparer.PrepareSemanticPredicate(
            CreateSource("ruleName == target", EmbeddedCodeKind.SemanticPredicate),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression, new HashSet<EmbeddedCodeContextSymbol> { EmbeddedCodeContextSymbol.InputPosition }));

        Assert.AreEqual(EmbeddedCodePreparationStatus.CompilationFailed, result.Status);
        Assert.AreSame(ParserDiagnostics.EmbeddedCodeCompilationFailed, result.DiagnosticDescriptor);
        Assert.IsNotNull(result.Exception);
    }

    [TestMethod]
    public void PrepareParserAction_WhenOnlyInputPositionIsSupported_AllowsInputPositionAction()
    {
        var compiler = new FakeExpressionCompiler();
        var preparer = new ExpressionEmbeddedCodePreparer(compiler);

        var result = preparer.PrepareParserAction(
            CreateSource("record-position inputPosition", EmbeddedCodeKind.ParserInlineAction),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression, new HashSet<EmbeddedCodeContextSymbol> { EmbeddedCodeContextSymbol.InputPosition }));

        Assert.AreEqual(EmbeddedCodePreparationStatus.Succeeded, result.Status);
        Assert.IsNotNull(result.Artifact);

        _ = result.Artifact.Execute(CreateActionContext("record-position inputPosition", inputPosition: 7));

        CollectionAssert.AreEqual(new List<int> { 7 }, compiler.RecordedPositions);
    }

    [TestMethod]
    public void PrepareParserAction_WhenRuleNameIsNotSupported_ReturnsCompilationFailed()
    {
        var preparer = new ExpressionEmbeddedCodePreparer(new FakeExpressionCompiler());

        var result = preparer.PrepareParserAction(
            CreateSource("record-rule ruleName", EmbeddedCodeKind.ParserInlineAction),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression, new HashSet<EmbeddedCodeContextSymbol> { EmbeddedCodeContextSymbol.InputPosition }));

        Assert.AreEqual(EmbeddedCodePreparationStatus.CompilationFailed, result.Status);
        Assert.AreSame(ParserDiagnostics.EmbeddedCodeCompilationFailed, result.DiagnosticDescriptor);
        Assert.IsNotNull(result.Exception);
    }

    /// <summary>
    /// Creates source metadata for a test embedded-code construct.
    /// </summary>
    /// <param name="sourceText">Source text to expose through the metadata.</param>
    /// <param name="kind">Embedded-code construct kind.</param>
    /// <returns>A source metadata instance.</returns>
    private static EmbeddedCodeSource CreateSource(string sourceText, EmbeddedCodeKind kind) =>
        new(sourceText, kind, ruleName: "start", alternativeIndex: 0, elementIndex: 0);

    /// <summary>
    /// Creates a preparation context for a test target.
    /// </summary>
    /// <param name="target">Preparation target under test.</param>
    /// <param name="supportedSymbols">Optional limited symbol set to expose during preparation.</param>
    /// <returns>A preparation context instance.</returns>
    private static EmbeddedCodePreparationContext CreateContext(
        EmbeddedCodeTarget target,
        IReadOnlySet<EmbeddedCodeContextSymbol>? supportedSymbols = null) =>
        new("G", target, ruleName: "start", languageOrCompilerIdentity: "fake", supportedSymbols: supportedSymbols);

    /// <summary>
    /// Creates a semantic predicate runtime context without invoking <see cref="ParserEngine"/>.
    /// </summary>
    /// <param name="predicateCode">Predicate source code stored in the context.</param>
    /// <param name="ruleName">Rule name exposed to contextual expressions.</param>
    /// <param name="inputPosition">Input position exposed to contextual expressions.</param>
    /// <returns>A semantic predicate runtime context.</returns>
    private static SemanticPredicateEvaluationContext CreatePredicateContext(
        string predicateCode,
        string ruleName = "start",
        int inputPosition = 0)
    {
        var rule = CreateRule(ruleName);
        return new SemanticPredicateEvaluationContext(
            Rule: rule,
            Predicate: new ValidatingPredicate(predicateCode),
            PredicateCode: predicateCode,
            InputPosition: inputPosition,
            AlternativeIndex: 0,
            ElementIndex: 0);
    }

    /// <summary>
    /// Creates a parser action runtime context without invoking <see cref="ParserEngine"/>.
    /// </summary>
    /// <param name="actionCode">Action source code stored in the context.</param>
    /// <param name="ruleName">Rule name exposed to contextual expressions.</param>
    /// <param name="inputPosition">Input position exposed to contextual expressions.</param>
    /// <returns>A parser action runtime context.</returns>
    private static ParserActionExecutionContext CreateActionContext(
        string actionCode,
        string ruleName = "start",
        int inputPosition = 0)
    {
        var rule = CreateRule(ruleName);
        return new ParserActionExecutionContext(
            Rule: rule,
            Action: new EmbeddedAction(actionCode, ActionContext.Alternative, ActionPosition.Inline, []),
            ActionCode: actionCode,
            InputPosition: inputPosition,
            AlternativeIndex: 0,
            ElementIndex: 0);
    }

    /// <summary>
    /// Creates a minimal parser rule for runtime context tests.
    /// </summary>
    /// <param name="ruleName">Rule name to expose through the context.</param>
    /// <returns>A minimal parser rule.</returns>
    private static Rule CreateRule(string ruleName) =>
        new(
            ruleName,
            0,
            false,
            new Alternation([
                new Alternative(0, Associativity.Left, new Sequence([]))
            ]));

    /// <summary>
    /// Minimal expression compiler used to drive deterministic preparer behavior.
    /// </summary>
    private sealed class FakeExpressionCompiler : IExpressionCompiler
    {
        /// <summary>
        /// Gets the number of compile calls observed by the fake compiler.
        /// </summary>
        public int CompileCount { get; private set; }

        /// <summary>Gets the last content passed to the fake compiler.</summary>
        public string? LastContent { get; private set; }

        /// <summary>
        /// Gets the number of action executions observed by generated action delegates.
        /// </summary>
        public int Counter { get; private set; }

        /// <summary>
        /// Gets rule names recorded by generated action delegates.
        /// </summary>
        public List<string> RecordedRules { get; } = [];

        /// <summary>
        /// Gets input positions recorded by generated action delegates.
        /// </summary>
        public List<int> RecordedPositions { get; } = [];

        /// <inheritdoc />
        public Expression Compile(string content, IReadOnlyDictionary<string, Expression>? symbols = null)
        {
            CompileCount++;
            LastContent = content;
            return content switch
            {
                "true" => Expression.Constant(true),
                "42" => Expression.Constant(42),
                "increment" => Expression.Call(Expression.Constant(this), nameof(Increment), Type.EmptyTypes),
                "record-rule ruleName" => Expression.Call(Expression.Constant(this), nameof(RecordRule), Type.EmptyTypes, symbols!["ruleName"]),
                "record-position inputPosition" => Expression.Call(Expression.Constant(this), nameof(RecordPosition), Type.EmptyTypes, symbols!["inputPosition"]),
                "ruleName == target" => Expression.Equal(symbols!["ruleName"], Expression.Constant("start")),
                "inputPosition >= 0" => Expression.GreaterThanOrEqual(symbols!["inputPosition"], Expression.Constant(0)),
                "throw-compile" => throw new InvalidOperationException("boom"),
                _ => Expression.Empty()
            };
        }

        /// <summary>
        /// Increments the execution counter.
        /// </summary>
        public void Increment() => Counter++;

        /// <summary>
        /// Records a rule name supplied through a runtime context symbol.
        /// </summary>
        /// <param name="ruleName">Rule name to record.</param>
        public void RecordRule(string ruleName) => RecordedRules.Add(ruleName);

        /// <summary>
        /// Records an input position supplied through a runtime context symbol.
        /// </summary>
        /// <param name="inputPosition">Input position to record.</param>
        public void RecordPosition(int inputPosition) => RecordedPositions.Add(inputPosition);
    }

    /// <summary>
    /// Test transformer that rewrites a sentinel into the fake compiler's action expression.
    /// </summary>
    private sealed class ReplaceRuntimeCodeTransformer : IParserEmbeddedCodeTransformer
    {
        /// <inheritdoc />
        public ParserEmbeddedCodeTransformationResult Transform(ParserEmbeddedCodeTransformationContext context)
        {
            return new ParserEmbeddedCodeTransformationResult { Code = context.Code.Replace("__TOKEN__", "increment") };
        }
    }

    /// <summary>
    /// Test transformer that blocks compilation with a deterministic error.
    /// </summary>
    private sealed class ErrorRuntimeCodeTransformer : IParserEmbeddedCodeTransformer
    {
        /// <inheritdoc />
        public ParserEmbeddedCodeTransformationResult Transform(ParserEmbeddedCodeTransformationContext context)
        {
            return new ParserEmbeddedCodeTransformationResult
            {
                Code = context.Code,
                Diagnostics = [new ParserEmbeddedCodeDiagnostic { Message = "blocked", Severity = ParserEmbeddedCodeDiagnosticSeverity.Error }]
            };
        }
    }

}
