using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Utils.Objects;

namespace Utils.Mathematics.Expressions
{
    /// <summary>
    /// Observed comparison memo: the second tier of the comparison-memo session (roadmap stage S5, P15).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Problem.</b> The P9 memo only accepts operands that cannot reach user code by construction (numeric
    /// arithmetic). An additive chain that also holds a call, a member access or an opaque constant is therefore
    /// simplified again by every enclosing comparison: a 9-term subtraction chain whose opaque term sorts last ran
    /// about 1.9 million nested transformations for a single public call, while the opaque constant's own code was
    /// never called once.
    /// </para>
    /// <para>
    /// <b>What is memoized.</b> Exactly what P9 memoizes: the result of one complete, successful comparer
    /// simplification (<see cref="ExpressionComparer.SimplifyForComparison(Expression)"/>), inside the same session,
    /// under the same activation, and consumed only by
    /// <see cref="ExpressionComparer.StructuralEqualsAfterSimplification"/>. The difference is the admission rule:
    /// a result is stored only when <see cref="ExpressionComparer.UserCodeInvocationCount"/> did not change while
    /// it was computed, i.e. when the comparer made no call into a constant value's own
    /// <see cref="object.Equals(object?)"/> or <see cref="object.GetHashCode"/> during that simplification.
    /// </para>
    /// <para>
    /// <b>Why skipping a later simplification is not observable.</b> Simplifying an eligible operand is a
    /// deterministic function of its strict key (below) and of the answers of the user code it calls. The simplifier
    /// reads an opaque constant value only through its reference identity, its runtime type
    /// (<see cref="object.GetType"/>), <see cref="RuntimeHelpers.GetHashCode(object)"/>, and the comparer's two
    /// counted calls; numeric-like values, whose rule matching may call their conversions, are not eligible. A run
    /// that made no counted call therefore depended on nothing but its key, and running it again on an operand with
    /// the same key would follow the same path: no user code, no exception, a structurally identical result. Not
    /// running it removes no user-code call, no exception and no change of call order, which is the guarantee the
    /// P3 factor cache and the public comparer give. A run that made a counted call (even one that returned
    /// normally) is never stored, so a stateful, throwing or re-entrant <see cref="object.Equals(object?)"/> is
    /// called exactly as often, in exactly the same order, as without this memo.
    /// </para>
    /// <para>
    /// <b>Key identity.</b> Strict and oriented, like P9: node type, static type, operator or call method, member,
    /// lifting flags, ordered operands and arguments, parameters by reference, primitive numeric constants by exact
    /// bits, and every other constant by its declared type and the reference identity of its value. Eligible nodes:
    /// parameters, constants, unary nodes other than <see cref="ExpressionType.Quote"/> and
    /// <see cref="ExpressionType.Throw"/>, binary nodes without conversion lambda, method calls and member accesses.
    /// Lambdas, conditionals and every other node kind (including extension nodes, whose members are user code) make
    /// the operand ineligible. The same 1,024-node per-path budget as P9 bounds key work and recursion.
    /// </para>
    /// <para>
    /// <b>Lifetime.</b> The observed memo belongs to the P9 session: it is allocated lazily after activation, detached
    /// by every independent <see cref="Transform(Expression)"/> call and released with the session.
    /// </para>
    /// </remarks>
    public partial class ExpressionSimplifier
    {
        /// <summary>Lazily allocated observed memo of the current comparison session (S5 P15).</summary>
        [ThreadStatic]
        private static Dictionary<ObservedComparisonMemoKey, Expression>? _observedComparisonMemo;

        /// <summary>Gets whether the current thread holds an allocated observed comparison memo (focused tests).</summary>
        internal static bool HasObservedComparisonMemo => _observedComparisonMemo is not null;

        /// <summary>Gets the number of entries in the current thread's observed comparison memo (focused tests).</summary>
        internal static int ObservedComparisonMemoEntryCount => _observedComparisonMemo?.Count ?? 0;

        /// <summary>Determines whether <paramref name="expression"/> has a strict observed memo key (focused tests).</summary>
        /// <param name="expression">The operand.</param>
        /// <returns><see langword="true"/> when an observed key can be built.</returns>
        internal static bool IsObservedComparisonMemoEligible(Expression expression) => ObservedComparisonMemoKey.TryCreate(expression, out _);

