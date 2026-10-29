using System.Linq.Expressions;
using System.Reflection;
using Utils.Mathematics.Expressions;

namespace Utils.Expressions;

/// <summary>
/// What evaluating an expression can do besides producing its value (ETC R1b, finding ETC-15). The categories are
/// independent: an expression that writes nothing can still throw, allocate or read mutable state.
/// </summary>
[Flags]
internal enum ExpressionEffects
{
    /// <summary>The evaluation produces a value and nothing else: it can be dropped, repeated or moved.</summary>
    None = 0,

    /// <summary>
    /// The evaluation reads a mutable location (a parameter or variable, or a field that is neither read-only nor constant).
    /// The value can change if a write runs between two evaluations, so such a read must not be moved across an
    /// <see cref="Observable"/> evaluation.
    /// </summary>
    ReadsMutableState = 1,

    /// <summary>
    /// The evaluation may throw: checked arithmetic, integer or decimal division, decimal overflow, array access, a null
    /// dereference, a cast or unboxing, a nullable value access, an array bound, or a library function that validates its
    /// arguments.
    /// </summary>
    MayThrow = 2,

    /// <summary>The evaluation allocates an object whose identity is observable (array, boxed value, delegate, object).</summary>
    Allocates = 4,

    /// <summary>
    /// The evaluation writes a variable or a location, transfers control (throw), or runs code that is not proven free of
    /// effects: a method outside the explicit allow-list of <see cref="ExpressionEffectAnalysis.MethodEffects"/>, a property
    /// getter, a constructor, a custom operator or conversion, an invocation, a dynamic operation, or an unknown or extension
    /// node.
    /// </summary>
    Observable = 8,
}

/// <summary>
/// Conservative classification of the effects of evaluating an expression tree, used where a symbolic rule of
/// <see cref="ExpressionSimplifier"/> decides to drop, repeat, merge or move evaluations (ETC R1b, compatibility entry
/// C-06, finding ETC-15).
/// </summary>
/// <remarks>
/// <para>
/// <b>Separation of concerns.</b> This classification never takes part in structural equality, hashing, canonical keys or
/// the comparison memos: two calls <c>f(x)</c> can be structurally equal (and keyed, compared and memoized as such) without
/// being algebraically interchangeable. It is consulted only by the decisions that rewrite evaluations: whether the rules
/// of a node may run (<see cref="HasInertOperands"/>) and which terms the canonicalization may regroup
/// (<see cref="IsInert"/>).
/// </para>
/// <para>
/// <b>Permissions.</b> A rewrite may drop an evaluation (<c>t * 0 -&gt; 0</c>, <c>t - t -&gt; 0</c>), repeat or merge it
/// (<c>t + t -&gt; 2 * t</c>, factoring, distribution), or move it (canonical reordering). Each needs its own permission:
/// <see cref="CanDiscard"/>, <see cref="CanRepeat"/>, <see cref="CanReorder"/>. Merging two evaluations of a deterministic
/// expression that may throw keeps the first exception, so <see cref="CanRepeat"/> accepts <see cref="ExpressionEffects.MayThrow"/>;
/// dropping or moving it may hide or swap an exception, so the other permissions do not. No permission accepts an
/// allocation (two allocations are distinct objects) or an observable effect. A rule whose effect on its operands is not
/// declared needs all three (<see cref="HasInertOperands"/>).
/// </para>
/// <para>
/// <b>Analysis.</b> The classification dispatches on the runtime class of each node and never reads a property of an
/// unknown or extension node (whose getters are user code): such a node is <see cref="ExpressionEffects.Observable"/>.
/// No node is evaluated, no getter, constructor or method is invoked, and a quoted expression or a lambda body is not
/// part of the evaluation of its parent (a lambda allocates a delegate; its body runs only when it is invoked). Inside an
/// expression operation the result of each node is cached by reference for the whole operation chain, and each node
/// analyzed for the first time counts one structural visit (ETC R1a-Security), so a shared subtree is analyzed once.
/// </para>
/// </remarks>
internal static class ExpressionEffectAnalysis
{
    /// <summary>
    /// The functions of <see cref="Math"/>, <see cref="MathF"/>, <see cref="double"/> and <see cref="float"/> accepted as
    /// total and pure when every parameter and the result have the same binary floating-point type: each is deterministic,
    /// reads and writes no state, and returns a value (possibly <c>NaN</c> or an infinity) for every input instead of
    /// throwing. Functions that validate their arguments (<c>Clamp</c>, <c>Sign</c>, <c>Round</c> with digits or a mode)
    /// are listed in <see cref="PartialNumericFunctions"/> instead; every other method is user code.
    /// </summary>
    private static readonly HashSet<string> TotalFloatingFunctions =
    [
        "Abs", "Acos", "Acosh", "Asin", "Asinh", "Atan", "Atan2", "Atanh", "BitDecrement", "BitIncrement", "Cbrt",
        "Ceiling", "CopySign", "Cos", "Cosh", "Exp", "Floor", "FusedMultiplyAdd", "Hypot", "IEEERemainder", "Log", "Log10",
        "Log2", "Max", "MaxMagnitude", "Min", "MinMagnitude", "Pow", "Round", "Sin", "Sinh", "Sqrt", "Tan", "Tanh",
        "Truncate",
    ];

