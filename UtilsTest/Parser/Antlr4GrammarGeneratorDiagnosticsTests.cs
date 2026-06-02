using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Immutable;
using System.Text;
using Utils.Parser.Diagnostics;
using Utils.Parser.Generators;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies source-generator diagnostics for visible embedded-code constructs that are intentionally not executed.
/// </summary>
[TestClass]
public class Antlr4GrammarGeneratorDiagnosticsTests
{
    /// <summary>
    /// Ensures lexer actions are reported as unsupported generator execution constructs.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_LexerAction_ReportsUnsupportedEmbeddedCode()
    {
        const string grammar = """
            grammar P;
            start : A ;
            A : 'a' { OnLex(context); } ;
            """;

        var diagnostics = RunGenerator(grammar);
        var diagnostic = AssertSingleUnsupportedDiagnostic(diagnostics, "Lexer action");

        Assert.AreEqual(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, diagnostic.Severity);
        StringAssert.Contains(diagnostic.GetMessage(), "not executed");
        AssertGeneratedSourceDoesNotContainLexerHook(grammar);
    }

    /// <summary>
    /// Ensures lexer predicates are reported as unsupported generator execution constructs.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_LexerPredicate_ReportsUnsupportedEmbeddedCode()
    {
        const string grammar = """
            grammar P;
            start : A ;
            A : { IsA(context) }? 'a' ;
            """;

        var diagnostic = AssertSingleUnsupportedDiagnostic(RunGenerator(grammar), "Lexer predicate");

        Assert.AreEqual(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, diagnostic.Severity);
        StringAssert.Contains(diagnostic.GetMessage(), "lexer-state-aware");
    }

