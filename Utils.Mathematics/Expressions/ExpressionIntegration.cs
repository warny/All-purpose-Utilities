using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using Utils.Expressions;
using Utils.Objects;

namespace Utils.Mathematics.Expressions;
#pragma warning disable CS8604 // Existence possible d'un argument de référence null.

/// <summary>
/// Provides integration rules for mathematical expression trees.
/// </summary>
public class ExpressionIntegration<T> : ExpressionTransformer where T : IFloatingPoint<T>
{
    /// <summary>
    /// Gets the name of the parameter used as the integration variable.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// The specific <see cref="ParameterExpression"/> instance, resolved once per <see cref="Integrate"/>
    /// call, that identifies the integration variable by reference rather than by name. Two distinct
    /// <see cref="ParameterExpression"/> objects may legally share the same <see cref="ParameterName"/>
    /// (e.g. an unrelated captured variable); comparing by reference instead of by name avoids treating
    /// such a foreign parameter as the integration variable. This field is set once, in the private
    /// constructor, on a fresh per-call worker instance (see <see cref="Integrate"/>), so it is never
    /// mutated after construction and each call gets its own isolated instance.
    /// </summary>
    private readonly ParameterExpression parameter;

    /// <summary>
    /// Determines whether <paramref name="candidate"/> is the specific parameter instance resolved as
    /// the integration variable for the current call.
    /// </summary>
    private bool IsTargetParameter(ParameterExpression candidate) => ReferenceEquals(candidate, parameter);

    /// <summary>
    /// Creates a <typeparamref name="T"/>-typed constant from a <see cref="double"/> value computed
    /// during rule evaluation (e.g. an exponent shifted by one). Rules must use this instead of
    /// <c>Expression.Constant(value)</c> (which creates a raw <see cref="double"/> constant regardless
    /// of <typeparamref name="T"/>), since combining a <see cref="double"/> constant with a
    /// <typeparamref name="T"/>-typed expression fails to construct for any <typeparamref name="T"/>
    /// other than <see cref="double"/> itself.
    /// </summary>
    private static ConstantExpression NumericConstant(double value) => ExpressionEx.CreateConstant(T.CreateChecked(value));

