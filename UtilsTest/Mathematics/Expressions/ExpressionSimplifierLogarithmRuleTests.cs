using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Verifies that the unary logarithm-combination rules are reachable through the public simplifier,
/// produce executable concrete calls, and conservatively reject unsupported logarithm overloads.
/// </summary>
[TestClass]
public class ExpressionSimplifierLogarithmRuleTests
{
    /// <summary>Positive finite operands used to compare source and simplified logarithm delegates.</summary>
    private static readonly (double Left, double Right)[] PositiveFiniteOperands =
    [
        (2.0, 5.0),
        (0.5, 8.0),
        (10.0, 100.0),
        (1.25, 3.75),
    ];

    /// <summary>
    /// Verifies that unary <see cref="double.Log(double)"/> addition becomes one executable concrete
    /// logarithm call whose argument is the product of the two original operands.
    /// </summary>
    [TestMethod]
    public void Simplify_UnaryLogAddition_CombinesAndExecutes()
    {
        Expression<Func<double, double, double>> source = (x, y) => double.Log(x) + double.Log(y);

        AssertCombinedUnaryLog(source, nameof(double.Log), ExpressionType.Multiply);
    }

    /// <summary>
    /// Verifies that unary <see cref="double.Log(double)"/> subtraction becomes one executable concrete
    /// logarithm call whose argument is the quotient of the two original operands.
    /// </summary>
    [TestMethod]
    public void Simplify_UnaryLogSubtraction_CombinesAndExecutes()
    {
        Expression<Func<double, double, double>> source = (x, y) => double.Log(x) - double.Log(y);

        AssertCombinedUnaryLog(source, nameof(double.Log), ExpressionType.Divide);
    }

    /// <summary>
    /// Regression for the missing method-level addition signature: unary
    /// <see cref="double.Log10(double)"/> calls must combine and execute.
    /// </summary>
    [TestMethod]
    public void Simplify_UnaryLog10Addition_CombinesAndExecutes()
    {
        Expression<Func<double, double, double>> source = (x, y) => double.Log10(x) + double.Log10(y);

        AssertCombinedUnaryLog(source, nameof(double.Log10), ExpressionType.Multiply);
    }

    /// <summary>
    /// Regression for the inconsistent declaring-type constraint: unary
    /// <see cref="double.Log10(double)"/> calls must combine under subtraction and execute.
    /// </summary>
    [TestMethod]
    public void Simplify_UnaryLog10Subtraction_CombinesAndExecutes()
    {
        Expression<Func<double, double, double>> source = (x, y) => double.Log10(x) - double.Log10(y);

        AssertCombinedUnaryLog(source, nameof(double.Log10), ExpressionType.Divide);
    }

    /// <summary>
    /// Verifies that two-argument logarithms with different bases retain both base operands and source
    /// semantics instead of being consumed by the unary natural-log rule.
    /// </summary>
    [TestMethod]
    public void Simplify_TwoArgumentLogsWithDifferentBases_AreNotCombined()
    {
        Expression<Func<double, double, double, double, double>> source =
            (value1, base1, value2, base2) => double.Log(value1, base1) + double.Log(value2, base2);

        var simplified = (Expression<Func<double, double, double, double, double>>)
            new ExpressionSimplifier().Simplify(source);

        AssertTwoBinaryLogCallsRemain(simplified.Body);
        Assert.AreEqual(source.Compile()(8.0, 2.0, 100.0, 10.0), simplified.Compile()(8.0, 2.0, 100.0, 10.0), 1e-12);
    }

    /// <summary>
    /// Verifies that two-argument logarithms sharing a base remain a negative control: S2 restores only
    /// the existing unary-log identities and does not introduce a same-base logarithm feature.
    /// </summary>
    [TestMethod]
    public void Simplify_TwoArgumentLogsWithSameBase_AreNotCombined()
    {
        Expression<Func<double, double, double, double>> source =
            (x, y, newBase) => double.Log(x, newBase) + double.Log(y, newBase);

        var simplified = (Expression<Func<double, double, double, double>>)
            new ExpressionSimplifier().Simplify(source);

        AssertTwoBinaryLogCallsRemain(simplified.Body);
        Assert.AreEqual(source.Compile()(8.0, 32.0, 2.0), simplified.Compile()(8.0, 32.0, 2.0), 1e-12);
    }

    /// <summary>
    /// Verifies that subtracting two-argument logarithms with different bases retains both base operands
    /// and source semantics instead of being consumed by the unary natural-log subtraction rule.
    /// </summary>
    [TestMethod]
    public void Simplify_SubtractedTwoArgumentLogsWithDifferentBases_AreNotCombined()
    {
        Expression<Func<double, double, double, double, double>> source =
            (value1, base1, value2, base2) => double.Log(value1, base1) - double.Log(value2, base2);

        var simplified = (Expression<Func<double, double, double, double, double>>)
            new ExpressionSimplifier().Simplify(source);

        AssertTwoBinaryLogCallsRemain(simplified.Body);
        Assert.AreEqual(source.Compile()(8.0, 2.0, 100.0, 10.0), simplified.Compile()(8.0, 2.0, 100.0, 10.0), 1e-12);
    }

    /// <summary>
    /// Verifies that subtracting two-argument logarithms sharing a base remains a negative control and
    /// does not introduce a same-base logarithm feature through the unary natural-log subtraction rule.
    /// </summary>
    [TestMethod]
    public void Simplify_SubtractedTwoArgumentLogsWithSameBase_AreNotCombined()
    {
        Expression<Func<double, double, double, double>> source =
            (x, y, newBase) => double.Log(x, newBase) - double.Log(y, newBase);

        var simplified = (Expression<Func<double, double, double, double>>)
            new ExpressionSimplifier().Simplify(source);

        AssertTwoBinaryLogCallsRemain(simplified.Body);
        Assert.AreEqual(source.Compile()(8.0, 32.0, 2.0), simplified.Compile()(8.0, 32.0, 2.0), 1e-12);
    }

