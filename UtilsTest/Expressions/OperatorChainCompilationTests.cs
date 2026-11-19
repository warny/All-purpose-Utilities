using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.CSyntax.Runtime;
using Utils.Expressions.VBSyntax.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// Issue #711: binary operator chains (<c>a op b op c ...</c>) of the VBSyntax and CSyntax compilers apply each operator
/// between the right operands, fold left-associatively (right-associatively for powers), keep parenthesized operands
/// intact and read operators from the parse tree rather than from the source text. Before the fix both compilers grouped
/// chains of three or more operands from the right (<c>a - b - c</c> as <c>a - (b - c)</c>) and VBSyntax could apply the
/// first operator of a chain to a later pair (<c>(x + 1) * (x - 1) / 2</c> as <c>(x + 1) * ((x - 1) * 2)</c>).
/// </summary>
[TestClass]
public class OperatorChainCompilationTests
{
    /// <summary>The values of <c>x</c> every numeric case is evaluated at.</summary>
    private static readonly double[] XValues = [-2.5, 0, 1, 3, 7];

    /// <summary>Numeric chains of both dialects, with the C# reference function of the same expression.</summary>
    /// <returns>Display name, VB text, C# text and reference function.</returns>
    public static IEnumerable<object[]> NumericChains()
    {
        yield return Case("mixed * / after parentheses", "(x + 1) * (x - 1) / 2", "(x + 1) * (x - 1) / 2", x => (x + 1) * (x - 1) / 2);
        yield return Case("unary operands in a * / chain", "-x * -(x - 3) / 4", "-x * -(x - 3) / 4", x => -x * -(x - 3) / 4);
        yield return Case("left-associative subtraction", "x - 10 - 1", "x - 10 - 1", x => x - 10 - 1);
        yield return Case("left-associative division", "x / 100 / 2", "x / 100 / 2", x => x / 100 / 2);
        yield return Case("alternating * and /", "x * 2 / 4 * 3", "x * 2 / 4 * 3", x => x * 2 / 4 * 3);
        yield return Case("alternating + and -", "x - 1 + 2 - 3 + 4", "x - 1 + 2 - 3 + 4", x => x - 1 + 2 - 3 + 4);
        yield return Case("parenthesized operand keeps its operators", "x - (1 - 2) - 3", "x - (1 - 2) - 3", x => x - (1 - 2) - 3);
        yield return Case("parenthesized * / operand", "x / (2 * 4) / 2", "x / (2 * 4) / 2", x => x / (2 * 4) / 2);
        yield return Case("precedence across levels", "x + x * 2 - 3.0 / 4 * x", "x + x * 2 - 3.0 / 4 * x", x => x + x * 2 - 3.0 / 4 * x);
        yield return Case("nested chains in parentheses", "(x - 1 - 2) * (x / 2 / 2) - (x - 3)", "(x - 1 - 2) * (x / 2 / 2) - (x - 3)", x => (x - 1 - 2) * (x / 2 / 2) - (x - 3));
        yield return Case("modulo in a chain", "x * 3 Mod 4 * 2", "x * 3 % 4 * 2", x => x * 3 % 4 * 2);
        yield return Case("long subtraction chain", "x - 1 - 2 - 3 - 4 - 5 - 6", "x - 1 - 2 - 3 - 4 - 5 - 6", x => x - 1 - 2 - 3 - 4 - 5 - 6);
        yield return Case("right-associative power", "x ^ 2 ^ 2", "x ** 2 ** 2", x => Math.Pow(x, Math.Pow(2, 2)));
        yield return Case("power inside a * / chain", "x ^ 2 / 2 * x ^ 3", "x ** 2 / 2 * x ** 3", x => Math.Pow(x, 2) / 2 * Math.Pow(x, 3));
    }

