using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Utils.Parser.Generators.Internal;
using Utils.Parser.Model;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Tests for the Utils.Parser.Generators source generator pipeline:
/// G4Tokenizer → G4Parser → GrammarEmitter, and validation of the
/// class generated from <c>Exp.g4</c> at build time.
/// </summary>
[TestClass]
public class Antlr4GrammarGeneratorTests
{
    // ── G4Tokenizer ───────────────────────────────────────────────────────────

    [TestMethod]
    public void Tokenizer_GrammarDeclaration_ProducesCorrectTokens()
    {
        var tokens = Tokenize("grammar Exp;");

        Assert.AreEqual(4, tokens.Length);
        Assert.AreEqual(G4TokenKind.Identifier, tokens[0].Kind); Assert.AreEqual("grammar", tokens[0].Value);
        Assert.AreEqual(G4TokenKind.Identifier, tokens[1].Kind); Assert.AreEqual("Exp",     tokens[1].Value);
        Assert.AreEqual(G4TokenKind.Semi,       tokens[2].Kind);
        Assert.AreEqual(G4TokenKind.Eof,        tokens[3].Kind);
    }

    [TestMethod]
    public void Tokenizer_StringLiteral_DecodesValue()
    {
        var tokens = Tokenize("'hello'");
        Assert.AreEqual(G4TokenKind.StringLiteral, tokens[0].Kind);
        Assert.AreEqual("hello", tokens[0].Value);
    }

    [TestMethod]
    public void Tokenizer_StringLiteral_DecodesEscape()
    {
        var tokens = Tokenize(@"'\n'");
        Assert.AreEqual(G4TokenKind.StringLiteral, tokens[0].Kind);
        Assert.AreEqual("\n", tokens[0].Value);
    }

    [TestMethod]
    public void Tokenizer_CharClass_PreservesRawContent()
    {
        var tokens = Tokenize("[a-z0-9]");
        Assert.AreEqual(G4TokenKind.CharClass, tokens[0].Kind);
        Assert.AreEqual("a-z0-9", tokens[0].Value);
    }

    [TestMethod]
    public void Tokenizer_Arrow_Recognized()
    {
        var tokens = Tokenize("-> skip");
        Assert.AreEqual(G4TokenKind.Arrow,      tokens[0].Kind);
        Assert.AreEqual(G4TokenKind.Identifier, tokens[1].Kind);
        Assert.AreEqual("skip",                 tokens[1].Value);
    }

    [TestMethod]
    public void Tokenizer_BraceBlock_CapturesContent()
    {
        var tokens = Tokenize("{ int x = 1; }");
        Assert.AreEqual(G4TokenKind.BraceBlock, tokens[0].Kind);
        Assert.AreEqual(" int x = 1; ",         tokens[0].Value);
    }

    [TestMethod]
    public void Tokenizer_LineComment_IsSkipped()
    {
        var tokens = Tokenize("// ignored\nfoo");
        Assert.AreEqual(2, tokens.Length); // foo + Eof
        Assert.AreEqual("foo", tokens[0].Value);
    }

    [TestMethod]
    public void Tokenizer_BlockComment_IsSkipped()
    {
        var tokens = Tokenize("/* ignored */ foo");
        Assert.AreEqual(2, tokens.Length);
        Assert.AreEqual("foo", tokens[0].Value);
    }

    [TestMethod]
    public void Tokenizer_Quantifiers_Recognized()
    {
        var tokens = Tokenize("* + ?");
        Assert.AreEqual(G4TokenKind.Star,  tokens[0].Kind);
        Assert.AreEqual(G4TokenKind.Plus,  tokens[1].Kind);
        Assert.AreEqual(G4TokenKind.QMark, tokens[2].Kind);
    }

    [TestMethod]
    public void Tokenizer_DotDot_Recognized()
    {
        var tokens = Tokenize("'0'..'9'");
        Assert.AreEqual(G4TokenKind.StringLiteral, tokens[0].Kind);
        Assert.AreEqual(G4TokenKind.DotDot,        tokens[1].Kind);
        Assert.AreEqual(G4TokenKind.StringLiteral, tokens[2].Kind);
    }

    [TestMethod]
    public void Tokenizer_LineNumbers_TrackCorrectly()
    {
        var tokens = Tokenize("foo\nbar\nbaz");
        Assert.AreEqual(1, tokens[0].Line); // foo
        Assert.AreEqual(2, tokens[1].Line); // bar
        Assert.AreEqual(3, tokens[2].Line); // baz
    }

