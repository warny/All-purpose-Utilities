using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using static UtilsTest.Expressions.OptimiserDifferential;
using static UtilsTest.Expressions.OptimiserProbe;

namespace UtilsTest.Expressions;

/// <summary>
/// Differential corpus of <see cref="ExpressionOptimiser"/> (audit A10-C, O1–O5): every rule is exercised on inputs where it
/// is valid and on inputs where a naive identity would change the value, the type, the evaluations, their order or the
/// exceptions. Each source tree and its optimized form are compiled and compared on representative inputs.
/// </summary>
[TestClass]
public class ExpressionOptimiserSemanticsTests
{
    /// <summary>An <see cref="int"/> parameter.</summary>
    private static readonly ParameterExpression I = Expression.Parameter(typeof(int), "i");

    /// <summary>A <see cref="double"/> parameter.</summary>
    private static readonly ParameterExpression D = Expression.Parameter(typeof(double), "d");

    /// <summary>A <see cref="float"/> parameter.</summary>
    private static readonly ParameterExpression F = Expression.Parameter(typeof(float), "f");

    /// <summary>A <see cref="decimal"/> parameter.</summary>
    private static readonly ParameterExpression M = Expression.Parameter(typeof(decimal), "m");

    /// <summary>A nullable <see cref="int"/> parameter.</summary>
    private static readonly ParameterExpression NI = Expression.Parameter(typeof(int?), "ni");

    /// <summary>A nullable <see cref="bool"/> parameter.</summary>
    private static readonly ParameterExpression NB = Expression.Parameter(typeof(bool?), "nb");

    /// <summary>A <see cref="bool"/> parameter.</summary>
    private static readonly ParameterExpression B = Expression.Parameter(typeof(bool), "b");

    /// <summary>A quiet NaN with a non-default payload.</summary>
    private static readonly double PayloadNaN = BitConverter.Int64BitsToDouble(0x7FF8_0000_0000_0123);

    /// <summary>Representative <see cref="double"/> inputs, special values included.</summary>
    private static readonly object?[][] DoubleInputs =
    [
        [0.0], [-0.0], [1.5], [-2.25], [double.NaN], [PayloadNaN], [double.PositiveInfinity], [double.NegativeInfinity],
        [double.Epsilon], [-double.Epsilon], [double.MaxValue], [double.MinValue],
    ];

    /// <summary>Representative <see cref="float"/> inputs, special values included.</summary>
    private static readonly object?[][] SingleInputs =
    [
        [0.0f], [-0.0f], [1.5f], [float.NaN], [float.PositiveInfinity], [float.NegativeInfinity], [float.Epsilon], [float.MaxValue],
    ];

    /// <summary>Representative <see cref="int"/> inputs, bounds included.</summary>
    private static readonly object?[][] IntInputs = [[0], [1], [-7], [int.MaxValue], [int.MinValue]];

    /// <summary>Representative nullable <see cref="int"/> inputs, <see langword="null"/> included.</summary>
    private static readonly object?[][] NullableIntInputs = [[null], [0], [5], [int.MinValue]];

    /// <summary>Representative <see cref="decimal"/> inputs, scales and bounds included.</summary>
    private static readonly object?[][] DecimalInputs =
    [
        [0m], [1.5m], [-2.50m], [10000000000000000000000000000m], [decimal.MaxValue], [decimal.MinValue], [0.0000000000000000000000000001m],
    ];

    /// <summary>Builds a typed constant.</summary>
    /// <param name="value">The value.</param>
    /// <param name="type">The constant type.</param>
    /// <returns>The constant.</returns>
    private static ConstantExpression C(object? value, Type type) => Expression.Constant(value, type);

    // ---------------------------------------------------------------- O1: multiplication by zero

    /// <summary>A counted call multiplied by zero is still evaluated (A10-C: <c>F() * 0 -&gt; 0</c> dropped the call).</summary>
    [TestMethod]
    public void O1_CallTimesZero_KeepsTheCall()
    {
        AssertEquivalent(Expression.Lambda<Func<int, int>>(Expression.Multiply(CountCall("f", I), C(0, typeof(int))), I), IntInputs);
        AssertEquivalent(Expression.Lambda<Func<int, int>>(Expression.Multiply(C(0, typeof(int)), CountCall("f", I)), I), IntInputs);
    }

    /// <summary>A mutation multiplied by zero still happens.</summary>
    [TestMethod]
    public void O1_MutationTimesZero_KeepsTheMutation()
    {
        ParameterExpression v = Expression.Variable(typeof(int), "v");
        Expression body = Expression.Block(
            [v],
            Expression.Assign(v, I),
            Expression.Add(Expression.Multiply(Expression.PreIncrementAssign(v), C(0, typeof(int))), v));
        AssertEquivalent(Expression.Lambda<Func<int, int>>(body, I), IntInputs);

        FieldInfo field = typeof(OptimiserProbe).GetField(nameof(MutableField))!;
        Expression write = Expression.Multiply(Expression.Assign(Expression.Field(null, field), I), C(0, typeof(int)));
        AssertEquivalent(Expression.Lambda<Func<int, int>>(write, I), IntInputs);
    }