        /// <summary>
        /// Simplifies an operand of an active session that the P9 memo does not accept, reusing a result recorded for a
        /// strictly identical operand whose simplification ran no user code, or recording this one under the same
        /// condition.
        /// </summary>
        /// <param name="simplifier">The comparer's shared exact built-in simplifier.</param>
        /// <param name="expression">The operand to simplify.</param>
        /// <param name="userCodeBefore">The value of <see cref="ExpressionComparer.UserCodeInvocationCount"/> read when this comparer simplification started.</param>
        /// <returns>The simplified operand.</returns>
        private static Expression SimplifyWithObservedComparisonMemo(ExpressionSimplifier simplifier, Expression expression, long userCodeBefore)
        {
            if (!ObservedComparisonMemoKey.TryCreate(expression, out ObservedComparisonMemoKey key))
            {
                return simplifier.TransformWithinBoundary(expression);
            }

            Dictionary<ObservedComparisonMemoKey, Expression>? memo = _observedComparisonMemo;
            if (memo is not null && memo.TryGetValue(key, out Expression? cached))
            {
                return cached;
            }

            Expression result = simplifier.TransformWithinBoundary(expression);
            if (ExpressionComparer.UserCodeInvocationCount == userCodeBefore) StoreObservedComparisonResult(key, result);
            return result;
        }

        /// <summary>
        /// Records a completed in-flight result in the observed memo when its operand has an observed key and its
        /// simplification ran no user code.
        /// </summary>
        /// <param name="expression">The completed operand.</param>
        /// <param name="result">Its successful simplification.</param>
        /// <param name="userCodeBefore">The value of <see cref="ExpressionComparer.UserCodeInvocationCount"/> read before the simplification started.</param>
        private static void RecordObservedComparisonResult(Expression expression, Expression result, long userCodeBefore)
        {
            if (ExpressionComparer.UserCodeInvocationCount == userCodeBefore
                && ObservedComparisonMemoKey.TryCreate(expression, out ObservedComparisonMemoKey key))
            {
                StoreObservedComparisonResult(key, result);
            }
        }

        /// <summary>
        /// Stores a completed result, allocating the observed memo on first use. As for P9, the shared state is read
        /// only after the recursive simplification has returned, and an entry it inserted for the same key is kept.
        /// </summary>
        /// <param name="key">The operand's strict observed key.</param>
        /// <param name="result">Its successful simplification.</param>
        private static void StoreObservedComparisonResult(in ObservedComparisonMemoKey key, Expression result)
        {
            Dictionary<ObservedComparisonMemoKey, Expression> memo = _observedComparisonMemo ??= new Dictionary<ObservedComparisonMemoKey, Expression>(ObservedComparisonMemoKeyComparer.Instance);
            memo.TryAdd(key, result);
        }

