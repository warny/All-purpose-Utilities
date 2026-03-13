using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class LexerEngineTests
{
    private static List<Token> Tokenize(string input)
    {
        var definition = ExpGrammar.Build();
        var lexer = new LexerEngine(definition);
        var stream = new StringCharStream(input);
        return lexer.Tokenize(stream).ToList();
    }

    // ═══════════════════════════════════════════════════════════════
    // StringCharStream tests
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void StringCharStream_EmptyString()
    {
        var stream = new StringCharStream("");
        Assert.IsTrue(stream.IsEnd);
        Assert.AreEqual(0, stream.Position);
    }

    [TestMethod]
    public void StringCharStream_PeekAndConsume()
    {
        var stream = new StringCharStream("abc");
        Assert.AreEqual('a', stream.Peek());
        Assert.AreEqual('b', stream.Peek(1));
        Assert.AreEqual('c', stream.Peek(2));
        Assert.AreEqual('\0', stream.Peek(3));

        stream.Consume();
        Assert.AreEqual('b', stream.Peek());
        Assert.AreEqual(1, stream.Position);
    }

    [TestMethod]
    public void StringCharStream_SaveRestore()
    {
        var stream = new StringCharStream("abc");
        stream.Consume(2);
        Assert.AreEqual('c', stream.Peek());

        var saved = stream.SavePosition();
        stream.Consume();
        Assert.IsTrue(stream.IsEnd);

        stream.RestorePosition(saved);
        Assert.AreEqual('c', stream.Peek());
        Assert.IsFalse(stream.IsEnd);
    }

    // ═══════════════════════════════════════════════════════════════
    // SourceSpan tests
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void SourceSpan_ToLineColumn_FirstChar()
    {
        var span = new SourceSpan(0, 1);
        var (line, col) = span.ToLineColumn("hello");
        Assert.AreEqual(1, line);
        Assert.AreEqual(1, col);
    }

    [TestMethod]
    public void SourceSpan_ToLineColumn_SecondLine()
    {
        var span = new SourceSpan(6, 1);
        var (line, col) = span.ToLineColumn("hello\nworld");
        Assert.AreEqual(2, line);
        Assert.AreEqual(1, col);
    }

    [TestMethod]
    public void SourceSpan_ToLineColumn_ThirdColumn()
    {
        var span = new SourceSpan(2, 1);
        var (line, col) = span.ToLineColumn("hello");
        Assert.AreEqual(1, line);
        Assert.AreEqual(3, col);
    }

    // ═══════════════════════════════════════════════════════════════
    // Simple number tokenization
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    public void Lexer_SingleInteger()
    {
        var tokens = Tokenize("42");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("Number", tokens[0].RuleName);
        Assert.AreEqual("42", tokens[0].Text);
        Assert.AreEqual(0, tokens[0].Span.Position);
        Assert.AreEqual(2, tokens[0].Span.Length);
    }

    [TestMethod]
    public void Lexer_SingleDigit()
    {
        var tokens = Tokenize("7");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("Number", tokens[0].RuleName);
        Assert.AreEqual("7", tokens[0].Text);
    }

    [TestMethod]
    public void Lexer_DecimalNumber()
    {
        var tokens = Tokenize("3.14");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("Number", tokens[0].RuleName);
        Assert.AreEqual("3.14", tokens[0].Text);
    }

    [TestMethod]
    public void Lexer_LargeInteger()
    {
        var tokens = Tokenize("123456789");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("123456789", tokens[0].Text);
    }

    [TestMethod]
    public void Lexer_DecimalWithMultipleDigits()
    {
        var tokens = Tokenize("100.005");
        Assert.AreEqual(1, tokens.Count);
        Assert.AreEqual("100.005", tokens[0].Text);
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
}
