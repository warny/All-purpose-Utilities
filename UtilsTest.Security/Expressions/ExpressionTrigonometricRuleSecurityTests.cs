using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Security.Expressions;

/// <summary>
/// The trigonometric rules of <see cref="ExpressionSimplifier"/> (mission "trigonometric rule and division chains", part A):
/// called directly with calls of impersonating reflection objects, as a derived simplifier may call them, they decline
/// without reading any member of those objects but the result type the framework reads for the call node (S5 A-10,
/// limitation L-1), and the public entry points keep every evaluation of such calls.
/// </summary>
[TestClass]
public class ExpressionTrigonometricRuleSecurityTests
{
    /// <summary>A free parameter of type <see cref="double"/>.</summary>
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");

    /// <summary>A derived simplifier exposing the protected trigonometric rules.</summary>
    private sealed class ExposedSimplifier : ExpressionSimplifier
    {
        public Expression? CosAndSin(Expression e, MethodCallExpression left, MethodCallExpression right) => DivisionOfCosAndSinNumber(e, left, right);
        public Expression? SinAndCos(Expression e, MethodCallExpression left, MethodCallExpression right) => DivisionOfSinAndCosNumber(e, left, right);
        public Expression? CosAndTan(Expression e, MethodCallExpression left, MethodCallExpression right) => MultiplicationOfCosAndTanNumber(e, left, right);
        public Expression? TanAndCos(Expression e, MethodCallExpression left, MethodCallExpression right) => MultiplicationOfTanAndCosNumber(e, left, right);
        public Expression? SinAndTan(Expression e, MethodCallExpression left, MethodCallExpression right) => DivisionOfSinAndTanNumber(e, left, right);
        public Expression? Squares(Expression e, BinaryExpression left, BinaryExpression right) => AdditionOfCos2andSin2Number(e, left, right);
    }

    /// <summary>The runtime function of <see cref="double"/> named <paramref name="name"/>.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The method.</returns>
    private static MethodInfo DoubleFunction(string name) => typeof(double).GetMethod(name, [typeof(double)])!;

    /// <summary>Asserts that every call into the impersonations was a type read the framework makes for the call node.</summary>
    /// <param name="methods">The impersonations.</param>
    private static void AssertOnlyTypeReads(params ImpersonatingMethod[] methods)
    {
        foreach (ImpersonatingMethod method in methods)
        {
            Assert.AreEqual(0, method.Trap.ArmedCalls, string.Join(", ", method.Trap.Calls));
            Assert.IsTrue(method.Trap.Calls.All(call => call == "ReturnType"), string.Join(", ", method.Trap.Calls));
        }
    }

    /// <summary>
    /// The rules called directly with impersonations of <see cref="double.Sin"/>, <see cref="double.Cos"/> and
    /// <see cref="double.Tan"/>: each declines and reads nothing of them but their result type.
    /// </summary>
    [TestMethod]
    public void Rules_DeclineImpersonatingMethods_WithoutReadingThem()
    {
        var sin = new ImpersonatingMethod(DoubleFunction(nameof(double.Sin)));
        var cos = new ImpersonatingMethod(DoubleFunction(nameof(double.Cos)));
        var tan = new ImpersonatingMethod(DoubleFunction(nameof(double.Tan)));
        MethodCallExpression sinX = Expression.Call(sin, X), cosX = Expression.Call(cos, X), tanX = Expression.Call(tan, X);
        BinaryExpression sin2 = Expression.Power(sinX, Expression.Constant(2.0)), cos2 = Expression.Power(cosX, Expression.Constant(2.0));
        var rules = new ExposedSimplifier();

        foreach (ImpersonatingMethod method in new[] { sin, cos, tan })
        {
            method.Trap.Reset();
            method.Trap.Armed = true;
        }

        try
        {
            Assert.IsNull(rules.CosAndSin(Expression.Divide(sinX, cosX), sinX, cosX));
            Assert.IsNull(rules.SinAndCos(Expression.Divide(cosX, sinX), cosX, sinX));
            Assert.IsNull(rules.CosAndTan(Expression.Multiply(cosX, tanX), cosX, tanX));
            Assert.IsNull(rules.TanAndCos(Expression.Multiply(tanX, cosX), tanX, cosX));
            Assert.IsNull(rules.SinAndTan(Expression.Divide(sinX, tanX), sinX, tanX));
            Assert.IsNull(rules.Squares(Expression.Add(sin2, cos2), sin2, cos2));
            Assert.IsNull(rules.Squares(Expression.Add(cos2, sin2), cos2, sin2));
        }
        finally
        {
            foreach (ImpersonatingMethod method in new[] { sin, cos, tan }) method.Trap.Armed = false;
        }

        AssertOnlyTypeReads(sin, cos, tan);
    }

    /// <summary>
    /// One real function and one impersonation (each order): the real method alone never makes the rule apply, so the
    /// impersonated evaluation is kept through the public entry point, and its trap observes the evaluations of the source.
    /// </summary>
    [TestMethod]
    public void MixedCalls_KeepTheImpersonatedEvaluation()
    {
        var sin = new ImpersonatingMethod(DoubleFunction(nameof(double.Sin)));
        MethodInfo cos = DoubleFunction(nameof(double.Cos));
        Expression[] sources =
        [
            Expression.Add(Expression.Power(Expression.Call(sin, X), Expression.Constant(2.0)), Expression.Power(Expression.Call(cos, X), Expression.Constant(2.0))),
            Expression.Add(Expression.Power(Expression.Call(cos, X), Expression.Constant(2.0)), Expression.Power(Expression.Call(sin, X), Expression.Constant(2.0))),
            Expression.Divide(Expression.Call(sin, X), Expression.Call(cos, X)),
        ];

        foreach (Expression source in sources)
        {
            Expression result = new ExpressionSimplifier().Simplify(source);
            Assert.IsNotInstanceOfType<ConstantExpression>(result, result.ToString());
            Func<double, double> compiled = Expression.Lambda<Func<double, double>>(result, X).Compile(preferInterpretation: true);
            sin.Trap.Reset();
            compiled(0.5);
            Assert.AreEqual(1, sin.Trap.Evaluations, result.ToString());
        }
    }
}
