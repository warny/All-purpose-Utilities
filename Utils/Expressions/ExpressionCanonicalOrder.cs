using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Utils.Objects;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// Builds a deterministic, structural, lexical-scope-aware canonical ordering key for an
/// <see cref="Expression"/>, used by <see cref="ExpressionSimplifier"/>'s additive/multiplicative
/// canonicalization (roadmap stage S4) instead of <see cref="Expression.ToString()"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this builds.</b> <see cref="BuildKey(Expression, IReadOnlyList{ParameterExpression[]})"/>
/// produces the COMPLETE structural identity/order for the node families <see cref="ExpressionComparer"/>
/// already understands structurally: <see cref="LambdaExpression"/>, <see cref="ParameterExpression"/>,
/// <see cref="ConstantExpression"/>, <see cref="UnaryExpression"/>, <see cref="BinaryExpression"/>,
/// <see cref="MethodCallExpression"/> and <see cref="MemberExpression"/>. This is deliberately the FULL
/// identity, not the coarser additive-grouping projection <see cref="ExpressionSimplifier"/> intentionally
/// keeps separate (see its "Additive grouping" region) — a <c>Power(MethodCall, exponent)</c> term's
/// complete key still distinguishes the exponent and the exact method, even though the grouping key
/// deliberately ignores the exponent to cluster same-argument trigonometric calls together.
/// </para>
/// <para>
/// <b>Bound parameters.</b> A <see cref="ParameterExpression"/> bound by a <see cref="LambdaExpression"/>
/// visible during this key's own construction — either because it was supplied via <c>enclosingScopes</c>
/// (the lexical scope enclosing the term being keyed, captured from <see cref="ExpressionSimplifier"/>'s
/// ambient traversal — see that type's "Structural canonicalization (S4)" region) or because a
/// <see cref="LambdaExpression"/> nested inside the term itself was encountered while walking down to it —
/// is encoded by a De-Bruijn-style (relative depth, declaration position, declared type) triple, never by
/// <see cref="ParameterExpression.Name"/> and never by <see cref="System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(object)"/>
/// or object identity. Two alpha-equivalent lambdas therefore produce IDENTICAL keys regardless of
/// parameter names or which <see cref="ParameterExpression"/> instances they use, and two distinct
/// same-named parameters in the same lambda remain distinguishable by declaration position.
/// </para>
/// <para>
/// <b>Free parameters.</b> A <see cref="ParameterExpression"/> not bound by any lexical scope this key
/// construction can see is "free". Two distinct free parameters have no name-independent, cross-tree
/// structural total order (unlike a bound parameter's declaration position, nothing else about a free
/// parameter is a canonical property of the tree being keyed) — inventing one would misrepresent structural
/// identity. <see cref="KeyNode.CompareTo(KeyNode?)"/> therefore compares two free-parameter keys only by
/// declared <see cref="Type"/> and otherwise reports them EQUAL (a tie), relying on the caller's stable sort
/// (<see cref="Enumerable.OrderBy{TSource, TKey}(IEnumerable{TSource}, Func{TSource, TKey})"/> is
/// documented stable) to preserve their original relative source order rather than inventing one. This
/// mirrors <see cref="ExpressionComparer"/>'s own free-parameter policy (reference equality, no ordering
/// claim beyond it).
/// </para>
/// <para>
/// <b>Unsupported node kinds.</b> A node kind other than the seven listed above (for example
/// <see cref="ConditionalExpression"/> or an <see cref="ExpressionType.Extension"/> node) is never
/// inspected beyond a simple <see langword="is"/> pattern match against those seven kinds — in particular
/// its <see cref="object.ToString"/> is never called. Its key always compares as a tie against any other
/// unsupported node's key, for the same "preserve stable source order, do not invent an order" reason as
/// free parameters; unlike free parameters, however, this key is deliberately never used to claim two
/// unsupported nodes are the same GROUP — <see cref="ExpressionSimplifier"/>'s additive grouping uses a
/// separate, dedicated equality (<see cref="ExpressionComparer.StructuralEqualsRaw(Expression?, Expression?)"/>,
/// wrapped with a same-instance shortcut) that never merges two distinct unsupported-kind instances.
/// </para>
/// <para>
/// <b>Deterministic reflection-based ordering, never hash-based.</b> <see cref="Type"/>,
/// <see cref="MethodInfo"/> and <see cref="MemberInfo"/> comparisons in this file are built from actual
/// reflection metadata (namespace/name text, declaring type, generic arity and argument identities,
/// parameter/return types, static/instance) compared ordinally, with a final tie-break using
/// <see cref="MemberInfo.MetadataToken"/>/<see cref="Module"/>/<see cref="System.Reflection.Assembly"/>
/// identity for the (practically unreachable, since C# does not allow two members with an identical
/// signature) case where every other dimension ties. None of <see cref="object.GetHashCode"/>,
/// <see cref="System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(object)"/>,
/// <see cref="HashCode.ToHashCode"/>, object reference order, or any <see cref="object.ToString"/> override
/// participates in ordering.
/// </para>
/// </remarks>
internal static class ExpressionCanonicalOrder
{
    /// <summary>Fixed rank for <see cref="NullKey"/>: an absent optional sub-expression sorts before every real node kind.</summary>
    private const int RankNull = -1;

    /// <summary>Fixed rank for <see cref="ConstantKey"/>.</summary>
    private const int RankConstant = 0;

    /// <summary>Fixed rank for <see cref="ParameterKey"/>.</summary>
    private const int RankParameter = 1;

    /// <summary>Fixed rank for <see cref="UnaryKey"/>.</summary>
    private const int RankUnary = 2;

    /// <summary>Fixed rank for <see cref="BinaryKey"/>.</summary>
    private const int RankBinary = 3;

    /// <summary>Fixed rank for <see cref="MethodCallKey"/>.</summary>
    private const int RankMethodCall = 4;