        /// <summary>A strict structural key of the observed memo: the operand plus its precomputed strict hash.</summary>
        private readonly struct ObservedComparisonMemoKey
        {
            /// <summary>Initializes a key.</summary>
            /// <param name="expression">The eligible operand.</param>
            /// <param name="hash">Its strict structural hash.</param>
            private ObservedComparisonMemoKey(Expression expression, int hash)
            {
                Expression = expression;
                Hash = hash;
            }

            /// <summary>Gets the eligible operand.</summary>
            public Expression Expression { get; }

            /// <summary>Gets the strict structural hash of <see cref="Expression"/>.</summary>
            public int Hash { get; }

            /// <summary>
            /// Builds a key in one traversal that also decides eligibility, within the P9 per-path node budget (every
            /// visited node consumes one unit before recursing, which also bounds the recursion depth).
            /// </summary>
            /// <param name="expression">The operand.</param>
            /// <param name="key">The key when the method returns <see langword="true"/>.</param>
            /// <returns><see langword="true"/> when <paramref name="expression"/> is eligible.</returns>
            public static bool TryCreate(Expression expression, out ObservedComparisonMemoKey key)
            {
                int budget = ComparisonMemoNodeBudget;
                if (TryHash(expression, ref budget, out int hash))
                {
                    key = new ObservedComparisonMemoKey(expression, hash);
                    return true;
                }

                key = default;
                return false;
            }

            /// <summary>Recursively hashes one eligible node, failing on any ineligible node or budget exhaustion.</summary>
            /// <param name="expression">The node, or <see langword="null"/> for an absent call receiver or static member owner.</param>
            /// <param name="budget">The remaining per-path node budget.</param>
            /// <param name="hash">The node hash.</param>
            /// <returns><see langword="true"/> when the node and its subtree are eligible.</returns>
            private static bool TryHash(Expression? expression, ref int budget, out int hash)
            {
                hash = 0;
                if (expression is null) return true;
                if (--budget < 0) return false;

                switch (expression)
                {
                    case ParameterExpression parameter:
                        if (parameter.IsByRef) return false;
                        hash = HashCode.Combine(ExpressionType.Parameter, RuntimeHelpers.GetHashCode(parameter));
                        return true;

                    case ConstantExpression constant:
                        if (!TryHashConstant(constant, out int valueHash)) return false;
                        hash = HashCode.Combine(ExpressionType.Constant, constant.Type, valueHash);
                        return true;

                    case UnaryExpression unary:
                        if (unary.NodeType is ExpressionType.Quote or ExpressionType.Throw || unary.Operand is null) return false;
                        if (!TryHash(unary.Operand, ref budget, out int operandHash)) return false;
                        hash = HashCode.Combine(unary.NodeType, unary.Type, unary.Method, unary.IsLiftedToNull, operandHash);
                        return true;

                    case BinaryExpression binary:
                        if (binary.Conversion is not null) return false;
                        if (!TryHash(binary.Left, ref budget, out int leftHash)) return false;
                        if (!TryHash(binary.Right, ref budget, out int rightHash)) return false;
                        hash = HashCode.Combine(binary.NodeType, binary.Type, binary.Method, binary.IsLiftedToNull, leftHash, rightHash);
                        return true;

                    case MethodCallExpression call:
                        if (!TryHash(call.Object, ref budget, out int objectHash)) return false;
                        var callHash = new HashCode();
                        callHash.Add(ExpressionType.Call);
                        callHash.Add(call.Method);
                        callHash.Add(objectHash);
                        for (int i = 0; i < call.Arguments.Count; i++)
                        {
                            if (!TryHash(call.Arguments[i], ref budget, out int argumentHash)) return false;
                            callHash.Add(argumentHash);
                        }
                        hash = callHash.ToHashCode();
                        return true;

                    case MemberExpression member:
                        if (!TryHash(member.Expression, ref budget, out int ownerHash)) return false;
                        hash = HashCode.Combine(ExpressionType.MemberAccess, member.Type, member.Member, ownerHash);
                        return true;

                    default:
                        return false;
                }
            }

            /// <summary>
            /// Hashes a constant: a primitive numeric value by its exact bit representation (as P9 does), a null value
            /// by its declared type, and any other value by reference identity. A numeric-like value of another type is
            /// ineligible, because rule matching may call its conversions (<c>ConstantNumericAttribute</c>).
            /// </summary>
            /// <param name="constant">The constant.</param>
            /// <param name="hash">The value hash.</param>
            /// <returns><see langword="true"/> when the constant is eligible.</returns>
            private static bool TryHashConstant(ConstantExpression constant, out int hash)
            {
                hash = 0;
                object? value = constant.Value;
                if (value is null) return true;

                if (value.GetType() == constant.Type && IsMemoizableNumericType(constant.Type))
                {
                    hash = value switch
                    {
                        double d => BitConverter.DoubleToInt64Bits(d).GetHashCode(),
                        float f => BitConverter.SingleToInt32Bits(f),
                        decimal m => HashCode.Combine(m, m.Scale),
                        _ => value.GetHashCode(),
                    };
                    return true;
                }

                if (Types.Number.Contains(constant.Type) || NumberUtils.IsNumeric(value)) return false;

                hash = RuntimeHelpers.GetHashCode(value);
                return true;
            }
        }

        /// <summary>Strict oriented structural equality over observed memo keys.</summary>
        private sealed class ObservedComparisonMemoKeyComparer : IEqualityComparer<ObservedComparisonMemoKey>
        {
            /// <summary>Gets the shared stateless instance.</summary>
            public static ObservedComparisonMemoKeyComparer Instance { get; } = new();

            /// <inheritdoc/>
            public bool Equals(ObservedComparisonMemoKey x, ObservedComparisonMemoKey y)
                => x.Hash == y.Hash && StrictEquals(x.Expression, y.Expression);

