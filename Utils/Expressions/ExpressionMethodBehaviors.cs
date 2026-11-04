using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

namespace Utils.Expressions;

/// <summary>
/// Classifies the execution of one method, its arguments already evaluated, for <see cref="ExpressionBehaviorAnalysis"/>
/// (S5 P18 Architecture A). Three sources of knowledge, in this order: the explicit list of trusted built-in numeric
/// functions, the explicit list of known nondeterministic framework members, and the <see cref="DeterministicAttribute"/>
/// declarations of user methods. Every other method is <see cref="ExpressionBehaviors.UnknownCode"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Trusted numeric functions.</b> A method is trusted only when all of the following hold: it is declared by one of
/// the types of <see cref="TrustedDeclaringTypes"/> itself (<see cref="Math"/>, <see cref="MathF"/>, a binary floating-point
/// type, <see cref="decimal"/>, a built-in integer type or <see cref="BigInteger"/>); it is a public, static, non-generic
/// runtime method; every parameter and the result have a type of <see cref="SignatureTypes"/>, passed by value; and its name is
/// listed for the numeric family of its signature (<see cref="FamilyNames"/>). The name alone never grants trust: a user type
/// with a method <c>Sin</c>, a user implementation of <see cref="INumber{TSelf}"/> or <see cref="IFloatingPoint{TSelf}"/>, an
/// extension method or a default interface method is not declared by a trusted type and is not trusted. Each listed name is
/// either total (no exception for any argument) or partial (<see cref="ExpressionBehaviors.MayThrowArithmetic"/>) for its
/// family; a name is listed as total only when no argument can make it throw (checked by the unit tests on the edge values of
/// every trusted signature). Estimate functions (<c>ReciprocalEstimate</c>, <c>ReciprocalSqrtEstimate</c>), whose result may
/// depend on the code path, parsing and formatting (culture), and conversions to text are not listed.
/// </para>
/// <para>
/// <b>Known nondeterministic members.</b> Clocks (<see cref="DateTime.Now"/>, <see cref="DateTime.UtcNow"/>,
/// <see cref="DateTime.Today"/>, <see cref="DateTimeOffset.Now"/>, <see cref="DateTimeOffset.UtcNow"/>,
/// <see cref="Environment.TickCount"/>, <see cref="Environment.TickCount64"/>, <see cref="Stopwatch.GetTimestamp"/>),
/// <see cref="Guid.NewGuid"/>, and every public instance method of <see cref="Random"/> (which advances the generator and may be
/// overridden) are recognized by their declaring type and name and classified as nondeterministic. Their classification is
/// known, so it does not carry <see cref="ExpressionBehaviors.Unknown"/>; a <see cref="Random"/> member still carries every
/// other flag, because an override is user code.
/// </para>
/// <para>
/// <b>Declared methods.</b> See <see cref="DeterministicAttribute"/> for the contract and the conditions under which the
/// declaration is honored.
/// </para>
/// <para>
/// <b>No user code runs.</b> A <see cref="MethodInfo"/> that is not a runtime reflection object (a user subclass, whose
/// members are user code) is <see cref="ExpressionBehaviors.UnknownCode"/> without any member read and is not cached. Reading
/// the attribute of a runtime method does not construct any attribute instance.
/// </para>
/// </remarks>
internal static class ExpressionMethodBehaviors
{
    /// <summary>The numeric family of a trusted signature, which selects the list of trusted names.</summary>
    internal enum NumericFamily
    {
        /// <summary>Not a numeric type.</summary>
        None,

        /// <summary><see cref="double"/>, <see cref="float"/> and <see cref="Half"/>: IEEE 754 arithmetic, total except argument validation.</summary>
        Floating,

        /// <summary><see cref="decimal"/>: overflow and division by zero throw.</summary>
        Decimal,

        /// <summary>The built-in integer types: unchecked arithmetic wraps, division and checked arithmetic throw.</summary>
        Integer,

        /// <summary><see cref="BigInteger"/>: arbitrary precision; division, powers and shifts throw.</summary>
        BigInteger,
    }

    /// <summary>The types whose own static methods may be trusted.</summary>
    internal static readonly Type[] TrustedDeclaringTypes =
    [
        typeof(Math), typeof(MathF), typeof(double), typeof(float), typeof(Half), typeof(decimal), typeof(sbyte), typeof(byte),
        typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(nint), typeof(nuint),
        typeof(Int128), typeof(UInt128), typeof(BigInteger),
    ];