    /// <summary>Fixed rank for <see cref="MemberKey"/>.</summary>
    private const int RankMember = 5;

    /// <summary>Fixed rank for <see cref="LambdaKey"/>.</summary>
    private const int RankLambda = 6;

    /// <summary>Fixed rank for <see cref="UnsupportedKey"/>: sorts after every real supported node kind.</summary>
    private const int RankUnsupported = 7;

    /// <summary>
    /// Builds the complete structural canonical-order key for <paramref name="expression"/>.
    /// </summary>
    /// <param name="expression">The (already simplified) expression to key, or <see langword="null"/>.</param>
    /// <param name="enclosingScopes">
    /// The lexical parameter-binding scopes enclosing <paramref name="expression"/>, outermost first —
    /// typically a snapshot of <see cref="ExpressionSimplifier"/>'s ambient lexical scope stack at the
    /// moment canonicalization runs. A <see cref="ParameterExpression"/> found in none of these scopes,
    /// nor in any <see cref="LambdaExpression"/> nested within <paramref name="expression"/> itself, is
    /// treated as free — see this type's remarks.
    /// </param>
    /// <returns>A comparable, deterministic structural key.</returns>
    internal static KeyNode BuildKey(Expression? expression, IReadOnlyList<ParameterExpression[]> enclosingScopes)
    {
        var scopes = new List<ParameterExpression[]>(enclosingScopes.Count + 2);
        scopes.AddRange(enclosingScopes);
        return Build(expression, scopes);
    }

    /// <summary>
    /// Compares two expressions' complete structural canonical-order keys under the same enclosing scope.
    /// </summary>
    /// <param name="x">The first (already simplified) expression, or <see langword="null"/>.</param>
    /// <param name="y">The second (already simplified) expression, or <see langword="null"/>.</param>
    /// <param name="enclosingScopes">See <see cref="BuildKey(Expression, IReadOnlyList{ParameterExpression[]})"/>.</param>
    /// <returns>A negative value if <paramref name="x"/> sorts before <paramref name="y"/>, zero if tied, positive otherwise.</returns>
    internal static int Compare(Expression? x, Expression? y, IReadOnlyList<ParameterExpression[]> enclosingScopes)
        => BuildKey(x, enclosingScopes).CompareTo(BuildKey(y, enclosingScopes));

    /// <summary>Dispatches to the node-family-specific <c>Build*</c> helper, or <see cref="UnsupportedKey"/> for any node kind not among the seven this class understands.</summary>
    /// <param name="e">The (already simplified) sub-expression to key, or <see langword="null"/>.</param>
    /// <param name="scopes">The local, per-<see cref="BuildKey(Expression, IReadOnlyList{ParameterExpression[]})"/>-call working scope list (ambient snapshot plus any lambda encountered so far during this walk).</param>
    /// <returns>The resulting key.</returns>
    private static KeyNode Build(Expression? e, List<ParameterExpression[]> scopes) => e switch
    {
        null => NullKey.Instance,
        ConstantExpression ce => BuildConstant(ce),
        ParameterExpression pe => BuildParameter(pe, scopes),
        UnaryExpression ue => BuildUnary(ue, scopes),
        BinaryExpression be => BuildBinary(be, scopes),
        MethodCallExpression mce => BuildMethodCall(mce, scopes),
        MemberExpression me => BuildMember(me, scopes),
        LambdaExpression le => BuildLambda(le, scopes),
        _ => UnsupportedKey.Instance,
    };

    /// <summary>Builds the key for a <see cref="ConstantExpression"/>: an exact numeric value for a native numeric type, or the declared type plus boxed value otherwise.</summary>
    /// <param name="ce">The constant to key.</param>
    /// <returns>The resulting <see cref="ConstantKey"/>.</returns>
    private static KeyNode BuildConstant(ConstantExpression ce)
        => new ConstantKey(ce.Type, ce.Value, TryGetNumericValue(ce.Type, ce.Value));

    /// <summary>
    /// Builds the exact numeric value for a constant, preferring <paramref name="declaredType"/> but
    /// falling back to <paramref name="value"/>'s own RUNTIME type when the declared type does not
    /// identify it as numeric — e.g. <c>Expression.Constant(1, typeof(object))</c>, where
    /// <see cref="ConstantExpression.Type"/> is <see cref="object"/> but the boxed value is an
    /// <see cref="int"/>. <see cref="object.GetType"/> is always safe to call (never user-overridable),
    /// so this never executes constant-value-owned code.
    /// </summary>
    /// <param name="declaredType">The constant's declared <see cref="Expression.Type"/>.</param>
    /// <param name="value">The constant's boxed value, or <see langword="null"/>.</param>
    /// <returns>The exact numeric value, when either the declared or the runtime type is native numeric; otherwise <see langword="null"/>.</returns>
    private static ExpressionComparer.ExactNumericValue? TryGetNumericValue(Type declaredType, object? value)
    {
        if (value is null) return null;
        if (Types.Number.Contains(declaredType)) return ExpressionComparer.ExactNumericValue.FromBoxed(declaredType, value);

        Type runtimeType = value.GetType();
        return Types.Number.Contains(runtimeType) ? ExpressionComparer.ExactNumericValue.FromBoxed(runtimeType, value) : null;
    }

    /// <summary>Builds the key for a <see cref="ParameterExpression"/>: a bound (depth, position, type) triple if found in <paramref name="scopes"/> (innermost scope searched first), or a free-parameter key otherwise.</summary>
    /// <param name="pe">The parameter to key.</param>
    /// <param name="scopes">The active scope list, outermost first.</param>
    /// <returns>The resulting <see cref="ParameterKey"/>.</returns>
    private static KeyNode BuildParameter(ParameterExpression pe, List<ParameterExpression[]> scopes)
    {
        for (int i = scopes.Count - 1; i >= 0; i--)
        {
            int index = Array.IndexOf(scopes[i], pe);
            if (index >= 0)
            {
                int depth = scopes.Count - 1 - i;
                return ParameterKey.Bound(depth, index, pe.Type);
            }
        }

        return ParameterKey.Free(pe.Type);
    }

