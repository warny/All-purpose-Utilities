using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Generators;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies project-wide MSBuild options consumed by the ANTLR4 grammar source generator.
/// </summary>
[TestClass]
public class Antlr4GrammarGeneratorOptionsTests
{
    private const string BindingGrammar = """
        grammar P;

        @members {
            internal int Seen;
        }

        start : child[42] ;

        child[int value]
        @init {
            Seen = GetRequiredRuleParameter<int>(context, "value");
        }
            : A
            ;

        A : 'a';
        """;

    /// <summary>
    /// Verifies boolean option parsing defaults and accepted values.
    /// </summary>
    [DataTestMethod]
    [DataRow(null, false, 0)]
    [DataRow("", false, 0)]
    [DataRow("false", false, 0)]
    [DataRow("true", true, 0)]
    [DataRow("TRUE", true, 0)]
    [DataRow("  true  ", true, 0)]
    [DataRow("not-bool", false, 1)]
    public void Parse_UtilsParserEnableGeneratedRuleArgumentBinding_UsesDocumentedBooleanContract(string? value, bool expectedEnabled, int expectedDiagnosticCount)
    {
        var parseResult = Antlr4GrammarGeneratorOptions.Parse(new DictionaryAnalyzerConfigOptions(CreateGlobalOptions(value)));

        Assert.AreEqual(expectedEnabled, parseResult.Options.EnableGeneratedRuleArgumentBinding);
        Assert.AreEqual(expectedDiagnosticCount, parseResult.Diagnostics.Length);
        if (expectedDiagnosticCount == 1)
        {
            Assert.AreEqual("APU0106", parseResult.Diagnostics[0].Id);
            StringAssert.Contains(parseResult.Diagnostics[0].GetMessage(), "UtilsParserEnableGeneratedRuleArgumentBinding");
        }
    }

    /// <summary>
    /// Ensures absent and explicit false options produce equivalent conservative generated sources.
    /// </summary>
    [TestMethod]
    public void Generator_AbsentAndFalseOptions_DisableGeneratedRuleCallBinding()
    {
        GeneratorDriverRunResult absent = RunGenerator(BindingGrammar, null);
        GeneratorDriverRunResult disabled = RunGenerator(BindingGrammar, "false");

        string absentSource = GetSingleGeneratedSource(absent);
        string disabledSource = GetSingleGeneratedSource(disabled);

        Assert.AreEqual(absentSource, disabledSource);
        AssertBindingWrapperDisabled(absentSource);
        StringAssert.Contains(absentSource, "GetRequiredRuleParameter<int>");
    }

    /// <summary>
    /// Ensures the explicit true option enables the existing generated positional literal binding contract.
    /// </summary>
    [TestMethod]
    public void Generator_TrueOption_EnablesGeneratedRuleCallBinding()
    {
        GeneratorDriverRunResult runResult = RunGenerator(BindingGrammar, "true");
        string generatedSource = GetSingleGeneratedSource(runResult);

        AssertBindingWrapperEnabled(generatedSource);
        AssertGeneratedSourceExecutesBinding(generatedSource);
    }

    /// <summary>
    /// Ensures an invalid option reports a warning and keeps generation conservative.
    /// </summary>
    [TestMethod]
    public void Generator_InvalidOption_ReportsDiagnosticAndDisablesBinding()
    {
        GeneratorDriverRunResult runResult = RunGenerator(BindingGrammar, "sometimes");
        string generatedSource = GetSingleGeneratedSource(runResult);
        Diagnostic diagnostic = runResult.Diagnostics.Single(d => d.Id == "APU0106");

        Assert.AreEqual(DiagnosticSeverity.Warning, diagnostic.Severity);
        StringAssert.Contains(diagnostic.GetMessage(), "sometimes");
        AssertBindingWrapperDisabled(generatedSource);
    }

    /// <summary>
    /// Ensures an invalid project-wide option reports one warning even when multiple grammars are present.
    /// </summary>
    [TestMethod]
    public void Generator_InvalidOptionWithMultipleGrammarFiles_ReportsOneProjectWideDiagnostic()
    {
        GeneratorDriverRunResult runResult = RunGenerator([
            new InMemoryAdditionalText("P.g4", BindingGrammar),
            new InMemoryAdditionalText("Q.g4", BindingGrammar.Replace("grammar P;", "grammar Q;"))],
            "sometimes");

        Assert.AreEqual(1, runResult.Diagnostics.Count(diagnostic => diagnostic.Id == "APU0106"));
        Assert.AreEqual(2, runResult.GeneratedTrees.Length);
    }

