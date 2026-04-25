using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Diagnostics;
using Utils.Parser.ProjectCompilation;
using Utils.Parser.Resolution;

namespace UtilsTest.Parser;

/// <summary>
/// Tests for ANTLR4 multi-grammar project compilation.
/// </summary>
[TestClass]
public class Antlr4GrammarProjectCompilerTests
{
    /// <summary>
    /// Verifies that parsing a standalone combined grammar remains unchanged.
    /// </summary>
    [TestMethod]
    public void Parse_CombinedGrammarWithoutImports_BehaviorUnchanged()
    {
        var resolver = CreateResolver(("A", "grammar A; start : ID ; ID : 'a' ;"));

        var definition = Antlr4GrammarProjectCompiler.Parse("A", resolver);

        Assert.IsTrue(definition.AllRules.ContainsKey("start"));
        Assert.IsTrue(definition.AllRules.ContainsKey("ID"));
        Assert.AreEqual("start", definition.RootRule?.Name);
    }

    /// <summary>
    /// Verifies that direct imports are resolved.
    /// </summary>
    [TestMethod]
    public void Parse_ImportDirectDependency_AddsImportedRules()
    {
        var resolver = CreateResolver(
            ("A", "grammar A; import B; start : br ;"),
            ("B", "grammar B; br : 'b' ;"));

        var definition = Antlr4GrammarProjectCompiler.Parse("A", resolver);

        Assert.IsTrue(definition.AllRules.ContainsKey("start"));
        Assert.IsTrue(definition.AllRules.ContainsKey("br"));
    }

