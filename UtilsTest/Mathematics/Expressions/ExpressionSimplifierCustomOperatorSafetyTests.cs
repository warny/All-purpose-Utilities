using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Protects the S3 contract that algebraic simplification applies only to intrinsic operators and never
/// interprets a custom or lifted operator solely from its <see cref="ExpressionType"/> shape.
/// </summary>
[TestClass]
public class ExpressionSimplifierCustomOperatorSafetyTests
{
    private static readonly MethodInfo CustomPowerMethod = GetOperator(nameof(CustomPower));
    private static readonly MethodInfo CustomMultiplyMethod = GetOperator(nameof(CustomMultiply));
    private static readonly MethodInfo CustomAddMethod = GetOperator(nameof(CustomAdd));
    private static readonly MethodInfo CustomSubtractMethod = GetOperator(nameof(CustomSubtract));
    private static readonly MethodInfo CustomNegateMethod = GetOperator(nameof(CustomNegate));
    private static readonly MethodInfo CustomDecimalAddMethod = GetOperator(nameof(CustomDecimalAdd));

    /// <summary>
    /// Verifies that the trigonometric sum-of-squares identity ignores a custom power on its left side.
    /// </summary>
    [TestMethod]
    public void Simplify_TrigonometricIdentity_CustomLeftPower_DoesNotReturnOne()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression sin = Expression.Call(GetDoubleMethod(nameof(double.Sin), 1), x);
        Expression cos = Expression.Call(GetDoubleMethod(nameof(double.Cos), 1), x);
        Expression source = Expression.Add(CustomBinary(ExpressionType.Power, sin, Expression.Constant(2.0), CustomPowerMethod), Expression.Power(cos, Expression.Constant(2.0)));

        Expression simplified = new ExpressionSimplifier().Simplify(source);

