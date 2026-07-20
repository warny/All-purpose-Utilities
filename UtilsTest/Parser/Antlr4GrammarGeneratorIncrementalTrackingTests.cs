using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Generators;

namespace UtilsTest.Parser;

/// <summary>
/// Tests Roslyn incremental tracking for ANTLR4 grammar generation independently of imported-rule binding semantics.
/// </summary>
[TestClass]
public sealed class Antlr4GrammarGeneratorIncrementalTrackingTests
{
    /// <summary>Verifies global option changes reuse parsed grammar files while rerunning project generation.</summary>
    [TestMethod]
    public void GlobalBindingOptionChange_ReusesParsedGrammarFilesAndUpdatesLocalDiagnostics()
    {
        var compilation = CreateCompilation();
        GeneratorDriver driver = CreateTrackingDriver([
            Grammar("Root.g4", "parser grammar Root; import Shared; start : child[bad] ; child[int value] : TOKEN ;"),
            Grammar("Shared.g4", "parser grammar Shared; imported[int value] : TOKEN ;"),
            Grammar("Independent.g4", "grammar Independent; start : A ; A : 'a' ;")], "false");

        driver = driver.RunGenerators(compilation);
        AssertStepReasons(driver.GetRunResult(), "ParseGrammarFile", IncrementalStepRunReason.New, 3);
        AssertNoBindingDiagnostics(driver.GetRunResult());

        driver = driver.WithUpdatedAnalyzerConfigOptions(new DictionaryAnalyzerConfigOptionsProvider(CreateGlobalOptions("true"))).RunGenerators(compilation);
        AssertStepReasons(driver.GetRunResult(), "ParseGrammarFile", IncrementalStepRunReason.Cached, 3);
        AssertStepContainsReason(driver.GetRunResult(), "GenerateGrammarProject", IncrementalStepRunReason.Modified);
        AssertSingleBindingDiagnostic(driver.GetRunResult());
        AssertTreeAbsent(driver.GetRunResult(), "Root");
        AssertTreePresent(driver.GetRunResult(), "Shared");
        AssertTreePresent(driver.GetRunResult(), "Independent");

        driver = driver.WithUpdatedAnalyzerConfigOptions(new DictionaryAnalyzerConfigOptionsProvider(CreateGlobalOptions("false"))).RunGenerators(compilation);
        AssertStepReasons(driver.GetRunResult(), "ParseGrammarFile", IncrementalStepRunReason.Cached, 3);
        AssertNoBindingDiagnostics(driver.GetRunResult());
        AssertTreePresent(driver.GetRunResult(), "Root");
    }

    /// <summary>Verifies editing an imported grammar reparses only that file without inventing imported binding diagnostics.</summary>
    [TestMethod]
    public void ImportedFileChange_ReparsesOnlyImportedFile()
    {
        var compilation = CreateCompilation();
        var root = Grammar("Root.g4", "parser grammar Root; import Shared; start : child[bad] ;");
        var shared = Grammar("Shared.g4", "parser grammar Shared; child[int value] : TOKEN ;");
        var independent = Grammar("Independent.g4", "grammar Independent; start : A ; A : 'a' ;");
        GeneratorDriver driver = CreateTrackingDriver([root, shared, independent], "true").RunGenerators(compilation);

        driver = driver.ReplaceAdditionalText(shared, Grammar("Shared.g4", "parser grammar Shared; child[int value, int other] : TOKEN ;")).RunGenerators(compilation);

        AssertParsedFiles(driver.GetRunResult(), ("Root.g4", IncrementalStepRunReason.Cached), ("Shared.g4", IncrementalStepRunReason.Modified), ("Independent.g4", IncrementalStepRunReason.Cached));
        AssertNoBindingDiagnostics(driver.GetRunResult());
        AssertTreePresent(driver.GetRunResult(), "Root");
        AssertTreePresent(driver.GetRunResult(), "Shared");
        AssertTreePresent(driver.GetRunResult(), "Independent");
    }

