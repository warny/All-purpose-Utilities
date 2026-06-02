using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Diagnostics;
using Utils.Parser.EmbeddedCode;
using Utils.Parser.Source;

namespace UtilsTest.Parser;

/// <summary>
/// Tests for embedded-code preparation boundary contracts.
/// </summary>
[TestClass]
public class EmbeddedCodePreparationContractTests
{
    /// <summary>
    /// Verifies that embedded-code source metadata preserves raw source text and construct identity.
    /// </summary>
    [TestMethod]
    public void EmbeddedCodeSource_StoresRawSourceAndConstructKind()
    {
        var location = new SourceCodeLocation("Grammar.g4", 3, 17);
        var source = new EmbeddedCodeSource(
            "inputPosition > 0",
            EmbeddedCodeKind.SemanticPredicate,
            ruleName: "expr",
            alternativeIndex: 1,
            elementIndex: 2,
            location: location);

        Assert.AreEqual("inputPosition > 0", source.SourceText);
        Assert.AreEqual(EmbeddedCodeKind.SemanticPredicate, source.Kind);
        Assert.AreEqual("expr", source.RuleName);
        Assert.AreEqual(1, source.AlternativeIndex);
        Assert.AreEqual(2, source.ElementIndex);
        Assert.AreSame(location, source.Location);
    }

    /// <summary>
    /// Verifies that preparation context metadata records the explicitly selected target path.
    /// </summary>
    [TestMethod]
    public void EmbeddedCodePreparationContext_RepresentsExplicitTargetPath()
    {
        var context = new EmbeddedCodePreparationContext(
            "ExprGrammar",
            EmbeddedCodeTarget.RuntimeInlineExpression,
            ruleName: "expr",
            languageOrCompilerIdentity: "TestExpressionCompiler",
            symbolModelVersion: 2,
            supportedSymbols: new HashSet<EmbeddedCodeContextSymbol>
            {
                EmbeddedCodeContextSymbol.RuleName,
                EmbeddedCodeContextSymbol.InputPosition
            });

        Assert.AreEqual("ExprGrammar", context.GrammarName);
        Assert.AreEqual(EmbeddedCodeTarget.RuntimeInlineExpression, context.Target);
        Assert.AreEqual("expr", context.RuleName);
        Assert.AreEqual("TestExpressionCompiler", context.LanguageOrCompilerIdentity);
        Assert.AreEqual(2, context.SymbolModelVersion);
        Assert.IsTrue(context.SupportedSymbols.Contains(EmbeddedCodeContextSymbol.RuleName));
        Assert.IsTrue(context.SupportedSymbols.Contains(EmbeddedCodeContextSymbol.InputPosition));
        Assert.IsFalse(context.SupportedSymbols.Contains(EmbeddedCodeContextSymbol.AlternativeIndex));
    }


    /// <summary>
    /// Verifies that the default preparation context exposes the complete current parser symbol model.
    /// </summary>
    [TestMethod]
    public void EmbeddedCodePreparationContext_WhenSymbolsAreOmitted_ExposesDefaultRuntimeSymbols()
    {
        var context = new EmbeddedCodePreparationContext("ExprGrammar", EmbeddedCodeTarget.RuntimeInlineExpression);

        CollectionAssert.AreEquivalent(
            new object[]
            {
                EmbeddedCodeContextSymbol.RuleName,
                EmbeddedCodeContextSymbol.InputPosition,
                EmbeddedCodeContextSymbol.AlternativeIndex,
                EmbeddedCodeContextSymbol.ElementIndex
            },
            context.SupportedSymbols.Cast<object>().ToArray());
    }

    /// <summary>
    /// Verifies that success results cannot silently omit the prepared artifact contract.
    /// </summary>
    [TestMethod]
    public void EmbeddedCodePreparationResult_WhenSuccessHasNoArtifact_ThrowsArgumentException()
    {
        Assert.ThrowsException<ArgumentException>(() => new EmbeddedCodePreparationResult<string>(EmbeddedCodePreparationStatus.Succeeded));
    }

    /// <summary>
    /// Verifies that diagnostic arguments are materialized and preserved even if the caller mutates the original collection.
    /// </summary>
    [TestMethod]
    public void EmbeddedCodePreparationResult_MaterializesDiagnosticArguments()
    {
        var arguments = new List<object?> { "predicate", "runtime" };
        var result = EmbeddedCodePreparationResult<string>.Unsupported(arguments);

        arguments[0] = "changed";

        Assert.AreEqual("predicate", result.DiagnosticArguments[0]);
        Assert.AreEqual("runtime", result.DiagnosticArguments[1]);
    }

    /// <summary>
    /// Verifies that preparation results can represent a successful artifact-producing outcome.
    /// </summary>
    [TestMethod]
    public void EmbeddedCodePreparationResult_RepresentsSuccess()
    {
        var result = EmbeddedCodePreparationResult<string>.Success("prepared-predicate");

        Assert.AreEqual(EmbeddedCodePreparationStatus.Succeeded, result.Status);
        Assert.AreEqual("prepared-predicate", result.Artifact);
        Assert.IsNull(result.DiagnosticDescriptor);
        Assert.AreEqual(0, result.DiagnosticArguments.Count);
    }

    /// <summary>
    /// Verifies that preparation results can represent an unsupported embedded-code path.
    /// </summary>
    [TestMethod]
    public void EmbeddedCodePreparationResult_RepresentsUnsupported()
    {
        var result = EmbeddedCodePreparationResult<string>.Unsupported(new object?[] { "semantic predicate" });

        Assert.AreEqual(EmbeddedCodePreparationStatus.Unsupported, result.Status);
        Assert.IsNull(result.Artifact);
        Assert.AreSame(ParserDiagnostics.EmbeddedCodeLanguageUnsupported, result.DiagnosticDescriptor);
        Assert.AreEqual("semantic predicate", result.DiagnosticArguments[0]);
    }


