using System.Globalization;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions.VBSyntax.Runtime;

namespace UtilsTest.Expressions;

/// <summary>
/// VBSyntax numeric promotion: binary arithmetic and comparisons promote both operands, symmetrically, to the operand
/// type of higher rank in Byte &lt; Int16 &lt; Int32 &lt; Int64 &lt; Single &lt; Double, and <c>/</c> is a real division
/// (Double). Before this change the right operand was converted to the left operand's type (<c>10 - x</c> with a Double
/// <c>x</c> truncated <c>x</c> to Int32), <c>/</c> on integers was an integer division and mixed-type comparisons threw.
/// </summary>
[TestClass]
public class VBSyntaxNumericPromotionTests
{
    /// <summary>The ranked types, in promotion order.</summary>
    private static readonly Type[] Ranked = [typeof(byte), typeof(short), typeof(int), typeof(long), typeof(float), typeof(double)];

    /// <summary>The operators of the matrix with the value of <c>7 op 2</c>.</summary>
    private static readonly (string Op, double Value)[] Operators = [("+", 9), ("-", 5), ("*", 14), ("/", 3.5), ("Mod", 1)];

    /// <summary>Every ordered pair of ranked types with every arithmetic operator (6 × 6 × 5 cases).</summary>
    /// <returns>Operator, left type and right type.</returns>
    public static IEnumerable<object[]> PromotionMatrix()
    {
        foreach (var (op, _) in Operators)
            foreach (Type left in Ranked)
                foreach (Type right in Ranked)
                    yield return [op, left, right];
    }

    /// <summary>
    /// Every ordered pair promotes to the type of higher rank (Double for <c>/</c>), converts both operands to it (no
    /// conversion of an operand already of that type, never a narrowing of the right operand) and computes the exact value.
    /// </summary>
    /// <param name="op">The operator.</param>
    /// <param name="leftType">The left operand type.</param>
    /// <param name="rightType">The right operand type.</param>
    [TestMethod]
    [DynamicData(nameof(PromotionMatrix))]
    public void Matrix_PromotesSymmetricallyToTheHigherRank(string op, Type leftType, Type rightType)
    {
        Type promoted = Ranked[Math.Max(System.Array.IndexOf(Ranked, leftType), System.Array.IndexOf(Ranked, rightType))];
        Type expectedType = op == "/" ? typeof(double) : promoted == typeof(byte) ? typeof(int) : promoted;
        double expectedValue = Operators.Single(o => o.Op == op).Value;

        (Expression body, object? value) = Evaluate($"a {op} b", ("a", leftType, Convert.ChangeType(7, leftType, CultureInfo.InvariantCulture)), ("b", rightType, Convert.ChangeType(2, rightType, CultureInfo.InvariantCulture)));

        Assert.AreEqual(expectedType, body.Type, $"{leftType.Name} {op} {rightType.Name}");
        Assert.AreEqual(expectedValue, Convert.ToDouble(value, CultureInfo.InvariantCulture), $"{leftType.Name} {op} {rightType.Name}");
        var binary = (BinaryExpression)body;
        Assert.AreEqual(expectedType, binary.Left.Type, "left operand converted to the common type");
        Assert.AreEqual(expectedType, binary.Right.Type, "right operand converted to the common type");
        Assert.IsTrue(leftType != expectedType || binary.Left is ParameterExpression, "no redundant conversion of the left operand");
        Assert.IsTrue(rightType != expectedType || binary.Right is ParameterExpression, "no redundant conversion of the right operand");
    }

