using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Antlr4.Common;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Generators.Internal;

namespace UtilsTest.Parser;

[TestClass]
public class Antlr4PrequelParityTests
{
    [TestMethod]
    public void Options_AreEquivalent()
    {
        var runtime = ParseRuntime(PrequelGrammar);
        var generator = ParseGenerator(PrequelGrammar);

        Assert.IsNotNull(runtime.Options);
        Assert.IsNotNull(generator.Options);
        CollectionAssert.AreEquivalent(runtime.Options!.Values.Keys.ToArray(), generator.Options!.Values.Keys.ToArray());
        foreach (var pair in runtime.Options.Values)
        {
            Assert.IsTrue(generator.Options.Values.TryGetValue(pair.Key, out var value));
            Assert.AreEqual(pair.Value, value);
        }
    }

    [TestMethod]
    public void Imports_AreEquivalent()
    {
        var runtime = ParseRuntime(PrequelGrammar);
        var generator = ParseGenerator(PrequelGrammar);

        CollectionAssert.AreEqual(runtime.Imports.Select(static i => i.GrammarName).ToArray(), generator.Imports.Select(static i => i.GrammarName).ToArray());
        CollectionAssert.AreEqual(runtime.Imports.Select(static i => i.Alias ?? string.Empty).ToArray(), generator.Imports.Select(static i => i.Alias ?? string.Empty).ToArray());
    }

    [TestMethod]
    public void GrammarActions_AreEquivalent()
    {
        var runtime = ParseRuntime(PrequelGrammar);
        var generator = ParseGenerator(PrequelGrammar);

        CollectionAssert.AreEqual(runtime.Actions.Select(static a => a.Name).ToArray(), generator.Actions.Select(static a => a.Name).ToArray());
        CollectionAssert.AreEqual(runtime.Actions.Select(static a => a.Target ?? string.Empty).ToArray(), generator.Actions.Select(static a => a.Target ?? string.Empty).ToArray());
        CollectionAssert.AreEqual(runtime.Actions.Select(static a => a.Code.Trim()).ToArray(), generator.Actions.Select(static a => a.Code.Trim()).ToArray());
    }

    [TestMethod]
    public void TokensBlock_AreEquivalent()
    {
        var runtime = ParseRuntime(PrequelGrammar);
        var generator = ParseGenerator(PrequelGrammar);

        CollectionAssert.AreEquivalent(runtime.DeclaredTokens.OrderBy(static x => x).ToArray(), generator.DeclaredTokens.OrderBy(static x => x).ToArray());
        Assert.IsFalse(runtime.DeclaredTokens.Contains("indent"));
        Assert.IsFalse(generator.DeclaredTokens.Contains("indent"));
    }

    [TestMethod]
    public void ChannelsBlock_AreEquivalent()
    {
        var runtime = ParseRuntime(PrequelGrammar);
        var generator = ParseGenerator(PrequelGrammar);

        CollectionAssert.AreEquivalent(runtime.DeclaredChannels.OrderBy(static x => x).ToArray(), generator.DeclaredChannels.OrderBy(static x => x).ToArray());
    }

    [TestMethod]
    public void MalformedImport_DivergenceExplicit()
    {
        const string grammar = """
            grammar P;
            import A=;
            start : ID ;
            ID : ('a'..'z')+ ;
            """;

        Assert.ThrowsException<GrammarParseException>(() => Antlr4GrammarConverter.ParseUnresolved(grammar, new DiagnosticBag()));
        var generatorDiagnostics = new DiagnosticBag();
        _ = new G4Parser(new G4Tokenizer(grammar).Tokenize(), generatorDiagnostics).Parse();
        Assert.IsTrue(generatorDiagnostics.Count > 0);
    }

    [TestMethod]
    public void MalformedChannels_DivergenceExplicit()
    {
        const string grammar = """
            grammar P;
            channels { , COMMENT }
            start : ID ;
            ID : ('a'..'z')+ ;
            """;

        Assert.ThrowsException<GrammarParseException>(() => Antlr4GrammarConverter.ParseUnresolved(grammar, new DiagnosticBag()));
        var generatorDiagnostics = new DiagnosticBag();
        _ = new G4Parser(new G4Tokenizer(grammar).Tokenize(), generatorDiagnostics).Parse();
        Assert.IsTrue(generatorDiagnostics.Count > 0);
    }

    private static Antlr4PrequelModel ParseRuntime(string grammar)
    {
        var definition = Antlr4GrammarConverter.ParseUnresolved(grammar);
        return Antlr4RuntimePrequelMapper.Map(definition);
    }

    private static Antlr4PrequelModel ParseGenerator(string grammar)
    {
        var g4 = new G4Parser(new G4Tokenizer(grammar).Tokenize()).Parse();
        return Antlr4GeneratorPrequelMapper.Map(
            g4.Options,
            g4.Imports.Select(static import => new Antlr4ImportInfo(import.GrammarName, import.Alias)).ToList(),
            g4.Actions.Select(static action => new Antlr4ActionInfo(action.Name, action.RawCode, action.Target)).ToList(),
            g4.DeclaredTokens,
            g4.DeclaredChannels,
            includeDefaultChannels: true);
    }

    private const string PrequelGrammar = """
        grammar PrequelMeta;
        options { caseInsensitive=true; tokenVocab=CommonLexer; }
        import CommonLexer, CommonParser=CommonParserAlias;
        tokens { INDENT, DEDENT }
        channels { COMMENT }

        @header { using System; }
        @members { int _global; }
        @parser::members { int _p; }
        @lexer::members { int _l; }

        start : ID ;
        ID : ('a'..'z')+ ;
        """;
}