    /// <summary>
    /// Builds <c>Log(Abs(operand))</c> rather than a bare <c>Log(operand)</c>. Unlike the source
    /// expressions these antiderivatives replace (e.g. <c>1/x</c> or <c>tan(x) = sin(x)/cos(x)</c>,
    /// both well-defined for negative arguments), a raw <c>Log</c> is only real-valued for a positive
    /// argument and would return NaN on exactly the negative-domain inputs the original expression
    /// supported. <c>Abs</c> is always available (it is part of <see cref="INumberBase{TSelf}"/>, which
    /// every <see cref="IFloatingPoint{TSelf}"/> implements), so this never needs a capability check.
    /// </summary>
    private static Expression LogAbs(Expression operand) =>
        Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Log)),
            Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Abs)), operand));

    /// <summary>
    /// Integrates a lambda expression with respect to the configured parameter.
    /// </summary>
    /// <param name="e">The lambda expression to integrate.</param>
    /// <returns>The integrated expression tree.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no parameter named <see cref="ParameterName"/> is found in <paramref name="e"/>, or
    /// when more than one distinct parameter shares that name (an ambiguous match that cannot be
    /// resolved by name alone).
    /// </exception>
    public Expression Integrate(LambdaExpression e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var candidates = e.Parameters.Where(p => p.Name == ParameterName).ToList();
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException($"The parameter '{ParameterName}' was not found in the lambda expression.");
        }
        if (candidates.Count > 1)
        {
            throw new InvalidOperationException(
                $"The lambda expression declares {candidates.Count} distinct parameters named '{ParameterName}'; " +
                "the integration variable is ambiguous. Use distinct parameter names.");
        }

        // A fresh worker instance isolates the resolved target parameter for this call: concurrent or
        // re-entrant calls on the same public ExpressionIntegration<T> instance no longer share mutable
        // state (see TODO-2026-07-11-pass3.md item #32).
        var worker = new ExpressionIntegration<T>(ParameterName, candidates[0]);
        return Expression.Lambda(worker.Transform(e.Body), e.Parameters);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionIntegration{T}"/> class.
    /// </summary>
    /// <param name="parameterName">The name of the parameter serving as the integration variable.</param>
    public ExpressionIntegration(string parameterName) : this(parameterName, null!)
    {
    }

    /// <summary>
    /// Initializes a per-call worker instance with its target parameter already resolved.
    /// </summary>
    /// <param name="parameterName">The name of the parameter serving as the integration variable.</param>
    /// <param name="parameter">
    /// The specific resolved <see cref="ParameterExpression"/> instance, or <see langword="null"/> for
    /// the publicly-constructed instance that has not yet performed an <see cref="Integrate"/> call.
    /// </param>
    private ExpressionIntegration(string parameterName, ParameterExpression parameter)
    {
        this.ParameterName = parameterName;
        this.parameter = parameter;
    }



    /// <summary>
    /// Integrates the wrapped operand and re-applies the conversion's declared result type when the
    /// integral's type does not already match it, so the result expression stays type-consistent with
    /// the original conversion node.
    /// </summary>
    /// <param name="e">The conversion expression.</param>
    /// <param name="operand">The wrapped operand.</param>
    /// <returns>The integral of the wrapped operand, converted back to <c>e.Type</c> if needed.</returns>
    [ExpressionSignature(ExpressionType.Convert)]
    public Expression Convert(
        UnaryExpression e,
        Expression operand
    )
    {
        return PreserveConversion(e, Transform(operand), isChecked: false);
    }

    /// <summary>
    /// Integrates the wrapped operand through a checked conversion. Checked conversions exist
    /// specifically to guard against narrowing/overflow, which has no well-defined symbolic integral;
    /// only a trivial same-type checked conversion is passed through, anything else is rejected instead
    /// of silently stripped.
    /// </summary>
    /// <param name="e">The checked conversion expression.</param>
    /// <param name="operand">The wrapped operand.</param>
    /// <returns>The integral of the wrapped operand when the checked conversion is a same-type no-op.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the checked conversion actually changes the type (a narrowing or otherwise
    /// non-trivial conversion), since integrating through it is not well-defined.
    /// </exception>
    [ExpressionSignature(ExpressionType.ConvertChecked)]
    public Expression ConvertChecked(
        UnaryExpression e,
        Expression operand
    )
    {
        return PreserveConversion(e, Transform(operand), isChecked: true);
    }

    /// <summary>
    /// Reconciles the type of a transformed integral with the declared result type of the original
    /// conversion node it replaces.
    /// </summary>
    /// <param name="original">The original <c>Convert</c>/<c>ConvertChecked</c> expression being replaced.</param>
    /// <param name="transformedOperand">The already-integrated operand.</param>
    /// <param name="isChecked"><see langword="true"/> for <c>ConvertChecked</c>; <see langword="false"/> for <c>Convert</c>.</param>
    /// <returns><paramref name="transformedOperand"/>, converted back to <c>original.Type</c> if needed.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the types differ and the conversion is either checked (narrowing/overflow-prone) or
    /// not a recognized numeric-to-numeric widening.
    /// </exception>
    private static Expression PreserveConversion(UnaryExpression original, Expression transformedOperand, bool isChecked)
    {
        if (transformedOperand.Type == original.Type)
        {
            return transformedOperand;
        }

        if (!isChecked
            && NumberUtils.IsNativeNumericType(transformedOperand.Type)
            && NumberUtils.IsNativeNumericType(original.Type))
        {
            return Expression.Convert(transformedOperand, original.Type);
        }

        throw new NotSupportedException(
            $"Cannot preserve the {(isChecked ? "checked " : string.Empty)}conversion from '{transformedOperand.Type}' " +
            $"to '{original.Type}': only same-type conversions and unchecked numeric widenings are supported.");
    }
    /// <summary>
    /// Integrates a numeric constant by multiplying it with the integration parameter.
    /// </summary>
    /// <param name="e">The constant expression being transformed.</param>
    /// <param name="value">The numeric value stored by the constant expression.</param>
    /// <returns>The integral expression for the constant value.</returns>
    [ExpressionSignature(ExpressionType.Constant)]
    public Expression Constant(
        ConstantExpression e,
        object value
    )
    {
        return Expression.Multiply(Expression.Convert(e, typeof(T)), parameter);
    }

    /// <summary>
    /// Integrates a negated expression by negating the integral of its operand.
    /// </summary>
    /// <param name="e">The unary expression representing the negation.</param>
    /// <param name="operand">The operand that is being negated.</param>
    /// <returns>The integral of the negated expression.</returns>
    [ExpressionSignature(ExpressionType.Negate)]
    public Expression Negate(
        UnaryExpression e,
        Expression operand
    )
    {
        return Expression.Negate(Transform(operand));
    }

    /// <summary>
    /// Integrates a parameter expression, returning x^2/2 when it matches the integration variable.
    /// </summary>
    /// <param name="e">The parameter expression that may match the integration variable.</param>
    /// <returns>The integral of the parameter expression.</returns>
    [ExpressionSignature(ExpressionType.Parameter)]
    public Expression Parameter(
        ParameterExpression e
    )
    {
        if (IsTargetParameter(e))
        {
            ConstantExpression two = ExpressionEx.CreateConstant(T.CreateChecked(2d));
            return Expression.Divide(
                Expression.Call(MathMethodResolver.ResolveBinary<T>(nameof(double.Pow)), e, two),
                two
            );
        }
        else
        {
            return Expression.Multiply(
                e, parameter
            );
        }
    }

    /// <summary>
    /// Integrates a sum by integrating each operand individually.
    /// </summary>
    /// <param name="e">The binary expression representing the addition.</param>
    /// <param name="left">The left operand of the addition.</param>
    /// <param name="right">The right operand of the addition.</param>
    /// <returns>The integral of the sum expression.</returns>
    [ExpressionSignature(ExpressionType.Add)]
    public Expression Add(
        BinaryExpression e,
        Expression left,
        Expression right
    )
    {
        return Expression.Add(
            Transform(left),
            Transform(right)
            );
    }

    /// <summary>
    /// Integrates a subtraction by integrating both operands and subtracting the results.
    /// </summary>
    /// <param name="e">The binary expression representing the subtraction.</param>
    /// <param name="left">The expression on the left side of the operator.</param>
    /// <param name="right">The expression on the right side of the operator.</param>
    /// <returns>The integral of the subtraction expression.</returns>
    [ExpressionSignature(ExpressionType.Subtract)]
    public Expression Subtract(
        BinaryExpression e,
        Expression left,
        Expression right
    )
    {
        return Expression.Subtract(
            Transform(left),
            Transform(right)
        );
    }

    /// <summary>
    /// Integrates a product of a constant and an expression by treating the constant as a factor.
    /// </summary>
    /// <param name="e">The binary expression representing the multiplication.</param>
    /// <param name="left">The constant factor on the left side.</param>
    /// <param name="right">The expression on the right side.</param>
    /// <returns>The integral of the product.</returns>
    [ExpressionSignature(ExpressionType.Multiply)]
    public Expression Multiply(
        BinaryExpression e,
        [ConstantNumeric] ConstantExpression left,
        Expression right
    )
    {
        return Expression.Multiply(left, Transform(right));
    }

    /// <summary>
    /// Integrates a product of an expression and a constant by treating the constant as a factor.
    /// </summary>
    /// <param name="e">The binary expression representing the multiplication.</param>
    /// <param name="left">The non-constant expression on the left side.</param>
    /// <param name="right">The constant factor on the right side.</param>
    /// <returns>The integral of the product.</returns>
    [ExpressionSignature(ExpressionType.Multiply)]
    public Expression Multiply(
        BinaryExpression e,
        Expression left,
        [ConstantNumeric] ConstantExpression right
    )
    {
        return Expression.Multiply(right, Transform(left));
    }

    /// <summary>
    /// Integrates a quotient whose denominator is a constant by integrating the numerator.
    /// </summary>
    /// <param name="e">The binary expression representing the division.</param>
    /// <param name="left">The expression in the numerator.</param>
    /// <param name="right">The constant expression in the denominator.</param>
    /// <returns>The integral of the division expression.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    public Expression Divide(
        BinaryExpression e,
        Expression left,
        [ConstantNumeric] ConstantExpression right
    )
    {
        return Expression.Divide(Transform(left), right);
    }

    /// <summary>
    /// Integrates a constant divided by a parameter by applying the logarithm rule.
    /// </summary>
    /// <param name="e">The division expression being transformed.</param>
    /// <param name="left">The constant expression in the numerator.</param>
    /// <param name="right">The parameter in the denominator.</param>
    /// <returns>The integral of the quotient expression.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    public Expression? Divide(
            BinaryExpression e,
            [ConstantNumeric] ConstantExpression left,
            ParameterExpression right
)
    {
        if (!IsTargetParameter(right)) return null;
        return Expression.Multiply(left, LogAbs(right));
    }

    /// <summary>
    /// Integrates a converted numeric constant divided by the integration parameter.
    /// This covers inputs where numeric normalization introduces a <see cref="ExpressionType.Convert"/> node.
    /// </summary>
    /// <param name="e">The division expression being transformed.</param>
    /// <param name="left">The converted numeric constant in the numerator.</param>
    /// <param name="right">The parameter in the denominator.</param>
    /// <returns>The integral of the quotient when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    public Expression? Divide(
        BinaryExpression e,
        UnaryExpression left,
        ParameterExpression right
    )
    {
        if (!IsTargetParameter(right)) return null;
        if (left.NodeType != ExpressionType.Convert && left.NodeType != ExpressionType.ConvertChecked) return null;
        if (left.Operand is not ConstantExpression constant || !NumberUtils.IsNumeric(constant.Value)) return null;

        ConstantExpression numericLeft = NumericConstant(System.Convert.ToDouble(constant.Value));
        return Expression.Multiply(numericLeft, LogAbs(right));
    }

    /// <summary>
    /// Integrates a constant divided by a power expression representing x raised to n.
    /// </summary>
    /// <param name="e">The division expression being transformed.</param>
    /// <param name="left">The constant factor in the numerator.</param>
    /// <param name="right">The power expression in the denominator.</param>
    /// <returns>The integral of the quotient when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    public Expression? Divide(
    BinaryExpression e,
    [ConstantNumeric] ConstantExpression left,
    [ExpressionSignature(ExpressionType.Power)] BinaryExpression right
)
    {
        if (right.Left is not ParameterExpression p || !IsTargetParameter(p))
        {
            return null;
        }

        ConstantExpression? expo = right.Right as ConstantExpression;
        if (expo is null && right.Right is UnaryExpression unaryExpo &&
            (unaryExpo.NodeType == ExpressionType.Convert || unaryExpo.NodeType == ExpressionType.ConvertChecked))
        {
            expo = unaryExpo.Operand as ConstantExpression;
        }

        if (expo is null || !NumberUtils.IsNumeric(expo.Value))
        {
            return null;
        }

        double n = System.Convert.ToDouble(expo.Value);
        if (double.Abs(n - 1.0) < 1e-10)
        {
            return Expression.Multiply(left, LogAbs(p));
        }

        double newExpo = 1.0 - n;
        ConstantExpression newExpoConstant = NumericConstant(newExpo);
        return Expression.Divide(
            Expression.Multiply(left, Expression.Call(MathMethodResolver.ResolveBinary<T>(nameof(double.Pow)), p, newExpoConstant)),
            newExpoConstant
        );
    }

    /// <summary>
    /// Integrates a converted numeric constant divided by a power expression in the denominator.
    /// </summary>
    /// <param name="e">The division expression being transformed.</param>
    /// <param name="left">The converted numeric constant in the numerator.</param>
    /// <param name="right">The power expression in the denominator.</param>
    /// <returns>The integral of the quotient when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    public Expression? Divide(
        BinaryExpression e,
        UnaryExpression left,
        [ExpressionSignature(ExpressionType.Power)] BinaryExpression right
    )
    {
        if (left.NodeType != ExpressionType.Convert && left.NodeType != ExpressionType.ConvertChecked) return null;
        if (left.Operand is not ConstantExpression constant || !NumberUtils.IsNumeric(constant.Value)) return null;
        ConstantExpression numericLeft = NumericConstant(System.Convert.ToDouble(constant.Value));
        return Divide(e, numericLeft, right);
    }

    /// <summary>
    /// Integrates a constant divided by a square root method call of the integration parameter.
    /// </summary>
    /// <param name="e">The division expression being transformed.</param>
    /// <param name="left">The constant factor in the numerator.</param>
    /// <param name="right">The method call expected to represent <c>double.Sqrt</c>.</param>
    /// <returns>The integral of the expression when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    public Expression? Divide(
    BinaryExpression e,
    [ConstantNumeric] ConstantExpression left,
    MethodCallExpression right
)
    {
        if (right.Method.Name == nameof(double.Sqrt) &&
            right.Arguments.Count == 1 &&
            right.Arguments[0] is ParameterExpression pSqrt &&
            IsTargetParameter(pSqrt))
        {
            double factor = 2.0 * System.Convert.ToDouble(left.Value);
            return Expression.Multiply(
                NumericConstant(factor),
                Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Sqrt)), pSqrt)
            );
        }

        if (right.Method.Name == nameof(double.Pow) &&
            right.Arguments.Count == 2 &&
            right.Arguments[0] is ParameterExpression pPow &&
            IsTargetParameter(pPow))
        {
            ConstantExpression? exponent = right.Arguments[1] as ConstantExpression;
            if (exponent is null && right.Arguments[1] is UnaryExpression unaryExponent &&
                (unaryExponent.NodeType == ExpressionType.Convert || unaryExponent.NodeType == ExpressionType.ConvertChecked))
            {
                exponent = unaryExponent.Operand as ConstantExpression;
            }

            if (exponent is null || !NumberUtils.IsNumeric(exponent.Value))
            {
                return null;
            }

            double n = System.Convert.ToDouble(exponent.Value);
            if (double.Abs(n - 1.0) < 1e-10)
            {
                return Expression.Multiply(left, LogAbs(pPow));
            }

            double newExpo = 1.0 - n;
            ConstantExpression newExpoConstant = NumericConstant(newExpo);
            return Expression.Divide(
                Expression.Multiply(left, Expression.Call(MathMethodResolver.ResolveBinary<T>(nameof(double.Pow)), pPow, newExpoConstant)),
                newExpoConstant
            );
        }

        return null;
    }

    /// <summary>
    /// Integrates a converted numeric constant divided by a method call denominator.
    /// </summary>
    /// <param name="e">The division expression being transformed.</param>
    /// <param name="left">The converted numeric constant in the numerator.</param>
    /// <param name="right">The method call denominator.</param>
    /// <returns>The integral of the quotient when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    public Expression? Divide(
        BinaryExpression e,
        UnaryExpression left,
        MethodCallExpression right
    )
    {
        if (left.NodeType != ExpressionType.Convert && left.NodeType != ExpressionType.ConvertChecked) return null;
        if (left.Operand is not ConstantExpression constant || !NumberUtils.IsNumeric(constant.Value)) return null;
        ConstantExpression numericLeft = NumericConstant(System.Convert.ToDouble(constant.Value));
        return Divide(e, numericLeft, right);
    }

    /// <summary>
    /// Integrates a natural logarithm call whose argument is the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Log</c>.</param>
    /// <param name="p">The parameter passed to the logarithm.</param>
    /// <returns>The integral of the logarithm when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), "Log")]
    public Expression? Log(
            MethodCallExpression e,
            ParameterExpression p
    )
    {
        if (!IsTargetParameter(p)) return null;
        return Expression.Multiply(
                    parameter,
                    Expression.Subtract(
                        Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Log)), parameter),
                        ExpressionEx.CreateConstant(T.CreateChecked(1d))
                        )
                );
    }

    /// <summary>
    /// Integrates a base-10 logarithm call that uses the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Log10</c>.</param>
    /// <param name="p">The parameter passed to the logarithm.</param>
    /// <returns>The integral of the logarithm when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Log10))]
    public Expression? Log10(
            MethodCallExpression e,
            ParameterExpression p
    )
    {
        if (!IsTargetParameter(p)) return null;
        var ln10 = ExpressionEx.CreateConstant(T.CreateChecked(double.Log(10.0)));
        return Expression.Subtract(
            Expression.Multiply(p,
                Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Log10)), p)),
            Expression.Divide(p, ln10)
        );
    }

    /// <summary>
    /// Integrates a power expression whose base is the integration parameter.
    /// </summary>
    /// <param name="e">The power expression being transformed.</param>
    /// <param name="p">The parameter expression that serves as the base.</param>
    /// <param name="expo">The constant exponent applied to the base.</param>
    /// <returns>The integral of the power expression when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Power)]
    public Expression? Power(
    BinaryExpression e,
    ParameterExpression p,
    [ConstantNumeric] ConstantExpression expo
)
    {
        if (!IsTargetParameter(p)) return null;
        double n = System.Convert.ToDouble(expo.Value);
        if (double.Abs(n + 1.0) < 1e-10)
        {
            return LogAbs(p);
        }
        ConstantExpression shiftedExpo = NumericConstant(n + 1.0);
        return Expression.Divide(
            Expression.Call(MathMethodResolver.ResolveBinary<T>(nameof(double.Pow)), p, shiftedExpo),
            shiftedExpo
        );
    }

    /// <summary>
    /// Integrates a power expression whose exponent is wrapped in a numeric conversion.
    /// </summary>
    /// <param name="e">The power expression being transformed.</param>
    /// <param name="p">The parameter expression that serves as the base.</param>
    /// <param name="expo">The converted exponent expression.</param>
    /// <returns>The integral of the power expression when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Power)]
    public Expression? Power(
        BinaryExpression e,
        ParameterExpression p,
        UnaryExpression expo
    )
    {
        if (expo.NodeType != ExpressionType.Convert && expo.NodeType != ExpressionType.ConvertChecked)
        {
            return null;
        }

        if (expo.Operand is not ConstantExpression constantExpo || !NumberUtils.IsNumeric(constantExpo.Value))
        {
            return null;
        }

        return Power(e, p, NumericConstant(System.Convert.ToDouble(constantExpo.Value)));
    }

    /// <summary>
    /// Integrates a <see cref="Math.Pow"/> call whose base is the integration parameter and exponent is a numeric constant.
    /// </summary>
    /// <param name="e">The method call expression representing <c>Math.Pow</c>.</param>
    /// <param name="p">The parameter expression that serves as the base.</param>
    /// <param name="expo">The constant numeric exponent.</param>
    /// <returns>The integral of the power expression when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(Math), nameof(Math.Pow))]
    public Expression? PowerMathCall(
        MethodCallExpression e,
        ParameterExpression p,
        [ConstantNumeric] ConstantExpression expo
    )
    {
        if (!IsTargetParameter(p)) return null;
        double n = System.Convert.ToDouble(expo.Value);
        if (double.Abs(n + 1.0) < 1e-10)
            return LogAbs(p);
        ConstantExpression shiftedExpo = NumericConstant(n + 1.0);
        return Expression.Divide(
            Expression.Call(MathMethodResolver.ResolveBinary<T>(nameof(double.Pow)), p, shiftedExpo),
            shiftedExpo
        );
    }

    /// <summary>
    /// Integrates a <see cref="Math.Pow"/> call whose base is the integration parameter and exponent is a convert-wrapped numeric constant.
    /// </summary>
    /// <param name="e">The method call expression representing <c>Math.Pow</c>.</param>
    /// <param name="p">The parameter expression that serves as the base.</param>
    /// <param name="expo">The converted exponent expression.</param>
    /// <returns>The integral of the power expression when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(Math), nameof(Math.Pow))]
    public Expression? PowerMathCall(
        MethodCallExpression e,
        ParameterExpression p,
        UnaryExpression expo
    )
    {
        if (expo.NodeType != ExpressionType.Convert && expo.NodeType != ExpressionType.ConvertChecked)
            return null;
        if (expo.Operand is not ConstantExpression constantExpo || !NumberUtils.IsNumeric(constantExpo.Value))
            return null;
        return PowerMathCall(e, p, NumericConstant(System.Convert.ToDouble(constantExpo.Value)));
    }

    /// <summary>
    /// Integrates an exponential call that directly uses the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Exp</c>.</param>
    /// <param name="op">The parameter passed to the exponential function.</param>
    /// <returns>The integral of the exponential call when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Exp))]
    public Expression? Exp(
    MethodCallExpression e,
    ParameterExpression op
)
    {
        if (!IsTargetParameter(op)) return null;
        return Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Exp)), op);
    }

    /// <summary>
    /// Integrates an exponential call whose argument is a scaled integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Exp</c>.</param>
    /// <param name="be">The binary multiplication composing the exponential argument.</param>
    /// <returns>The integral of the exponential call when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Exp))]
    public Expression? Exp(
            MethodCallExpression e,
            BinaryExpression be
    )
    {
        if (be.NodeType != ExpressionType.Multiply ||
            be.Left is not ConstantExpression c || !NumberUtils.IsNumeric(c.Value) ||
            be.Right is not ParameterExpression p2 || !IsTargetParameter(p2))
        {
            return null;
        }
        return Expression.Divide(
                Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Exp)), be),
                c
            );
    }

    /// <summary>
    /// Integrates a sine call that directly uses the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Sin</c>.</param>
    /// <param name="op">The parameter passed to the sine function.</param>
    /// <returns>The integral of the sine call when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Sin))]
    public Expression? Sin(
    MethodCallExpression e,
    ParameterExpression op
)
    {
        if (!IsTargetParameter(op)) return null;
        return Expression.Negate(
                Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Cos)), op)
            );
    }

    /// <summary>
    /// Integrates a sine call whose argument is a scaled integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Sin</c>.</param>
    /// <param name="be">The binary multiplication composing the sine argument.</param>
    /// <returns>The integral of the sine call when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Sin))]
    public Expression? Sin(
            MethodCallExpression e,
            BinaryExpression be
    )
    {
        if (be.NodeType != ExpressionType.Multiply ||
            be.Left is not ConstantExpression c || !NumberUtils.IsNumeric(c.Value) ||
            be.Right is not ParameterExpression p2 || !IsTargetParameter(p2))
        {
            return null;
        }
        return Expression.Divide(
                Expression.Negate(Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Cos)), be)),
                c
            );
    }

    /// <summary>
    /// Integrates a cosine call that directly uses the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Cos</c>.</param>
    /// <param name="op">The parameter passed to the cosine function.</param>
    /// <returns>The integral of the cosine call when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Cos))]
    public Expression? Cos(
    MethodCallExpression e,
    ParameterExpression op
)
    {
        if (!IsTargetParameter(op)) return null;
        return Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Sin)), op);
    }

    /// <summary>
    /// Integrates a cosine call whose argument is a scaled integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Cos</c>.</param>
    /// <param name="be">The binary multiplication composing the cosine argument.</param>
    /// <returns>The integral of the cosine call when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Cos))]
    public Expression? Cos(
    MethodCallExpression e,
    BinaryExpression be
)
    {
        if (be.NodeType != ExpressionType.Multiply ||
            be.Left is not ConstantExpression c || !NumberUtils.IsNumeric(c.Value) ||
            be.Right is not ParameterExpression p2 || !IsTargetParameter(p2))
        {
            return null;
        }
        return Expression.Divide(
                Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Sin)), be),
                c
            );
    }


    /// <summary>
    /// Integrates a tangent call that directly uses the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Tan</c>.</param>
    /// <param name="op">The parameter passed to the tangent function.</param>
    /// <returns>The integral of the tangent call when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Tan))]
    public Expression? Tan(
    MethodCallExpression e,
    ParameterExpression op
)
    {
        if (!IsTargetParameter(op)) return null;

        return Expression.Negate(LogAbs(Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Cos)), op)));
    }

    /// <summary>
    /// Integrates a tangent call whose argument is a scaled integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Tan</c>.</param>
    /// <param name="be">The binary multiplication composing the tangent argument.</param>
    /// <returns>The integral of the tangent call when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Tan))]
    public Expression? Tan(
    MethodCallExpression e,
            BinaryExpression be
)
    {
        if (be.NodeType != ExpressionType.Multiply ||
            be.Left is not ConstantExpression c || !NumberUtils.IsNumeric(c.Value) ||
            be.Right is not ParameterExpression p2 || !IsTargetParameter(p2))
        {
            return null;
        }
        return Expression.Divide(
                Expression.Negate(LogAbs(Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Cos)), be))),
                c
            );
    }

    /// <summary>
    /// Integrates a hyperbolic sine call that directly uses the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Sinh</c>.</param>
    /// <param name="op">The parameter passed to the hyperbolic sine function.</param>
    /// <returns>The integral of the hyperbolic sine call when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Sinh))]
    public Expression? Sinh(
    MethodCallExpression e,
    ParameterExpression op
)
    {
        if (!IsTargetParameter(op))
        {
            return null;
        }
        return Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Cosh)), op);
    }

    /// <summary>
    /// Integrates a hyperbolic sine call whose argument is a scaled integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Sinh</c>.</param>
    /// <param name="be">The binary multiplication composing the hyperbolic sine argument.</param>
    /// <returns>The integral of the hyperbolic sine call when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Sinh))]
    public Expression? Sinh(
    MethodCallExpression e,
            BinaryExpression be
)
    {
        if (be.NodeType != ExpressionType.Multiply ||
            be.Left is not ConstantExpression c || !NumberUtils.IsNumeric(c.Value) ||
            be.Right is not ParameterExpression p2 || !IsTargetParameter(p2))
        {
            return null;
        }
        return Expression.Divide(
                Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Cosh)), be),
                c
            );
    }

    /// <summary>
    /// Integrates a hyperbolic cosine call that directly uses the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Cosh</c>.</param>
    /// <param name="op">The parameter passed to the hyperbolic cosine function.</param>
    /// <returns>The integral of the hyperbolic cosine call when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Cosh))]
    public Expression? Cosh(
            MethodCallExpression e,
            ParameterExpression op
    )
    {
        if (!IsTargetParameter(op)) return null;

        return Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Sinh)), op);
    }

    /// <summary>
    /// Integrates a hyperbolic cosine call whose argument is a scaled integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Cosh</c>.</param>
    /// <param name="be">The binary multiplication composing the hyperbolic cosine argument.</param>
    /// <returns>The integral of the hyperbolic cosine call when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Cosh))]
    public Expression? Cosh(
            MethodCallExpression e,
            BinaryExpression be
    )
    {
        if (be.NodeType != ExpressionType.Multiply ||
            be.Left is not ConstantExpression c || !NumberUtils.IsNumeric(c.Value) ||
            be.Right is not ParameterExpression p2 || !IsTargetParameter(p2))
        {
            return null;
        }
        return Expression.Divide(
                Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Sinh)), be),
                c
            );
    }

    /// <summary>
    /// Integrates a hyperbolic tangent call that directly uses the integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Tanh</c>.</param>
    /// <param name="op">The parameter passed to the hyperbolic tangent function.</param>
    /// <returns>The integral of the hyperbolic tangent call when the parameter matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Tanh))]
    public Expression? Tanh(
            MethodCallExpression e,
            ParameterExpression op
    )
    {
        if (!IsTargetParameter(op)) return null;

        return Expression.Call(
            MathMethodResolver.Resolve<T>(nameof(double.Log)),
            Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Cosh)), op)
        );
    }

    /// <summary>
    /// Integrates a hyperbolic tangent call whose argument is a scaled integration parameter.
    /// </summary>
    /// <param name="e">The method call expression representing <c>double.Tanh</c>.</param>
    /// <param name="be">The binary multiplication composing the hyperbolic tangent argument.</param>
    /// <returns>The integral of the hyperbolic tangent call when the pattern matches; otherwise, <see langword="null"/>.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Tanh))]
    public Expression? Tanh(
    MethodCallExpression e,
            BinaryExpression be
)
    {
        if (be.NodeType != ExpressionType.Multiply ||
            be.Left is not ConstantExpression c || !NumberUtils.IsNumeric(c?.Value) ||
            be.Right is not ParameterExpression p2 || !IsTargetParameter(p2))
        {
            return null;
        }
        return Expression.Divide(
                Expression.Call(
                    MathMethodResolver.Resolve<T>(nameof(double.Log)),
                    Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Cosh)), be)
                ),
                c
            );
    }

}



/// <summary>
/// Provides the historical non-generic entry-point for double-based integration.
/// </summary>
public class ExpressionIntegration : ExpressionIntegration<double>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionIntegration"/> class.
    /// </summary>
    /// <param name="parameterName">The name of the parameter serving as the integration variable.</param>
    public ExpressionIntegration(string parameterName)
        : base(parameterName)
    {
    }
}

#pragma warning restore CS8604 // Existence possible d'un argument de référence null.
