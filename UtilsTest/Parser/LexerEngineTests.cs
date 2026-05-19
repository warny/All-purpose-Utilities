using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Runtime;
using Utils.Parser.Resolution;
using System.IO;
using Utils.Parser.Model;

namespace UtilsTest.Parser;

[TestClass]
public class LexerEngineTests
{
    private static List<Token> Tokenize(string input)
    {
        var definition = ExpGrammar.Build();
        var lexer = new LexerEngine(definition);
        var stream = new StringReader(input);
        return lexer.Tokenize(stream).ToList();
    }

    [TestMethod]
    public void Lexer_LineAndColumn_AreComputed()
    {
        var tokens = Tokenize("1\n2");
        Assert.AreEqual(2, tokens.Count);
        Assert.AreEqual(1, tokens[0].Span.Line);
        Assert.AreEqual(1, tokens[0].Span.Column);
        Assert.AreEqual(2, tokens[1].Span.Line);
        Assert.AreEqual(1, tokens[1].Span.Column);
    }

    // ═══════════════════════════════════════════════════════════════
    // Simple number tokenization
    // ═══════════════════════════════════════════════════════════════

    [DataTestMethod]
    [DataRow("42")]
    [DataRow("7")]
    [DataRow("3.14")]
    [DataRow("123456789")]
    [DataRow("100.005")]
    public void Lexer_NumberToken_IsTokenizedAsSingleToken(string input)
    {
        var tokens = Tokenize(input);
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("Number", tokens[0].RuleName);
        Assert.AreEqual(input, tokens[0].Text);
    }

    // ═══════════════════════════════════════════════════════════════
    // Operator tokenization
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Lexer_AllOperators()
    {
        var tokens = Tokenize("+-*/");
        Assert.AreEqual(4, tokens.Count);
        Assert.AreEqual("PLUS", tokens[0].RuleName);
        Assert.AreEqual("+", tokens[0].Text);
        Assert.AreEqual("MINUS", tokens[1].RuleName);
        Assert.AreEqual("-", tokens[1].Text);
        Assert.AreEqual("MULT", tokens[2].RuleName);
        Assert.AreEqual("*", tokens[2].Text);
        Assert.AreEqual("DIV", tokens[3].RuleName);
        Assert.AreEqual("/", tokens[3].Text);
    }

    [TestMethod]
    public void Lexer_Parentheses()
    {
        var tokens = Tokenize("()");
        Assert.AreEqual(2, tokens.Count);
        Assert.AreEqual("LPAREN", tokens[0].RuleName);
        Assert.AreEqual("RPAREN", tokens[1].RuleName);
    }

    // ═══════════════════════════════════════════════════════════════
    // Whitespace handling (skip)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Lexer_WhitespaceIsSkipped()
    {
        var tokens = Tokenize("  42  ");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("Number", tokens[0].RuleName);
        Assert.AreEqual("42", tokens[0].Text);
    }

    [TestMethod]
    public void Lexer_TabsAndNewlinesSkipped()
    {
        var tokens = Tokenize("\t\n\r 42\t");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("42", tokens[0].Text);
    }

    // ═══════════════════════════════════════════════════════════════
    // Expression tokenization
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Lexer_SimpleAddition()
    {
        var tokens = Tokenize("1+2");
        Assert.AreEqual(3, tokens.Count);
        Assert.AreEqual("Number", tokens[0].RuleName);
        Assert.AreEqual("1", tokens[0].Text);
        Assert.AreEqual("PLUS", tokens[1].RuleName);
        Assert.AreEqual("Number", tokens[2].RuleName);
        Assert.AreEqual("2", tokens[2].Text);
    }

    [TestMethod]
    public void Lexer_ExpressionWithSpaces()
    {
        var tokens = Tokenize("1 + 2 * 3");
        Assert.AreEqual(5, tokens.Count);
        Assert.AreEqual("1", tokens[0].Text);
        Assert.AreEqual("+", tokens[1].Text);
        Assert.AreEqual("2", tokens[2].Text);
        Assert.AreEqual("*", tokens[3].Text);
        Assert.AreEqual("3", tokens[4].Text);
    }

    [TestMethod]
    public void Lexer_ComplexExpression()
    {
        var tokens = Tokenize("(1 + 2) * 3 - 4 / 5");
        Assert.AreEqual(11, tokens.Count);
        Assert.AreEqual("LPAREN", tokens[0].RuleName);
        Assert.AreEqual("Number", tokens[1].RuleName);
        Assert.AreEqual("PLUS", tokens[2].RuleName);
        Assert.AreEqual("Number", tokens[3].RuleName);
        Assert.AreEqual("RPAREN", tokens[4].RuleName);
        Assert.AreEqual("MULT", tokens[5].RuleName);
        Assert.AreEqual("Number", tokens[6].RuleName);
        Assert.AreEqual("MINUS", tokens[7].RuleName);
        Assert.AreEqual("Number", tokens[8].RuleName);
        Assert.AreEqual("DIV", tokens[9].RuleName);
        Assert.AreEqual("Number", tokens[10].RuleName);
    }

