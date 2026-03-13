using Utils.Parser.Bootstrap;
using Utils.Parser.Model;
using Utils.Parser.Resolution;

namespace UtilsTest.Parser;

[TestClass]
public class Antlr4GrammarTests
{
    [TestMethod]
    public void Antlr4Grammar_BuildsSuccessfully()
    {
        var definition = Antlr4Grammar.Build();
        Assert.IsNotNull(definition);
        Assert.AreEqual("ANTLR4", definition.Name);
        Assert.AreEqual(GrammarType.Combined, definition.Type);
    }

    [TestMethod]
    public void Antlr4Grammar_HasRootRule()
    {
        var definition = Antlr4Grammar.Build();
        Assert.IsNotNull(definition.RootRule);
        Assert.AreEqual("grammarSpec", definition.RootRule!.Name);
    }

    [TestMethod]
    public void Antlr4Grammar_HasDefaultMode()
    {
        var definition = Antlr4Grammar.Build();
        var defaultMode = definition.Modes.FirstOrDefault(m => m.Name == "DEFAULT_MODE");
        Assert.IsNotNull(defaultMode);
        Assert.IsTrue(defaultMode!.Rules.Count > 0);
    }

    [TestMethod]
    public void Antlr4Grammar_HasArgumentMode()
    {
        var definition = Antlr4Grammar.Build();
        var argMode = definition.Modes.FirstOrDefault(m => m.Name == "Argument");
        Assert.IsNotNull(argMode);
        Assert.IsTrue(argMode!.Rules.Count > 0);
    }

    [TestMethod]
    public void Antlr4Grammar_HasLexerCharSetMode()
    {
        var definition = Antlr4Grammar.Build();
        var charSetMode = definition.Modes.FirstOrDefault(m => m.Name == "LexerCharSet");
        Assert.IsNotNull(charSetMode);
        Assert.IsTrue(charSetMode!.Rules.Count > 0);
    }

    [TestMethod]
    public void Antlr4Grammar_HasParserRules()
    {
        var definition = Antlr4Grammar.Build();
        Assert.IsTrue(definition.ParserRules.Count > 0);
    }

    [TestMethod]
    public void Antlr4Grammar_ContainsKeyLexerRules()
    {
        var definition = Antlr4Grammar.Build();
        var defaultMode = definition.Modes.First(m => m.Name == "DEFAULT_MODE");
        var ruleNames = defaultMode.Rules.Select(r => r.Name).ToHashSet();

        // Keywords
        Assert.IsTrue(ruleNames.Contains("GRAMMAR"));
        Assert.IsTrue(ruleNames.Contains("LEXER"));
        Assert.IsTrue(ruleNames.Contains("PARSER"));
        Assert.IsTrue(ruleNames.Contains("IMPORT"));
        Assert.IsTrue(ruleNames.Contains("FRAGMENT"));
        Assert.IsTrue(ruleNames.Contains("RETURNS"));
        Assert.IsTrue(ruleNames.Contains("MODE"));

        // Punctuation
        Assert.IsTrue(ruleNames.Contains("COLON"));
        Assert.IsTrue(ruleNames.Contains("SEMI"));
        Assert.IsTrue(ruleNames.Contains("LPAREN"));
        Assert.IsTrue(ruleNames.Contains("RPAREN"));
        Assert.IsTrue(ruleNames.Contains("OR"));
        Assert.IsTrue(ruleNames.Contains("STAR"));
        Assert.IsTrue(ruleNames.Contains("PLUS"));
        Assert.IsTrue(ruleNames.Contains("QUESTION"));
        Assert.IsTrue(ruleNames.Contains("NOT"));
        Assert.IsTrue(ruleNames.Contains("POUND"));
        Assert.IsTrue(ruleNames.Contains("RARROW"));
        Assert.IsTrue(ruleNames.Contains("AT"));
        Assert.IsTrue(ruleNames.Contains("RANGE"));
        Assert.IsTrue(ruleNames.Contains("ASSIGN"));
        Assert.IsTrue(ruleNames.Contains("PLUS_ASSIGN"));

        // String and number
        Assert.IsTrue(ruleNames.Contains("STRING_LITERAL"));
        Assert.IsTrue(ruleNames.Contains("INT"));

        // Comments
        Assert.IsTrue(ruleNames.Contains("DOC_COMMENT"));
        Assert.IsTrue(ruleNames.Contains("BLOCK_COMMENT"));
        Assert.IsTrue(ruleNames.Contains("LINE_COMMENT"));

        // Identifiers
        Assert.IsTrue(ruleNames.Contains("RULE_REF"));
        Assert.IsTrue(ruleNames.Contains("TOKEN_REF"));

        // Whitespace
        Assert.IsTrue(ruleNames.Contains("WS"));

        // Action
        Assert.IsTrue(ruleNames.Contains("ACTION"));
        Assert.IsTrue(ruleNames.Contains("OPTIONS"));
        Assert.IsTrue(ruleNames.Contains("TOKENS"));
        Assert.IsTrue(ruleNames.Contains("CHANNELS"));
    }