    /// <summary>Verifies editing the caller grammar reparses only the caller and refreshes local binding diagnostics.</summary>
    [TestMethod]
    public void CallerFileChange_ReparsesOnlyCallerFile()
    {
        var compilation = CreateCompilation();
        var root = Grammar("Root.g4", "parser grammar Root; import Shared; start : child[bad] ; child[int value] : TOKEN ;");
        var shared = Grammar("Shared.g4", "parser grammar Shared; imported[int value] : TOKEN ;");
        var independent = Grammar("Independent.g4", "grammar Independent; start : A ; A : 'a' ;");
        GeneratorDriver driver = CreateTrackingDriver([root, shared, independent], "true").RunGenerators(compilation);
        AssertSingleBindingDiagnostic(driver.GetRunResult());

        driver = driver.ReplaceAdditionalText(root, Grammar("Root.g4", "parser grammar Root; import Shared; start : child[1] ; child[int value] : TOKEN ;")).RunGenerators(compilation);

        AssertParsedFiles(driver.GetRunResult(), ("Root.g4", IncrementalStepRunReason.Modified), ("Shared.g4", IncrementalStepRunReason.Cached), ("Independent.g4", IncrementalStepRunReason.Cached));
        AssertNoBindingDiagnostics(driver.GetRunResult());
        AssertTreePresent(driver.GetRunResult(), "Root");
        AssertTreePresent(driver.GetRunResult(), "Shared");
        AssertTreePresent(driver.GetRunResult(), "Independent");
    }

    /// <summary>Verifies editing an independent grammar reparses only that grammar without changing imported-call diagnostics.</summary>
    [TestMethod]
    public void IndependentFileChange_ReparsesOnlyIndependentFile()
    {
        var compilation = CreateCompilation();
        var root = Grammar("Root.g4", "parser grammar Root; import Shared; start : child[bad] ;");
        var shared = Grammar("Shared.g4", "parser grammar Shared; child[int value] : TOKEN ;");
        var independent = Grammar("Independent.g4", "grammar Independent; start : A ; A : 'a' ;");
        GeneratorDriver driver = CreateTrackingDriver([root, shared, independent], "true").RunGenerators(compilation);
        AssertNoBindingDiagnostics(driver.GetRunResult());

        driver = driver.ReplaceAdditionalText(independent, Grammar("Independent.g4", "grammar Independent; start : B ; B : 'b' ;")).RunGenerators(compilation);

        AssertParsedFiles(driver.GetRunResult(), ("Root.g4", IncrementalStepRunReason.Cached), ("Shared.g4", IncrementalStepRunReason.Cached), ("Independent.g4", IncrementalStepRunReason.Modified));
        AssertNoBindingDiagnostics(driver.GetRunResult());
        AssertTreePresent(driver.GetRunResult(), "Root");
        AssertTreePresent(driver.GetRunResult(), "Shared");
    }

    /// <summary>Verifies changing metadata for one grammar reparses only that grammar and removes stale generated names.</summary>
    [TestMethod]
    public void MetadataChange_ReparsesOnlyChangedFileAndRemovesStaleGeneratedNames()
    {
        var compilation = CreateCompilation();
        var root = Grammar("Root.g4", "parser grammar Root; import Shared; start : child[1] ; child[int value] : TOKEN ;");
        var shared = Grammar("Shared.g4", "parser grammar Shared; imported[int value] : TOKEN ;");
        var metadata = ImmutableDictionary.CreateBuilder<string, ImmutableDictionary<string, string>>();
        metadata["Root.g4"] = CreateFileOptions(className: "RootGrammar", namespaceName: "Generated.One");
        GeneratorDriver driver = CreateTrackingDriver([root, shared], "true", metadata.ToImmutable()).RunGenerators(compilation);
        AssertTreePresent(driver.GetRunResult(), "RootGrammar");

        metadata["Root.g4"] = CreateFileOptions(className: "RenamedRootGrammar", namespaceName: "Generated.Two");
        driver = driver.WithUpdatedAnalyzerConfigOptions(new DictionaryAnalyzerConfigOptionsProvider(CreateGlobalOptions("true"), metadata.ToImmutable())).RunGenerators(compilation);

        AssertParsedFiles(driver.GetRunResult(), ("Root.g4", IncrementalStepRunReason.Modified), ("Shared.g4", IncrementalStepRunReason.Cached));
        SyntaxTree tree = AssertTreePresent(driver.GetRunResult(), "RenamedRootGrammar.Grammar.g.cs");
        StringAssert.Contains(tree.ToString(), "namespace Generated.Two");
        Assert.AreEqual(0, ResultGeneratedSources(driver.GetRunResult()).Count(source => source.Contains("namespace Generated.One")));
    }