            /// <inheritdoc/>
            public int GetHashCode(ObservedComparisonMemoKey obj) => obj.Hash;

            /// <summary>
            /// Compares two eligible trees strictly: same node kinds, types, methods, members, lifting flags, ordered
            /// operands and arguments, parameters by reference, primitive constants by exact bits and other constant
            /// values by reference. No user code is called. A reference-equal pair is equal because eligible trees
            /// contain no lambda binding context.
            /// </summary>
            /// <param name="x">The first eligible tree, or <see langword="null"/> for an absent receiver or owner.</param>
            /// <param name="y">The second eligible tree, or <see langword="null"/>.</param>
            /// <returns><see langword="true"/> when both trees are strictly identical.</returns>
            private static bool StrictEquals(Expression? x, Expression? y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null) return false;
                if (x.NodeType != y.NodeType || x.Type != y.Type) return false;

                switch (x)
                {
                    case ParameterExpression:
                        return false;

                    case ConstantExpression xc:
                        return ConstantValuesEqual(xc.Value, ((ConstantExpression)y).Value);

                    case UnaryExpression xu:
                        UnaryExpression yu = (UnaryExpression)y;
                        return xu.Method == yu.Method
                            && xu.IsLifted == yu.IsLifted
                            && xu.IsLiftedToNull == yu.IsLiftedToNull
                            && StrictEquals(xu.Operand, yu.Operand);

                    case BinaryExpression xb:
                        BinaryExpression yb = (BinaryExpression)y;
                        return xb.Method == yb.Method
                            && xb.IsLifted == yb.IsLifted
                            && xb.IsLiftedToNull == yb.IsLiftedToNull
                            && xb.Conversion is null && yb.Conversion is null
                            && StrictEquals(xb.Left, yb.Left)
                            && StrictEquals(xb.Right, yb.Right);

                    case MethodCallExpression xm:
                        MethodCallExpression ym = (MethodCallExpression)y;
                        if (xm.Method != ym.Method || xm.Arguments.Count != ym.Arguments.Count) return false;
                        if (!StrictEquals(xm.Object, ym.Object)) return false;
                        for (int i = 0; i < xm.Arguments.Count; i++)
                        {
                            if (!StrictEquals(xm.Arguments[i], ym.Arguments[i])) return false;
                        }
                        return true;

                    case MemberExpression xa:
                        MemberExpression ya = (MemberExpression)y;
                        return xa.Member == ya.Member && StrictEquals(xa.Expression, ya.Expression);

                    default:
                        return false;
                }
            }

            /// <summary>
            /// Compares two constant values of the same declared type: primitive numeric values by exact bits, every
            /// other value by reference identity (never through its own <see cref="object.Equals(object?)"/>).
            /// </summary>
            /// <param name="x">The first value.</param>
            /// <param name="y">The second value.</param>
            /// <returns><see langword="true"/> when both values are the same representation or the same object.</returns>
            private static bool ConstantValuesEqual(object? x, object? y) => (x, y) switch
            {
                (null, null) => true,
                (null, _) or (_, null) => false,
                (double xd, double yd) => BitConverter.DoubleToInt64Bits(xd) == BitConverter.DoubleToInt64Bits(yd),
                (float xf, float yf) => BitConverter.SingleToInt32Bits(xf) == BitConverter.SingleToInt32Bits(yf),
                (decimal xm, decimal ym) => DecimalBitsEqual(xm, ym),
                _ when IsMemoizableNumericType(x.GetType()) => x.GetType() == y.GetType() && x.Equals(y),
                _ => ReferenceEquals(x, y),
            };

            /// <summary>Compares two decimals by their four 32-bit words, so that scale is significant.</summary>
            /// <param name="x">The first decimal.</param>
            /// <param name="y">The second decimal.</param>
            /// <returns><see langword="true"/> when both decimals have identical bits.</returns>
            private static bool DecimalBitsEqual(decimal x, decimal y)
            {
                Span<int> xBits = stackalloc int[4];
                Span<int> yBits = stackalloc int[4];
                decimal.GetBits(x, xBits);
                decimal.GetBits(y, yBits);
                return xBits.SequenceEqual(yBits);
            }
        }
    }
}