    /// <summary>The cases of the defect report, with literals and typed variables, keep the associativity of #713.</summary>
    [TestMethod]
    public void RegressionCases_UseThePromotedTypeAndKeepLeftAssociativity()
    {
        AssertValue(7.5, "10 - x", ("x", typeof(double), 2.5));
        AssertValue(-7.5, "x - 10", ("x", typeof(double), 2.5));
        AssertValue(12.5, "10 + x", ("x", typeof(double), 2.5));
        AssertValue(25d, "10 * x", ("x", typeof(double), 2.5));
        AssertValue(0.75, "3 / 4");
        AssertValue(5d, "10 / 2");
        AssertValue(3.5, "x / 2", ("x", typeof(int), 7));
        AssertValue(3.5, "x / 2", ("x", typeof(float), 7f));
        AssertValue(3.5, "x / 2", ("x", typeof(double), 7d));
        AssertValue(-8.5, "x - 10 - 1", ("x", typeof(double), 2.5));
        AssertValue(3.75, "x * 2 / 4 * 3", ("x", typeof(double), 2.5));
        AssertValue(2.625, "(x + 1) * (x - 1) / 2", ("x", typeof(double), -2.5));
        AssertValue(1.0, "10 / 2 / 5");
        AssertValue(-8L, "x - 10 - 1", ("x", typeof(long), 3L));
    }

    /// <summary>The compiled tree of a promoted chain is still the left fold, with the promotion applied per operation.</summary>
    [TestMethod]
    public void PromotedChain_IsStillALeftFold()
    {
        (Expression body, _) = Evaluate("10 - x - 1", ("x", typeof(double), 2.5));
        var outer = (BinaryExpression)body;
        Assert.AreEqual(ExpressionType.Subtract, outer.NodeType);
        var inner = (BinaryExpression)outer.Left;
        Assert.AreEqual(ExpressionType.Subtract, inner.NodeType, "(10 - x) is the left operand");
        Assert.AreEqual(ExpressionType.Convert, inner.Left.NodeType, "the Int32 literal is widened to Double");
        Assert.IsInstanceOfType<ParameterExpression>(inner.Right, "the Double operand is not converted");
    }

    /// <summary>Integral boundaries wrap like every unchecked integral operation of the compiler; widening keeps values; Byte arithmetic is computed and returned in Int32.</summary>
    [TestMethod]
    public void Boundaries_IntegralOverflowWrapsAndWideningKeepsValues()
    {
        AssertValue(256, "a + b", "Byte + Byte is an Int32, not wrapped modulo 256", ("a", typeof(byte), byte.MaxValue), ("b", typeof(byte), (byte)1));
        AssertValue(-1, "a - b", "Byte - Byte is an Int32", ("a", typeof(byte), byte.MinValue), ("b", typeof(byte), (byte)1));
        AssertValue(65025, "a * b", "Byte * Byte is an Int32", ("a", typeof(byte), byte.MaxValue), ("b", typeof(byte), byte.MaxValue));
        AssertValue((short)255, "a + b", "Byte + Int16 stays Int16", ("a", typeof(byte), byte.MaxValue), ("b", typeof(short), (short)0));
        AssertValue((short)(short.MinValue), "a + b", ("a", typeof(short), short.MaxValue), ("b", typeof(short), (short)1));
        AssertValue(32768, "a + b", ("a", typeof(short), short.MaxValue), ("b", typeof(int), 1), "Int16 + Int32 is computed in Int32");
        AssertValue(int.MinValue, "a + b", ("a", typeof(int), int.MaxValue), ("b", typeof(int), 1));
        AssertValue(2147483648L, "a + b", ("a", typeof(int), int.MaxValue), ("b", typeof(long), 1L), "Int32 + Int64 is computed in Int64");
        AssertValue(-2147483646L, "a - b", ("a", typeof(long), 1L), ("b", typeof(int), int.MaxValue));
        AssertValue(long.MinValue, "a + b", ("a", typeof(long), long.MaxValue), ("b", typeof(long), 1L));
        AssertValue(255, "a * b", ("a", typeof(byte), byte.MaxValue), ("b", typeof(int), 1));
        AssertValue(-32768L, "a * b", ("a", typeof(short), short.MinValue), ("b", typeof(long), 1L));
    }