    /// <summary>The numeric family of each numeric type.</summary>
    private static readonly Dictionary<Type, NumericFamily> Families = new()
    {
        [typeof(double)] = NumericFamily.Floating,
        [typeof(float)] = NumericFamily.Floating,
        [typeof(Half)] = NumericFamily.Floating,
        [typeof(decimal)] = NumericFamily.Decimal,
        [typeof(sbyte)] = NumericFamily.Integer,
        [typeof(byte)] = NumericFamily.Integer,
        [typeof(short)] = NumericFamily.Integer,
        [typeof(ushort)] = NumericFamily.Integer,
        [typeof(int)] = NumericFamily.Integer,
        [typeof(uint)] = NumericFamily.Integer,
        [typeof(long)] = NumericFamily.Integer,
        [typeof(ulong)] = NumericFamily.Integer,
        [typeof(nint)] = NumericFamily.Integer,
        [typeof(nuint)] = NumericFamily.Integer,
        [typeof(Int128)] = NumericFamily.Integer,
        [typeof(UInt128)] = NumericFamily.Integer,
        [typeof(BigInteger)] = NumericFamily.BigInteger,
    };

    /// <summary>The types a trusted signature may use: the numeric types, <see cref="bool"/> and <see cref="MidpointRounding"/>.</summary>
    private static readonly HashSet<Type> SignatureTypes = [.. Families.Keys, typeof(bool), typeof(MidpointRounding)];

    /// <summary>The comparison operators, total in every family.</summary>
    private static readonly string[] ComparisonOperators =
        ["op_Equality", "op_Inequality", "op_LessThan", "op_LessThanOrEqual", "op_GreaterThan", "op_GreaterThanOrEqual"];

    /// <summary>
    /// The trusted names of each family, with their exception behavior: <see langword="true"/> for a total function,
    /// <see langword="false"/> for a partial one (<see cref="ExpressionBehaviors.MayThrowArithmetic"/>). <c>Round</c> is total
    /// only with a single parameter (see <see cref="ClassifyTrusted"/>); every <c>op_Checked*</c> operator and every
    /// <c>op_Explicit</c> conversion is partial.
    /// </summary>
    private static readonly Dictionary<NumericFamily, Dictionary<string, bool>> FamilyNames = new()
    {
        [NumericFamily.Floating] = Names(
            total:
            [
                "Abs", "Acos", "Acosh", "AcosPi", "Asin", "Asinh", "AsinPi", "Atan", "Atan2", "Atan2Pi", "Atanh", "AtanPi",
                "BitDecrement", "BitIncrement", "Cbrt", "Ceiling", "CopySign", "Cos", "Cosh", "CosPi", "DegreesToRadians", "Exp",
                "Exp10", "Exp10M1", "Exp2", "Exp2M1", "ExpM1", "Floor", "FusedMultiplyAdd", "Hypot", "IEEERemainder", "ILogB",
                "IsEvenInteger", "IsFinite", "IsInfinity", "IsInteger", "IsNaN", "IsNegative", "IsNegativeInfinity", "IsNormal",
                "IsOddInteger", "IsPositive", "IsPositiveInfinity", "IsPow2", "IsRealNumber", "IsSubnormal", "Lerp", "Log",
                "Log10", "Log10P1", "Log2", "Log2P1", "LogP1", "Max", "MaxMagnitude", "MaxMagnitudeNumber", "MaxNumber", "Min",
                "MinMagnitude", "MinMagnitudeNumber", "MinNumber", "RadiansToDegrees", "RootN", "Round", "ScaleB", "Sin", "Sinh",
                "SinPi", "Sqrt", "Tan", "Tanh", "TanPi", "Truncate", "op_Addition", "op_Subtraction", "op_Multiply",
                "op_Division", "op_Modulus", "op_UnaryNegation", "op_UnaryPlus", "op_Increment", "op_Decrement", "op_Implicit",
                .. ComparisonOperators,
            ],
            partial: ["Clamp", "Sign", "op_Explicit"]),
        [NumericFamily.Decimal] = Names(
            total:
            [
                "Abs", "Ceiling", "Compare", "CopySign", "Equals", "Floor", "IsCanonical", "IsEvenInteger", "IsInteger",
                "IsNegative", "IsOddInteger", "IsPositive", "Max", "MaxMagnitude", "Min", "MinMagnitude", "Negate", "Round",
                "Sign", "Truncate", "op_UnaryNegation", "op_UnaryPlus", "op_Implicit", .. ComparisonOperators,
            ],
            partial:
            [
                "Add", "Clamp", "Divide", "Multiply", "Remainder", "Subtract", "op_Addition", "op_Subtraction", "op_Multiply",
                "op_Division", "op_Modulus", "op_Increment", "op_Decrement", "op_Explicit",
            ]),
        [NumericFamily.Integer] = Names(
            total:
            [
                "BigMul", "IsEvenInteger", "IsNegative", "IsOddInteger", "IsPositive", "IsPow2", "LeadingZeroCount", "Max",
                "MaxMagnitude", "Min", "MinMagnitude", "PopCount", "RotateLeft", "RotateRight", "Sign", "TrailingZeroCount",
                "op_Addition", "op_Subtraction", "op_Multiply", "op_UnaryNegation", "op_UnaryPlus", "op_OnesComplement",
                "op_Increment", "op_Decrement", "op_BitwiseAnd", "op_BitwiseOr", "op_ExclusiveOr", "op_LeftShift",
                "op_RightShift", "op_UnsignedRightShift", "op_Implicit", .. ComparisonOperators,
            ],
            partial: ["Abs", "Clamp", "CopySign", "Log2", "op_Division", "op_Modulus", "op_Explicit"]),
        [NumericFamily.BigInteger] = Names(
            total:
            [
                "Abs", "Add", "Compare", "CopySign", "GreatestCommonDivisor", "IsEvenInteger", "IsNegative", "IsOddInteger",
                "IsPositive", "IsPow2", "Log", "Log10", "Max", "MaxMagnitude", "Min", "MinMagnitude", "Multiply", "Negate",
                "Subtract", "op_Addition", "op_Subtraction", "op_Multiply", "op_UnaryNegation", "op_UnaryPlus",
                "op_OnesComplement", "op_Increment", "op_Decrement", "op_BitwiseAnd", "op_BitwiseOr", "op_ExclusiveOr",
                "op_Implicit", .. ComparisonOperators,
            ],
            partial:
            [
                "Clamp", "Divide", "Log2", "ModPow", "Pow", "Remainder", "op_Division", "op_Modulus", "op_Explicit",
                "op_LeftShift", "op_RightShift", "op_UnsignedRightShift",
            ]),
    };

