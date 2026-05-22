using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Resolution;
using Utils.Parser.Runtime;
using System.IO;

namespace UtilsTest.Parser;

[TestClass]
public class Antlr4GrammarConverterTests
{
    // ─── 1. Grammaire minimale ────────────────────────────────────────────────

    [TestMethod]
    public void ParseMinimalGrammar_Succeeds()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar Foo; 
            start : 'x' ;
            """);

        Assert.AreEqual("Foo", def.Name);
        Assert.AreEqual(GrammarType.Combined, def.Type);
        Assert.IsTrue(def.AllRules.ContainsKey("start"));
        Assert.AreEqual(1, def.ParserRules.Count);

        var startRule = def.AllRules["start"];
        Assert.AreEqual("start", startRule.Name);
        Assert.IsFalse(startRule.IsFragment);
    }

    // ─── 2. Noms des règles ExpGrammar-like ───────────────────────────────────

    [TestMethod]
    public void ParseExpGrammar_RuleNames()
    {
        const string grammar = """
            grammar Exp;
            eval        : additionExp ;
            additionExp : multiplyExp (('+' | '-') multiplyExp)* ;
            multiplyExp : atomExp (('*' | '/') atomExp)* ;
            atomExp     : Number | '(' additionExp ')' ;
            Number      : ('0'..'9')+ ;
            WS          : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;
        var def = Antlr4GrammarConverter.Parse(grammar);

        var allNames = def.AllRules.Keys.ToHashSet();
        Assert.IsTrue(allNames.Contains("eval"),        "Missing parser rule 'eval'");
        Assert.IsTrue(allNames.Contains("additionExp"), "Missing parser rule 'additionExp'");
        Assert.IsTrue(allNames.Contains("multiplyExp"), "Missing parser rule 'multiplyExp'");
        Assert.IsTrue(allNames.Contains("atomExp"),     "Missing parser rule 'atomExp'");
        Assert.IsTrue(allNames.Contains("Number"),      "Missing lexer rule 'Number'");
        Assert.IsTrue(allNames.Contains("WS"),          "Missing lexer rule 'WS'");