    /// <summary>A throwing call, a throwing division and a throwing allocation multiplied by zero still throw.</summary>
    [TestMethod]
    public void O1_ThrowingOperandTimesZero_KeepsTheException()
    {
        AssertEquivalent(Expression.Lambda<Func<int, int>>(Expression.Multiply(Expression.Call(Method(nameof(Fail)), Expression.Constant("boom")), C(0, typeof(int))), I), IntInputs);
        AssertEquivalent(Expression.Lambda<Func<int, int>>(Expression.Multiply(Expression.Divide(C(10, typeof(int)), I), C(0, typeof(int))), I), IntInputs);
        AssertEquivalent(Expression.Lambda<Func<int, int>>(Expression.Multiply(C(0, typeof(int)), Expression.Modulo(C(10, typeof(int)), I)), I), IntInputs);
        Expression array = Expression.ArrayLength(Expression.NewArrayBounds(typeof(byte), Expression.Subtract(I, C(1, typeof(int)))));
        AssertEquivalent(Expression.Lambda<Func<int, int>>(Expression.Multiply(array, C(0, typeof(int))), I), IntInputs);
    }

    /// <summary>An observable allocation multiplied by zero still happens.</summary>
    [TestMethod]
    public void O1_AllocationTimesZero_KeepsTheAllocation()
    {
        Expression allocation = Expression.Field(Expression.New(typeof(ProbeAllocation)), nameof(ProbeAllocation.Value));
        AssertEquivalent(Expression.Lambda<Func<int>>(Expression.Multiply(allocation, C(0, typeof(int)))));
    }

    /// <summary>
    /// A plain read of a parameter or of mutable state multiplied by an integral zero is dropped (the rule is kept where it is
    /// exact: an integral product with zero is zero, and a read has no effect).
    /// </summary>
    [TestMethod]
    public void O1_InertIntegralOperandTimesZero_IsFolded()
    {
        LambdaExpression optimized = AssertEquivalent(Expression.Lambda<Func<int, int>>(Expression.Multiply(I, C(0, typeof(int))), I), IntInputs);
        Assert.AreEqual(0, ((ConstantExpression)optimized.Body).Value);

        FieldInfo field = typeof(OptimiserProbe).GetField(nameof(MutableField))!;
        optimized = AssertEquivalent(Expression.Lambda<Func<int>>(Expression.Multiply(C(0, typeof(int)), Expression.Field(null, field))));
        Assert.AreEqual(0, ((ConstantExpression)optimized.Body).Value);

        ParameterExpression l = Expression.Parameter(typeof(long), "l");
        optimized = AssertEquivalent(Expression.Lambda<Func<long, long>>(Expression.Multiply(Expression.Add(l, C(3L, typeof(long))), C(0L, typeof(long))), l), [long.MaxValue], [-1L]);
        Assert.AreEqual(0L, ((ConstantExpression)optimized.Body).Value);
    }

    /// <summary>A custom operator method multiplying by zero is still called.</summary>
    [TestMethod]
    public void O1_CustomOperatorTimesZero_KeepsTheOperator()
    {
        Expression product = Expression.Multiply(I, C(0, typeof(int)), Method(nameof(MultiplyCounted)));
        LambdaExpression optimized = AssertEquivalent(Expression.Lambda<Func<int, int>>(product, I), IntInputs);
        Assert.AreSame(Method(nameof(MultiplyCounted)), ((BinaryExpression)optimized.Body).Method);
    }

    /// <summary>A custom operator method on an enumeration multiplying by zero neither throws while optimizing nor is dropped.</summary>
    [TestMethod]
    public void O1_CustomEnumerationOperatorTimesZero_DoesNotThrow()
    {
        ParameterExpression level = Expression.Parameter(typeof(ProbeLevel), "level");
        Expression product = Expression.Multiply(level, C(ProbeLevel.Zero, typeof(ProbeLevel)), Method(nameof(MultiplyLevels)));
        AssertEquivalent(Expression.Lambda<Func<ProbeLevel, ProbeLevel>>(product, level), [ProbeLevel.One], [ProbeLevel.Three]);
    }

    /// <summary>A lifted product with zero is <see langword="null"/> for a <see langword="null"/> operand, never zero.</summary>
    [TestMethod]
    public void O1_NullableTimesZero_KeepsNull()
    {
        AssertEquivalent(Expression.Lambda<Func<int?, int?>>(Expression.Multiply(NI, C(0, typeof(int?))), NI), NullableIntInputs);
        AssertEquivalent(Expression.Lambda<Func<int?, int?>>(Expression.Multiply(C(0, typeof(int?)), NI), NI), NullableIntInputs);
    }

    /// <summary>A floating-point product with zero keeps NaN, infinities and the sign of zero.</summary>
    [TestMethod]
    public void O1_FloatingTimesZero_KeepsIeeeSemantics()
    {
        AssertEquivalent(Expression.Lambda<Func<double, double>>(Expression.Multiply(D, C(0.0, typeof(double))), D), DoubleInputs);
        AssertEquivalent(Expression.Lambda<Func<double, double>>(Expression.Multiply(C(0.0, typeof(double)), D), D), DoubleInputs);
        AssertEquivalent(Expression.Lambda<Func<double, double>>(Expression.Multiply(D, C(-0.0, typeof(double))), D), DoubleInputs);
        AssertEquivalent(Expression.Lambda<Func<float, float>>(Expression.Multiply(F, C(0.0f, typeof(float))), F), SingleInputs);
    }