    /// <summary>The behavior of a known nondeterministic member that reads a clock or a fresh identifier source.</summary>
    private const ExpressionBehaviors ClockBehaviors = ExpressionBehaviors.Nondeterministic | ExpressionBehaviors.ReadsMutableState;

    /// <summary>The behavior of a <see cref="Random"/> member: every "may" flag but <see cref="ExpressionBehaviors.Unknown"/>.</summary>
    private const ExpressionBehaviors RandomBehaviors = ExpressionBehaviors.UnknownCode & ~ExpressionBehaviors.Unknown;

    /// <summary>The known nondeterministic static members, by declaring type and method name.</summary>
    private static readonly Dictionary<(Type Type, string Name), ExpressionBehaviors> KnownNondeterministic = new()
    {
        [(typeof(DateTime), "get_Now")] = ClockBehaviors,
        [(typeof(DateTime), "get_UtcNow")] = ClockBehaviors,
        [(typeof(DateTime), "get_Today")] = ClockBehaviors,
        [(typeof(DateTimeOffset), "get_Now")] = ClockBehaviors,
        [(typeof(DateTimeOffset), "get_UtcNow")] = ClockBehaviors,
        [(typeof(Environment), "get_TickCount")] = ClockBehaviors,
        [(typeof(Environment), "get_TickCount64")] = ClockBehaviors,
        [(typeof(Stopwatch), "GetTimestamp")] = ClockBehaviors,
        [(typeof(Guid), "NewGuid")] = ClockBehaviors,
    };

    /// <summary>The classification of every runtime method met so far; it depends on the method's metadata only.</summary>
    private static readonly ConcurrentDictionary<MethodInfo, ExpressionBehaviors> Cache = new();

    /// <summary>Builds the name table of a family.</summary>
    /// <param name="total">The total functions.</param>
    /// <param name="partial">The partial functions.</param>
    /// <returns>The table: name to totality.</returns>
    private static Dictionary<string, bool> Names(string[] total, string[] partial)
    {
        var names = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (string name in total) names.Add(name, true);
        foreach (string name in partial) names.Add(name, false);
        return names;
    }