    /// <summary>Promotion to Single or Double is not lossless for large integers; the documented precision loss applies.</summary>
    [TestMethod]
    public void Boundaries_FloatingPointPromotionMayLoseIntegerPrecision()
    {
        AssertValue(16777216f, "a + b", ("a", typeof(int), 16777217), ("b", typeof(float), 0f), "Int32 to Single rounds above 2^24");
        AssertValue(9007199254740992d, "a + b", ("a", typeof(long), 9007199254740993L), ("b", typeof(double), 0d), "Int64 to Double rounds above 2^53");
        AssertValue((float)long.MaxValue, "a * b", ("a", typeof(long), long.MaxValue), ("b", typeof(float), 1f));
        AssertValue(-9.223372036854775808E18, "a + b", ("a", typeof(long), long.MinValue), ("b", typeof(double), 0d));
        AssertValue(float.MaxValue * 2.0, "a * b", ("a", typeof(float), float.MaxValue), ("b", typeof(double), 2d), "Single * Double is computed in Double without Single overflow");
    }

    /// <summary>Division by zero and floating-point special values follow IEEE 754 for <c>/</c>; integral Mod by zero throws.</summary>
    [TestMethod]
    public void Boundaries_DivisionByZeroAndSpecialValues()
    {
        AssertValue(double.PositiveInfinity, "a / b", ("a", typeof(int), 1), ("b", typeof(int), 0));
        AssertValue(double.NegativeInfinity, "a / b", ("a", typeof(long), -1L), ("b", typeof(byte), (byte)0));
        AssertValue(double.NaN, "a / b", ("a", typeof(short), (short)0), ("b", typeof(int), 0));
        AssertValue(double.NaN, "a Mod b", ("a", typeof(double), 5d), ("b", typeof(int), 0));
        AssertValue(double.NaN, "a + b", ("a", typeof(int), 1), ("b", typeof(double), double.NaN));
        AssertValue(double.PositiveInfinity, "a - b", ("a", typeof(long), 1L), ("b", typeof(double), double.NegativeInfinity));
        AssertValue(false, "a < b", ("a", typeof(int), 1), ("b", typeof(double), double.NaN));
        AssertValue(true, "a < b", ("a", typeof(long), long.MaxValue), ("b", typeof(double), double.PositiveInfinity));
        Assert.ThrowsExactly<DivideByZeroException>(() => Evaluate("a Mod b", ("a", typeof(int), 5), ("b", typeof(short), (short)0)));
    }

    /// <summary>Mod keeps the sign of the dividend and works in floating point after promotion.</summary>
    [TestMethod]
    public void Mod_NegativeAndFloatingPointOperands()
    {
        AssertValue(-1, "a Mod b", ("a", typeof(int), -7), ("b", typeof(int), 3));
        AssertValue(1L, "a Mod b", ("a", typeof(int), 7), ("b", typeof(long), -3L));
        AssertValue(-2d, "a Mod b", ("a", typeof(int), -7), ("b", typeof(double), 2.5));
        AssertValue(1.5f, "a Mod b", ("a", typeof(float), 7.5f), ("b", typeof(short), (short)2));
        AssertValue(1, "a Mod b", ("a", typeof(byte), (byte)7), ("b", typeof(byte), (byte)3));
    }

