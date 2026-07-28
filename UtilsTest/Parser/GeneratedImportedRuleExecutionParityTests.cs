using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Generators;
using Utils.Parser.Resolution;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies generated imported-rule diagnostics stay aligned with generated parser execution capabilities.
/// </summary>
[TestClass]
public sealed class GeneratedImportedRuleExecutionParityTests
{
    /// <summary>Verifies a directly imported parser and lexer rule are emitted and execute through the generated facade.</summary>
    [TestMethod]
    public void ImportedRuleCall_IsEmittedAndExecutes()
    {
        GeneratorDriverRunResult result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; import Shared; start : child[1] ;"),
            Grammar("Shared.g4", "grammar Shared; child[int value] : TOKEN ; TOKEN : 'a' ;")]);

        AssertNoBindingDiagnostics(result);
        Assert.AreEqual(2, result.GeneratedTrees.Length);
        StringAssert.Contains(GetGeneratedSource(result, "Root"), "new GrammarImport(\"Shared\"");

        Assembly assembly = CompileGeneratedSources(result);
        Type root = assembly.GetTypes().Single(type => type.Name == "Root");
        MethodInfo build = root.GetMethod("Build", BindingFlags.Public | BindingFlags.Static)!;
        var definition = (Utils.Parser.Model.ParserDefinition)build.Invoke(null, null)!;
        CollectionAssert.IsSubsetOf(new[] { "start", "child", "TOKEN" }, definition.AllRules.Keys.ToArray());
        MethodInfo parse = root.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!;
        Assert.IsNotNull(parse.Invoke(null, new object[] { "a" }));
    }

    /// <summary>Verifies an entry without a local parser rule does not promote an imported parser rule to root.</summary>
    [TestMethod]
    public void EntryWithoutLocalParserRule_ImportedRuleDoesNotBecomeRoot()
    {
        GeneratorDriverRunResult result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; import Shared;"),
            Grammar("Shared.g4", "parser grammar Shared; child : TOKEN ;")]);

        string source = GetGeneratedSource(result, "Root");
        StringAssert.Contains(source, "ParserRules: new Rule[] {_child }");
        StringAssert.Contains(source, "RootRule: null");
    }

    /// <summary>Verifies imported mode declarations remain in the effective definition even when they contain no rules.</summary>
    [TestMethod]
    public void ImportedEmptyLexerMode_IsEmitted()
    {
        GeneratorDriverRunResult result = RunGenerator([
            Grammar("Root.g4", "grammar Root; import Shared; start : TOKEN ; TOKEN : 'a' ;"),
            Grammar("Shared.g4", "lexer grammar Shared; mode EMPTY;")]);

        AssertNoBindingDiagnostics(result);
        Assembly assembly = CompileGeneratedSources(result);
        MethodInfo build = assembly.GetTypes().Single(type => type.Name == "Root").GetMethod("Build", BindingFlags.Public | BindingFlags.Static)!;
        var definition = (Utils.Parser.Model.ParserDefinition)build.Invoke(null, null)!;
        Assert.IsTrue(definition.Modes.Any(mode => mode.Name == "EMPTY" && mode.Rules.Count == 0));
    }

    /// <summary>Verifies generated definitions retain the two built-in channels alongside composed channel declarations.</summary>
    [TestMethod]
    public void DeclaredChannels_IncludeBuiltInAndComposedChannels()
    {
        GeneratorDriverRunResult result = RunGenerator([
            Grammar("Root.g4", "lexer grammar Root; channels { COMMENTS } TOKEN : 'a' ;")]);

        Assembly assembly = CompileGeneratedSources(result);
        MethodInfo build = assembly.GetTypes().Single(type => type.Name == "Root").GetMethod("Build", BindingFlags.Public | BindingFlags.Static)!;
        var definition = (Utils.Parser.Model.ParserDefinition)build.Invoke(null, null)!;
        CollectionAssert.AreEquivalent(new[] { "DEFAULT_CHANNEL", "HIDDEN", "COMMENTS" }, definition.DeclaredChannels.ToArray());
    }

    /// <summary>Verifies a lexer rule declared locally in a parser grammar is rejected before effective import projection.</summary>
    [TestMethod]
    public void ParserGrammarWithLocalLexerRule_IsRejected()
    {
        GeneratorDriverRunResult result = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; start : TOKEN ; TOKEN : 'a' ;")]);

        Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Id == "UP0013"));
        Assert.AreEqual(0, result.GeneratedTrees.Length);
    }

    /// <summary>Verifies a parser grammar enables external lexer rules only when a lexer dependency contributes one.</summary>
    [TestMethod]
    public void ParserGrammarWithImportedLexerRule_EnablesExternalLexerRules()
    {
        GeneratorDriverRunResult imported = RunGenerator([
            Grammar("Root.g4", "parser grammar Root; options { tokenVocab=Tokens; } start : TOKEN ;"),
            Grammar("Tokens.g4", "lexer grammar Tokens; TOKEN : 'a' ;")]);
        GeneratorDriverRunResult localOnly = RunGenerator([
            Grammar("Local.g4", "parser grammar Local; start : child ; child : ;")]);

        StringAssert.Contains(GetGeneratedSource(imported, "Root"), "AllowExternalLexerRules = true");
        StringAssert.Contains(GetGeneratedSource(localOnly, "Local"), "AllowExternalLexerRules = false");
    }

    /// <summary>Verifies local parser-rule priority is the only generated binding target and executes through the generated definition.</summary>
    [TestMethod]
    public void LocalRuleShadowsImportedRule_ReportsLocalBindingDiagnosticAndBuildsAfterValidLocalCall()
    {
        GeneratorDriverRunResult invalid = RunGenerator([
            Grammar("Root.g4", "grammar Root; import Shared; start : child[bad] ; child[int value] : TOKEN ; TOKEN : 'a' ;"),
            Grammar("Shared.g4", "parser grammar Shared; child[string value] : TOKEN ;")]);

        Assert.AreEqual(1, BindingDiagnostics(invalid).Length);
        Assert.AreEqual(1, invalid.GeneratedTrees.Length);
        StringAssert.Contains(invalid.GeneratedTrees[0].FilePath, "Shared");

        GeneratorDriverRunResult valid = RunGenerator([
            Grammar("Root.g4", "grammar Root; import Shared; start : child[1] ; child[int value] : TOKEN ; TOKEN : 'a' ;"),
            Grammar("Shared.g4", "parser grammar Shared; child[string value] : TOKEN ;")]);

        AssertNoBindingDiagnostics(valid);
        Assembly assembly = CompileGeneratedSources(valid);
        MethodInfo build = assembly.GetTypes().Single(type => type.Name == "Root").GetMethod("Build", BindingFlags.Public | BindingFlags.Static)!;
        Assert.IsNotNull(build.Invoke(null, null));
    }

    /// <summary>Verifies transitive, ambiguous, missing, aliased, lexer, and duplicate-name imports do not produce binding conclusions.</summary>
    [DataTestMethod]
    [DataRow("Transitive", 1, "parser grammar Root; import Middle; start : child[bad] ;", "parser grammar Middle; import Shared;", "parser grammar Shared; child[int value] : TOKEN ;")]
    [DataRow("Ambiguous", 0, "parser grammar Root; import A, B; start : child[bad] ;", "parser grammar A; child[int value] : TOKEN ;", "parser grammar B; child[int value] : TOKEN ;")]
    [DataRow("Missing", 0, "parser grammar Root; import Missing; start : child[bad] ;", "parser grammar Other; other : TOKEN ;", "parser grammar Shared; child[int value] : TOKEN ;")]
    [DataRow("Aliased", 1, "parser grammar Root; import Alias=Shared; start : child[bad] ;", "parser grammar Shared; child[int value] : TOKEN ;", "parser grammar Other; other : TOKEN ;")]
    [DataRow("Lexer", 0, "parser grammar Root; import Tokens; start : TOKEN[bad] ;", "lexer grammar Tokens; TOKEN : 'a' ;", "parser grammar Other; other : TOKEN ;")]
    [DataRow("DuplicateName", 0, "parser grammar Root; import Shared; start : child[bad] ;", "parser grammar Shared; child[int value] : TOKEN ;", "parser grammar Shared; child[int value] : TOKEN ;")]
    public void ImportedRuleScenarios_ReportOnlyCertainEffectiveTargets(string scenario, int expectedCount, string root, string first, string second)
    {
        GeneratorDriverRunResult result = RunGenerator([
            Grammar("Root.g4", root),
            Grammar($"{scenario}One.g4", first),
            Grammar($"{scenario}Two.g4", second)]);

        Assert.AreEqual(expectedCount, BindingDiagnostics(result).Length, scenario);
    }

    /// <summary>Runs the generator with generated rule-argument binding enabled.</summary>
    private static GeneratorDriverRunResult RunGenerator(IReadOnlyList<AdditionalText> grammars)
    {
        var options = new DictionaryAnalyzerConfigOptionsProvider(ImmutableDictionary.CreateRange(new[] { new KeyValuePair<string, string>(Antlr4GrammarGeneratorOptions.EnableGeneratedRuleArgumentBindingKey, "true") }));
        var driver = CSharpGeneratorDriver.Create([new Antlr4GrammarGenerator().AsSourceGenerator()], grammars, parseOptions: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview), optionsProvider: options);
        return driver.RunGenerators(CreateCompilation()).GetRunResult();
    }

    /// <summary>Creates an in-memory grammar file.</summary>
    private static InMemoryAdditionalText Grammar(string path, string text) => new(path, text);

    /// <summary>Creates a compilation used by the source generator driver.</summary>
    private static CSharpCompilation CreateCompilation() => CSharpCompilation.Create("GeneratedImportedRuleExecutionParityTests", [CSharpSyntaxTree.ParseText("namespace Generated.Tests;", CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview))], References(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    /// <summary>Compiles generated sources and returns the produced in-memory assembly.</summary>
    private static Assembly CompileGeneratedSources(GeneratorDriverRunResult result)
    {
        var syntaxTrees = result.GeneratedTrees.Select(tree => CSharpSyntaxTree.ParseText(tree.GetText().ToString(), CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview), tree.FilePath)).ToArray();
        CSharpCompilation compilation = CSharpCompilation.Create("GeneratedParsers", syntaxTrees, References(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.IsTrue(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        stream.Position = 0;
        return Assembly.Load(stream.ToArray());
    }

    /// <summary>Gets all metadata references needed to compile generated parser sources.</summary>
    private static MetadataReference[] References() => AppDomain.CurrentDomain.GetAssemblies()
        .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
        .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
        .ToArray();

    /// <summary>Gets the generated source for a grammar class by generated file-path fragment.</summary>
    private static string GetGeneratedSource(GeneratorDriverRunResult result, string pathFragment) => result.GeneratedTrees.Single(tree => tree.FilePath.Contains(pathFragment)).GetText().ToString();

    /// <summary>Gets generated binding diagnostics.</summary>
    private static Diagnostic[] BindingDiagnostics(GeneratorDriverRunResult result) => result.Diagnostics.Where(static diagnostic => diagnostic.Id == "APU0107").ToArray();

    /// <summary>Asserts that no generated binding diagnostics were emitted.</summary>
    private static void AssertNoBindingDiagnostics(GeneratorDriverRunResult result) => Assert.AreEqual(0, BindingDiagnostics(result).Length);

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

        /// <summary>Initializes the provider with global options.</summary>
        public DictionaryAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string> globalOptions) => _globalOptions = new DictionaryAnalyzerConfigOptions(globalOptions);

        /// <inheritdoc />
        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        /// <inheritdoc />
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _emptyOptions;

        /// <inheritdoc />
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _emptyOptions;
    }

    /// <summary>Represents an in-memory additional text grammar file.</summary>
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