    [TestMethod]
    public void Lexer_NestedParentheses()
    {
        var tokens = Tokenize("((1))");
        Assert.AreEqual(5, tokens.Count);
        Assert.AreEqual("LPAREN", tokens[0].RuleName);
        Assert.AreEqual("LPAREN", tokens[1].RuleName);
        Assert.AreEqual("Number", tokens[2].RuleName);
        Assert.AreEqual("RPAREN", tokens[3].RuleName);
        Assert.AreEqual("RPAREN", tokens[4].RuleName);
    }

    [TestMethod]
    public void Lexer_DecimalInExpression()
    {
        var tokens = Tokenize("3.14 + 2.72");
        Assert.AreEqual(3, tokens.Count);
        Assert.AreEqual("3.14", tokens[0].Text);
        Assert.AreEqual("+", tokens[1].Text);
        Assert.AreEqual("2.72", tokens[2].Text);
    }

    // ═══════════════════════════════════════════════════════════════
    // Token positions
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Lexer_PositionsAreCorrect()
    {
        var tokens = Tokenize("1 + 2");
        // "1 + 2"
        //  0123456
        Assert.AreEqual(0, tokens[0].Span.Position);  // "1" at 0
        Assert.AreEqual(1, tokens[0].Span.Length);

        Assert.AreEqual(2, tokens[1].Span.Position);  // "+" at 2
        Assert.AreEqual(1, tokens[1].Span.Length);

        Assert.AreEqual(4, tokens[2].Span.Position);  // "2" at 4
        Assert.AreEqual(1, tokens[2].Span.Length);
    }

    [TestMethod]
    public void Lexer_ModeName()
    {
        var tokens = Tokenize("42");
        Assert.AreEqual("DEFAULT_MODE", tokens[0].ModeName);
    }

    // ═══════════════════════════════════════════════════════════════
    // Maximal munch
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Lexer_MaximalMunch_MultiDigitNumber()
    {
        // "123" should be ONE token, not three separate "1", "2", "3"
        var tokens = Tokenize("123");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("123", tokens[0].Text);
    }

    [TestMethod]
    public void Lexer_MaximalMunch_DecimalVsInteger()
    {
        // "3.14" should be ONE decimal token, not "3" + error(".") + "14"
        var tokens = Tokenize("3.14");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("3.14", tokens[0].Text);
    }

    // ═══════════════════════════════════════════════════════════════
    // Edge cases
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Lexer_EmptyInput()
    {
        var tokens = Tokenize("");
        Assert.AreEqual(0, tokens.Count);
    }

    [TestMethod]
    public void Lexer_OnlyWhitespace()
    {
        var tokens = Tokenize("   \t\n  ");
        Assert.AreEqual(0, tokens.Count);
    }

    [TestMethod]
    public void Lexer_UnknownCharEmitsErrorToken()
    {
        var tokens = Tokenize("1 @ 2");
        // "1", ERROR("@"), "2"
        Assert.AreEqual(3, tokens.Count);
        Assert.AreEqual("Number", tokens[0].RuleName);
        Assert.AreEqual("ERROR", tokens[1].RuleName);
        Assert.AreEqual("@", tokens[1].Text);
        Assert.AreEqual("Number", tokens[2].RuleName);
    }

    [TestMethod]
    public void Lexer_MultipleErrors()
    {
        var tokens = Tokenize("@#!");
        Assert.AreEqual(3, tokens.Count);
        Assert.IsTrue(tokens.All(t => t.RuleName == "ERROR"));
    }

    // ═══════════════════════════════════════════════════════════════
    // Lexer command scoping — only matched branch (P1 review fix)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void LexerCommand_Skip_AppliesOnlyToMatchedAlternative()
    {
        // Grammar: TOK : 'a' -> skip | 'b' ;
        // Tokenizing 'b' must NOT trigger the skip from the 'a' alternative.
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            TOK : 'a' -> skip | 'b' ;
            """);
        var lexer  = new LexerEngine(grammar);
        var stream = new StringReader("b");
        var tokens = lexer.Tokenize(stream).ToList();

        Assert.AreEqual(1, tokens.Count,
            "Token 'b' must be emitted; skip from the 'a'-branch must not fire.");
        Assert.AreEqual("b", tokens[0].Text);
    }

    [TestMethod]
    public void LexerCommand_Skip_AppliesWhenAlternativeMatches()
    {
        // When 'a' is input, the skip alternative IS matched → no token emitted.
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            TOK : 'a' -> skip | 'b' ;
            """);
        var lexer  = new LexerEngine(grammar);
        var stream = new StringReader("a");
        var tokens = lexer.Tokenize(stream).ToList();