    /// <summary>Verifies adding and removing an imported file refreshes outputs without stale generated trees.</summary>
    [TestMethod]
    public void AddAndRemoveImport_RefreshesAdditionalTextsWithoutStaleState()
    {
        var compilation = CreateCompilation();
        var root = Grammar("Root.g4", "parser grammar Root; import Shared; start : child[bad] ;");
        var shared = Grammar("Shared.g4", "parser grammar Shared; child[int value] : TOKEN ;");
        GeneratorDriver driver = CreateTrackingDriver([root], "true").RunGenerators(compilation);
        AssertNoBindingDiagnostics(driver.GetRunResult());
        AssertTreePresent(driver.GetRunResult(), "Root");
        AssertTreeAbsent(driver.GetRunResult(), "Shared");

        driver = driver.AddAdditionalTexts([shared]).RunGenerators(compilation);
        AssertParsedFiles(driver.GetRunResult(), ("Root.g4", IncrementalStepRunReason.Cached), ("Shared.g4", IncrementalStepRunReason.New));
        AssertNoBindingDiagnostics(driver.GetRunResult());
        AssertTreePresent(driver.GetRunResult(), "Root");
        AssertTreePresent(driver.GetRunResult(), "Shared");

        driver = driver.RemoveAdditionalTexts([shared]).RunGenerators(compilation);
        AssertParsedFiles(driver.GetRunResult(), ("Root.g4", IncrementalStepRunReason.Cached));
        AssertNoBindingDiagnostics(driver.GetRunResult());
        AssertTreePresent(driver.GetRunResult(), "Root");
        AssertTreeAbsent(driver.GetRunResult(), "Shared");
    }

    /// <summary>Creates an in-memory grammar additional file.</summary>
    private static InMemoryAdditionalText Grammar(string path, string text) => new(path, text);