    /// <summary>
    /// The functions of <see cref="Math"/>, <see cref="MathF"/>, <see cref="double"/> and <see cref="float"/> that are
    /// deterministic and pure but may throw: <c>Sign</c> (<see cref="ArithmeticException"/> on <c>NaN</c>), <c>Clamp</c>
    /// (<see cref="ArgumentException"/> when the bounds are reversed), <c>Round</c> with a number of digits or a rounding mode
    /// (argument validation), and <c>Abs</c> of a signed integer (<see cref="OverflowException"/> on the minimum value).
    /// </summary>
    private static readonly HashSet<string> PartialNumericFunctions = ["Abs", "Clamp", "Round", "Sign"];

    /// <summary>
    /// Whether an evaluation with <paramref name="effects"/> can be dropped without observable difference: no write, no
    /// exception, no allocation.
    /// </summary>
    /// <param name="effects">The effects.</param>
    /// <returns><see langword="true"/> when dropping is safe.</returns>
    internal static bool CanDiscard(ExpressionEffects effects) => (effects & ~ExpressionEffects.ReadsMutableState) == 0;

    /// <summary>
    /// Whether an evaluation with <paramref name="effects"/> can be performed a different number of times (at least once)
    /// without observable difference: no write and no allocation; an exception is raised by the first evaluation either way.
    /// </summary>
    /// <param name="effects">The effects.</param>
    /// <returns><see langword="true"/> when repeating or merging is safe.</returns>
    internal static bool CanRepeat(ExpressionEffects effects) => (effects & (ExpressionEffects.Observable | ExpressionEffects.Allocates)) == 0;

    /// <summary>
    /// Whether an evaluation with <paramref name="effects"/> can be moved relative to other such evaluations: no write, no
    /// allocation, and no exception whose order relative to another one could change.
    /// </summary>
    /// <param name="effects">The effects.</param>
    /// <returns><see langword="true"/> when reordering is safe.</returns>
    internal static bool CanReorder(ExpressionEffects effects) => (effects & ~ExpressionEffects.ReadsMutableState) == 0;

    /// <summary>Whether an evaluation with <paramref name="effects"/> can be dropped, repeated and moved (all three permissions).</summary>
    /// <param name="effects">The effects.</param>
    /// <returns><see langword="true"/> when every symbolic rewrite of the evaluation is safe.</returns>
    internal static bool IsInert(ExpressionEffects effects) => CanDiscard(effects) && CanRepeat(effects) && CanReorder(effects);