    /// <summary>A decimal product with zero keeps the overflow of its operand and the scale of the result.</summary>
    [TestMethod]
    public void O1_DecimalTimesZero_KeepsOverflowAndScale()
    {
        AssertEquivalent(Expression.Lambda<Func<decimal, decimal>>(Expression.Multiply(Expression.Multiply(M, M), C(0m, typeof(decimal))), M), DecimalInputs);
        AssertEquivalent(Expression.Lambda<Func<decimal, decimal>>(Expression.Multiply(M, C(0.00m, typeof(decimal))), M), DecimalInputs);
    }

    /// <summary>Two products with zero whose operands have distinct effects keep both effects, in order.</summary>
    [TestMethod]
    public void O1_DistinctEffectsOnBothSides_KeepTheirOrder()
    {
        Expression body = Expression.Add(
            Expression.Multiply(CountCall("left", I), C(0, typeof(int))),
            Expression.Multiply(C(0, typeof(int)), CountCall("right", I)));
        AssertEquivalent(Expression.Lambda<Func<int, int>>(body, I), IntInputs);
        AssertEquivalent(Expression.Lambda<Func<int, int>>(Expression.Multiply(CountCall("left", I), CountCall("right", C(0, typeof(int)))), I), IntInputs);
    }

    // ---------------------------------------------------------------- O2: arithmetic identities

    /// <summary>The integral identities <c>x + 0</c>, <c>0 + x</c>, <c>x - 0</c>, <c>x * 1</c>, <c>1 * x</c>, <c>x / 1</c> are applied and exact.</summary>
    [TestMethod]
    public void O2_IntegralIdentities_AreAppliedAndExact()
    {
        Func<Expression, Expression>[] identities =
        [
            x => Expression.Add(x, C(0, typeof(int))),
            x => Expression.Add(C(0, typeof(int)), x),
            x => Expression.Subtract(x, C(0, typeof(int))),
            x => Expression.Multiply(x, C(1, typeof(int))),
            x => Expression.Multiply(C(1, typeof(int)), x),
            x => Expression.Divide(x, C(1, typeof(int))),
        ];
        foreach (Func<Expression, Expression> identity in identities)
        {
            LambdaExpression optimized = AssertEquivalent(Expression.Lambda<Func<int, int>>(identity(CountCall("x", I)), I), IntInputs);
            Assert.AreEqual(ExpressionType.Call, optimized.Body.NodeType, identity(I).ToString());
        }
    }

    /// <summary>Other integral widths (<see cref="long"/>, <see cref="uint"/>, <see cref="short"/>) keep exact identities, bounds included.</summary>
    [TestMethod]
    public void O2_OtherIntegralWidths_AreExact()
    {
        ParameterExpression l = Expression.Parameter(typeof(long), "l");
        ParameterExpression u = Expression.Parameter(typeof(uint), "u");
        ParameterExpression s = Expression.Parameter(typeof(short), "s");
        Assert.AreSame(l, AssertEquivalent(Expression.Lambda<Func<long, long>>(Expression.Add(l, C(0L, typeof(long))), l), [long.MaxValue], [long.MinValue]).Body);
        Assert.AreSame(u, AssertEquivalent(Expression.Lambda<Func<uint, uint>>(Expression.Divide(u, C(1u, typeof(uint))), u), [uint.MaxValue], [0u]).Body);
        Assert.AreSame(s, AssertEquivalent(Expression.Lambda<Func<short, short>>(Expression.Multiply(C((short)1, typeof(short)), s), s), [short.MinValue], [(short)7]).Body);

        // Large constants that are neither zero nor one are never identities.
        AssertEquivalent(Expression.Lambda<Func<long, long>>(Expression.Multiply(l, C(9_007_199_254_740_993L, typeof(long))), l), [1L], [3L]);
        AssertEquivalent(Expression.Lambda<Func<long, long>>(Expression.Add(l, C(long.MaxValue, typeof(long))), l), [1L], [-1L]);
    }

    /// <summary>Checked arithmetic is never rewritten (its overflow exceptions are preserved as built).</summary>
    [TestMethod]
    public void O2_CheckedArithmetic_IsKept()
    {
        Expression checkedSum = Expression.AddChecked(Expression.AddChecked(I, C(1, typeof(int))), C(0, typeof(int)));
        AssertEquivalent(Expression.Lambda<Func<int, int>>(checkedSum, I), IntInputs);
        AssertEquivalent(Expression.Lambda<Func<int, int>>(Expression.MultiplyChecked(Expression.MultiplyChecked(I, C(2, typeof(int))), C(1, typeof(int))), I), IntInputs);
    }

    /// <summary>A custom operator method is never removed by an identity: <c>x + 0</c>, <c>x - 0</c>, <c>x * 1</c>, <c>x / 1</c> keep their call.</summary>
    [TestMethod]
    public void O2_CustomOperatorIdentities_KeepTheOperator()
    {
        (string Name, Func<Expression, Expression, MethodInfo, BinaryExpression> Build, int Identity)[] operators =
        [
            (nameof(AddCounted), Expression.Add, 0),
            (nameof(SubtractCounted), Expression.Subtract, 0),
            (nameof(MultiplyCounted), Expression.Multiply, 1),
            (nameof(DivideCounted), Expression.Divide, 1),
        ];
        foreach ((string name, Func<Expression, Expression, MethodInfo, BinaryExpression> build, int identity) in operators)
        {
            LambdaExpression optimized = AssertEquivalent(Expression.Lambda<Func<int, int>>(build(I, C(identity, typeof(int)), Method(name)), I), IntInputs);
            Assert.AreSame(Method(name), ((BinaryExpression)optimized.Body).Method, name);
        }
    }