    /// <summary>
    /// Every comparison operator promotes mixed numeric operands (it threw before); the compared operands have the common
    /// type. Constants are used for <c>=</c> because <c>=</c> with an assignable left operand is an assignment.
    /// </summary>
    [TestMethod]
    public void Comparisons_PromoteMixedNumericOperands()
    {
        (string Op, bool Expected)[] cases = [("=", false), ("<>", true), ("<", true), ("<=", true), (">", false), (">=", false)];
        (Type Left, object LeftValue, Type Right, object RightValue)[] pairs =
        [
            (typeof(int), 2, typeof(double), 2.5),
            (typeof(double), 2.0, typeof(long), 3L),
            (typeof(float), 2f, typeof(double), 2.5),
            (typeof(byte), (byte)2, typeof(int), 300),
            (typeof(short), (short)-3, typeof(long), 4L),
        ];
        foreach (var (left, leftValue, right, rightValue) in pairs)
        {
            foreach (var (op, expected) in cases)
            {
                var symbols = new Dictionary<string, Expression>(StringComparer.OrdinalIgnoreCase)
                {
                    ["a"] = Expression.Constant(leftValue, left),
                    ["b"] = Expression.Constant(rightValue, right),
                };
                Expression body = new VBSyntaxExpressionCompiler().CompileExpression($"a {op} b", symbols);
                var comparison = (BinaryExpression)body;
                Type common = Ranked[Math.Max(System.Array.IndexOf(Ranked, left), System.Array.IndexOf(Ranked, right))];
                Assert.AreEqual(common, comparison.Left.Type, $"{left.Name} {op} {right.Name}");
                Assert.AreEqual(common, comparison.Right.Type, $"{left.Name} {op} {right.Name}");
                Assert.AreEqual(expected, Expression.Lambda<Func<bool>>(body).Compile()(), $"{left.Name} {op} {right.Name}");
            }
        }

        Assert.IsTrue(Expression.Lambda<Func<bool>>(new VBSyntaxExpressionCompiler().CompileExpression("2 = 2.0")).Compile()());
        Assert.IsFalse(Expression.Lambda<Func<bool>>(new VBSyntaxExpressionCompiler().CompileExpression("2 = 2.5")).Compile()(), "the right operand is not truncated to the left type");
        Assert.IsTrue(Expression.Lambda<Func<bool>>(new VBSyntaxExpressionCompiler().CompileExpression("1 < 1.5")).Compile()());
    }

    /// <summary>Comparisons of non-numeric operands are not promoted.</summary>
    [TestMethod]
    public void Comparisons_OfNonNumericOperandsAreUnchanged()
    {
        var compiler = new VBSyntaxExpressionCompiler();
        Assert.IsTrue(Expression.Lambda<Func<bool>>(compiler.CompileExpression("\"a\" <> \"b\"")).Compile()());
        Assert.IsTrue(Expression.Lambda<Func<bool>>(compiler.CompileExpression("True = True")).Compile()());
        Assert.IsTrue(Expression.Lambda<Func<bool>>(compiler.CompileExpression("Nothing = Nothing")).Compile()());
        var symbols = new Dictionary<string, Expression>(StringComparer.OrdinalIgnoreCase) { ["o"] = Expression.Constant(new object()), ["n"] = Expression.Constant(1) };
        Assert.ThrowsExactly<InvalidOperationException>(() => compiler.CompileExpression("o < n", symbols), "objects are not promoted to numbers");
    }

    /// <summary>
    /// A nullable operand lifts the whole operation to the nullable common type, whichever side it is on: nulls propagate
    /// and nullability is never discarded (before, a nullable right operand was converted to the left non-nullable type).
    /// </summary>
    [TestMethod]
    public void Nullable_LiftsToTheNullableCommonType()
    {
        AssertValue((double?)5.5, "a + b", ("a", typeof(int), 3), ("b", typeof(double?), (double?)2.5));
        AssertValue((double?)null, "a + b", ("a", typeof(int), 3), ("b", typeof(double?), null));
        AssertValue((long?)null, "a * b", ("a", typeof(int?), null), ("b", typeof(long), 2L));
        AssertValue((double?)3.5, "a / b", ("a", typeof(int?), (int?)7), ("b", typeof(int), 2));
        AssertValue((int?)260, "a + b", ("a", typeof(byte?), (byte?)250), ("b", typeof(byte), (byte)10));
        AssertValue((int?)null, "a + b", ("a", typeof(byte), (byte)250), ("b", typeof(byte?), null));
        AssertValue(true, "a < b", ("a", typeof(int?), (int?)3), ("b", typeof(double), 4d));
        AssertValue(false, "a < b", ("a", typeof(int?), null), ("b", typeof(double), 4d));
        (Expression body, _) = Evaluate("a + b", ("a", typeof(int), 3), ("b", typeof(double?), (double?)1));
        Assert.AreEqual(typeof(double?), body.Type);
    }

