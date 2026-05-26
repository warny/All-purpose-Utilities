using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Generators.Internal;
using Utils.Parser.Model;

namespace UtilsTest.Parser;

[TestClass]
public class Antlr4GeneratorRuntimeParityTests
{
    [TestMethod]
    public void Parity_SupportedFacts_GeneratorAndRuntimeExposeSameCoreGrammarShape()
    {
        const string grammar = """
            grammar Parity;
            options { caseInsensitive=true; tokenVocab=CommonLexer; superClass=BaseParser; }

            start : 'x' ;

            ID : ('a'..'z' | 'A'..'Z')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;

            mode COMMENTS;
            COMMENT_TEXT : . -> more ;
            """;

        RuntimeFacts runtime = RuntimeFacts.From(grammar);
        GeneratorFacts generator = GeneratorFacts.From(grammar);

        CollectionAssert.AreEquivalent(runtime.OptionKeys, generator.OptionKeys);
        CollectionAssert.AreEqual(runtime.ParserRuleNames, generator.ParserRuleNames);
        CollectionAssert.AreEqual(runtime.DefaultLexerRuleNames, generator.DefaultLexerRuleNames);
        CollectionAssert.AreEqual(runtime.ExtraModeNames, generator.ExtraModeNames);
        CollectionAssert.AreEqual(runtime.ExtraModeRuleNames, generator.ExtraModeRuleNames);

        Assert.AreEqual(runtime.Name, generator.Name);
        Assert.AreEqual(runtime.Kind, generator.Kind);
        Assert.AreEqual(runtime.RootRuleName, generator.RootRuleName);
    }

    [TestMethod]
    public void Parity_SupportedFacts_InlineActionsAndSemanticPredicatesAreVisibleInBothPaths()
    {
        const string grammar = """
            grammar InlineFacts;

            start : { Track(); } { IsReady() }? ID ;

            ID : ('a'..'z')+ ;
            """;

        RuntimeFacts runtime = RuntimeFacts.From(grammar);
        GeneratorFacts generator = GeneratorFacts.From(grammar);

        Assert.AreEqual(1, runtime.InlineActions.Length);
        Assert.AreEqual(1, generator.InlineActions.Length);
        Assert.AreEqual(1, runtime.ValidatingPredicates.Length);
        Assert.AreEqual(1, generator.ValidatingPredicates.Length);

        Assert.AreEqual(runtime.InlineActions[0], generator.InlineActions[0]);
        Assert.AreEqual(runtime.ValidatingPredicates[0], generator.ValidatingPredicates[0]);
    }

    [TestMethod]
    public void Parity_SupportedFacts_RuleLifecycleActionsMetadata()
    {
        const string grammar = """
            grammar RulePrequels;

            withInit
                @init { Init(); }
                : ID
                ;

            withAfter
                @after { After(); }
                : ID
                ;

            ID : ('a'..'z')+ ;
            """;

        RuntimeFacts runtime = RuntimeFacts.From(grammar);
        GeneratorFacts generator = GeneratorFacts.From(grammar);

        Assert.AreEqual(1, runtime.RuleInitActionCount);
        Assert.AreEqual(1, runtime.RuleAfterActionCount);

        Assert.AreEqual(runtime.RuleInitActionCount, generator.RuleInitActionCount);
        Assert.AreEqual(runtime.RuleAfterActionCount, generator.RuleAfterActionCount);
        CollectionAssert.AreEqual(runtime.RuleInitActionRawCodes, generator.RuleInitActionRawCodes);
        CollectionAssert.AreEqual(runtime.RuleAfterActionRawCodes, generator.RuleAfterActionRawCodes);
        CollectionAssert.AreEqual(new[] { "Init();" }, generator.RuleInitActionRawCodes);
        CollectionAssert.AreEqual(new[] { "After();" }, generator.RuleAfterActionRawCodes);
    }