    /// <summary>A lifted custom operator method keeps its call for a non-null operand and its <see langword="null"/> result otherwise.</summary>
    [TestMethod]
    public void O2_LiftedCustomOperator_IsKept()
    {
        Expression sum = Expression.Add(NI, C(0, typeof(int?)), Method(nameof(AddCounted)));
        AssertEquivalent(Expression.Lambda<Func<int?, int?>>(sum, NI), NullableIntInputs);
    }

    /// <summary>Lifted identities keep the nullable type and the <see langword="null"/> result, with one or both operands nullable.</summary>
    [TestMethod]
    public void O2_LiftedIdentities_KeepTheNullableType()
    {
        Assert.AreSame(NI, AssertEquivalent(Expression.Lambda<Func<int?, int?>>(Expression.Add(NI, C(0, typeof(int?))), NI), NullableIntInputs).Body);
        Assert.AreSame(NI, AssertEquivalent(Expression.Lambda<Func<int?, int?>>(Expression.Multiply(C(1, typeof(int?)), NI), NI), NullableIntInputs).Body);

        // A null constant is no identity element.
        AssertEquivalent(Expression.Lambda<Func<int?, int?>>(Expression.Add(NI, C(null, typeof(int?))), NI), NullableIntInputs);

        // A lifted operand meeting a non-nullable constant goes through a conversion, which keeps its own type.
        Expression converted = Expression.Add(NI, Expression.Convert(C(0, typeof(int)), typeof(int?)));
        AssertEquivalent(Expression.Lambda<Func<int?, int?>>(converted, NI), NullableIntInputs);

        ParameterExpression nd = Expression.Parameter(typeof(double?), "nd");
        AssertEquivalent(Expression.Lambda<Func<double?, double?>>(Expression.Add(nd, C(0.0, typeof(double?))), nd), [null], [-0.0], [1.0]);
        Assert.AreSame(nd, AssertEquivalent(Expression.Lambda<Func<double?, double?>>(Expression.Subtract(nd, C(0.0, typeof(double?))), nd), [null], [-0.0], [1.0]).Body);
    }

    /// <summary>
    /// Adding a positive zero is not an identity for <c>x = -0.0</c> (<c>-0.0 + 0.0 = +0.0</c>), nor is subtracting a negative
    /// zero; the sign-correct forms <c>x + (-0.0)</c> and <c>x - 0.0</c> are applied.
    /// </summary>
    [TestMethod]
    public void O2_FloatingAdditiveZero_RespectsTheSignOfZero()
    {
        AssertEquivalent(Expression.Lambda<Func<double, double>>(Expression.Add(D, C(0.0, typeof(double))), D), DoubleInputs);
        AssertEquivalent(Expression.Lambda<Func<double, double>>(Expression.Add(C(0.0, typeof(double)), D), D), DoubleInputs);
        AssertEquivalent(Expression.Lambda<Func<double, double>>(Expression.Subtract(D, C(-0.0, typeof(double))), D), DoubleInputs);
        AssertEquivalent(Expression.Lambda<Func<float, float>>(Expression.Add(F, C(0.0f, typeof(float))), F), SingleInputs);

        Assert.AreSame(D, AssertEquivalent(Expression.Lambda<Func<double, double>>(Expression.Add(D, C(-0.0, typeof(double))), D), DoubleInputs).Body);
        Assert.AreSame(D, AssertEquivalent(Expression.Lambda<Func<double, double>>(Expression.Add(C(-0.0, typeof(double)), D), D), DoubleInputs).Body);
        Assert.AreSame(D, AssertEquivalent(Expression.Lambda<Func<double, double>>(Expression.Subtract(D, C(0.0, typeof(double))), D), DoubleInputs).Body);
        Assert.AreSame(F, AssertEquivalent(Expression.Lambda<Func<float, float>>(Expression.Subtract(F, C(0.0f, typeof(float))), F), SingleInputs).Body);
    }

    /// <summary><c>x * 1</c>, <c>1 * x</c> and <c>x / 1</c> are exact for binary floating point, NaN payloads, infinities and subnormals included.</summary>
    [TestMethod]
    public void O2_FloatingMultiplicativeOne_IsExact()
    {
        Assert.AreSame(D, AssertEquivalent(Expression.Lambda<Func<double, double>>(Expression.Multiply(D, C(1.0, typeof(double))), D), DoubleInputs).Body);
        Assert.AreSame(D, AssertEquivalent(Expression.Lambda<Func<double, double>>(Expression.Multiply(C(1.0, typeof(double)), D), D), DoubleInputs).Body);
        Assert.AreSame(D, AssertEquivalent(Expression.Lambda<Func<double, double>>(Expression.Divide(D, C(1.0, typeof(double))), D), DoubleInputs).Body);
        Assert.AreSame(F, AssertEquivalent(Expression.Lambda<Func<float, float>>(Expression.Multiply(F, C(1.0f, typeof(float))), F), SingleInputs).Body);

        // Constants next to one are not one.
        AssertEquivalent(Expression.Lambda<Func<double, double>>(Expression.Multiply(D, C(double.BitIncrement(1.0), typeof(double))), D), DoubleInputs);
        AssertEquivalent(Expression.Lambda<Func<float, float>>(Expression.Multiply(F, C(float.BitDecrement(1.0f), typeof(float))), F), SingleInputs);
        AssertEquivalent(Expression.Lambda<Func<double, double>>(Expression.Add(D, C(double.Epsilon, typeof(double))), D), DoubleInputs);
    }

