using System;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using Utils.Expressions;
using Utils.Objects;

namespace Utils.Mathematics.Expressions;
#pragma warning disable CS8604
/// <summary>
/// Provides single-variable symbolic differentiation for LINQ expression trees.
/// </summary>
public class ExpressionDerivation<T> : ExpressionTransformer where T : IFloatingPoint<T>
{
    private static readonly double FiniteDifferenceEpsilon = typeof(T) == typeof(float) ? 1e-4 : 1e-7;

    /// <summary>
    /// Gets the name of the parameter that will be considered the differentiation variable.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// The specific <see cref="ParameterExpression"/> instance, resolved once per <see cref="Derivate"/>
    /// call, that identifies the differentiation variable by reference rather than by name. Two distinct
    /// <see cref="ParameterExpression"/> objects may legally share the same <see cref="ParameterName"/>
    /// (e.g. an unrelated captured variable); comparing by reference instead of by name avoids treating
    /// such a foreign parameter as the differentiation variable. Set once, in the private constructor, on
    /// a fresh per-call worker instance (see <see cref="Derivate"/>).
    /// </summary>
    private readonly ParameterExpression targetParameter;

    /// <summary>
    /// Determines whether <paramref name="candidate"/> is the specific parameter instance resolved as
    /// the differentiation variable for the current call.
    /// </summary>
    private bool IsTargetParameter(ParameterExpression candidate) => ReferenceEquals(candidate, targetParameter);

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionDerivation{T}"/> class for the specified parameter.
    /// </summary>
    /// <param name="parameterName">Name of the variable with respect to which derivatives are computed.</param>
    public ExpressionDerivation(string parameterName) : this(parameterName, null!)
    {
    }

    /// <summary>
    /// Initializes a per-call worker instance with its target parameter already resolved.
    /// </summary>
    /// <param name="parameterName">Name of the variable with respect to which derivatives are computed.</param>
    /// <param name="targetParameter">
    /// The specific resolved <see cref="ParameterExpression"/> instance, or <see langword="null"/> for
    /// the publicly-constructed instance that has not yet performed a <see cref="Derivate"/> call.
    /// </param>
    private ExpressionDerivation(string parameterName, ParameterExpression targetParameter)
    {
        this.ParameterName = parameterName;
        this.targetParameter = targetParameter;
    }