    /// <summary>Classifies the execution of <paramref name="method"/>, its receiver and arguments already evaluated.</summary>
    /// <param name="method">The method.</param>
    /// <returns>Its behavior.</returns>
    internal static ExpressionBehaviors Classify(MethodInfo method)
        => IsRuntimeReflectionObject(method) ? Cache.GetOrAdd(method, static m => ClassifyCore(m)) : ExpressionBehaviors.UnknownCode;

    /// <summary>
    /// Whether <paramref name="member"/> is a reflection object of the runtime itself: its members are framework code, while
    /// those of a user subclass of <see cref="MemberInfo"/> are user code that must not run.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns><see langword="true"/> for a runtime reflection object.</returns>
    internal static bool IsRuntimeReflectionObject(MemberInfo member) => member.GetType().Assembly == typeof(object).Assembly;

    /// <summary>Computes the classification returned by <see cref="Classify"/>.</summary>
    /// <param name="method">A runtime method.</param>
    /// <returns>Its behavior.</returns>
    private static ExpressionBehaviors ClassifyCore(MethodInfo method)
    {
        if (ClassifyTrusted(method) is { } trusted) return trusted;

        Type? declaringType = method.DeclaringType;
        if (declaringType is not null)
        {
            if (method.IsStatic && KnownNondeterministic.TryGetValue((declaringType, method.Name), out ExpressionBehaviors known)) return known;
            if (declaringType == typeof(Random) && !method.IsStatic) return RandomBehaviors;
        }

        return ClassifyDeclared(method);
    }

    /// <summary>
    /// Classifies a trusted built-in numeric function, or returns <see langword="null"/> when <paramref name="method"/> is not one
    /// (see the remarks of the class for the conditions).
    /// </summary>
    /// <param name="method">A runtime method.</param>
    /// <returns><see cref="ExpressionBehaviors.None"/> for a total function, <see cref="ExpressionBehaviors.MayThrowArithmetic"/> for a partial one, or <see langword="null"/>.</returns>
    internal static ExpressionBehaviors? ClassifyTrusted(MethodInfo method)
    {
        Type? declaringType = method.DeclaringType;
        if (declaringType is null
            || !method.IsStatic
            || !method.IsPublic
            || method.IsGenericMethod
            || Array.IndexOf(TrustedDeclaringTypes, declaringType) < 0
            || !SignatureTypes.Contains(method.ReturnType))
        {
            return null;
        }

        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length == 0) return null;
        foreach (ParameterInfo parameter in parameters)
        {
            if (!SignatureTypes.Contains(parameter.ParameterType)) return null;
        }

        // The family of a numeric type's own method is its own; that of a Math/MathF overload is its first parameter's.
        Type familyType = declaringType == typeof(Math) || declaringType == typeof(MathF) ? parameters[0].ParameterType : declaringType;
        if (!Families.TryGetValue(familyType, out NumericFamily family)) return null;

        string name = method.Name;
        if (name.StartsWith("op_Checked", StringComparison.Ordinal)) return ExpressionBehaviors.MayThrowArithmetic;
        if (!FamilyNames[family].TryGetValue(name, out bool total)) return null;