    /// <summary>Every VBSyntax numeric chain evaluates like the C# reference.</summary>
    /// <param name="name">The case name.</param>
    /// <param name="vbText">The VBSyntax text.</param>
    /// <param name="cText">The CSyntax text (unused here).</param>
    /// <param name="reference">The reference function.</param>
    [TestMethod]
    [DynamicData(nameof(NumericChains))]
    public void VBSyntax_NumericChain_MatchesReference(string name, string vbText, string cText, Func<double, double> reference)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        var compiled = (Func<double, double>)new VBSyntaxExpressionCompiler().CompileExpression<Func<double, double>>(vbText, [x]).Compile();
        AssertSameValues(name, reference, compiled);
    }

    /// <summary>Every CSyntax numeric chain evaluates like the C# reference.</summary>
    /// <param name="name">The case name.</param>
    /// <param name="vbText">The VBSyntax text (unused here).</param>
    /// <param name="cText">The CSyntax text.</param>
    /// <param name="reference">The reference function.</param>
    [TestMethod]
    [DynamicData(nameof(NumericChains))]
    public void CSyntax_NumericChain_MatchesReference(string name, string vbText, string cText, Func<double, double> reference)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        var compiled = (Func<double, double>)new CSyntaxExpressionCompiler().CompileExpression<Func<double, double>>(cText, [x], typeof(Math), false).Compile();
        AssertSameValues(name, reference, compiled);
    }

    /// <summary>Integer chains keep left associativity and integer division in both dialects.</summary>
    [TestMethod]
    public void IntegerChains_FoldLeftWithIntegerSemantics()
    {
        Assert.AreEqual(100 - 10 - 1, EvaluateVb<int>("100 - 10 - 1"));
        Assert.AreEqual(100 / 10 / 2, EvaluateVb<int>("100 / 10 / 2"));
        Assert.AreEqual(7 * 5 / 2 * 3, EvaluateVb<int>("7 * 5 / 2 * 3"));
        Assert.AreEqual(17 % 5 * 3, EvaluateVb<int>("17 Mod 5 * 3"));
        Assert.AreEqual(1 << 4 >> 2, EvaluateVb<int>("1 << 4 >> 2"));
        Assert.AreEqual(100 - 10 - 1, EvaluateC<int>("100 - 10 - 1"));
        Assert.AreEqual(100 / 10 / 2, EvaluateC<int>("100 / 10 / 2"));
        Assert.AreEqual(7 * 5 / 2 * 3, EvaluateC<int>("7 * 5 / 2 * 3"));
        Assert.AreEqual(17 % 5 * 3, EvaluateC<int>("17 % 5 * 3"));
        Assert.AreEqual(1 << 4 >> 2, EvaluateC<int>("1 << 4 >> 2"));
    }

    /// <summary>The compiled tree of a chain is the left fold of its operands, with operators in source order.</summary>
    [TestMethod]
    public void ChainTrees_AreLeftFoldsInSourceOrder()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression vb = new VBSyntaxExpressionCompiler().CompileExpression<Func<double, double>>("x - 10 - 1", [x]).Body;
        Expression c = new CSyntaxExpressionCompiler().CompileExpression<Func<double, double>>("x * 2 / 4", [x], typeof(Math), false).Body;

        var vbOuter = (BinaryExpression)vb;
        Assert.AreEqual(ExpressionType.Subtract, vbOuter.NodeType);
        Assert.AreEqual(ExpressionType.Subtract, vbOuter.Left.NodeType, "(x - 10) is the left operand");
        Assert.AreSame(x, ((BinaryExpression)vbOuter.Left).Left);

        var cOuter = (BinaryExpression)c;
        Assert.AreEqual(ExpressionType.Divide, cOuter.NodeType);
        Assert.AreEqual(ExpressionType.Multiply, cOuter.Left.NodeType, "(x * 2) is the left operand");
    }

    /// <summary>Comparison chains apply each operator to the right pair in both dialects.</summary>
    [TestMethod]
    public void ComparisonChains_ApplyEachOperatorToItsPair()
    {
        // (1 < 2) = (3 > 4) is False = False is True; (1 < 2) <> (3 > 4) is True.
        Assert.IsTrue(EvaluateVb<bool>("1 < 2 = 3 > 4 = False"));
        Assert.IsTrue(EvaluateVb<bool>("1 < 2 <> 3 > 4"));
        Assert.IsTrue(EvaluateVb<bool>("2 <= 2 = 3 >= 4 <> True"));
        Assert.IsTrue(EvaluateC<bool>("(1 < 2) != (3 > 4)"));
        Assert.IsTrue(EvaluateC<bool>("1 < 2 == 3 > 4 == false"));
    }

    /// <summary>
    /// VBSyntax logical chains mixing operators and parentheses use the operators of the chain itself, not those of a
    /// parenthesized operand, and keep left associativity.
    /// </summary>
    [TestMethod]
    public void VBSyntax_LogicalChains_UseTheirOwnOperators()
    {
        // (True Or False) OrElse False Xor True == ((True Or False) OrElse False) Xor True == True Xor True == False.
        Assert.IsFalse(EvaluateVb<bool>("(True Or False) OrElse False Xor True"));
        // True Xor True Or True == (True Xor True) Or True == True; a right fold would give True Xor True == False.
        Assert.IsTrue(EvaluateVb<bool>("True Xor True Or True"));
        // False Or True Xor True == (False Or True) Xor True == False; the former right grouping gave True.
        Assert.IsFalse(EvaluateVb<bool>("False Or True Xor True"));
        // (False And True) AndAlso True And True == False.
        Assert.IsFalse(EvaluateVb<bool>("(False And True) AndAlso True And True"));
        Assert.IsTrue(EvaluateVb<bool>("True AndAlso (False Or True) And True"));
    }

    /// <summary>
    /// VBSyntax reads <c>Not</c> from the parse tree: an identifier or literal whose text starts with "Not" is not
    /// negated, while <c>Not</c> itself still is.
    /// </summary>
    [TestMethod]
    public void VBSyntax_Not_IsReadFromTheParseTree()
    {
        var symbols = new Dictionary<string, Expression>(StringComparer.OrdinalIgnoreCase)
        {
            ["notes"] = Expression.Constant(true),
            ["notable"] = Expression.Constant(false),
        };
        var compiler = new VBSyntaxExpressionCompiler();

        Assert.IsTrue(Expression.Lambda<Func<bool>>(compiler.CompileExpression("notes", symbols)).Compile()());
        Assert.IsFalse(Expression.Lambda<Func<bool>>(compiler.CompileExpression("Not notes", symbols)).Compile()());
        Assert.IsTrue(Expression.Lambda<Func<bool>>(compiler.CompileExpression("Not notable AndAlso notes", symbols)).Compile()());
        Assert.IsNull(Expression.Lambda<Func<object?>>(compiler.CompileExpression("Nothing")).Compile()());
    }

    /// <summary>VBSyntax string concatenation chains keep every operand in order, including parenthesized chains.</summary>
    [TestMethod]
    public void VBSyntax_ConcatChains_KeepEveryOperandInOrder()
    {
        Assert.AreEqual("abcd", EvaluateVb<string>("\"a\" & \"b\" & (\"c\" & \"d\")"));
        Assert.AreEqual("x1y", EvaluateVb<string>("\"x\" & 1 & \"y\""));
    }

    /// <summary>Builds a dynamic-data case.</summary>
    /// <param name="name">The case name.</param>
    /// <param name="vbText">The VBSyntax text.</param>
    /// <param name="cText">The CSyntax text.</param>
    /// <param name="reference">The reference function.</param>
    /// <returns>The case.</returns>
    private static object[] Case(string name, string vbText, string cText, Func<double, double> reference) => [name, vbText, cText, reference];

    /// <summary>Asserts that two functions return the same value at every sampled <c>x</c>.</summary>
    /// <param name="name">The case name.</param>
    /// <param name="reference">The reference function.</param>
    /// <param name="compiled">The compiled function.</param>
    private static void AssertSameValues(string name, Func<double, double> reference, Func<double, double> compiled)
    {
        foreach (double x in XValues)
        {
            Assert.AreEqual(reference(x), compiled(x), 1e-12, $"{name} at x = {x}");
        }
    }

    /// <summary>Compiles and evaluates a VBSyntax expression without parameters.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="text">The expression.</param>
    /// <returns>The value.</returns>
    private static T EvaluateVb<T>(string text)
        => Expression.Lambda<Func<T>>(Expression.Convert(new VBSyntaxExpressionCompiler().CompileExpression(text), typeof(T))).Compile()();

    /// <summary>Compiles and evaluates a CSyntax expression without parameters.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="text">The expression.</param>
    /// <returns>The value.</returns>
    private static T EvaluateC<T>(string text)
        => Expression.Lambda<Func<T>>(Expression.Convert(new CSyntaxExpressionCompiler().CompileExpression(text), typeof(T))).Compile()();
}
