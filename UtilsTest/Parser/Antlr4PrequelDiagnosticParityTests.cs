using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Antlr4.Common;
using Utils.Parser.Antlr4.Common.Diagnostics;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Generators.Internal;

namespace UtilsTest.Parser;

[TestClass]
public class Antlr4PrequelDiagnosticParityTests
{
    [TestMethod]
    public void RuntimeDiagnostics_MatchSharedPrequelFacts_ForImportsTokensChannelsAndGrammarActions()
    {
        var runtimeBag = new DiagnosticBag();
        var definition = Antlr4GrammarConverter.ParseUnresolved(PrequelGrammar, runtimeBag);
        var prequel = Antlr4RuntimePrequelMapper.Map(definition);
        var sharedFacts = Antlr4PrequelValidator.Validate(prequel);
        var mapped = global::Utils.Parser.Antlr4.Common.Antlr4PrequelDiagnosticMapper.ToParserDiagnostics(sharedFacts.Diagnostics);

        var runtimeRelevant = runtimeBag.ToList()
            .Where(static d => d.Code is "UP1001" or "UP1002" or "UP1003" or "UP1004")
            .Select(d => (d.Code, Subject: ExtractSubjectFromMessage(d.Message)))
            .ToArray();

        var mappedRelevant = mapped
            .Select(d => (d.Code, Subject: ExtractSubjectFromMessage(d.Message)))
            .ToArray();

        var mappedRuntimeRelevant = mappedRelevant.Where(static d => d.Code != "UP1004").ToArray();
        CollectionAssert.AreEquivalent(runtimeRelevant, mappedRuntimeRelevant);
    }

    [TestMethod]
    public void GeneratorDiagnostics_MatchSharedPrequelFacts_ForImportsTokensChannelsAndGrammarActions()
    {
        var generatorBag = new DiagnosticBag();
        var g4 = new G4Parser(new G4Tokenizer(PrequelGrammar).Tokenize(), generatorBag).Parse();
        var prequel = Antlr4GeneratorPrequelMapper.Map(g4);
        var sharedFacts = Antlr4PrequelValidator.Validate(prequel);
        var mapped = global::Utils.Parser.Generators.Internal.Antlr4PrequelDiagnosticMapper.ToParserDiagnostics(sharedFacts.Diagnostics);

        var generatorRelevantCodes = generatorBag.ToList()
            .Where(static d => d.Code is "UP1001" or "UP1002" or "UP1003" or "UP1004")
            .GroupBy(static d => d.Code)
            .ToDictionary(static g => g.Key, static g => g.Count());

        var mappedRelevantCodes = mapped
            .Where(static d => d.Code is "UP1001" or "UP1002" or "UP1003" or "UP1004")
            .GroupBy(static d => d.Code)
            .ToDictionary(static g => g.Key, static g => g.Count());

        Assert.AreEqual(1, generatorRelevantCodes["UP1002"]);
        Assert.AreEqual(1, generatorRelevantCodes["UP1003"]);
        Assert.AreEqual(4, generatorRelevantCodes["UP1004"]);
        Assert.AreEqual(1, generatorRelevantCodes["UP1001"]);

        Assert.AreEqual(1, mappedRelevantCodes["UP1002"]);
        Assert.AreEqual(1, mappedRelevantCodes["UP1003"]);
        Assert.AreEqual(4, mappedRelevantCodes["UP1004"]);
        Assert.IsTrue(mappedRelevantCodes["UP1001"] >= 1);
    }

    private static string ExtractSubjectFromMessage(string message)
    {
        var start = message.IndexOf('\'');
        var end = message.LastIndexOf('\'');
        if (start >= 0 && end > start)
        {
            return message.Substring(start + 1, end - start - 1);
        }

        return string.Empty;
    }

    private const string PrequelGrammar = """
        grammar PrequelMeta;
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