        Assert.AreEqual(RuleKind.Parser, def.AllRules["eval"].Kind);
        Assert.AreEqual(RuleKind.Lexer,  def.AllRules["Number"].Kind);
        Assert.AreEqual(RuleKind.Lexer,  def.AllRules["WS"].Kind);
    }

    // ─── 3. Lexer rule avec characterRange ('0'..'9') ─────────────────────────

    [TestMethod]
    public void ParseLexerRule_CharRange()
    {
        var def = Antlr4GrammarConverter.Parse(
            """
            grammar A; 
            start : ID ; 
            ID : 'a'..'z'+ ;
            """);

        Assert.IsTrue(def.AllRules.ContainsKey("ID"));
        var id = def.AllRules["ID"];
        Assert.AreEqual(RuleKind.Lexer, id.Kind);
        Assert.IsFalse(id.IsFragment);

        // Content should be a Quantifier(RangeMatch('a','z'), 1, null, true)
        var alt = id.Content.Alternatives[0];
        Assert.IsInstanceOfType<Quantifier>(alt.Content);
        var quant = (Quantifier)alt.Content;
        Assert.AreEqual(1, quant.Min);
        Assert.IsNull(quant.Max);
        Assert.IsTrue(quant.Greedy);

        // The inner should be the range match (directly or via Alternation)
        var inner = quant.Inner;
        if (inner is Alternation altInner)
            inner = altInner.Alternatives[0].Content;
        Assert.IsInstanceOfType<RangeMatch>(inner);
        var range = (RangeMatch)inner;
        Assert.AreEqual('a', range.From);
        Assert.AreEqual('z', range.To);
    }

    // ─── 4. Lexer rule avec -> skip ───────────────────────────────────────────

    [TestMethod]
    public void ParseLexerRule_Skip()
    {
        var def = Antlr4GrammarConverter.Parse(
            """
            grammar A; 
            r : 'x' ; 
            WS : ' '+ -> skip ;
            """);

        Assert.IsTrue(def.AllRules.ContainsKey("WS"));
        var ws = def.AllRules["WS"];
        Assert.AreEqual(RuleKind.Lexer, ws.Kind);

        // The WS rule content should contain a LexerCommand(Skip)
        bool hasSkip = ContainsLexerCommand(ws.Content, LexerCommandType.Skip);
        Assert.IsTrue(hasSkip, "WS rule should contain a skip lexer command");
    }

    // ─── 5. Parser rule avec alternatives ─────────────────────────────────────

    [TestMethod]
    public void ParseParserRule_Alternation()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar A; 
            r : 'a' | 'b' | 'c' ;
            """);

        var r = def.AllRules["r"];
        Assert.AreEqual(3, r.Content.Alternatives.Count);

        // Each alternative should contain a LiteralMatch
        foreach (var alt in r.Content.Alternatives)
        {
            var content = alt.Content;
            Assert.IsInstanceOfType<LiteralMatch>(content);
        }
    }

    // ─── 6. Quantificateurs *,+,? ─────────────────────────────────────────────

    [TestMethod]
    public void ParseParserRule_Quantifiers()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar A; 
            r : 'a'* 'b'+ 'c'? ;
            """);

        var r = def.AllRules["r"];
        var content = r.Content.Alternatives[0].Content;
        Assert.IsInstanceOfType<Sequence>(content);
        var seq = (Sequence)content;

        Assert.AreEqual(3, seq.Items.Count);

        var star = (Quantifier)seq.Items[0];
        Assert.AreEqual(0, star.Min);
        Assert.IsNull(star.Max);

        var plus = (Quantifier)seq.Items[1];
        Assert.AreEqual(1, plus.Min);
        Assert.IsNull(plus.Max);

        var opt = (Quantifier)seq.Items[2];
        Assert.AreEqual(0, opt.Min);
        Assert.AreEqual(1, opt.Max);
    }

    // ─── 7. Labels e=rule, ids+=rule ──────────────────────────────────────────

    [TestMethod]
    public void ParseParserRule_Labels()
    {
        var def = Antlr4GrammarConverter.Parse(
            """
            grammar A; 
            r : e=sub ids+=sub ; 
            sub : 'x' ;
            """);

        var r = def.AllRules["r"];
        var content = r.Content.Alternatives[0].Content;
        Assert.IsInstanceOfType<Sequence>(content);
        var seq = (Sequence)content;
        Assert.AreEqual(2, seq.Items.Count);

        // e=sub — scalar label
        var first = (RuleRef)seq.Items[0];
        Assert.IsNotNull(first.Label);
        Assert.AreEqual("e",   first.Label!.Label);
        Assert.AreEqual("sub", first.Label.RuleName);
        Assert.IsFalse(first.Label.IsAdditive);

        // ids+=sub — additive label
        var second = (RuleRef)seq.Items[1];
        Assert.IsNotNull(second.Label);
        Assert.AreEqual("ids", second.Label!.Label);
        Assert.AreEqual("sub", second.Label.RuleName);
        Assert.IsTrue(second.Label.IsAdditive);
    }


    [TestMethod]
    public void ParseParserRule_AssocRight_SetsRightAssociativity()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            expr
              : <assoc=right> expr '^' expr
              | INT
              ;
            INT : ('0'..'9')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """);

        var expr = def.AllRules["expr"];
        var powerAlternative = expr.Content.Alternatives
            .First(a => a.Content is Sequence seq && seq.Items.OfType<LiteralMatch>().Any(l => l.Value == "^"));
        Assert.AreEqual(Associativity.Right, powerAlternative.Assoc);
    }

    [TestMethod]
    public void ParseParserRule_WithoutAssocOption_DefaultsToLeftAssociativity()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            expr
              : expr '^' expr
              | INT
              ;
            INT : ('0'..'9')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """);

        var expr = def.AllRules["expr"];
        var powerAlternative = expr.Content.Alternatives
            .First(a => a.Content is Sequence seq && seq.Items.OfType<LiteralMatch>().Any(l => l.Value == "^"));
        Assert.AreEqual(Associativity.Left, powerAlternative.Assoc);
    }

    [TestMethod]
    public void ParseLexerRule_UnsupportedLexerCommand_EmitsDeterministicDiagnostic()
    {
        var diagnostics = new DiagnosticBag();

        Assert.ThrowsException<GrammarParseException>(() => Antlr4GrammarConverter.Parse("""
            grammar G;
            start : ID ;
            ID : 'a' -> customCommand ;
            """, diagnostics));

        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.UnsupportedLexerCommand.Code));
    }

    // ─── 8. Alternative label # Label ─────────────────────────────────────────

    [TestMethod]
    public void ParseParserRule_AltLabel()
    {
        var def = Antlr4GrammarConverter.Parse(
            """
            grammar A; 
            r : ID # IdAlt | '(' r ')' # ParenAlt ; 
            ID : 'a'..'z'+ ;
            """);

        var r = def.AllRules["r"];
        Assert.AreEqual(2, r.Content.Alternatives.Count);

        Assert.AreEqual("IdAlt",   r.Content.Alternatives[0].Label);
        Assert.AreEqual("ParenAlt", r.Content.Alternatives[1].Label);
    }

    // ─── 9. returns [...] ─────────────────────────────────────────────────────

    [TestMethod]
    public void RuleParameters_SimpleParameterBlock_IsPreserved()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start[int x] : 'a' ;
            """);

        var rule = def.AllRules["start"];
        Assert.IsNotNull(rule.Parameters);
        Assert.AreEqual("int x", rule.Parameters![0].Type);
    }

    [TestMethod]
    public void RuleParameters_GenericParameterBlock_IsPreserved()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start[List<int> items] : 'a' ;
            """);

        var rule = def.AllRules["start"];
        Assert.IsNotNull(rule.Parameters);
        Assert.AreEqual("List<int> items", rule.Parameters![0].Type);
    }

    [TestMethod]
    public void RuleParameters_NestedGenericArguments_AreBalanced()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start[Dictionary<string, List<int>> map] : 'a' ;
            """);

        var rule = def.AllRules["start"];
        Assert.AreEqual("Dictionary<string, List<int>> map", rule.Parameters![0].Type);
    }

    [TestMethod]
    public void RuleReturns_Block_IsPreserved()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start returns [int value] : 'a' ;
            """);

        var rule = def.AllRules["start"];
        Assert.IsNotNull(rule.Returns);
        Assert.AreEqual("int value", rule.Returns![0].Type);
    }

    [TestMethod]
    public void RuleParametersAndReturns_Both_AreCaptured()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start[int x] returns [int value] : 'a' ;
            """);

        var rule = def.AllRules["start"];
        Assert.AreEqual("int x", rule.Parameters![0].Type);
        Assert.AreEqual("int value", rule.Returns![0].Type);
    }

    [TestMethod]
    public void RuleParameters_MultilineBlock_IsPreserved()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar G;
            start[
                int x,
                string y
            ]
            : 'a'
            ;
            """);

        var raw = def.AllRules["start"].Parameters![0].Type;
        StringAssert.Contains(raw, "int x,");
        StringAssert.Contains(raw, "string y");
    }

    [TestMethod]
    public void RuleParameters_MalformedBlock_ProducesDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        Assert.ThrowsException<GrammarParseException>(() => Antlr4GrammarConverter.Parse("""
            grammar G;
            start[List<int x : 'a' ;
            """, diagnostics));
    }


    [TestMethod]
    public void Converter_LocalsClause_EmitsExplicitDiagnostic()
    {
        var diagnostics = new DiagnosticBag();

        _ = Antlr4GrammarConverter.Parse("""
            grammar G;
            start returns [int value] locals [int x] : 'a' ;
            """, diagnostics);

        var matches = diagnostics
            .Where(d => d.Code == ParserDiagnostics.RuleLocalsIgnored.Code)
            .ToList();

        Assert.AreEqual(1, matches.Count);
        Assert.AreEqual("UP1008", matches[0].Code);
        StringAssert.Contains(matches[0].Message, "recognized but ignored");
        StringAssert.Contains(matches[0].Message, "start");
    }

    [TestMethod]
    public void Converter_ReturnsAndLocals_EmitIndependentDiagnostics()
    {
        var diagnostics = new DiagnosticBag();

        _ = Antlr4GrammarConverter.Parse("""
            grammar G;
            start returns [int value] locals [int x] : 'a' ;
            """, diagnostics);

        Assert.AreEqual(1, diagnostics.Count(d => d.Code == ParserDiagnostics.RuleReturnsIgnored.Code));
        Assert.AreEqual(1, diagnostics.Count(d => d.Code == ParserDiagnostics.RuleLocalsIgnored.Code));
    }

    // ─── 10. Fragment rule ────────────────────────────────────────────────────

    [TestMethod]
    public void ParseLexerRule_Fragment()
    {
        var def = Antlr4GrammarConverter.Parse("""
            
            grammar A; 
            r : DIGIT+ ; 
            fragment DIGIT : '0'..'9' ;
            """);
            

        Assert.IsTrue(def.AllRules.ContainsKey("DIGIT"));
        var digit = def.AllRules["DIGIT"];
        Assert.IsTrue(digit.IsFragment);
        Assert.AreEqual(RuleKind.Lexer, digit.Kind);

        Assert.IsTrue(def.AllRules.ContainsKey("r"));
        Assert.AreEqual(RuleKind.Parser, def.AllRules["r"].Kind);
    }

    // ─── 11. options { tokenVocab=X; } ───────────────────────────────────────

    [TestMethod]
    public void ParseOptionsSpec()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar A; 
            options { tokenVocab=MyLexer; } r : 'x' ;
            """);

        Assert.IsNotNull(def.Options);
        Assert.IsTrue(def.Options!.Values.ContainsKey("tokenVocab"));
        Assert.AreEqual("MyLexer", def.Options.Values["tokenVocab"]);
    }

    [TestMethod]
    public void ParseCaseInsensitiveOption_AllowsUppercaseLexerInput()
    {
        var def = Antlr4GrammarConverter.Parse("""
            lexer grammar A;
            options { caseInsensitive=true; }
            WORD : 'abc' ;
            """);

        var lexer = new LexerEngine(def);
        var tokens = lexer.Tokenize(new StringReader("ABC")).ToList();

        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("WORD", tokens[0].RuleName);
        Assert.AreEqual("ABC", tokens[0].Text);
    }

    [TestMethod]
    public void ParseCaseInsensitiveFalse_KeepsExistingCaseSensitiveBehavior()
    {
        var def = Antlr4GrammarConverter.Parse("""
            lexer grammar A;
            options { caseInsensitive=false; }
            WORD : 'abc' ;
            """);

        var lexer = new LexerEngine(def);
        var tokens = lexer.Tokenize(new StringReader("ABC")).ToList();

        Assert.IsTrue(tokens.Count > 0);
        Assert.IsTrue(tokens.All(token => token.RuleName == "ERROR"));
    }

    // ─── 12. mode spec ────────────────────────────────────────────────────────

    [TestMethod]
    public void ParseModeSpec()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar M;
            r : 'x' ;
            mode ARG;
            ARG_CONTENT : . -> more ;
            """);
        // Should have an extra mode named ARG
        var argMode = def.Modes.FirstOrDefault(m => m.Name == "ARG");
        Assert.IsNotNull(argMode, "Expected mode 'ARG'");
        Assert.AreEqual(1, argMode!.Rules.Count);
        Assert.AreEqual("ARG_CONTENT", argMode.Rules[0].Name);
    }

    // ─── 13. lexer grammar type ───────────────────────────────────────────────

    [TestMethod]
    public void ParseLexerGrammarType()
    {
        var def = Antlr4GrammarConverter.Parse("""
            lexer grammar L; 
            A : 'a' ; 
            B : 'b'..'z' ;
            """);

        Assert.AreEqual("L", def.Name);
        Assert.AreEqual(GrammarType.Lexer, def.Type);
        Assert.AreEqual(0, def.ParserRules.Count);
        Assert.IsTrue(def.AllRules.ContainsKey("A"));
        Assert.IsTrue(def.AllRules.ContainsKey("B"));
    }

    [TestMethod]
    public void ParseLexerGrammar_WithParserRule_ThrowsValidation()
    {
        Assert.ThrowsExactly<GrammarValidationException>(() =>
            Antlr4GrammarConverter.Parse("""
                lexer grammar L;
                start : TOKEN ;
                TOKEN : 'a' ;
                """));
    }

    [TestMethod]
    public void ParseParserGrammar_WithLexerRule_ThrowsValidation()
    {
        Assert.ThrowsExactly<GrammarValidationException>(() =>
            Antlr4GrammarConverter.Parse("""
                parser grammar P;
                start : TOKEN ;
                TOKEN : 'a' ;
                """));
    }

    // ─── 14. combined grammar type ────────────────────────────────────────────

    [TestMethod]
    public void ParseCombinedGrammarType()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar C; 
            r : 'x' ;
            """);

        Assert.AreEqual(GrammarType.Combined, def.Type);
    }

    [TestMethod]
    public void ParseCombinedGrammar_WithLexerAndParserRules_IsValid()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar C;
            start : TOKEN ;
            TOKEN : 'x' ;
            """);

        Assert.AreEqual(1, def.ParserRules.Count);
        Assert.IsTrue(def.Modes[0].Rules.Any(rule => rule.Name == "TOKEN"));
    }

    [TestMethod]
    public void ParseSuperClass_InParserGrammar_PopulatesParserSuperClass()
    {
        var def = Antlr4GrammarConverter.Parse("""
            parser grammar P;
            options { superClass=MyBaseParser; }
            start : 'x' ;
            """);

        Assert.AreEqual("MyBaseParser", def.EffectiveOptions.ParserSuperClass);
        Assert.IsNull(def.EffectiveOptions.LexerSuperClass);
    }

    [TestMethod]
    public void ParseSuperClass_InLexerGrammar_PopulatesLexerSuperClass()
    {
        var def = Antlr4GrammarConverter.Parse("""
            lexer grammar L;
            options { superClass=MyBaseLexer; }
            ID : 'a' ;
            """);

        Assert.AreEqual("MyBaseLexer", def.EffectiveOptions.LexerSuperClass);
        Assert.IsNull(def.EffectiveOptions.ParserSuperClass);
    }

    [TestMethod]
    public void ParseSuperClass_InCombinedGrammar_PopulatesParserSuperClass()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar C;
            options { superClass=MyBaseParser; }
            start : ID ;
            ID : 'a' ;
            """);

        Assert.AreEqual("MyBaseParser", def.EffectiveOptions.ParserSuperClass);
        Assert.IsNull(def.EffectiveOptions.LexerSuperClass);
    }

    [TestMethod]
    public void ParseSuperClass_DoesNotEmitUnsupportedOptionDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        Antlr4GrammarConverter.Parse("""
            grammar C;
            options { superClass=MyBaseParser; }
            start : 'x' ;
            """, diagnostics);

        Assert.IsFalse(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.UnsupportedAntlrOptionIgnored.Code));
    }

    [TestMethod]
    public void ParseCaseInsensitive_DoesNotEmitUnsupportedOptionDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        Antlr4GrammarConverter.Parse("""
            grammar C;
            options { caseInsensitive=true; }
            start : 'x' ;
            """, diagnostics);

        Assert.IsFalse(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.UnsupportedAntlrOptionIgnored.Code));
    }

    [DataTestMethod]
    [DataRow("CSharp")]
    [DataRow("Java")]
    public void ParseLanguageOption_EmitsWarning(string language)
    {
        var diagnostics = new DiagnosticBag();
        Antlr4GrammarConverter.Parse($$"""
            grammar C;
            options { language={{language}}; }
            start : 'x' ;
            """, diagnostics);

        Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.UnsupportedAntlrLanguageOptionIgnored.Code));
    }

    [TestMethod]
    public void ParseUnsupportedOption_EmitsExplicitDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        Antlr4GrammarConverter.Parse("""
            grammar C;
            options { visitor=true; }
            start : 'x' ;
            """, diagnostics);

        Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.UnsupportedAntlrOptionIgnored.Code));
    }

    [TestMethod]
    public void ParseTokensAndChannelsSpec_EmitsPartialSupportDiagnostics()
    {
        var diagnostics = new DiagnosticBag();
        Antlr4GrammarConverter.Parse("""
            grammar C;
            tokens { A, B }
            channels { COMMENTS }
            start : 'x' ;
            """, diagnostics);

        Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.TokensBlockIgnored.Code));
        Assert.IsTrue(diagnostics.Any(diagnostic => diagnostic.Code == ParserDiagnostics.ChannelsBlockIgnored.Code));
    }

    // ─── 15. grammaire invalide → exception ──────────────────────────────────

    [TestMethod]
    public void Parse_InvalidGrammar_Throws()
    {
        Assert.ThrowsException<GrammarParseException>(() =>
            Antlr4GrammarConverter.Parse("not a valid grammar at all"));
    }

    // ─── Action block / semantic predicate parsing ───────────────────────────

    [TestMethod]
    public void ParseGrammar_WithSemanticPredicate_ProducesValidatingPredicate()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar P;
            start : {canProceed}? A ;
            A : 'a' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, diagnostics: null);

        Assert.AreEqual(1, def.ParserRules.Count);
        var startRule = def.AllRules["start"];
        var alt = ((Alternation)startRule.Content).Alternatives[0];
        var seq = (Sequence)alt.Content;
        Assert.IsInstanceOfType<ValidatingPredicate>(seq.Items[0]);
        Assert.AreEqual("canProceed", ((ValidatingPredicate)seq.Items[0]).Code);
    }

    [TestMethod]
    public void ParseGrammar_ActionWithLineComment_Succeeds()
    {
        var def = Antlr4GrammarConverter.Parse(
            "grammar P;\nstart : {canProceed // check\n}? A ;\nA : 'a' ;\n",
            diagnostics: null);

        var alt = ((Alternation)def.AllRules["start"].Content).Alternatives[0];
        var pred = (ValidatingPredicate)((Sequence)alt.Content).Items[0];
        StringAssert.StartsWith(pred.Code, "canProceed");
    }

    [TestMethod]
    public void ParseGrammar_ActionWithBlockComment_Succeeds()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar P;
            start : {/* check */ canProceed}? A ;
            A : 'a' ;
            """, diagnostics: null);

        var alt = ((Alternation)def.AllRules["start"].Content).Alternatives[0];
        var pred = (ValidatingPredicate)((Sequence)alt.Content).Items[0];
        StringAssert.Contains(pred.Code, "canProceed");
    }

    [TestMethod]
    public void ParseGrammar_ActionWithBlockComment_ContainingClosingBrace_Succeeds()
    {
        // Verifies that '}' inside /* ... */ does not prematurely terminate the action block.
        var def = Antlr4GrammarConverter.Parse("""
            grammar P;
            start : {/* } */ canProceed}? A ;
            A : 'a' ;
            """, diagnostics: null);

        var alt = ((Alternation)def.AllRules["start"].Content).Alternatives[0];
        var pred = (ValidatingPredicate)((Sequence)alt.Content).Items[0];
        StringAssert.Contains(pred.Code, "canProceed");
    }

    [TestMethod]
    public void ParseGrammar_ActionWithNestedBraces_Succeeds()
    {
        var def = Antlr4GrammarConverter.Parse("""
            grammar P;
            start : {f({x})}? A ;
            A : 'a' ;
            """, diagnostics: null);

        var alt = ((Alternation)def.AllRules["start"].Content).Alternatives[0];
        var pred = (ValidatingPredicate)((Sequence)alt.Content).Items[0];
        StringAssert.Contains(pred.Code, "f(");
    }

    [TestMethod]
    public void ParseGrammar_WithBlockCommentInBody_Succeeds()
    {
        // Verifies that multi-character block comments in grammar text are skipped correctly.
        var def = Antlr4GrammarConverter.Parse("""
            grammar P;
            /* this is a block comment */
            start : A ;
            A : 'a' ;
            """, diagnostics: null);

        Assert.IsTrue(def.AllRules.ContainsKey("start"));
    }

    // ─── Utilitaires ─────────────────────────────────────────────────────────

    private static bool ContainsLexerCommand(RuleContent content, LexerCommandType type)
    {
        return content switch
        {
            LexerCommand cmd     => cmd.Type == type,
            Sequence seq         => seq.Items.Any(i => ContainsLexerCommand(i, type)),
            Alternation a        => a.Alternatives.Any(alt => ContainsLexerCommand(alt.Content, type)),
            Alternative alt      => ContainsLexerCommand(alt.Content, type),
            Quantifier q         => ContainsLexerCommand(q.Inner, type),
            _                    => false
        };
    }
}
