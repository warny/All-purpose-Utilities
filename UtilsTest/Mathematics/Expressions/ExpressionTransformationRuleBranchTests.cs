using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Provides AST-specific, branch-level coverage for symbolic rules that cannot be represented faithfully in Gherkin.
/// </summary>
[TestClass]
public sealed class ExpressionTransformationRuleBranchTests
{
    /// <summary>Exposes protected simplifier rules that require direct body characterization.</summary>
    private sealed class ExposedSimplifier : ExpressionSimplifier
    {
        /// <summary>Invokes <c>PowerConvertionNumber1</c>.</summary>
        public Expression ConvertNumberPower(Expression source, Expression left, Expression right) => PowerConvertionNumber1(source, left, right);

        /// <summary>Invokes <c>PowerConvertionNumber2</c>.</summary>
        public Expression ConvertMathPower(Expression source, Expression left, Expression right) => PowerConvertionNumber2(source, left, right);

        /// <summary>Invokes <c>PowerConversionMath</c>.</summary>
        public Expression ConvertDoublePower(Expression source, Expression left, Expression right) => PowerConversionMath(source, left, right);

        /// <summary>Invokes <c>NegateWithSubstraction</c> without recursive operand preparation.</summary>
        public Expression RewriteNegatedSubtraction(UnaryExpression source, BinaryExpression operand) => NegateWithSubstraction(source, operand);

        /// <summary>Invokes <c>SubstractionWithSubstraction</c> without recursive right-operand preparation.</summary>
        public Expression RewriteNestedSubtraction(BinaryExpression source, Expression left, BinaryExpression right) => SubstractionWithSubstraction(source, left, right);
    }

    /// <summary>Verifies direct public dispatch to the derivation addition rule without pre-simplification.</summary>
    [TestMethod]
    public void Derivate_Transform_Add_DispatchesDedicatedRule()
    {
        AssertDirectDerivationRule(ExpressionType.Add, ExpressionType.Add, 1.0);
    }

    /// <summary>Verifies direct public dispatch to the derivation subtraction rule without pre-simplification.</summary>
    [TestMethod]
    public void Derivate_Transform_Subtract_DispatchesDedicatedRule()
    {
        AssertDirectDerivationRule(ExpressionType.Subtract, ExpressionType.Subtract, 1.0);
    }

    /// <summary>Verifies direct public dispatch to the derivation product rule without pre-simplification.</summary>
    [TestMethod]
    public void Derivate_Transform_Multiply_DispatchesDedicatedRule()
    {
        AssertDirectDerivationRule(ExpressionType.Multiply, ExpressionType.Add, 3.0);
    }

    /// <summary>Verifies successful same-type checked conversion differentiation.</summary>
    [TestMethod]
    public void Derivate_SameTypeCheckedConversion_PassesThrough()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        UnaryExpression conversion = Expression.ConvertChecked(x, typeof(double));
        var source = Expression.Lambda<Func<double, double>>(conversion, x);

        var result = (Expression<Func<double, double>>)new ExpressionDerivation<double>("x").Derivate(source);