    /// <summary>
    /// Ensures a project-wide true option applies to every grammar additional file.
    /// </summary>
    [TestMethod]
    public void Generator_TrueOption_AppliesToAllAdditionalFiles()
    {
        GeneratorDriverRunResult runResult = RunGenerator([
            new InMemoryAdditionalText("P.g4", BindingGrammar),
            new InMemoryAdditionalText("Q.g4", BindingGrammar.Replace("grammar P;", "grammar Q;"))],
            "true");

        Assert.AreEqual(2, runResult.GeneratedTrees.Length);
        foreach (SyntaxTree tree in runResult.GeneratedTrees)
        {
            AssertBindingWrapperEnabled(tree.ToString());
        }
    }

    /// <summary>
    /// Ensures changing only the project-wide option changes generated output in both directions.
    /// </summary>
    [TestMethod]
    public void Generator_OptionChange_InvalidatesGeneratedOutput()
    {
        var compilation = CreateGeneratorCompilation();
        GeneratorDriver driver = CreateGeneratorDriver([new InMemoryAdditionalText("P.g4", BindingGrammar)], "false");

        driver = driver.RunGenerators(compilation);
        string disabledSource = GetSingleGeneratedSource(driver.GetRunResult());

        driver = driver.WithUpdatedAnalyzerConfigOptions(new DictionaryAnalyzerConfigOptionsProvider(CreateGlobalOptions("true")));
        driver = driver.RunGenerators(compilation);
        string enabledSource = GetSingleGeneratedSource(driver.GetRunResult());

        driver = driver.WithUpdatedAnalyzerConfigOptions(new DictionaryAnalyzerConfigOptionsProvider(CreateGlobalOptions("false")));
        driver = driver.RunGenerators(compilation);
        string disabledAgainSource = GetSingleGeneratedSource(driver.GetRunResult());

        Assert.AreNotEqual(disabledSource, enabledSource);
        Assert.AreEqual(disabledSource, disabledAgainSource);
        AssertBindingWrapperEnabled(enabledSource);
        AssertBindingWrapperDisabled(disabledAgainSource);
    }

    /// <summary>
    /// Runs the generator for a single in-memory grammar and optional MSBuild option value.
    /// </summary>
    private static GeneratorDriverRunResult RunGenerator(string grammar, string? optionValue)
    {
        return RunGenerator([new InMemoryAdditionalText("P.g4", grammar)], optionValue);
    }

    /// <summary>
    /// Runs the generator for in-memory grammar files and optional MSBuild option value.
    /// </summary>
    private static GeneratorDriverRunResult RunGenerator(IReadOnlyList<AdditionalText> grammars, string? optionValue)
    {
        var compilation = CreateGeneratorCompilation();
        GeneratorDriver driver = CreateGeneratorDriver(grammars, optionValue);

        return driver.RunGenerators(compilation).GetRunResult();
    }

    /// <summary>
    /// Creates a compilation used to host source generator executions in tests.
    /// </summary>
    private static CSharpCompilation CreateGeneratorCompilation()
    {
        return CSharpCompilation.Create(
            "Antlr4GrammarGeneratorOptionsTests",
            syntaxTrees: [CSharpSyntaxTree.ParseText("namespace Generated.Tests;", CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview))],
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Creates a source generator driver with in-memory grammars and optional project-wide option value.
    /// </summary>
    private static GeneratorDriver CreateGeneratorDriver(IReadOnlyList<AdditionalText> grammars, string? optionValue)
    {
        return CSharpGeneratorDriver.Create(
            generators: [new Antlr4GrammarGenerator().AsSourceGenerator()],
            additionalTexts: grammars,
            parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview),
            optionsProvider: new DictionaryAnalyzerConfigOptionsProvider(CreateGlobalOptions(optionValue)));
    }

