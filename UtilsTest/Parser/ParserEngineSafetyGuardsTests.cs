using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Resolution;
using Utils.Parser.Runtime;

namespace UtilsTest.Parser;

/// <summary>
/// Verifies runtime safeguards that prevent non-terminating parser execution.
/// </summary>
[TestClass]
public class ParserEngineSafetyGuardsTests
{
    /// <summary>
    /// Ensures nested optional-star quantifiers terminate even when inner content can match empty.
    /// </summary>
    [TestMethod]
    public void Quantifier_EmptyMatchLoop_TerminatesAndReportsDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var tree = ParseWithDiagnostics("""
            grammar G;
            start : (A?)* ;
            A : 'a' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, string.Empty, diagnostics);

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.NonProgressiveQuantifierStopped.Code));
    }

    /// <summary>
    /// Ensures ambiguous repeated alternatives are parsed without entering repeated parser states forever.
    /// </summary>
    [TestMethod]
    public void AmbiguousRepeatedAlternatives_Terminates()
    {
        var diagnostics = new DiagnosticBag();
        var tree = ParseWithDiagnostics("""
            grammar G;
            start : ('a' | 'a')* ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, "a a a", diagnostics);

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.AmbiguousAlternativesPruned.Code));
    }

    /// <summary>
    /// Ensures direct left recursion remains bounded and can parse simple infix expressions.
    /// </summary>
    [TestMethod]
    public void DirectLeftRecursion_TerminatesAndParsesExpression()
    {
        var diagnostics = new DiagnosticBag();
        var tree = ParseWithDiagnostics("""
            grammar G;
            start : expr ;
            expr : expr '+' expr
                 | INT
                 ;
            INT : ('0'..'9')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, "1 + 2 + 3", diagnostics);

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.AreEqual(3, CountTokens(tree, "INT"));
    }

    /// <summary>
    /// Ensures indirect recursion cycles are still rejected with a clear validation diagnostic.
    /// </summary>
    [TestMethod]
    public void IndirectCycle_IsRejectedCleanly()
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

    private static ParseNode ParseWithDiagnostics(string grammar, string input, DiagnosticBag diagnostics)
    {
        var definition = Antlr4GrammarConverter.Parse(grammar, diagnostics);
        var lexer = new LexerEngine(definition);
        var tokens = lexer.Tokenize(new StringReader(input), diagnostics: diagnostics).ToList();
        var parser = new ParserEngine(definition);
        return parser.Parse(tokens, diagnostics: diagnostics);
    }

    private static int CountTokens(ParseNode node, string ruleName)
    {
        if (node is LexerNode lexerNode)
        {
            return string.Equals(lexerNode.Token.RuleName, ruleName, StringComparison.Ordinal) ? 1 : 0;
        }

        if (node is not ParserNode parserNode)
        {
            return 0;
        }

        return parserNode.Children.Sum(child => CountTokens(child, ruleName));
    }
}
