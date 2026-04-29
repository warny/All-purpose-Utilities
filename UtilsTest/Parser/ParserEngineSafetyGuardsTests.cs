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
    /// Ensures nested star inside plus also stops on non-progressive matches.
    /// </summary>
    [TestMethod]
    public void Quantifier_NestedStarInsidePlus_FailsCleanlyAndReportsDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var tree = ParseWithDiagnostics("""
            grammar G;
            start : (B*)+ ;
            B : 'b' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, string.Empty, diagnostics);

        Assert.IsInstanceOfType<ErrorNode>(tree);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.NonProgressiveQuantifierStopped.Code));
    }

    /// <summary>
    /// Ensures plus quantifiers with optional inner content do not loop forever and fail when no consuming match exists.
    /// </summary>
    [TestMethod]
    public void Quantifier_EmptyMatchPlus_FailsCleanlyAndReportsDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var tree = ParseWithDiagnostics("""
            grammar G;
            start : (A?)+ ;
            A : 'a' ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, string.Empty, diagnostics);

        Assert.IsInstanceOfType<ErrorNode>(tree);
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
        Assert.AreEqual(3, CountTokens(tree, "a"));
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.AmbiguousAlternativesPruned.Code));
    }

    /// <summary>
    /// Ensures the parser emits both a cycle diagnostic and a non-progressive diagnostic
    /// for left-recursive rules whose extension produces no token progress.
    /// Both codes are expected because a non-progressive extension is simultaneously
    /// a cycle state and a termination trigger.
    /// </summary>
    [TestMethod]
    public void RecursiveNonProgressivePattern_EmitsBothCycleAndNonProgressiveDiagnostics()
    {
        var diagnostics = new DiagnosticBag();
        _ = ParseWithDiagnostics("""
            grammar G;
            start : expr ;
            expr : expr
                 | INT
                 ;
            INT : ('0'..'9')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, "1", diagnostics);

        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.ParserStateCycleDetected.Code),
            "expected ParserStateCycleDetected for non-progressive left-recursive extension");
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.NonProgressiveLeftRecursionStopped.Code),
            "expected NonProgressiveLeftRecursionStopped for non-progressive left-recursive extension");
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
    /// Ensures directly recursive rules without consuming tail content terminate with a non-progressive diagnostic.
    /// </summary>
    [TestMethod]
    public void DirectLeftRecursion_NoProgress_TerminatesAndReportsDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        var tree = ParseWithDiagnostics("""
            grammar G;
            start : expr ;
            expr : expr
                 | INT
                 ;
            INT : ('0'..'9')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, "1", diagnostics);

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.NonProgressiveLeftRecursionStopped.Code));
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

    /// <summary>
    /// Ensures shared rule invocations from different parent alternatives are not rejected as false cycles.
    /// </summary>
    [TestMethod]
    public void SharedRuleInvocation_FromDifferentParents_IsAllowed()
    {
        var diagnosticsA = new DiagnosticBag();
        var treeA = ParseWithDiagnostics("""
            grammar G;
            root : a | b ;
            a : common 'X' ;
            b : common 'Y' ;
            common : ID ;
            ID : ('a'..'z')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, "id X", diagnosticsA);

        Assert.IsNotInstanceOfType<ErrorNode>(treeA);

        var diagnosticsB = new DiagnosticBag();
        var treeB = ParseWithDiagnostics("""
            grammar G;
            root : a | b ;
            a : common 'X' ;
            b : common 'Y' ;
            common : ID ;
            ID : ('a'..'z')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, "id Y", diagnosticsB);

        Assert.IsNotInstanceOfType<ErrorNode>(treeB);
        Assert.IsFalse(diagnosticsA.Any(d => d.Code == ParserDiagnostics.ParserStateCycleDetected.Code));
        Assert.IsFalse(diagnosticsB.Any(d => d.Code == ParserDiagnostics.ParserStateCycleDetected.Code));
    }

    /// <summary>
    /// Ensures same rule invoked at the same position from two different alternatives
    /// (distinct continuations) is not rejected as a false cycle.
    /// Within a single parse, both alternatives of root call 'common' at position 0:
    /// alt[0] continues with 'X', alt[1] continues with 'Y'.
    /// The engine must allow both and select the correct winner for each input.
    /// </summary>
    [TestMethod]
    public void SameRuleSamePositionDifferentContinuation_IsAllowed()
    {
        const string grammar = """
            grammar G;
            root : common 'X'
                 | common 'Y'
                 ;
            common : ID ;
            ID : ('a'..'z')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """;

        var diagnosticsX = new DiagnosticBag();
        var treeX = ParseWithDiagnostics(grammar, "id X", diagnosticsX);

        var diagnosticsY = new DiagnosticBag();
        var treeY = ParseWithDiagnostics(grammar, "id Y", diagnosticsY);

        Assert.IsNotInstanceOfType<ErrorNode>(treeX);
        Assert.IsNotInstanceOfType<ErrorNode>(treeY);
        // The correct continuation wins: 'X' terminal in first parse, 'Y' in second.
        Assert.AreEqual(1, CountTokens(treeX, "X"), "alt[0] continuation should win for 'id X'");
        Assert.AreEqual(1, CountTokens(treeY, "Y"), "alt[1] continuation should win for 'id Y'");
        Assert.IsFalse(diagnosticsX.Any(d => d.Code == ParserDiagnostics.ParserStateCycleDetected.Code));
        Assert.IsFalse(diagnosticsY.Any(d => d.Code == ParserDiagnostics.ParserStateCycleDetected.Code));
    }

    /// <summary>
    /// Ensures a duplicate full parser state is detected and reported.
    /// </summary>
    [TestMethod]
    public void DuplicateFullState_ReportsCycleDiagnostic()
    {
        var diagnostics = new DiagnosticBag();
        _ = ParseWithDiagnostics("""
            grammar G;
            start : expr ;
            expr : expr
                 | INT
                 ;
            INT : ('0'..'9')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, "1", diagnostics);

        Assert.IsTrue(diagnostics.Any(d => d.Code == ParserDiagnostics.ParserStateCycleDetected.Code));
    }

    /// <summary>
    /// Ensures repeated shared rule evaluations do not corrupt parse-tree structure.
    /// </summary>
    [TestMethod]
    public void CompletedRuleReuse_DoesNotCorruptTreeShape()
    {
        var diagnostics = new DiagnosticBag();
        var tree = ParseWithDiagnostics("""
            grammar G;
            root : common 'X'
                 | common 'Y'
                 ;
            common : ID ;
            ID : ('a'..'z')+ ;
            WS : (' ' | '\t' | '\r' | '\n')+ -> skip ;
            """, "id X", diagnostics);

        Assert.IsNotInstanceOfType<ErrorNode>(tree);
        Assert.AreEqual(1, CountTokens(tree, "ID"));
        Assert.AreEqual(1, CountTokens(tree, "X"));
        Assert.IsFalse(diagnostics.Any(d => d.Code == ParserDiagnostics.ParserStateCycleDetected.Code));
    }

    private static ParseNode ParseWithDiagnostics(string grammar, string input, DiagnosticBag diagnostics)
    {
        var definition = Antlr4GrammarConverter.Parse(grammar, diagnostics);
        var lexer = new LexerEngine(definition);
        var tokens = lexer.Tokenize(new StringReader(input), diagnostics: diagnostics).ToList();
        var parser = new ParserEngine(definition);
        return parser.Parse(tokens, diagnostics: diagnostics);
    }

    private static int CountTokens(ParseNode node, string ruleNameOrText)
    {
        if (node is LexerNode lexerNode)
        {
            return string.Equals(lexerNode.Token.RuleName, ruleNameOrText, StringComparison.Ordinal)
                || string.Equals(lexerNode.Token.Text, ruleNameOrText, StringComparison.Ordinal)
                ? 1
                : 0;
        }

        if (node is not ParserNode parserNode)
        {
            return 0;
        }

        return parserNode.Children.Sum(child => CountTokens(child, ruleNameOrText));
    }
}