    [TestMethod]
    public void Antlr4Grammar_ContainsKeyParserRules()
    {
        var definition = Antlr4Grammar.Build();
        var ruleNames = definition.ParserRules.Select(r => r.Name).ToHashSet();

        Assert.IsTrue(ruleNames.Contains("grammarSpec"));
        Assert.IsTrue(ruleNames.Contains("grammarDecl"));
        Assert.IsTrue(ruleNames.Contains("grammarType"));
        Assert.IsTrue(ruleNames.Contains("prequelConstruct"));
        Assert.IsTrue(ruleNames.Contains("optionsSpec"));
        Assert.IsTrue(ruleNames.Contains("option"));
        Assert.IsTrue(ruleNames.Contains("optionValue"));
        Assert.IsTrue(ruleNames.Contains("delegateGrammars"));
        Assert.IsTrue(ruleNames.Contains("delegateGrammar"));
        Assert.IsTrue(ruleNames.Contains("tokensSpec"));
        Assert.IsTrue(ruleNames.Contains("channelsSpec"));
        Assert.IsTrue(ruleNames.Contains("idList"));
        Assert.IsTrue(ruleNames.Contains("action_"));
        Assert.IsTrue(ruleNames.Contains("actionScopeName"));
        Assert.IsTrue(ruleNames.Contains("actionBlock"));
        Assert.IsTrue(ruleNames.Contains("argActionBlock"));
        Assert.IsTrue(ruleNames.Contains("modeSpec"));
        Assert.IsTrue(ruleNames.Contains("rules"));
        Assert.IsTrue(ruleNames.Contains("ruleSpec"));
        Assert.IsTrue(ruleNames.Contains("parserRuleSpec"));
        Assert.IsTrue(ruleNames.Contains("exceptionGroup"));
        Assert.IsTrue(ruleNames.Contains("exceptionHandler"));
        Assert.IsTrue(ruleNames.Contains("finallyClause"));
        Assert.IsTrue(ruleNames.Contains("rulePrequel"));
        Assert.IsTrue(ruleNames.Contains("ruleReturns"));
        Assert.IsTrue(ruleNames.Contains("throwsSpec"));
        Assert.IsTrue(ruleNames.Contains("localsSpec"));
        Assert.IsTrue(ruleNames.Contains("ruleAction"));
        Assert.IsTrue(ruleNames.Contains("ruleModifiers"));
        Assert.IsTrue(ruleNames.Contains("ruleModifier"));
        Assert.IsTrue(ruleNames.Contains("ruleBlock"));
        Assert.IsTrue(ruleNames.Contains("ruleAltList"));
        Assert.IsTrue(ruleNames.Contains("labeledAlt"));
        Assert.IsTrue(ruleNames.Contains("lexerRuleSpec"));
        Assert.IsTrue(ruleNames.Contains("lexerRuleBlock"));
        Assert.IsTrue(ruleNames.Contains("lexerAltList"));
        Assert.IsTrue(ruleNames.Contains("lexerAlt"));
        Assert.IsTrue(ruleNames.Contains("lexerElements"));
        Assert.IsTrue(ruleNames.Contains("lexerElement"));
        Assert.IsTrue(ruleNames.Contains("lexerBlock"));
        Assert.IsTrue(ruleNames.Contains("lexerCommands"));
        Assert.IsTrue(ruleNames.Contains("lexerCommand"));
        Assert.IsTrue(ruleNames.Contains("lexerCommandName"));
        Assert.IsTrue(ruleNames.Contains("lexerCommandExpr"));
        Assert.IsTrue(ruleNames.Contains("altList"));
        Assert.IsTrue(ruleNames.Contains("alternative"));
        Assert.IsTrue(ruleNames.Contains("element"));
        Assert.IsTrue(ruleNames.Contains("labeledElement"));
        Assert.IsTrue(ruleNames.Contains("ebnf"));
        Assert.IsTrue(ruleNames.Contains("blockSuffix"));
        Assert.IsTrue(ruleNames.Contains("ebnfSuffix"));
        Assert.IsTrue(ruleNames.Contains("lexerAtom"));
        Assert.IsTrue(ruleNames.Contains("atom"));
        Assert.IsTrue(ruleNames.Contains("wildcard"));
        Assert.IsTrue(ruleNames.Contains("notSet"));
        Assert.IsTrue(ruleNames.Contains("blockSet"));
        Assert.IsTrue(ruleNames.Contains("setElement"));
        Assert.IsTrue(ruleNames.Contains("block"));
        Assert.IsTrue(ruleNames.Contains("ruleref"));
        Assert.IsTrue(ruleNames.Contains("characterRange"));
        Assert.IsTrue(ruleNames.Contains("terminalDef"));
        Assert.IsTrue(ruleNames.Contains("elementOptions"));
        Assert.IsTrue(ruleNames.Contains("elementOption"));
        Assert.IsTrue(ruleNames.Contains("identifier"));
        Assert.IsTrue(ruleNames.Contains("qualifiedIdentifier"));
    }

