using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using Utils.Expressions;

namespace Utils.Mathematics.Expressions;

public partial class ExpressionSimplifier
{
    #region Power

    /// <summary>
    /// Replaces calls to <see cref="IPowerFunctions{T}.Pow"/> with the equivalent <see cref="Expression.Power(Expression, Expression)"/> node.
    /// </summary>
    /// <param name="e">Original call expression.</param>
    /// <param name="left">Base argument of the power operation.</param>
    /// <param name="right">Exponent argument of the power operation.</param>
    /// <returns>The transformed power expression.</returns>
    [ExpressionCallSignature(typeof(double), nameof(IPowerFunctions<double>.Pow))]
    protected Expression PowerConvertionNumber1(Expression e, Expression left, Expression right)
    {
        return TransformCore(Expression.Power(left, right));
    }

    /// <summary>
    /// Replaces calls to <see cref="Math.Pow(double, double)"/> with an <see cref="Expression.Power(Expression, Expression)"/> node.
    /// </summary>
    /// <param name="e">Original call expression.</param>
    /// <param name="left">Base argument of the power operation.</param>
    /// <param name="right">Exponent argument of the power operation.</param>
    /// <returns>The transformed power expression.</returns>
    [ExpressionCallSignature(typeof(Math), nameof(Math.Pow))]
    protected Expression PowerConvertionNumber2(Expression e, Expression left, Expression right)
    {
        return TransformCore(Expression.Power(left, right));
    }
    #endregion

    #region logarithm 

    /// <summary>
    /// Simplifies the addition of two natural logarithms by turning it into a single logarithm of a product.
    /// </summary>
    /// <param name="e">The original binary addition expression.</param>
    /// <param name="left">Left logarithm call.</param>
    /// <param name="right">Right logarithm call.</param>
    /// <returns>A combined logarithm expression when the arguments are compatible; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Add)]
    protected Expression LogarithmSimplificationAddNumber(Expression e,
            [ExpressionCallSignature(typeof(double), nameof(ILogarithmicFunctions<double>.Log))] MethodCallExpression left,
            [ExpressionCallSignature(typeof(double), nameof(ILogarithmicFunctions<double>.Log))] MethodCallExpression right)
    {
        if (e is not BinaryExpression outer || !IsOrdinaryBinaryArithmetic(outer)) return null;
        if (!CanCombineUnaryLogCalls(left, right))
        {
            return null;
        }

        return Expression.Call(left.Method, TransformCore(Expression.Multiply(left.Arguments[0], right.Arguments[0])));
    }

    /// <summary>
    /// Simplifies the subtraction of two natural logarithms by turning it into a single logarithm of a quotient.
    /// </summary>
    /// <param name="e">The original binary subtraction expression.</param>
    /// <param name="left">Left logarithm call.</param>
    /// <param name="right">Right logarithm call.</param>
    /// <returns>A combined logarithm expression when the arguments are compatible; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Subtract)]
    protected Expression LogarithmSimplificationSubstractNumber(Expression e,
            [ExpressionCallSignature(typeof(double), nameof(ILogarithmicFunctions<double>.Log))] MethodCallExpression left,
            [ExpressionCallSignature(typeof(double), nameof(ILogarithmicFunctions<double>.Log))] MethodCallExpression right)
    {
        if (e is not BinaryExpression outer || !IsOrdinaryBinaryArithmetic(outer)) return null;
        if (!CanCombineUnaryLogCalls(left, right))
        {
            return null;
        }

        return Expression.Call(left.Method, TransformCore(Expression.Divide(left.Arguments[0], right.Arguments[0])));
    }