    // ── G4Parser ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void Parser_CombinedGrammarDeclaration()
    {
        var g = Parse("grammar Exp; rule : 'a' ;");
        Assert.AreEqual("Exp",              g.Name);
        Assert.AreEqual(G4GrammarKind.Combined, g.Kind);
    }

    [TestMethod]
    public void Parser_LexerGrammarDeclaration()
    {
        var g = Parse("lexer grammar Lex; TOKEN : 'a' ;");
        Assert.AreEqual("Lex",           g.Name);
        Assert.AreEqual(G4GrammarKind.Lexer, g.Kind);
    }

    [TestMethod]
    public void Parser_ParserGrammarDeclaration()
    {
        var g = Parse("parser grammar Par; rule : TOKEN ;");
        Assert.AreEqual("Par",            g.Name);
        Assert.AreEqual(G4GrammarKind.Parser, g.Kind);
    }

    [TestMethod]
    public void Parser_LexerAndParserRulesAreClassifiedCorrectly()
    {
        var g = Parse("grammar G; parserRule : TOKEN ; TOKEN : 'a' ;");
        Assert.AreEqual(1, g.ParserRules.Count);
        Assert.AreEqual("parserRule", g.ParserRules[0].Name);
        Assert.AreEqual(1, g.LexerRules.Count);
        Assert.AreEqual("TOKEN",      g.LexerRules[0].Name);
    }

    [TestMethod]
    public void Parser_FragmentRuleIsMarked()
    {
        var g = Parse("grammar G; fragment DIGIT : [0-9] ;");
        Assert.AreEqual(1, g.LexerRules.Count);
        Assert.IsTrue(g.LexerRules[0].IsFragment);
        Assert.AreEqual("DIGIT", g.LexerRules[0].Name);
    }

    [TestMethod]
    public void Parser_Alternation_CountsAlternativesCorrectly()
    {
        var g   = Parse("grammar G; rule : 'a' | 'b' | 'c' ;");
        var alt = (G4Alternation)g.ParserRules[0].Content;
        Assert.AreEqual(3, alt.Alternatives.Count);
    }

    [TestMethod]
    public void Parser_Quantifiers_ParsedWithCorrectBounds()
    {
        var g    = Parse("grammar G; rule : 'a'* 'b'+ 'c'? ;");
        var items = ((G4Alternation)g.ParserRules[0].Content).Alternatives[0].Items;
        Assert.AreEqual(3, items.Count);

        var star  = (G4Quantifier)items[0];
        Assert.AreEqual(0, star.Min);  Assert.IsNull(star.Max);  Assert.IsTrue(star.Greedy);

        var plus  = (G4Quantifier)items[1];
        Assert.AreEqual(1, plus.Min);  Assert.IsNull(plus.Max);  Assert.IsTrue(plus.Greedy);

        var qmark = (G4Quantifier)items[2];
        Assert.AreEqual(0, qmark.Min); Assert.AreEqual(1, qmark.Max); Assert.IsTrue(qmark.Greedy);
    }

    [TestMethod]
    public void Parser_NonGreedyQuantifier_MarkedCorrectly()
    {
        var g     = Parse("grammar G; rule : 'a'*? ;");
        var items = ((G4Alternation)g.ParserRules[0].Content).Alternatives[0].Items;
        var q     = (G4Quantifier)items[0];
        Assert.IsFalse(q.Greedy);
    }

    [TestMethod]
    public void Parser_LexerCommand_ParsedCorrectly()
    {
        var g     = Parse("grammar G; WS : ' '+ -> skip ;");
        var items = ((G4Alternation)g.LexerRules[0].Content).Alternatives[0].Items;
        var cmd   = items.OfType<G4LexerCommand>().Single();
        Assert.AreEqual("skip", cmd.Name);
        Assert.IsNull(cmd.Arg);
    }

    [TestMethod]
    public void Parser_RangeMatch_ParsedCorrectly()
    {
        var g     = Parse("grammar G; DIGIT : '0'..'9' ;");
        var items = ((G4Alternation)g.LexerRules[0].Content).Alternatives[0].Items;
        var range = items.OfType<G4RangeMatch>().Single();
        Assert.AreEqual('0', range.From);
        Assert.AreEqual('9', range.To);
    }

