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
    public void SupportedSubset_GeneratorAndRuntimeExposeSameCoreGrammarShape()
    {
        const string grammar = """
            grammar Parity;
            options { caseInsensitive=true; tokenVocab=CommonLexer; superClass=BaseParser; }

            start : ID ;

            ID : [a-zA-Z]+ ;
            INT : [0-9]+ ;
            WS : [ \t\r\n]+ -> skip ;

            mode COMMENTS;
            COMMENT_TEXT : ~[\r\n]+ ;
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
    public void InlineActionsAndSemanticPredicates_AreVisibleInBothPathsAsAlternativeMetadata()
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
    public void RuleLifecycleActions_AreRuntimeOnlyMetadataToday()
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

        Assert.AreEqual(0, generator.RuleInitActionCount,
            "G4Parser currently skips rule prequel metadata before ':' for @init actions.");
        Assert.AreEqual(0, generator.RuleAfterActionCount,
            "G4Parser currently skips rule prequel metadata before ':' for @after actions.");
    }

    [TestMethod]
    public void GrammarPrequelMetadata_DocumentsCurrentRuntimeGeneratorDivergence()
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

        Assert.AreEqual(0, generator.ImportGrammarNames.Length,
            "Generator import metadata is diagnosed but not preserved today.");
        Assert.AreEqual(0, generator.DeclaredTokens.Length,
            "Generator tokens metadata is diagnosed but token names are not preserved today.");
        Assert.AreEqual(0, generator.DeclaredChannels.Length,
            "Generator channels metadata is diagnosed but channel names are not preserved today.");
        Assert.AreEqual(0, generator.GrammarActionNames.Length,
            "Generator grammar actions are diagnosed but action metadata is not preserved today.");
    }

    [TestMethod]
    public void PrequelDiagnostics_AreReportedByBothPathsForIgnoredCompatibilityConstructs()
    {
        const string grammar = """
            grammar DiagnosticParity;
            import CommonLexer;
            tokens { INDENT }
            channels { COMMENT }
            @members { int _g; }

            start : { Act(); } { Pred() }? ID ;
            ID : ('a'..'z')+ ;
            """;

        RuntimeFacts runtime = RuntimeFacts.From(grammar);
        GeneratorFacts generator = GeneratorFacts.From(grammar);

        string[] expectedCodes = ["UP1001", "UP1002", "UP1003", "UP1004", "UP1005", "UP1006"];
        foreach (string code in expectedCodes)
        {
            CollectionAssert.Contains(runtime.DiagnosticCodes, code);
            CollectionAssert.Contains(generator.DiagnosticCodes, code);
        }
    }

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
        string[] InlineActions,
        string[] ValidatingPredicates,
        int RuleInitActionCount,
        int RuleAfterActionCount,
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
                InlineActions: inlineActions.Select(TrimCode).ToArray(),
                ValidatingPredicates: predicates.Select(TrimCode).ToArray(),
                RuleInitActionCount: definition.ParserRules.Count(r => r.InitAction is not null),
                RuleAfterActionCount: definition.ParserRules.Count(r => r.AfterAction is not null),
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
        string[] DeclaredTokens,
        string[] DeclaredChannels,
        string[] GrammarActionNames,
        string[] InlineActions,
        string[] ValidatingPredicates,
        int RuleInitActionCount,
        int RuleAfterActionCount,
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
                ImportGrammarNames: [],
                DeclaredTokens: [],
                DeclaredChannels: [],
                GrammarActionNames: [],
                InlineActions: inlineActions.Select(TrimCode).ToArray(),
                ValidatingPredicates: predicates.Select(TrimCode).ToArray(),
                RuleInitActionCount: 0,
                RuleAfterActionCount: 0,
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
    }

    private static string TrimCode(string code) => code.Trim();
}
