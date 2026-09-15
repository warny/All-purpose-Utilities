using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Expressions;
using Utils.Mathematics.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// Detects additions, removals, or signature changes in the audited symbolic transformation-rule inventory.
/// This change-detection guard does not replace the dedicated behavioral tests mapped in the coverage ledger.
/// </summary>
[TestClass]
public sealed class ExpressionTransformationRuleInventoryTests
{
    private static readonly string[] ExpectedRules =
    [
        "Utils.Mathematics.Expressions.ExpressionDerivation<System.Double>.Add(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.Expression)|Node:Add",
        "Utils.Mathematics.Expressions.ExpressionDerivation<System.Double>.Constant(System.Linq.Expressions.ConstantExpression,System.Object)|Node:Constant",
        "Utils.Mathematics.Expressions.ExpressionDerivation<System.Double>.Convert(System.Linq.Expressions.UnaryExpression,System.Linq.Expressions.Expression)|Node:Convert",
        "Utils.Mathematics.Expressions.ExpressionDerivation<System.Double>.ConvertChecked(System.Linq.Expressions.UnaryExpression,System.Linq.Expressions.Expression)|Node:ConvertChecked",
        "Utils.Mathematics.Expressions.ExpressionDerivation<System.Double>.Cos(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.Expression)|Call:System.Double.Cos",
        "Utils.Mathematics.Expressions.ExpressionDerivation<System.Double>.Divide(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.Expression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionDerivation<System.Double>.Exp(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.Expression)|Call:System.Double.Exp",
        "Utils.Mathematics.Expressions.ExpressionDerivation<System.Double>.Log10(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.Expression)|Call:System.Double.Log10",
        "Utils.Mathematics.Expressions.ExpressionDerivation<System.Double>.LogMath(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.Expression)|Call:System.Double.Log",
        "Utils.Mathematics.Expressions.ExpressionDerivation<System.Double>.Multiply(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.Expression)|Node:Multiply",
        "Utils.Mathematics.Expressions.ExpressionDerivation<System.Double>.Negate(System.Linq.Expressions.UnaryExpression,System.Linq.Expressions.Expression)|Node:Negate",
        "Utils.Mathematics.Expressions.ExpressionDerivation<System.Double>.Parameter(System.Linq.Expressions.ParameterExpression)|Node:Parameter",
        "Utils.Mathematics.Expressions.ExpressionDerivation<System.Double>.Power(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.ConstantExpression)|Node:Power",
        "Utils.Mathematics.Expressions.ExpressionDerivation<System.Double>.Power(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.Expression)|Node:Power",
        "Utils.Mathematics.Expressions.ExpressionDerivation<System.Double>.Sin(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.Expression)|Call:System.Double.Sin",
        "Utils.Mathematics.Expressions.ExpressionDerivation<System.Double>.Subtract(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.Expression)|Node:Subtract",
        "Utils.Mathematics.Expressions.ExpressionDerivation<System.Double>.Tan(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.Expression)|Call:System.Double.Tan",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Add(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.Expression)|Node:Add",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Constant(System.Linq.Expressions.ConstantExpression,System.Object)|Node:Constant",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Convert(System.Linq.Expressions.UnaryExpression,System.Linq.Expressions.Expression)|Node:Convert",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.ConvertChecked(System.Linq.Expressions.UnaryExpression,System.Linq.Expressions.Expression)|Node:ConvertChecked",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Cos(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.BinaryExpression)|Call:System.Double.Cos",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Cos(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.ParameterExpression)|Call:System.Double.Cos",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Cosh(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.BinaryExpression)|Call:System.Double.Cosh",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Cosh(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.ParameterExpression)|Call:System.Double.Cosh",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Divide(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.ConstantExpression,System.Linq.Expressions.BinaryExpression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Divide(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.ConstantExpression,System.Linq.Expressions.MethodCallExpression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Divide(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.ConstantExpression,System.Linq.Expressions.ParameterExpression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Divide(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.ConstantExpression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Divide(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.UnaryExpression,System.Linq.Expressions.BinaryExpression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Divide(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.UnaryExpression,System.Linq.Expressions.MethodCallExpression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Divide(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.UnaryExpression,System.Linq.Expressions.ParameterExpression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Exp(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.BinaryExpression)|Call:System.Double.Exp",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Exp(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.ParameterExpression)|Call:System.Double.Exp",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Log(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.ParameterExpression)|Call:System.Double.Log",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Log10(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.ParameterExpression)|Call:System.Double.Log10",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Multiply(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.ConstantExpression,System.Linq.Expressions.Expression)|Node:Multiply",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Multiply(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.ConstantExpression)|Node:Multiply",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Negate(System.Linq.Expressions.UnaryExpression,System.Linq.Expressions.Expression)|Node:Negate",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Parameter(System.Linq.Expressions.ParameterExpression)|Node:Parameter",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Power(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.ParameterExpression,System.Linq.Expressions.ConstantExpression)|Node:Power",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Power(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.ParameterExpression,System.Linq.Expressions.UnaryExpression)|Node:Power",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.PowerMathCall(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.ParameterExpression,System.Linq.Expressions.ConstantExpression)|Call:System.Math.Pow",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.PowerMathCall(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.ParameterExpression,System.Linq.Expressions.UnaryExpression)|Call:System.Math.Pow",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Sin(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.BinaryExpression)|Call:System.Double.Sin",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Sin(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.ParameterExpression)|Call:System.Double.Sin",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Sinh(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.BinaryExpression)|Call:System.Double.Sinh",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Sinh(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.ParameterExpression)|Call:System.Double.Sinh",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Subtract(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.Expression)|Node:Subtract",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Tan(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.BinaryExpression)|Call:System.Double.Tan",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Tan(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.ParameterExpression)|Call:System.Double.Tan",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Tanh(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.BinaryExpression)|Call:System.Double.Tanh",
        "Utils.Mathematics.Expressions.ExpressionIntegration<System.Double>.Tanh(System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.ParameterExpression)|Call:System.Double.Tanh",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.ACosConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Acos",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.ACoshConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Acosh",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.ASinConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Asin",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.ASinhConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Asinh",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.ATanConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Atan",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.ATanhConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Atanh",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.AbsConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Abs",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.AdditionOfConstants(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.ConstantExpression,System.Linq.Expressions.ConstantExpression)|Node:Add",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.AdditionOfCos2andSin2Number(System.Linq.Expressions.Expression,System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.BinaryExpression)|Node:Add",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.AdditionOfEqualsElements(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.Expression)|Node:Add",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.AdditionWithNegate(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.UnaryExpression)|Node:Add",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.AdditionWithNegate(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.UnaryExpression,System.Linq.Expressions.Expression)|Node:Add",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.AdditionWithZero(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.ConstantExpression,System.Linq.Expressions.Expression)|Node:Add",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.AdditionWithZero(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.ConstantExpression)|Node:Add",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.CbrtConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Cbrt",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.CeilingConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Ceiling",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.ClampConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Clamp",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.CosConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Cos",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.CoshConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Cosh",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.DivideWithZero(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.ConstantExpression,System.Linq.Expressions.Expression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.DivideWithZeroOrOne(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.ConstantExpression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.DivisionOfConstants(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.ConstantExpression,System.Linq.Expressions.ConstantExpression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.DivisionOfCosAndSinNumber(System.Linq.Expressions.Expression,System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.MethodCallExpression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.DivisionOfDivision(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.BinaryExpression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.DivisionOfDivision(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.DivisionOfDivision(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.BinaryExpression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.DivisionOfSinAndCosNumber(System.Linq.Expressions.Expression,System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.MethodCallExpression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.DivisionOfSinAndTanNumber(System.Linq.Expressions.Expression,System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.MethodCallExpression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.DivisionWithNegate(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.UnaryExpression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.DivisionWithNegate(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.UnaryExpression,System.Linq.Expressions.Expression)|Node:Divide",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.FloorConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Floor",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.InvokeExpression(System.Linq.Expressions.InvocationExpression)|Node:Invoke",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.Log10ConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Log10",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.Log2ConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Log2",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.LogConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Log",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.Logarithm10SimplificationAddNumber(System.Linq.Expressions.Expression,System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.MethodCallExpression)|Node:Add",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.Logarithm10SimplificationSubstractNumber(System.Linq.Expressions.Expression,System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.MethodCallExpression)|Node:Subtract",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.LogarithmSimplificationAddNumber(System.Linq.Expressions.Expression,System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.MethodCallExpression)|Node:Add",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.LogarithmSimplificationSubstractNumber(System.Linq.Expressions.Expression,System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.MethodCallExpression)|Node:Subtract",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.MaxConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Max",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.MinConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Min",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.Multiplication(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.BinaryExpression)|Node:Multiply",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.Multiplication(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.ConstantExpression,System.Linq.Expressions.BinaryExpression)|Node:Multiply",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.Multiplication(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.ConstantExpression)|Node:Multiply",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.MultiplicationOfConstants(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.ConstantExpression,System.Linq.Expressions.ConstantExpression)|Node:Multiply",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.MultiplicationOfCosAndTanNumber(System.Linq.Expressions.Expression,System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.MethodCallExpression)|Node:Multiply",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.MultiplicationOfEqualsElements(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression)|Node:Multiply",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.MultiplicationOfEqualsElements(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.BinaryExpression)|Node:Multiply",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.MultiplicationOfEqualsElements(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.Expression)|Node:Multiply",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.MultiplicationOfTanAndCosNumber(System.Linq.Expressions.Expression,System.Linq.Expressions.MethodCallExpression,System.Linq.Expressions.MethodCallExpression)|Node:Multiply",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.MultiplicationWithNegate(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.UnaryExpression)|Node:Multiply",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.MultiplicationWithNegate(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.UnaryExpression,System.Linq.Expressions.Expression)|Node:Multiply",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.MultiplicationWithZeroOrOne(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.ConstantExpression,System.Linq.Expressions.Expression)|Node:Multiply",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.MultiplicationWithZeroOrOne(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.ConstantExpression)|Node:Multiply",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.NegateWithSubstraction(System.Linq.Expressions.UnaryExpression,System.Linq.Expressions.BinaryExpression)|Node:Negate",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.PowerByZeroOrOne(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.ConstantExpression)|Node:Power",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.PowerConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression,System.Linq.Expressions.Expression)|Call:System.Double.Pow",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.PowerConvertionNumber1(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression,System.Linq.Expressions.Expression)|Call:System.Double.Pow",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.PowerConvertionNumber2(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression,System.Linq.Expressions.Expression)|Call:System.Math.Pow",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.PowerOfConstants(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.ConstantExpression,System.Linq.Expressions.ConstantExpression)|Node:Power",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.PowerOfZeroOrOne(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.ConstantExpression,System.Linq.Expressions.Expression)|Node:Power",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.RoundConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Round",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.SignConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Sign",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.SinConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Sin",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.SinhConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Sinh",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.SqrtConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Sqrt",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.SubstractionOfConstants(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.ConstantExpression,System.Linq.Expressions.ConstantExpression)|Node:Subtract",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.SubstractionOfEqualsElements(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.Expression)|Node:Subtract",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.SubstractionWithAddition(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.BinaryExpression)|Node:Subtract",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.SubstractionWithNegate(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.UnaryExpression)|Node:Subtract",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.SubstractionWithNegate(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.UnaryExpression,System.Linq.Expressions.Expression)|Node:Subtract",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.SubstractionWithSubstraction(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.BinaryExpression)|Node:Subtract",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.SubstractionWithZero(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.ConstantExpression,System.Linq.Expressions.Expression)|Node:Subtract",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.SubstractionWithZero(System.Linq.Expressions.BinaryExpression,System.Linq.Expressions.Expression,System.Linq.Expressions.ConstantExpression)|Node:Subtract",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.TanConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Tan",
        "Utils.Mathematics.Expressions.ExpressionSimplifier.TanhConversionMath(System.Linq.Expressions.Expression,System.Linq.Expressions.Expression[])|Call:System.Double.Tanh",
    ];

    /// <summary>Compares the reflected rule inventory with the explicitly reviewed snapshot.</summary>
    [TestMethod]
    public void TransformationRules_MatchAuditedInventory()
    {
        string[] actual =
        [
            .. new[]
            {
                typeof(ExpressionDerivation<double>),
                typeof(ExpressionIntegration<double>),
                typeof(ExpressionSimplifier)
            }
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Select(method => (Method: method, Attribute: method.GetCustomAttribute<ExpressionSignatureAttribute>(false)))
            .Where(item => item.Attribute is not null)
            .Select(item => FormatRule(item.Method, item.Attribute!))
            .OrderBy(identifier => identifier, StringComparer.Ordinal)
        ];

        if (!ExpectedRules.SequenceEqual(actual, StringComparer.Ordinal))
        {
            Assert.Fail("The transformation-rule inventory changed. Review behavioral coverage and update the snapshot:\n" + string.Join("\n", actual));
        }
    }

    /// <summary>Formats a stable rule identifier containing its declaring type, overload parameters, and dispatch signature.</summary>
    private static string FormatRule(MethodInfo method, ExpressionSignatureAttribute attribute)
    {
        string parameters = string.Join(",", method.GetParameters().Select(parameter => FormatType(parameter.ParameterType)));
        string signature = attribute is ExpressionCallSignatureAttribute call
            ? $"Call:{string.Join("+", call.Types.Select(FormatType))}.{call.FunctionName}"
            : $"Node:{attribute.ExpressionType}";
        return $"{FormatType(method.DeclaringType!)}.{method.Name}({parameters})|{signature}";
    }

    /// <summary>Formats a CLR type without assembly-version details.</summary>
    private static string FormatType(Type type)
    {
        if (type.IsArray)
        {
            return $"{FormatType(type.GetElementType()!)}[]";
        }

        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        string name = type.GetGenericTypeDefinition().FullName!.Split('`')[0];
        return $"{name}<{string.Join(",", type.GetGenericArguments().Select(FormatType))}>";
    }
}