    /// <summary>
    /// Decimal identities are not applied: a constant slightly different from one is not one (it was rounded to <c>1.0</c>
    /// through <see cref="double"/>), and a zero or one with a scale changes the scale of the result.
    /// </summary>
    [TestMethod]
    public void O2_DecimalIdentities_KeepValueAndScale()
    {
        AssertEquivalent(Expression.Lambda<Func<decimal, decimal>>(Expression.Multiply(M, C(1.0000000000000000000000000001m, typeof(decimal))), M), DecimalInputs);
        AssertEquivalent(Expression.Lambda<Func<decimal, decimal>>(Expression.Multiply(M, C(1.0m, typeof(decimal))), M), DecimalInputs);
        AssertEquivalent(Expression.Lambda<Func<decimal, decimal>>(Expression.Add(M, C(0.00m, typeof(decimal))), M), DecimalInputs);
        AssertEquivalent(Expression.Lambda<Func<decimal, decimal>>(Expression.Divide(M, C(1.0m, typeof(decimal))), M), DecimalInputs);
        AssertEquivalent(Expression.Lambda<Func<decimal, decimal>>(Expression.Subtract(M, C(0m, typeof(decimal))), M), DecimalInputs);
    }

    // ---------------------------------------------------------------- O3: double negation and conversions

    /// <summary><c>-(-x)</c> is exact for primitive negation: signed minimum values, NaN payloads and signed zeros included.</summary>
    [TestMethod]
    public void O3_PrimitiveDoubleNegation_IsRemovedExactly()
    {
        Assert.AreSame(D, AssertEquivalent(Expression.Lambda<Func<double, double>>(Expression.Negate(Expression.Negate(D)), D), DoubleInputs).Body);
        Assert.AreSame(I, AssertEquivalent(Expression.Lambda<Func<int, int>>(Expression.Negate(Expression.Negate(I)), I), IntInputs).Body);
        Assert.AreSame(NI, AssertEquivalent(Expression.Lambda<Func<int?, int?>>(Expression.Negate(Expression.Negate(NI)), NI), NullableIntInputs).Body);
        Assert.AreSame(F, AssertEquivalent(Expression.Lambda<Func<float, float>>(Expression.Negate(Expression.Negate(F)), F), SingleInputs).Body);
    }

    /// <summary>A checked negation, outer or inner, is never removed: <c>-(-int.MinValue)</c> throws in checked context.</summary>
    [TestMethod]
    public void O3_CheckedNegation_IsKept()
    {
        AssertEquivalent(Expression.Lambda<Func<int, int>>(Expression.Negate(Expression.NegateChecked(I)), I), IntInputs);
        AssertEquivalent(Expression.Lambda<Func<int, int>>(Expression.NegateChecked(Expression.Negate(I)), I), IntInputs);
    }

    /// <summary>A double negation through custom operators keeps both operator calls.</summary>
    [TestMethod]
    public void O3_CustomDoubleNegation_KeepsTheOperators()
    {
        ParameterExpression money = Expression.Parameter(typeof(ProbeMoney), "money");
        AssertEquivalent(Expression.Lambda<Func<ProbeMoney, ProbeMoney>>(Expression.Negate(Expression.Negate(money)), money), [new ProbeMoney(3)]);

        MethodInfo negate = Method(nameof(NegateCounted));
        AssertEquivalent(Expression.Lambda<Func<int, int>>(Expression.Negate(Expression.Negate(I, negate), negate), I), IntInputs);
        AssertEquivalent(Expression.Lambda<Func<int, int>>(Expression.Negate(Expression.Negate(I), negate), I), IntInputs);
        AssertEquivalent(Expression.Lambda<Func<int, int>>(Expression.Negate(Expression.Negate(I, negate)), I), IntInputs);
    }

    /// <summary>A double negation whose inner node carries a conversion is not a double negation of the same value.</summary>
    [TestMethod]
    public void O3_NegationThroughAConversion_IsKept()
    {
        Expression body = Expression.Negate(Expression.Convert(Expression.Negate(I), typeof(double)));
        AssertEquivalent(Expression.Lambda<Func<int, double>>(body, I), IntInputs);
    }

    /// <summary>An identity conversion without method is removed; a conversion through a method is kept even when its types are equal.</summary>
    [TestMethod]
    public void O3_Conversions_KeepTheirMethod()
    {
        Assert.AreSame(I, AssertEquivalent(Expression.Lambda<Func<int, int>>(Expression.Convert(I, typeof(int)), I), IntInputs).Body);

        Expression counted = Expression.Convert(I, typeof(int), Method(nameof(IdentityCounted)));
        LambdaExpression optimized = AssertEquivalent(Expression.Lambda<Func<int, int>>(counted, I), IntInputs);
        Assert.AreSame(Method(nameof(IdentityCounted)), ((UnaryExpression)optimized.Body).Method);

        // Conversions that change the type, checked conversions and lifted conversions are kept.
        AssertEquivalent(Expression.Lambda<Func<double, int>>(Expression.ConvertChecked(D, typeof(int)), D), [1.5], [double.NaN], [1e20]);
        AssertEquivalent(Expression.Lambda<Func<int?, int>>(Expression.Convert(NI, typeof(int)), NI), NullableIntInputs);
        AssertEquivalent(Expression.Lambda<Func<int, object>>(Expression.Convert(I, typeof(object)), I), IntInputs);
    }

