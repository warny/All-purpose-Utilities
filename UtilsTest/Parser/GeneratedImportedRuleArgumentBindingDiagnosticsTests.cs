using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Generators;

namespace UtilsTest.Parser;

/// <summary>
/// Tests the conservative imported parser-rule argument binding diagnostics boundary for the ANTLR4 source generator.
/// </summary>
[TestClass]
public sealed class GeneratedImportedRuleArgumentBindingDiagnosticsTests
{
    /// <summary>Verifies imported binding validation remains disabled even when the local generated-binding option is enabled.</summary>
    [DataTestMethod]
    [DataRow(null)]
    [DataRow("false")]
    [DataRow("true")]
    public void ImportedBindingDiagnostics_DoNotResolveTargetsAbsentFromGeneratedDefinition(string? optionValue)
    {
        var result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; import Shared; start : child[bad] ;"),
            Grammar("Shared.g4", "parser grammar Shared; child[int value] : TOKEN ;")], optionValue);

        AssertNoBindingDiagnostics(result);
        Assert.AreEqual(2, result.GeneratedTrees.Length);
    }

    /// <summary>Verifies local parser rules still receive APU0107 diagnostics and suppress only the invalid caller.</summary>
    [TestMethod]
    public void LocalRule_InvalidArgument_ReportsCallerDiagnostic()
    {
        var result = RunGenerator([
            Grammar("Caller.g4", "parser grammar Caller; import Shared; start : child[bad] ; child[int value] : TOKEN ;"),
            Grammar("Shared.g4", "parser grammar Shared; child[string value] : TOKEN ;"),
            Grammar("Independent.g4", "grammar Independent; start : A ; A : 'a' ;")], "true");

        Diagnostic diagnostic = AssertSingleBindingDiagnostic(result);
        StringAssert.Contains(diagnostic.GetMessage(), "Argument 0 is not a supported simple literal.");
        Assert.AreEqual("Caller.g4", diagnostic.Location.GetLineSpan().Path);
        AssertTreeAbsent(result, "Caller");
        AssertTreePresent(result, "Shared");
        AssertTreePresent(result, "Independent");
    }

    /// <summary>Verifies imported direct and transitive targets do not produce APU0107 conclusions.</summary>
    [DataTestMethod]
    [DataRow("Direct", "parser grammar Root; import Shared; start : child[bad] ;", "parser grammar Shared; child[int value] : TOKEN ;", "parser grammar Other; other : TOKEN ;")]
    [DataRow("Transitive", "parser grammar Root; import Middle; start : child[] ;", "parser grammar Middle; import Shared;", "parser grammar Shared; child[int value] : TOKEN ;")]
    [DataRow("Diamond", "parser grammar Root; import A, B; start : child[bad] ;", "parser grammar A; import Shared;", "parser grammar B; import Shared; child[int value] : TOKEN ;")]
    [DataRow("Ambiguous", "parser grammar Root; import A, B; start : child[bad] ;", "parser grammar A; child[int value] : TOKEN ;", "parser grammar B; child[int value] : TOKEN ;")]
    [DataRow("Cycle", "parser grammar Root; import Other; start : child[bad] ;", "parser grammar Other; import Root;", "parser grammar Shared; child[int value] : TOKEN ;")]
    [DataRow("Missing", "parser grammar Root; import Missing; start : child[bad] ;", "parser grammar Other; other : TOKEN ;", "parser grammar Shared; child[int value] : TOKEN ;")]
    [DataRow("Aliased", "parser grammar Root; import Alias=Shared; start : child[bad] ;", "parser grammar Shared; child[int value] : TOKEN ;", "parser grammar Other; other : TOKEN ;")]
    [DataRow("Lexer", "parser grammar Root; import Tokens; start : TOKEN[bad] ;", "lexer grammar Tokens; TOKEN : 'a' ;", "parser grammar Other; other : TOKEN ;")]
    [DataRow("DifferentFile", "parser grammar Root; import Shared; start : child[bad] ;", "parser grammar Shared; child[int value] : TOKEN ;", "parser grammar Other; other : TOKEN ;")]
    [DataRow("DuplicateName", "parser grammar Root; import Shared; start : child[bad] ;", "parser grammar Shared; child[int value] : TOKEN ;", "parser grammar Shared; child[int value] : TOKEN ;")]
    public void ImportedTargets_DoNotReportBindingDiagnostic(string scenario, string root, string first, string second)
    {
        var result = RunGenerator([
            Grammar("Root.g4", root),
            Grammar($"{scenario}One.g4", first),
            Grammar($"{scenario}Two.g4", second)], "true");

        AssertNoBindingDiagnostics(result);
    }

    /// <summary>Creates an in-memory grammar additional file.</summary>
    private static InMemoryAdditionalText Grammar(string path, string text) => new(path, text);

    /// <summary>Runs the source generator for in-memory grammars.</summary>
    private static GeneratorDriverRunResult RunGenerator(IReadOnlyList<AdditionalText> grammars, string? optionValue) => CreateDriver(grammars, optionValue).RunGenerators(CreateCompilation()).GetRunResult();

    /// <summary>Creates a test compilation.</summary>
    private static CSharpCompilation CreateCompilation() => CSharpCompilation.Create("GeneratedImportedRuleArgumentBindingDiagnosticsTests", [CSharpSyntaxTree.ParseText("namespace Generated.Tests;", CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview))], [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    /// <summary>Creates a generator driver with global options.</summary>
    private static GeneratorDriver CreateDriver(IReadOnlyList<AdditionalText> grammars, string? optionValue) => CSharpGeneratorDriver.Create([new Antlr4GrammarGenerator().AsSourceGenerator()], grammars, parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview), optionsProvider: new DictionaryAnalyzerConfigOptionsProvider(CreateGlobalOptions(optionValue)));

    /// <summary>Creates a generator driver with Roslyn incremental-step tracking enabled.</summary>
    private static GeneratorDriver CreateTrackingDriver(IReadOnlyList<AdditionalText> grammars, string? optionValue) => CSharpGeneratorDriver.Create(generators: [new Antlr4GrammarGenerator().AsSourceGenerator()], additionalTexts: grammars, parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview), optionsProvider: new DictionaryAnalyzerConfigOptionsProvider(CreateGlobalOptions(optionValue)), driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));

    /// <summary>Creates analyzer config global options.</summary>
    private static ImmutableDictionary<string, string> CreateGlobalOptions(string? optionValue)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>();
        if (optionValue is not null) builder[Antlr4GrammarGeneratorOptions.EnableGeneratedRuleArgumentBindingKey] = optionValue;
        return builder.ToImmutable();
    }

    /// <summary>Gets binding diagnostics in generator output order.</summary>
    private static Diagnostic[] BindingDiagnostics(GeneratorDriverRunResult result) => result.Diagnostics.Where(static diagnostic => diagnostic.Id == "APU0107").ToArray();

    /// <summary>Asserts that exactly one binding diagnostic exists.</summary>
    private static Diagnostic AssertSingleBindingDiagnostic(GeneratorDriverRunResult result) => BindingDiagnostics(result).Single();

    /// <summary>Asserts that no binding diagnostics exist.</summary>
    private static void AssertNoBindingDiagnostics(GeneratorDriverRunResult result) => Assert.AreEqual(0, BindingDiagnostics(result).Length);

    /// <summary>Asserts that one generated tree path contains the expected fragment.</summary>
    private static SyntaxTree AssertTreePresent(GeneratorDriverRunResult result, string fragment) => result.GeneratedTrees.Single(tree => tree.FilePath.Contains(fragment));

    /// <summary>Asserts that no generated tree path contains the expected fragment.</summary>
    private static void AssertTreeAbsent(GeneratorDriverRunResult result, string fragment) => Assert.AreEqual(0, result.GeneratedTrees.Count(tree => tree.FilePath.Contains(fragment)));

    /// <summary>Analyzer config options backed by an immutable dictionary.</summary>
    private sealed class DictionaryAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly ImmutableDictionary<string, string> _values;

        /// <summary>Initializes the option map.</summary>
        public DictionaryAnalyzerConfigOptions(ImmutableDictionary<string, string> values) => _values = values;

        /// <inheritdoc />
        public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);
    }

    /// <summary>Provides deterministic analyzer-config options for source-generator tests.</summary>
    private sealed class DictionaryAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _globalOptions;
        private readonly AnalyzerConfigOptions _emptyOptions = new DictionaryAnalyzerConfigOptions(ImmutableDictionary<string, string>.Empty);
        public DictionaryAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string> globalOptions) => _globalOptions = new DictionaryAnalyzerConfigOptions(globalOptions);
        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _emptyOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _emptyOptions;
    }

    /// <summary>In-memory additional text used as a grammar file.</summary>
    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;
        public InMemoryAdditionalText(string path, string text) { Path = path; _text = SourceText.From(text); }
        public override string Path { get; }
        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }
}