    [TestMethod]
    public void Parity_SupportedFacts_GrammarPrequelMetadata()
    {
        const string grammar = """
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

        RuntimeFacts runtime = RuntimeFacts.From(grammar);
        GeneratorFacts generator = GeneratorFacts.From(grammar);

        CollectionAssert.AreEqual(new[] { "CommonLexer", "CommonParserAlias" }, runtime.ImportGrammarNames);
        CollectionAssert.AreEqual(new[] { string.Empty, "CommonParser" }, runtime.ImportAliases);
        CollectionAssert.AreEquivalent(new[] { "INDENT", "DEDENT" }, runtime.DeclaredTokens);
        CollectionAssert.AreEquivalent(new[] { "COMMENT", "DEFAULT_CHANNEL", "HIDDEN" }, runtime.DeclaredChannels);
        CollectionAssert.AreEqual(new[] { "header", "members", "members", "members" }, runtime.GrammarActionNames);
        CollectionAssert.AreEqual(new[] { string.Empty, string.Empty, "parser", "lexer" }, runtime.GrammarActionTargets);
        CollectionAssert.AreEqual(
            new[] { "using System;", "int _global;", "int _p;", "int _l;" },
            runtime.GrammarActionRawCodes);

        CollectionAssert.AreEqual(runtime.ImportGrammarNames, generator.ImportGrammarNames);
        CollectionAssert.AreEqual(runtime.ImportAliases, generator.ImportAliases);
        CollectionAssert.AreEquivalent(runtime.DeclaredTokens, generator.DeclaredTokens);
        CollectionAssert.AreEquivalent(runtime.DeclaredChannels, generator.DeclaredChannels);
        CollectionAssert.AreEqual(runtime.GrammarActionNames, generator.GrammarActionNames);
        CollectionAssert.AreEqual(runtime.GrammarActionTargets, generator.GrammarActionTargets);
        CollectionAssert.AreEqual(runtime.GrammarActionRawCodes, generator.GrammarActionRawCodes);
    }

    [TestMethod]
    public void Parity_SupportedFacts_PrequelNameListsIgnoreCommentsAndTrivia()
    {
        const string grammar = """
            grammar PrequelComments;
            tokens {
                INDENT, // indentation start
                DEDENT
            }
            channels {
                COMMENT /* inline channel */
            }

            start : ID ;
            ID : ('a'..'z')+ ;
            """;

        RuntimeFacts runtime = RuntimeFacts.From(grammar);
        GeneratorFacts generator = GeneratorFacts.From(grammar);

        CollectionAssert.AreEquivalent(runtime.DeclaredTokens, generator.DeclaredTokens);
        CollectionAssert.AreEquivalent(runtime.DeclaredChannels, generator.DeclaredChannels);
    }

    [TestMethod]
    public void Parity_SupportedFacts_RuleLabelsDoNotBreakGeneratorAlternativeParsing()
    {
        const string grammar = """
            grammar G;
            start : id=ID ids+=ID ;
            ID : 'a' ;
            """;

        RuntimeFacts runtime = RuntimeFacts.From(grammar);
        GeneratorFacts generator = GeneratorFacts.From(grammar);

        Assert.AreEqual(2, runtime.ParserRuleReferenceCount);
        Assert.AreEqual(runtime.ParserRuleReferenceCount, generator.ParserRuleReferenceCount);
    }

    [TestMethod]
    public void RuntimeAndGenerator_Diagnostics_ImportParity()
    {
        const string grammar = """
            grammar DiagnosticParity;
            import CommonLexer;
            start : ID ;
            ID : ('a'..'z')+ ;
            """;

        AssertDiagnosticParity(grammar);
    }

    [TestMethod]
    public void RuntimeAndGenerator_Diagnostics_TokensParity()
    {
        const string grammar = """
            grammar DiagnosticParity;
            tokens { INDENT }
            start : ID ;
            ID : ('a'..'z')+ ;
            """;

        AssertDiagnosticParity(grammar);
    }

    [TestMethod]
    public void RuntimeAndGenerator_Diagnostics_ChannelsParity()
    {
        const string grammar = """
            grammar DiagnosticParity;
            channels { COMMENT }
            start : ID ;
            ID : ('a'..'z')+ ;
            """;

        AssertDiagnosticParity(grammar);
    }

    [TestMethod]
    public void Divergence_RuntimeAndGenerator_Diagnostics_GrammarActions()
    {
        const string grammar = """
            grammar DiagnosticParity;
            @members { int _g; }
            start : ID ;
            ID : ('a'..'z')+ ;
            """;

        var runtimeDiagnostics = new DiagnosticBag();
        _ = Antlr4GrammarConverter.ParseUnresolved(grammar, runtimeDiagnostics);
        var generatorDiagnostics = new DiagnosticBag();
        _ = new G4Parser(new G4Tokenizer(grammar).Tokenize(), generatorDiagnostics).Parse();
        Assert.AreEqual(0, runtimeDiagnostics.Count(d => d.Code == "UP1004"));
        Assert.AreEqual(1, generatorDiagnostics.Count(d => d.Code == "UP1004"));
    }

    [TestMethod]
    public void RuntimeAndGenerator_Diagnostics_InlineActionParity()
    {
        const string grammar = """
            grammar DiagnosticParity;
            start : { Act(); } ID ;
            ID : ('a'..'z')+ ;
            """;

        AssertDiagnosticParity(grammar);
    }

    [TestMethod]
    public void RuntimeAndGenerator_Diagnostics_PredicateParity()
    {
        const string grammar = """
            grammar DiagnosticParity;
            start : { Pred() }? ID ;
            ID : ('a'..'z')+ ;
            """;

        AssertDiagnosticParity(grammar);
    }

    [TestMethod]
    public void Divergence_RuntimeAndGenerator_Diagnostics_MissingBraceRecovery()
    {
        const string grammar = "grammar G; start : { Act(); ID ; ID : ('a'..'z')+ ;";
        AssertRuntimeFailsAndGeneratorRecovers(grammar, minimumGeneratorDiagnostics: 1);
    }

    [TestMethod]
    public void Divergence_RuntimeAndGenerator_Diagnostics_MalformedImportRecovery()
    {
        const string grammar = "grammar G; import ; start : ID ; ID : ('a'..'z')+ ;";
        AssertRuntimeFailsAndGeneratorRecovers(grammar, minimumGeneratorDiagnostics: 1);
    }

    [TestMethod]
    public void Divergence_RuntimeAndGenerator_Diagnostics_MalformedChannelListRecovery()
    {
        const string grammar = "grammar G; channels ; start : ID ; ID : ('a'..'z')+ ;";
        AssertRuntimeFailsAndGeneratorRecovers(grammar, minimumGeneratorDiagnostics: 1);
    }

    [TestMethod]
    public void Divergence_RuntimeAndGenerator_Diagnostics_MalformedActionRecovery()
    {
        const string grammar = "grammar G; @ ; start : ID ; ID : ('a'..'z')+ ;";
        AssertRuntimeFailsAndGeneratorRecovers(grammar, minimumGeneratorDiagnostics: 1);
    }

    [TestMethod]
    public void Divergence_RuntimeAndGenerator_Diagnostics_MalformedLifecycleBlockRecovery()
    {
        const string grammar = "grammar G; start @init { Init(); : ID ; ID : ('a'..'z')+ ;";
        AssertRuntimeFailsAndGeneratorRecovers(grammar, minimumGeneratorDiagnostics: 1);
    }

    private static void AssertDiagnosticParity(string grammar)
    {
        var runtimeDiagnostics = new DiagnosticBag();
        _ = Antlr4GrammarConverter.ParseUnresolved(grammar, runtimeDiagnostics);

        var generatorDiagnostics = new DiagnosticBag();
        _ = new G4Parser(new G4Tokenizer(grammar).Tokenize(), generatorDiagnostics).Parse();

        var runtime = runtimeDiagnostics
            .Where(static d => IsCompatibilityParityDiagnostic(d.Code))
            .Select(ToSnapshot)
            .ToArray();
        var generator = generatorDiagnostics
            .Where(static d => IsCompatibilityParityDiagnostic(d.Code))
            .Select(ToSnapshot)
            .ToArray();
        Assert.AreEqual(runtime.Length, generator.Length, "Diagnostic count mismatch.");

        for (int i = 0; i < runtime.Length; i++)
        {
            Assert.AreEqual(runtime[i].Code, generator[i].Code, $"Diagnostic code mismatch at index {i}.");
            Assert.AreEqual(runtime[i].Severity, generator[i].Severity, $"Diagnostic severity mismatch at index {i}.");
            Assert.AreEqual(runtime[i].Line, generator[i].Line, $"Diagnostic line mismatch at index {i}.");
            Assert.AreEqual(runtime[i].Column, generator[i].Column, $"Diagnostic column mismatch at index {i}.");
            Assert.AreEqual(runtime[i].SpanStart, generator[i].SpanStart, $"Diagnostic span start mismatch at index {i}.");
            Assert.AreEqual(runtime[i].SpanLength, generator[i].SpanLength, $"Diagnostic span length mismatch at index {i}.");
        }
    }

    private static DiagnosticSnapshot ToSnapshot(ParserDiagnostic diagnostic)
        => new(
            diagnostic.Code,
            diagnostic.Severity,
            diagnostic.Line,
            diagnostic.Column,
            diagnostic.SpanStart,
            diagnostic.SpanLength);

    private static bool IsCompatibilityParityDiagnostic(string code)
        => code is "UP1001" or "UP1002" or "UP1003" or "UP1004" or "UP1005" or "UP1006";

    private static void AssertRuntimeFailsAndGeneratorRecovers(string grammar, int minimumGeneratorDiagnostics)
    {
        Assert.ThrowsException<GrammarParseException>(() => Antlr4GrammarConverter.ParseUnresolved(grammar, new DiagnosticBag()));
        var diagnostics = new DiagnosticBag();
        _ = new G4Parser(new G4Tokenizer(grammar).Tokenize(), diagnostics).Parse();
        Assert.IsTrue(
            diagnostics.Count >= minimumGeneratorDiagnostics,
            $"Generator diagnostics count {diagnostics.Count} is lower than expected minimum {minimumGeneratorDiagnostics}.");
    }

    private sealed record DiagnosticSnapshot(
        string Code,
        DiagnosticSeverity Severity,
        int? Line,
        int? Column,
        int? SpanStart,
        int? SpanLength);

    private sealed record RuntimeFacts(
        string Name,
        GrammarType Kind,
        string[] OptionKeys,
        string[] ParserRuleNames,
        string[] DefaultLexerRuleNames,
        string[] ExtraModeNames,
        string[] ExtraModeRuleNames,
        string RootRuleName,
        string[] ImportGrammarNames,
        string[] ImportAliases,
        string[] DeclaredTokens,
        string[] DeclaredChannels,
        string[] GrammarActionNames,
        string[] GrammarActionTargets,
        string[] GrammarActionRawCodes,
        string[] InlineActions,
        string[] ValidatingPredicates,
        int RuleInitActionCount,
        int RuleAfterActionCount,
        string[] RuleInitActionRawCodes,
        string[] RuleAfterActionRawCodes,
        int ParserRuleReferenceCount,
        string[] DiagnosticCodes)
    {
        public static RuntimeFacts From(string grammarText)
        {
            var diagnostics = new DiagnosticBag();
            var definition = Antlr4GrammarConverter.ParseUnresolved(grammarText, diagnostics);

            List<string> inlineActions = [];
            List<string> predicates = [];
            foreach (Rule rule in definition.ParserRules)
            {
                CollectAlternativeMetadata(rule.Content, inlineActions, predicates);
            }

            string[] defaultLexerRules = definition.Modes
                .FirstOrDefault(m => m.Name == "DEFAULT_MODE")?.Rules.Select(r => r.Name).ToArray() ?? [];

            var extraModes = definition.Modes.Where(m => m.Name != "DEFAULT_MODE").ToArray();
            return new RuntimeFacts(
                Name: definition.Name,
                Kind: definition.Type,
                OptionKeys: definition.Options?.Values.Keys.OrderBy(k => k).ToArray() ?? [],
                ParserRuleNames: definition.ParserRules.Select(r => r.Name).ToArray(),
                DefaultLexerRuleNames: defaultLexerRules,
                ExtraModeNames: extraModes.Select(m => m.Name).ToArray(),
                ExtraModeRuleNames: extraModes.SelectMany(m => m.Rules.Select(r => $"{m.Name}:{r.Name}")).ToArray(),
                RootRuleName: definition.RootRule?.Name ?? string.Empty,
                ImportGrammarNames: definition.Imports.Select(i => i.GrammarName).ToArray(),
                ImportAliases: definition.Imports.Select(i => i.Alias ?? string.Empty).ToArray(),
                DeclaredTokens: definition.DeclaredTokens.OrderBy(x => x).ToArray(),
                DeclaredChannels: definition.DeclaredChannels.OrderBy(x => x).ToArray(),
                GrammarActionNames: definition.Actions.Select(a => a.Name).ToArray(),
                GrammarActionTargets: definition.Actions.Select(a => a.Target ?? string.Empty).ToArray(),
                GrammarActionRawCodes: definition.Actions.Select(a => TrimCode(a.RawCode)).ToArray(),
                InlineActions: inlineActions.Select(TrimCode).ToArray(),
                ValidatingPredicates: predicates.Select(TrimCode).ToArray(),
                RuleInitActionCount: definition.ParserRules.Count(r => r.InitAction is not null),
                RuleAfterActionCount: definition.ParserRules.Count(r => r.AfterAction is not null),
                RuleInitActionRawCodes: definition.ParserRules
                    .Where(r => r.InitAction is not null)
                    .Select(r => TrimCode(r.InitAction!.RawCode))
                    .ToArray(),
                RuleAfterActionRawCodes: definition.ParserRules
                    .Where(r => r.AfterAction is not null)
                    .Select(r => TrimCode(r.AfterAction!.RawCode))
                    .ToArray(),
                ParserRuleReferenceCount: definition.ParserRules.Sum(r => CountParserRuleReferences(r.Content)),
                DiagnosticCodes: diagnostics.Select(d => d.Code).Distinct().OrderBy(x => x).ToArray());
        }

        private static void CollectAlternativeMetadata(RuleContent content, List<string> inlineActions, List<string> predicates)
        {
            switch (content)
            {
                case Alternation alternation:
                    foreach (Alternative alternative in alternation.Alternatives)
                    {
                        CollectAlternativeMetadata(alternative, inlineActions, predicates);
                    }
                    break;
                case Alternative alternative:
                    CollectAlternativeMetadata(alternative.Content, inlineActions, predicates);
                    break;
                case Sequence sequence:
                    foreach (RuleContent item in sequence.Items)
                    {
                        CollectAlternativeMetadata(item, inlineActions, predicates);
                    }
                    break;
                case Quantifier quantifier:
                    CollectAlternativeMetadata(quantifier.Inner, inlineActions, predicates);
                    break;
                case EmbeddedAction action when action.Context == ActionContext.Alternative:
                    inlineActions.Add(action.RawCode);
                    break;
                case ValidatingPredicate predicate:
                    predicates.Add(predicate.Code);
                    break;
                case Negation negation:
                    CollectAlternativeMetadata(negation.Inner, inlineActions, predicates);
                    break;
            }
        }

        private static int CountParserRuleReferences(RuleContent content)
        {
            return content switch
            {
                RuleRef => 1,
                Alternation alternation => alternation.Alternatives.Sum(CountParserRuleReferences),
                Alternative alternative => CountParserRuleReferences(alternative.Content),
                Sequence sequence => sequence.Items.Sum(CountParserRuleReferences),
                Quantifier quantifier => CountParserRuleReferences(quantifier.Inner),
                Negation negation => CountParserRuleReferences(negation.Inner),
                _ => 0,
            };
        }
    }

    private sealed record GeneratorFacts(
        string Name,
        GrammarType Kind,
        string[] OptionKeys,
        string[] ParserRuleNames,
        string[] DefaultLexerRuleNames,
        string[] ExtraModeNames,
        string[] ExtraModeRuleNames,
        string RootRuleName,
        string[] ImportGrammarNames,
        string[] ImportAliases,
        string[] DeclaredTokens,
        string[] DeclaredChannels,
        string[] GrammarActionNames,
        string[] GrammarActionTargets,
        string[] GrammarActionRawCodes,
        string[] InlineActions,
        string[] ValidatingPredicates,
        int RuleInitActionCount,
        int RuleAfterActionCount,
        string[] RuleInitActionRawCodes,
        string[] RuleAfterActionRawCodes,
        int ParserRuleReferenceCount,
        string[] DiagnosticCodes)
    {
        public static GeneratorFacts From(string grammarText)
        {
            var diagnostics = new DiagnosticBag();
            var grammar = new G4Parser(new G4Tokenizer(grammarText).Tokenize(), diagnostics).Parse();

            List<string> inlineActions = [];
            List<string> predicates = [];
            foreach (G4Rule rule in grammar.ParserRules)
            {
                CollectAlternativeMetadata(rule.Content, inlineActions, predicates);
            }

            return new GeneratorFacts(
                Name: grammar.Name,
                Kind: ConvertKind(grammar.Kind),
                OptionKeys: grammar.Options.Keys.OrderBy(k => k).ToArray(),
                ParserRuleNames: grammar.ParserRules.Select(r => r.Name).ToArray(),
                DefaultLexerRuleNames: grammar.LexerRules.Select(r => r.Name).ToArray(),
                ExtraModeNames: grammar.ExtraModes.Select(m => m.Name).ToArray(),
                ExtraModeRuleNames: grammar.ExtraModes.SelectMany(m => m.Rules.Select(r => $"{m.Name}:{r.Name}")).ToArray(),
                RootRuleName: grammar.ParserRules.FirstOrDefault()?.Name ?? string.Empty,
                ImportGrammarNames: grammar.Imports.Select(i => i.GrammarName).ToArray(),
                ImportAliases: grammar.Imports.Select(i => i.Alias ?? string.Empty).ToArray(),
                DeclaredTokens: grammar.DeclaredTokens.OrderBy(x => x).ToArray(),
                DeclaredChannels: grammar.DeclaredChannels.Concat(new[] { "DEFAULT_CHANNEL", "HIDDEN" }).Distinct().OrderBy(x => x).ToArray(),
                GrammarActionNames: grammar.Actions.Select(a => a.Name).ToArray(),
                GrammarActionTargets: grammar.Actions.Select(a => a.Target ?? string.Empty).ToArray(),
                GrammarActionRawCodes: grammar.Actions.Select(a => TrimCode(a.RawCode)).ToArray(),
                InlineActions: inlineActions.Select(TrimCode).ToArray(),
                ValidatingPredicates: predicates.Select(TrimCode).ToArray(),
                RuleInitActionCount: grammar.ParserRules.Count(r => r.InitAction is not null),
                RuleAfterActionCount: grammar.ParserRules.Count(r => r.AfterAction is not null),
                RuleInitActionRawCodes: grammar.ParserRules
                    .Where(r => r.InitAction is not null)
                    .Select(r => TrimCode(r.InitAction!.Code))
                    .ToArray(),
                RuleAfterActionRawCodes: grammar.ParserRules
                    .Where(r => r.AfterAction is not null)
                    .Select(r => TrimCode(r.AfterAction!.Code))
                    .ToArray(),
                ParserRuleReferenceCount: grammar.ParserRules.Sum(r => CountParserRuleReferences(r.Content)),
                DiagnosticCodes: diagnostics.Select(d => d.Code).Distinct().OrderBy(x => x).ToArray());
        }

        private static GrammarType ConvertKind(G4GrammarKind kind)
            => kind switch
            {
                G4GrammarKind.Combined => GrammarType.Combined,
                G4GrammarKind.Lexer => GrammarType.Lexer,
                G4GrammarKind.Parser => GrammarType.Parser,
                _ => GrammarType.Combined,
            };

        private static void CollectAlternativeMetadata(G4Content content, List<string> inlineActions, List<string> predicates)
        {
            switch (content)
            {
                case G4Alternation alternation:
                    foreach (G4Alternative alternative in alternation.Alternatives)
                    {
                        CollectAlternativeMetadata(alternative, inlineActions, predicates);
                    }
                    break;
                case G4Alternative alternative:
                    foreach (G4Content item in alternative.Items)
                    {
                        CollectAlternativeMetadata(item, inlineActions, predicates);
                    }
                    break;
                case G4Sequence sequence:
                    foreach (G4Content item in sequence.Items)
                    {
                        CollectAlternativeMetadata(item, inlineActions, predicates);
                    }
                    break;
                case G4Quantifier quantifier:
                    CollectAlternativeMetadata(quantifier.Inner, inlineActions, predicates);
                    break;
                case G4EmbeddedAction action when action.IsPredicate:
                    predicates.Add(action.Code);
                    break;
                case G4EmbeddedAction action:
                    inlineActions.Add(action.Code);
                    break;
                case G4Negation negation:
                    CollectAlternativeMetadata(negation.Inner, inlineActions, predicates);
                    break;
            }
        }

        private static int CountParserRuleReferences(G4Content content)
        {
            return content switch
            {
                G4RuleRef => 1,
                G4Alternation alternation => alternation.Alternatives.Sum(CountParserRuleReferences),
                G4Alternative alternative => alternative.Items.Sum(CountParserRuleReferences),
                G4Sequence sequence => sequence.Items.Sum(CountParserRuleReferences),
                G4Quantifier quantifier => CountParserRuleReferences(quantifier.Inner),
                G4Negation negation => CountParserRuleReferences(negation.Inner),
                _ => 0,
            };
        }
    }

    private static string TrimCode(string code) => code.Trim();
}