    /// <summary>
    /// Comparisons with nullable operands follow the lifted LINQ comparison semantics, operator by operator, and return
    /// a non-nullable Boolean: <c>=</c> is true when both operands are null and false when only one is, <c>&lt;&gt;</c>
    /// is its negation (so a null operand compared with a value is different), and <c>&lt;</c>, <c>&lt;=</c>,
    /// <c>&gt;</c>, <c>&gt;=</c> are false as soon as one operand is null (including null &lt;= null). Constants are used
    /// because <c>=</c> with an assignable left operand is an assignment.
    /// </summary>
    [TestMethod]
    public void Nullable_ComparisonsFollowLiftedSemanticsPerOperator()
    {
        var cases = new (string Op, int? Left, double? Right, bool Expected)[]
        {
            ("=", null, 5d, false), ("=", 5, null, false), ("=", null, null, true), ("=", 5, 5d, true),
            ("<>", null, 5d, true), ("<>", 5, null, true), ("<>", null, null, false), ("<>", 5, 5d, false),
            ("<", null, 5d, false), ("<", 5, null, false), ("<", null, null, false), ("<", 4, 5d, true),
            ("<=", null, 5d, false), ("<=", 5, null, false), ("<=", null, null, false), ("<=", 5, 5d, true),
            (">", null, 5d, false), (">", 5, null, false), (">", null, null, false), (">", 6, 5d, true),
            (">=", null, 5d, false), (">=", 5, null, false), (">=", null, null, false), (">=", 5, 5d, true),
        };

        foreach (var (op, left, right, expected) in cases)
        {
            string label = $"a {op} b with a = {left?.ToString() ?? "null"} (Int32?), b = {right?.ToString(CultureInfo.InvariantCulture) ?? "null"} (Double?)";
            AssertNullableComparison(expected, op, Expression.Constant(left, typeof(int?)), Expression.Constant(right, typeof(double?)), label);
        }

        // Nullable on one side only: the other operand is lifted, and null compared with a value is still different.
        AssertNullableComparison(true, "<>", Expression.Constant(5), Expression.Constant(null, typeof(double?)), "5 <> null");
        AssertNullableComparison(false, "=", Expression.Constant(5), Expression.Constant(null, typeof(double?)), "5 = null");
        AssertNullableComparison(false, ">=", Expression.Constant(null, typeof(long?)), Expression.Constant((short)1), "null >= 1");
    }

    /// <summary>
    /// Compiles <c>a op b</c> over constant operands and checks that the comparison works on the nullable common type and
    /// returns the expected non-nullable Boolean.
    /// </summary>
    /// <param name="expected">The expected result.</param>
    /// <param name="op">The comparison operator.</param>
    /// <param name="left">The left constant.</param>
    /// <param name="right">The right constant.</param>
    /// <param name="label">The assertion label.</param>
    private static void AssertNullableComparison(bool expected, string op, Expression left, Expression right, string label)
    {
        var symbols = new Dictionary<string, Expression>(StringComparer.OrdinalIgnoreCase) { ["a"] = left, ["b"] = right };
        Expression body = new VBSyntaxExpressionCompiler().CompileExpression($"a {op} b", symbols);
        var comparison = (BinaryExpression)body;
        Assert.AreEqual(typeof(bool), body.Type, label);
        Assert.IsTrue(Nullable.GetUnderlyingType(comparison.Left.Type) is not null, label);
        Assert.AreEqual(comparison.Left.Type, comparison.Right.Type, label);
        Assert.AreEqual(expected, Expression.Lambda<Func<bool>>(body).Compile()(), label);
    }