    [TestMethod]
    public void Antlr4Grammar_ContainsFragments()
    {
        var definition = Antlr4Grammar.Build();
        var defaultMode = definition.Modes.First(m => m.Name == "DEFAULT_MODE");

        var fragments = defaultMode.Rules.Where(r => r.IsFragment).Select(r => r.Name).ToHashSet();

        Assert.IsTrue(fragments.Contains("HexDigit"));
        Assert.IsTrue(fragments.Contains("UnicodeESC"));
        Assert.IsTrue(fragments.Contains("ESC_SEQUENCE"));
        Assert.IsTrue(fragments.Contains("NameStartChar"));
        Assert.IsTrue(fragments.Contains("NameChar"));
        Assert.IsTrue(fragments.Contains("NESTED_ACTION"));
        Assert.IsTrue(fragments.Contains("DoubleQuoteLiteral"));
    }

    [TestMethod]
    public void Antlr4Grammar_ArgumentModeRules()
    {
        var definition = Antlr4Grammar.Build();
        var argMode = definition.Modes.First(m => m.Name == "Argument");
        var ruleNames = argMode.Rules.Select(r => r.Name).ToHashSet();

        Assert.IsTrue(ruleNames.Contains("NESTED_ARGUMENT"));
        Assert.IsTrue(ruleNames.Contains("ARGUMENT_ESCAPE"));
        Assert.IsTrue(ruleNames.Contains("END_ARGUMENT"));
        Assert.IsTrue(ruleNames.Contains("ARGUMENT_CONTENT"));
    }

    [TestMethod]
    public void Antlr4Grammar_LexerCharSetModeRules()
    {
        var definition = Antlr4Grammar.Build();
        var csMode = definition.Modes.First(m => m.Name == "LexerCharSet");
        var ruleNames = csMode.Rules.Select(r => r.Name).ToHashSet();

        Assert.IsTrue(ruleNames.Contains("LEXER_CHAR_SET_BODY"));
        Assert.IsTrue(ruleNames.Contains("LEXER_CHAR_SET"));
        Assert.IsTrue(ruleNames.Contains("UNTERMINATED_CHAR_SET"));
    }

    [TestMethod]
    public void Antlr4Grammar_ResolvesSuccessfully()
    {
        var definition = Antlr4Grammar.Build();
        var resolved = RuleResolver.Resolve(definition);

        Assert.IsNotNull(resolved);
        Assert.IsTrue(resolved.AllRules.Count > 0);
    }

    [TestMethod]
    public void Antlr4Grammar_LexerRulesHaveCorrectKind()
    {
        var definition = Antlr4Grammar.Build();
        var resolved = RuleResolver.Resolve(definition);

        // Some rules that should definitely be Lexer
        Assert.AreEqual(RuleKind.Lexer, resolved.AllRules["STRING_LITERAL"].Kind);
        Assert.AreEqual(RuleKind.Lexer, resolved.AllRules["INT"].Kind);
        Assert.AreEqual(RuleKind.Lexer, resolved.AllRules["COLON"].Kind);
        Assert.AreEqual(RuleKind.Lexer, resolved.AllRules["SEMI"].Kind);
        Assert.AreEqual(RuleKind.Lexer, resolved.AllRules["WS"].Kind);
    }

    [TestMethod]
    public void Antlr4Grammar_ParserRulesHaveCorrectKind()
    {
        var definition = Antlr4Grammar.Build();
        var resolved = RuleResolver.Resolve(definition);

        // Parser rules should be inferred as Parser kind
        Assert.AreEqual(RuleKind.Parser, resolved.AllRules["grammarSpec"].Kind);
        Assert.AreEqual(RuleKind.Parser, resolved.AllRules["grammarDecl"].Kind);
        Assert.AreEqual(RuleKind.Parser, resolved.AllRules["identifier"].Kind);
    }

    [TestMethod]
    public void Antlr4Grammar_TotalRuleCount()
    {
        var definition = Antlr4Grammar.Build();

        // We expect a large number of rules covering the full ANTLR4 grammar
        var totalLexerRules = definition.Modes.Sum(m => m.Rules.Count);
        var totalParserRules = definition.ParserRules.Count;

        // At least 40 lexer rules (including fragments) and 50+ parser rules
        Assert.IsTrue(totalLexerRules >= 40,
            $"Expected at least 40 lexer rules, got {totalLexerRules}");
        Assert.IsTrue(totalParserRules >= 50,
            $"Expected at least 50 parser rules, got {totalParserRules}");
    }
}