    // ---------------------------------------------------------------- O4: booleans and conditionals

    /// <summary>Short-circuit identities on <see cref="bool"/> keep the evaluated operand and its effects.</summary>
    [TestMethod]
    public void O4_BooleanShortCircuits_KeepEvaluations()
    {
        object?[][] bools = [[true], [false]];
        Assert.AreEqual(ExpressionType.Call, AssertEquivalent(Expression.Lambda<Func<bool, bool>>(Expression.AndAlso(C(true, typeof(bool)), CountBoolCall("r", B)), B), bools).Body.NodeType);
        Assert.AreEqual(ExpressionType.Constant, AssertEquivalent(Expression.Lambda<Func<bool, bool>>(Expression.AndAlso(C(false, typeof(bool)), CountBoolCall("r", B)), B), bools).Body.NodeType);
        Assert.AreEqual(ExpressionType.Call, AssertEquivalent(Expression.Lambda<Func<bool, bool>>(Expression.AndAlso(CountBoolCall("l", B), C(true, typeof(bool))), B), bools).Body.NodeType);
        AssertEquivalent(Expression.Lambda<Func<bool, bool>>(Expression.AndAlso(CountBoolCall("l", B), C(false, typeof(bool))), B), bools);
        Assert.AreEqual(ExpressionType.Constant, AssertEquivalent(Expression.Lambda<Func<bool, bool>>(Expression.OrElse(C(true, typeof(bool)), CountBoolCall("r", B)), B), bools).Body.NodeType);
        Assert.AreEqual(ExpressionType.Call, AssertEquivalent(Expression.Lambda<Func<bool, bool>>(Expression.OrElse(C(false, typeof(bool)), CountBoolCall("r", B)), B), bools).Body.NodeType);
        AssertEquivalent(Expression.Lambda<Func<bool, bool>>(Expression.OrElse(CountBoolCall("l", B), C(true, typeof(bool))), B), bools);
        Assert.AreEqual(ExpressionType.Call, AssertEquivalent(Expression.Lambda<Func<bool, bool>>(Expression.OrElse(CountBoolCall("l", B), C(false, typeof(bool))), B), bools).Body.NodeType);
    }

    /// <summary>Lifted short circuits on <see cref="Nullable{T}"/> of <see cref="bool"/> keep the nullable type and <see langword="null"/> results.</summary>
    [TestMethod]
    public void O4_NullableBooleanShortCircuits_KeepTheNullableType()
    {
        object?[][] inputs = [[true], [false], [null]];
        AssertEquivalent(Expression.Lambda<Func<bool?, bool?>>(Expression.AndAlso(C(false, typeof(bool?)), NB), NB), inputs);
        AssertEquivalent(Expression.Lambda<Func<bool?, bool?>>(Expression.OrElse(C(true, typeof(bool?)), NB), NB), inputs);
        Assert.AreSame(NB, AssertEquivalent(Expression.Lambda<Func<bool?, bool?>>(Expression.AndAlso(C(true, typeof(bool?)), NB), NB), inputs).Body);
        Assert.AreSame(NB, AssertEquivalent(Expression.Lambda<Func<bool?, bool?>>(Expression.OrElse(NB, C(false, typeof(bool?))), NB), inputs).Body);
        AssertEquivalent(Expression.Lambda<Func<bool?, bool?>>(Expression.AndAlso(C(null, typeof(bool?)), NB), NB), inputs);
        AssertEquivalent(Expression.Lambda<Func<bool?, bool?>>(Expression.AndAlso(NB, C(false, typeof(bool?))), NB), inputs);
    }

    /// <summary>A constant condition selects its branch only when the branch has exactly the type of the conditional.</summary>
    [TestMethod]
    public void O4_ConstantConditional_KeepsTheType()
    {
        Expression toVoid = Expression.Condition(C(true, typeof(bool)), CountCall("t", C(1, typeof(int))), CountCall("f", C(2, typeof(int))), typeof(void));
        AssertEquivalent(Expression.Lambda<Action>(toVoid));
        Assert.AreSame(typeof(void), new ExpressionOptimiser().Optimize(toVoid).Type);

        Expression toObject = Expression.Condition(C(false, typeof(bool)), C(null, typeof(object)), C("text", typeof(string)), typeof(object));
        AssertEquivalent(Expression.Lambda<Func<object>>(toObject));
        Assert.AreSame(typeof(object), new ExpressionOptimiser().Optimize(toObject).Type);

        LambdaExpression selected = AssertEquivalent(Expression.Lambda<Func<int>>(Expression.Condition(C(true, typeof(bool)), CountCall("t", C(1, typeof(int))), Expression.Call(Method(nameof(Fail)), Expression.Constant("f")))));
        Assert.AreEqual(ExpressionType.Call, selected.Body.NodeType);
    }

