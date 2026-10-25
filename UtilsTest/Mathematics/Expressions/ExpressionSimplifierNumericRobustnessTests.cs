using System;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Regression coverage for roadmap stage S5 P11 (numeric robustness and NaN propagation, see
/// <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>). P11a: simplifying ordinary floating-point arithmetic
/// must not throw because a constant is <c>NaN</c>, infinite, beyond the decimal range or tiny, and must not mistake
/// such a constant for zero, one or minus one. P11b: an ordinary <see cref="double"/>/<see cref="float"/> addition,
/// subtraction, multiplication or division with a <c>NaN</c> constant operand folds to <c>NaN</c> only when the other
/// operand can be dropped without any observable effect.
/// </summary>
/// <remarks>
/// Every test goes through the public <c>Simplify</c> entry point. Results are checked by evaluating the compiled
/// source and simplified trees on the same inputs, so a rewrite is judged by what it computes, <c>NaN</c>-aware.
/// </remarks>
[TestClass]
public class ExpressionSimplifierNumericRobustnessTests
{
    // ------------------------------------------------------------------------------------------
    // Shared helpers
    // ------------------------------------------------------------------------------------------

    /// <summary>Inputs every unary <see cref="double"/> function is evaluated on, special values included.</summary>
    private static readonly double[] DoubleSamples = [1.5, -2.0, 0.0, -0.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue];

    /// <summary>Counts the calls of <see cref="CountingIdentity"/>.</summary>
    [ThreadStatic]
    private static int _countingIdentityCalls;

    /// <summary>A method with an observable side effect: it counts its calls.</summary>
    /// <param name="value">The value returned unchanged.</param>
    /// <returns><paramref name="value"/>.</returns>
    public static double CountingIdentity(double value)
    {
        _countingIdentityCalls++;
        return value;
    }

    /// <summary>A method that always throws.</summary>
    /// <param name="value">Ignored.</param>
    /// <returns>Never returns.</returns>
    public static double Throwing(double value) => throw new InvalidOperationException("observable failure");

    /// <summary>A user-defined addition, so a node built with it is never ordinary arithmetic.</summary>
    /// <param name="a">The left operand.</param>
    /// <param name="b">The right operand.</param>
    /// <returns>A finite value even for a <c>NaN</c> operand, so an erased custom operator is observable.</returns>
    public static double CustomAdd(double a, double b) => 42.0;

    /// <summary>A class whose property getter counts its calls.</summary>
    public sealed class ObservableHolder
    {
        /// <summary>The number of reads of <see cref="Value"/>.</summary>
        public int Reads { get; private set; }

        /// <summary>Gets 3.0 and records the read.</summary>
        public double Value
        {
            get
            {
                Reads++;
                return 3.0;
            }
        }
    }

    /// <summary>A constant payload whose equality members count their calls, to detect user code run during simplification.</summary>
    private sealed class CountingConstant
    {
        /// <summary>The number of <see cref="Equals(object)"/> and <see cref="GetHashCode"/> calls.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            Calls++;
            return ReferenceEquals(this, obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            Calls++;
            return 0;
        }
    }

    /// <summary>Reads its argument's payload as a <see cref="double"/>.</summary>
    /// <param name="value">Any object.</param>
    /// <returns>Always <c>1.0</c>.</returns>
    public static double FromObject(object value) => 1.0;

    /// <summary>A derived simplifier with no overrides, used to check that subclasses get the same rules.</summary>
    private sealed class MinimalDerivedSimplifier : ExpressionSimplifier
    {
    }

    /// <summary>Gets a public static method of this class by name.</summary>
    /// <param name="name">The method name.</param>
    /// <returns>The method.</returns>
    private static MethodInfo Method(string name) => typeof(ExpressionSimplifierNumericRobustnessTests).GetMethod(name)!;

    /// <summary>Simplifies <paramref name="expression"/> with a fresh built-in simplifier.</summary>
    /// <param name="expression">The source expression.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression Simplify(Expression expression) => new ExpressionSimplifier().Simplify(expression);

    /// <summary>Asserts that <paramref name="action"/> throws <typeparamref name="TException"/> from a rule, unwrapping the reflective invocation.</summary>
    /// <typeparam name="TException">The exception the rule throws.</typeparam>
    /// <param name="action">The simplification to run.</param>
    private static void AssertThrowsRuleException<TException>(Action action) where TException : Exception
    {
        Exception thrown = Assert.Throws<Exception>(action);
        while (thrown is TargetInvocationException { InnerException: not null } wrapper) thrown = wrapper.InnerException;
        Assert.IsInstanceOfType<TException>(thrown);
    }

    /// <summary>Formats a result so that every <c>NaN</c> compares equal and signed zeros differ.</summary>
    /// <param name="value">The value.</param>
    /// <returns>A comparison key.</returns>
    private static string Key(double value) => double.IsNaN(value) ? "NaN" : BitConverter.DoubleToInt64Bits(value).ToString("X16");

    /// <summary>Formats a <see cref="float"/> result like <see cref="Key(double)"/>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>A comparison key.</returns>
    private static string Key(float value) => float.IsNaN(value) ? "NaN" : BitConverter.SingleToInt32Bits(value).ToString("X8");

    /// <summary>
    /// Simplifies <paramref name="body"/>, asserting that it does not throw, and checks that the result computes
    /// the same values as the source on <see cref="DoubleSamples"/>.
    /// </summary>
    /// <param name="x">The only parameter of <paramref name="body"/>.</param>
    /// <param name="body">The source expression.</param>
    /// <returns>The simplified expression.</returns>
    private static Expression AssertSimplifiesEquivalently(ParameterExpression x, Expression body)
    {
        Expression simplified = Simplify(body);
        Func<double, double> source = Expression.Lambda<Func<double, double>>(body, x).Compile();
        Func<double, double> result = Expression.Lambda<Func<double, double>>(simplified, x).Compile();
        foreach (double sample in DoubleSamples)
        {
            Assert.AreEqual(Key(source(sample)), Key(result(sample)), $"{body} => {simplified} differs at x = {sample}.");
        }

        return simplified;
    }

    /// <summary>Builds the binary node named by <paramref name="operation"/>.</summary>
    /// <param name="operation">One of <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c> and <c>^</c>.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The node.</returns>
    private static BinaryExpression Binary(string operation, Expression left, Expression right) => operation switch
    {
        "+" => Expression.Add(left, right),
        "-" => Expression.Subtract(left, right),
        "*" => Expression.Multiply(left, right),
        "/" => Expression.Divide(left, right),
        "^" => Expression.Power(left, right),
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    /// <summary>Asserts that <paramref name="expression"/> is a <see cref="double"/> constant equal to <paramref name="expected"/>, <c>NaN</c>-aware.</summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="expression">The simplified expression.</param>
    private static void AssertDoubleConstant(double expected, Expression expression)
    {
        ConstantExpression constant = expression as ConstantExpression ?? throw new AssertFailedException($"Expected a constant, got {expression}.");
        Assert.AreEqual(typeof(double), constant.Type);
        Assert.AreEqual(Key(expected), Key((double)constant.Value!), $"Unexpected constant {expression}.");
    }

    // ------------------------------------------------------------------------------------------
    // P11a - no exception, no false identity
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Non-finite and out-of-decimal-range <see cref="double"/> constants, on either side of every ordinary binary
    /// operator, simplify without throwing and without changing what the expression computes. Before P11,
    /// <c>x + c</c>, <c>c + x</c>, <c>x / c</c> and <c>x ^ c</c> threw <see cref="OverflowException"/> from the decimal
    /// conversion in <c>NumberUtils.CompareNumeric</c>.
    /// </summary>
    /// <param name="value">The constant.</param>
    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    [DataRow(double.MaxValue)]
    [DataRow(-double.MaxValue)]
    [DataRow(1e30)]
    public void Simplify_DoubleConstantOutsideDecimalRange_DoesNotThrowAndPreservesValues(double value)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        foreach (string operation in new[] { "+", "-", "*", "/", "^" })
        {
            AssertSimplifiesEquivalently(x, Binary(operation, x, Expression.Constant(value)));
            AssertSimplifiesEquivalently(x, Binary(operation, Expression.Constant(value), x));
        }
    }

    /// <summary>The <see cref="float"/> counterpart of <see cref="Simplify_DoubleConstantOutsideDecimalRange_DoesNotThrowAndPreservesValues"/> (no power: it is only defined on <see cref="double"/>).</summary>
    /// <param name="value">The constant.</param>
    [TestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(float.MaxValue)]
    public void Simplify_FloatConstantOutsideDecimalRange_DoesNotThrowAndPreservesValues(float value)
    {
        ParameterExpression x = Expression.Parameter(typeof(float), "x");
        foreach (string operation in new[] { "+", "-", "*", "/" })
        {
            foreach (BinaryExpression body in new[] { Binary(operation, x, Expression.Constant(value)), Binary(operation, Expression.Constant(value), x) })
            {
                Expression simplified = Simplify(body);
                Func<float, float> source = Expression.Lambda<Func<float, float>>(body, x).Compile();
                Func<float, float> result = Expression.Lambda<Func<float, float>>(simplified, x).Compile();
                foreach (double sample in DoubleSamples)
                {
                    Assert.AreEqual(Key(source((float)sample)), Key(result((float)sample)), $"{body} => {simplified} differs at x = {sample}.");
                }
            }
        }
    }

    /// <summary>
    /// The factoring rule no longer throws on a non-finite constant term, including inside a longer sum and next to
    /// a factored term.
    /// </summary>
    [TestMethod]
    public void Simplify_NonFiniteTermInSum_DoesNotThrow()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        AssertSimplifiesEquivalently(x, Expression.Add(Expression.Add(x, Expression.Constant(double.PositiveInfinity)), Expression.Multiply(Expression.Constant(2.0), x)));
        AssertSimplifiesEquivalently(x, Expression.Add(Expression.Multiply(x, Expression.Constant(double.MaxValue)), x));
        AssertSimplifiesEquivalently(x, Expression.Subtract(Expression.Constant(double.NegativeInfinity), Expression.Multiply(Expression.Constant(3.0), x)));
    }

    /// <summary>
    /// <see cref="double.Epsilon"/> rounds to zero in <see cref="decimal"/>, so before P11 <c>x / Epsilon</c> threw
    /// <see cref="DivideByZeroException"/> and <c>x ^ Epsilon</c> became <c>1</c>. It is now compared exactly.
    /// </summary>
    [TestMethod]
    public void Simplify_TinyConstant_IsNotTakenForZero()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression division = AssertSimplifiesEquivalently(x, Expression.Divide(x, Expression.Constant(double.Epsilon)));
        Assert.AreEqual(ExpressionType.Divide, division.NodeType);
        Expression power = AssertSimplifiesEquivalently(x, Expression.Power(x, Expression.Constant(double.Epsilon)));
        Assert.AreEqual(ExpressionType.Power, power.NodeType);

        ParameterExpression xf = Expression.Parameter(typeof(float), "xf");
        Expression floatDivision = Simplify(Expression.Divide(xf, Expression.Constant(float.Epsilon)));
        Assert.AreEqual(ExpressionType.Divide, floatDivision.NodeType);
    }

    /// <summary>
    /// The zero/one identities keep their symbolic behavior on finite constants, signed zero included:
    /// <c>x * -0.0</c> is still <c>-0.0</c> and <c>x ^ -0.0</c> is still <c>1</c>.
    /// </summary>
    [TestMethod]
    public void Simplify_FiniteIdentities_AreUnchanged()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Assert.AreSame(x, Simplify(Expression.Add(x, Expression.Constant(-0.0))));
        Assert.AreSame(x, Simplify(Expression.Multiply(Expression.Constant(1.0), x)));
        Assert.AreSame(x, Simplify(Expression.Divide(x, Expression.Constant(1.0))));
        AssertDoubleConstant(-0.0, Simplify(Expression.Multiply(x, Expression.Constant(-0.0))));
        AssertDoubleConstant(1.0, Simplify(Expression.Power(x, Expression.Constant(-0.0))));
        Assert.AreEqual(ExpressionType.Negate, Simplify(Expression.Divide(x, Expression.Constant(-1.0))).NodeType);
    }

    /// <summary>
    /// A zero/one identity declines on a non-finite constant operand, so the exact constant folding applies:
    /// <c>inf * 0</c> is <c>NaN</c> (it was <c>0</c>), <c>0 ^ NaN</c> is <c>NaN</c> (it was <c>0</c>).
    /// </summary>
    /// <param name="left">The left constant.</param>
    /// <param name="operation">The operator.</param>
    /// <param name="right">The right constant.</param>
    /// <param name="expected">The IEEE-754 result.</param>
    [TestMethod]
    [DataRow(double.PositiveInfinity, "*", 0.0, double.NaN)]
    [DataRow(0.0, "*", double.NegativeInfinity, double.NaN)]
    [DataRow(double.NaN, "*", 0.0, double.NaN)]
    [DataRow(0.0, "*", double.NaN, double.NaN)]
    [DataRow(0.0, "/", double.PositiveInfinity, 0.0)]
    [DataRow(0.0, "^", double.NaN, double.NaN)]
    [DataRow(0.0, "^", double.PositiveInfinity, 0.0)]
    [DataRow(0.0, "^", double.NegativeInfinity, double.PositiveInfinity)]
    [DataRow(1.0, "^", double.NaN, 1.0)]
    [DataRow(double.NaN, "^", 0.0, 1.0)]
    [DataRow(double.PositiveInfinity, "^", -1.0, 0.0)]
    public void Simplify_ConstantsWithNonFiniteOperand_FoldExactly(double left, string operation, double right, double expected)
    {
        AssertDoubleConstant(expected, Simplify(Binary(operation, Expression.Constant(left), Expression.Constant(right))));
    }

    /// <summary>
    /// Characterizes the remaining zero-divisor behavior (a separate defect in the S5 P11 roadmap entry): division by
    /// a <see cref="double"/> zero still throws while simplifying, as do integral and decimal division by zero, which
    /// match their runtime behavior. A <c>NaN</c> dividend is folded by P11b before that rule is reached.
    /// </summary>
    [TestMethod]
    public void Simplify_ZeroDivisor_KeepsHistoricalBehavior()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        ParameterExpression m = Expression.Parameter(typeof(decimal), "m");
        AssertThrowsRuleException<DivideByZeroException>(() => Simplify(Expression.Divide(x, Expression.Constant(-0.0))));
        AssertThrowsRuleException<DivideByZeroException>(() => Simplify(Expression.Divide(i, Expression.Constant(0))));
        AssertThrowsRuleException<DivideByZeroException>(() => Simplify(Expression.Divide(m, Expression.Constant(0m))));
        AssertDoubleConstant(double.NaN, Simplify(Expression.Divide(Expression.Constant(double.NaN), Expression.Constant(0.0))));
    }

    /// <summary>Large integral and decimal constants keep their exact comparison: none is taken for zero, one or minus one.</summary>
    [TestMethod]
    public void Simplify_LargeIntegralAndDecimalConstants_AreComparedExactly()
    {
        ParameterExpression u = Expression.Parameter(typeof(ulong), "u");
        ParameterExpression l = Expression.Parameter(typeof(long), "l");
        ParameterExpression m = Expression.Parameter(typeof(decimal), "m");
        Assert.AreEqual(ExpressionType.Multiply, Simplify(Expression.Multiply(u, Expression.Constant(ulong.MaxValue))).NodeType);
        Assert.AreEqual(ExpressionType.Divide, Simplify(Expression.Divide(u, Expression.Constant(ulong.MaxValue))).NodeType);
        Assert.AreSame(u, Simplify(Expression.Multiply(u, Expression.Constant(1UL))));
        Assert.AreEqual(ExpressionType.Negate, Simplify(Expression.Multiply(l, Expression.Constant(-1L))).NodeType);
        Assert.AreEqual(ExpressionType.Divide, Simplify(Expression.Divide(m, Expression.Constant(1.0000000000000000000000000001m))).NodeType);
        Assert.AreSame(m, Simplify(Expression.Divide(m, Expression.Constant(1.000m))));
        Assert.AreEqual(ExpressionType.Add, Simplify(Expression.Add(m, Expression.Constant(decimal.MaxValue))).NodeType);
    }

    /// <summary>
    /// A numeric constant that is not <see cref="IConvertible"/> (<see cref="BigInteger"/>) no longer makes
    /// <see cref="ConstantNumericAttribute"/> throw <see cref="InvalidCastException"/>: it matches no listed value,
    /// so a custom-operator node holding it is left as is.
    /// </summary>
    [TestMethod]
    public void Simplify_NonConvertibleNumericConstant_DoesNotThrow()
    {
        ParameterExpression big = Expression.Parameter(typeof(BigInteger), "big");
        BinaryExpression sum = Expression.Add(big, Expression.Constant(BigInteger.Zero));
        BinaryExpression product = Expression.Multiply(big, Expression.Constant(BigInteger.One));
        Assert.AreEqual(ExpressionType.Add, Simplify(sum).NodeType);
        Assert.AreEqual(ExpressionType.Multiply, Simplify(product).NodeType);

        Assert.IsFalse(new ConstantNumericAttribute(0).Match(Expression.Constant(BigInteger.Zero)));
        Assert.IsTrue(new ConstantNumericAttribute().Match(Expression.Constant(BigInteger.Zero)));
        Assert.IsTrue(new ConstantNumericAttribute(0).Match(Expression.Constant(0.0)));
        Assert.IsFalse(new ConstantNumericAttribute(0).Match(Expression.Constant(double.NaN)));
    }

    // ------------------------------------------------------------------------------------------
    // P11b - NaN propagation
    // ------------------------------------------------------------------------------------------

    /// <summary>Each of the four operators, with <c>NaN</c> on either side of a parameter, folds to a <c>NaN</c> constant of the node's type.</summary>
    /// <param name="operation">The operator.</param>
    [TestMethod]
    [DataRow("+")]
    [DataRow("-")]
    [DataRow("*")]
    [DataRow("/")]
    public void Simplify_NaNWithParameter_FoldsToNaN(string operation)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ConstantExpression nan = Expression.Constant(double.NaN);
        AssertDoubleConstant(double.NaN, AssertSimplifiesEquivalently(x, Binary(operation, x, nan)));
        AssertDoubleConstant(double.NaN, AssertSimplifiesEquivalently(x, Binary(operation, nan, x)));

        ParameterExpression xf = Expression.Parameter(typeof(float), "xf");
        ConstantExpression nanf = Expression.Constant(float.NaN);
        foreach (Expression folded in new[] { Simplify(Binary(operation, xf, nanf)), Simplify(Binary(operation, nanf, xf)) })
        {
            ConstantExpression constant = folded as ConstantExpression ?? throw new AssertFailedException($"Expected a constant, got {folded}.");
            Assert.AreEqual(typeof(float), constant.Type, "A float expression must stay a float constant.");
            Assert.IsTrue(float.IsNaN((float)constant.Value!));
        }
    }

    /// <summary>Any <c>NaN</c> payload is recognized, and the folded constant keeps the operand's payload bits.</summary>
    [TestMethod]
    public void Simplify_NaNPayload_IsKept()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        long bits = unchecked((long)0xFFF8000000000001UL);
        Expression folded = Simplify(Expression.Multiply(x, Expression.Constant(BitConverter.Int64BitsToDouble(bits))));
        ConstantExpression constant = folded as ConstantExpression ?? throw new AssertFailedException($"Expected a constant, got {folded}.");
        Assert.AreEqual(bits, BitConverter.DoubleToInt64Bits((double)constant.Value!));
    }

    /// <summary>A <c>NaN</c> next to a pure, total arithmetic sub-tree, or nested in it, folds the whole expression.</summary>
    [TestMethod]
    public void Simplify_NaNWithPureArithmeticTree_FoldsToNaN()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        Expression pure = Expression.Subtract(Expression.Multiply(x, y), Expression.Divide(Expression.Negate(x), y));
        Expression folded = Simplify(Expression.Add(pure, Expression.Constant(double.NaN)));
        AssertDoubleConstant(double.NaN, folded);

        Expression nested = Expression.Multiply(Expression.Add(x, Expression.Divide(y, Expression.Constant(double.NaN))), Expression.Subtract(x, y));
        AssertDoubleConstant(double.NaN, Simplify(nested));
    }

    /// <summary>A <c>NaN</c> in a lambda body folds the body; the lambda and its parameters are kept.</summary>
    [TestMethod]
    public void Simplify_NaNInLambda_FoldsBodyOnly()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression<Func<double, double>> lambda = Expression.Lambda<Func<double, double>>(Expression.Add(x, Expression.Constant(double.NaN)), x);
        var simplified = (LambdaExpression)Simplify(lambda);
        Assert.AreEqual(1, simplified.Parameters.Count);
        AssertDoubleConstant(double.NaN, simplified.Body);
    }

    /// <summary>A subclass of the simplifier gets the same folding.</summary>
    [TestMethod]
    public void Simplify_DerivedSimplifier_FoldsNaN()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        AssertDoubleConstant(double.NaN, new MinimalDerivedSimplifier().Simplify(Expression.Subtract(x, Expression.Constant(double.NaN))));
    }

    /// <summary>A method call with a side effect is never dropped: the compiled result still runs it once.</summary>
    [TestMethod]
    public void Simplify_NaNWithCountingCall_KeepsTheCall()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression body = Expression.Add(Expression.Call(Method(nameof(CountingIdentity)), x), Expression.Constant(double.NaN));
        Expression simplified = Simplify(body);
        Func<double, double> compiled = Expression.Lambda<Func<double, double>>(simplified, x).Compile();
        _countingIdentityCalls = 0;
        Assert.IsTrue(double.IsNaN(compiled(1.0)));
        Assert.AreEqual(1, _countingIdentityCalls);
    }

    /// <summary>A throwing call, a counting property getter and a custom operator are all kept.</summary>
    [TestMethod]
    public void Simplify_NaNWithObservableOperands_KeepsThem()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ConstantExpression nan = Expression.Constant(double.NaN);

        Expression throwing = Simplify(Expression.Multiply(nan, Expression.Call(Method(nameof(Throwing)), x)));
        Func<double, double> throwingCompiled = Expression.Lambda<Func<double, double>>(throwing, x).Compile();
        Assert.ThrowsExactly<InvalidOperationException>(() => throwingCompiled(1.0));

        var holder = new ObservableHolder();
        Expression property = Simplify(Expression.Divide(Expression.Property(Expression.Constant(holder), nameof(ObservableHolder.Value)), nan));
        Func<double, double> propertyCompiled = Expression.Lambda<Func<double, double>>(property, x).Compile();
        Assert.IsTrue(double.IsNaN(propertyCompiled(1.0)));
        Assert.AreEqual(1, holder.Reads);

        BinaryExpression custom = Expression.Add(x, nan, Method(nameof(CustomAdd)));
        Expression customSimplified = Simplify(custom);
        Assert.AreEqual(42.0, Expression.Lambda<Func<double, double>>(customSimplified, x).Compile()(1.0));
    }

    /// <summary>
    /// An integral division (which can throw) behind a conversion, a checked operation and a checked conversion are
    /// kept, so the compiled result still throws where the source throws.
    /// </summary>
    [TestMethod]
    public void Simplify_NaNWithThrowingOrCheckedOperands_KeepsThem()
    {
        ParameterExpression i = Expression.Parameter(typeof(int), "i");
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ConstantExpression nan = Expression.Constant(double.NaN);

        Expression integerDivision = Expression.Convert(Expression.Divide(Expression.Constant(10), i), typeof(double));
        Expression simplified = Simplify(Expression.Add(integerDivision, nan));
        Func<int, double> compiled = Expression.Lambda<Func<int, double>>(simplified, i).Compile();
        Assert.ThrowsExactly<DivideByZeroException>(() => compiled(0));
        Assert.IsTrue(double.IsNaN(compiled(2)));

        Expression checkedSum = Simplify(Expression.Add(Expression.AddChecked(x, x), nan));
        Assert.AreEqual(ExpressionType.Add, checkedSum.NodeType);

        Expression checkedConversion = Expression.ConvertChecked(Expression.Multiply(i, Expression.Constant(int.MaxValue)), typeof(double));
        Expression conversionSimplified = Simplify(Expression.Subtract(checkedConversion, nan));
        Assert.AreNotEqual(ExpressionType.Constant, conversionSimplified.NodeType);
    }

    /// <summary>Deciding whether to fold runs no user code: a constant payload's equality members are never called.</summary>
    [TestMethod]
    public void Simplify_NaNDecision_RunsNoUserCode()
    {
        var payload = new CountingConstant();
        Expression call = Expression.Call(Method(nameof(FromObject)), Expression.Constant(payload, typeof(object)));
        Expression simplified = Simplify(Expression.Add(call, Expression.Constant(double.NaN)));
        Assert.AreEqual(ExpressionType.Add, simplified.NodeType);
        Assert.AreEqual(0, payload.Calls);
    }

    /// <summary>
    /// Power is not folded on a <c>NaN</c> operand with an unknown other operand: <c>Math.Pow(NaN, 0)</c> and
    /// <c>Math.Pow(1, NaN)</c> are <c>1</c>, so <c>NaN</c> is not inevitable.
    /// </summary>
    [TestMethod]
    public void Simplify_PowerWithNaN_IsNotFolded()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Assert.AreEqual(ExpressionType.Power, AssertSimplifiesEquivalently(x, Expression.Power(x, Expression.Constant(double.NaN))).NodeType);
        Assert.AreEqual(ExpressionType.Power, AssertSimplifiesEquivalently(x, Expression.Power(Expression.Constant(double.NaN), x)).NodeType);
    }

    /// <summary>An infinite constant is not propagated: <c>x + inf</c> is <c>NaN</c> for <c>x = -inf</c>.</summary>
    [TestMethod]
    public void Simplify_InfinityWithParameter_IsNotFolded()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression simplified = AssertSimplifiesEquivalently(x, Expression.Add(x, Expression.Constant(double.PositiveInfinity)));
        Assert.AreNotEqual(ExpressionType.Constant, simplified.NodeType);
    }
}
