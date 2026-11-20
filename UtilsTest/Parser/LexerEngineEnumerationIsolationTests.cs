using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Engine-reuse audit: the mode stack and the pending <c>more</c> text of a <see cref="LexerEngine"/> belong to each
/// <see cref="LexerEngine.Tokenize"/> enumeration. Before the fix they belonged to the engine, so a second enumeration
/// started while a first one was suspended reset the first one's modes (<c>"ab"</c> inside <c>"abab"</c> produced
/// <c>ERROR:b</c>).
/// </summary>
[TestClass]
public class LexerEngineEnumerationIsolationTests
{
    /// <summary>A grammar with a pushed string mode and a <c>more</c> escape inside it.</summary>
    private const string Grammar = """
        grammar Isolation;
        start : (ID | str)* ;
        str : OPEN STRTEXT* CLOSE ;
        ID : ('a'..'z')+ ;
        WS : ' ' -> skip ;
        OPEN : '"' -> pushMode(STRING) ;
        mode STRING;
        CLOSE : '"' -> popMode ;
        ESCAPE : '\\' -> more ;
        STRTEXT : (~('"' | '\\'))+ ;
        """;

    /// <summary>Inputs that leave the lexer in the string mode, or with pending <c>more</c> text, between two tokens.</summary>
    private static readonly string[] Inputs =
    [
        "a \"b c\" d",
        "\"x\\\"y\" z",
        "\"open",
        "p q r",
        "\"\\\\\" \"t\"",
    ];

    /// <summary>
    /// Every pair of inputs, enumerated alternately one token at a time on one engine, produces for each input exactly
    /// the tokens of a sequential enumeration.
    /// </summary>
    [TestMethod]
    public void InterleavedEnumerations_OnOneEngine_AreIndependent()
    {
        CompiledGrammar grammar = Antlr4GrammarConverter.Compile(Grammar);
        var lexer = new LexerEngine(grammar.Definition);
        Dictionary<string, string> expected = Inputs.ToDictionary(input => input, input => Render(lexer.Tokenize(new StringReader(input))));

        foreach (string first in Inputs)
        {
            foreach (string second in Inputs)
            {
                using IEnumerator<Token> left = lexer.Tokenize(new StringReader(first)).GetEnumerator();
                using IEnumerator<Token> right = lexer.Tokenize(new StringReader(second)).GetEnumerator();
                var leftTokens = new List<Token>();
                var rightTokens = new List<Token>();
                bool leftMore = true;
                bool rightMore = true;
                while (leftMore || rightMore)
                {
                    if (leftMore && (leftMore = left.MoveNext())) leftTokens.Add(left.Current);
                    if (rightMore && (rightMore = right.MoveNext())) rightTokens.Add(right.Current);
                }

                Assert.AreEqual(expected[first], Render(leftTokens), $"{first} interleaved with {second}");
                Assert.AreEqual(expected[second], Render(rightTokens), $"{second} interleaved with {first}");
            }
        }
    }

    /// <summary>A nested enumeration that runs to completion while another is suspended inside the string mode leaves it intact.</summary>
    [TestMethod]
    public void NestedEnumeration_DoesNotResetASuspendedOne()
    {
        CompiledGrammar grammar = Antlr4GrammarConverter.Compile(Grammar);
        var lexer = new LexerEngine(grammar.Definition);
        const string Outer = "a \"b c\" d";
        string expected = Render(lexer.Tokenize(new StringReader(Outer)));

        var tokens = new List<Token>();
        foreach (Token token in lexer.Tokenize(new StringReader(Outer)))
        {
            tokens.Add(token);
            if (token.RuleName == "OPEN")
            {
                Assert.AreEqual("ID:x DEFAULT_MODE|ID:y DEFAULT_MODE", Render(lexer.Tokenize(new StringReader("x y"))));
            }
        }

        Assert.AreEqual(expected, Render(tokens));
        StringAssert.Contains(expected, "STRTEXT:b c STRING", "the string body is lexed in the pushed mode");
    }

    /// <summary>An enumeration abandoned in the string mode does not change where the next enumeration starts.</summary>
    [TestMethod]
    public void AbandonedEnumeration_DoesNotAffectTheNextOne()
    {
        CompiledGrammar grammar = Antlr4GrammarConverter.Compile(Grammar);
        var lexer = new LexerEngine(grammar.Definition);
        using (IEnumerator<Token> abandoned = lexer.Tokenize(new StringReader("\"pending text")).GetEnumerator())
        {
            Assert.IsTrue(abandoned.MoveNext());
            Assert.AreEqual("OPEN", abandoned.Current.RuleName);
        }

        Assert.AreEqual("ID:a DEFAULT_MODE|ID:b DEFAULT_MODE", Render(lexer.Tokenize(new StringReader("a b"))));
    }

    /// <summary>Renders tokens with their rule, text and mode.</summary>
    /// <param name="tokens">The tokens.</param>
    /// <returns>The rendering.</returns>
    private static string Render(IEnumerable<Token> tokens)
        => string.Join("|", tokens.Select(token => $"{token.RuleName}:{token.Text} {token.ModeName}"));
}