    /// <summary>
    /// Verifies that entry grammar rules override imported rules with identical names.
    /// </summary>
    [TestMethod]
    public void Parse_EntryRuleOverridesImportedRule_EntryWins()
    {
        var diagnostics = new DiagnosticBag();
        var resolver = CreateResolver(
            ("A", "grammar A; import B; start : 'a' ;"),
            ("B", "grammar B; start : 'b' ;"));

        var definition = Antlr4GrammarProjectCompiler.Parse("A", resolver, diagnostics);

        Assert.AreEqual("start", definition.RootRule?.Name);
        Assert.AreEqual(1, definition.ParserRules.Count(static rule => rule.Name == "start"));
        Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.ImportedRuleIgnoredBecauseAlreadyDefined.Code));
    }

    /// <summary>
    /// Verifies that transitive imports are resolved recursively.
    /// </summary>
    [TestMethod]
    public void Parse_ImportTransitiveDependency_ResolvesAllLevels()
    {
        var resolver = CreateResolver(
            ("A", "grammar A; import B; start : br ;"),
            ("B", "grammar B; import C; br : cr ;"),
            ("C", "grammar C; cr : 'c' ;"));

        var definition = Antlr4GrammarProjectCompiler.Parse("A", resolver);

        Assert.IsTrue(definition.AllRules.ContainsKey("cr"));
    }

    /// <summary>
    /// Verifies that cyclic imports are detected.
    /// </summary>
    [TestMethod]
    public void Parse_ImportCycle_ThrowsValidationException()
    {
        var diagnostics = new DiagnosticBag();
        var resolver = CreateResolver(
            ("A", "grammar A; import B; start : 'a' ;"),
            ("B", "grammar B; import A; br : 'b' ;"));

        var exception = Assert.ThrowsExactly<GrammarValidationException>(() => Antlr4GrammarProjectCompiler.Parse("A", resolver, diagnostics));

        StringAssert.Contains(exception.Message, "Import cycle");
        Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.ImportCycleDetected.Code));
    }

    /// <summary>
    /// Verifies that unresolved imports produce explicit diagnostics and errors.
    /// </summary>
    [TestMethod]
    public void Parse_MissingImport_ThrowsValidationException()
    {
        var diagnostics = new DiagnosticBag();
        var resolver = CreateResolver(("A", "grammar A; import Missing; start : 'a' ;"));

        var exception = Assert.ThrowsExactly<GrammarValidationException>(() => Antlr4GrammarProjectCompiler.Parse("A", resolver, diagnostics));

        StringAssert.Contains(exception.Message, "Unable to resolve grammar 'Missing'");
        Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.ImportedGrammarNotFound.Code));
    }

    /// <summary>
    /// Verifies token vocabulary resolution for parser grammars.
    /// </summary>
    [TestMethod]
    public void Parse_ParserGrammarWithTokenVocab_ImportsLexerRulesOnly()
    {
        var resolver = CreateResolver(
            ("MainParser", "parser grammar MainParser; options { tokenVocab=MyLexer; } start : ID ;"),
            ("MyLexer", "lexer grammar MyLexer; ID : 'a'+ ;"));

        var definition = Antlr4GrammarProjectCompiler.Parse("MainParser", resolver);

        Assert.IsTrue(definition.AllRules.ContainsKey("ID"));
        Assert.AreEqual("start", definition.RootRule?.Name);
    }

    /// <summary>
    /// Verifies that token vocabulary imports lexer modes and mode rules.
    /// </summary>
    [TestMethod]
    public void Parse_ParserGrammarWithTokenVocab_ImportsLexerModes()
    {
        var resolver = CreateResolver(
            ("MainParser", "parser grammar MainParser; options { tokenVocab=MyLexer; } start : ID ;"),
            ("MyLexer", "lexer grammar MyLexer; ID : 'a'+ ; mode Extra; EXTRA_ID : 'X'+ ;"));

        var definition = Antlr4GrammarProjectCompiler.Parse("MainParser", resolver);

        Assert.IsTrue(definition.Modes.Any(mode => mode.Name == "Extra"));
        Assert.IsTrue(definition.Modes.Single(mode => mode.Name == "Extra").Rules.Any(rule => rule.Name == "EXTRA_ID"));
    }

    /// <summary>
    /// Verifies that a missing token vocabulary dependency produces a clear error.
    /// </summary>
    [TestMethod]
    public void Parse_ParserGrammarWithMissingTokenVocab_ThrowsValidationException()
    {
        var diagnostics = new DiagnosticBag();
        var resolver = CreateResolver(
            ("MainParser", "parser grammar MainParser; options { tokenVocab=MissingLexer; } start : ID ;"));

        var exception = Assert.ThrowsExactly<GrammarValidationException>(
            () => Antlr4GrammarProjectCompiler.Parse("MainParser", resolver, diagnostics));

        StringAssert.Contains(exception.Message, "MissingLexer");
        Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.ImportedGrammarNotFound.Code));
    }

    /// <summary>
    /// Verifies parser grammars cannot declare their own lexer rules.
    /// </summary>
    [TestMethod]
    public void Parse_ParserGrammarWithOwnLexerRule_ThrowsValidationException()
    {
        var diagnostics = new DiagnosticBag();
        var resolver = CreateResolver(
            ("MainParser", "parser grammar MainParser; start : ID ; ID : 'a'+ ;"));

        var exception = Assert.ThrowsExactly<GrammarValidationException>(
            () => Antlr4GrammarProjectCompiler.Parse("MainParser", resolver, diagnostics));

        StringAssert.Contains(exception.Message, "ID");
        Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.LexerRuleNotAllowedInParserGrammar.Code));
    }

    /// <summary>
    /// Verifies that imported lexer modes are merged with existing modes.
    /// </summary>
    [TestMethod]
    public void Parse_ImportedLexerModes_AreMerged()
    {
        var resolver = CreateResolver(
            ("A", "grammar A; import B; start : 'a' ; A_TOKEN : 'a' ; mode Extra; EXTRA_A : 'x' ;"),
            ("B", "grammar B; B_TOKEN : 'b' ; mode Extra; EXTRA_B : 'y' ; mode ImportedMode; IMPORTED : 'z' ;"));

        var definition = Antlr4GrammarProjectCompiler.Parse("A", resolver);

        var extraMode = definition.Modes.Single(mode => mode.Name == "Extra");
        Assert.IsTrue(extraMode.Rules.Any(rule => rule.Name == "EXTRA_A"));
        Assert.IsTrue(extraMode.Rules.Any(rule => rule.Name == "EXTRA_B"));
        Assert.IsTrue(definition.Modes.Any(mode => mode.Name == "ImportedMode"));
    }

    /// <summary>
    /// Verifies cold-mode generation inputs using the in-memory resolver.
    /// </summary>
    [TestMethod]
    public void Compile_InMemoryResolver_ColdModeCompilationWorks()
    {
        var resolver = CreateResolver(
            ("Entry", "grammar Entry; import Shared; start : item ;"),
            ("Shared", "grammar Shared; item : ID ; ID : 'a'+ ;"));

        var compiled = Antlr4GrammarProjectCompiler.Compile("Entry", resolver);
        var parseNode = compiled.Parse("abc");

        Assert.IsNotNull(parseNode);
    }



    /// <summary>
    /// Verifies hot-mode file compilation resolves imports from the entry directory.
    /// </summary>
    [TestMethod]
    public void ParseFromFile_HotMode_ResolvesImportsFromDisk()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"UtilsParser_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            File.WriteAllText(Path.Combine(tempDirectory, "A.g4"), "grammar A; import B; start : br ;");
            File.WriteAllText(Path.Combine(tempDirectory, "B.g4"), "grammar B; br : 'b' ;");

            var definition = Antlr4GrammarProjectCompiler.ParseFromFile(Path.Combine(tempDirectory, "A.g4"));
            Assert.IsTrue(definition.AllRules.ContainsKey("br"));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
    /// <summary>
    /// Creates an in-memory resolver from simple name/text tuples.
    /// </summary>
    /// <param name="grammars">Grammar tuples.</param>
    /// <returns>Resolver instance.</returns>
    private static InMemoryGrammarSourceResolver CreateResolver(params (string Name, string Text)[] grammars)
    {
        var sources = grammars
            .Select(grammar => new GrammarSource(grammar.Name, $"{grammar.Name}.g4", grammar.Text));
        return new InMemoryGrammarSourceResolver(sources);
    }
}
