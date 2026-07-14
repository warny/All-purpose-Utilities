using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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
    public void EmbeddedCodeSource_WhenCreated_ExposesTypedRawCode()
    {
        var source = CreateSource("true", EmbeddedCodeKind.SemanticPredicate);

        Assert.AreEqual("true", source.RawCode.Text);
        Assert.IsInstanceOfType(source.RawCode, typeof(RawEmbeddedCode));
    }

    [TestMethod]
    public void PrepareSemanticPredicate_WhenTransformerProvided_CompilerReceivesTransformedCode()
    {
        var compiler = new FakeExpressionCompiler();
        var preparer = new ExpressionEmbeddedCodePreparer(compiler, new ReplaceRuntimeCodeTransformer());

        var result = preparer.PrepareSemanticPredicate(
            CreateSource("__TOKEN_PREDICATE__", EmbeddedCodeKind.SemanticPredicate),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression));

        Assert.AreEqual(EmbeddedCodePreparationStatus.Succeeded, result.Status);
        Assert.AreEqual("true", compiler.LastContent);
        Assert.AreNotEqual("__TOKEN_PREDICATE__", compiler.LastContent);
    }

    [TestMethod]
    public void PrepareParserAction_WhenNoOpTransformerUsed_CompilerReceivesTextuallyIdenticalTransformedCode()
    {
        var compiler = new FakeExpressionCompiler();
        var preparer = new ExpressionEmbeddedCodePreparer(compiler, NoOpParserEmbeddedCodeTransformer.Instance);

        var result = preparer.PrepareParserAction(
            CreateSource("increment", EmbeddedCodeKind.ParserInlineAction),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression));

        Assert.AreEqual(EmbeddedCodePreparationStatus.Succeeded, result.Status);
        Assert.AreEqual("increment", compiler.LastContent);
    }


    [TestMethod]
    public void TransformedEmbeddedCode_DoesNotExposePublicConstructorsOrManualResultConversion()
    {
        ConstructorInfo[] constructors = typeof(TransformedEmbeddedCode).GetConstructors();
        MethodInfo[] conversionMethods = typeof(ParserEmbeddedCodeTransformationService)
            .Assembly
            .GetTypes()
            .Where(static type => type.IsAbstract && type.IsSealed)
            .SelectMany(static type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(static method => method.ReturnType == typeof(TransformedEmbeddedCode))
            .ToArray();

        Assert.AreEqual(0, constructors.Length);
        CollectionAssert.AreEqual(
            new[] { nameof(ParserEmbeddedCodeTransformationService.TransformOrThrow) },
            conversionMethods.Select(static method => method.Name).Distinct().ToArray());
    }

    [TestMethod]
    public void ParserEmbeddedCodeTransformationService_WhenRawCodeIsNull_DoesNotInvokeTransformer()
    {
        var transformer = new CountingTransformer();

        Assert.ThrowsException<ArgumentNullException>(() => ParserEmbeddedCodeTransformationService.TransformOrThrow(
            transformer,
            null!,
            new ParserEmbeddedCodeTransformationContext { Location = ParserEmbeddedCodeLocation.InlineAction },
            new ParserEmbeddedCodeTransformationFailureContext { Location = ParserEmbeddedCodeLocation.InlineAction }));
        Assert.AreEqual(0, transformer.Count);
    }


    [TestMethod]
    public void ParserEmbeddedCodeTransformationService_WhenTransformerReturnsWarning_AllowsTransformationAndPreservesDiagnostic()
    {
        var transformer = new ConfigurableTransformer
        {
            Result = new ParserEmbeddedCodeTransformationResult
            {
                Code = "transformed",
                Diagnostics = [new ParserEmbeddedCodeDiagnostic { Severity = ParserEmbeddedCodeDiagnosticSeverity.Warning, Message = "warning" }]
            }
        };

        TransformedEmbeddedCode result = ParserEmbeddedCodeTransformationService.TransformOrThrow(
            transformer,
            new RawEmbeddedCode("raw"),
            new ParserEmbeddedCodeTransformationContext { Location = ParserEmbeddedCodeLocation.InlineAction },
            new ParserEmbeddedCodeTransformationFailureContext { Location = ParserEmbeddedCodeLocation.InlineAction });

        Assert.AreEqual("transformed", result.Text);
        Assert.AreEqual(1, result.Diagnostics.Count);
        Assert.AreEqual(ParserEmbeddedCodeDiagnosticSeverity.Warning, result.Diagnostics[0].Severity);
        Assert.AreEqual(1, transformer.Count);
    }

    [TestMethod]
    public void ParserEmbeddedCodeTransformationService_WhenTransformerReturnsNullResult_ThrowsDeterministicException()
    {
        var transformer = new ConfigurableTransformer { Result = null };

        var exception = Assert.ThrowsException<Utils.Parser.Diagnostics.EmbeddedCode.ParserEmbeddedCodeTransformationException>(() => ParserEmbeddedCodeTransformationService.TransformOrThrow(
            transformer,
            new RawEmbeddedCode("raw"),
            new ParserEmbeddedCodeTransformationContext { Location = ParserEmbeddedCodeLocation.SemanticPredicate },
            new ParserEmbeddedCodeTransformationFailureContext { Location = ParserEmbeddedCodeLocation.SemanticPredicate }));

        Assert.AreEqual("Embedded-code transformer returned null.", exception.Message);
        Assert.AreEqual(1, transformer.Count);
    }

    [TestMethod]
    public void ParserEmbeddedCodeTransformationService_WhenTransformerReturnsNullCode_ThrowsDeterministicException()
    {
        var transformer = new ConfigurableTransformer { Result = new ParserEmbeddedCodeTransformationResult { Code = null! } };

        var exception = Assert.ThrowsException<Utils.Parser.Diagnostics.EmbeddedCode.ParserEmbeddedCodeTransformationException>(() => ParserEmbeddedCodeTransformationService.TransformOrThrow(
            transformer,
            new RawEmbeddedCode("raw"),
            new ParserEmbeddedCodeTransformationContext { Location = ParserEmbeddedCodeLocation.SemanticPredicate },
            new ParserEmbeddedCodeTransformationFailureContext { Location = ParserEmbeddedCodeLocation.SemanticPredicate }));

        Assert.AreEqual("Embedded-code transformer returned null code.", exception.Message);
    }

    [TestMethod]
    public void ParserEmbeddedCodeTransformationService_WhenDiagnosticsAreNull_TreatsDiagnosticsAsEmpty()
    {
        var transformer = new ConfigurableTransformer { Result = new ParserEmbeddedCodeTransformationResult { Code = "ok", Diagnostics = null! } };

        TransformedEmbeddedCode result = ParserEmbeddedCodeTransformationService.TransformOrThrow(
            transformer,
            new RawEmbeddedCode("raw"),
            new ParserEmbeddedCodeTransformationContext { Location = ParserEmbeddedCodeLocation.SemanticPredicate },
            new ParserEmbeddedCodeTransformationFailureContext { Location = ParserEmbeddedCodeLocation.SemanticPredicate });

        Assert.AreEqual("ok", result.Text);
        Assert.AreEqual(0, result.Diagnostics.Count);
    }

    [TestMethod]
    public void ParserEmbeddedCodeTransformationService_WhenTransformerThrows_PreservesInnerException()
    {
        var inner = new FormatException("boom");
        var transformer = new ConfigurableTransformer { Exception = inner };

        var exception = Assert.ThrowsException<Utils.Parser.Diagnostics.EmbeddedCode.ParserEmbeddedCodeTransformationException>(() => ParserEmbeddedCodeTransformationService.TransformOrThrow(
            transformer,
            new RawEmbeddedCode("raw"),
            new ParserEmbeddedCodeTransformationContext { Location = ParserEmbeddedCodeLocation.InlineAction },
            new ParserEmbeddedCodeTransformationFailureContext { Path = ParserEmbeddedCodeTransformationPath.RuntimeCompilation, Location = ParserEmbeddedCodeLocation.InlineAction }));

        Assert.AreSame(inner, exception.InnerException);
        Assert.AreEqual(ParserEmbeddedCodeTransformationPath.RuntimeCompilation, exception.Path);
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
        Assert.IsInstanceOfType(result.Exception, typeof(Utils.Parser.Expressions.ParserEmbeddedCodeTransformationException));
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


    [DataTestMethod]
    [DataRow(nameof(ExpressionEmbeddedCodePreparer.PrepareSemanticPredicate), EmbeddedCodeKind.SemanticPredicate, typeof(SemanticPredicateEvaluationContext))]
    [DataRow(nameof(ExpressionEmbeddedCodePreparer.PrepareParserAction), EmbeddedCodeKind.ParserInlineAction, typeof(ParserActionExecutionContext))]
    public void PrepareRuntimeArtifact_WhenNoSymbolsAreSupported_PassesEmptySymbolDictionary(string prepareMethodName, EmbeddedCodeKind kind, Type runtimeContextType)
    {
        var compiler = new FakeExpressionCompiler();
        var preparer = new ExpressionEmbeddedCodePreparer(compiler);

        InvokePrepare(preparer, prepareMethodName, CreateSource("true", kind), CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression, new HashSet<EmbeddedCodeContextSymbol>()));

        Assert.AreEqual(1, compiler.CompileCount);
        Assert.IsNotNull(compiler.LastSymbols);
        Assert.AreEqual(0, compiler.LastSymbols.Count);
    }

    [DataTestMethod]
    [DataRow(nameof(ExpressionEmbeddedCodePreparer.PrepareSemanticPredicate), EmbeddedCodeKind.SemanticPredicate, typeof(SemanticPredicateEvaluationContext), EmbeddedCodeContextSymbol.RuleName, "ruleName", typeof(string), "Rule.Name")]
    [DataRow(nameof(ExpressionEmbeddedCodePreparer.PrepareSemanticPredicate), EmbeddedCodeKind.SemanticPredicate, typeof(SemanticPredicateEvaluationContext), EmbeddedCodeContextSymbol.InputPosition, "inputPosition", typeof(int), "InputPosition")]
    [DataRow(nameof(ExpressionEmbeddedCodePreparer.PrepareSemanticPredicate), EmbeddedCodeKind.SemanticPredicate, typeof(SemanticPredicateEvaluationContext), EmbeddedCodeContextSymbol.AlternativeIndex, "alternativeIndex", typeof(int), "AlternativeIndex")]
    [DataRow(nameof(ExpressionEmbeddedCodePreparer.PrepareSemanticPredicate), EmbeddedCodeKind.SemanticPredicate, typeof(SemanticPredicateEvaluationContext), EmbeddedCodeContextSymbol.ElementIndex, "elementIndex", typeof(int), "ElementIndex")]
    [DataRow(nameof(ExpressionEmbeddedCodePreparer.PrepareParserAction), EmbeddedCodeKind.ParserInlineAction, typeof(ParserActionExecutionContext), EmbeddedCodeContextSymbol.RuleName, "ruleName", typeof(string), "Rule.Name")]
    [DataRow(nameof(ExpressionEmbeddedCodePreparer.PrepareParserAction), EmbeddedCodeKind.ParserInlineAction, typeof(ParserActionExecutionContext), EmbeddedCodeContextSymbol.InputPosition, "inputPosition", typeof(int), "InputPosition")]
    [DataRow(nameof(ExpressionEmbeddedCodePreparer.PrepareParserAction), EmbeddedCodeKind.ParserInlineAction, typeof(ParserActionExecutionContext), EmbeddedCodeContextSymbol.AlternativeIndex, "alternativeIndex", typeof(int), "AlternativeIndex")]
    [DataRow(nameof(ExpressionEmbeddedCodePreparer.PrepareParserAction), EmbeddedCodeKind.ParserInlineAction, typeof(ParserActionExecutionContext), EmbeddedCodeContextSymbol.ElementIndex, "elementIndex", typeof(int), "ElementIndex")]
    public void PrepareRuntimeArtifact_WhenSingleSymbolIsSupported_PassesOnlyExpectedRuntimeMember(
        string prepareMethodName,
        EmbeddedCodeKind kind,
        Type runtimeContextType,
        EmbeddedCodeContextSymbol symbol,
        string expectedName,
        Type expectedType,
        string expectedPath)
    {
        var compiler = new FakeExpressionCompiler();
        var preparer = new ExpressionEmbeddedCodePreparer(compiler);

        InvokePrepare(preparer, prepareMethodName, CreateSource("true", kind), CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression, new HashSet<EmbeddedCodeContextSymbol> { symbol }));

        Assert.IsNotNull(compiler.LastSymbols);
        Assert.AreEqual(1, compiler.LastSymbols.Count);
        Assert.IsTrue(compiler.LastSymbols.ContainsKey(expectedName));
        AssertSymbolExpression(compiler.LastSymbols[expectedName], runtimeContextType, expectedType, expectedPath);
    }

    [DataTestMethod]
    [DataRow(nameof(ExpressionEmbeddedCodePreparer.PrepareSemanticPredicate), EmbeddedCodeKind.SemanticPredicate, typeof(SemanticPredicateEvaluationContext))]
    [DataRow(nameof(ExpressionEmbeddedCodePreparer.PrepareParserAction), EmbeddedCodeKind.ParserInlineAction, typeof(ParserActionExecutionContext))]
    public void PrepareRuntimeArtifact_WhenAllSymbolsAreSupported_PassesExpectedRuntimeMembers(string prepareMethodName, EmbeddedCodeKind kind, Type runtimeContextType)
    {
        var compiler = new FakeExpressionCompiler();
        var preparer = new ExpressionEmbeddedCodePreparer(compiler);

        InvokePrepare(preparer, prepareMethodName, CreateSource("true", kind), CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression));

        Assert.IsNotNull(compiler.LastSymbols);
        AssertSymbolSet(compiler.LastSymbols, runtimeContextType);
    }

    [DataTestMethod]
    [DataRow(nameof(ExpressionEmbeddedCodePreparer.PrepareSemanticPredicate), EmbeddedCodeKind.SemanticPredicate, typeof(SemanticPredicateEvaluationContext))]
    [DataRow(nameof(ExpressionEmbeddedCodePreparer.PrepareParserAction), EmbeddedCodeKind.ParserInlineAction, typeof(ParserActionExecutionContext))]
    public void PrepareRuntimeArtifact_WhenSubsetIsUnordered_DoesNotAddExtraSymbols(string prepareMethodName, EmbeddedCodeKind kind, Type runtimeContextType)
    {
        var compiler = new FakeExpressionCompiler();
        var preparer = new ExpressionEmbeddedCodePreparer(compiler);
        var symbols = new HashSet<EmbeddedCodeContextSymbol>
        {
            EmbeddedCodeContextSymbol.ElementIndex,
            EmbeddedCodeContextSymbol.RuleName
        };

        InvokePrepare(preparer, prepareMethodName, CreateSource("true", kind), CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression, symbols));

        Assert.IsNotNull(compiler.LastSymbols);
        CollectionAssert.AreEquivalent(new[] { "elementIndex", "ruleName" }, compiler.LastSymbols.Keys.ToArray());
        AssertSymbolExpression(compiler.LastSymbols["ruleName"], runtimeContextType, typeof(string), "Rule.Name");
        AssertSymbolExpression(compiler.LastSymbols["elementIndex"], runtimeContextType, typeof(int), "ElementIndex");
    }

    [DataTestMethod]
    [DataRow(nameof(ExpressionEmbeddedCodePreparer.PrepareSemanticPredicate), EmbeddedCodeKind.SemanticPredicate)]
    [DataRow(nameof(ExpressionEmbeddedCodePreparer.PrepareParserAction), EmbeddedCodeKind.ParserInlineAction)]
    public void PrepareRuntimeArtifact_WhenUnknownSymbolIsSupported_IgnoresIt(string prepareMethodName, EmbeddedCodeKind kind)
    {
        var compiler = new FakeExpressionCompiler();
        var preparer = new ExpressionEmbeddedCodePreparer(compiler);
        var symbols = new HashSet<EmbeddedCodeContextSymbol> { (EmbeddedCodeContextSymbol)999 };

        InvokePrepare(preparer, prepareMethodName, CreateSource("true", kind), CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression, symbols));

        Assert.IsNotNull(compiler.LastSymbols);
        Assert.AreEqual(0, compiler.LastSymbols.Count);
    }

    [TestMethod]
    public void PreparedSemanticPredicate_WhenIndexSymbolsAreUsed_ReadsCurrentRuntimeContext()
    {
        var preparer = new ExpressionEmbeddedCodePreparer(new FakeExpressionCompiler());

        var result = preparer.PrepareSemanticPredicate(
            CreateSource("indexes-match", EmbeddedCodeKind.SemanticPredicate),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression));

        Assert.AreEqual(EmbeddedCodePreparationStatus.Succeeded, result.Status);
        Assert.IsNotNull(result.Artifact);
        Assert.AreEqual(SemanticPredicateEvaluationStatus.Satisfied, result.Artifact.Evaluate(CreatePredicateContext("indexes-match", inputPosition: 1, alternativeIndex: 2, elementIndex: 3)).Status);
        Assert.AreEqual(SemanticPredicateEvaluationStatus.Rejected, result.Artifact.Evaluate(CreatePredicateContext("indexes-match", inputPosition: 4, alternativeIndex: 5, elementIndex: 6)).Status);
    }

    [TestMethod]
    public void PreparedParserAction_WhenIndexSymbolsAreUsed_ReadsCurrentRuntimeContext()
    {
        var compiler = new FakeExpressionCompiler();
        var preparer = new ExpressionEmbeddedCodePreparer(compiler);

        var result = preparer.PrepareParserAction(
            CreateSource("record-indexes", EmbeddedCodeKind.ParserInlineAction),
            CreateContext(EmbeddedCodeTarget.RuntimeInlineExpression));

        Assert.AreEqual(EmbeddedCodePreparationStatus.Succeeded, result.Status);
        Assert.IsNotNull(result.Artifact);
        _ = result.Artifact.Execute(CreateActionContext("record-indexes", inputPosition: 1, alternativeIndex: 2, elementIndex: 3));
        _ = result.Artifact.Execute(CreateActionContext("record-indexes", inputPosition: 4, alternativeIndex: 5, elementIndex: 6));
        CollectionAssert.AreEqual(new[] { "1:2:3", "4:5:6" }, compiler.RecordedIndexes);
    }


    /// <summary>
    /// Invokes a preparation method selected by a data-driven test.
    /// </summary>
    /// <param name="preparer">Preparer under test.</param>
    /// <param name="prepareMethodName">Name of the preparation method to invoke.</param>
    /// <param name="source">Embedded-code source to prepare.</param>
    /// <param name="context">Preparation context to pass to the method.</param>
    private static void InvokePrepare(
        ExpressionEmbeddedCodePreparer preparer,
        string prepareMethodName,
        EmbeddedCodeSource source,
        EmbeddedCodePreparationContext context)
    {
        if (prepareMethodName == nameof(ExpressionEmbeddedCodePreparer.PrepareSemanticPredicate))
        {
            _ = preparer.PrepareSemanticPredicate(source, context);
            return;
        }

        _ = preparer.PrepareParserAction(source, context);
    }

    /// <summary>
    /// Asserts that a full symbol dictionary exposes the expected public names and runtime member expressions.
    /// </summary>
    /// <param name="symbols">Symbol dictionary passed to the expression compiler.</param>
    /// <param name="runtimeContextType">Runtime context type expected at the root of each expression.</param>
    private static void AssertSymbolSet(IReadOnlyDictionary<string, Expression> symbols, Type runtimeContextType)
    {
        CollectionAssert.AreEquivalent(new[] { "ruleName", "inputPosition", "alternativeIndex", "elementIndex" }, symbols.Keys.ToArray());
        AssertSymbolExpression(symbols["ruleName"], runtimeContextType, typeof(string), "Rule.Name");
        AssertSymbolExpression(symbols["inputPosition"], runtimeContextType, typeof(int), "InputPosition");
        AssertSymbolExpression(symbols["alternativeIndex"], runtimeContextType, typeof(int), "AlternativeIndex");
        AssertSymbolExpression(symbols["elementIndex"], runtimeContextType, typeof(int), "ElementIndex");
    }

    /// <summary>
    /// Asserts that a symbol expression targets the expected runtime member chain without relying on expression text.
    /// </summary>
    /// <param name="expression">Expression to inspect.</param>
    /// <param name="runtimeContextType">Runtime context type expected at the root of the member chain.</param>
    /// <param name="expectedType">Expression type expected by the compiler.</param>
    /// <param name="expectedPath">Dot-separated member path expected from the runtime context.</param>
    private static void AssertSymbolExpression(Expression expression, Type runtimeContextType, Type expectedType, string expectedPath)
    {
        Assert.AreEqual(expectedType, expression.Type);
        string[] members = expectedPath.Split('.');
        Expression current = expression;

        for (int index = members.Length - 1; index >= 0; index--)
        {
            Assert.IsInstanceOfType(current, typeof(MemberExpression));
            var memberExpression = (MemberExpression)current;
            Assert.AreEqual(members[index], memberExpression.Member.Name);
            current = memberExpression.Expression!;
        }

        Assert.IsInstanceOfType(current, typeof(ParameterExpression));
        Assert.AreEqual(runtimeContextType, current.Type);
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
    /// <param name="alternativeIndex">Alternative index exposed to contextual expressions.</param>
    /// <param name="elementIndex">Element index exposed to contextual expressions.</param>
    /// <returns>A semantic predicate runtime context.</returns>
    private static SemanticPredicateEvaluationContext CreatePredicateContext(
        string predicateCode,
        string ruleName = "start",
        int inputPosition = 0,
        int alternativeIndex = 0,
        int elementIndex = 0)
    {
        var rule = CreateRule(ruleName);
        return new SemanticPredicateEvaluationContext(
            Rule: rule,
            Predicate: new ValidatingPredicate(predicateCode),
            PredicateCode: predicateCode,
            InputPosition: inputPosition,
            AlternativeIndex: alternativeIndex,
            ElementIndex: elementIndex);
    }

    /// <summary>
    /// Creates a parser action runtime context without invoking <see cref="ParserEngine"/>.
    /// </summary>
    /// <param name="actionCode">Action source code stored in the context.</param>
    /// <param name="ruleName">Rule name exposed to contextual expressions.</param>
    /// <param name="inputPosition">Input position exposed to contextual expressions.</param>
    /// <param name="alternativeIndex">Alternative index exposed to contextual expressions.</param>
    /// <param name="elementIndex">Element index exposed to contextual expressions.</param>
    /// <returns>A parser action runtime context.</returns>
    private static ParserActionExecutionContext CreateActionContext(
        string actionCode,
        string ruleName = "start",
        int inputPosition = 0,
        int alternativeIndex = 0,
        int elementIndex = 0)
    {
        var rule = CreateRule(ruleName);
        return new ParserActionExecutionContext(
            Rule: rule,
            Action: new EmbeddedAction(actionCode, ActionContext.Alternative, ActionPosition.Inline, []),
            ActionCode: actionCode,
            InputPosition: inputPosition,
            AlternativeIndex: alternativeIndex,
            ElementIndex: elementIndex);
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

        /// <summary>
        /// Gets index tuples recorded by generated action delegates.
        /// </summary>
        public List<string> RecordedIndexes { get; } = [];

        /// <summary>Gets the last symbol dictionary passed to the fake compiler.</summary>
        public IReadOnlyDictionary<string, Expression>? LastSymbols { get; private set; }

        /// <inheritdoc />
        public Expression Compile(string content, IReadOnlyDictionary<string, Expression>? symbols = null)
        {
            CompileCount++;
            LastContent = content;
            LastSymbols = symbols;
            return content switch
            {
                "true" => Expression.Constant(true),
                "42" => Expression.Constant(42),
                "increment" => Expression.Call(Expression.Constant(this), nameof(Increment), Type.EmptyTypes),
                "record-rule ruleName" => Expression.Call(Expression.Constant(this), nameof(RecordRule), Type.EmptyTypes, symbols!["ruleName"]),
                "record-position inputPosition" => Expression.Call(Expression.Constant(this), nameof(RecordPosition), Type.EmptyTypes, symbols!["inputPosition"]),
                "ruleName == target" => Expression.Equal(symbols!["ruleName"], Expression.Constant("start")),
                "inputPosition >= 0" => Expression.GreaterThanOrEqual(symbols!["inputPosition"], Expression.Constant(0)),
                "indexes-match" => Expression.AndAlso(
                    Expression.Equal(symbols!["inputPosition"], Expression.Constant(1)),
                    Expression.AndAlso(
                        Expression.Equal(symbols!["alternativeIndex"], Expression.Constant(2)),
                        Expression.Equal(symbols!["elementIndex"], Expression.Constant(3)))),
                "record-indexes" => Expression.Call(
                    Expression.Constant(this),
                    nameof(RecordIndexes),
                    Type.EmptyTypes,
                    symbols!["inputPosition"],
                    symbols!["alternativeIndex"],
                    symbols!["elementIndex"]),
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

        /// <summary>
        /// Records runtime indexes supplied through runtime context symbols.
        /// </summary>
        /// <param name="inputPosition">Runtime input position.</param>
        /// <param name="alternativeIndex">Runtime alternative index.</param>
        /// <param name="elementIndex">Runtime element index.</param>
        public void RecordIndexes(int inputPosition, int alternativeIndex, int elementIndex) =>
            RecordedIndexes.Add($"{inputPosition}:{alternativeIndex}:{elementIndex}");
    }


    /// <summary>
    /// Test transformer that counts invocations for service validation tests.
    /// </summary>

    private sealed class ConfigurableTransformer : IParserEmbeddedCodeTransformer
    {
        public int Count { get; private set; }

        public ParserEmbeddedCodeTransformationResult? Result { get; set; }

        public Exception? Exception { get; set; }

        public ParserEmbeddedCodeTransformationResult Transform(ParserEmbeddedCodeTransformationContext context)
        {
            Count++;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Result!;
        }
    }

    private sealed class CountingTransformer : IParserEmbeddedCodeTransformer
    {
        /// <summary>Gets the number of transform calls.</summary>
        public int Count { get; private set; }

        /// <inheritdoc />
        public ParserEmbeddedCodeTransformationResult Transform(ParserEmbeddedCodeTransformationContext context)
        {
            Count++;
            return new ParserEmbeddedCodeTransformationResult { Code = context.Code };
        }
    }

    /// <summary>
    /// Test transformer that rewrites a sentinel into the fake compiler's action expression.
    /// </summary>
    private sealed class ReplaceRuntimeCodeTransformer : IParserEmbeddedCodeTransformer
    {
        /// <inheritdoc />
        public ParserEmbeddedCodeTransformationResult Transform(ParserEmbeddedCodeTransformationContext context)
        {
            return new ParserEmbeddedCodeTransformationResult { Code = context.Code.Replace("__TOKEN__", "increment").Replace("__TOKEN_PREDICATE__", "true") };
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