        Assert.IsFalse(simplified is ConstantExpression { Value: double value } && value == 1.0);
        AssertBinarySemantics(source, simplified, x, [(-0.75, 0.0), (0.25, 0.0), (1.5, 0.0)]);
    }

    /// <summary>
    /// Verifies that the trigonometric sum-of-squares identity ignores a custom power on its right side.
    /// </summary>
    [TestMethod]
    public void Simplify_TrigonometricIdentity_CustomRightPower_DoesNotReturnOne()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression sin = Expression.Call(GetDoubleMethod(nameof(double.Sin), 1), x);
        Expression cos = Expression.Call(GetDoubleMethod(nameof(double.Cos), 1), x);
        Expression source = Expression.Add(Expression.Power(sin, Expression.Constant(2.0)), CustomBinary(ExpressionType.Power, cos, Expression.Constant(2.0), CustomPowerMethod));

        Expression simplified = new ExpressionSimplifier().Simplify(source);

        Assert.IsFalse(simplified is ConstantExpression { Value: double value } && value == 1.0);
        AssertBinarySemantics(source, simplified, x, [(-0.75, 0.0), (0.25, 0.0), (1.5, 0.0)]);
    }

    /// <summary>Verifies that a custom power is not combined with an intrinsic power sharing its base.</summary>
    [TestMethod]
    public void Simplify_Multiply_CustomPower_DoesNotCombineExponents()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression customPower = CustomBinary(ExpressionType.Power, x, Expression.Constant(2.0), CustomPowerMethod);
        Expression source = Expression.Multiply(customPower, Expression.Power(x, Expression.Constant(3.0)));

        Expression simplified = new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified, CustomPowerMethod));
        AssertBinarySemantics(source, simplified, x, [(0.5, 0.0), (2.0, 0.0), (3.0, 0.0)]);
    }

    /// <summary>Verifies that addition does not factor custom multiplication nodes.</summary>
    [TestMethod]
    public void Simplify_Add_CustomMultiplyTerms_DoesNotFactorThem()
    {
        AssertCustomMultiplicationIsNotFactored(ExpressionType.Add);
    }

    /// <summary>Verifies that subtraction does not factor custom multiplication nodes.</summary>
    [TestMethod]
    public void Simplify_Subtract_CustomMultiplyTerms_DoesNotFactorThem()
    {
        AssertCustomMultiplicationIsNotFactored(ExpressionType.Subtract);
    }

    /// <summary>Verifies that subtraction does not reassociate a custom addition on its right.</summary>
    [TestMethod]
    public void Simplify_Subtract_CustomRightAdd_DoesNotReassociate()
    {
        AssertCustomRightOperandIsNotReassociated(ExpressionType.Add, CustomAddMethod);
    }

    /// <summary>Verifies that subtraction does not reassociate a custom subtraction on its right.</summary>
    [TestMethod]
    public void Simplify_Subtract_CustomRightSubtract_DoesNotReassociate()
    {
        AssertCustomRightOperandIsNotReassociated(ExpressionType.Subtract, CustomSubtractMethod);
    }

    /// <summary>Verifies that multiplication does not rewrite a custom negation on its right.</summary>
    [TestMethod]
    public void Simplify_Multiply_CustomRightNegate_DoesNotRewriteNegation()
    {
        AssertCustomNegationIsNotRewritten(ExpressionType.Multiply, customOnLeft: false);
    }

    /// <summary>Verifies that multiplication does not rewrite a custom negation on its left.</summary>
    [TestMethod]
    public void Simplify_Multiply_CustomLeftNegate_DoesNotRewriteNegation()
    {
        AssertCustomNegationIsNotRewritten(ExpressionType.Multiply, customOnLeft: true);
    }

    /// <summary>Verifies that division does not rewrite a custom negation on its right.</summary>
    [TestMethod]
    public void Simplify_Divide_CustomRightNegate_DoesNotRewriteNegation()
    {
        AssertCustomNegationIsNotRewritten(ExpressionType.Divide, customOnLeft: false);
    }

    /// <summary>Verifies that division does not rewrite a custom negation on its left.</summary>
    [TestMethod]
    public void Simplify_Divide_CustomLeftNegate_DoesNotRewriteNegation()
    {
        AssertCustomNegationIsNotRewritten(ExpressionType.Divide, customOnLeft: true);
    }

    /// <summary>
    /// Verifies that a custom decimal operator with the predefined operator's signature is not classified
    /// as intrinsic decimal addition and simplified through the additive-zero rule.
    /// </summary>
    [TestMethod]
    public void Simplify_DecimalCustomAdd_WithPredefinedSignature_IsNotClassifiedAsIntrinsic()
    {
        ParameterExpression x = Expression.Parameter(typeof(decimal), "x");
        Expression source = CustomBinary(ExpressionType.Add, x, Expression.Constant(0m), CustomDecimalAddMethod);

        Expression simplified = new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified, CustomDecimalAddMethod));
        Func<decimal, decimal> sourceDelegate = Expression.Lambda<Func<decimal, decimal>>(source, x).Compile();
        Func<decimal, decimal> simplifiedDelegate = Expression.Lambda<Func<decimal, decimal>>(simplified, x).Compile();
        Assert.AreEqual(sourceDelegate(7m), simplifiedDelegate(7m));
        Assert.AreEqual(1007m, simplifiedDelegate(7m));
    }

    /// <summary>Asserts that custom multiplication nodes remain semantically intact under an outer add or subtract.</summary>
    /// <param name="outerNodeType">The intrinsic outer additive operation.</param>
    private static void AssertCustomMultiplicationIsNotFactored(ExpressionType outerNodeType)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression left = CustomBinary(ExpressionType.Multiply, Expression.Constant(2.0), x, CustomMultiplyMethod);
        Expression right = CustomBinary(ExpressionType.Multiply, Expression.Constant(3.0), x, CustomMultiplyMethod);
        Expression source = Expression.MakeBinary(outerNodeType, left, right);

        Expression simplified = new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified, CustomMultiplyMethod));
        AssertBinarySemantics(source, simplified, x, [(-2.0, 0.0), (0.0, 0.0), (4.0, 0.0)]);
    }

    /// <summary>Asserts that a custom right-side additive node remains intact under subtraction.</summary>
    /// <param name="customNodeType">The node type used by the custom operator.</param>
    /// <param name="customMethod">The deliberately nonstandard custom implementation.</param>
    private static void AssertCustomRightOperandIsNotReassociated(ExpressionType customNodeType, MethodInfo customMethod)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        Expression custom = CustomBinary(customNodeType, y, z, customMethod);
        Expression source = Expression.Subtract(x, custom);

        Expression simplified = new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified, customMethod));
        AssertTernarySemantics(source, simplified, x, y, z);
    }

    /// <summary>Asserts that a custom unary method remains attached on either side of multiply or divide.</summary>
    /// <param name="outerNodeType">The intrinsic outer multiply or divide operation.</param>
    /// <param name="customOnLeft">Whether the custom negate is the left operand.</param>
    private static void AssertCustomNegationIsNotRewritten(ExpressionType outerNodeType, bool customOnLeft)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        UnaryExpression customNegate = Expression.Negate(customOnLeft ? x : y, CustomNegateMethod);
        Expression source = customOnLeft
            ? Expression.MakeBinary(outerNodeType, customNegate, y)
            : Expression.MakeBinary(outerNodeType, x, customNegate);

        Expression simplified = new ExpressionSimplifier().Simplify(source);

        Assert.IsTrue(ContainsMethod(simplified, CustomNegateMethod));
        AssertBinarySemantics(source, simplified, x, [(2.0, 4.0), (-3.0, 2.0), (5.0, -2.0)], y);
    }

    /// <summary>Compiles two one- or two-parameter expressions and asserts identical representative results.</summary>
    /// <param name="source">The source expression.</param>
    /// <param name="simplified">The simplified expression.</param>
    /// <param name="x">The first parameter.</param>
    /// <param name="samples">The representative input pairs.</param>
    /// <param name="y">The optional second parameter.</param>
    private static void AssertBinarySemantics(Expression source, Expression simplified, ParameterExpression x, (double X, double Y)[] samples, ParameterExpression? y = null)
    {
        ParameterExpression second = y ?? Expression.Parameter(typeof(double), "unused");
        Func<double, double, double> sourceDelegate = Expression.Lambda<Func<double, double, double>>(source, x, second).Compile();
        Func<double, double, double> simplifiedDelegate = Expression.Lambda<Func<double, double, double>>(simplified, x, second).Compile();
        foreach ((double first, double secondValue) in samples)
        {
            Assert.AreEqual(sourceDelegate(first, secondValue), simplifiedDelegate(first, secondValue), 1e-10);
        }
    }

    /// <summary>Compiles two three-parameter expressions and asserts identical representative results.</summary>
    /// <param name="source">The source expression.</param>
    /// <param name="simplified">The simplified expression.</param>
    /// <param name="x">The first parameter.</param>
    /// <param name="y">The second parameter.</param>
    /// <param name="z">The third parameter.</param>
    private static void AssertTernarySemantics(Expression source, Expression simplified, ParameterExpression x, ParameterExpression y, ParameterExpression z)
    {
        Func<double, double, double, double> sourceDelegate = Expression.Lambda<Func<double, double, double, double>>(source, x, y, z).Compile();
        Func<double, double, double, double> simplifiedDelegate = Expression.Lambda<Func<double, double, double, double>>(simplified, x, y, z).Compile();
        foreach ((double first, double second, double third) in new[] { (2.0, 3.0, 4.0), (-1.0, 5.0, 2.0), (7.0, -2.0, 3.0) })
        {
            Assert.AreEqual(sourceDelegate(first, second, third), simplifiedDelegate(first, second, third), 1e-10);
        }
    }

    /// <summary>Creates a binary expression with an explicit custom operator implementation.</summary>
    /// <param name="nodeType">The binary expression node type.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="method">The custom operator method.</param>
    /// <returns>The custom-method binary expression.</returns>
    private static BinaryExpression CustomBinary(ExpressionType nodeType, Expression left, Expression right, MethodInfo method) =>
        Expression.MakeBinary(nodeType, left, right, false, method);

    /// <summary>Determines whether an expression tree retains a specified custom operator method.</summary>
    /// <param name="expression">The expression tree to inspect.</param>
    /// <param name="method">The method whose presence is required.</param>
    /// <returns><see langword="true"/> when the method remains attached to a node.</returns>
    private static bool ContainsMethod(Expression expression, MethodInfo method)
    {
        var visitor = new OperatorMethodVisitor(method);
        visitor.Visit(expression);
        return visitor.Found;
    }

    /// <summary>Gets a private custom operator method declared by this test class.</summary>
    /// <param name="name">The method name.</param>
    /// <returns>The reflected method.</returns>
    private static MethodInfo GetOperator(string name) =>
        typeof(ExpressionSimplifierCustomOperatorSafetyTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Gets a public static floating-point method by name and arity.</summary>
    /// <param name="name">The method name.</param>
    /// <param name="arity">The required parameter count.</param>
    /// <returns>The reflected floating-point method.</returns>
    private static MethodInfo GetDoubleMethod(string name, int arity) =>
        System.Array.Find(typeof(double).GetMethods(BindingFlags.Public | BindingFlags.Static), method => method.Name == name && method.GetParameters().Length == arity)!;

    /// <summary>Implements a deliberately nonstandard power operation for semantic discrimination.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>A value intentionally unlike exponentiation.</returns>
    private static double CustomPower(double left, double right) => left + right + 100.0;

    /// <summary>Implements a deliberately nonstandard multiplication operation for semantic discrimination.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>A value intentionally unlike multiplication.</returns>
    private static double CustomMultiply(double left, double right) => left + right + 200.0;

    /// <summary>Implements a deliberately nonstandard addition operation for semantic discrimination.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>A value intentionally unlike addition.</returns>
    private static double CustomAdd(double left, double right) => left - right + 300.0;

    /// <summary>Implements a deliberately nonstandard subtraction operation for semantic discrimination.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>A value intentionally unlike subtraction.</returns>
    private static double CustomSubtract(double left, double right) => left + right + 400.0;

    /// <summary>Implements a deliberately nonstandard negation operation for semantic discrimination.</summary>
    /// <param name="value">The operand.</param>
    /// <returns>A value intentionally unlike arithmetic negation.</returns>
    private static double CustomNegate(double value) => value + 500.0;

    /// <summary>Implements a custom decimal addition with the predefined operator's exact parameter signature.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>A value intentionally unlike decimal addition.</returns>
    private static decimal CustomDecimalAdd(decimal left, decimal right) => left - right + 1000m;

    /// <summary>Finds an explicit custom operator method attached to unary or binary nodes.</summary>
    private sealed class OperatorMethodVisitor(MethodInfo target) : ExpressionVisitor
    {
        /// <summary>Gets whether the requested operator method was found.</summary>
        public bool Found { get; private set; }

        /// <summary>Examines a binary node for the requested method.</summary>
        /// <param name="node">The binary node.</param>
        /// <returns>The visited expression.</returns>
        protected override Expression VisitBinary(BinaryExpression node)
        {
            Found |= node.Method == target;
            return base.VisitBinary(node);
        }

        /// <summary>Examines a unary node for the requested method.</summary>
        /// <param name="node">The unary node.</param>
        /// <returns>The visited expression.</returns>
        protected override Expression VisitUnary(UnaryExpression node)
        {
            Found |= node.Method == target;
            return base.VisitUnary(node);
        }
    }
}
