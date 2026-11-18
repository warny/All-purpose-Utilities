using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// The trigonometric rules of <see cref="ExpressionSimplifier"/> (<c>AdditionOfCos2andSin2Number</c>, the quotient and product
/// rules of <c>sin</c>, <c>cos</c> and <c>tan</c>): a shape that is not the identity declines, without exception, and the
/// identities keep applying (mission "trigonometric rule and division chains", part A, <c>docs/expressions/SimplifierFlatten.md</c>
/// §15).
/// </summary>
[TestClass]
public class ExpressionSimplifierTrigonometricRuleTests
{
    private static readonly ParameterExpression X = Expression.Parameter(typeof(double), "x");
    private static readonly ParameterExpression Y = Expression.Parameter(typeof(double), "y");

    private static readonly MethodInfo Sin = typeof(double).GetMethod(nameof(double.Sin), [typeof(double)])!;
    private static readonly MethodInfo Cos = typeof(double).GetMethod(nameof(double.Cos), [typeof(double)])!;
    private static readonly MethodInfo Tan = typeof(double).GetMethod(nameof(double.Tan), [typeof(double)])!;

    private static readonly MethodInfo DegreeSin = typeof(DegreeTrigonometry).GetMethod(nameof(DegreeTrigonometry.Sin), [typeof(double)])!;
    private static readonly MethodInfo DegreeCos = typeof(DegreeTrigonometry).GetMethod(nameof(DegreeTrigonometry.Cos), [typeof(double)])!;
    private static readonly MethodInfo DegreeTan = typeof(DegreeTrigonometry).GetMethod(nameof(DegreeTrigonometry.Tan), [typeof(double)])!;
    private static readonly MethodInfo ScaledSin = typeof(DegreeTrigonometry).GetMethod(nameof(DegreeTrigonometry.Sin), [typeof(double), typeof(double)])!;
    private static readonly MethodInfo ScaledCos = typeof(DegreeTrigonometry).GetMethod(nameof(DegreeTrigonometry.Cos), [typeof(double), typeof(double)])!;

    private static ConstantExpression K(double value) => Expression.Constant(value);

    private static Expression Square(Expression e) => Expression.Power(e, K(2));

    private static Expression S(Expression e) => new ExpressionSimplifier().Simplify(e);

    /// <summary>Compiles both trees over x and y and compares them on samples (NaN matches NaN).</summary>
    private static void AssertSameValues(Expression source, Expression result)
    {
        Func<double, double, double> f = Expression.Lambda<Func<double, double, double>>(source, X, Y).Compile();
        Func<double, double, double> g = Expression.Lambda<Func<double, double, double>>(result, X, Y).Compile();
        double[] samples = [0.5, 1.25, 2, 3.5, -0.75, 10];
        foreach (double x in samples)
        {
            foreach (double y in samples)
            {
                double a = f(x, y), b = g(x, y);
                if (double.IsNaN(a) && double.IsNaN(b)) continue;
                Assert.AreEqual(a, b, 1e-9 * Math.Max(1, Math.Abs(a)), $"{source} -> {result} at ({x}, {y})");
            }
        }
    }

    /// <summary>
    /// S5A-01: a power whose exponent is not a constant (a parameter, a call, a computed expression) made the rule dereference a
    /// missing constant and throw <see cref="NullReferenceException"/>; it declines and the sum is simplified like any other.
    /// </summary>
    [TestMethod]
    public void NonConstantExponent_DeclinesWithoutException()
    {
        Expression[] sources =
        [
            Expression.Add(Expression.Power(X, X), Expression.Power(X, Y)),
            Expression.Add(Square(X), Expression.Power(X, Y)),
            Expression.Add(Expression.Power(X, Y), Square(X)),
            Expression.Add(Expression.Power(X, Expression.Add(X, K(1))), Square(Y)),
            Expression.Add(Expression.Power(X, Expression.Call(Sin, Y)), Square(X)),
            Expression.Add(Expression.Power(Expression.Call(Sin, X), Y), Square(Expression.Call(Cos, X))),
        ];

        foreach (Expression source in sources)
        {
            Expression result = S(source);
            AssertSameValues(source, result);
            // The comparer simplifies first: it threw too.
            _ = ExpressionComparer.Default.GetHashCode(source);
            Assert.IsTrue(ExpressionComparer.Default.Equals(source, result), source.ToString());
        }
    }

