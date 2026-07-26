using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.ProjectCompilation;
using Utils.Parser.Resolution;

namespace UtilsTest.Parser;

/// <summary>Tests deterministic in-memory grammar project composition behavior.</summary>
[TestClass]
public class Antlr4GrammarProjectCompilerCompositionTests
{
    /// <summary>Verifies imported collisions preserve declaration-order precedence while remaining explicit in the plan.</summary>
    [TestMethod]
    public void Parse_ImportedRuleCollision_PreservesFirstDeclaredImport()
    {
        InMemoryGrammarSourceResolver resolver = CreateResolver(
            ("Entry", "grammar Entry; import One, Two; start : item ;"),
            ("Two", "grammar Two; item : '2' ;"),
            ("One", "grammar One; item : '1' ;"));

        ParserDefinition definition = Antlr4GrammarProjectCompiler.Parse("Entry", resolver);

        Assert.AreEqual(1, definition.ParserRules.Count(rule => rule.Name == "item"));
        Assert.IsNotNull(Antlr4GrammarProjectCompiler.Compile("Entry", resolver).Parse("1"));
    }

    /// <summary>Verifies candidate ambiguity is diagnosed before malformed candidate text can be parsed.</summary>
    [TestMethod]
    public void Parse_AmbiguousGrammarSourceWithMalformedCandidate_ReportsAmbiguity()
    {
        var diagnostics = new DiagnosticBag();
        var resolver = new InMemoryGrammarSourceResolver([
            new GrammarSource("Entry", "/entry.g4", "grammar Entry; import Shared; start : item ;"),
            new GrammarSource("Shared", "/one/Shared.g4", "grammar Shared; item : '1' ;"),
            new GrammarSource("Shared", "/two/Shared.g4", "this is not a grammar")]);

        Assert.ThrowsExactly<GrammarValidationException>(() => Antlr4GrammarProjectCompiler.Parse("Entry", resolver, diagnostics));
        Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.AmbiguousImportedGrammar.Code));
    }

    /// <summary>Verifies duplicate declared names with distinct source identities are rejected as ambiguous.</summary>
    [TestMethod]
    public void Parse_AmbiguousGrammarSource_ReportsCandidates()
    {
        var diagnostics = new DiagnosticBag();
        var resolver = new InMemoryGrammarSourceResolver([
            new GrammarSource("Entry", "/entry.g4", "grammar Entry; import Shared; start : item ;"),
            new GrammarSource("Shared", "/one/Shared.g4", "grammar Shared; item : '1' ;"),
            new GrammarSource("Shared", "/two/Shared.g4", "grammar Shared; item : '2' ;")]);

        Assert.ThrowsExactly<GrammarValidationException>(() => Antlr4GrammarProjectCompiler.Parse("Entry", resolver, diagnostics));
        Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.AmbiguousImportedGrammar.Code));
    }

    /// <summary>Verifies aliases are retained without introducing qualified-call syntax and preserve current unqualified composition.</summary>
    [TestMethod]
    public void Parse_AliasedImport_PreservesUnqualifiedCompatibility()
    {
        InMemoryGrammarSourceResolver resolver = CreateResolver(
            ("Entry", "grammar Entry; import Alias=Shared; start : item ;"),
            ("Shared", "grammar Shared; item : 'a' ;"));

        ParserDefinition definition = Antlr4GrammarProjectCompiler.Parse("Entry", resolver);
        Assert.IsTrue(definition.AllRules.ContainsKey("item"));
    }

    /// <summary>Creates an in-memory resolver from grammar name and source text pairs.</summary>
    private static InMemoryGrammarSourceResolver CreateResolver(params (string Name, string Text)[] grammars) =>
        new(grammars.Select(grammar => new GrammarSource(grammar.Name, $"/{grammar.Name}.g4", grammar.Text)));
}