    /// <summary>Builds the key for a <see cref="UnaryExpression"/>: node type, result type, operator method, lifting flags, then the operand's key.</summary>
    /// <param name="ue">The unary expression to key.</param>
    /// <param name="scopes">The active scope list, outermost first.</param>
    /// <returns>The resulting <see cref="UnaryKey"/>.</returns>
    private static KeyNode BuildUnary(UnaryExpression ue, List<ParameterExpression[]> scopes)
        => new UnaryKey(ue.NodeType, ue.Type, ue.Method, ue.IsLifted, ue.IsLiftedToNull, Build(ue.Operand, scopes));

    /// <summary>Builds the key for a <see cref="BinaryExpression"/>: node type, result type, operator method, lifting flags, left/right/conversion keys.</summary>
    /// <param name="be">The binary expression to key.</param>
    /// <param name="scopes">The active scope list, outermost first.</param>
    /// <returns>The resulting <see cref="BinaryKey"/>.</returns>
    private static KeyNode BuildBinary(BinaryExpression be, List<ParameterExpression[]> scopes)
        => new BinaryKey(
            be.NodeType,
            be.Type,
            be.Method,
            be.IsLifted,
            be.IsLiftedToNull,
            Build(be.Left, scopes),
            Build(be.Right, scopes),
            Build(be.Conversion, scopes));

    /// <summary>Builds the key for a <see cref="MethodCallExpression"/>: exact method, receiver key, then each argument's key in order.</summary>
    /// <param name="mce">The method call to key.</param>
    /// <param name="scopes">The active scope list, outermost first.</param>
    /// <returns>The resulting <see cref="MethodCallKey"/>.</returns>
    private static KeyNode BuildMethodCall(MethodCallExpression mce, List<ParameterExpression[]> scopes)
    {
        var arguments = new KeyNode[mce.Arguments.Count];
        for (int i = 0; i < arguments.Length; i++)
        {
            arguments[i] = Build(mce.Arguments[i], scopes);
        }

        return new MethodCallKey(mce.Method, Build(mce.Object, scopes), arguments);
    }

    /// <summary>Builds the key for a <see cref="MemberExpression"/>: exact member, then the receiver's key.</summary>
    /// <param name="me">The member access to key.</param>
    /// <param name="scopes">The active scope list, outermost first.</param>
    /// <returns>The resulting <see cref="MemberKey"/>.</returns>
    private static KeyNode BuildMember(MemberExpression me, List<ParameterExpression[]> scopes)
        => new MemberKey(me.Member, Build(me.Expression, scopes));

    /// <summary>Builds the key for a <see cref="LambdaExpression"/>: delegate type, <see cref="LambdaExpression.TailCall"/>, parameter types, then the body's key under a pushed local scope frame for this lambda's own parameters.</summary>
    /// <param name="le">The lambda to key.</param>
    /// <param name="scopes">The active scope list (mutated locally: this lambda's parameters are pushed before, and popped after, keying the body).</param>
    /// <returns>The resulting <see cref="LambdaKey"/>.</returns>
    private static KeyNode BuildLambda(LambdaExpression le, List<ParameterExpression[]> scopes)
    {
        ParameterExpression[] parameters = le.Parameters.ToArray();
        Type[] parameterTypes = new Type[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            parameterTypes[i] = parameters[i].Type;
        }

        scopes.Add(parameters);
        KeyNode body;
        try
        {
            body = Build(le.Body, scopes);
        }
        finally
        {
            scopes.RemoveAt(scopes.Count - 1);
        }

        return new LambdaKey(le.Type, le.TailCall, parameterTypes, body);
    }

    /// <summary>
    /// Deterministically compares two <see cref="Type"/> values by reflection metadata: namespace/name
    /// text, then (for a constructed generic type) its generic arguments recursively. Never uses
    /// <see cref="object.GetHashCode"/>, <see cref="Type.ToString"/>, or object identity/reference order as
    /// the decision itself, only as an initial short-circuit and a last-resort deterministic tie-break.
    /// </summary>
    /// <param name="a">The first type, or <see langword="null"/>.</param>
    /// <param name="b">The second type, or <see langword="null"/>.</param>
    /// <returns>A negative value if <paramref name="a"/> sorts before <paramref name="b"/>, zero if equal, positive otherwise.</returns>
    internal static int CompareType(Type? a, Type? b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a is null) return -1;
        if (b is null) return 1;

        if (a.IsArray || b.IsArray)
        {
            if (a.IsArray != b.IsArray) return a.IsArray ? -1 : 1;

            // A single-dimensional zero-based ("SZ"/vector) array (int[]) and a general
            // single-dimensional array with explicit bounds (int[*]) both report GetArrayRank() == 1
            // and the same element type, but are distinct CLR types (Type.MakeArrayType() vs
            // Type.MakeArrayType(1)) that are not interchangeable at the IL/reflection level. Compare
            // IsSZArray first so the two are never conflated, including when nested inside a
            // constructed generic argument (e.g. List<int[]> vs List<int[*]>), since that case reaches
            // this same branch through the generic-argument recursion below.
            bool aIsSzArray = a.IsSZArray;
            bool bIsSzArray = b.IsSZArray;
            if (aIsSzArray != bIsSzArray) return aIsSzArray ? -1 : 1;

            int rankCompare = a.GetArrayRank().CompareTo(b.GetArrayRank());
            if (rankCompare != 0) return rankCompare;
            return CompareType(a.GetElementType(), b.GetElementType());
        }

        Type da = a.IsGenericType ? a.GetGenericTypeDefinition() : a;
        Type db = b.IsGenericType ? b.GetGenericTypeDefinition() : b;

        int namespaceCompare = string.CompareOrdinal(da.Namespace ?? string.Empty, db.Namespace ?? string.Empty);
        if (namespaceCompare != 0) return namespaceCompare;