        Assert.AreEqual(0, tokens.Count, "Token 'a' must be skipped.");
    }

    // ═══════════════════════════════════════════════════════════════
    // "more" command buffering (P2 review fix)
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void LexerCommand_More_ConcatenatesWithNextToken()
    {
        // Grammar: PREFIX : 'pre' -> more ;  WORD : 'fix' ;
        // Tokenizing "prefix" → one token "prefix" (not two).
        // Note: [a-z]+ cannot be used here because the bootstrap parser does not
        // support character-class tokenization at runtime; 'fix' covers the same case.
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            PREFIX : 'pre' -> more ;
            WORD   : 'fix' ;
            """);
        var lexer  = new LexerEngine(grammar);
        var stream = new StringReader("prefix");
        var tokens = lexer.Tokenize(stream).ToList();

        Assert.AreEqual(1, tokens.Count,
            "The 'more' command must concatenate 'pre' with the next token.");
        Assert.AreEqual("prefix", tokens[0].Text);
        Assert.AreEqual("WORD",   tokens[0].RuleName);
    }

    [TestMethod]
    public void LexerCommand_More_SpanCoversFullConcatenatedText()
    {
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            PREFIX : 'pre' -> more ;
            WORD   : 'fix' ;
            """);
        var lexer  = new LexerEngine(grammar);
        var stream = new StringReader("prefix");
        var tokens = lexer.Tokenize(stream).ToList();

        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual(0, tokens[0].Span.Position, "Span must start at position 0 (before 'pre').");
        Assert.AreEqual(6, tokens[0].Span.Length,   "Span must cover all 6 characters of 'prefix'.");
    }

    [TestMethod]
    public void Lexer_ChannelHidden_IsNotSkipped()
    {
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            root : ID ID ;
            ID : 'a'..'z'+ ;
            WS : (' ' | '\t')+ -> channel(HIDDEN) ;
            """);
        var lexer = new LexerEngine(grammar);
        var tokens = lexer.Tokenize(new StringReader("a b")).ToList();

        Assert.AreEqual(3, tokens.Count);
        Assert.AreEqual("ID", tokens[0].RuleName);
        Assert.AreEqual("WS", tokens[1].RuleName);
        Assert.AreEqual("HIDDEN", tokens[1].Channel);
        Assert.AreEqual("ID", tokens[2].RuleName);
    }

    [TestMethod]
    public void Lexer_CustomChannel_IsPreserved()
    {
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            channels { COMMENTS }
            root : ID ID ;
            ID : 'a'..'z'+ ;
            COMMENT : '/' '/' ~('\r' | '\n')* -> channel(COMMENTS) ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> channel(HIDDEN) ;
            """);
        grammar = grammar with
        {
            DeclaredChannels = new HashSet<string>(grammar.DeclaredChannels, StringComparer.Ordinal) { "COMMENTS" },
        };
        var lexer = new LexerEngine(grammar);
        var tokens = lexer.Tokenize(new StringReader("foo //comment\n bar")).ToList();

        Assert.IsTrue(tokens.Any(token => token.RuleName == "COMMENT" && token.Channel == "COMMENTS"));
    }

    [TestMethod]
    public void Lexer_Skip_RemovesToken()
    {
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            root : ID ID ;
            ID : 'a'..'z'+ ;
            SKIP_WS : (' ' | '\t')+ -> skip ;
            """);
        var lexer = new LexerEngine(grammar);
        var tokens = lexer.Tokenize(new StringReader("a b")).ToList();

        Assert.AreEqual(2, tokens.Count);
        Assert.IsFalse(tokens.Any(token => token.RuleName == "SKIP_WS"));
    }

    [TestMethod]
    public void Lexer_UnknownChannelFromRule_Throws()
    {
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            ID : 'a'..'z'+ ;
            BAD : '!' -> channel(UNKNOWN) ;
            """);
        var lexer = new LexerEngine(grammar);

        Assert.ThrowsExactly<LexerValidationException>(() => lexer.Tokenize(new StringReader("!")).ToList());
    }

    [TestMethod]
    public void Lexer_TextReaderBuffer_HandlesCrLfAndPeek()
    {
        var buffer = new TextReaderBuffer(new StringReader("a\r\nb"));
        Assert.AreEqual('a', buffer.Peek(0));
        Assert.AreEqual('\r', buffer.Peek(1));
        Assert.AreEqual('\n', buffer.Peek(2));
        Assert.AreEqual('b', buffer.Peek(3));

        buffer.Consume(); // a
        Assert.AreEqual(1, buffer.Position);
        Assert.AreEqual(1, buffer.Line);
        Assert.AreEqual(2, buffer.Column);
        Assert.AreEqual('\r', buffer.Peek(0));

        buffer.Consume(); // \r
        Assert.AreEqual(2, buffer.Position);
        Assert.AreEqual(2, buffer.Line);
        Assert.AreEqual(1, buffer.Column);
        Assert.AreEqual('\n', buffer.Peek(0));

        buffer.Consume(); // \n
        Assert.AreEqual(3, buffer.Position);
        Assert.AreEqual(2, buffer.Line);
        Assert.AreEqual(1, buffer.Column);
        Assert.AreEqual('b', buffer.Peek(0));

        buffer.Consume(); // b
        Assert.AreEqual(4, buffer.Position);
        Assert.AreEqual(2, buffer.Line);
        Assert.AreEqual(2, buffer.Column);
    }

    [TestMethod]
    public void Lexer_TextReaderBuffer_ConsumeCount_ConsumesExactlyCountCharacters()
    {
        var buffer = new TextReaderBuffer(new StringReader("\r\nx"));
        buffer.Consume(2);

        Assert.AreEqual(2, buffer.Position);
        Assert.AreEqual(2, buffer.Line);
        Assert.AreEqual(1, buffer.Column);
        Assert.AreEqual('x', buffer.Peek(0));
    }

    [TestMethod]
    public void Lexer_TextReaderBuffer_PeekLargeOffsetAndLookaheadAfterConsume()
    {
        var input = "ab" + new string('x', 120) + "z";
        var buffer = new TextReaderBuffer(new StringReader(input));

        Assert.AreEqual('a', buffer.Peek(0));
        Assert.AreEqual('b', buffer.Peek(1));
        Assert.AreEqual('x', buffer.Peek(100));

        buffer.Consume(2);

        Assert.AreEqual(2, buffer.Position);
        Assert.AreEqual('x', buffer.Peek(0));
        Assert.AreEqual('z', buffer.Peek(120));
    }

    [TestMethod]
    public void Lexer_TextReaderBuffer_HandlesCrOnlyAndLfOnly()
    {
        var lfBuffer = new TextReaderBuffer(new StringReader("a\nb"));
        lfBuffer.Consume();
        lfBuffer.Consume();
        Assert.AreEqual(2, lfBuffer.Line);
        Assert.AreEqual(1, lfBuffer.Column);

        var crBuffer = new TextReaderBuffer(new StringReader("a\rb"));
        crBuffer.Consume();
        crBuffer.Consume();
        Assert.AreEqual(2, crBuffer.Line);
        Assert.AreEqual(1, crBuffer.Column);
    }

    [TestMethod]
    public void Lexer_TokensWithoutSuperClass_Throws()
    {
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            tokens { INDENT }
            root : ID ;
            ID : 'a' ;
            """);
        grammar = grammar with
        {
            DeclaredTokens = new HashSet<string>(grammar.DeclaredTokens, StringComparer.Ordinal) { "INDENT" },
        };
        var lexer = new LexerEngine(grammar);
        Assert.ThrowsExactly<LexerValidationException>(() => lexer.Tokenize(new StringReader("a")).ToList());
    }

    [TestMethod]
    public void Lexer_SuperClassWithoutExtension_Throws()
    {
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            options { superClass=MyExt; }
            ID : 'a' ;
            """);
        var lexer = new LexerEngine(grammar);
        Assert.ThrowsExactly<LexerValidationException>(() => lexer.Tokenize(new StringReader("a")).ToList());
    }

    [TestMethod]
    public void Lexer_ExtensionUnknownToken_Throws()
    {
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            options { superClass=MyExt; }
            ID : 'a' ;
            """);
        var lexer = new LexerEngine(grammar);
        var options = new LexerEngineOptions { Extensions = [new UnknownTokenExtension()] };

        Assert.ThrowsExactly<LexerValidationException>(() => lexer.Tokenize(new StringReader("a"), options).ToList());
    }

    [TestMethod]
    public void Lexer_ExtensionUnknownChannel_Throws()
    {
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            options { superClass=MyExt; }
            ID : 'a' ;
            """);
        var lexer = new LexerEngine(grammar);
        var options = new LexerEngineOptions { Extensions = [new UnknownChannelExtension()] };

        Assert.ThrowsExactly<LexerValidationException>(() => lexer.Tokenize(new StringReader("a"), options).ToList());
    }

    [TestMethod]
    public void Lexer_ExtensionReturningNull_Throws()
    {
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            options { superClass=MyExt; }
            ID : 'a' ;
            """);
        var lexer = new LexerEngine(grammar);
        var options = new LexerEngineOptions { Extensions = [new NullTryReadExtension()] };

        Assert.ThrowsExactly<LexerValidationException>(() => lexer.Tokenize(new StringReader("a"), options).ToList());
    }

    [TestMethod]
    public void Lexer_ExtensionInvalidSpan_Throws()
    {
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            options { superClass=MyExt; }
            tokens { INDENT }
            ID : 'a' ;
            """);
        grammar = grammar with
        {
            DeclaredTokens = new HashSet<string>(grammar.DeclaredTokens, StringComparer.Ordinal) { "INDENT" },
        };
        var lexer = new LexerEngine(grammar);
        var options = new LexerEngineOptions { Extensions = [new InvalidSpanExtension()] };

        Assert.ThrowsExactly<LexerValidationException>(() => lexer.Tokenize(new StringReader("a"), options).ToList());
    }

    [TestMethod]
    public void Lexer_ExtensionTryReadWithoutProgress_Throws()
    {
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            options { superClass=MyExt; }
            tokens { INDENT }
            ID : 'a' ;
            """);
        grammar = grammar with
        {
            DeclaredTokens = new HashSet<string>(grammar.DeclaredTokens, StringComparer.Ordinal) { "INDENT" },
        };
        var lexer = new LexerEngine(grammar);
        var options = new LexerEngineOptions { Extensions = [new NoProgressExtension()] };

        Assert.ThrowsExactly<LexerValidationException>(() => lexer.Tokenize(new StringReader("a"), options).ToList());
    }

    [TestMethod]
    public void Lexer_ExtensionTryReadWithProgress_IsAllowed()
    {
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            options { superClass=MyExt; }
            tokens { INDENT }
            ID : 'a' ;
            """);
        grammar = grammar with
        {
            DeclaredTokens = new HashSet<string>(grammar.DeclaredTokens, StringComparer.Ordinal) { "INDENT" },
        };
        var lexer = new LexerEngine(grammar);
        var options = new LexerEngineOptions { Extensions = [new ConsumingExtension()] };
        var tokens = lexer.Tokenize(new StringReader("a"), options).ToList();

        Assert.IsTrue(tokens.Any(token => token.RuleName == "INDENT"));
    }

    [TestMethod]
    public void Lexer_Integration_CommentsAndHiddenAreKeptButParserIgnoresThem()
    {
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar Test;

            channels { COMMENTS }
            tokens { INDENT }
            options { superClass=TestLexerExtension; }

            root : ID ID ;

            ID : ('a'..'z' | 'A'..'Z')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> channel(HIDDEN);
            COMMENT : '/' '/' ~('\r' | '\n')* -> channel(COMMENTS);
            """);
        grammar = grammar with
        {
            DeclaredChannels = new HashSet<string>(grammar.DeclaredChannels, StringComparer.Ordinal) { "COMMENTS" },
            DeclaredTokens = new HashSet<string>(grammar.DeclaredTokens, StringComparer.Ordinal) { "INDENT" },
        };

        var lexer = new LexerEngine(grammar);
        var options = new LexerEngineOptions { Extensions = [new NoopExtension()] };
        var tokenized = lexer.Tokenize(new StringReader("foo // comment\n bar"), options).ToList();

        Assert.IsTrue(tokenized.Any(token => token.RuleName == "COMMENT" && token.Channel == "COMMENTS"));
        Assert.IsTrue(tokenized.Any(token => token.RuleName == "WS" && token.Channel == "HIDDEN"));

        var parser = new ParserEngine(grammar);
        var result = parser.Parse(tokenized);
        Assert.IsNotInstanceOfType<ErrorNode>(result);
    }

    [TestMethod]
    public void Lexer_Integration_CrLfWhitespace_IsKeptAndParserStaysSynchronized()
    {
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            root : ID ID ;
            ID : 'a'..'z'+ ;
            WS : (' ' | '\r' | '\n')+ -> channel(HIDDEN);
            """);
        var lexer = new LexerEngine(grammar);
        var tokens = lexer.Tokenize(new StringReader("a\r\nb")).ToList();

        Assert.AreEqual(3, tokens.Count);
        Assert.AreEqual("ID", tokens[0].RuleName);
        Assert.AreEqual(0, tokens[0].Span.Position);

        Assert.AreEqual("WS", tokens[1].RuleName);
        Assert.AreEqual("\r\n", tokens[1].Text);
        Assert.AreEqual(1, tokens[1].Span.Position);
        Assert.AreEqual(2, tokens[1].Span.Length);
        Assert.AreEqual("HIDDEN", tokens[1].Channel);

        Assert.AreEqual("ID", tokens[2].RuleName);
        Assert.AreEqual("b", tokens[2].Text);
        Assert.AreEqual(3, tokens[2].Span.Position);
        Assert.AreEqual(2, tokens[2].Span.Line);
        Assert.AreEqual(1, tokens[2].Span.Column);

        var parser = new ParserEngine(grammar);
        var result = parser.Parse(tokens);
        Assert.IsNotInstanceOfType<ErrorNode>(result);
    }

    [TestMethod]
    public void Lexer_ExtensionOnAfterToken_EmitsWithoutConsuming_IsAllowed()
    {
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            options { superClass=MyExt; }
            tokens { INDENT }
            ID : 'a' ;
            """);
        grammar = grammar with
        {
            DeclaredTokens = new HashSet<string>(grammar.DeclaredTokens, StringComparer.Ordinal) { "INDENT" },
        };

        var lexer = new LexerEngine(grammar);
        var options = new LexerEngineOptions { Extensions = [new AfterTokenEmitterExtension()] };
        var tokens = lexer.Tokenize(new StringReader("a"), options).ToList();

        Assert.IsTrue(tokens.Any(token => token.RuleName == "INDENT"));
    }

    [TestMethod]
    public void Lexer_ExtensionOnEndOfInput_EmitsWithoutConsuming_IsAllowed()
    {
        var grammar = Antlr4GrammarConverter.Parse("""
            grammar G;
            options { superClass=MyExt; }
            tokens { INDENT }
            ID : 'a' ;
            """);
        grammar = grammar with
        {
            DeclaredTokens = new HashSet<string>(grammar.DeclaredTokens, StringComparer.Ordinal) { "INDENT" },
        };

        var lexer = new LexerEngine(grammar);
        var options = new LexerEngineOptions { Extensions = [new EndOfInputEmitterExtension()] };
        var tokens = lexer.Tokenize(new StringReader("a"), options).ToList();

        Assert.IsTrue(tokens.Any(token => token.RuleName == "INDENT"));
    }

    private sealed class UnknownTokenExtension : ILexerExtension
    {
        public IReadOnlyList<Token> TryReadTokens(LexerExtensionContext context) =>
        [
            new Token(new SourceSpan(context.Position, 1, context.Line, context.Column), "UNKNOWN", "DEFAULT_MODE", "DEFAULT_CHANNEL", "x"),
        ];

        public IReadOnlyList<Token> OnAfterToken(Token token, LexerExtensionContext context) => [];

        public IReadOnlyList<Token> OnEndOfInput(LexerExtensionContext context) => [];
    }

    private sealed class UnknownChannelExtension : ILexerExtension
    {
        public IReadOnlyList<Token> TryReadTokens(LexerExtensionContext context) =>
        [
            new Token(new SourceSpan(context.Position, 1, context.Line, context.Column), "ID", "DEFAULT_MODE", "UNKNOWN_CHANNEL", "x"),
        ];

        public IReadOnlyList<Token> OnAfterToken(Token token, LexerExtensionContext context) => [];

        public IReadOnlyList<Token> OnEndOfInput(LexerExtensionContext context) => [];
    }

    private sealed class NullTryReadExtension : ILexerExtension
    {
        public IReadOnlyList<Token> TryReadTokens(LexerExtensionContext context) => null!;

        public IReadOnlyList<Token> OnAfterToken(Token token, LexerExtensionContext context) => [];

        public IReadOnlyList<Token> OnEndOfInput(LexerExtensionContext context) => [];
    }

    private sealed class InvalidSpanExtension : ILexerExtension
    {
        public IReadOnlyList<Token> TryReadTokens(LexerExtensionContext context) =>
        [
            new Token(new SourceSpan(-1, -1, context.Line, context.Column), "INDENT", "DEFAULT_MODE", "DEFAULT_CHANNEL", ""),
        ];

        public IReadOnlyList<Token> OnAfterToken(Token token, LexerExtensionContext context) => [];

        public IReadOnlyList<Token> OnEndOfInput(LexerExtensionContext context) => [];
    }

    private sealed class NoProgressExtension : ILexerExtension
    {
        public IReadOnlyList<Token> TryReadTokens(LexerExtensionContext context) =>
        [
            new Token(new SourceSpan(context.Position, 0, context.Line, context.Column), "INDENT", "DEFAULT_MODE", "DEFAULT_CHANNEL", ""),
        ];

        public IReadOnlyList<Token> OnAfterToken(Token token, LexerExtensionContext context) => [];

        public IReadOnlyList<Token> OnEndOfInput(LexerExtensionContext context) => [];
    }

    private sealed class ConsumingExtension : ILexerExtension
    {
        public IReadOnlyList<Token> TryReadTokens(LexerExtensionContext context)
        {
            if (context.Position == 0)
            {
                return
                [
                    new Token(new SourceSpan(context.Position, 1, context.Line, context.Column), "ID", "DEFAULT_MODE", "DEFAULT_CHANNEL", "a"),
                ];
            }

            return [];
        }

        public IReadOnlyList<Token> OnAfterToken(Token token, LexerExtensionContext context) => [];

        public IReadOnlyList<Token> OnEndOfInput(LexerExtensionContext context) =>
        [
            new Token(new SourceSpan(context.Position, 0, context.Line, context.Column), "INDENT", "DEFAULT_MODE", "DEFAULT_CHANNEL", ""),
        ];
    }

    private sealed class NoopExtension : ILexerExtension
    {
        public IReadOnlyList<Token> TryReadTokens(LexerExtensionContext context) => [];

        public IReadOnlyList<Token> OnAfterToken(Token token, LexerExtensionContext context) => [];

        public IReadOnlyList<Token> OnEndOfInput(LexerExtensionContext context) => [];
    }

    private sealed class AfterTokenEmitterExtension : ILexerExtension
    {
        public IReadOnlyList<Token> TryReadTokens(LexerExtensionContext context) => [];

        public IReadOnlyList<Token> OnAfterToken(Token token, LexerExtensionContext context) =>
        [
            new Token(new SourceSpan(token.Span.Position + token.Span.Length, 0, context.Line, context.Column), "INDENT", "DEFAULT_MODE", "DEFAULT_CHANNEL", ""),
        ];

        public IReadOnlyList<Token> OnEndOfInput(LexerExtensionContext context) => [];
    }

    private sealed class EndOfInputEmitterExtension : ILexerExtension
    {
        public IReadOnlyList<Token> TryReadTokens(LexerExtensionContext context) => [];

        public IReadOnlyList<Token> OnAfterToken(Token token, LexerExtensionContext context) => [];

        public IReadOnlyList<Token> OnEndOfInput(LexerExtensionContext context) =>
        [
            new Token(new SourceSpan(context.Position, 0, context.Line, context.Column), "INDENT", "DEFAULT_MODE", "DEFAULT_CHANNEL", ""),
        ];
    }

    // ═══════════════════════════════════════════════════════════════
    // Lexer-command scoping — regression tests for command leakage
    //
    // Invariant: commands must remain scoped to successful matches only.
    // A failed sequence or a failed quantifier iteration must not
    // contribute commands to the enclosing match.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Builds a lexer-only <see cref="ParserDefinition"/> from the supplied rules,
    /// running the full resolution pass so that <c>AllRules</c> and <c>Kind</c> are set.
    /// </summary>
    private static ParserDefinition BuildLexerDef(params Rule[] rules)
    {
        var mode = new LexerMode("DEFAULT_MODE", rules);
        var def = new ParserDefinition(
            "Test", GrammarType.Lexer, null, [], [], [mode], [], null);
        return RuleResolver.Resolve(def, null);
    }

    /// <summary>
    /// Wraps <paramref name="content"/> in a single-alternative <see cref="Alternation"/>
    /// and returns the resulting lexer <see cref="Rule"/>.
    /// </summary>
    private static Rule LexerRule(string name, int order, RuleContent content) =>
        new Rule(name, order, false,
            new Alternation([new Alternative(0, Associativity.Left, content)]));

    private static List<Token> TokenizeWith(ParserDefinition def, string input)
    {
        var lexer = new LexerEngine(def);
        return lexer.Tokenize(new StringReader(input)).ToList();
    }

    // ── A: Quantifier — failed iteration must not leak commands ──────────────

    [TestMethod]
    public void Lexer_QuantifierFailedIteration_DoesNotLeakCommands()
    {
        // Rule:  TOK = (channel_cmd 'x')* 'a'
        //
        // 'channel_cmd' is a LexerCommand(Channel, HIDDEN) placed as the FIRST
        // item in the iteration sequence, before the character match.
        // On input "a" the quantifier attempts one iteration:
        //   1. channel_cmd always succeeds and appends HIDDEN to the command buffer.
        //   2. LiteralMatch('x') then fails against 'a'.
        // Without isolation: the HIDDEN command leaks into the outer buffer,
        // causing the produced token to land on the HIDDEN channel.
        // With isolation: the failed iteration's local buffer is discarded.

        var hiddenCmd = new LexerCommand(LexerCommandType.Channel, "HIDDEN");
        var iterSeq = new Sequence([hiddenCmd, new LiteralMatch("x")]);
        var quant = new Quantifier(iterSeq, 0, null);      // (cmd 'x')*
        var outerSeq = new Sequence([quant, new LiteralMatch("a")]);
        var def = BuildLexerDef(LexerRule("TOK", 0, outerSeq));

        var tokens = TokenizeWith(def, "a");

        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("TOK", tokens[0].RuleName);
        Assert.AreEqual("DEFAULT_CHANNEL", tokens[0].Channel,
            "Channel command from a failed quantifier iteration must not leak.");
    }

    // ── B: Sequence — failed sequence must not leak commands ─────────────────

    [TestMethod]
    public void Lexer_FailedSequenceItem_DoesNotLeakCommandsFromEarlierItems()
    {
        // Rule:  TOK = (skip_cmd 'x' 'y') | 'a'
        //
        // Alternative 1 contains Sequence([LexerCommand(Skip), Literal('x'), Literal('y')]).
        // On input "a": skip_cmd always succeeds (appends Skip), then Literal('x') fails.
        // Without isolation: the Skip command is already in the buffer when the sequence
        // fails, so the fallback token from Alternative 2 gets Skip applied and is
        // discarded — leaving an empty token list.
        // With isolation: the failed sequence's local buffer is discarded before
        // Alternative 2 is tried.

        var skipCmd = new LexerCommand(LexerCommandType.Skip, null);
        var failSeq = new Sequence([skipCmd, new LiteralMatch("x"), new LiteralMatch("y")]);
        var alt1 = new Alternative(0, Associativity.Left, failSeq);
        var alt2 = new Alternative(1, Associativity.Left, new LiteralMatch("a"));
        var rule = new Rule("TOK", 0, false, new Alternation([alt1, alt2]));
        var def = BuildLexerDef(rule);

        var tokens = TokenizeWith(def, "a");

        Assert.AreEqual(1, tokens.Count,
            "The skip command from the failed sequence must not be applied to the fallback match.");
        Assert.AreEqual("TOK", tokens[0].RuleName);
        Assert.AreEqual("a", tokens[0].Text);
    }

    // ── C: Sequence — successful sequence still propagates commands ───────────

    [TestMethod]
    public void Lexer_SuccessfulSequence_PropagatesCommandsCorrectly()
    {
        // Rule:  TOK = 'a' channel_cmd
        //
        // On input "a": Literal('a') succeeds, then the command is appended.
        // The sequence succeeds — commands must be flushed to the caller.

        var hiddenCmd = new LexerCommand(LexerCommandType.Channel, "HIDDEN");
        var seq = new Sequence([new LiteralMatch("a"), hiddenCmd]);
        var def = BuildLexerDef(LexerRule("TOK", 0, seq));

        var tokens = TokenizeWith(def, "a");

        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("TOK", tokens[0].RuleName);
        Assert.AreEqual("HIDDEN", tokens[0].Channel,
            "Command from a successful sequence must be applied.");
    }

    // ── D: Quantifier — successful iterations still propagate commands ────────

    [TestMethod]
    public void Lexer_SuccessfulQuantifierIterations_PropagateCommandsCorrectly()
    {
        // Rule:  TOK = ('x' channel_cmd)+ 'a'
        //
        // channel_cmd = LexerCommand(Channel, HIDDEN).
        // On input "xa": iteration 1 matches 'x' and records the HIDDEN command.
        // The quantifier (min=1) then succeeds.
        // The outer sequence then matches 'a'.
        // The HIDDEN command from the successful iteration must survive to the token.

        var hiddenCmd = new LexerCommand(LexerCommandType.Channel, "HIDDEN");
        var iterSeq = new Sequence([new LiteralMatch("x"), hiddenCmd]);
        var quant = new Quantifier(iterSeq, 1, null);      // (x cmd)+
        var outerSeq = new Sequence([quant, new LiteralMatch("a")]);
        var def = BuildLexerDef(LexerRule("TOK", 0, outerSeq));

        var tokens = TokenizeWith(def, "xa");

        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("TOK", tokens[0].RuleName);
        Assert.AreEqual("HIDDEN", tokens[0].Channel,
            "Command from a successful quantifier iteration must be applied.");
    }
}