    /// <summary>
    /// Simplifies the addition of two base-10 logarithms by turning it into a single logarithm of a product.
    /// </summary>
    /// <param name="e">The original binary addition expression.</param>
    /// <param name="left">Left base-10 logarithm call.</param>
    /// <param name="right">Right base-10 logarithm call.</param>
    /// <returns>A combined logarithm expression when the arguments are compatible; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Add)]
    protected Expression Logarithm10SimplificationAddNumber(Expression e,
            [ExpressionCallSignature(typeof(double), nameof(ILogarithmicFunctions<double>.Log10))] MethodCallExpression left,
            [ExpressionCallSignature(typeof(double), nameof(ILogarithmicFunctions<double>.Log10))] MethodCallExpression right)
    {
        if (e is not BinaryExpression outer || !IsOrdinaryBinaryArithmetic(outer)) return null;
        if (!CanCombineUnaryLogCalls(left, right))
        {
            return null;
        }

        return Expression.Call(left.Method, TransformCore(Expression.Multiply(left.Arguments[0], right.Arguments[0])));
    }

    /// <summary>
    /// Simplifies the subtraction of two base-10 logarithms by turning it into a single logarithm of a quotient.
    /// </summary>
    /// <param name="e">The original binary subtraction expression.</param>
    /// <param name="left">Left base-10 logarithm call.</param>
    /// <param name="right">Right base-10 logarithm call.</param>
    /// <returns>A combined logarithm expression when the arguments are compatible; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Subtract)]
    protected Expression Logarithm10SimplificationSubstractNumber(Expression e,
            [ExpressionCallSignature(typeof(double), nameof(ILogarithmicFunctions<double>.Log10))] MethodCallExpression left,
            [ExpressionCallSignature(typeof(double), nameof(ILogarithmicFunctions<double>.Log10))] MethodCallExpression right)
    {
        if (e is not BinaryExpression outer || !IsOrdinaryBinaryArithmetic(outer)) return null;
        if (!CanCombineUnaryLogCalls(left, right))
        {
            return null;
        }

        return Expression.Call(left.Method, TransformCore(Expression.Divide(left.Arguments[0], right.Arguments[0])));
    }

    /// <summary>
    /// Determines whether two logarithm calls are the same concrete unary overload understood by the
    /// existing combination rules. Rejecting other shapes prevents overload bases or mixed call families
    /// from being discarded during the binary rewrite.
    /// </summary>
    /// <param name="left">The left logarithm call.</param>
    /// <param name="right">The right logarithm call.</param>
    /// <returns><see langword="true"/> only for matching concrete unary logarithm methods.</returns>
    private static bool CanCombineUnaryLogCalls(MethodCallExpression left, MethodCallExpression right)
    {
        // S5 A-10: the equality of a reflection object that is not a runtime one is never called.
        return left.Arguments.Count == 1
            && right.Arguments.Count == 1
            && ExpressionReflectionProvenance.IsRuntime(right.Method)
            && ExpressionReflectionProvenance.IsSameMember(left.Method, right.Method);
    }

    #endregion

    #region trigonometry

    /// <summary>
    /// The public static <c>Cos</c>, <c>Sin</c> and <c>Tan</c> functions of one numeric type <c>T</c>, each of signature
    /// <c>T F(T)</c>, or <see langword="null"/> for a function the type does not declare so (an explicit interface
    /// implementation, another signature): implementing <see cref="ITrigonometricFunctions{TSelf}"/> does not guarantee them.
    /// </summary>
    private sealed class TrigonometricMethods
    {
        /// <summary>Gets the cosine of the type, or <see langword="null"/>.</summary>
        public MethodInfo? Cos { get; }

        /// <summary>Gets the sine of the type, or <see langword="null"/>.</summary>
        public MethodInfo? Sin { get; }

        /// <summary>Gets the tangent of the type, or <see langword="null"/>.</summary>
        public MethodInfo? Tan { get; }

        /// <summary>Resolves the functions of <paramref name="type"/>.</summary>
        /// <param name="type">A runtime numeric type (the type of a node the rules matched).</param>
        public TrigonometricMethods(Type type)
        {
            Cos = Resolve(type, nameof(ITrigonometricFunctions<double>.Cos));
            Sin = Resolve(type, nameof(ITrigonometricFunctions<double>.Sin));
            Tan = Resolve(type, nameof(ITrigonometricFunctions<double>.Tan));
        }

