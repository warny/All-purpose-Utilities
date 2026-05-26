using System.Collections.Generic;
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
    public void RuntimeDiagnostics_MatchSharedPrequelFacts_ForImportsTokensAndChannels()
    {
        var runtimeBag = new DiagnosticBag();
        var definition = Antlr4GrammarConverter.ParseUnresolved(PrequelGrammar, runtimeBag);
        var prequel = Antlr4RuntimePrequelMapper.Map(definition);
        var sharedFacts = Antlr4PrequelValidator.Validate(prequel);
        var mapped = global::Utils.Parser.Antlr4.Common.Antlr4PrequelDiagnosticMapper.ToParserDiagnostics(sharedFacts.Diagnostics);

        var runtimeRelevant = runtimeBag.ToList()
            .Where(static d => d.Code is "UP1001" or "UP1002" or "UP1003")
            .Select(d => (d.Code, Subject: ExtractSubjectFromMessage(d.Message)))
            .ToArray();

        var mappedRelevant = mapped
            .Where(static d => d.Code is "UP1001" or "UP1002" or "UP1003")
            .Select(d => (d.Code, Subject: ExtractSubjectFromMessage(d.Message)))
            .ToArray();

        CollectionAssert.AreEquivalent(runtimeRelevant, mappedRelevant);
    }

    [TestMethod]
    public void RuntimeDiagnostics_Divergence_GrammarActionsAreNotEmittedAsUp1004()
    {
        var runtimeBag = new DiagnosticBag();
        var definition = Antlr4GrammarConverter.ParseUnresolved(PrequelGrammar, runtimeBag);
        var prequel = Antlr4RuntimePrequelMapper.Map(definition);
        var sharedFacts = Antlr4PrequelValidator.Validate(prequel);
        var mapped = global::Utils.Parser.Antlr4.Common.Antlr4PrequelDiagnosticMapper.ToParserDiagnostics(sharedFacts.Diagnostics);

        var runtimeActionCount = runtimeBag.ToList().Count(static d => d.Code == "UP1004");
        var mappedActionCount = mapped.Count(static d => d.Code == "UP1004");

        Assert.AreEqual(0, runtimeActionCount);
        Assert.AreEqual(4, mappedActionCount);
    }

    [TestMethod]
    public void GeneratorDiagnostics_Divergence_ImportDiagnosticGranularityDiffers()
    {
        var generatorBag = new DiagnosticBag();
        var g4 = new G4Parser(new G4Tokenizer(PrequelGrammar).Tokenize(), generatorBag).Parse();
        var prequel = Antlr4GeneratorPrequelMapper.Map(g4);
        var sharedFacts = Antlr4PrequelValidator.Validate(prequel);
        var mapped = global::Utils.Parser.Generators.Internal.Antlr4PrequelDiagnosticMapper.ToParserDiagnostics(sharedFacts.Diagnostics);

        var generatorImportCount = generatorBag.ToList().Count(static d => d.Code == "UP1001");
        var mappedImportCount = mapped.Count(static d => d.Code == "UP1001");

        Assert.AreEqual(1, generatorImportCount);
        Assert.AreEqual(2, mappedImportCount);
    }

    [TestMethod]
    public void GeneratorDiagnostics_MatchSharedPrequelFacts_ForTokensChannelsAndGrammarActions()
    {
        var generatorBag = new DiagnosticBag();
        var g4 = new G4Parser(new G4Tokenizer(PrequelGrammar).Tokenize(), generatorBag).Parse();
        var prequel = Antlr4GeneratorPrequelMapper.Map(g4);
        var sharedFacts = Antlr4PrequelValidator.Validate(prequel);
        var mapped = global::Utils.Parser.Generators.Internal.Antlr4PrequelDiagnosticMapper.ToParserDiagnostics(sharedFacts.Diagnostics);

        var generatorRelevantCodes = generatorBag.ToList()
            .Where(static d => d.Code is "UP1002" or "UP1003" or "UP1004")
            .GroupBy(static d => d.Code)
            .ToDictionary(static g => g.Key, static g => g.Count());

        var mappedRelevantCodes = mapped
            .Where(static d => d.Code is "UP1002" or "UP1003" or "UP1004")
            .GroupBy(static d => d.Code)
            .ToDictionary(static g => g.Key, static g => g.Count());

        AssertCodeCount(generatorRelevantCodes, "UP1002", 1);
        AssertCodeCount(generatorRelevantCodes, "UP1003", 1);
        AssertCodeCount(generatorRelevantCodes, "UP1004", 4);

        AssertCodeCount(mappedRelevantCodes, "UP1002", 1);
        AssertCodeCount(mappedRelevantCodes, "UP1003", 1);
        AssertCodeCount(mappedRelevantCodes, "UP1004", 4);
    }

    /// <summary>
    /// Asserts that a grouped diagnostic map contains an expected count for one code.
    /// </summary>
    /// <param name="counts">Diagnostic code counts.</param>
    /// <param name="code">Diagnostic code to check.</param>
    /// <param name="expected">Expected occurrences.</param>
    private static void AssertCodeCount(IReadOnlyDictionary<string, int> counts, string code, int expected)
    {
        Assert.IsTrue(counts.TryGetValue(code, out var actual), $"Missing diagnostic code '{code}'.");
        Assert.AreEqual(expected, actual, $"Unexpected count for diagnostic code '{code}'.");
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
