using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Generators;
using Utils.Parser.Generators.Internal;

namespace UtilsTest.Parser;

/// <summary>
/// Tests source-located diagnostics for generated-C# positional rule-call argument binding.
/// </summary>
[TestClass]
public sealed class GeneratedRuleArgumentBindingDiagnosticsTests
{
    /// <summary>Verifies valid exact positional literals produce generated source and no APU0107 diagnostic.</summary>
    [TestMethod]
    public void Enabled_ValidSimpleAndMultipleParameters_GeneratesSource()
    {
        var result = RunGenerator("""
            grammar P;
            start : child[1, "a"] ;
            child[int x, string y] : A ;
            A : 'a' ;
            """, "true");

        AssertNoBindingDiagnostics(result);
        Assert.AreEqual(1, result.GeneratedTrees.Length);
    }

    /// <summary>Verifies invalid forms remain metadata-only when the option is absent or disabled.</summary>
    [DataTestMethod]
    [DataRow(null)]
    [DataRow("false")]
    public void Disabled_InvalidForms_DoNotReportBindingDiagnostics(string? optionValue)
    {
        var result = RunGenerator("""
            grammar P;
            start : child[value: 42] | child[1 + 2] | child[SomeMember] | child[$x] ;
            child[int value] : A ;
            A : 'a' ;
            """, optionValue);

        AssertNoBindingDiagnostics(result);
        Assert.AreEqual(1, result.GeneratedTrees.Length);
    }