        /// <summary>The public static method <c>T name(T)</c> of <paramref name="type"/>, or <see langword="null"/>.</summary>
        /// <param name="type">The type.</param>
        /// <param name="name">The function name.</param>
        /// <returns>The method, or <see langword="null"/> when the type declares none of that signature and return type.</returns>
        private static MethodInfo? Resolve(Type type, string name)
            => type.GetMethod(name, BindingFlags.Static | BindingFlags.Public, [type]) is { } method && method.ReturnType == type ? method : null;
    }

    private IDictionary<Type, TrigonometricMethods> TrigonometricMethodsByType { get; }
        = new CachedLoader<Type, TrigonometricMethods>((Type type, out TrigonometricMethods methods) => { methods = new TrigonometricMethods(type); return true; });

    /// <summary>
    /// The argument of <paramref name="call"/> when it calls exactly the function <paramref name="function"/> selects among the
    /// functions of its own result type, with one argument: the shape every trigonometric rule rewrites.
    /// </summary>
    /// <param name="call">A call the signature of a rule matched (by declaring type and name only).</param>
    /// <param name="function">Selects the expected function among the functions of the type.</param>
    /// <param name="argument">The argument of the call.</param>
    /// <returns><see langword="true"/> when <paramref name="call"/> is that function of a type that declares it.</returns>
    /// <remarks>
    /// The signatures match any public method named <c>Sin</c>, <c>Cos</c> or <c>Tan</c> of a type implementing
    /// <see cref="ITrigonometricFunctions{TSelf}"/>: an overload of another arity, or a homonym with other values (a degree-based
    /// sine), would otherwise be rewritten with the functions of the result type. The provenance of the method is checked
    /// before any of its members is read (S5 A-10): <see cref="ExpressionReflectionProvenance.IsSameMember"/> compares with the
    /// runtime method only, so an impersonating reflection object never matches. The type read first is the call node's
    /// result type, computed by the framework from a runtime method.
    /// </remarks>
    private bool TryGetTrigonometricArgument(MethodCallExpression call, Func<TrigonometricMethods, MethodInfo?> function, out Expression argument)
    {
        argument = null!;
        if (call.Object is not null || !ExpressionReflectionProvenance.IsRuntime(call.Method)) return false;
        if (function(TrigonometricMethodsByType[call.Type]) is not { } expected || !ExpressionReflectionProvenance.IsSameMember(call.Method, expected)) return false;
        if (call.Arguments.Count != 1) return false;
        argument = call.Arguments[0];
        return true;
    }

    /// <summary>
    /// Whether <paramref name="exponent"/> is the <see cref="double"/> constant <c>2</c>: the only exponent of the identity
    /// <c>sin²(x) + cos²(x) = 1</c>. Any other exponent (a parameter, a call, a computed expression, another constant) is not.
    /// </summary>
    /// <param name="exponent">The exponent of an ordinary power (ordinary powers are <see cref="double"/> only).</param>
    /// <returns><see langword="true"/> for the constant <c>2</c>.</returns>
    private static bool IsSquareExponent(Expression exponent)
        => exponent is ConstantExpression { Value: double value } constant && constant.Type == typeof(double) && value == 2;