    [TestMethod]
    public void Parser_ExpGrammar_ProducesCorrectRuleCount()
    {
        var g = Parse(ExpG4);
        Assert.AreEqual("Exp", g.Name);
        Assert.AreEqual(G4GrammarKind.Combined, g.Kind);
        Assert.AreEqual(4, g.ParserRules.Count);
        Assert.AreEqual(8, g.LexerRules.Count); // Number PLUS MINUS MULT DIV LPAREN RPAREN WS
    }

    [TestMethod]
    public void Parser_GrammarOptions_AreCaptured()
    {
        var grammar = Parse("""
            grammar G;
            options { caseInsensitive = true; tokenVocab = CommonLexer; }
            rule : 'a' ;
            """);

        Assert.AreEqual("true", grammar.Options["caseInsensitive"]);
        Assert.AreEqual("CommonLexer", grammar.Options["tokenVocab"]);
    }

    [TestMethod]
    public void Emitter_StringSyntaxName_RemovesTrailingGrammarSuffix()
    {
        var src = Emit("grammar SqlQueryGrammar; query : SELECT ; SELECT : 'SELECT' ;", "", "Cls", "g.g4");
        StringAssert.Contains(src, "public const string StringSyntaxName = \"SqlQuery\";");
    }

    [TestMethod]
    public void Emitter_StringSyntaxKeywords_IncludeCaseInsensitiveLiteralRules()
    {
        var src = Emit("""
            grammar SqlQueryGrammar;
            query : SELECT fromClause identifier ;
            fromClause : FROM identifier ;
            identifier : IDENTIFIER ;
            SELECT : [Ss] [Ee] [Ll] [Ee] [Cc] [Tt] ;
            FROM : 'from' ;
            IDENTIFIER : [a-z]+ ;
            """, "", "Cls", "g.g4");

        StringAssert.Contains(src, "public static IReadOnlyList<string> StringSyntaxKeywords");
        StringAssert.Contains(src, "\"SELECT\"");
        StringAssert.Contains(src, "\"FROM\"");
        Assert.IsFalse(src.Contains("StringSyntaxKeywords { get; } = new string[] { \"FROM\", \"IDENTIFIER\""));
        Assert.IsFalse(src.Contains("StringSyntaxKeywords { get; } = new string[] { \"IDENTIFIER\""));
    }

    [TestMethod]
    public void Emitter_StringSyntaxNonAlphanumericTokens_IncludeOperatorsAndPunctuation()
    {
        var src = Emit("""
            grammar SqlQueryGrammar;
            query : SELECT PLUS LPAREN LTE identifier ;
            identifier : IDENTIFIER ;
            SELECT : 'select' ;
            PLUS : '+' ;
            LPAREN : '(' ;
            LTE : '<=' ;
            IDENTIFIER : [a-z]+ ;
            """, "", "Cls", "g.g4");

        StringAssert.Contains(src, "public static IReadOnlyList<string> StringSyntaxNonAlphanumericTokens");
        StringAssert.Contains(src, "\"+\"");
        StringAssert.Contains(src, "\"(\"");
        StringAssert.Contains(src, "\"<=\"");
        Assert.IsFalse(src.Contains("StringSyntaxNonAlphanumericTokens { get; } = new string[] { \"(\", \"+\", \"<=\", \"select\" }"));
    }

    [TestMethod]
    public void Emitter_StringSyntaxNumberAndStringRules_IncludeMatchingRuleNames()
    {
        var src = Emit("""
            grammar SqlQueryGrammar;
            query : INTEGER_NUMBER QUOTED_STRING RAW_STRING identifier ;
            identifier : IDENTIFIER ;
            INTEGER_NUMBER : [0-9]+ ;
            QUOTED_STRING : '"' ~["\r\n]* '"' ;
            RAW_STRING : '\'' ~['\r\n]* '\'' ;
            IDENTIFIER : [a-z]+ ;
            """, "", "Cls", "g.g4");

        StringAssert.Contains(src, "public static IReadOnlyList<string> StringSyntaxNumberRules");
        StringAssert.Contains(src, "\"INTEGER_NUMBER\"");
        StringAssert.Contains(src, "public static IReadOnlyList<string> StringSyntaxStringRules");
        StringAssert.Contains(src, "\"QUOTED_STRING\"");
        StringAssert.Contains(src, "\"RAW_STRING\"");
        Assert.IsFalse(src.Contains("StringSyntaxNumberRules { get; } = new string[] { \"IDENTIFIER\""));
        Assert.IsFalse(src.Contains("StringSyntaxStringRules { get; } = new string[] { \"IDENTIFIER\""));
    }