    /// <summary>
    /// SByte, UInt16, UInt32, UInt64 and Decimal are outside the promotion hierarchy: operations on one same type keep
    /// their previous behavior, mixed combinations are refused explicitly instead of being converted silently.
    /// </summary>
    [TestMethod]
    public void UnrankedNumericTypes_SameTypeKeptMixedRefused()
    {
        AssertValue(5m, "a + b", ("a", typeof(decimal), 3m), ("b", typeof(decimal), 2m));
        AssertValue(3.5m, "a / b", ("a", typeof(decimal), 7m), ("b", typeof(decimal), 2m));
        AssertValue(3u, "a / b", ("a", typeof(uint), 7u), ("b", typeof(uint), 2u), "same-type unsigned division keeps its integral semantics");
        AssertValue(5UL, "a + b", ("a", typeof(ulong), 3UL), ("b", typeof(ulong), 2UL));
        AssertValue((ushort)6, "a * b", ("a", typeof(ushort), (ushort)3), ("b", typeof(ushort), (ushort)2));
        AssertValue((decimal?)null, "a + b", ("a", typeof(decimal?), null), ("b", typeof(decimal), 2m));

        foreach (var (left, right) in new (Type, Type)[] { (typeof(decimal), typeof(int)), (typeof(int), typeof(decimal)), (typeof(uint), typeof(int)), (typeof(ulong), typeof(long)), (typeof(sbyte), typeof(int)), (typeof(decimal), typeof(double)), (typeof(ushort), typeof(uint)) })
        {
            object leftValue = Convert.ChangeType(3, left, CultureInfo.InvariantCulture);
            object rightValue = Convert.ChangeType(2, right, CultureInfo.InvariantCulture);
            var error = Assert.ThrowsExactly<NotSupportedException>(() => Evaluate("a + b", ("a", left, leftValue), ("b", right, rightValue)), $"{left.Name} + {right.Name}");
            StringAssert.Contains(error.Message, left.FullName!);
            StringAssert.Contains(error.Message, right.FullName!);
            Assert.ThrowsExactly<NotSupportedException>(() => Evaluate("a < b", ("a", left, leftValue), ("b", right, rightValue)), $"{left.Name} < {right.Name}");
        }

        Assert.ThrowsExactly<InvalidOperationException>(() => Evaluate("a + b", ("a", typeof(sbyte), (sbyte)1), ("b", typeof(sbyte), (sbyte)2)), "LINQ defines no SByte arithmetic (unchanged)");
    }

    /// <summary>Numeric literals: integers are Int32, a decimal point or an exponent gives Double, suffixes are ignored.</summary>
    [TestMethod]
    public void Literals_KeepTheirExistingTypes()
    {
        var compiler = new VBSyntaxExpressionCompiler();
        Assert.AreEqual(typeof(int), compiler.CompileExpression("42").Type);
        Assert.AreEqual(typeof(double), compiler.CompileExpression("4.2").Type);
        Assert.AreEqual(typeof(double), compiler.CompileExpression("4e2").Type);
        Assert.AreEqual(typeof(int), compiler.CompileExpression("42L").Type, "type suffixes are accepted but ignored");
        Assert.AreEqual(typeof(int), compiler.CompileExpression("-42").Type);
        Assert.ThrowsExactly<OverflowException>(() => compiler.CompileExpression("2147483648"), "integer literals outside Int32 are not widened");
        Assert.AreEqual(typeof(double), compiler.CompileExpression("1 + 2.5").Type);
    }

    /// <summary>Promotion only wraps operands in conversions: each is evaluated once, left before right.</summary>
    [TestMethod]
    public void Promotion_PreservesEvaluationOrderAndCount()
    {
        var log = new List<string>();
        var symbols = new Dictionary<string, Expression>(StringComparer.OrdinalIgnoreCase)
        {
            ["f"] = Expression.Invoke(Expression.Constant(new Func<int>(() => { log.Add("f"); return 10; }))),
            ["g"] = Expression.Invoke(Expression.Constant(new Func<double>(() => { log.Add("g"); return 2.5; }))),
            ["h"] = Expression.Invoke(Expression.Constant(new Func<long>(() => { log.Add("h"); return 1L; }))),
        };
        Expression body = new VBSyntaxExpressionCompiler().CompileExpression("f - g - h / f", symbols);
        double value = Expression.Lambda<Func<double>>(body).Compile()();

        Assert.AreEqual(10 - 2.5 - 1.0 / 10, value, 1e-12);
        CollectionAssert.AreEqual(new[] { "f", "g", "h", "f" }, log, "operands are evaluated once each, in source order");
    }