    /// <summary>
    /// Applies the trigonometric identity <c>sin²(x) + cos²(x) = 1</c> when possible.
    /// </summary>
    /// <param name="e">The original addition expression.</param>
    /// <param name="left">Left power expression.</param>
    /// <param name="right">Right power expression.</param>
    /// <returns>A constant expression representing one when the identity applies; otherwise <see langword="null"/>.</returns>
    /// <remarks>
    /// Every shape is checked before any value is read (S5A-01: a power whose exponent was not a constant, <c>x^x + x^y</c>,
    /// threw <see cref="NullReferenceException"/>): two ordinary powers of one type, both exponents the constant <c>2</c>, both
    /// bases calls of the sine and the cosine of that type, with one argument each, then the structural identity of the
    /// arguments.
    /// </remarks>
    [ExpressionSignature(ExpressionType.Add)]
    protected Expression AdditionOfCos2andSin2Number(Expression e,
            [ExpressionSignature(ExpressionType.Power)] BinaryExpression left,
            [ExpressionSignature(ExpressionType.Power)] BinaryExpression right)
    {
        if (e is not BinaryExpression outer || !IsOrdinaryBinaryArithmetic(outer)) return null;
        if (!IsOrdinaryBinaryArithmetic(left) || !IsOrdinaryBinaryArithmetic(right)) return null;
        if (right.Type != left.Type) return null;
        if (!IsSquareExponent(left.Right) || !IsSquareExponent(right.Right)) return null;
        if (left.Left is not MethodCallExpression leftCall || right.Left is not MethodCallExpression rightCall) return null;

        if (!(TryGetTrigonometricArgument(leftCall, static methods => methods.Sin, out Expression leftArgument)
                && TryGetTrigonometricArgument(rightCall, static methods => methods.Cos, out Expression rightArgument))
            && !(TryGetTrigonometricArgument(leftCall, static methods => methods.Cos, out leftArgument)
                && TryGetTrigonometricArgument(rightCall, static methods => methods.Sin, out rightArgument)))
        {
            return null;
        }

        if (ExpressionComparer.EqualsWithinOperation(leftArgument, rightArgument)) return Expression.Constant(Convert.ChangeType(1.0, left.Type));

        return null;
    }

    /// <summary>
    /// The common argument of two trigonometric calls when they are exactly the functions <paramref name="leftFunction"/> and
    /// <paramref name="rightFunction"/> of one type (<see cref="TryGetTrigonometricArgument"/>) and their arguments are equal
    /// for the comparer, together with the function <paramref name="resultFunction"/> of that type the rule builds.
    /// </summary>
    /// <param name="left">The left call.</param>
    /// <param name="right">The right call.</param>
    /// <param name="leftFunction">The function <paramref name="left"/> must call.</param>
    /// <param name="rightFunction">The function <paramref name="right"/> must call.</param>
    /// <param name="resultFunction">The function of the result.</param>
    /// <param name="argument">The argument of <paramref name="left"/>.</param>
    /// <param name="result">The function of the result.</param>
    /// <returns><see langword="true"/> when the rule applies.</returns>
    private bool TryMatchTrigonometricPair(MethodCallExpression left, MethodCallExpression right,
        Func<TrigonometricMethods, MethodInfo?> leftFunction, Func<TrigonometricMethods, MethodInfo?> rightFunction, Func<TrigonometricMethods, MethodInfo?> resultFunction,
        out Expression argument, out MethodInfo result)
    {
        result = null!;
        if (!TryGetTrigonometricArgument(left, leftFunction, out argument)
            || !TryGetTrigonometricArgument(right, rightFunction, out Expression rightArgument)
            || left.Type != right.Type
            || resultFunction(TrigonometricMethodsByType[left.Type]) is not { } function)
        {
            return false;
        }

        result = function;
        return ExpressionComparer.EqualsWithinOperation(argument, rightArgument);
    }