    // ── GrammarEmitter ────────────────────────────────────────────────────────

    [TestMethod]
    public void Emitter_ContainsAutoGeneratedHeader()
    {
        var src = Emit("grammar G; rule : 'a' ;", "Ns", "Cls", "g.g4");
        StringAssert.Contains(src, "// <auto-generated/>");
        StringAssert.Contains(src, "// Source: g.g4");
    }

    [TestMethod]
    public void Emitter_ContainsNamespaceDeclaration()
    {
        var src = Emit("grammar G; rule : 'a' ;", "My.Namespace", "Cls", "g.g4");
        StringAssert.Contains(src, "namespace My.Namespace;");
    }

    [TestMethod]
    public void Emitter_EmptyNamespace_NoNamespaceStatement()
    {
        var src = Emit("grammar G; rule : 'a' ;", "", "Cls", "g.g4");
        Assert.IsFalse(src.Contains("namespace ;"));
        Assert.IsFalse(src.Contains("namespace  ;"));
    }

    [TestMethod]
    public void Emitter_ContainsPartialClassDeclaration()
    {
        var src = Emit("grammar G; rule : 'a' ;", "Ns", "MyGrammar", "g.g4");
        StringAssert.Contains(src, "internal static partial class MyGrammar");
    }

    [TestMethod]
    public void Emitter_ContainsBuildDefinitionMethod()
    {
        var src = Emit("grammar G; rule : 'a' ;", "", "Cls", "g.g4");
        StringAssert.Contains(src, "public static ParserDefinition BuildDefinition()");
    }

    [TestMethod]
    public void Emitter_ContainsBuildConvenienceMethod()
    {
        var src = Emit("grammar G; rule : 'a' ;", "", "Cls", "g.g4");
        StringAssert.Contains(src, "public static ParserDefinition Build()");
    }

    [TestMethod]
    public void Emitter_ContainsGrammarProperty()
    {
        var src = Emit("grammar G; rule : 'a' ;", "", "Cls", "g.g4");
        StringAssert.Contains(src, "public static CompiledGrammar Grammar");
    }

    [TestMethod]
    public void Emitter_ContainsStringSyntaxAnnotatedParseHelpers()
    {
        var src = Emit("grammar SqlQueryGrammar; rule : 'a' ;", "", "Cls", "g.g4");
        StringAssert.Contains(src, "[global::System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxName, typeof(Cls))] string input");
        StringAssert.Contains(src, "public static IReadOnlyList<Token> Tokenize");
        StringAssert.Contains(src, "public static ParseNode Parse");
    }

    [TestMethod]
    public void Emitter_ContainsRuleVariable_ForEachRule()
    {
        var src = Emit("grammar G; myRule : 'x' ; TOKEN : 'y' ;", "", "Cls", "g.g4");
        StringAssert.Contains(src, "var _myRule =");
        StringAssert.Contains(src, "var _TOKEN =");
    }

    [TestMethod]
    public void Emitter_GrammarName_IsEmbeddedInOutput()
    {
        var src = Emit("grammar HelloWorld; rule : 'a' ;", "", "Cls", "g.g4");
        StringAssert.Contains(src, "\"HelloWorld\"");
    }

    [TestMethod]
    public void Emitter_GeneratedCodeAttribute_Present()
    {
        var src = Emit("grammar G; rule : 'a' ;", "", "Cls", "g.g4");
        StringAssert.Contains(src, "[global::System.CodeDom.Compiler.GeneratedCode");
    }

    [TestMethod]
    public void Emitter_GrammarOptions_AreIncludedInOutput()
    {
        var src = Emit("""
            grammar G;
            options { caseInsensitive = true; }
            rule : 'a' ;
            """, "", "Cls", "g.g4");

        StringAssert.Contains(src, "Options: new GrammarOptions");
        StringAssert.Contains(src, "[\"caseInsensitive\"] = \"true\"");
    }

    // ── Generated ExpGrammar (integration) ───────────────────────────────────