    /// <summary>Creates a test compilation.</summary>
    private static CSharpCompilation CreateCompilation() => CSharpCompilation.Create("Antlr4GrammarGeneratorIncrementalTrackingTests", [CSharpSyntaxTree.ParseText("namespace Generated.Tests;", CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview))], [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    /// <summary>Creates a generator driver with Roslyn incremental-step tracking enabled.</summary>
    private static GeneratorDriver CreateTrackingDriver(IReadOnlyList<AdditionalText> grammars, string? optionValue, ImmutableDictionary<string, ImmutableDictionary<string, string>>? fileOptions = null)
    {
        return CSharpGeneratorDriver.Create(
            generators: [new Antlr4GrammarGenerator().AsSourceGenerator()],
            additionalTexts: grammars,
            parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview),
            optionsProvider: new DictionaryAnalyzerConfigOptionsProvider(CreateGlobalOptions(optionValue), fileOptions),
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));
    }

    /// <summary>Creates analyzer config global options.</summary>
    private static ImmutableDictionary<string, string> CreateGlobalOptions(string? optionValue)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>();
        if (optionValue is not null) builder[Antlr4GrammarGeneratorOptions.EnableGeneratedRuleArgumentBindingKey] = optionValue;
        return builder.ToImmutable();
    }

    /// <summary>Creates analyzer-config AdditionalFiles metadata for one grammar file.</summary>
    private static ImmutableDictionary<string, string> CreateFileOptions(string? className = null, string? namespaceName = null)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>();
        if (className is not null) builder["build_metadata.AdditionalFiles.ClassName"] = className;
        if (namespaceName is not null) builder["build_metadata.AdditionalFiles.Namespace"] = namespaceName;
        return builder.ToImmutable();
    }

    /// <summary>Gets binding diagnostics in generator output order.</summary>
    private static Diagnostic[] BindingDiagnostics(GeneratorDriverRunResult result) => result.Diagnostics.Where(static diagnostic => diagnostic.Id == "APU0107").ToArray();

    /// <summary>Asserts that exactly one binding diagnostic exists.</summary>
    private static Diagnostic AssertSingleBindingDiagnostic(GeneratorDriverRunResult result) => BindingDiagnostics(result).Single();

    /// <summary>Asserts that no binding diagnostics exist.</summary>
    private static void AssertNoBindingDiagnostics(GeneratorDriverRunResult result) => Assert.AreEqual(0, BindingDiagnostics(result).Length);

    /// <summary>Asserts every output from a tracked step has the expected reason and count.</summary>
    private static void AssertStepReasons(GeneratorDriverRunResult result, string stepName, IncrementalStepRunReason reason, int count)
    {
        var reasons = GetStepReasons(result, stepName);
        Assert.AreEqual(count, reasons.Length, stepName);
        Assert.IsTrue(reasons.All(actual => actual == reason), stepName + " reasons: " + string.Join(", ", reasons));
    }

    /// <summary>Asserts at least one output from a tracked step has the expected reason.</summary>
    private static void AssertStepContainsReason(GeneratorDriverRunResult result, string stepName, IncrementalStepRunReason reason)
    {
        var reasons = GetStepReasons(result, stepName);
        Assert.IsTrue(reasons.Contains(reason), stepName + " reasons: " + string.Join(", ", reasons));
    }

    /// <summary>Asserts parser tracking reasons by grammar file path.</summary>
    private static void AssertParsedFiles(GeneratorDriverRunResult result, params (string Path, IncrementalStepRunReason Reason)[] expected)
    {
        var actual = GetStepOutputs(result, "ParseGrammarFile")
            .ToDictionary(output => GetProperty<string>(output.Value, "Path"), output => output.Reason, StringComparer.Ordinal);
        foreach (var item in expected)
        {
            Assert.IsTrue(actual.TryGetValue(item.Path, out var reason), item.Path);
            Assert.AreEqual(item.Reason, reason, item.Path);
        }
    }

    /// <summary>Gets the output reasons for one tracked incremental step.</summary>
    private static IncrementalStepRunReason[] GetStepReasons(GeneratorDriverRunResult result, string stepName) => GetStepOutputs(result, stepName).Select(output => output.Reason).ToArray();

    /// <summary>Gets tracked step outputs from the first generator result.</summary>
    private static ImmutableArray<(object Value, IncrementalStepRunReason Reason)> GetStepOutputs(GeneratorDriverRunResult result, string stepName)
    {
        Assert.IsTrue(result.Results[0].TrackedSteps.TryGetValue(stepName, out var steps), stepName);
        return steps.SelectMany(step => step.Outputs).ToImmutableArray();
    }

    /// <summary>Reads a generated pipeline property by name for test assertions.</summary>
    private static T GetProperty<T>(object value, string name) => (T)value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(value)!;

    /// <summary>Gets generated source text snapshots from the current run result.</summary>
    private static IEnumerable<string> ResultGeneratedSources(GeneratorDriverRunResult result) => result.GeneratedTrees.Select(tree => tree.ToString());

    /// <summary>Asserts a generated tree with a hint-name fragment is present.</summary>
    private static SyntaxTree AssertTreePresent(GeneratorDriverRunResult result, string hintName)
    {
        SyntaxTree? tree = result.GeneratedTrees.SingleOrDefault(tree => tree.FilePath.Contains(hintName));
        Assert.IsNotNull(tree, hintName);
        return tree;
    }

    /// <summary>Asserts no generated tree with a hint-name fragment is present.</summary>
    private static void AssertTreeAbsent(GeneratorDriverRunResult result, string hintName) => Assert.AreEqual(0, result.GeneratedTrees.Count(tree => tree.FilePath.Contains(hintName)), hintName);

    /// <summary>Analyzer config options backed by an immutable dictionary.</summary>
    private sealed class DictionaryAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly ImmutableDictionary<string, string> _values;

        /// <summary>Initializes the option map.</summary>
        public DictionaryAnalyzerConfigOptions(ImmutableDictionary<string, string> values) => _values = values;

        /// <inheritdoc />
        public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);
    }

    /// <summary>Analyzer config options provider with optional per-file options.</summary>
    private sealed class DictionaryAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _globalOptions;
        private readonly ImmutableDictionary<string, ImmutableDictionary<string, string>> _fileOptions;
        private readonly AnalyzerConfigOptions _emptyOptions = new DictionaryAnalyzerConfigOptions(ImmutableDictionary<string, string>.Empty);

        /// <summary>Initializes the provider with global and optional per-file options.</summary>
        public DictionaryAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string> globalOptions, ImmutableDictionary<string, ImmutableDictionary<string, string>>? fileOptions = null)
        {
            _globalOptions = new DictionaryAnalyzerConfigOptions(globalOptions);
            _fileOptions = fileOptions ?? ImmutableDictionary<string, ImmutableDictionary<string, string>>.Empty;
        }

        /// <inheritdoc />
        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        /// <inheritdoc />
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _emptyOptions;

        /// <inheritdoc />
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _fileOptions.TryGetValue(textFile.Path, out var options) ? new DictionaryAnalyzerConfigOptions(options) : _emptyOptions;
    }

    /// <summary>In-memory additional text used as a grammar file.</summary>
    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        /// <summary>Initializes the grammar file with a path and text.</summary>
        public InMemoryAdditionalText(string path, string text)
        {
            Path = path;
            _text = SourceText.From(text);
        }

        /// <inheritdoc />
        public override string Path { get; }

        /// <inheritdoc />
        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }
}
