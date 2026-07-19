using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Generators;

namespace UtilsTest.Parser;

/// <summary>
/// Tests project-wide imported parser-rule argument binding diagnostics for the ANTLR4 source generator.
/// </summary>
[TestClass]
public sealed class GeneratedImportedRuleArgumentBindingDiagnosticsTests
{
    /// <summary>Verifies imported binding validation remains disabled unless the documented option is true.</summary>
    [DataTestMethod]
    [DataRow(null, 0)]
    [DataRow("false", 0)]
    [DataRow("true", 1)]
    public void ImportedBindingDiagnostics_RespectOptIn(string? optionValue, int expectedDiagnostics)
    {
        var result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; import Shared; start : child[bad] ;"),
            Grammar("Shared.g4", "parser grammar Shared; child[int value] : TOKEN ;")], optionValue);

        Assert.AreEqual(expectedDiagnostics, BindingDiagnostics(result).Length);
    }

    /// <summary>Verifies a directly imported parser rule is validated at the caller call site.</summary>
    [TestMethod]
    public void DirectImport_InvalidArgument_ReportsCallerDiagnostic()
    {
        var result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; import Shared; start : child[bad] ;"),
            Grammar("Shared.g4", "parser grammar Shared; child[int value] : TOKEN ;")], "true");

        var diagnostic = AssertSingleBindingDiagnostic(result);
        StringAssert.Contains(diagnostic.GetMessage(), "Argument 0 is not a supported simple literal.");
        Assert.AreEqual("Root.g4", diagnostic.Location.GetLineSpan().Path);
        Assert.AreEqual(0, result.GeneratedTrees.Count(tree => tree.FilePath.Contains("Root")));
        Assert.AreEqual(1, result.GeneratedTrees.Count(tree => tree.FilePath.Contains("Shared")));
    }

    /// <summary>Verifies a transitively imported parser rule is validated when every grammar name is uniquely resolvable.</summary>
    [TestMethod]
    public void TransitiveImport_InvalidArity_ReportsDiagnostic()
    {
        var result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; import Middle; start : child[] ;"),
            Grammar("Middle.g4", "parser grammar Middle; import Shared;"),
            Grammar("Shared.g4", "parser grammar Shared; child[int value] : TOKEN ;")], "true");

        var diagnostic = AssertSingleBindingDiagnostic(result);
        StringAssert.Contains(diagnostic.GetMessage(), "Expected exactly 1 positional argument(s), but received 0.");
    }

    /// <summary>Verifies local parser rules retain priority over imported parser rules with the same name.</summary>
    [TestMethod]
    public void LocalRule_ShadowsImportedRule()
    {
        var result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; import Shared; start : child[1] ; child[int value] : TOKEN ;"),
            Grammar("Shared.g4", "parser grammar Shared; child[string value] : TOKEN ;")], "true");

        AssertNoBindingDiagnostics(result);
        Assert.AreEqual(2, result.GeneratedTrees.Length);
    }

    /// <summary>Verifies absent imports and absent rules remain unresolved metadata rather than diagnostics.</summary>
    [TestMethod]
    public void MissingImportOrRule_DoesNotReportDiagnostic()
    {
        var result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; import Missing, Shared; start : missing[bad] | absent[bad] ;"),
            Grammar("Shared.g4", "parser grammar Shared; other[int value] : TOKEN ;")], "true");

        AssertNoBindingDiagnostics(result);
    }

    /// <summary>Verifies ambiguous direct imports do not invent a priority and therefore do not report APU0107.</summary>
    [TestMethod]
    public void AmbiguousDirectImports_DoNotReportDiagnostic()
    {
        var result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; import A, B; start : child[bad] ;"),
            Grammar("A.g4", "parser grammar A; child[int value] : TOKEN ;"),
            Grammar("B.g4", "parser grammar B; child[int value] : TOKEN ;")], "true");

        AssertNoBindingDiagnostics(result);
    }

    /// <summary>Verifies a direct/transitive collision is ambiguous and ignored by static binding diagnostics.</summary>
    [TestMethod]
    public void DirectAndTransitiveCollision_DoNotReportDiagnostic()
    {
        var result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; import A, B; start : child[bad] ;"),
            Grammar("A.g4", "parser grammar A; import Shared;"),
            Grammar("B.g4", "parser grammar B; child[int value] : TOKEN ;"),
            Grammar("Shared.g4", "parser grammar Shared; child[int value] : TOKEN ;")], "true");

        AssertNoBindingDiagnostics(result);
    }

    /// <summary>Verifies a diamond import graph resolves to one imported declaration when both paths reach the same rule instance.</summary>
    [TestMethod]
    public void DiamondImport_SameRuleReachedTwice_IsResolved()
    {
        var result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; import A, B; start : child[bad] ;"),
            Grammar("A.g4", "parser grammar A; import Shared;"),
            Grammar("B.g4", "parser grammar B; import Shared;"),
            Grammar("Shared.g4", "parser grammar Shared; child[int value] : TOKEN ;")], "true");

        var diagnostic = AssertSingleBindingDiagnostic(result);
        Assert.AreEqual("Root.g4", diagnostic.Location.GetLineSpan().Path);
        StringAssert.Contains(diagnostic.GetMessage(), "Argument 0 is not a supported simple literal.");
    }

    /// <summary>Verifies direct and transitive paths to the same imported declaration do not create artificial ambiguity.</summary>
    [TestMethod]
    public void DirectAndTransitivePathToSameDeclaration_IsResolved()
    {
        var result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; import A, Shared; start : child[bad] ;"),
            Grammar("A.g4", "parser grammar A; import Shared;"),
            Grammar("Shared.g4", "parser grammar Shared; child[int value] : TOKEN ;")], "true");

        var diagnostic = AssertSingleBindingDiagnostic(result);
        Assert.AreEqual("Root.g4", diagnostic.Location.GetLineSpan().Path);
    }

    /// <summary>Verifies importing the same grammar name more than once does not create artificial ambiguity.</summary>
    [TestMethod]
    public void DuplicateImportOfSameGrammar_DoesNotBecomeAmbiguous()
    {
        var result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; import Shared, Shared; start : child[bad] ;"),
            Grammar("Shared.g4", "parser grammar Shared; child[int value] : TOKEN ;")], "true");

        var diagnostic = AssertSingleBindingDiagnostic(result);
        Assert.AreEqual("Root.g4", diagnostic.Location.GetLineSpan().Path);
    }

    /// <summary>Verifies import cycles terminate deterministically without duplicated candidates.</summary>
    [TestMethod]
    public void ImportCycle_TerminatesAndKeepsGeneration()
    {
        var result = RunGenerator([
            Grammar("A.g4", "parser grammar A; import B; start : child[bad] ;"),
            Grammar("B.g4", "parser grammar B; import A;")], "true");

        AssertNoBindingDiagnostics(result);
        Assert.AreEqual(2, result.GeneratedTrees.Length);
    }

    /// <summary>Verifies imports resolve by declared grammar names rather than file names.</summary>
    [TestMethod]
    public void DeclaredGrammarName_DrivesResolution()
    {
        var result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; import Shared; start : child[bad] ;"),
            Grammar("DifferentFileName.g4", "parser grammar Shared; child[int value] : TOKEN ;")], "true");

        AssertSingleBindingDiagnostic(result);
    }

    /// <summary>Verifies duplicate declared grammar names are ambiguous and ignored conservatively.</summary>
    [TestMethod]
    public void DuplicateDeclaredGrammarName_IsAmbiguous()
    {
        var result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; import Shared; start : child[bad] ;"),
            Grammar("One.g4", "parser grammar Shared; child[int value] : TOKEN ;"),
            Grammar("Two.g4", "parser grammar Shared; child[int value] : TOKEN ;")], "true");

        AssertNoBindingDiagnostics(result);
    }

    /// <summary>Verifies aliased imports are preserved but not resolved by the static imported-call resolver.</summary>
    [TestMethod]
    public void AliasedImport_IsNotResolvedForBindingDiagnostics()
    {
        var result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; import Alias=Shared; start : child[bad] ;"),
            Grammar("Shared.g4", "parser grammar Shared; child[int value] : TOKEN ;")], "true");

        AssertNoBindingDiagnostics(result);
    }

    /// <summary>Verifies lexer grammars and lexer-domain rules are not binding targets.</summary>
    [TestMethod]
    public void LexerImport_DoesNotProvideParserRuleTarget()
    {
        var result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; import Tokens; start : TOKEN[bad] ;"),
            Grammar("Tokens.g4", "lexer grammar Tokens; TOKEN : 'a' ;")], "true");

        AssertNoBindingDiagnostics(result);
    }

    /// <summary>Verifies imported targets reuse representative exact binding validation failures.</summary>
    [DataTestMethod]
    [DataRow("child[]", "int value", "Expected exactly 1 positional argument(s), but received 0.")]
    [DataRow("child[1, 2]", "int value", "Expected exactly 1 positional argument(s), but received 2.")]
    [DataRow("child[value: 42]", "int value", "Named rule-call arguments are not supported.")]
    [DataRow("child[1, value: 2]", "int value, int other", "Named rule-call arguments are not supported.")]
    [DataRow("child[1 + 2]", "int value", "Argument 0 is not a supported simple literal.")]
    [DataRow("child[\"text\"]", "int value", "Argument 0 cannot bind to declared type 'int'.")]
    [DataRow("child[300]", "byte value", "Argument 0 cannot bind to declared type 'byte'.")]
    [DataRow("child[1]", "CustomType value", "Target parameter 'value' uses unsupported declared type 'CustomType'.")]
    [DataRow("child[1, 2]", "int value, int value", "Target parameter name 'value' is duplicated.")]
    [DataRow("child[1]", "int", "The declared parameter type is unavailable or does not use the supported")]
    public void ImportedTarget_ReusesBindingValidator(string call, string parameters, string reason)
    {
        var result = RunGenerator([
            Grammar("Root.g4", $"parser grammar Root; import Shared; start : {call} ;"),
            Grammar("Shared.g4", $"parser grammar Shared; child[{parameters}] : TOKEN ;")], "true");

        var diagnostic = AssertSingleBindingDiagnostic(result);
        StringAssert.Contains(diagnostic.GetMessage(), reason);
    }

    /// <summary>Verifies only the invalid caller stops emitting while imported and independent files continue.</summary>
    [TestMethod]
    public void InvalidCaller_DoesNotSuppressImportedOrIndependentOutputs()
    {
        var result = RunGenerator([
            Grammar("Caller.g4", "parser grammar Caller; import Shared; start : child[bad] ;"),
            Grammar("Shared.g4", "parser grammar Shared; child[int value] : TOKEN ;"),
            Grammar("Independent.g4", "grammar Independent; start : A ; A : 'a' ;")], "true");

        AssertSingleBindingDiagnostic(result);
        Assert.AreEqual(0, result.GeneratedTrees.Count(tree => tree.FilePath.Contains("Caller")));
        Assert.AreEqual(1, result.GeneratedTrees.Count(tree => tree.FilePath.Contains("Shared")));
        Assert.AreEqual(1, result.GeneratedTrees.Count(tree => tree.FilePath.Contains("Independent")));
    }

    /// <summary>Verifies diagnostic ordering is stable across input order changes.</summary>
    [TestMethod]
    public void MultipleInvalidCallers_ReportStableDiagnosticsIndependentOfInputOrder()
    {
        var first = BindingDiagnostics(RunGenerator([
            Grammar("BRoot.g4", "parser grammar BRoot; import Shared; start : child[bad] ;"),
            Grammar("ARoot.g4", "parser grammar ARoot; import Shared; start : child[bad] ;"),
            Grammar("Shared.g4", "parser grammar Shared; child[int value] : TOKEN ;")], "true")).Select(d => d.Location.GetLineSpan().Path).ToArray();
        var second = BindingDiagnostics(RunGenerator([
            Grammar("Shared.g4", "parser grammar Shared; child[int value] : TOKEN ;"),
            Grammar("ARoot.g4", "parser grammar ARoot; import Shared; start : child[bad] ;"),
            Grammar("BRoot.g4", "parser grammar BRoot; import Shared; start : child[bad] ;")], "true")).Select(d => d.Location.GetLineSpan().Path).ToArray();

        CollectionAssert.AreEqual(first, second);
        CollectionAssert.AreEqual(new[] { "ARoot.g4", "BRoot.g4" }, first);
    }

    /// <summary>Verifies an imported signature edit invalidates the caller and a later fix restores output.</summary>
    [TestMethod]
    public void Incremental_ImportedSignatureChange_TogglesCallerDiagnosticAndSource()
    {
        var compilation = CreateCompilation();
        var root = Grammar("Root.g4", "parser grammar Root; import Shared; start : child[1] ;");
        var shared = Grammar("Shared.g4", "parser grammar Shared; child[int value] : TOKEN ;");
        GeneratorDriver driver = CreateDriver([root, shared], "true").RunGenerators(compilation);
        AssertNoBindingDiagnostics(driver.GetRunResult());
        Assert.AreEqual(2, driver.GetRunResult().GeneratedTrees.Length);

        driver = driver.ReplaceAdditionalText(shared, Grammar("Shared.g4", "parser grammar Shared; child[int value, int other] : TOKEN ;"));
        driver = driver.RunGenerators(compilation);
        AssertSingleBindingDiagnostic(driver.GetRunResult());
        Assert.AreEqual(1, driver.GetRunResult().GeneratedTrees.Length);

        driver = driver.ReplaceAdditionalText(root, Grammar("Root.g4", "parser grammar Root; import Shared; start : child[1, 2] ;"));
        driver = driver.RunGenerators(compilation);
        AssertNoBindingDiagnostics(driver.GetRunResult());
        Assert.AreEqual(2, driver.GetRunResult().GeneratedTrees.Length);
    }

    /// <summary>Verifies option changes with imports do not leave stale diagnostics or generated trees.</summary>
    [TestMethod]
    public void Incremental_OptionFalseTrueFalse_WithImports_TogglesCleanly()
    {
        var compilation = CreateCompilation();
        GeneratorDriver driver = CreateDriver([
            Grammar("Root.g4", "parser grammar Root; import Shared; start : child[bad] ;"),
            Grammar("Shared.g4", "parser grammar Shared; child[int value] : TOKEN ;")], "false").RunGenerators(compilation);
        AssertNoBindingDiagnostics(driver.GetRunResult());

        driver = driver.WithUpdatedAnalyzerConfigOptions(new DictionaryAnalyzerConfigOptionsProvider(CreateGlobalOptions("true"))).RunGenerators(compilation);
        AssertSingleBindingDiagnostic(driver.GetRunResult());

        driver = driver.WithUpdatedAnalyzerConfigOptions(new DictionaryAnalyzerConfigOptionsProvider(CreateGlobalOptions("false"))).RunGenerators(compilation);
        AssertNoBindingDiagnostics(driver.GetRunResult());
        Assert.AreEqual(2, driver.GetRunResult().GeneratedTrees.Length);
    }

    /// <summary>Creates an in-memory grammar additional file.</summary>
    private static InMemoryAdditionalText Grammar(string path, string text) => new(path, text);

    /// <summary>Runs the source generator for in-memory grammars.</summary>
    private static GeneratorDriverRunResult RunGenerator(IReadOnlyList<AdditionalText> grammars, string? optionValue) => CreateDriver(grammars, optionValue).RunGenerators(CreateCompilation()).GetRunResult();

    /// <summary>Creates a test compilation.</summary>
    private static CSharpCompilation CreateCompilation() => CSharpCompilation.Create("GeneratedImportedRuleArgumentBindingDiagnosticsTests", [CSharpSyntaxTree.ParseText("namespace Generated.Tests;", CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview))], [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    /// <summary>Creates a generator driver with global options.</summary>
    private static GeneratorDriver CreateDriver(IReadOnlyList<AdditionalText> grammars, string? optionValue) => CSharpGeneratorDriver.Create([new Antlr4GrammarGenerator().AsSourceGenerator()], grammars, parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview), optionsProvider: new DictionaryAnalyzerConfigOptionsProvider(CreateGlobalOptions(optionValue)));

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