    /// <summary>Verifies representative invalid enabled forms report deterministic messages.</summary>
    [DataTestMethod]
    [DataRow("child[]", "int value", "Expected exactly 1 positional argument(s), but received 0.")]
    [DataRow("child[1, 2]", "int value", "Expected exactly 1 positional argument(s), but received 2.")]
    [DataRow("child[]", "int value = 1", "Expected exactly 1 positional argument(s), but received 0.")]
    [DataRow("child[1]", null, "Expected exactly 0 positional argument(s), but received 1.")]
    [DataRow("child[value: 42]", "int value", "Named rule-call arguments are not supported.")]
    [DataRow("child[value = 42]", "int value", "Named rule-call arguments are not supported.")]
    [DataRow("child[1, value: 2]", "int value, int other", "Named rule-call arguments are not supported.")]
    [DataRow("child[SomeMember]", "int value", "Argument 0 is not a supported simple literal.")]
    [DataRow("child[1 + 2]", "int value", "Argument 0 is not a supported simple literal.")]
    [DataRow("child[Other()]", "int value", "Argument 0 is not a supported simple literal.")]
    [DataRow("child[$x]", "int value", "Argument 0 is not a supported simple literal.")]
    [DataRow("child[$label.return]", "int value", "Argument 0 is not a supported simple literal.")]
    [DataRow("child[\"text\"]", "int value", "Argument 0 cannot bind to declared type 'int'.")]
    [DataRow("child[300]", "byte value", "Argument 0 cannot bind to declared type 'byte'.")]
    [DataRow("child[null]", "int value", "Argument 0 cannot bind to declared type 'int'.")]
    [DataRow("child[1]", "CustomType value", "Target parameter 'value' uses unsupported declared type 'CustomType'.")]
    [DataRow("child[1, 2]", "int value, int value", "Target parameter name 'value' is duplicated.")]
    public void Enabled_InvalidForms_ReportBindingDiagnostic(string call, string? parameters, string reason)
    {
        string declaration = parameters is null ? "child : A ;" : $"child[{parameters}] : A ;";
        var result = RunGenerator($"""
            grammar P;
            start : {call} ;
            {declaration}
            A : 'a' ;
            """, "true");

        var diagnostic = AssertSingleBindingDiagnostic(result);
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
        StringAssert.Contains(diagnostic.GetMessage(), reason);
        Assert.AreEqual(1, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
        Assert.AreEqual(8, diagnostic.Location.GetLineSpan().StartLinePosition.Character);
        Assert.AreEqual(0, result.GeneratedTrees.Length);
    }

    /// <summary>Verifies zero-parameter calls without and with empty clauses are valid.</summary>
    [DataTestMethod]
    [DataRow("child")]
    [DataRow("child[]")]
    public void Enabled_ZeroParameterCalls_AreValid(string call)
    {
        var result = RunGenerator($"""
            grammar P;
            start : {call} ;
            child : A ;
            A : 'a' ;
            """, "true");

        AssertNoBindingDiagnostics(result);
        Assert.AreEqual(1, result.GeneratedTrees.Length);
    }

    /// <summary>Verifies nested and alternative call references are traversed in source order.</summary>
    [TestMethod]
    public void Validator_NestedAlternativesQuantifierAndRightRecursion_FindsCallsInSourceOrder()
    {
        var grammar = new G4Parser(new G4Tokenizer("""
            grammar P;
            start : (child[bad])* | child[bad] ;
            right : A right[bad]? | A ;
            child[int value] : A ;
            A : 'a' ;
            """).Tokenize()).Parse();

        var issues = GeneratedRuleArgumentBindingValidator.Validate(grammar);

        Assert.AreEqual(3, issues.Length);
        CollectionAssert.AreEqual(new[] { 2, 2, 3 }, issues.Select(static issue => issue.CallSite.Line).ToArray());
    }

    /// <summary>Verifies unresolved or imported targets do not produce generated-binding diagnostics.</summary>
    [TestMethod]
    public void Enabled_UnresolvedOrImportedTargets_DoNotReportBindingDiagnostic()
    {
        var result = RunGenerator("""
            grammar P;
            import Shared;
            start : importedRule[1] | unknownRule[2] ;
            A : 'a' ;
            """, "true");

        AssertNoBindingDiagnostics(result);
        Assert.AreEqual(1, result.GeneratedTrees.Length);
    }

    /// <summary>Verifies an invalid file does not prevent a second valid file from being emitted.</summary>
    [TestMethod]
    public void Enabled_MultipleFiles_EmitsValidFileOnly()
    {
        var result = RunGenerator([
            new InMemoryAdditionalText("Invalid.g4", "grammar Invalid; start : child[bad] ; child[int value] : A ; A : 'a' ;"),
            new InMemoryAdditionalText("Valid.g4", "grammar Valid; start : child[1] ; child[int value] : A ; A : 'a' ;")], "true");

        Assert.AreEqual(1, result.Diagnostics.Count(static d => d.Id == "APU0107"));
        Assert.AreEqual(1, result.GeneratedTrees.Length);
        StringAssert.Contains(result.GeneratedTrees[0].FilePath, "Valid");
    }

    /// <summary>Verifies option changes invalidate diagnostics and generated output in both directions.</summary>
    [TestMethod]
    public void Incremental_OptionChange_TogglesDiagnosticsAndGeneratedSource()
    {
        var compilation = CreateCompilation();
        var grammar = new InMemoryAdditionalText("P.g4", "grammar P; start : child[bad] ; child[int value] : A ; A : 'a' ;");
        GeneratorDriver driver = CreateDriver([grammar], "false");

        driver = driver.RunGenerators(compilation);
        AssertNoBindingDiagnostics(driver.GetRunResult());
        Assert.AreEqual(1, driver.GetRunResult().GeneratedTrees.Length);

        driver = driver.WithUpdatedAnalyzerConfigOptions(new DictionaryAnalyzerConfigOptionsProvider(CreateGlobalOptions("true")));
        driver = driver.RunGenerators(compilation);
        Assert.AreEqual(1, driver.GetRunResult().Diagnostics.Count(static d => d.Id == "APU0107"));
        Assert.AreEqual(0, driver.GetRunResult().GeneratedTrees.Length);

        driver = driver.WithUpdatedAnalyzerConfigOptions(new DictionaryAnalyzerConfigOptionsProvider(CreateGlobalOptions("false")));
        driver = driver.RunGenerators(compilation);
        AssertNoBindingDiagnostics(driver.GetRunResult());
        Assert.AreEqual(1, driver.GetRunResult().GeneratedTrees.Length);
    }

    /// <summary>Runs the source generator for a single in-memory grammar.</summary>
    private static GeneratorDriverRunResult RunGenerator(string grammar, string? optionValue) => RunGenerator([new InMemoryAdditionalText("P.g4", grammar)], optionValue);

    /// <summary>Runs the source generator for in-memory grammars.</summary>
    private static GeneratorDriverRunResult RunGenerator(IReadOnlyList<AdditionalText> grammars, string? optionValue) => CreateDriver(grammars, optionValue).RunGenerators(CreateCompilation()).GetRunResult();

    /// <summary>Creates a test compilation.</summary>
    private static CSharpCompilation CreateCompilation() => CSharpCompilation.Create("GeneratedRuleArgumentBindingDiagnosticsTests", [CSharpSyntaxTree.ParseText("namespace Generated.Tests;", CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview))], [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    /// <summary>Creates a generator driver with global options.</summary>
    private static GeneratorDriver CreateDriver(IReadOnlyList<AdditionalText> grammars, string? optionValue) => CSharpGeneratorDriver.Create([new Antlr4GrammarGenerator().AsSourceGenerator()], grammars, parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview), optionsProvider: new DictionaryAnalyzerConfigOptionsProvider(CreateGlobalOptions(optionValue)));

    /// <summary>Creates analyzer config global options.</summary>
    private static ImmutableDictionary<string, string> CreateGlobalOptions(string? optionValue)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>();
        if (optionValue is not null) builder[Antlr4GrammarGeneratorOptions.EnableGeneratedRuleArgumentBindingKey] = optionValue;
        return builder.ToImmutable();
    }

    /// <summary>Asserts that exactly one binding diagnostic exists.</summary>
    private static Diagnostic AssertSingleBindingDiagnostic(GeneratorDriverRunResult result) => result.Diagnostics.Single(static diagnostic => diagnostic.Id == "APU0107");

    /// <summary>Asserts that no binding diagnostics exist.</summary>
    private static void AssertNoBindingDiagnostics(GeneratorDriverRunResult result) => Assert.AreEqual(0, result.Diagnostics.Count(static diagnostic => diagnostic.Id == "APU0107"));

    /// <summary>Analyzer config options backed by an immutable dictionary.</summary>
    private sealed class DictionaryAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly ImmutableDictionary<string, string> _values;
        public DictionaryAnalyzerConfigOptions(ImmutableDictionary<string, string> values) => _values = values;
        public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);
    }

    /// <summary>Analyzer config options provider with empty per-file options.</summary>
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
