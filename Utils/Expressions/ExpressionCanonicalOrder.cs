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
    private const int RankNull = -1;
    private const int RankConstant = 0;
    private const int RankParameter = 1;
    private const int RankUnary = 2;
    private const int RankBinary = 3;
    private const int RankMethodCall = 4;
    private const int RankMember = 5;
    private const int RankLambda = 6;
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

    private static KeyNode BuildConstant(ConstantExpression ce)
    {
        ExpressionComparer.ExactNumericValue? numeric = Types.Number.Contains(ce.Type) && ce.Value is not null
            ? ExpressionComparer.ExactNumericValue.FromBoxed(ce.Type, ce.Value)
            : null;
        return new ConstantKey(ce.Type, ce.Value, numeric);
    }

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

    private static KeyNode BuildUnary(UnaryExpression ue, List<ParameterExpression[]> scopes)
        => new UnaryKey(ue.NodeType, ue.Type, ue.Method, ue.IsLifted, ue.IsLiftedToNull, Build(ue.Operand, scopes));

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

    private static KeyNode BuildMethodCall(MethodCallExpression mce, List<ParameterExpression[]> scopes)
    {
        var arguments = new KeyNode[mce.Arguments.Count];
        for (int i = 0; i < arguments.Length; i++)
        {
            arguments[i] = Build(mce.Arguments[i], scopes);
        }

        return new MethodCallKey(mce.Method, Build(mce.Object, scopes), arguments);
    }

    private static KeyNode BuildMember(MemberExpression me, List<ParameterExpression[]> scopes)
        => new MemberKey(me.Member, Build(me.Expression, scopes));

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
        // coexist in the same load context) - a final, deterministic (not hash-based) tie-break.
        int tokenCompare = a.MetadataToken.CompareTo(b.MetadataToken);
        if (tokenCompare != 0) return tokenCompare;
        int moduleCompare = string.CompareOrdinal(a.Module.Name, b.Module.Name);
        if (moduleCompare != 0) return moduleCompare;
        return string.CompareOrdinal(a.Assembly.FullName, b.Assembly.FullName);
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

    private static int CompareFinalMemberTiebreak(MemberInfo a, MemberInfo b)
    {
        int tokenCompare = a.MetadataToken.CompareTo(b.MetadataToken);
        if (tokenCompare != 0) return tokenCompare;
        int moduleCompare = string.CompareOrdinal(a.Module.Name, b.Module.Name);
        if (moduleCompare != 0) return moduleCompare;
        return string.CompareOrdinal(a.Module.Assembly.FullName, b.Module.Assembly.FullName);
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
        public static readonly NullKey Instance = new();
        private NullKey() { }
        protected override int Rank => RankNull;
        protected override int CompareSameRank(KeyNode other) => 0;
    }

    /// <summary>Key for a node kind not among the seven this class understands structurally. See this type's remarks on unsupported node kinds.</summary>
    private sealed class UnsupportedKey : KeyNode
    {
        public static readonly UnsupportedKey Instance = new();
        private UnsupportedKey() { }
        protected override int Rank => RankUnsupported;
        protected override int CompareSameRank(KeyNode other) => 0;
    }

    private sealed class ConstantKey : KeyNode
    {
        private readonly Type _type;
        private readonly object? _value;
        private readonly ExpressionComparer.ExactNumericValue? _numeric;

        public ConstantKey(Type type, object? value, ExpressionComparer.ExactNumericValue? numeric)
        {
            _type = type;
            _value = value;
            _numeric = numeric;
        }

        protected override int Rank => RankConstant;

        protected override int CompareSameRank(KeyNode other)
        {
            var o = (ConstantKey)other;

            if (_numeric is not null && o._numeric is not null)
            {
                return _numeric.Value.CompareTo(o._numeric.Value);
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

            if (_value.GetType() == o._value.GetType() && _value is IComparable comparable)
            {
                try
                {
                    return Math.Sign(comparable.CompareTo(o._value));
                }
                catch (ArgumentException)
                {
                    // The runtime type implements IComparable but refuses to compare against this
                    // particular other instance: no structural basis to order them, fall through to tie.
                }
            }

            // No structural, non-hash-based way to order two distinct non-numeric constant values of a
            // type that either differs or does not support IComparable: tie, relying on stable sort.
            return 0;
        }
    }

    private sealed class ParameterKey : KeyNode
    {
        private readonly bool _isBound;
        private readonly int _depth;
        private readonly int _position;
        private readonly Type _type;

        private ParameterKey(bool isBound, int depth, int position, Type type)
        {
            _isBound = isBound;
            _depth = depth;
            _position = position;
            _type = type;
        }

        public static ParameterKey Bound(int depth, int position, Type type) => new(true, depth, position, type);
        public static ParameterKey Free(Type type) => new(false, 0, 0, type);

        protected override int Rank => RankParameter;

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

    private sealed class UnaryKey : KeyNode
    {
        private readonly ExpressionType _nodeType;
        private readonly Type _type;
        private readonly MethodInfo? _method;
        private readonly bool _isLifted;
        private readonly bool _isLiftedToNull;
        private readonly KeyNode _operand;

        public UnaryKey(ExpressionType nodeType, Type type, MethodInfo? method, bool isLifted, bool isLiftedToNull, KeyNode operand)
        {
            _nodeType = nodeType;
            _type = type;
            _method = method;
            _isLifted = isLifted;
            _isLiftedToNull = isLiftedToNull;
            _operand = operand;
        }

        protected override int Rank => RankUnary;

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

        protected override int Rank => RankBinary;

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

    private sealed class MethodCallKey : KeyNode
    {
        private readonly MethodInfo _method;
        private readonly KeyNode _receiver;
        private readonly KeyNode[] _arguments;

        public MethodCallKey(MethodInfo method, KeyNode receiver, KeyNode[] arguments)
        {
            _method = method;
            _receiver = receiver;
            _arguments = arguments;
        }

        protected override int Rank => RankMethodCall;

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

    private sealed class MemberKey : KeyNode
    {
        private readonly MemberInfo _member;
        private readonly KeyNode _receiver;

        public MemberKey(MemberInfo member, KeyNode receiver)
        {
            _member = member;
            _receiver = receiver;
        }

        protected override int Rank => RankMember;

        protected override int CompareSameRank(KeyNode other)
        {
            var o = (MemberKey)other;
            int c = CompareMember(_member, o._member);
            return c != 0 ? c : _receiver.CompareTo(o._receiver);
        }
    }

    private sealed class LambdaKey : KeyNode
    {
        private readonly Type _type;
        private readonly bool _tailCall;
        private readonly Type[] _parameterTypes;
        private readonly KeyNode _body;

        public LambdaKey(Type type, bool tailCall, Type[] parameterTypes, KeyNode body)
        {
            _type = type;
            _tailCall = tailCall;
            _parameterTypes = parameterTypes;
            _body = body;
        }

        protected override int Rank => RankLambda;

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