    /// <summary>
    /// A constant condition does not drop a branch defining a label that a jump outside the conditional targets: the source
    /// compiles and runs, and so must the optimized tree.
    /// </summary>
    [TestMethod]
    public void O4_ConstantConditional_KeepsALabelTargetedFromOutside()
    {
        LabelTarget target = Expression.Label("target");
        Expression body = Expression.Block(
            Expression.Goto(target),
            Expression.Condition(
                C(false, typeof(bool)),
                Expression.Block(typeof(void), Expression.Label(target), CountCall("after label", C(1, typeof(int)))),
                Expression.Block(typeof(void), CountCall("else", C(2, typeof(int)))),
                typeof(void)));
        AssertEquivalent(Expression.Lambda<Action>(body));

        // Without a label in the discarded branch the same conditional is folded to its selected branch.
        Expression foldable = Expression.Condition(
            C(false, typeof(bool)),
            Expression.Block(typeof(void), CountCall("then", C(1, typeof(int)))),
            Expression.Block(typeof(void), CountCall("else", C(2, typeof(int)))),
            typeof(void));
        Assert.AreEqual(ExpressionType.Block, AssertEquivalent(Expression.Lambda<Action>(foldable)).Body.NodeType);
    }

    // ---------------------------------------------------------------- O5: blocks

    /// <summary>The mandatory reproducer: <c>Block(Constant(7), Empty())</c> stays of type <see cref="void"/>.</summary>
    [TestMethod]
    public void O5_BlockEndingWithEmpty_StaysVoid()
    {
        Expression block = Expression.Block(C(7, typeof(int)), Expression.Empty());
        Expression optimized = new ExpressionOptimiser().Optimize(block);
        Assert.AreSame(typeof(void), optimized.Type);
        AssertEquivalent(Expression.Lambda<Action>(block));
    }

    /// <summary>Explicitly typed blocks keep their type, with or without variables.</summary>
    [TestMethod]
    public void O5_ExplicitlyTypedBlocks_KeepTheirType()
    {
        Expression typed = Expression.Block(typeof(object), C("s", typeof(string)));
        Assert.AreSame(typeof(object), new ExpressionOptimiser().Optimize(typed).Type);
        AssertEquivalent(Expression.Lambda<Func<object>>(typed));

        ParameterExpression v = Expression.Variable(typeof(string), "v");
        Expression withVariable = Expression.Block(typeof(object), [v], Expression.Empty(), Expression.Assign(v, C("s", typeof(string))), v);
        Assert.AreSame(typeof(object), new ExpressionOptimiser().Optimize(withVariable).Type);
        AssertEquivalent(Expression.Lambda<Func<object>>(withVariable));

        Expression typedVoid = Expression.Block(typeof(void), CountCall("x", C(1, typeof(int))));
        Assert.AreSame(typeof(void), new ExpressionOptimiser().Optimize(typedVoid).Type);
    }

    /// <summary>Blocks of every shape keep their type, their variables (by identity) and their behavior; inner empties are removed.</summary>
    [TestMethod]
    public void O5_BlockShapes_KeepTypeVariablesAndBehavior()
    {
        ParameterExpression v = Expression.Variable(typeof(int), "v");
        ParameterExpression w = Expression.Variable(typeof(int), "w");

        Expression empty = Expression.Block(typeof(void), System.Array.Empty<Expression>());
        Assert.AreSame(typeof(void), new ExpressionOptimiser().Optimize(empty).Type);

        Expression single = Expression.Block(CountCall("one", I));
        Assert.AreEqual(ExpressionType.Call, AssertEquivalent(Expression.Lambda<Func<int, int>>(single, I), IntInputs).Body.NodeType);

        Expression several = Expression.Block(Expression.Empty(), CountCall("a", I), Expression.Empty(), CountCall("b", I));
        LambdaExpression optimized = AssertEquivalent(Expression.Lambda<Func<int, int>>(several, I), IntInputs);
        Assert.AreEqual(2, ((BlockExpression)optimized.Body).Expressions.Count);

        Expression withVariables = Expression.Block([v], Expression.Empty(), Expression.Assign(v, Expression.Multiply(I, C(1, typeof(int)))), v);
        optimized = AssertEquivalent(Expression.Lambda<Func<int, int>>(withVariables, I), IntInputs);
        Assert.AreSame(v, ((BlockExpression)optimized.Body).Variables[0]);

        Expression nested = Expression.Block([v], Expression.Assign(v, I), Expression.Block([w], Expression.Assign(w, Expression.Add(v, C(0, typeof(int)))), Expression.Empty()), v);
        optimized = AssertEquivalent(Expression.Lambda<Func<int, int>>(nested, I), IntInputs);
        BlockExpression inner = (BlockExpression)((BlockExpression)optimized.Body).Expressions[1];
        Assert.AreSame(w, inner.Variables[0]);
        Assert.AreSame(typeof(void), inner.Type);

        Expression singleVariable = Expression.Block([v], Expression.Assign(v, I));
        Assert.AreSame(v, ((BlockExpression)AssertEquivalent(Expression.Lambda<Func<int, int>>(singleVariable, I), IntInputs).Body).Variables[0]);
    }