        Assert.AreEqual(1.0, result.Compile()(4.0), 1e-9);
    }

    /// <summary>Verifies successful same-type checked conversion integration.</summary>
    [TestMethod]
    public void Integrate_SameTypeCheckedConversion_PassesThrough()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        UnaryExpression conversion = Expression.ConvertChecked(x, typeof(double));
        var source = Expression.Lambda<Func<double, double>>(conversion, x);

        var result = (Expression<Func<double, double>>)new ExpressionIntegration<double>("x").Integrate(source);

        Assert.AreEqual(8.0, result.Compile()(4.0), 1e-9);
    }

    /// <summary>Verifies the general constant-over-power branch for a constant exponent.</summary>
    [TestMethod]
    public void Integrate_Divide_ConstantByPower_GeneralExponent_ReturnsReciprocalPower()
    {
        AssertIntegral(
            x => Expression.Divide(Expression.Constant(3.0), Expression.Power(x, Expression.Constant(2.0))),
            value => -3.0 / value,
            [0.5, 1.0, 2.0, 4.0]);
    }

    /// <summary>Verifies converted exponent extraction in the constant-over-power rule.</summary>
    [TestMethod]
    public void Integrate_Divide_ConstantByPower_ConvertedExponent_ReturnsReciprocalPower()
    {
        AssertIntegral(
            x => Expression.Divide(
                Expression.Constant(3.0),
                Expression.Power(x, Expression.Convert(Expression.Constant(2), typeof(double)))),
            value => -3.0 / value,
            [0.5, 1.0, 2.0, 4.0]);
    }

    /// <summary>Verifies the converted-numerator overload delegates to the power denominator rule.</summary>
    [TestMethod]
    public void Integrate_Divide_ConvertedConstantByPower_DelegatesToPowerRule()
    {
        AssertIntegral(
            x => Expression.Divide(
                Expression.Convert(Expression.Constant(3), typeof(double)),
                Expression.Power(x, Expression.Constant(2.0))),
            value => -3.0 / value,
            [0.5, 1.0, 2.0, 4.0]);
    }

    /// <summary>Verifies the normal shifted-exponent branch for an actual Power node.</summary>
    [TestMethod]
    public void Integrate_Power_ConstantExponent_ShiftsExponent()
    {
        AssertIntegral(
            x => Expression.Power(x, Expression.Constant(2.0)),
            value => double.Pow(value, 3.0) / 3.0,
            [-2.0, -1.0, 0.0, 1.0, 2.0]);
    }

    /// <summary>Verifies converted exponent extraction for an actual Power node.</summary>
    [TestMethod]
    public void Integrate_Power_ConvertedExponent_ShiftsExponent()
    {
        AssertIntegral(
            x => Expression.Power(x, Expression.Convert(Expression.Constant(2), typeof(double))),
            value => double.Pow(value, 3.0) / 3.0,
            [-2.0, -1.0, 0.0, 1.0, 2.0]);
    }

    /// <summary>Verifies the minus-one branch after a converted exponent on an actual Power node.</summary>
    [TestMethod]
    public void Integrate_Power_ConvertedMinusOneExponent_ReturnsLogAbsoluteValue()
    {
        AssertIntegral(
            x => Expression.Power(x, Expression.Convert(Expression.Constant(-1), typeof(double))),
            value => double.Log(double.Abs(value)),
            [-2.0, -0.5, 0.5, 2.0]);
    }

    /// <summary>Verifies public dispatch to the normal constant-exponent Math.Pow integration branch.</summary>
    [TestMethod]
    public void Integrate_PowerMathCall_ConstantExponent_ShiftsExponent()
    {
        AssertIntegral(
            x => CreateMathPowCall(x, Expression.Constant(2.0)),
            value => double.Pow(value, 3.0) / 3.0,
            [-2.0, -1.0, 0.0, 1.0, 2.0]);
    }

    /// <summary>Verifies public dispatch to the minus-one constant-exponent Math.Pow integration branch.</summary>
    [TestMethod]
    public void Integrate_PowerMathCall_ConstantMinusOne_ReturnsLogAbsoluteValue()
    {
        AssertIntegral(
            x => CreateMathPowCall(x, Expression.Constant(-1.0)),
            value => double.Log(double.Abs(value)),
            [-2.0, -0.5, 0.5, 2.0]);
    }

    /// <summary>Verifies public dispatch to the normal converted-exponent Math.Pow integration branch.</summary>
    [TestMethod]
    public void Integrate_PowerMathCall_ConvertedExponent_ShiftsExponent()
    {
        AssertIntegral(
            x => CreateMathPowCall(x, Expression.Convert(Expression.Constant(2), typeof(double))),
            value => double.Pow(value, 3.0) / 3.0,
            [-2.0, -1.0, 0.0, 1.0, 2.0]);
    }

    /// <summary>Verifies public dispatch to the minus-one converted-exponent Math.Pow integration branch.</summary>
    [TestMethod]
    public void Integrate_PowerMathCall_ConvertedMinusOne_ReturnsLogAbsoluteValue()
    {
        AssertIntegral(
            x => CreateMathPowCall(x, Expression.Convert(Expression.Constant(-1), typeof(double))),
            value => double.Log(double.Abs(value)),
            [-2.0, -0.5, 0.5, 2.0]);
    }

    /// <summary>Verifies right-side negative-one multiplication uses an actual negative constant.</summary>
    [TestMethod]
    public void Simplify_MultiplicationWithZeroOrOne_RightMinusOne_ReturnsNegatedOperand()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        AssertStructural(Expression.Multiply(x, Expression.Constant(-1.0)), Expression.Negate(x));
    }

    /// <summary>Verifies left-side negative-one multiplication uses an actual negative constant.</summary>
    [TestMethod]
    public void Simplify_MultiplicationWithZeroOrOne_LeftMinusOne_ReturnsNegatedOperand()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        AssertStructural(Expression.Multiply(Expression.Constant(-1.0), x), Expression.Negate(x));
    }

    /// <summary>Verifies division by an actual zero constant fails with the documented exception.</summary>
    [TestMethod]
    public void Simplify_DivideWithZeroOrOne_ZeroDenominator_ThrowsDivideByZeroException()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ConstantExpression zero = Expression.Constant(0.0);
        BinaryExpression source = Expression.Divide(x, zero);
        Assert.ThrowsExactly<DivideByZeroException>(() => new ExpressionSimplifier().DivideWithZeroOrOne(source, x, zero));
    }

    /// <summary>Verifies division by an actual negative-one constant negates the numerator.</summary>
    [TestMethod]
    public void Simplify_DivideWithZeroOrOne_RightMinusOne_ReturnsNegatedOperand()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        AssertStructural(Expression.Divide(x, Expression.Constant(-1.0)), Expression.Negate(x));
    }

    /// <summary>Verifies an actual minus-one Power exponent becomes a reciprocal.</summary>
    [TestMethod]
    public void Simplify_PowerByZeroOrOne_MinusOne_ReturnsReciprocal()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        AssertStructural(
            Expression.Power(x, Expression.Constant(-1.0)),
            Expression.Divide(Expression.Constant(1.0), x));
    }

    /// <summary>Verifies a general negative Power exponent becomes a reciprocal positive power.</summary>
    [TestMethod]
    public void Simplify_PowerByZeroOrOne_GeneralNegativeExponent_ReturnsReciprocalPower()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression result = new ExpressionSimplifier().Simplify(Expression.Power(x, Expression.Constant(-2.0)));
        var division = (BinaryExpression)result;
        Assert.AreEqual(ExpressionType.Divide, division.NodeType);
        var denominator = (BinaryExpression)division.Right;
        Assert.AreEqual(ExpressionType.Power, denominator.NodeType);
        Assert.AreEqual(Expression.Constant(2.0), denominator.Right, ExpressionComparer.Default);
    }

    /// <summary>Verifies constant folding for an actual Power node.</summary>
    [TestMethod]
    public void Simplify_PowerOfConstants_FoldsActualPowerNode()
    {
        AssertStructural(Expression.Power(Expression.Constant(2.0), Expression.Constant(3.0)), Expression.Constant(8.0));
    }

    /// <summary>Verifies equal operands without Power nodes become an actual Power node.</summary>
    [TestMethod]
    public void Simplify_MultiplicationOfEqualsElements_ProducesActualPowerNode()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        var result = (BinaryExpression)new ExpressionSimplifier().Simplify(Expression.Multiply(x, x));
        Assert.AreEqual(ExpressionType.Power, result.NodeType);
        Assert.AreSame(x, result.Left);
        Assert.AreEqual(Expression.Constant(2.0), result.Right, ExpressionComparer.Default);
    }

    /// <summary>Verifies multiplication combines exponents of actual same-base Power nodes.</summary>
    [TestMethod]
    public void Simplify_MultiplicationOfEqualsElements_SamePowerBase_AddsExponents()
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        Expression source = Expression.Multiply(
            Expression.Power(x, Expression.Constant(2.0)),
            Expression.Power(x, Expression.Constant(3.0)));
        Expression expected = Expression.Power(x, Expression.Constant(5.0));
        AssertStructural(source, expected);
    }

    /// <summary>Independently verifies the generic-number power-call conversion rule.</summary>
    [TestMethod]
    public void Simplify_PowerConvertionNumber1_ReturnsActualPowerNode()
    {
        AssertPowerConversion((simplifier, source, left, right) => simplifier.ConvertNumberPower(source, left, right));
    }

    /// <summary>Independently verifies the System.Math power-call conversion rule.</summary>
    [TestMethod]
    public void Simplify_PowerConvertionNumber2_ReturnsActualPowerNode()
    {
        AssertPowerConversion((simplifier, source, left, right) => simplifier.ConvertMathPower(source, left, right));
    }

    /// <summary>Independently verifies the double power-call conversion rule.</summary>
    [TestMethod]
    public void Simplify_PowerConversionMath_ReturnsActualPowerNode()
    {
        AssertPowerConversion((simplifier, source, left, right) => simplifier.ConvertDoublePower(source, left, right));
    }

    /// <summary>Characterizes the prepared-away negate-with-subtraction rule body directly.</summary>
    [TestMethod]
    public void Simplify_NegateWithSubstraction_DirectRuleBody_RewritesOperands()
    {
        var simplifier = new ExposedSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        BinaryExpression operand = Expression.Subtract(x, y);
        UnaryExpression source = Expression.Negate(operand);

        Expression result = simplifier.RewriteNegatedSubtraction(source, operand);

        Assert.AreEqual(ExpressionType.Add, result.NodeType);
        Assert.AreEqual(5.0, Expression.Lambda<Func<double, double, double>>(result, x, y).Compile()(2.0, 7.0), 1e-9);
    }

    /// <summary>Characterizes the prepared-away nested-subtraction rule body directly.</summary>
    [TestMethod]
    public void Simplify_SubstractionWithSubstraction_DirectRuleBody_ReassociatesOperands()
    {
        var simplifier = new ExposedSimplifier();
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        ParameterExpression z = Expression.Parameter(typeof(double), "z");
        BinaryExpression right = Expression.Subtract(y, z);
        BinaryExpression source = Expression.Subtract(x, right);

        Expression result = simplifier.RewriteNestedSubtraction(source, x, right);

        var subtraction = (BinaryExpression)result;
        Assert.AreEqual(ExpressionType.Add, subtraction.Left.NodeType);
        Assert.AreEqual(3.0, Expression.Lambda<Func<double, double, double, double>>(result, x, y, z).Compile()(2.0, 3.0, 4.0), 1e-9);
    }

    /// <summary>Asserts a symbolic integral numerically over the supplied sample domain.</summary>
    private static void AssertIntegral(Func<ParameterExpression, Expression> bodyFactory, Func<double, double> expected, double[] samples)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        var source = Expression.Lambda<Func<double, double>>(bodyFactory(x), x);
        var integral = (Expression<Func<double, double>>)new ExpressionIntegration<double>("x").Integrate(source);
        Func<double, double> actual = integral.Compile();
        foreach (double sample in samples)
        {
            Assert.AreEqual(expected(sample), actual(sample), 1e-9, $"Unexpected integral at x={sample}.");
        }
    }

    /// <summary>Asserts direct public dispatch to one bare binary derivation rule.</summary>
    /// <param name="sourceNodeType">The source binary operation.</param>
    /// <param name="expectedNodeType">The expected top-level derivative operation.</param>
    /// <param name="expectedValue">The derivative value at the representative inputs.</param>
    private static void AssertDirectDerivationRule(ExpressionType sourceNodeType, ExpressionType expectedNodeType, double expectedValue)
    {
        ParameterExpression x = Expression.Parameter(typeof(double), "x");
        ParameterExpression y = Expression.Parameter(typeof(double), "y");
        Expression source = Expression.MakeBinary(sourceNodeType, x, y);

        Expression result = new ExpressionDerivation<double>(x).Transform(source);

        Assert.AreEqual(expectedNodeType, result.NodeType);
        Assert.AreEqual(expectedValue, Expression.Lambda<Func<double, double, double>>(result, x, y).Compile()(2.0, 3.0), 1e-9);
    }

    /// <summary>Creates a double.Pow call with an explicitly supplied exponent node.</summary>
    private static MethodCallExpression CreatePowCall(Expression left, Expression right) =>
        Expression.Call(typeof(double).GetMethod(nameof(double.Pow), [typeof(double), typeof(double)])!, left, right);

    /// <summary>Creates a genuine System.Math.Pow call for public-dispatch integration coverage.</summary>
    private static MethodCallExpression CreateMathPowCall(Expression left, Expression right) =>
        Expression.Call(typeof(Math).GetMethod(nameof(Math.Pow), [typeof(double), typeof(double)])!, left, right);

    /// <summary>Asserts the structurally exact simplified result.</summary>
    private static void AssertStructural(Expression source, Expression expected) =>
        Assert.AreEqual(expected, new ExpressionSimplifier().Simplify(source), ExpressionComparer.Default);

    /// <summary>Asserts one protected power conversion independently of dispatcher selection.</summary>
    private static void AssertPowerConversion(Func<ExposedSimplifier, Expression, Expression, Expression, Expression> conversion)
    {
        var simplifier = new ExposedSimplifier();
        ParameterExpression left = Expression.Parameter(typeof(double), "x");
        ConstantExpression right = Expression.Constant(2.0);
        MethodCallExpression source = CreatePowCall(left, right);
        var result = (BinaryExpression)conversion(simplifier, source, left, right);
        Assert.AreEqual(ExpressionType.Power, result.NodeType);
        Assert.AreSame(left, result.Left);
        Assert.AreEqual(right, result.Right, ExpressionComparer.Default);
    }
}