    /// <summary>
    /// Whether every symbolic rewrite (dropping, repeating and moving evaluations) is allowed on the operands of
    /// <paramref name="node"/>: <see cref="IsInert"/> over the effects of its directly evaluated children, the requirement of
    /// a rule whose own effect on its operands is not declared.
    /// </summary>
    /// <param name="node">The (prepared) node whose operands a rule may rewrite.</param>
    /// <returns><see langword="true"/> when the operands of <paramref name="node"/> are inert.</returns>
    internal static bool HasInertOperands(Expression node) => IsInert(ChildEffects(node));

    /// <summary>
    /// Returns the union of the effects of the direct children of <paramref name="node"/> that are evaluated with it (the
    /// operands of the rules that match it). A lambda's body and a quoted expression are not evaluated with their node;
    /// an unknown family reports no child (no simplifier rule matches it).
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The union of the effects of its evaluated children.</returns>
    internal static ExpressionEffects ChildEffects(Expression node) => node switch
    {
        UnaryExpression { NodeType: ExpressionType.Quote } => ExpressionEffects.None,
        UnaryExpression unary => Analyze(unary.Operand),
        BinaryExpression binary => Analyze(binary.Left) | Analyze(binary.Right) | Analyze(binary.Conversion),
        MethodCallExpression call => Analyze(call.Object) | AnalyzeEach(call.Arguments),
        ConditionalExpression conditional => Analyze(conditional.Test) | Analyze(conditional.IfTrue) | Analyze(conditional.IfFalse),
        InvocationExpression invocation => Analyze(invocation.Expression) | AnalyzeEach(invocation.Arguments),
        MemberExpression member => Analyze(member.Expression),
        TypeBinaryExpression typeBinary => Analyze(typeBinary.Expression),
        NewExpression creation => AnalyzeEach(creation.Arguments),
        NewArrayExpression array => AnalyzeEach(array.Expressions),
        _ => ExpressionEffects.None,
    };

    /// <summary>Classifies the effects of evaluating <paramref name="expression"/> (the node and every evaluated descendant).</summary>
    /// <param name="expression">The expression, or <see langword="null"/> for an absent optional child.</param>
    /// <returns>Its effects.</returns>
    /// <exception cref="ExpressionResourceLimitException">The structural budget of the running operation is exhausted.</exception>
    internal static ExpressionEffects Analyze(Expression? expression)
    {
        // Leaves are classified without touching the cache or the budget. A parameter or variable is a mutable location: an
        // assignment, an increment or a closure invoked by user code can write it.
        if (expression is null or ConstantExpression or DefaultExpression) return ExpressionEffects.None;
        if (expression is ParameterExpression) return ExpressionEffects.ReadsMutableState;

        ExpressionResourceBudget.Session? session = ExpressionResourceBudget.ActiveSession;
        Dictionary<Expression, ExpressionEffects>? cache = session is null
            ? null
            : session.EffectCache ??= new Dictionary<Expression, ExpressionEffects>(ReferenceEqualityComparer.Instance);
        if (cache is not null && cache.TryGetValue(expression, out ExpressionEffects cached)) return cached;

        // One structural visit per newly analyzed node, stack check included (ETC R1a-Security).
        ExpressionResourceBudget.ConsumeStructuralVisit();
        ExpressionEffects effects = AnalyzeNode(expression);
        if (cache is not null) cache[expression] = effects;
        return effects;
    }

