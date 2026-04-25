using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Resolution;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

[TestClass]
public class ParserEngineLeftRecursionTests
{
    [TestMethod]
    public void DirectLeftRecursion_ParsesSimpleAddition()
    {
        var (tree, _) = Parse("""
            grammar G;
            start : expr ;
            expr : expr '+' expr | INT ;
            INT : ('0'..'9')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, "1 + 2");

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.AreEqual(2, FindTokens(tree, "INT").Count);
        Assert.AreEqual(1, FindTokens(tree, "+").Count);
    }

    [TestMethod]
    public void LeftRecursion_Precedence_MultiplyBeforeAdd()
    {
        var (tree, _) = Parse("""
            grammar G;
            start : expr ;
            expr : expr '*' expr | expr '+' expr | INT ;
            INT : ('0'..'9')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, "1 + 2 * 3");

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        var plusDepth = FindTokenDepths(tree, "+").Single();
        var starDepth = FindTokenDepths(tree, "*").Single();
        Assert.IsTrue(starDepth > plusDepth, "Expected '*' to be nested under '+'.");
    }

    [TestMethod]
    public void LeftRecursion_Precedence_AddAfterMultiply()
    {
        var (tree, _) = Parse("""
            grammar G;
            start : expr ;
            expr : expr '*' expr | expr '+' expr | INT ;
            INT : ('0'..'9')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, "1 * 2 + 3");

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        var plusDepth = FindTokenDepths(tree, "+").Single();
        var starDepth = FindTokenDepths(tree, "*").Single();
        Assert.IsTrue(plusDepth < starDepth, "Expected '*' to be nested under '+'.");
    }

    [TestMethod]
    public void LeftRecursion_DefaultLeftAssociativity_IsApplied()
    {
        var (tree, _) = Parse("""
            grammar G;
            start : expr ;
            expr : expr '+' expr | INT ;
            INT : ('0'..'9')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, "1 + 2 + 3");

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        var plusDepths = FindTokenDepths(tree, "+").OrderBy(x => x).ToList();
        Assert.AreEqual(2, plusDepths.Count);
        Assert.IsTrue(plusDepths[1] > plusDepths[0], "Expected left-associative tree nesting.");
    }

    [TestMethod]
    public void LeftRecursion_WithoutBaseAlternative_Throws()
    {
        var diagnostics = new DiagnosticBag();
        Assert.ThrowsException<GrammarValidationException>(() => Antlr4GrammarConverter.Parse("""
            grammar G;
            start : expr ;
            expr : expr '+' expr ;
            INT : ('0'..'9')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, diagnostics));
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.LeftRecursiveRuleWithoutBaseAlternative.Code));
    }

    [TestMethod]
    public void IndirectLeftRecursion_ReportsClearDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        Assert.ThrowsException<GrammarValidationException>(() => Antlr4GrammarConverter.Parse("""
            grammar G;
            start : a ;
            a : b ;
            b : a '+' INT | INT ;
            INT : ('0'..'9')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, diagnostics));
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.IndirectLeftRecursionNotSupported.Code));
    }

    [TestMethod]
    public void ParallelAlternativeSelection_FallbacksWhenLongPrefixFails()
    {
        var (tree, _) = Parse("""
            grammar G;
            start : stmt ;
            stmt : ID '(' ')' | ID ;
            ID : ('a'..'z')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, "foo");
        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.AreEqual(1, FindTokens(tree, "ID").Count);

        var (callTree, _) = Parse("""
            grammar G;
            start : stmt ;
            stmt : ID '(' ')' | ID ;
            ID : ('a'..'z')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, "foo()");
        Assert.IsNotInstanceOfType<ErrorNode>(callTree);
        Assert.AreEqual(1, FindTokens(callTree, "(").Count);
        Assert.AreEqual(1, FindTokens(callTree, ")").Count);
    }

    [TestMethod]
    public void StaticDuplicateAlternative_IsRemoved()
    {
        var diagnostics = new DiagnosticBag();
        var definition = Antlr4GrammarConverter.Parse("""
            grammar G;
            start : a ;
            a : b | b ;
            b : 'x' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, diagnostics);

        var rule = definition.AllRules["a"];
        Assert.AreEqual(1, rule.Content.Alternatives.Count);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.StaticDuplicateAlternativeRemoved.Code));
    }

    [TestMethod]
    public void DistinctLabels_AreNotCompacted()
    {
        var definition = Antlr4GrammarConverter.Parse("""
            grammar G;
            start : a ;
            a : b #One | b #Two ;
            b : 'x' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """);

        var rule = definition.AllRules["a"];
        Assert.AreEqual(2, rule.Content.Alternatives.Count);
    }

    [TestMethod]
    public void QuantifierGreedy_ConsumesAllMatches()
    {
        var (tree, _) = Parse("""
            grammar G;
            start : a ;
            a : b* ;
            b : 'x' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, "x x");

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.AreEqual(2, FindTokens(tree, "x").Count);
    }

    [TestMethod]
    public void Memoization_EmitsHitAndMissDiagnostics()
    {
        var diagnostics = new DiagnosticBag();
        var definition = Antlr4GrammarConverter.Parse("""
            grammar G;
            start : expr ;
            expr : expr '+' expr | INT ;
            INT : ('0'..'9')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, diagnostics);

        var lexer = new LexerEngine(definition);
        var tokens = lexer.Tokenize(new StringCharStream("1+2+3"), diagnostics).ToList();
        var parser = new ParserEngine(definition);
        var result = parser.Parse(tokens, diagnostics: diagnostics);

        Assert.IsNotInstanceOfType<ErrorNode>(result);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.ParseMemoHit.Code));
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.ParseMemoMiss.Code));
    }

    private static (ParseNode tree, ParserDefinition definition) Parse(string grammar, string input)
    {
        var definition = Antlr4GrammarConverter.Parse(grammar);
        var lexer = new LexerEngine(definition);
        var tokens = lexer.Tokenize(new StringCharStream(input)).ToList();
        var parser = new ParserEngine(definition);
        return (parser.Parse(tokens), definition);
    }

    private static List<LexerNode> FindTokens(ParseNode node, string tokenTextOrRule)
    {
        var result = new List<LexerNode>();
        Visit(node, 0, (_, current) =>
        {
            if (current is LexerNode lexer &&
                (string.Equals(lexer.Token.Text, tokenTextOrRule, StringComparison.Ordinal) ||
                 string.Equals(lexer.Token.RuleName, tokenTextOrRule, StringComparison.Ordinal)))
            {
                result.Add(lexer);
            }
        });
        return result;
    }

    private static List<int> FindTokenDepths(ParseNode node, string tokenText)
    {
        var result = new List<int>();
        Visit(node, 0, (depth, current) =>
        {
            if (current is LexerNode lexer &&
                string.Equals(lexer.Token.Text, tokenText, StringComparison.Ordinal))
            {
                result.Add(depth);
            }
        });
        return result;
    }

    private static void Visit(ParseNode node, int depth, Action<int, ParseNode> visitor)
    {
        visitor(depth, node);
        if (node is not ParserNode parserNode)
        {
            return;
        }

        foreach (var child in parserNode.Children)
        {
            Visit(child, depth + 1, visitor);
        }
    }
}