    /// <summary>
    /// Ensures grammar-level actions are reported as metadata-only generator constructs.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_GrammarAction_ReportsUnsupportedEmbeddedCode()
    {
        const string grammar = """
            grammar P;

            @header {
                // metadata only
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostic = AssertSingleUnsupportedDiagnostic(RunGenerator(grammar), "Grammar @header action");

        StringAssert.Contains(diagnostic.GetMessage(), "preserved as metadata only");
    }

    /// <summary>
    /// Ensures <c>@members</c> is reported as not injected and recommends the supported partial-class extension point.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_MembersAction_ReportsPartialClassGuidance()
    {
        const string grammar = """
            grammar P;

            @members {
                private int Value;
            }

            start : A ;
            A : 'a' ;
            """;

        var diagnostic = AssertSingleUnsupportedDiagnostic(RunGenerator(grammar), "Grammar @members action");

        StringAssert.Contains(diagnostic.GetMessage(), "not injected");
        StringAssert.Contains(diagnostic.GetMessage(), "partial class");
    }

    /// <summary>
    /// Ensures rule <c>@init</c> lifecycle actions are reported as recognized but not executed.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_RuleInitAction_ReportsUnsupportedEmbeddedCode()
    {
        const string grammar = """
            grammar P;

            start
            @init { OnInit(context); }
                : A ;

            A : 'a' ;
            """;

        var diagnostic = AssertSingleUnsupportedDiagnostic(RunGenerator(grammar), "Rule @init action");

        StringAssert.Contains(diagnostic.GetMessage(), "lifecycle actions require a dedicated execution model");
    }

    /// <summary>
    /// Ensures rule <c>@after</c> lifecycle actions are reported as recognized but not executed.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_RuleAfterAction_ReportsUnsupportedEmbeddedCode()
    {
        const string grammar = """
            grammar P;

            start
            @after { OnAfter(context); }
                : A ;

            A : 'a' ;
            """;

        var diagnostic = AssertSingleUnsupportedDiagnostic(RunGenerator(grammar), "Rule @after action");

        StringAssert.Contains(diagnostic.GetMessage(), "metadata-only");
    }

    /// <summary>
    /// Ensures supported parser semantic predicates are not reported as unsupported generator constructs.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_SupportedParserPredicate_DoesNotReportUnsupportedEmbeddedCode()
    {
        const string grammar = """
            grammar P;
            start : { inputPosition == 0 }? A ;
            A : 'a' ;
            """;

        AssertNoUnsupportedDiagnostics(RunGenerator(grammar));
    }

    /// <summary>
    /// Ensures supported inline parser actions are not reported as unsupported generator constructs.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_SupportedInlineParserAction_DoesNotReportUnsupportedEmbeddedCode()
    {
        const string grammar = """
            grammar P;
            start : { OnAction(context); } A ;
            A : 'a' ;
            """;

        AssertNoUnsupportedDiagnostics(RunGenerator(grammar));
    }

    /// <summary>
    /// Ensures invalid C# inside a supported generated predicate remains owned by Roslyn instead of the unsupported-construct diagnostic.
    /// </summary>
    [TestMethod]
    public void GeneratorDiagnostics_InvalidSupportedPredicateCode_DoesNotReportUnsupportedEmbeddedCode()
    {
        const string grammar = """
            grammar P;
            start : { return "not bool"; }? A ;
            A : 'a' ;
            """;

        AssertNoUnsupportedDiagnostics(RunGenerator(grammar));
    }

    /// <summary>
    /// Asserts that exactly one unsupported embedded-code diagnostic exists and that it describes the expected construct.
    /// </summary>
    /// <param name="diagnostics">Diagnostics emitted by the source generator.</param>
    /// <param name="expectedConstruct">Expected construct kind text.</param>
    /// <returns>The matching diagnostic.</returns>
    private static Diagnostic AssertSingleUnsupportedDiagnostic(ImmutableArray<Diagnostic> diagnostics, string expectedConstruct)
    {
        var matches = diagnostics
            .Where(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedCodeConstructNotExecutedByGenerator.Code)
            .ToArray();

        Assert.AreEqual(1, matches.Length, string.Join(Environment.NewLine, diagnostics));
        Assert.AreEqual(ParserDiagnostics.EmbeddedCodeConstructNotExecutedByGenerator.Code, matches[0].Id);
        StringAssert.Contains(matches[0].GetMessage(), expectedConstruct);
        StringAssert.Contains(matches[0].GetMessage(), "Only parser semantic predicates and inline parser actions");
        return matches[0];
    }

    /// <summary>
    /// Asserts that no unsupported embedded-code source-generator diagnostics were emitted.
    /// </summary>
    /// <param name="diagnostics">Diagnostics emitted by the source generator.</param>
    private static void AssertNoUnsupportedDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        Assert.IsFalse(
            diagnostics.Any(static diagnostic => diagnostic.Id == ParserDiagnostics.EmbeddedCodeConstructNotExecutedByGenerator.Code),
            string.Join(Environment.NewLine, diagnostics));
    }

    /// <summary>
    /// Runs the ANTLR4 grammar source generator against an in-memory grammar file.
    /// </summary>
    /// <param name="grammar">ANTLR4 grammar source.</param>
    /// <returns>Generator diagnostics.</returns>
    private static ImmutableArray<Diagnostic> RunGenerator(string grammar)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorDiagnosticsTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText("namespace Generated.Tests;", CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview))],
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new Antlr4GrammarGenerator().AsSourceGenerator()],
            additionalTexts: [new InMemoryAdditionalText("P.g4", grammar)],
            parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview));

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult().Diagnostics;
    }

    /// <summary>
    /// Ensures generated source does not contain lexer hook helpers for lexer actions.
    /// </summary>
    /// <param name="grammar">ANTLR4 grammar source.</param>
    private static void AssertGeneratedSourceDoesNotContainLexerHook(string grammar)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new Antlr4GrammarGenerator().AsSourceGenerator()],
            additionalTexts: [new InMemoryAdditionalText("P.g4", grammar)],
            parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview));

        var compilation = CSharpCompilation.Create("GeneratorDiagnosticsSourceTests");
        driver = driver.RunGenerators(compilation);
        var generatedSource = driver.GetRunResult().GeneratedTrees.Single().ToString();

        Assert.IsFalse(generatedSource.Contains("__Lexer", StringComparison.Ordinal));
        Assert.IsFalse(generatedSource.Contains("__Action_A", StringComparison.Ordinal));
    }

    /// <summary>
    /// In-memory additional file used to provide grammar text to the source generator.
    /// </summary>
    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        /// <summary>
        /// Initializes an in-memory additional text file.
        /// </summary>
        /// <param name="path">Virtual file path.</param>
        /// <param name="text">File content.</param>
        public InMemoryAdditionalText(string path, string text)
        {
            Path = path;
            _text = SourceText.From(text, Encoding.UTF8);
        }

        /// <inheritdoc />
        public override string Path { get; }

        /// <inheritdoc />
        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            return _text;
        }
    }
}