    /// <summary>
    /// The quotient and product rules match any public <c>Sin</c>, <c>Cos</c> or <c>Tan</c> of a type implementing
    /// <see cref="System.Numerics.ITrigonometricFunctions{TSelf}"/> by its name. Through the public entry points the effect gate
    /// refuses every rule on a call of a user method, under every policy, so such a call is never rewritten
    /// (<c>DegreeTrigonometry.Sin(x) / DegreeTrigonometry.Cos(x)</c> works in degrees and must not become <c>double.Tan(x)</c>).
    /// </summary>
    [TestMethod]
    public void ForeignTrigonometricMethods_AreNotRewritten()
    {
        Expression[] sources =
        [
            Expression.Divide(Expression.Call(DegreeSin, X), Expression.Call(DegreeCos, X)),
            Expression.Divide(Expression.Call(DegreeCos, X), Expression.Call(DegreeSin, X)),
            Expression.Multiply(Expression.Call(DegreeCos, X), Expression.Call(DegreeTan, X)),
            Expression.Multiply(Expression.Call(DegreeTan, X), Expression.Call(DegreeCos, X)),
            Expression.Divide(Expression.Call(DegreeSin, X), Expression.Call(DegreeTan, X)),
            Expression.Divide(Expression.Call(ScaledSin, X, K(2)), Expression.Call(ScaledCos, X, K(3))),
            Expression.Add(Square(Expression.Call(DegreeSin, X)), Square(Expression.Call(DegreeCos, X))),
            Expression.Add(Square(Expression.Call(ScaledSin, X, K(2))), Square(Expression.Call(ScaledCos, X, K(3)))),
        ];

        foreach (ExpressionSimplifierOptions options in Policies)
        {
            foreach (Expression source in sources)
            {
                AssertSameValues(source, new ExpressionSimplifier { Options = options }.Simplify(source));
            }
        }
    }