        int nameCompare = string.CompareOrdinal(da.Name, db.Name);
        if (nameCompare != 0) return nameCompare;

        bool aGeneric = a.IsGenericType;
        bool bGeneric = b.IsGenericType;
        if (aGeneric != bGeneric) return aGeneric ? 1 : -1;

        if (aGeneric)
        {
            Type[] aArguments = a.GetGenericArguments();
            Type[] bArguments = b.GetGenericArguments();
            int arityCompare = aArguments.Length.CompareTo(bArguments.Length);
            if (arityCompare != 0) return arityCompare;

            for (int i = 0; i < aArguments.Length; i++)
            {
                int argumentCompare = CompareType(aArguments[i], bArguments[i]);
                if (argumentCompare != 0) return argumentCompare;
            }
        }

        if (a == b) return 0;

        // Every other dimension ties: this happens only for two genuinely distinct Type instances that
        // otherwise share namespace, name and generic shape (in practice unreachable for real CLR types,
        // since the CLR does not allow two distinct types with an identical fully-qualified name to
        // coexist in the same load context). System.Type derives from MemberInfo, so the shared,
        // non-throwing tie-break below applies here too.
        return CompareFinalMemberTiebreak(a, b);
    }

    /// <summary>Deterministically compares two <see cref="MethodInfo"/> values; see <see cref="CompareType(Type, Type)"/> for the underlying policy.</summary>
    /// <param name="a">The first method, or <see langword="null"/>.</param>
    /// <param name="b">The second method, or <see langword="null"/>.</param>
    /// <returns>A negative value if <paramref name="a"/> sorts before <paramref name="b"/>, zero if equal, positive otherwise.</returns>
    internal static int CompareMethod(MethodInfo? a, MethodInfo? b)
    {
        if (a is null && b is null) return 0;
        if (a is null) return -1;
        if (b is null) return 1;
        if (a == b) return 0;

        int declaringTypeCompare = CompareType(a.DeclaringType, b.DeclaringType);
        if (declaringTypeCompare != 0) return declaringTypeCompare;

        int nameCompare = string.CompareOrdinal(a.Name, b.Name);
        if (nameCompare != 0) return nameCompare;

        int staticCompare = a.IsStatic.CompareTo(b.IsStatic);
        if (staticCompare != 0) return staticCompare;

        Type[] aGenericArguments = a.IsGenericMethod ? a.GetGenericArguments() : Array.Empty<Type>();
        Type[] bGenericArguments = b.IsGenericMethod ? b.GetGenericArguments() : Array.Empty<Type>();
        int arityCompare = aGenericArguments.Length.CompareTo(bGenericArguments.Length);
        if (arityCompare != 0) return arityCompare;
        for (int i = 0; i < aGenericArguments.Length; i++)
        {
            int genericArgumentCompare = CompareType(aGenericArguments[i], bGenericArguments[i]);
            if (genericArgumentCompare != 0) return genericArgumentCompare;
        }

        ParameterInfo[] aParameters = a.GetParameters();
        ParameterInfo[] bParameters = b.GetParameters();
        int parameterCountCompare = aParameters.Length.CompareTo(bParameters.Length);
        if (parameterCountCompare != 0) return parameterCountCompare;
        for (int i = 0; i < aParameters.Length; i++)
        {
            int parameterCompare = CompareType(aParameters[i].ParameterType, bParameters[i].ParameterType);
            if (parameterCompare != 0) return parameterCompare;
        }

        int returnTypeCompare = CompareType(a.ReturnType, b.ReturnType);
        if (returnTypeCompare != 0) return returnTypeCompare;

        return CompareFinalMemberTiebreak(a, b);
    }

    /// <summary>Deterministically compares two field/property <see cref="MemberInfo"/> values reached through a <see cref="MemberExpression"/>.</summary>
    /// <param name="a">The first member.</param>
    /// <param name="b">The second member.</param>
    /// <returns>A negative value if <paramref name="a"/> sorts before <paramref name="b"/>, zero if equal, positive otherwise.</returns>
    internal static int CompareMember(MemberInfo a, MemberInfo b)
    {
        if (a == b) return 0;

        int kindCompare = ((int)a.MemberType).CompareTo((int)b.MemberType);
        if (kindCompare != 0) return kindCompare;

        int declaringTypeCompare = CompareType(a.DeclaringType, b.DeclaringType);
        if (declaringTypeCompare != 0) return declaringTypeCompare;

        int nameCompare = string.CompareOrdinal(a.Name, b.Name);
        if (nameCompare != 0) return nameCompare;

        Type? aValueType = a switch { PropertyInfo p => p.PropertyType, FieldInfo f => f.FieldType, _ => null };
        Type? bValueType = b switch { PropertyInfo p => p.PropertyType, FieldInfo f => f.FieldType, _ => null };
        int valueTypeCompare = CompareType(aValueType, bValueType);
        if (valueTypeCompare != 0) return valueTypeCompare;

        return CompareFinalMemberTiebreak(a, b);
    }

    /// <summary>
    /// A final, deterministic (never hash-based) tie-break for two <see cref="MemberInfo"/> values
    /// (including <see cref="Type"/>, which derives from <see cref="MemberInfo"/>) that already tied on
    /// every earlier structural dimension. Every access is guarded and falls back to the next dimension,
    /// and ultimately to a conservative tie, rather than throwing: <see cref="MemberInfo.MetadataToken"/>
    /// throws <see cref="InvalidOperationException"/> for some dynamically-generated members (for example
    /// an unbaked <see cref="System.Reflection.Emit.DynamicMethod"/>), and this key must never let
    /// <c>Simplify()</c> fail merely because two otherwise-identical-looking members happen to be such a
    /// member.
    /// </summary>
    /// <param name="a">The first member or type.</param>
    /// <param name="b">The second member or type.</param>
    /// <returns>A negative value if <paramref name="a"/> sorts before <paramref name="b"/>, zero if tied (including when no safe dimension distinguishes them), positive otherwise.</returns>
    private static int CompareFinalMemberTiebreak(MemberInfo a, MemberInfo b)
    {
        if (TryGetMetadataToken(a, out int aToken) && TryGetMetadataToken(b, out int bToken) && aToken != bToken)
        {
            return aToken.CompareTo(bToken);
        }

        if (TryGetModuleName(a, out string? aModule) && TryGetModuleName(b, out string? bModule))
        {
            int moduleCompare = string.CompareOrdinal(aModule, bModule);
            if (moduleCompare != 0) return moduleCompare;
        }

        if (TryGetAssemblyFullName(a, out string? aAssembly) && TryGetAssemblyFullName(b, out string? bAssembly))
        {
            int assemblyCompare = string.CompareOrdinal(aAssembly, bAssembly);
            if (assemblyCompare != 0) return assemblyCompare;
        }

        // No further safe, non-throwing, deterministic dimension distinguishes two members that already
        // tied on every earlier structural dimension (declaring type, name, generic shape,
        // parameter/return types, ...) - conservatively tie, relying on the caller's stable sort to
        // preserve source order, rather than fabricating an order from runtime identity (object
        // reference/hash), which the roadmap explicitly forbids.
        return 0;
    }

    /// <summary>Safely reads <see cref="MemberInfo.MetadataToken"/>, which throws for some dynamically-generated members.</summary>
    /// <param name="member">The member or type to inspect.</param>
    /// <param name="token">The metadata token, when available.</param>
    /// <returns><see langword="true"/> if <paramref name="token"/> was read successfully.</returns>
    private static bool TryGetMetadataToken(MemberInfo member, out int token)
    {
        try
        {
            token = member.MetadataToken;
            return true;
        }
        catch (InvalidOperationException)
        {
            token = 0;
            return false;
        }
    }

    /// <summary>Safely reads <see cref="Module.Name"/> for <paramref name="member"/>'s declaring module, which can throw for some dynamically-generated members.</summary>
    /// <param name="member">The member or type to inspect.</param>
    /// <param name="name">The module name, when available.</param>
    /// <returns><see langword="true"/> if <paramref name="name"/> was read successfully.</returns>
    private static bool TryGetModuleName(MemberInfo member, out string? name)
    {
        try
        {
            name = member.Module.Name;
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            name = null;
            return false;
        }
    }

    /// <summary>Safely reads the declaring module's assembly <see cref="Assembly.FullName"/> for <paramref name="member"/>, which can throw for some dynamically-generated members.</summary>
    /// <param name="member">The member or type to inspect.</param>
    /// <param name="name">The assembly full name, when available.</param>
    /// <returns><see langword="true"/> if <paramref name="name"/> was read successfully.</returns>
    private static bool TryGetAssemblyFullName(MemberInfo member, out string? name)
    {
        try
        {
            name = member.Module.Assembly.FullName;
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            name = null;
            return false;
        }
    }

    /// <summary>
    /// Base type for every structural canonical-order key produced by this class. Comparison is
    /// lexicographic/structural: a fixed rank across node kinds is compared first, then kind-specific
    /// metadata for two keys of the same kind. Never based on <see cref="object.GetHashCode"/> or
    /// object identity.
    /// </summary>
    internal abstract class KeyNode : IComparable<KeyNode>
    {
        /// <summary>A fixed, deterministic rank distinguishing this key's node family from every other family.</summary>
        protected abstract int Rank { get; }

        /// <inheritdoc/>
        public int CompareTo(KeyNode? other)
        {
            if (other is null) return 1;
            if (ReferenceEquals(this, other)) return 0;

            int rankCompare = Rank.CompareTo(other.Rank);
            return rankCompare != 0 ? rankCompare : CompareSameRank(other);
        }

        /// <summary>Compares this key against <paramref name="other"/>, which is guaranteed to share this key's <see cref="Rank"/> (and therefore concrete type).</summary>
        /// <param name="other">Another key with the same <see cref="Rank"/>.</param>
        /// <returns>A negative value if this key sorts first, zero if tied, positive otherwise.</returns>
        protected abstract int CompareSameRank(KeyNode other);
    }

    /// <summary>Key for an absent optional sub-expression (a <see langword="null"/> receiver, coalesce conversion, etc.). Always sorts before every real node kind.</summary>
    private sealed class NullKey : KeyNode
    {
        /// <summary>The single shared <see cref="NullKey"/> instance.</summary>
        public static readonly NullKey Instance = new();

        /// <summary>Prevents external instantiation; use <see cref="Instance"/>.</summary>
        private NullKey() { }

        /// <inheritdoc/>
        protected override int Rank => RankNull;

        /// <inheritdoc/>
        protected override int CompareSameRank(KeyNode other) => 0;
    }

    /// <summary>Key for a node kind not among the seven this class understands structurally. See this type's remarks on unsupported node kinds.</summary>
    private sealed class UnsupportedKey : KeyNode
    {
        /// <summary>The single shared <see cref="UnsupportedKey"/> instance.</summary>
        public static readonly UnsupportedKey Instance = new();

        /// <summary>Prevents external instantiation; use <see cref="Instance"/>.</summary>
        private UnsupportedKey() { }

        /// <inheritdoc/>
        protected override int Rank => RankUnsupported;

        /// <inheritdoc/>
        protected override int CompareSameRank(KeyNode other) => 0;
    }

    /// <summary>Key for a <see cref="ConstantExpression"/>: an exact numeric value when the declared type is native numeric, else declared type plus boxed value.</summary>
    private sealed class ConstantKey : KeyNode
    {
        private readonly Type _type;
        private readonly object? _value;
        private readonly ExpressionComparer.ExactNumericValue? _numeric;

        /// <summary>Initializes a new <see cref="ConstantKey"/>.</summary>
        /// <param name="type">The constant's declared <see cref="Type"/>.</param>
        /// <param name="value">The constant's boxed value.</param>
        /// <param name="numeric">The exact numeric value, when <paramref name="type"/> is a native numeric type; otherwise <see langword="null"/>.</param>
        public ConstantKey(Type type, object? value, ExpressionComparer.ExactNumericValue? numeric)
        {
            _type = type;
            _value = value;
            _numeric = numeric;
        }

        /// <inheritdoc/>
        protected override int Rank => RankConstant;

        /// <inheritdoc/>
        protected override int CompareSameRank(KeyNode other)
        {
            var o = (ConstantKey)other;

            if (_numeric is not null && o._numeric is not null)
            {
                int numericCompare = _numeric.Value.CompareTo(o._numeric.Value);
                if (numericCompare != 0) return numericCompare;

                // Mathematically equal exact numeric values (S4 review, round 4). This can happen for two
                // constants whose DECLARED Type does not itself identify them as numeric - e.g.
                // Expression.Constant(1, typeof(object)) and Expression.Constant(1.0, typeof(object)):
                // TryGetNumericValue falls back to each value's own RUNTIME type in that case (see its
                // remarks), so _numeric can be non-null on both sides even though _type is typeof(object)
                // for both. ExpressionComparer.ConstantsEqual does not (yet) apply that same runtime-type
                // fallback - its own TryGetExactNumericValue only ever consults the DECLARED Type - so two
                // such constants are NOT structurally equal even though they tied above; without a further
                // tie-break here, two reversed source orderings of these two (unequal) terms would each just
                // keep their own source order (a stable-sort tie in both directions), breaking S4's
                // source-order-independent canonicalization guarantee for this shape. Tie-break by the boxed
                // value's own runtime type (via the same non-throwing, non-hash-based CompareType helper -
                // object.GetType() is always safe to call, never user-overridable) whenever the DECLARED
                // type is not itself native numeric on EITHER side. When both sides ARE declared native
                // numeric types (the ordinary, long-established cross-type-numeric-equality case - e.g.
                // int 1 vs long 1, which ConstantsEqual DOES already treat as equal via the declared type),
                // this branch is skipped and the two constants keep tying here exactly as before S4 review
                // round 4, consistent with that existing equality.
                if (!Types.Number.Contains(_type) || !Types.Number.Contains(o._type))
                {
                    Type leftRuntimeType = _value!.GetType();
                    Type rightRuntimeType = o._value!.GetType();
                    if (leftRuntimeType != rightRuntimeType)
                    {
                        return CompareType(leftRuntimeType, rightRuntimeType);
                    }

                    // Runtime types also tie (S4 review, round 5): e.g. Expression.Constant(1.0,
                    // typeof(object)) vs Expression.Constant(1.0, typeof(IConvertible)) - same exact value,
                    // same boxed runtime type (double), but different DECLARED types. ConstantsEqual's
                    // fallback path (used whenever the fast numeric-value check above does not apply to
                    // BOTH sides, exactly the scenario reached here) requires x.Type == y.Type, so these two
                    // are NOT structurally equal despite tying on both value and runtime type; without this
                    // further tie-break, two reversed source orderings of these two (unequal) terms would
                    // again both just keep their own source order. Tie-break by the DECLARED type itself
                    // (same non-throwing CompareType helper).
                    if (_type != o._type)
                    {
                        return CompareType(_type, o._type);
                    }
                }

                return 0;
            }

            if (_numeric is not null != o._numeric is not null)
            {
                return _numeric is not null ? -1 : 1;
            }

            int typeCompare = CompareType(_type, o._type);
            if (typeCompare != 0) return typeCompare;

            if (_value is null || o._value is null)
            {
                return _value is null && o._value is null ? 0 : _value is null ? -1 : 1;
            }

            // Deliberately does NOT call an arbitrary boxed value's IComparable.CompareTo(): that can
            // execute user-defined code (including code with side effects or that throws) merely because
            // an expression tree happens to embed such a constant, and even a "safe" framework
            // IComparable (e.g. string's default comparer) can depend on Thread.CurrentCulture, which
            // would make this supposedly deterministic key vary with ambient culture state. The gate below
            // reuses ExpressionComparer.IsKnownSafeConstantValue - the SAME predicate the S4
            // additive-grouping equality/hash uses - so the order and grouping sides can never drift on
            // which constant types are "known safe". Every branch also re-checks both operands' runtime
            // types match (not just that both satisfy the safe-value predicate) before casting, since
            // CompareType's own final tie-break can conservatively report two genuinely different Type
            // instances as tied. Every other non-numeric constant type ties (0), relying on the caller's
            // stable sort to preserve source order.
            if (ExpressionComparer.IsKnownSafeConstantValue(_value) && ExpressionComparer.IsKnownSafeConstantValue(o._value))
            {
                // Two individually-safe values (e.g. bool vs string) can still share a DECLARED type wide
                // enough to mask their difference (both Expression.Constant(..., typeof(object))), which is
                // exactly why CompareType(_type, o._type) above may already have tied. Order by RUNTIME
                // type identity first, via the same non-throwing, non-hash-based CompareType helper -
                // Type.GetType() is always safe to call, never user-overridable.
                Type leftRuntimeType = _value.GetType();
                Type rightRuntimeType = o._value.GetType();
                if (leftRuntimeType != rightRuntimeType)
                {
                    return CompareType(leftRuntimeType, rightRuntimeType);
                }

                if (_value is string leftString && o._value is string rightString)
                {
                    return Math.Sign(string.CompareOrdinal(leftString, rightString));
                }

                if (_value is bool leftBool && o._value is bool rightBool)
                {
                    return leftBool.CompareTo(rightBool);
                }

                if (_value is char leftChar && o._value is char rightChar)
                {
                    return leftChar.CompareTo(rightChar);
                }

                if (_value is Enum leftEnum && o._value is Enum rightEnum && leftEnum.GetType() == rightEnum.GetType())
                {
                    // Enum's own IComparable implementation compares the underlying integral value: fixed
                    // BCL behavior, never user-overridable (an enum type cannot declare methods) and never
                    // culture-dependent - unlike a generic IComparable.CompareTo(), which this method
                    // deliberately never calls on an arbitrary type.
                    return Math.Sign(((IComparable)leftEnum).CompareTo(rightEnum));
                }
            }

            return 0;
        }
    }

    /// <summary>Key for a <see cref="ParameterExpression"/>: a De-Bruijn-style (depth, position, type) triple when bound, or a type-only key when free. See this class's remarks on the bound/free policy.</summary>
    private sealed class ParameterKey : KeyNode
    {
        private readonly bool _isBound;
        private readonly int _depth;
        private readonly int _position;
        private readonly Type _type;

        /// <summary>Initializes a new <see cref="ParameterKey"/>. Use <see cref="Bound"/>/<see cref="Free"/> instead of calling this directly.</summary>
        /// <param name="isBound">Whether the parameter was found in an active lexical scope.</param>
        /// <param name="depth">The relative binding depth (0 = innermost); meaningful only when <paramref name="isBound"/> is <see langword="true"/>.</param>
        /// <param name="position">The declaration position within the binding scope; meaningful only when <paramref name="isBound"/> is <see langword="true"/>.</param>
        /// <param name="type">The parameter's declared type.</param>
        private ParameterKey(bool isBound, int depth, int position, Type type)
        {
            _isBound = isBound;
            _depth = depth;
            _position = position;
            _type = type;
        }

        /// <summary>Creates the key for a parameter bound at the given relative depth and declaration position.</summary>
        /// <param name="depth">The relative binding depth (0 = innermost).</param>
        /// <param name="position">The declaration position within the binding scope.</param>
        /// <param name="type">The parameter's declared type.</param>
        /// <returns>The resulting bound <see cref="ParameterKey"/>.</returns>
        public static ParameterKey Bound(int depth, int position, Type type) => new(true, depth, position, type);

        /// <summary>Creates the key for a parameter not found in any active lexical scope.</summary>
        /// <param name="type">The parameter's declared type.</param>
        /// <returns>The resulting free <see cref="ParameterKey"/>.</returns>
        public static ParameterKey Free(Type type) => new(false, 0, 0, type);

        /// <inheritdoc/>
        protected override int Rank => RankParameter;

        /// <inheritdoc/>
        protected override int CompareSameRank(KeyNode other)
        {
            var o = (ParameterKey)other;
            if (_isBound != o._isBound) return _isBound ? -1 : 1;

            if (_isBound)
            {
                int depthCompare = _depth.CompareTo(o._depth);
                if (depthCompare != 0) return depthCompare;
                int positionCompare = _position.CompareTo(o._position);
                if (positionCompare != 0) return positionCompare;
                return CompareType(_type, o._type);
            }

            // Two distinct free parameters have no name-independent structural total order - see this
            // type's remarks. Compare only by declared type, then tie.
            return CompareType(_type, o._type);
        }
    }

    /// <summary>Key for a <see cref="UnaryExpression"/>: node type, result type, operator method, lifting flags, then the operand's key.</summary>
    private sealed class UnaryKey : KeyNode
    {
        private readonly ExpressionType _nodeType;
        private readonly Type _type;
        private readonly MethodInfo? _method;
        private readonly bool _isLifted;
        private readonly bool _isLiftedToNull;
        private readonly KeyNode _operand;

        /// <summary>Initializes a new <see cref="UnaryKey"/>.</summary>
        /// <param name="nodeType">The unary <see cref="ExpressionType"/>.</param>
        /// <param name="type">The result <see cref="Type"/>.</param>
        /// <param name="method">The operator method, or <see langword="null"/>.</param>
        /// <param name="isLifted"><see cref="UnaryExpression.IsLifted"/>.</param>
        /// <param name="isLiftedToNull"><see cref="UnaryExpression.IsLiftedToNull"/>.</param>
        /// <param name="operand">The operand's key.</param>
        public UnaryKey(ExpressionType nodeType, Type type, MethodInfo? method, bool isLifted, bool isLiftedToNull, KeyNode operand)
        {
            _nodeType = nodeType;
            _type = type;
            _method = method;
            _isLifted = isLifted;
            _isLiftedToNull = isLiftedToNull;
            _operand = operand;
        }

        /// <inheritdoc/>
        protected override int Rank => RankUnary;

        /// <inheritdoc/>
        protected override int CompareSameRank(KeyNode other)
        {
            var o = (UnaryKey)other;
            int c = ((int)_nodeType).CompareTo((int)o._nodeType);
            if (c != 0) return c;
            c = CompareType(_type, o._type);
            if (c != 0) return c;
            c = CompareMethod(_method, o._method);
            if (c != 0) return c;
            c = _isLifted.CompareTo(o._isLifted);
            if (c != 0) return c;
            c = _isLiftedToNull.CompareTo(o._isLiftedToNull);
            if (c != 0) return c;
            return _operand.CompareTo(o._operand);
        }
    }

    /// <summary>Key for a <see cref="BinaryExpression"/>: node type, result type, operator method, lifting flags, then the left/right/conversion keys.</summary>
    private sealed class BinaryKey : KeyNode
    {
        private readonly ExpressionType _nodeType;
        private readonly Type _type;
        private readonly MethodInfo? _method;
        private readonly bool _isLifted;
        private readonly bool _isLiftedToNull;
        private readonly KeyNode _left;
        private readonly KeyNode _right;
        private readonly KeyNode _conversion;

        /// <summary>Initializes a new <see cref="BinaryKey"/>.</summary>
        /// <param name="nodeType">The binary <see cref="ExpressionType"/>.</param>
        /// <param name="type">The result <see cref="Type"/>.</param>
        /// <param name="method">The operator method, or <see langword="null"/>.</param>
        /// <param name="isLifted"><see cref="BinaryExpression.IsLifted"/>.</param>
        /// <param name="isLiftedToNull"><see cref="BinaryExpression.IsLiftedToNull"/>.</param>
        /// <param name="left">The left operand's key.</param>
        /// <param name="right">The right operand's key.</param>
        /// <param name="conversion">The coalesce conversion lambda's key, or the shared <see cref="NullKey"/> when absent.</param>
        public BinaryKey(ExpressionType nodeType, Type type, MethodInfo? method, bool isLifted, bool isLiftedToNull, KeyNode left, KeyNode right, KeyNode conversion)
        {
            _nodeType = nodeType;
            _type = type;
            _method = method;
            _isLifted = isLifted;
            _isLiftedToNull = isLiftedToNull;
            _left = left;
            _right = right;
            _conversion = conversion;
        }

        /// <inheritdoc/>
        protected override int Rank => RankBinary;

        /// <inheritdoc/>
        protected override int CompareSameRank(KeyNode other)
        {
            var o = (BinaryKey)other;
            int c = ((int)_nodeType).CompareTo((int)o._nodeType);
            if (c != 0) return c;
            c = CompareType(_type, o._type);
            if (c != 0) return c;
            c = CompareMethod(_method, o._method);
            if (c != 0) return c;
            c = _isLifted.CompareTo(o._isLifted);
            if (c != 0) return c;
            c = _isLiftedToNull.CompareTo(o._isLiftedToNull);
            if (c != 0) return c;
            c = _left.CompareTo(o._left);
            if (c != 0) return c;
            c = _right.CompareTo(o._right);
            if (c != 0) return c;
            return _conversion.CompareTo(o._conversion);
        }
    }

    /// <summary>Key for a <see cref="MethodCallExpression"/>: exact method, receiver key, then each argument's key in order.</summary>
    private sealed class MethodCallKey : KeyNode
    {
        private readonly MethodInfo _method;
        private readonly KeyNode _receiver;
        private readonly KeyNode[] _arguments;

        /// <summary>Initializes a new <see cref="MethodCallKey"/>.</summary>
        /// <param name="method">The exact called <see cref="MethodInfo"/>.</param>
        /// <param name="receiver">The instance receiver's key, or the shared <see cref="NullKey"/> for a static call.</param>
        /// <param name="arguments">Each argument's key, in declaration order.</param>
        public MethodCallKey(MethodInfo method, KeyNode receiver, KeyNode[] arguments)
        {
            _method = method;
            _receiver = receiver;
            _arguments = arguments;
        }

        /// <inheritdoc/>
        protected override int Rank => RankMethodCall;

        /// <inheritdoc/>
        protected override int CompareSameRank(KeyNode other)
        {
            var o = (MethodCallKey)other;
            int c = CompareMethod(_method, o._method);
            if (c != 0) return c;
            c = _receiver.CompareTo(o._receiver);
            if (c != 0) return c;

            int minCount = Math.Min(_arguments.Length, o._arguments.Length);
            for (int i = 0; i < minCount; i++)
            {
                c = _arguments[i].CompareTo(o._arguments[i]);
                if (c != 0) return c;
            }
            return _arguments.Length.CompareTo(o._arguments.Length);
        }
    }

    /// <summary>Key for a <see cref="MemberExpression"/>: exact member, then the receiver's key.</summary>
    private sealed class MemberKey : KeyNode
    {
        private readonly MemberInfo _member;
        private readonly KeyNode _receiver;

        /// <summary>Initializes a new <see cref="MemberKey"/>.</summary>
        /// <param name="member">The exact accessed <see cref="MemberInfo"/> (a field or property).</param>
        /// <param name="receiver">The receiver's key, or the shared <see cref="NullKey"/> for a static member.</param>
        public MemberKey(MemberInfo member, KeyNode receiver)
        {
            _member = member;
            _receiver = receiver;
        }

        /// <inheritdoc/>
        protected override int Rank => RankMember;

        /// <inheritdoc/>
        protected override int CompareSameRank(KeyNode other)
        {
            var o = (MemberKey)other;
            int c = CompareMember(_member, o._member);
            return c != 0 ? c : _receiver.CompareTo(o._receiver);
        }
    }

    /// <summary>Key for a <see cref="LambdaExpression"/>: delegate type, <see cref="LambdaExpression.TailCall"/>, parameter types, then the body's key. <see cref="LambdaExpression.Name"/> is deliberately ignored as non-semantic debug metadata.</summary>
    private sealed class LambdaKey : KeyNode
    {
        private readonly Type _type;
        private readonly bool _tailCall;
        private readonly Type[] _parameterTypes;
        private readonly KeyNode _body;

        /// <summary>Initializes a new <see cref="LambdaKey"/>.</summary>
        /// <param name="type">The lambda's delegate <see cref="Type"/>.</param>
        /// <param name="tailCall"><see cref="LambdaExpression.TailCall"/>.</param>
        /// <param name="parameterTypes">The declared parameters' types, in declaration order.</param>
        /// <param name="body">The body's key, built under a scope frame for this lambda's own parameters.</param>
        public LambdaKey(Type type, bool tailCall, Type[] parameterTypes, KeyNode body)
        {
            _type = type;
            _tailCall = tailCall;
            _parameterTypes = parameterTypes;
            _body = body;
        }

        /// <inheritdoc/>
        protected override int Rank => RankLambda;

        /// <inheritdoc/>
        protected override int CompareSameRank(KeyNode other)
        {
            var o = (LambdaKey)other;
            int c = CompareType(_type, o._type);
            if (c != 0) return c;
            c = _tailCall.CompareTo(o._tailCall);
            if (c != 0) return c;
            c = _parameterTypes.Length.CompareTo(o._parameterTypes.Length);
            if (c != 0) return c;
            for (int i = 0; i < _parameterTypes.Length; i++)
            {
                c = CompareType(_parameterTypes[i], o._parameterTypes[i]);
                if (c != 0) return c;
            }
            return _body.CompareTo(o._body);
        }
    }
}