    /// <summary>
    /// Verifies that preparation results can represent a missing compiler configuration.
    /// </summary>
    [TestMethod]
    public void EmbeddedCodePreparationResult_RepresentsCompilerNotConfigured()
    {
        var result = EmbeddedCodePreparationResult<string>.CompilerNotConfigured(new object?[] { "inline action" });

        Assert.AreEqual(EmbeddedCodePreparationStatus.CompilerNotConfigured, result.Status);
        Assert.IsNull(result.Artifact);
        Assert.AreSame(ParserDiagnostics.EmbeddedCodeCompilerNotConfigured, result.DiagnosticDescriptor);
        Assert.AreEqual("inline action", result.DiagnosticArguments[0]);
    }

    /// <summary>
    /// Verifies that preparation results can represent a failed compilation attempt.
    /// </summary>
    [TestMethod]
    public void EmbeddedCodePreparationResult_RepresentsCompilationFailed()
    {
        var exception = new InvalidOperationException("compiler failed");
        var result = EmbeddedCodePreparationResult<string>.CompilationFailed(
            exception,
            new object?[] { "inline action", exception.Message });

        Assert.AreEqual(EmbeddedCodePreparationStatus.CompilationFailed, result.Status);
        Assert.IsNull(result.Artifact);
        Assert.AreSame(ParserDiagnostics.EmbeddedCodeCompilationFailed, result.DiagnosticDescriptor);
        Assert.AreSame(exception, result.Exception);
        Assert.AreEqual("inline action", result.DiagnosticArguments[0]);
        Assert.AreEqual("compiler failed", result.DiagnosticArguments[1]);
    }

    /// <summary>
    /// Verifies that preparation results can represent source preserved without compilation.
    /// </summary>
    [TestMethod]
    public void EmbeddedCodePreparationResult_RepresentsPreservedWithoutCompilation()
    {
        var result = EmbeddedCodePreparationResult<string>.PreservedNotCompiled(new object?[] { "expr:ParserInlineAction" });

        Assert.AreEqual(EmbeddedCodePreparationStatus.PreservedNotCompiled, result.Status);
        Assert.IsNull(result.Artifact);
        Assert.AreSame(ParserDiagnostics.EmbeddedCodePreservedNotCompiled, result.DiagnosticDescriptor);
        Assert.AreEqual("expr:ParserInlineAction", result.DiagnosticArguments[0]);
    }

    /// <summary>
    /// Verifies that the neutral preparer preserves semantic predicates without compilation.
    /// </summary>
    [TestMethod]
    public void PreservingEmbeddedCodePreparer_DoesNotCompileSemanticPredicate()
    {
        var preparer = new PreservingEmbeddedCodePreparer<string, string>();
        var source = new EmbeddedCodeSource("inputPosition > 0", EmbeddedCodeKind.SemanticPredicate, ruleName: "expr");
        var context = new EmbeddedCodePreparationContext("ExprGrammar", EmbeddedCodeTarget.SourceGeneratorCSharp, ruleName: "expr");

        var result = preparer.PrepareSemanticPredicate(source, context);

        Assert.AreEqual(EmbeddedCodePreparationStatus.PreservedNotCompiled, result.Status);
        Assert.IsNull(result.Artifact);
        Assert.AreSame(ParserDiagnostics.EmbeddedCodePreservedNotCompiled, result.DiagnosticDescriptor);
    }

    /// <summary>
    /// Verifies that the neutral preparer preserves inline parser actions without compilation.
    /// </summary>
    [TestMethod]
    public void PreservingEmbeddedCodePreparer_DoesNotCompileParserAction()
    {
        var preparer = new PreservingEmbeddedCodePreparer<string, string>();
        var source = new EmbeddedCodeSource("count++;", EmbeddedCodeKind.ParserInlineAction, ruleName: "expr");
        var context = new EmbeddedCodePreparationContext("ExprGrammar", EmbeddedCodeTarget.RuntimeInlineExpression, ruleName: "expr");

        var result = preparer.PrepareParserAction(source, context);

        Assert.AreEqual(EmbeddedCodePreparationStatus.PreservedNotCompiled, result.Status);
        Assert.IsNull(result.Artifact);
        Assert.AreSame(ParserDiagnostics.EmbeddedCodePreservedNotCompiled, result.DiagnosticDescriptor);
    }

    /// <summary>
    /// Verifies that the neutral preparer rejects source kinds outside the requested preparation method.
    /// </summary>
    [TestMethod]
    public void PreservingEmbeddedCodePreparer_ReturnsUnsupportedForWrongConstruct()
    {
        var preparer = new PreservingEmbeddedCodePreparer<string, string>();
        var source = new EmbeddedCodeSource("count++;", EmbeddedCodeKind.RuleInitAction, ruleName: "expr");
        var context = new EmbeddedCodePreparationContext("ExprGrammar", EmbeddedCodeTarget.RuntimeInlineExpression, ruleName: "expr");

        var result = preparer.PrepareParserAction(source, context);

        Assert.AreEqual(EmbeddedCodePreparationStatus.Unsupported, result.Status);
        Assert.IsNull(result.Artifact);
        Assert.AreSame(ParserDiagnostics.EmbeddedCodeLanguageUnsupported, result.DiagnosticDescriptor);
    }
}