    /// <summary>
    /// The policies: under the default one the effect gate refuses every rule on a call of an unknown method, so only a policy
    /// that lets rules run on such calls reaches the quotient and product rules with them.
    /// </summary>
    private static readonly ExpressionSimplifierOptions[] Policies =
    [
        ExpressionSimplifierOptions.Default,
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations },
        new() { AggressiveSimplification = true },
        new() { ExceptionPreservation = ExpressionExceptionPreservation.None },
        new() { TrustPolicy = ExpressionTrustPolicy.TrustDeclarations, AggressiveSimplification = true, ExceptionPreservation = ExpressionExceptionPreservation.None },
    ];

    /// <summary>A derived simplifier that exposes the protected trigonometric rules, as a subclass may call them.</summary>
    private sealed class ExposedSimplifier : ExpressionSimplifier
    {
        public Expression? CosAndSin(Expression e, MethodCallExpression left, MethodCallExpression right) => DivisionOfCosAndSinNumber(e, left, right);
        public Expression? SinAndCos(Expression e, MethodCallExpression left, MethodCallExpression right) => DivisionOfSinAndCosNumber(e, left, right);
        public Expression? CosAndTan(Expression e, MethodCallExpression left, MethodCallExpression right) => MultiplicationOfCosAndTanNumber(e, left, right);
        public Expression? TanAndCos(Expression e, MethodCallExpression left, MethodCallExpression right) => MultiplicationOfTanAndCosNumber(e, left, right);
        public Expression? SinAndTan(Expression e, MethodCallExpression left, MethodCallExpression right) => DivisionOfSinAndTanNumber(e, left, right);
        public Expression? Squares(Expression e, BinaryExpression left, BinaryExpression right) => AdditionOfCos2andSin2Number(e, left, right);
    }

    /// <summary>
    /// The rules themselves, called directly with the calls their signatures match: a method that is not the very function of
    /// the operand type (a degree-based <c>Sin</c>, a two-argument overload) gave a <see cref="double.Tan"/>, <see cref="double.Sin"/>
    /// or <see cref="double.Cos"/> call of other values (or dropped an argument); every rule declines.
    /// </summary>
    [TestMethod]
    public void ForeignTrigonometricMethods_RulesDecline()
    {
        var rules = new ExposedSimplifier();
        MethodCallExpression sin = Expression.Call(DegreeSin, X), cos = Expression.Call(DegreeCos, X), tan = Expression.Call(DegreeTan, X);
        MethodCallExpression scaledSin = Expression.Call(ScaledSin, X, K(2)), scaledCos = Expression.Call(ScaledCos, X, K(3));
        Assert.IsNull(rules.CosAndSin(Expression.Divide(sin, cos), sin, cos));
        Assert.IsNull(rules.CosAndSin(Expression.Divide(scaledSin, scaledCos), scaledSin, scaledCos));
        Assert.IsNull(rules.SinAndCos(Expression.Divide(cos, sin), cos, sin));
        Assert.IsNull(rules.SinAndCos(Expression.Divide(scaledCos, scaledSin), scaledCos, scaledSin));
        Assert.IsNull(rules.CosAndTan(Expression.Multiply(cos, tan), cos, tan));
        Assert.IsNull(rules.TanAndCos(Expression.Multiply(tan, cos), tan, cos));
        Assert.IsNull(rules.SinAndTan(Expression.Divide(sin, tan), sin, tan));
        Assert.IsNull(rules.Squares(Expression.Add(Square(sin), Square(cos)), (BinaryExpression)Square(sin), (BinaryExpression)Square(cos)));
        Assert.IsNull(rules.Squares(Expression.Add(Square(scaledSin), Square(scaledCos)), (BinaryExpression)Square(scaledSin), (BinaryExpression)Square(scaledCos)));
    }

    /// <summary>A constant exponent other than <c>2</c> is not the identity: <c>sin(x)^1 + cos(x)^2</c> is not <c>1</c>.</summary>
    [TestMethod]
    public void OtherConstantExponents_Decline()
    {
        Expression sin = Expression.Call(Sin, X), cos = Expression.Call(Cos, X);
        foreach ((double a, double b) in new[] { (1.0, 2.0), (2.0, 3.0), (0.0, 2.0), (2.0, 2.0000000000000004), (-2.0, 2.0) })
        {
            Expression source = Expression.Add(Expression.Power(sin, K(a)), Expression.Power(cos, K(b)));
            Expression result = S(source);
            Assert.IsNotInstanceOfType<ConstantExpression>(result, $"{source} -> {result}");
            AssertSameValues(source, result);
        }

        foreach (Expression source in new Expression[] { Expression.Add(Expression.Power(X, K(1)), Square(Y)), Expression.Add(Square(X), Expression.Power(Y, K(3))), Expression.Add(Expression.Power(X, K(0)), Square(Y)) })
        {
            AssertSameValues(source, S(source));
        }
    }

    /// <summary>The identity needs one argument: <c>sin(x)^2 + cos(y)^2</c> is not <c>1</c>; two sines or two cosines neither.</summary>
    [TestMethod]
    public void OtherArgumentsOrFunctions_Decline()
    {
        Expression[] sources =
        [
            Expression.Add(Square(Expression.Call(Sin, X)), Square(Expression.Call(Cos, Y))),
            Expression.Add(Square(Expression.Call(Sin, X)), Square(Expression.Call(Sin, X))),
            Expression.Add(Square(Expression.Call(Cos, X)), Square(Expression.Call(Tan, X))),
            Expression.Add(Square(X), Square(Expression.Call(Cos, X))),
        ];

        foreach (Expression source in sources)
        {
            Expression result = S(source);
            Assert.IsNotInstanceOfType<ConstantExpression>(result, $"{source} -> {result}");
            AssertSameValues(source, result);
        }

        // The arguments are compared by the comparer, after simplification: x + 0 is x.
        Assert.AreEqual(1.0, (S(Expression.Add(Square(Expression.Call(Sin, Expression.Add(X, K(0)))), Square(Expression.Call(Cos, X)))) as ConstantExpression)?.Value);
    }

    /// <summary>An extension node never matches the shapes of the rule and is never read by it.</summary>
    [TestMethod]
    public void ExtensionOperands_Decline()
    {
        var exponent = new OpaqueDouble();
        var argument = new OpaqueDouble();
        Expression[] sources =
        [
            Expression.Add(Expression.Power(Expression.Call(Sin, X), exponent), Square(Expression.Call(Cos, X))),
            Expression.Add(Square(Expression.Call(Sin, argument)), Square(Expression.Call(Cos, argument))),
            Expression.Add(Square(argument), Square(Expression.Call(Cos, X))),
        ];

        foreach (Expression source in sources)
        {
            Expression result = S(source);
            Assert.IsNotInstanceOfType<ConstantExpression>(result, result.ToString());
        }
    }

    /// <summary>An extension node of type <see cref="double"/>, irreducible: the simplifier must keep it as an operand.</summary>
    private sealed class OpaqueDouble : Expression
    {
        /// <inheritdoc/>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <inheritdoc/>
        public override Type Type => typeof(double);
    }

    /// <summary>The number of calls of <see cref="Observed"/>.</summary>
    [ThreadStatic]
    private static int s_observed;

    /// <summary>A user function with an effect: it counts its calls.</summary>
    /// <param name="value">The value.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static double Observed(double value)
    {
        s_observed++;
        return value;
    }

    /// <summary>
    /// The arguments may run user code: the identity would drop both evaluations of the argument, so the effect gate keeps the
    /// sum, and the compiled result evaluates the argument as often as the source.
    /// </summary>
    [TestMethod]
    public void EffectfulArguments_KeepTheirEvaluations()
    {
        MethodInfo observed = typeof(ExpressionSimplifierTrigonometricRuleTests).GetMethod(nameof(Observed))!;
        Expression argument = Expression.Call(observed, X);
        Expression source = Expression.Add(Square(Expression.Call(Sin, argument)), Square(Expression.Call(Cos, argument)));
        Expression result = S(source);
        Func<double, double, double> f = Expression.Lambda<Func<double, double, double>>(source, X, Y).Compile();
        Func<double, double, double> g = Expression.Lambda<Func<double, double, double>>(result, X, Y).Compile();
        s_observed = 0;
        double a = f(0.5, 0);
        int sourceCalls = s_observed;
        s_observed = 0;
        double b = g(0.5, 0);
        Assert.AreEqual(sourceCalls, s_observed, result.ToString());
        Assert.AreEqual(a, b, 1e-12);
    }

    /// <summary>Positive control: the identities of the runtime methods of <see cref="double"/> keep applying.</summary>
    [TestMethod]
    public void Identities_StillApply()
    {
        Assert.AreEqual(1.0, (S(Expression.Add(Square(Expression.Call(Sin, X)), Square(Expression.Call(Cos, X)))) as ConstantExpression)?.Value);
        Assert.AreEqual(1.0, (S(Expression.Add(Square(Expression.Call(Cos, X)), Square(Expression.Call(Sin, X)))) as ConstantExpression)?.Value);
        Assert.AreSame(Tan, (S(Expression.Divide(Expression.Call(Sin, X), Expression.Call(Cos, X))) as MethodCallExpression)?.Method);
        Assert.AreSame(Sin, (S(Expression.Multiply(Expression.Call(Cos, X), Expression.Call(Tan, X))) as MethodCallExpression)?.Method);
        Assert.AreSame(Sin, (S(Expression.Multiply(Expression.Call(Tan, X), Expression.Call(Cos, X))) as MethodCallExpression)?.Method);
        Assert.AreSame(Cos, (S(Expression.Divide(Expression.Call(Sin, X), Expression.Call(Tan, X))) as MethodCallExpression)?.Method);
        Expression cot = S(Expression.Divide(Expression.Call(Cos, X), Expression.Call(Sin, X)));
        Assert.AreSame(Tan, ((cot as BinaryExpression)?.Right as MethodCallExpression)?.Method, cot.ToString());

        // The same rules on float: its own functions, never those of double.
        ParameterExpression f = Expression.Parameter(typeof(float), "f");
        MethodInfo sinF = typeof(float).GetMethod(nameof(float.Sin), [typeof(float)])!, cosF = typeof(float).GetMethod(nameof(float.Cos), [typeof(float)])!;
        Assert.AreSame(typeof(float).GetMethod(nameof(float.Tan), [typeof(float)]), (S(Expression.Divide(Expression.Call(sinF, f), Expression.Call(cosF, f))) as MethodCallExpression)?.Method);

        // Both orders and an argument that is itself an expression.
        Expression sum = Expression.Add(X, Y);
        Assert.AreEqual(1.0, (S(Expression.Add(Square(Expression.Call(Cos, sum)), Square(Expression.Call(Sin, sum)))) as ConstantExpression)?.Value);
    }
}