    /// <summary>
    /// Rewrites <c>sin(x) / cos(x)</c> into <c>tan(x)</c> when the arguments match.
    /// </summary>
    /// <param name="e">The original division expression.</param>
    /// <param name="left">Sine call in the numerator.</param>
    /// <param name="right">Cosine call in the denominator.</param>
    /// <returns>An expression that calls tangent when possible; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    protected Expression DivisionOfCosAndSinNumber(Expression e,
            [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Sin))] MethodCallExpression left,
            [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Cos))] MethodCallExpression right)
    {
        if (e is not BinaryExpression outer || !IsOrdinaryBinaryArithmetic(outer)) return null;
        if (TryMatchTrigonometricPair(left, right, static methods => methods.Sin, static methods => methods.Cos, static methods => methods.Tan, out Expression argument, out MethodInfo tan))
        {
            return Expression.Call(null, tan, argument);
        }

        return null;
    }

    /// <summary>
    /// Rewrites <c>cos(x) / sin(x)</c> into <c>cot(x)</c> (expressed as <c>1 / tan(x)</c>) when the arguments match.
    /// </summary>
    /// <param name="e">The original division expression.</param>
    /// <param name="left">Cosine call in the numerator.</param>
    /// <param name="right">Sine call in the denominator.</param>
    /// <returns>An expression rewriting the division when possible; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    protected Expression DivisionOfSinAndCosNumber(Expression e,
    [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Cos))] MethodCallExpression left,
    [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Sin))] MethodCallExpression right)
    {
        if (e is not BinaryExpression outer || !IsOrdinaryBinaryArithmetic(outer)) return null;
        if (TryMatchTrigonometricPair(left, right, static methods => methods.Cos, static methods => methods.Sin, static methods => methods.Tan, out Expression argument, out MethodInfo tan))
        {
            return Expression.Divide(Expression.Constant(Convert.ChangeType(1.0, left.Type)), Expression.Call(null, tan, argument));
        }

        return null;
    }

    /// <summary>
    /// Rewrites <c>cos(x) * tan(x)</c> into <c>sin(x)</c> when the arguments match.
    /// </summary>
    /// <param name="e">The original multiplication expression.</param>
    /// <param name="left">Cosine call.</param>
    /// <param name="right">Tangent call.</param>
    /// <returns>A sine call when possible; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Multiply)]
    protected Expression MultiplicationOfCosAndTanNumber(Expression e,
            [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Cos))] MethodCallExpression left,
            [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Tan))] MethodCallExpression right)
    {
        if (e is not BinaryExpression outer || !IsOrdinaryBinaryArithmetic(outer)) return null;
        if (TryMatchTrigonometricPair(left, right, static methods => methods.Cos, static methods => methods.Tan, static methods => methods.Sin, out Expression argument, out MethodInfo sin))
        {
            return Expression.Call(null, sin, argument);
        }

        return null;
    }

    /// <summary>
    /// Rewrites <c>tan(x) * cos(x)</c> into <c>sin(x)</c> when the arguments match.
    /// </summary>
    /// <param name="e">The original multiplication expression.</param>
    /// <param name="left">Tangent call.</param>
    /// <param name="right">Cosine call.</param>
    /// <returns>A sine call when possible; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Multiply)]
    protected Expression MultiplicationOfTanAndCosNumber(Expression e,
    [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Tan))] MethodCallExpression left,
    [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Cos))] MethodCallExpression right)
    {
        if (e is not BinaryExpression outer || !IsOrdinaryBinaryArithmetic(outer)) return null;
        if (TryMatchTrigonometricPair(left, right, static methods => methods.Tan, static methods => methods.Cos, static methods => methods.Sin, out Expression argument, out MethodInfo sin))
        {
            return Expression.Call(null, sin, argument);
        }

        return null;
    }


    /// <summary>
    /// Rewrites <c>sin(x) / tan(x)</c> into <c>cos(x)</c> when the arguments match.
    /// </summary>
    /// <param name="e">The original division expression.</param>
    /// <param name="left">Sine call.</param>
    /// <param name="right">Tangent call.</param>
    /// <returns>A cosine call when possible; otherwise <see langword="null"/>.</returns>
    [ExpressionSignature(ExpressionType.Divide)]
    protected Expression DivisionOfSinAndTanNumber(Expression e,
    [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Sin))] MethodCallExpression left,
    [ExpressionCallSignature(typeof(ITrigonometricFunctions<>), nameof(ITrigonometricFunctions<double>.Tan))] MethodCallExpression right)
    {
        if (e is not BinaryExpression outer || !IsOrdinaryBinaryArithmetic(outer)) return null;
        if (TryMatchTrigonometricPair(left, right, static methods => methods.Sin, static methods => methods.Tan, static methods => methods.Cos, out Expression argument, out MethodInfo cos))
        {
            return Expression.Call(null, cos, argument);
        }

        return null;
    }

    #endregion

}