    /// <summary>Labels, jumps and exception handlers inside blocks keep their control flow.</summary>
    [TestMethod]
    public void O5_ControlFlowInBlocks_IsPreserved()
    {
        LabelTarget skip = Expression.Label("skip");
        LabelTarget result = Expression.Label(typeof(int), "result");
        Expression jumps = Expression.Block(
            Expression.Goto(skip),
            CountCall("skipped", I),
            Expression.Label(skip),
            Expression.Empty(),
            Expression.Return(result, Expression.Add(CountCall("returned", I), C(0, typeof(int)))),
            Expression.Label(result, C(-1, typeof(int))));
        AssertEquivalent(Expression.Lambda<Func<int, int>>(jumps, I), IntInputs);

        Expression handled = Expression.TryCatchFinally(
            Expression.Block(Expression.Empty(), Expression.Multiply(Expression.Divide(C(10, typeof(int)), I), C(1, typeof(int)))),
            Expression.Block(typeof(void), CountCall("finally", C(0, typeof(int))), Expression.Empty()),
            Expression.Catch(typeof(DivideByZeroException), Expression.Block(Expression.Empty(), CountCall("caught", C(-1, typeof(int))))));
        AssertEquivalent(Expression.Lambda<Func<int, int>>(handled, I), IntInputs);

        // A label alone in a block is not hoisted into the enclosing scope.
        LabelTarget twice = Expression.Label("twice");
        Expression scoped = Expression.Block(Expression.Block(Expression.Label(twice)), Expression.Block(Expression.Label(twice)), CountCall("end", I));
        AssertEquivalent(Expression.Lambda<Func<int, int>>(scoped, I), IntInputs);
    }

    // ---------------------------------------------------------------- Other families

    /// <summary>A quoted expression is data: its value (the quoted tree) is never rewritten.</summary>
    [TestMethod]
    public void QuotedExpression_IsNotRewritten()
    {
        LambdaExpression quoted = Expression.Lambda<Func<int, int>>(Expression.Multiply(I, C(1, typeof(int))), I);
        UnaryExpression quote = Expression.Quote(quoted);
        Expression optimized = new ExpressionOptimiser().Optimize(Expression.Lambda<Func<Expression<Func<int, int>>>>(quote));
        Assert.AreSame(quoted, ((UnaryExpression)((LambdaExpression)optimized).Body).Operand);
    }

    /// <summary>Rewrites reach inside lambdas, calls, member initializations and switches, and keep their structure.</summary>
    [TestMethod]
    public void NestedFamilies_AreRewrittenFaithfully()
    {
        Expression switched = Expression.Switch(
            typeof(int),
            I,
            Expression.Multiply(CountCall("default", I), C(1, typeof(int))),
            null,
            Expression.SwitchCase(Expression.Add(CountCall("one", I), C(0, typeof(int))), C(1, typeof(int))),
            Expression.SwitchCase(Expression.Block(Expression.Empty(), C(20, typeof(int))), C(2, typeof(int))));
        AssertEquivalent(Expression.Lambda<Func<int, int>>(switched, I), [1], [2], [3]);

        Expression init = Expression.Field(
            Expression.MemberInit(
                Expression.New(typeof(ProbeAllocation)),
                Expression.Bind(typeof(ProbeAllocation).GetField(nameof(ProbeAllocation.Value))!, Expression.Multiply(CountCall("value", I), C(1, typeof(int))))),
            nameof(ProbeAllocation.Value));
        LambdaExpression optimized = AssertEquivalent(Expression.Lambda<Func<int, int>>(init, I), IntInputs);
        var binding = (MemberAssignment)((MemberInitExpression)((MemberExpression)optimized.Body).Expression!).Bindings[0];
        Assert.AreEqual(ExpressionType.Call, binding.Expression.NodeType);

        MethodInfo add = typeof(System.Collections.Generic.List<int>).GetMethod(nameof(System.Collections.Generic.List<int>.Add))!;
        Expression list = Expression.Property(
            Expression.ListInit(Expression.New(typeof(System.Collections.Generic.List<int>)), Expression.ElementInit(add, Expression.Add(CountCall("element", I), C(0, typeof(int))))),
            nameof(System.Collections.Generic.List<int>.Count));
        optimized = AssertEquivalent(Expression.Lambda<Func<int, int>>(list, I), IntInputs);
        ElementInit initializer = ((ListInitExpression)((MemberExpression)optimized.Body).Expression!).Initializers[0];
        Assert.AreSame(add, initializer.AddMethod);
        Assert.AreEqual(ExpressionType.Call, initializer.Arguments[0].NodeType);

        ParameterExpression inner = Expression.Parameter(typeof(int), "inner");
        Expression invoke = Expression.Invoke(Expression.Lambda<Func<int, int>>(Expression.Multiply(inner, C(1, typeof(int))), inner), CountCall("arg", I));
        AssertEquivalent(Expression.Lambda<Func<int, int>>(invoke, I), IntInputs);
    }

    /// <summary>The optimiser is deterministic and stateless: the same tree optimized twice gives the same result, also from several threads.</summary>
    [TestMethod]
    public void Optimize_IsStatelessAcrossCallsAndThreads()
    {
        var optimiser = new ExpressionOptimiser();
        Expression<Func<int, int>> source = x => (x * 1 + 0) * (x - 0);
        string expected = optimiser.Optimize(source).ToString();
        System.Threading.Tasks.Parallel.For(0, 200, _ => Assert.AreEqual(expected, optimiser.Optimize(source).ToString()));
        Assert.AreEqual("x => (x * x)", expected);
    }
}