    /// <summary>
    /// Builds the derivative of the provided lambda expression with respect to the configured parameter.
    /// </summary>
    /// <param name="e">Lambda expression to differentiate.</param>
    /// <returns>The simplified derivative expression.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no parameter named <see cref="ParameterName"/> is found in <paramref name="e"/>, or
    /// when more than one distinct parameter shares that name (an ambiguous match that cannot be
    /// resolved by name alone).
    /// </exception>
    public Expression Derivate(LambdaExpression e)
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
                "the differentiation variable is ambiguous. Use distinct parameter names.");
        }

        // A fresh worker instance isolates the resolved target parameter for this call: concurrent or
        // re-entrant calls on the same public ExpressionDerivation<T> instance no longer share mutable
        // state (see TODO-2026-07-11-pass3.md items #31 and #32).
        var worker = new ExpressionDerivation<T>(ParameterName, candidates[0]);
        return Expression.Lambda(worker.Transform(e.Body.Simplify()).Simplify(), e.Parameters);
    }

    /// <summary>
    /// Computes the derivative of a constant expression, which always yields zero.
    /// </summary>
    /// <param name="e">Source constant expression.</param>
    /// <param name="value">Value carried by the constant.</param>
    /// <returns>A constant expression representing zero.</returns>
    [ExpressionSignature(ExpressionType.Constant)]
    protected Expression Constant(
        ConstantExpression e,
        object value
    )
    {
        return ExpressionEx.CreateConstant(T.CreateChecked(0d));
    }

    /// <summary>
    /// Computes the derivative of a parameter expression by comparing it to the differentiation variable.
    /// </summary>
    /// <param name="e">Source parameter expression.</param>
    /// <returns>One when the parameter matches the configured variable; otherwise zero.</returns>
    [ExpressionSignature(ExpressionType.Parameter)]
    protected Expression Parameter(
        ParameterExpression e
    )
    {
        if (IsTargetParameter(e))
        {
            return ExpressionEx.CreateConstant(T.CreateChecked(1d));
        }
        else
        {
            return ExpressionEx.CreateConstant(T.CreateChecked(0d));
        }
    }

    /// <summary>
    /// Applies the derivative to a negated expression following the chain rule.
    /// </summary>
    /// <param name="e">Negation expression to transform.</param>
    /// <param name="operand">Operand of the negation.</param>
    /// <returns>The derivative of the negated operand.</returns>
    [ExpressionSignature(ExpressionType.Negate)]
    protected Expression Negate(
        UnaryExpression e,
        Expression operand
    )
    {
        return Expression.Negate(Transform(operand));
    }

    /// <summary>
    /// Differentiates the wrapped operand and re-applies the conversion's declared result type when the
    /// derivative's type does not already match it, so the derivative expression stays type-consistent
    /// with the original conversion node.
    /// </summary>
    /// <param name="e">Conversion expression to transform.</param>
    /// <param name="operand">Wrapped expression operand.</param>
    /// <returns>The derivative of the wrapped operand, converted back to <c>e.Type</c> if needed.</returns>
    [ExpressionSignature(ExpressionType.Convert)]
    protected Expression Convert(
        UnaryExpression e,
        Expression operand
    )
    {
        return PreserveConversion(e, Transform(operand), isChecked: false);
    }

    /// <summary>
    /// Differentiates the wrapped operand through a checked conversion. Checked conversions exist
    /// specifically to guard against narrowing/overflow, which has no well-defined symbolic derivative;
    /// only a trivial same-type checked conversion is passed through, anything else is rejected instead
    /// of silently stripped.
    /// </summary>
    /// <param name="e">Checked conversion expression to transform.</param>
    /// <param name="operand">Wrapped expression operand.</param>
    /// <returns>The derivative of the wrapped operand when the checked conversion is a same-type no-op.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the checked conversion actually changes the type (a narrowing or otherwise
    /// non-trivial conversion), since differentiating through it is not well-defined.
    /// </exception>
    [ExpressionSignature(ExpressionType.ConvertChecked)]
    protected Expression ConvertChecked(
        UnaryExpression e,
        Expression operand
    )
    {
        return PreserveConversion(e, Transform(operand), isChecked: true);
    }

    /// <summary>
    /// Reconciles the type of a transformed derivative with the declared result type of the original
    /// conversion node it replaces.
    /// </summary>
    /// <param name="original">The original <c>Convert</c>/<c>ConvertChecked</c> expression being replaced.</param>
    /// <param name="transformedOperand">The already-differentiated operand.</param>
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
    /// Applies the sum rule to differentiate the addition of two expressions.
    /// </summary>
    /// <param name="e">Addition expression to transform.</param>
    /// <param name="left">Left operand of the addition.</param>
    /// <param name="right">Right operand of the addition.</param>
    /// <returns>The sum of the derivatives of the operands.</returns>
    [ExpressionSignature(ExpressionType.Add)]
    protected Expression Add(
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
    /// Applies the difference rule to differentiate the subtraction of two expressions.
    /// </summary>
    /// <param name="e">Subtraction expression to transform.</param>
    /// <param name="left">Left operand of the subtraction.</param>
    /// <param name="right">Right operand of the subtraction.</param>
    /// <returns>The difference of the derivatives of the operands.</returns>
    [ExpressionSignature(ExpressionType.Subtract)]
    protected Expression Subtract(
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
    /// Applies the product rule to differentiate the multiplication of two expressions.
    /// </summary>
    /// <param name="e">Multiplication expression to transform.</param>
    /// <param name="left">Left operand of the multiplication.</param>
    /// <param name="right">Right operand of the multiplication.</param>
    /// <returns>The derivative computed using the product rule.</returns>
    [ExpressionSignature(ExpressionType.Multiply)]
    protected Expression Multiply(
        BinaryExpression e,
        Expression left,
        Expression right
    )
    {
        return Expression.Add(
            Expression.Multiply(Transform(left), right),
            Expression.Multiply(left, Transform(right))
        );
    }

    /// <summary>
    /// Applies the quotient rule to differentiate the division of two expressions.
    /// </summary>
    /// <param name="e">Division expression to transform.</param>
    /// <param name="left">Dividend expression.</param>
    /// <param name="right">Divisor expression.</param>
    /// <returns>The derivative computed using the quotient rule.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    protected Expression Divide(
        BinaryExpression e,
        Expression left,
        Expression right
    )
    {
        // Quotient rule: (f'g − fg') / g²
        return Expression.Divide(
            Expression.Subtract(
                Expression.Multiply(Transform(left), right),
                Expression.Multiply(left, Transform(right))),
            Expression.Power(right, ExpressionEx.CreateConstant(T.CreateChecked(2d))));
    }

    /// <summary>
    /// Differentiates a power expression with a constant exponent using the standard power rule.
    /// </summary>
    /// <param name="e">Power expression being transformed.</param>
    /// <param name="left">Base expression.</param>
    /// <param name="right">Constant exponent.</param>
    /// <returns>The derivative computed by the power rule.</returns>
    [ExpressionSignature(ExpressionType.Power)]
    protected Expression Power(
        BinaryExpression e,
        Expression left,
        ConstantExpression right)
    {
        return Expression.Multiply(
            right,
            Expression.Multiply(
                Expression.Power(left, Expression.Subtract(right, ExpressionEx.CreateConstant(T.CreateChecked(1d)))),
                Transform(left)
                )
            );
    }

    /// <summary>
    /// Differentiates a power expression that has an expression exponent by applying logarithmic differentiation.
    /// </summary>
    /// <param name="e">Power expression being transformed.</param>
    /// <param name="left">Base expression.</param>
    /// <param name="right">Exponent expression.</param>
    /// <returns>The derivative computed through logarithmic differentiation.</returns>
    [ExpressionSignature(ExpressionType.Power)]
    protected Expression Power(
        BinaryExpression e,
        Expression left,
        Expression right)
    {
        return
            Expression.Multiply(
                Expression.Power(
                    left,
                    Expression.Subtract(right, ExpressionEx.CreateConstant(T.CreateChecked(1d)))
                ),
                Expression.Add(
                    Expression.Multiply(right, Transform(left)),
                    Expression.Multiply(
                        left,
                        Expression.Multiply(
                            Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Log)), left),
                            Transform(right)
                        )
                    )

                )
            );
    }

    /// <summary>
    /// Differentiates an exponential call expression by applying the chain rule.
    /// </summary>
    /// <param name="e">Call expression describing the exponential invocation.</param>
    /// <param name="operand">Operand of the exponential call.</param>
    /// <returns>The derivative of the exponential expression.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Exp))]
    protected Expression Exp(
        MethodCallExpression e,
        Expression operand)
    {
        return
            Expression.Multiply(
                Transform(operand),
                Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Exp)), operand)
            );
    }

    /// <summary>
    /// Differentiates a natural logarithm call expression.
    /// </summary>
    /// <param name="e">Call expression describing the logarithm invocation.</param>
    /// <param name="operand">Operand of the logarithm call.</param>
    /// <returns>The derivative of the logarithm expression.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Log))]
    protected Expression LogMath(
        MethodCallExpression e,
        Expression operand)
    {
        return Expression.Divide(
            Transform(operand),
            operand
            );
    }

    /// <summary>
    /// Differentiates a base-10 logarithm call expression.
    /// </summary>
    /// <param name="e">Call expression describing the logarithm invocation.</param>
    /// <param name="operand">Operand of the logarithm call.</param>
    /// <returns>The derivative of the base-10 logarithm expression.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Log10))]
    protected Expression Log10(
            MethodCallExpression e,
            Expression operand)
    {
        return Expression.Divide(
            Transform(operand),
            Expression.Multiply(
                operand,
                ExpressionEx.CreateConstant(T.CreateChecked(double.Log(10d)))
            )
        );
    }

    /// <summary>
    /// Differentiates a sine call expression by applying the chain rule.
    /// </summary>
    /// <param name="e">Call expression describing the sine invocation.</param>
    /// <param name="operand">Operand of the sine call.</param>
    /// <returns>The derivative of the sine expression.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Sin))]
    protected Expression Sin(
        MethodCallExpression e,
        Expression operand)
    {
        return Expression.Multiply(
            Transform(operand),
            Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Cos)), operand));
    }

    /// <summary>
    /// Differentiates a cosine call expression by applying the chain rule and negating the sine.
    /// </summary>
    /// <param name="e">Call expression describing the cosine invocation.</param>
    /// <param name="operand">Operand of the cosine call.</param>
    /// <returns>The derivative of the cosine expression.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Cos))]
    protected Expression Cos(
        MethodCallExpression e,
        Expression operand)
    {
        return Expression.Negate(
            Expression.Multiply(
            Transform(operand),
            Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Sin)), operand)));
    }

    /// <summary>
    /// Differentiates a tangent call expression using the identity d/dx tan(f) = f'(x) / cos²(f(x)).
    /// </summary>
    /// <param name="e">Call expression describing the tangent invocation.</param>
    /// <param name="operand">Operand of the tangent call.</param>
    /// <returns>The derivative of the tangent expression.</returns>
    [ExpressionCallSignature(typeof(double), nameof(double.Tan))]
    protected Expression Tan(
        MethodCallExpression e,
        Expression operand)
    {
        // Applying Simplify to Sin(x)/Cos(x) can rebuild Tan(x), causing infinite recursion.
        // Use the direct identity instead: (tan(f))' = f'(x) / cos²(f(x))
        return Expression.Divide(
            Transform(operand),
            Expression.Power(
                Expression.Call(MathMethodResolver.Resolve<T>(nameof(double.Cos)), operand),
                ExpressionEx.CreateConstant(T.CreateChecked(2d))
            )
        );
    }

    /// <summary>
    /// Handles method-call derivatives that are not covered by specific derivative rules.
    /// For single-argument <see cref="double"/> functions, this applies a centered finite-difference
    /// approximation and multiplies by the inner derivative.
    /// </summary>
    /// <param name="e">Expression currently being finalized.</param>
    /// <param name="parameters">Prepared child expressions.</param>
    /// <returns>A derivative expression when supported.</returns>
    protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(parameters);

        if (e is MethodCallExpression methodCallExpression)
        {
            return DeriveUnknownMethodCall(methodCallExpression, parameters);
        }

        throw new NotSupportedException($"The expression '{e.NodeType}' is not supported by {nameof(ExpressionDerivation<T>)}.");
    }

    /// <summary>
    /// Applies a finite-difference fallback derivative for unknown single-argument <see cref="double"/> methods.
    /// </summary>
    /// <param name="methodCallExpression">Method call to derive.</param>
    /// <param name="parameters">Prepared arguments for the call.</param>
    /// <returns>The derivative expression using centered finite difference.</returns>
    private Expression DeriveUnknownMethodCall(MethodCallExpression methodCallExpression, Expression[] parameters)
    {
        if (methodCallExpression.Method.ReturnType != typeof(T)
            || methodCallExpression.Method.GetParameters().Length != 1
            || methodCallExpression.Method.GetParameters()[0].ParameterType != typeof(T))
        {
            throw new NotSupportedException($"No derivative rule is registered for '{methodCallExpression.Method}'.");
        }

        Expression operand = parameters[0];
        if (!ContainsParameter(operand))
        {
            return ExpressionEx.CreateConstant(T.CreateChecked(0d));
        }

        var epsilon = ExpressionEx.CreateConstant(T.CreateChecked(FiniteDifferenceEpsilon));
        var twoEpsilon = ExpressionEx.CreateConstant(T.CreateChecked(2.0 * FiniteDifferenceEpsilon));
        var operandDerivative = Transform(operand);

        var plus = Expression.Call(methodCallExpression.Method, Expression.Add(operand, epsilon));
        var minus = Expression.Call(methodCallExpression.Method, Expression.Subtract(operand, epsilon));
        var finiteDifference = Expression.Divide(Expression.Subtract(plus, minus), twoEpsilon);

        return Expression.Multiply(finiteDifference, operandDerivative);
    }

    /// <summary>
    /// Determines whether an expression depends on the configured differentiation parameter.
    /// </summary>
    /// <param name="expression">Expression to inspect.</param>
    /// <returns><see langword="true"/> when the expression depends on the target parameter; otherwise <see langword="false"/>.</returns>
    private bool ContainsParameter(Expression expression)
    {
        return expression switch
        {
            ParameterExpression parameterExpression => IsTargetParameter(parameterExpression),
            UnaryExpression unaryExpression => ContainsParameter(unaryExpression.Operand),
            BinaryExpression binaryExpression => ContainsParameter(binaryExpression.Left) || ContainsParameter(binaryExpression.Right),
            MethodCallExpression methodCallExpression => methodCallExpression.Arguments.Any(ContainsParameter),
            InvocationExpression invocationExpression => invocationExpression.Arguments.Any(ContainsParameter),
            _ => false
        };
    }

}



/// <summary>
/// Provides the historical non-generic entry-point for double-based derivation.
/// </summary>
public class ExpressionDerivation : ExpressionDerivation<double>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionDerivation"/> class.
    /// </summary>
    /// <param name="parameterName">Name of the variable with respect to which derivatives are computed.</param>
    public ExpressionDerivation(string parameterName)
        : base(parameterName)
    {
    }
}


#pragma warning restore CS8604