    /// <summary>
    /// Creates analyzer-config global options for the public generator MSBuild property.
    /// </summary>
    private static ImmutableDictionary<string, string> CreateGlobalOptions(string? optionValue)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>();
        if (optionValue != null)
        {
            builder[Antlr4GrammarGeneratorOptions.EnableGeneratedRuleArgumentBindingKey] = optionValue;
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Gets the only generated source from a generator run result.
    /// </summary>
    private static string GetSingleGeneratedSource(GeneratorDriverRunResult runResult)
    {
        return runResult.GeneratedTrees.Single().ToString();
    }

    /// <summary>
    /// Asserts that the generated source contains the generated rule-call binding wrapper.
    /// </summary>
    private static void AssertBindingWrapperEnabled(string source)
    {
        StringAssert.Contains(source, "GeneratedRuleCallExecutionPolicy");
        StringAssert.Contains(source, "TypedPositionalLiteralRuleCallExecutionPolicy");
        StringAssert.Contains(source, "RuleCallExecutionPolicy = enableGeneratedRuleArgumentBinding && true ? new GeneratedRuleCallExecutionPolicy(effectiveBase.RuleCallExecutionPolicy) : effectiveBase.RuleCallExecutionPolicy");
    }

    /// <summary>
    /// Asserts that the generated source keeps generated rule-call binding disabled.
    /// </summary>
    private static void AssertBindingWrapperDisabled(string source)
    {
        StringAssert.Contains(source, "TypedPositionalLiteralRuleCallExecutionPolicy");
        StringAssert.Contains(source, "RuleCallExecutionPolicy = enableGeneratedRuleArgumentBinding && false ? new GeneratedRuleCallExecutionPolicy(effectiveBase.RuleCallExecutionPolicy) : effectiveBase.RuleCallExecutionPolicy");
    }

    /// <summary>
    /// Compiles and executes generated source to verify the true option makes child arguments visible to <c>@init</c>.
    /// </summary>
    private static void AssertGeneratedSourceExecutesBinding(string source)
    {
        using var assemblyStream = new MemoryStream();
        CSharpCompilation compilation = CreateGeneratedSourceCompilation(source);
        var emitResult = compilation.Emit(assemblyStream);
        Diagnostic[] errors = emitResult.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.AreEqual(0, errors.Length, string.Join("\n", errors.Select(error => error.ToString())));

        assemblyStream.Position = 0;
        Assembly assembly = Assembly.Load(assemblyStream.ToArray());
        Type parserType = assembly.GetType("P", throwOnError: true)!;
        Type contextType = assembly.GetType("PExecutionContext", throwOnError: true)!;
        object executionContext = Activator.CreateInstance(contextType, nonPublic: true)!;
        MethodInfo parseMethod = parserType.GetMethod(
            "ParseWithEmbeddedCode",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(string), contextType],
            modifiers: null)!;

        _ = parseMethod.Invoke(null, ["a", executionContext]);
        FieldInfo seenField = contextType.GetField("Seen", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.AreEqual(42, seenField.GetValue(executionContext));
    }

    /// <summary>
    /// Creates a Roslyn compilation for generated source execution tests.
    /// </summary>
    private static CSharpCompilation CreateGeneratedSourceCompilation(string source)
    {
        MetadataReference[] references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .ToArray();

        return CSharpCompilation.Create(
            "GeneratedParserBindingCompilation",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview))],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Dictionary-backed analyzer config options used by tests.
    /// </summary>
    private sealed class DictionaryAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly ImmutableDictionary<string, string> _values;

        /// <summary>
        /// Initializes options with immutable key-value pairs.
        /// </summary>
        public DictionaryAnalyzerConfigOptions(ImmutableDictionary<string, string> values)
        {
            _values = values;
        }

        /// <inheritdoc />
        public override bool TryGetValue(string key, out string value)
        {
            return _values.TryGetValue(key, out value!);
        }
    }

    /// <summary>
    /// Analyzer config options provider with project-wide global options and empty per-tree options.
    /// </summary>
    private sealed class DictionaryAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _globalOptions;
        private readonly AnalyzerConfigOptions _emptyOptions = new DictionaryAnalyzerConfigOptions(ImmutableDictionary<string, string>.Empty);

        /// <summary>
        /// Initializes the provider with global options.
        /// </summary>
        public DictionaryAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string> globalOptions)
        {
            _globalOptions = new DictionaryAnalyzerConfigOptions(globalOptions);
        }

        /// <inheritdoc />
        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        /// <inheritdoc />
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return _emptyOptions;
        }

        /// <inheritdoc />
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return _emptyOptions;
        }
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