        // Round(x) is total; Round(x, digits), Round(x, mode) and Round(x, digits, mode) validate their arguments.
        if (name == "Round" && parameters.Length > 1) total = false;
        return total ? ExpressionBehaviors.None : ExpressionBehaviors.MayThrowArithmetic;
    }

    /// <summary>
    /// Classifies a method through its <see cref="DeterministicAttribute"/> declaration (see that attribute for the contract
    /// and the conditions), or as <see cref="ExpressionBehaviors.UnknownCode"/> when it has none or it cannot be honored.
    /// </summary>
    /// <param name="method">A runtime method.</param>
    /// <returns>Its behavior.</returns>
    private static ExpressionBehaviors ClassifyDeclared(MethodInfo method)
    {
        if (!method.IsDefined(typeof(DeterministicAttribute), inherit: false)) return ExpressionBehaviors.UnknownCode;

        Type? declaringType = method.DeclaringType;
        if (declaringType is null || method.ContainsGenericParameters || method.IsAbstract) return ExpressionBehaviors.UnknownCode;

        // The declaration must be bound to the code that runs: no override can replace a static, non-virtual or final method,
        // nor a method of a sealed or value type.
        bool fixedDispatch = method.IsStatic || !method.IsVirtual || method.IsFinal || declaringType.IsSealed || declaringType.IsValueType;
        if (!fixedDispatch) return ExpressionBehaviors.UnknownCode;

        ExpressionBehaviors behaviors = ExpressionBehaviors.ReliesOnDeclaration | ExpressionBehaviors.MayThrowUserCode;
        if (!method.IsStatic && MayReferenceMutableState(declaringType)) behaviors |= ExpressionBehaviors.ReadsMutableState;

        foreach (ParameterInfo parameter in method.GetParameters())
        {
            Type type = parameter.ParameterType;
            if (type.IsByRef || type.IsPointer || IsCodeType(type)) return ExpressionBehaviors.UnknownCode;
            if (MayReferenceMutableState(type)) behaviors |= ExpressionBehaviors.ReadsMutableState;
        }

        Type returnType = method.ReturnType;
        if (returnType.IsByRef || returnType.IsPointer) return ExpressionBehaviors.UnknownCode;
        if (returnType != typeof(void) && !returnType.IsValueType) behaviors |= ExpressionBehaviors.Allocates;
        return behaviors;
    }

    /// <summary>
    /// Whether a value of <paramref name="type"/> is code a declaration cannot speak for: a delegate, which the method may
    /// invoke, or an expression tree, which it may compile and run.
    /// </summary>
    /// <param name="type">The parameter type.</param>
    /// <returns><see langword="true"/> for a delegate or expression type.</returns>
    private static bool IsCodeType(Type type) => typeof(Delegate).IsAssignableFrom(type) || typeof(Expression).IsAssignableFrom(type);

    /// <summary>
    /// Whether a value of <paramref name="type"/> may give access to state that can change between two calls: any reference
    /// type but <see cref="string"/>, and any value type that holds such a reference in one of its instance fields
    /// (recursively). The primitive, enumeration, built-in numeric and time types never do, possibly nullable.
    /// </summary>
    /// <param name="type">The receiver or parameter type.</param>
    /// <returns><see langword="true"/> when the value may reference mutable state.</returns>
    internal static bool MayReferenceMutableState(Type type) => MayReferenceMutableState(type, 0);

    /// <summary>The nesting of value-type fields beyond which a value type is assumed to reference mutable state.</summary>
    private const int MaxFieldNesting = 8;

    /// <summary>The value types already examined by <see cref="MayReferenceMutableState(Type)"/>.</summary>
    private static readonly ConcurrentDictionary<Type, bool> ValueTypeStates = new();

    /// <summary>Same as <see cref="MayReferenceMutableState(Type)"/>, at a given nesting of value-type fields.</summary>
    /// <param name="type">The type.</param>
    /// <param name="nesting">The number of enclosing value types.</param>
    /// <returns><see langword="true"/> when the value may reference mutable state.</returns>
    private static bool MayReferenceMutableState(Type type, int nesting)
    {
        Type value = Nullable.GetUnderlyingType(type) ?? type;
        if (value.IsPrimitive || value.IsEnum || value == typeof(string)) return false;
        if (value == typeof(decimal) || value == typeof(Half) || value == typeof(Int128) || value == typeof(UInt128)
            || value == typeof(BigInteger) || value == typeof(DateTime) || value == typeof(DateTimeOffset)
            || value == typeof(TimeSpan) || value == typeof(Guid))
        {
            return false;
        }

        if (!value.IsValueType || value.IsByRefLike || value.ContainsGenericParameters || nesting >= MaxFieldNesting) return true;
        if (ValueTypeStates.TryGetValue(value, out bool cached)) return cached;

        bool result = false;
        foreach (FieldInfo field in value.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType.IsPointer || MayReferenceMutableState(field.FieldType, nesting + 1))
            {
                result = true;
                break;
            }
        }

        // Only a complete answer is cached: a deeply nested type may still be answered at a lower nesting later.
        if (nesting == 0) ValueTypeStates.TryAdd(value, result);
        return result;
    }

    /// <summary>
    /// Enumerates the trusted numeric functions of the running runtime with their classification, for the documentation and
    /// the tests: every method of <see cref="TrustedDeclaringTypes"/> that <see cref="ClassifyTrusted"/> accepts.
    /// </summary>
    /// <returns>The trusted methods and their behaviors.</returns>
    internal static IEnumerable<(MethodInfo Method, ExpressionBehaviors Behaviors)> EnumerateTrusted()
    {
        foreach (Type type in TrustedDeclaringTypes)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (ClassifyTrusted(method) is { } behaviors) yield return (method, behaviors);
            }
        }
    }
}