    /// <summary>Unary operators and shifts are not promoted; their existing behavior is characterized.</summary>
    [TestMethod]
    public void UnaryAndShiftOperators_AreUnchanged()
    {
        AssertValue(-3.5, "-x", ("x", typeof(double), 3.5));
        AssertValue((short)-3, "-x", ("x", typeof(short), (short)3));
        Assert.ThrowsExactly<InvalidOperationException>(() => Evaluate("-x", ("x", typeof(byte), (byte)3)), "LINQ defines no Byte negation (unchanged)");
        AssertValue(-6, "Not x", ("x", typeof(int), 5));
        AssertValue(false, "Not x", ("x", typeof(bool), true));
        AssertValue(16, "x << 4", ("x", typeof(int), 1));
        AssertValue(4L, "x << y", ("x", typeof(long), 1L), ("y", typeof(int), 2));
        Assert.ThrowsExactly<InvalidOperationException>(() => Evaluate("x << y", ("x", typeof(int), 1), ("y", typeof(long), 2L)), "the shift count must be Int32 (unchanged)");
        AssertValue(1024d, "x ^ 10", ("x", typeof(int), 2));
    }

    /// <summary>Compiles an expression over typed parameters and evaluates it.</summary>
    /// <param name="text">The expression.</param>
    /// <param name="variables">The parameters with their types and values.</param>
    /// <returns>The compiled body and its value.</returns>
    private static (Expression Body, object? Value) Evaluate(string text, params (string Name, Type Type, object? Value)[] variables)
    {
        ParameterExpression[] parameters = variables.Select(v => Expression.Parameter(v.Type, v.Name)).ToArray();
        var symbols = parameters.ToDictionary(p => p.Name!, p => (Expression)p, StringComparer.OrdinalIgnoreCase);
        Expression body = new VBSyntaxExpressionCompiler().CompileExpression(text, symbols);
        Delegate compiled = Expression.Lambda(body, parameters).Compile();
        try
        {
            return (body, compiled.DynamicInvoke(variables.Select(v => v.Value).ToArray()));
        }
        catch (System.Reflection.TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    /// <summary>Asserts the value and the exact type of an expression.</summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="expected">The expected value.</param>
    /// <param name="text">The expression.</param>
    /// <param name="variables">The parameters.</param>
    private static void AssertValue<T>(T expected, string text, params (string Name, Type Type, object? Value)[] variables)
        => AssertValue(expected, text, null, variables);

    /// <summary>Asserts the value and the exact type of an expression, with a message.</summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="expected">The expected value.</param>
    /// <param name="text">The expression.</param>
    /// <param name="message">The assertion message.</param>
    /// <param name="variables">The parameters.</param>
    private static void AssertValue<T>(T expected, string text, string? message, params (string Name, Type Type, object? Value)[] variables)
    {
        (Expression body, object? value) = Evaluate(text, variables);
        Assert.AreEqual(typeof(T), body.Type, $"type of {text} {message}");
        Assert.AreEqual(expected, (T?)value, $"value of {text} {message}");
    }

    /// <summary>Asserts the value and the exact type of an expression with two parameters and a message.</summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="expected">The expected value.</param>
    /// <param name="text">The expression.</param>
    /// <param name="first">The first parameter.</param>
    /// <param name="second">The second parameter.</param>
    /// <param name="message">The assertion message.</param>
    private static void AssertValue<T>(T expected, string text, (string Name, Type Type, object? Value) first, (string Name, Type Type, object? Value) second, string message)
        => AssertValue(expected, text, message, first, second);
}