    /// <summary>
    /// Returns the effects of the operator of <paramref name="binary"/> alone, without its operands: what reassociating or
    /// reordering a chain of such operators could change.
    /// </summary>
    /// <param name="binary">The binary node.</param>
    /// <returns>The effects of its operator.</returns>
    internal static ExpressionEffects OperatorEffects(BinaryExpression binary) => binary.NodeType switch
    {
        ExpressionType.Assign or ExpressionType.AddAssign or ExpressionType.AddAssignChecked or ExpressionType.SubtractAssign
            or ExpressionType.SubtractAssignChecked or ExpressionType.MultiplyAssign or ExpressionType.MultiplyAssignChecked
            or ExpressionType.DivideAssign or ExpressionType.ModuloAssign or ExpressionType.PowerAssign
            or ExpressionType.AndAssign or ExpressionType.OrAssign or ExpressionType.ExclusiveOrAssign
            or ExpressionType.LeftShiftAssign or ExpressionType.RightShiftAssign => ExpressionEffects.Observable,
        ExpressionType.Coalesce when binary.Conversion is not null => ExpressionEffects.Observable,
        ExpressionType.AddChecked or ExpressionType.SubtractChecked or ExpressionType.MultiplyChecked or ExpressionType.ArrayIndex
            => ExpressionEffects.MayThrow | OperatorMethodEffects(binary),
        ExpressionType.Divide or ExpressionType.Modulo => DivisionEffects(binary),
        ExpressionType.Add or ExpressionType.Subtract or ExpressionType.Multiply when binary.Type == typeof(decimal)
            => ExpressionEffects.MayThrow | OperatorMethodEffects(binary),
        _ => OperatorMethodEffects(binary),
    };

    /// <summary>
    /// Dispatches on the runtime class of <paramref name="expression"/>. Unknown classes (Block, Try, Loop, Index, MemberInit,
    /// ListInit, Dynamic, Extension, …) are <see cref="ExpressionEffects.Observable"/> without any property read.
    /// </summary>
    /// <param name="expression">A non-leaf node.</param>
    /// <returns>The effects of the node and of its evaluated children.</returns>
    private static ExpressionEffects AnalyzeNode(Expression expression) => expression switch
    {
        UnaryExpression unary => AnalyzeUnary(unary),
        BinaryExpression binary => OperatorEffects(binary) | Analyze(binary.Left) | Analyze(binary.Right),
        MethodCallExpression call => MethodEffects(call.Method) | Analyze(call.Object) | AnalyzeEach(call.Arguments),
        ConditionalExpression conditional => ChildEffects(conditional),
        MemberExpression member => AnalyzeMember(member),
        TypeBinaryExpression typeBinary => ChildEffects(typeBinary),
        NewExpression creation => creation.Constructor is null
            ? ExpressionEffects.None
            : ExpressionEffects.Observable | ExpressionEffects.Allocates | ChildEffects(creation),
        NewArrayExpression array => AnalyzeNewArray(array),
        LambdaExpression => ExpressionEffects.Allocates,
        _ => ExpressionEffects.Observable,
    };

    /// <summary>Classifies a unary node.</summary>
    /// <param name="unary">The node.</param>
    /// <returns>Its effects, operand included (except for a quoted expression, which is data).</returns>
    private static ExpressionEffects AnalyzeUnary(UnaryExpression unary)
    {
        ExpressionEffects own = unary.NodeType switch
        {
            ExpressionType.Quote => ExpressionEffects.None,
            ExpressionType.Throw or ExpressionType.PreIncrementAssign or ExpressionType.PreDecrementAssign
                or ExpressionType.PostIncrementAssign or ExpressionType.PostDecrementAssign => ExpressionEffects.Observable,
            ExpressionType.Convert => ConversionEffects(unary),
            ExpressionType.ConvertChecked or ExpressionType.NegateChecked => unary.Method is null ? ExpressionEffects.MayThrow : ExpressionEffects.Observable,
            ExpressionType.ArrayLength or ExpressionType.Unbox => ExpressionEffects.MayThrow,
            ExpressionType.TypeAs => unary.Operand.Type.IsValueType && !unary.Type.IsValueType ? ExpressionEffects.Allocates : ExpressionEffects.None,
            _ => unary.Method is null || ExpressionSimplifier.IsOrdinaryUnaryNegate(unary) ? ExpressionEffects.None : ExpressionEffects.Observable,
        };

        return unary.NodeType == ExpressionType.Quote ? own : own | Analyze(unary.Operand);
    }