    /// <summary>
    /// Characterizes the current input-family boundary: direct <see cref="Math.Log(double)"/> calls are
    /// preserved rather than normalized or combined, while the simplifier's active logarithm conversion
    /// and combination rules target static methods declared by <see cref="double"/>.
    /// </summary>
    [TestMethod]
    public void Simplify_DirectMathLogFamilies_ArePreserved()
    {
        Expression<Func<double, double, double>> natural = (x, y) => Math.Log(x) + Math.Log(y);
        Expression<Func<double, double, double>> base10 = (x, y) => Math.Log10(x) - Math.Log10(y);

        var simplifiedNatural = (Expression<Func<double, double, double>>)new ExpressionSimplifier().Simplify(natural);
        var simplifiedBase10 = (Expression<Func<double, double, double>>)new ExpressionSimplifier().Simplify(base10);

        Assert.AreEqual(2, FindCalls(simplifiedNatural.Body).Count(call => call.Method.DeclaringType == typeof(Math)));
        Assert.AreEqual(2, FindCalls(simplifiedBase10.Body).Count(call => call.Method.DeclaringType == typeof(Math)));
        Assert.AreEqual(natural.Compile()(2.0, 5.0), simplifiedNatural.Compile()(2.0, 5.0), 1e-12);
        Assert.AreEqual(base10.Compile()(100.0, 10.0), simplifiedBase10.Compile()(100.0, 10.0), 1e-12);
    }

    /// <summary>
    /// Structural dispatch invariant: a method with parameter-level signature constraints must also have
    /// a method-level signature, because only method-level signatures register rules in the build plan.
    /// </summary>
    [TestMethod]
    public void SimplifierMethods_WithParameterSignatureConstraints_HaveMethodSignature()
    {
        MethodInfo[] malformedMethods = typeof(ExpressionSimplifier)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.GetParameters().Any(parameter =>
                parameter.GetCustomAttributes<ExpressionSignatureAttribute>().Any()))
            .Where(method => !method.GetCustomAttributes<ExpressionSignatureAttribute>().Any())
            .ToArray();

        Assert.AreEqual(0, malformedMethods.Length,
            $"Methods with dispatch-inert parameter constraints: {string.Join(", ", malformedMethods.Select(method => method.Name))}");
    }

    /// <summary>Asserts the expected concrete combined shape and executable equivalence.</summary>
    /// <param name="source">The source logarithm lambda.</param>
    /// <param name="methodName">The expected concrete logarithm method name.</param>
    /// <param name="argumentNodeType">The expected operation combining the logarithm arguments.</param>
    private static void AssertCombinedUnaryLog(
        Expression<Func<double, double, double>> source,
        string methodName,
        ExpressionType argumentNodeType)
    {
        var simplified = (Expression<Func<double, double, double>>)new ExpressionSimplifier().Simplify(source);

        var call = simplified.Body as MethodCallExpression;
        Assert.IsNotNull(call);
        Assert.AreEqual(typeof(double), call.Method.DeclaringType);
        Assert.AreEqual(methodName, call.Method.Name);
        Assert.AreEqual(1, call.Arguments.Count);
        var combinedArgument = call.Arguments[0] as BinaryExpression;
        Assert.IsNotNull(combinedArgument);
        Assert.AreEqual(argumentNodeType, combinedArgument.NodeType);
        Assert.AreSame(source.Parameters[0], combinedArgument.Left);
        Assert.AreSame(source.Parameters[1], combinedArgument.Right);

        Func<double, double, double> sourceDelegate = source.Compile();
        Func<double, double, double> simplifiedDelegate = simplified.Compile();
        foreach ((double left, double right) in PositiveFiniteOperands)
        {
            Assert.AreEqual(sourceDelegate(left, right), simplifiedDelegate(left, right), 1e-12);
        }
    }

    /// <summary>Asserts that a simplified tree still contains two complete two-argument logarithm calls.</summary>
    /// <param name="body">The simplified lambda body.</param>
    private static void AssertTwoBinaryLogCallsRemain(Expression body)
    {
        MethodCallExpression[] calls = FindCalls(body)
            .Where(call => call.Method.DeclaringType == typeof(double) && call.Method.Name == nameof(double.Log))
            .ToArray();

        Assert.AreEqual(2, calls.Length);
        Assert.IsTrue(calls.All(call => call.Arguments.Count == 2), "Both logarithm bases must remain in the tree.");
    }

    /// <summary>Collects every method call below an expression without changing the tree.</summary>
    /// <param name="expression">The expression to inspect.</param>
    /// <returns>All method-call nodes in traversal order.</returns>
    private static IReadOnlyList<MethodCallExpression> FindCalls(Expression expression)
    {
        var visitor = new MethodCallCollector();
        visitor.Visit(expression);
        return visitor.Calls;
    }

    /// <summary>Collects method calls for structural assertions in the negative-control tests.</summary>
    private sealed class MethodCallCollector : ExpressionVisitor
    {
        /// <summary>Gets the collected method-call nodes.</summary>
        public List<MethodCallExpression> Calls { get; } = [];

        /// <summary>Records a method call and continues traversing its children.</summary>
        /// <param name="node">The method-call node being visited.</param>
        /// <returns>The unchanged visited node.</returns>
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Calls.Add(node);
            return base.VisitMethodCall(node);
        }
    }
}