    [TestMethod]
    public void GeneratedExpGrammar_HasCorrectName()
    {
        Assert.AreEqual("Exp", ExpGrammar.BuildDefinition().Name);
    }

    [TestMethod]
    public void GeneratedExpGrammar_IsCombinedGrammar()
    {
        Assert.AreEqual(GrammarType.Combined, ExpGrammar.BuildDefinition().Type);
    }

    [TestMethod]
    public void GeneratedExpGrammar_HasFourParserRules()
    {
        var def = ExpGrammar.BuildDefinition();
        Assert.AreEqual(4, def.ParserRules.Count);
        CollectionAssert.AreEquivalent(
            new[] { "eval", "additionExp", "multiplyExp", "atomExp" },
            def.ParserRules.Select(r => r.Name).ToArray());
    }

    [TestMethod]
    public void GeneratedExpGrammar_RootRuleIsEval()
    {
        var def = ExpGrammar.BuildDefinition();
        Assert.IsNotNull(def.RootRule);
        Assert.AreEqual("eval", def.RootRule!.Name);
    }

    [TestMethod]
    public void GeneratedExpGrammar_AllRulesAreResolved()
    {
        var def = ExpGrammar.BuildDefinition();
        // RuleResolver populates AllRules
        Assert.IsTrue(def.AllRules.Count > 0);
        Assert.IsTrue(def.AllRules.ContainsKey("eval"));
        Assert.IsTrue(def.AllRules.ContainsKey("Number"));
    }

    [TestMethod]
    public void GeneratedExpGrammar_GrammarProperty_CanParseExpression()
    {
        var root = ExpGrammar.Grammar.Parse("2+5");
        Assert.IsInstanceOfType<ParserNode>(root);
    }

    [TestMethod]
    public void GeneratedExpGrammar_BuildAndBuildDefinition_ReturnEquivalentGrammars()
    {
        var a = ExpGrammar.Build();
        var b = ExpGrammar.BuildDefinition();
        Assert.AreEqual(a.Name,                   b.Name);
        Assert.AreEqual(a.Type,                   b.Type);
        Assert.AreEqual(a.ParserRules.Count,      b.ParserRules.Count);
        Assert.AreEqual(a.RootRule?.Name,         b.RootRule?.Name);
    }

    [TestMethod]
    public void GeneratedExpGrammar_FileIsEmittedUnderObjFolder()
    {
        var projectDirectory = GetProjectDirectory();
        var generatedFiles = Directory.GetFiles(projectDirectory, "ExpGrammar.Grammar.g.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToArray();

        Assert.AreEqual(1, generatedFiles.Length);
        StringAssert.Contains(File.ReadAllText(generatedFiles[0]), "// Source: Exp.g4");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static G4Token[] Tokenize(string text) =>
        new G4Tokenizer(text).Tokenize().ToArray();

    private static G4Grammar Parse(string g4Text)
    {
        var tokens = new G4Tokenizer(g4Text).Tokenize();
        return new G4Parser(tokens).Parse();
    }

    private static string Emit(string g4Text, string ns, string cls, string file)
    {
        var grammar = Parse(g4Text);
        return GrammarEmitter.Emit(grammar, ns, cls, file);
    }

    private static string GetProjectDirectory()
    {
        var currentDirectory = AppContext.BaseDirectory;

        while (!string.IsNullOrEmpty(currentDirectory))
        {
            if (File.Exists(Path.Combine(currentDirectory, "UtilsTest.csproj")))
                return currentDirectory;

            currentDirectory = Path.GetDirectoryName(currentDirectory);
        }

        throw new DirectoryNotFoundException("Unable to locate the UtilsTest project directory.");
    }

    // Exp.g4 content embedded so the test is self-contained
    private const string ExpG4 = """
        grammar Exp;
        eval        : additionExp ;
        additionExp : multiplyExp ('+' multiplyExp | '-' multiplyExp)* ;
        multiplyExp : atomExp ('*' atomExp | '/' atomExp)* ;
        atomExp     : Number | '(' additionExp ')' ;
        Number : ('0'..'9')+ ('.' ('0'..'9')+)? ;
        PLUS   : '+' ;
        MINUS  : '-' ;
        MULT   : '*' ;
        DIV    : '/' ;
        LPAREN : '(' ;
        RPAREN : ')' ;
        WS     : (' ' | '\t' | '\r' | '\n')+ -> skip ;
        """;
}