    /// <summary>
    /// Classifies a <see cref="ExpressionType.Convert"/> node: numeric, enumeration and nullable-wrapping conversions are
    /// inert; boxing allocates; casts, unboxing and nullable unwrapping may throw; a decimal implicit conversion is exact, an
    /// explicit one may overflow; any other conversion method is user code.
    /// </summary>
    /// <param name="unary">The conversion.</param>
    /// <returns>The effects of the conversion itself.</returns>
    private static ExpressionEffects ConversionEffects(UnaryExpression unary)
    {
        if (unary.Method is not null)
        {
            if (unary.Method.DeclaringType != typeof(decimal)) return ExpressionEffects.Observable;
            return unary.Method.Name == "op_Implicit" ? ExpressionEffects.None : ExpressionEffects.MayThrow;
        }

        Type source = unary.Operand.Type;
        Type target = unary.Type;
        if (source == target) return ExpressionEffects.None;

        Type? targetUnderlying = Nullable.GetUnderlyingType(target);
        if (targetUnderlying is not null && Nullable.GetUnderlyingType(source) is null && IsPrimitiveValue(source) && IsPrimitiveValue(targetUnderlying))
        {
            return ExpressionEffects.None;
        }

        if (IsPrimitiveValue(source) && IsPrimitiveValue(target)) return ExpressionEffects.None;
        if (source.IsValueType && !target.IsValueType) return ExpressionEffects.Allocates;
        if (!source.IsValueType && !target.IsValueType && target.IsAssignableFrom(source)) return ExpressionEffects.None;

        return ExpressionEffects.MayThrow;
    }

    /// <summary>Whether <paramref name="type"/> is a primitive numeric, character, Boolean or enumeration type (unchecked conversions between them never throw).</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for such a type.</returns>
    private static bool IsPrimitiveValue(Type type) => type.IsPrimitive || type.IsEnum;

    /// <summary>
    /// Classifies a division or remainder: binary floating-point operators never throw; an integer or decimal one throws on a
    /// zero divisor (and an integer one on <c>MinValue / -1</c>), unless the divisor is a constant other than 0 and -1.
    /// </summary>
    /// <param name="binary">The division or remainder.</param>
    /// <returns>The effects of the operator itself.</returns>
    private static ExpressionEffects DivisionEffects(BinaryExpression binary)
    {
        ExpressionEffects method = OperatorMethodEffects(binary);
        Type type = binary.Type;
        if (type == typeof(double) || type == typeof(float) || type == typeof(Half)) return method;

        bool safeDivisor = binary.Right is ConstantExpression { Value: { } divisor } && divisor.GetType() == type && IsSafeDivisor(divisor);
        return safeDivisor ? method : method | ExpressionEffects.MayThrow;
    }

    /// <summary>Whether a boxed constant divisor can never make a division throw: neither zero nor minus one (and any nonzero decimal).</summary>
    /// <param name="divisor">The boxed divisor, of the division's type.</param>
    /// <returns><see langword="true"/> for a divisor that never throws.</returns>
    private static bool IsSafeDivisor(object divisor) => divisor switch
    {
        decimal value => value != 0m,
        int value => value is not (0 or -1),
        long value => value is not (0 or -1),
        short value => value is not (0 or -1),
        sbyte value => value is not (0 or -1),
        uint value => value != 0,
        ulong value => value != 0,
        ushort value => value != 0,
        byte value => value != 0,
        _ => false,
    };

    /// <summary>
    /// Classifies the operator method of a binary node: none or an ordinary arithmetic operator is inert, as is a predefined
    /// operator of <see cref="decimal"/> or <see cref="string"/> (their exceptions are reported by the callers); any other
    /// method is a custom operator, hence user code.
    /// </summary>
    /// <param name="binary">The node.</param>
    /// <returns>The effects of the operator method.</returns>
    private static ExpressionEffects OperatorMethodEffects(BinaryExpression binary)
    {
        MethodInfo? method = binary.Method;
        if (method is null || ExpressionSimplifier.IsOrdinaryBinaryArithmetic(binary)) return ExpressionEffects.None;

        return method.DeclaringType == typeof(decimal) || method.DeclaringType == typeof(string)
            ? ExpressionEffects.None
            : ExpressionEffects.Observable;
    }

    /// <summary>
    /// Classifies the execution of a method from an explicit allow-list, evaluated function by function. A static function of
    /// <see cref="Math"/>, <see cref="MathF"/>, <see cref="double"/> or <see cref="float"/> named in
    /// <see cref="TotalFloatingFunctions"/>, whose parameters and result all have the same binary floating-point type, is
    /// total and pure (<see cref="ExpressionEffects.None"/>). A static function of the same types named in
    /// <see cref="PartialNumericFunctions"/> (any numeric overload) is pure but may throw. Every other method, including
    /// every instance method and every user method, is <see cref="ExpressionEffects.Observable"/>.
    /// </summary>
    /// <param name="method">The called method.</param>
    /// <returns>The effects of the call itself.</returns>
    internal static ExpressionEffects MethodEffects(MethodInfo method)
    {
        Type? declaringType = method.DeclaringType;
        if (!method.IsStatic
            || method.ContainsGenericParameters
            || !(declaringType == typeof(Math) || declaringType == typeof(MathF) || declaringType == typeof(double) || declaringType == typeof(float)))
        {
            return ExpressionEffects.Observable;
        }

        ParameterInfo[] parameters = method.GetParameters();
        foreach (ParameterInfo parameter in parameters)
        {
            if (parameter.ParameterType.IsByRef || parameter.ParameterType.IsPointer) return ExpressionEffects.Observable;
        }

        Type returnType = method.ReturnType;
        if (TotalFloatingFunctions.Contains(method.Name) && IsFloating(returnType) && Array.TrueForAll(parameters, parameter => parameter.ParameterType == returnType))
        {
            return ExpressionEffects.None;
        }

        return PartialNumericFunctions.Contains(method.Name) ? ExpressionEffects.MayThrow : ExpressionEffects.Observable;
    }

    /// <summary>Whether <paramref name="type"/> is <see cref="double"/> or <see cref="float"/>.</summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for a binary floating-point type.</returns>
    private static bool IsFloating(Type type) => type == typeof(double) || type == typeof(float);

    /// <summary>
    /// Classifies a member access: a property getter is user code; a field read reads mutable state unless the field is
    /// read-only or constant, and an instance field may throw unless its receiver is a value type or a non-null constant.
    /// </summary>
    /// <param name="member">The node.</param>
    /// <returns>Its effects, receiver included.</returns>
    private static ExpressionEffects AnalyzeMember(MemberExpression member)
    {
        if (member.Member is not FieldInfo field) return ExpressionEffects.Observable | Analyze(member.Expression);

        ExpressionEffects own = field.IsInitOnly || field.IsLiteral ? ExpressionEffects.None : ExpressionEffects.ReadsMutableState;
        Expression? receiver = member.Expression;
        if (receiver is null) return own;

        if (!receiver.Type.IsValueType && receiver is not ConstantExpression { Value: not null }) own |= ExpressionEffects.MayThrow;
        return own | Analyze(receiver);
    }

    /// <summary>Classifies an array creation: it allocates; explicit bounds may throw (negative size).</summary>
    /// <param name="array">The node.</param>
    /// <returns>Its effects, elements or bounds included.</returns>
    private static ExpressionEffects AnalyzeNewArray(NewArrayExpression array)
    {
        ExpressionEffects own = array.NodeType == ExpressionType.NewArrayBounds
            ? ExpressionEffects.Allocates | ExpressionEffects.MayThrow
            : ExpressionEffects.Allocates;
        return own | AnalyzeEach(array.Expressions);
    }

    /// <summary>Returns the union of the effects of <paramref name="expressions"/>.</summary>
    /// <param name="expressions">The expressions.</param>
    /// <returns>The union of their effects.</returns>
    private static ExpressionEffects AnalyzeEach(IReadOnlyList<Expression> expressions)
    {
        ExpressionEffects effects = ExpressionEffects.None;
        for (int i = 0; i < expressions.Count; i++)
        {
            effects |= Analyze(expressions[i]);
        }

        return effects;
    }
}
