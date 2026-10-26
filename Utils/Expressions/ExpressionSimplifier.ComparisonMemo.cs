using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Utils.Mathematics.Expressions
{
    /// <summary>
    /// Selective strict structural memoization of comparer-triggered simplifications (roadmap stage S5, P9).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What is memoized.</b> Only the result of <see cref="ExpressionComparer.SimplifyForComparison(Expression)"/>,
    /// i.e. one complete, successful simplification of a candidate operand by the shared exact built-in
    /// comparer simplifier. The structural comparison that follows
    /// (<see cref="ExpressionComparer.StructuralEqualsAfterSimplification"/>) is never memoized or replaced.
    /// </para>
    /// <para>
    /// <b>Session boundary.</b> A comparison session is one outermost comparer simplification on the current
    /// thread (comparison depth 0 to 1) together with every comparer simplification synchronously nested in it,
    /// including those reached through nested <see cref="FactorEqualityProbe"/> instances and through the
    /// comparer's own nested simplifier pass. The memo can only become active inside a session, only when the
    /// activation signal fires, and it is released when the outermost comparer simplification of that session
    /// returns or throws. Every other <see cref="Transform(Expression)"/> call (a public
    /// <see cref="Simplify(Expression)"/>, <see cref="ExpressionComparer.GetHashCode(Expression)"/>, or a call
    /// made from user code) detaches the caller's session state and restores it afterwards, so it starts with an
    /// inactive memo and can never observe or populate the caller's entries.
    /// </para>
    /// <para>
    /// <b>Key identity.</b> Keys are strict and oriented: node type, static result type, operator
    /// <see cref="BinaryExpression.Method"/>/<see cref="UnaryExpression.Method"/>, lifting flags, conversion,
    /// ordered operands, parameter reference identity, and the exact bit representation of constants
    /// (<c>+0.0</c> and <c>-0.0</c>, and distinct NaN payloads, are distinct keys). No commutative or symbolic
    /// equality is used.
    /// </para>
    /// <para>
    /// <b>Eligibility.</b> Numeric parameters, constants of the exact primitive numeric types listed in
    /// <see cref="IsMemoizableNumericType"/>, ordinary predefined negation, and ordinary non-lifted
    /// Add/Subtract/Multiply/Divide/Power without conversion. Every other node kind makes the whole operand
    /// ineligible. Simplifying an eligible operand cannot reach user code, so skipping a repeated simplification
    /// is not observable beyond its cost. Exceptions are never cached.
    /// </para>
    /// <para>
    /// <b>Observed tier (S5 P15).</b> An operand of an active session that is not eligible here is handed to the
    /// observed comparison memo (<c>ExpressionSimplifier.ObservedComparisonMemo.cs</c>), which accepts calls, members
    /// and opaque constants but stores a result only when its simplification called no user code. Eligibility,
    /// keys and entries of this memo are unchanged.
    /// </para>
    /// </remarks>
    public partial class ExpressionSimplifier
    {
        /// <summary>
        /// Minimum comparison depth at which the activation signal is evaluated (P9 policy D6/A2: depth 6 and a
        /// continuous ordinary-arithmetic depth of at least 2, see <see cref="HasArithmeticDepthOfAtLeastTwo"/>).
        /// </summary>
        private const int ComparisonMemoMinimumDepth = 6;

        /// <summary>Maximum logical node count, counted per path, for which a strict key is built.</summary>
        private const int ComparisonMemoNodeBudget = 1024;

        /// <summary>Current comparer-simplification nesting depth of the active comparison session.</summary>
        [ThreadStatic]
        private static int _comparisonMemoDepth;

        /// <summary>Whether the activation signal has fired in the current comparison session.</summary>
        [ThreadStatic]
        private static bool _comparisonMemoActive;

        /// <summary>Lazily allocated memo of the current comparison session.</summary>
        [ThreadStatic]
        private static Dictionary<ComparisonMemoKey, Expression>? _comparisonMemo;

        /// <summary>Gets whether the current thread holds an active comparison memo session (focused tests).</summary>
        internal static bool IsComparisonMemoActive => _comparisonMemoActive;

        /// <summary>Gets whether the current thread holds an allocated comparison memo (focused tests).</summary>
        internal static bool HasComparisonMemo => _comparisonMemo is not null;

        /// <summary>Gets the current comparison memo nesting depth (focused tests).</summary>
        internal static int ComparisonMemoDepth => _comparisonMemoDepth;

        /// <summary>Gets the number of entries in the current thread's comparison memo (focused tests).</summary>
        internal static int ComparisonMemoEntryCount => _comparisonMemo?.Count ?? 0;

        /// <summary>
        /// Test support: opens an already activated comparison session on the current thread, so focused tests can
        /// exercise key identity, reuse, re-entrancy and isolation on small expressions without reaching the
        /// production activation depth. Disposing the returned scope restores the previous session state.
        /// </summary>
        /// <returns>The scope restoring the previous state.</returns>
        internal static ComparisonMemoTestSession BeginActivatedComparisonMemoSessionForTesting()
        {
            ComparisonMemoTestSession scope = ComparisonMemoTestSession.CaptureCurrent();
            _comparisonMemoDepth = 1;
            _comparisonMemoActive = true;
            _comparisonMemo = null;
            _observedComparisonMemo = null;
            return scope;
        }

        /// <summary>Restores the comparison session state saved by <see cref="BeginActivatedComparisonMemoSessionForTesting"/>.</summary>
        internal readonly struct ComparisonMemoTestSession : IDisposable
        {
            /// <summary>The saved depth.</summary>
            private readonly int _depth;

            /// <summary>The saved activation flag.</summary>
            private readonly bool _active;

            /// <summary>The saved memo.</summary>
            private readonly Dictionary<ComparisonMemoKey, Expression>? _memo;

            /// <summary>The saved observed memo (S5 P15).</summary>
            private readonly Dictionary<ObservedComparisonMemoKey, Expression>? _observedMemo;

            /// <summary>Captures the current thread's session state.</summary>
            /// <param name="capture">Ignored; distinguishes this constructor from the default one.</param>
            private ComparisonMemoTestSession(bool capture)
            {
                _depth = _comparisonMemoDepth;
                _active = _comparisonMemoActive;
                _memo = _comparisonMemo;
                _observedMemo = _observedComparisonMemo;
            }

            /// <summary>Captures the current thread's session state for later restoration.</summary>
            /// <returns>The captured state.</returns>
            internal static ComparisonMemoTestSession CaptureCurrent() => new(capture: true);

            /// <summary>Restores the saved state.</summary>
            public void Dispose()
            {
                _comparisonMemoDepth = _depth;
                _comparisonMemoActive = _active;
                _comparisonMemo = _memo;
                _observedComparisonMemo = _observedMemo;
            }
        }

        /// <summary>Determines whether <paramref name="expression"/> is eligible for a strict memo key (focused tests).</summary>
        /// <param name="expression">The operand.</param>
        /// <returns><see langword="true"/> when a strict key can be built.</returns>
        internal static bool IsComparisonMemoEligible(Expression expression) => ComparisonMemoKey.TryCreate(expression, out _);

        /// <summary>Determines whether two operands have equal strict memo keys (focused tests).</summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns><see langword="true"/> when both are eligible and their strict keys are equal.</returns>
        internal static bool ComparisonMemoKeysEqual(Expression x, Expression y)
            => ComparisonMemoKey.TryCreate(x, out ComparisonMemoKey xKey)
                && ComparisonMemoKey.TryCreate(y, out ComparisonMemoKey yKey)
                && ComparisonMemoKeyComparer.Instance.Equals(xKey, yKey);

        /// <summary>
        /// Simplifies <paramref name="expression"/> for comparison with <paramref name="simplifier"/> (the comparer's
        /// shared exact built-in instance), joining the current comparison session and reusing or recording a
        /// completed result when the session's memo is active and the operand is eligible.
        /// </summary>
        /// <param name="simplifier">The comparer's shared exact built-in simplifier.</param>
        /// <param name="expression">The operand to simplify; never <see langword="null"/>.</param>
        /// <returns>The simplified operand.</returns>
        internal static Expression SimplifyForComparisonInSession(ExpressionSimplifier simplifier, Expression expression)
        {
            int depth = ++_comparisonMemoDepth;
            long userCodeBefore = ExpressionComparer.UserCodeInvocationCount;
            try
            {
                if (!_comparisonMemoActive)
                {
                    if (depth < ComparisonMemoMinimumDepth || !HasArithmeticDepthOfAtLeastTwo(expression))
                    {
                        Expression inactiveResult = simplifier.TransformWithinBoundary(expression);

                        // The session may have been activated by a nested comparison while this one was in
                        // progress: its completed result can still serve later siblings of the session.
                        if (_comparisonMemoActive) RecordComparisonResult(expression, inactiveResult, userCodeBefore);
                        return inactiveResult;
                    }

                    _comparisonMemoActive = true;
                }

                if (!ComparisonMemoKey.TryCreate(expression, out ComparisonMemoKey key))
                {
                    return SimplifyWithObservedComparisonMemo(simplifier, expression, userCodeBefore);
                }

                Dictionary<ComparisonMemoKey, Expression>? memo = _comparisonMemo;
                if (memo is not null && memo.TryGetValue(key, out Expression? cached))
                {
                    return cached;
                }

                Expression result = simplifier.TransformWithinBoundary(expression);
                StoreComparisonResult(key, result);
                return result;
            }
            finally
            {
                if (--_comparisonMemoDepth == 0)
                {
                    _comparisonMemoActive = false;
                    _comparisonMemo = null;
                    _observedComparisonMemo = null;
                }
            }
        }

        /// <summary>
        /// Records a completed result in the active session's memo when the operand is eligible, or in the observed
        /// memo when it is only eligible there and its simplification ran no user code (S5 P15).
        /// </summary>
        /// <param name="expression">The completed operand.</param>
        /// <param name="result">Its successful simplification.</param>
        /// <param name="userCodeBefore">The value of <see cref="ExpressionComparer.UserCodeInvocationCount"/> read before the simplification started.</param>
        private static void RecordComparisonResult(Expression expression, Expression result, long userCodeBefore)
        {
            if (ComparisonMemoKey.TryCreate(expression, out ComparisonMemoKey key))
            {
                StoreComparisonResult(key, result);
            }
            else
            {
                RecordObservedComparisonResult(expression, result, userCodeBefore);
            }
        }

        /// <summary>
        /// Stores a completed result, allocating the session's memo on first use. The shared state is read here,
        /// after the recursive simplification that produced <paramref name="result"/> has returned: that recursion
        /// may already have allocated or populated the memo (P6's re-entrancy failure), so nothing observed before
        /// it is assumed unchanged, and an entry it inserted for the same key is kept.
        /// </summary>
        /// <param name="key">The operand's strict key.</param>
        /// <param name="result">Its successful simplification.</param>
        private static void StoreComparisonResult(in ComparisonMemoKey key, Expression result)
        {
            Dictionary<ComparisonMemoKey, Expression> memo = _comparisonMemo ??= new Dictionary<ComparisonMemoKey, Expression>(ComparisonMemoKeyComparer.Instance);
            memo.TryAdd(key, result);
        }

        /// <summary>
        /// Determines in constant time whether <paramref name="expression"/>'s continuous ordinary-arithmetic depth
        /// (the P8 <c>ArithmeticDepth</c> metric) is at least 2: the root is ordinary arithmetic and so is at least
        /// one of its operands. No subtree is traversed and no metric cache is consulted.
        /// </summary>
        /// <param name="expression">The operand at the activation candidate.</param>
        /// <returns><see langword="true"/> when the signal's arithmetic condition holds.</returns>
        private static bool HasArithmeticDepthOfAtLeastTwo(Expression expression) => expression switch
        {
            UnaryExpression unary => IsOrdinaryUnaryNegate(unary) && IsOrdinaryArithmeticNode(unary.Operand),
            BinaryExpression binary => IsOrdinaryBinaryArithmetic(binary)
                && (IsOrdinaryArithmeticNode(binary.Left) || IsOrdinaryArithmeticNode(binary.Right)),
            _ => false,
        };
        /// <summary>Whether <paramref name="expression"/> is itself an ordinary negation or binary arithmetic node.</summary>
        /// <param name="expression">The node to classify.</param>
        /// <returns><see langword="true"/> for ordinary predefined arithmetic.</returns>
        private static bool IsOrdinaryArithmeticNode(Expression expression) => expression switch
        {
            UnaryExpression unary => IsOrdinaryUnaryNegate(unary),
            BinaryExpression binary => IsOrdinaryBinaryArithmetic(binary),
            _ => false,
        };

        /// <summary>
        /// Whether <paramref name="type"/> is a primitive numeric type whose boxed values have an exact,
        /// representation-preserving bit-level identity handled by <see cref="ComparisonMemoKey"/>.
        /// </summary>
        /// <param name="type">The static type to classify.</param>
        /// <returns><see langword="true"/> for the memoizable numeric types.</returns>
        private static bool IsMemoizableNumericType(Type type)
            => type == typeof(double) || type == typeof(float) || type == typeof(decimal)
                || type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(sbyte)
                || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(byte);

        /// <summary>
        /// A strict structural memo key: the eligible operand plus its precomputed strict structural hash.
        /// </summary>
        private readonly struct ComparisonMemoKey
        {
            /// <summary>Initializes a key.</summary>
            /// <param name="expression">The eligible operand.</param>
            /// <param name="hash">Its strict structural hash.</param>
            private ComparisonMemoKey(Expression expression, int hash)
            {
                Expression = expression;
                Hash = hash;
            }

            /// <summary>Gets the eligible operand.</summary>
            public Expression Expression { get; }

            /// <summary>Gets the strict structural hash of <see cref="Expression"/>.</summary>
            public int Hash { get; }

            /// <summary>
            /// Builds a key in one traversal that also decides eligibility. Traversal stops, and the operand is
            /// ineligible, as soon as an unsupported node is met or the per-path node budget is exhausted. Every
            /// visited node consumes one budget unit before recursing, so the budget bounds both the work (strongly
            /// shared DAGs are counted per path) and the recursion depth of this traversal; strict key equality only
            /// ever runs on two keys built here, so its recursion is bounded by the same budget.
            /// </summary>
            /// <param name="expression">The operand.</param>
            /// <param name="key">The key when the method returns <see langword="true"/>.</param>
            /// <returns><see langword="true"/> when <paramref name="expression"/> is eligible.</returns>
            public static bool TryCreate(Expression expression, out ComparisonMemoKey key)
            {
                int budget = ComparisonMemoNodeBudget;
                if (TryHash(expression, ref budget, out int hash))
                {
                    key = new ComparisonMemoKey(expression, hash);
                    return true;
                }

                key = default;
                return false;
            }

            /// <summary>Recursively hashes one eligible node, failing on any ineligible node or budget exhaustion.</summary>
            /// <param name="expression">The node.</param>
            /// <param name="budget">The remaining per-path node budget.</param>
            /// <param name="hash">The node hash.</param>
            /// <returns><see langword="true"/> when the node and its subtree are eligible.</returns>
            private static bool TryHash(Expression expression, ref int budget, out int hash)
            {
                hash = 0;
                if (--budget < 0) return false;

                switch (expression)
                {
                    case ParameterExpression parameter:
                        if (parameter.IsByRef || !IsMemoizableNumericType(parameter.Type)) return false;
                        hash = HashCode.Combine(ExpressionType.Parameter, RuntimeHelpers.GetHashCode(parameter));
                        return true;

                    case ConstantExpression constant:
                        if (!TryHashConstant(constant, out int valueHash)) return false;
                        hash = HashCode.Combine(ExpressionType.Constant, constant.Type, valueHash);
                        return true;

                    case UnaryExpression unary:
                        if (!IsOrdinaryUnaryNegate(unary) || !TryHash(unary.Operand, ref budget, out int operandHash)) return false;
                        hash = HashCode.Combine(unary.NodeType, unary.Type, unary.Method, operandHash);
                        return true;

                    case BinaryExpression binary:
                        if (!IsOrdinaryBinaryArithmetic(binary) || binary.Conversion is not null) return false;
                        if (!TryHash(binary.Left, ref budget, out int leftHash)) return false;
                        if (!TryHash(binary.Right, ref budget, out int rightHash)) return false;
                        hash = HashCode.Combine(binary.NodeType, binary.Type, binary.Method, leftHash, rightHash);
                        return true;

                    default:
                        return false;
                }
            }

            /// <summary>Hashes a constant's exact bit representation when its type is memoizable.</summary>
            /// <param name="constant">The constant.</param>
            /// <param name="hash">The representation hash.</param>
            /// <returns><see langword="true"/> when the constant is eligible.</returns>
            private static bool TryHashConstant(ConstantExpression constant, out int hash)
            {
                hash = 0;
                object? value = constant.Value;
                if (value is null || value.GetType() != constant.Type || !IsMemoizableNumericType(constant.Type)) return false;

                hash = value switch
                {
                    double d => BitConverter.DoubleToInt64Bits(d).GetHashCode(),
                    float f => BitConverter.SingleToInt32Bits(f),
                    decimal m => HashDecimalBits(m),
                    _ => value.GetHashCode(),
                };
                return true;
            }

            /// <summary>Hashes the four 32-bit words of a decimal, including its scale.</summary>
            /// <param name="value">The decimal value.</param>
            /// <returns>The representation hash.</returns>
            private static int HashDecimalBits(decimal value)
            {
                Span<int> bits = stackalloc int[4];
                decimal.GetBits(value, bits);
                return HashCode.Combine(bits[0], bits[1], bits[2], bits[3]);
            }
        }

        /// <summary>Strict oriented structural equality over eligible memo keys.</summary>
        private sealed class ComparisonMemoKeyComparer : IEqualityComparer<ComparisonMemoKey>
        {
            /// <summary>Gets the shared stateless instance.</summary>
            public static ComparisonMemoKeyComparer Instance { get; } = new();

            /// <inheritdoc/>
            public bool Equals(ComparisonMemoKey x, ComparisonMemoKey y)
                => x.Hash == y.Hash && StrictEquals(x.Expression, y.Expression);

            /// <inheritdoc/>
            public int GetHashCode(ComparisonMemoKey obj) => obj.Hash;

            /// <summary>
            /// Compares two eligible trees strictly: same node kinds, types, operator metadata and lifting flags,
            /// operands in the same order, parameters by reference, constants by exact bit representation. A
            /// reference-equal pair is equal because eligible trees contain no lambda binding context.
            /// </summary>
            /// <param name="x">The first eligible tree.</param>
            /// <param name="y">The second eligible tree.</param>
            /// <returns><see langword="true"/> when both trees are strictly identical.</returns>
            private static bool StrictEquals(Expression x, Expression y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x.NodeType != y.NodeType || x.Type != y.Type) return false;

                switch (x)
                {
                    case ParameterExpression:
                        return false;

                    case ConstantExpression xc:
                        return ConstantBitsEqual(xc.Value!, ((ConstantExpression)y).Value!);

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

                    default:
                        return false;
                }
            }

            /// <summary>Compares two boxed constants of the same memoizable type by exact representation.</summary>
            /// <param name="x">The first value.</param>
            /// <param name="y">The second value.</param>
            /// <returns><see langword="true"/> when both values have the same bit representation.</returns>
            private static bool ConstantBitsEqual(object x, object y) => (x, y) switch
            {
                (double xd, double yd) => BitConverter.DoubleToInt64Bits(xd) == BitConverter.DoubleToInt64Bits(yd),
                (float xf, float yf) => BitConverter.SingleToInt32Bits(xf) == BitConverter.SingleToInt32Bits(yf),
                (decimal xm, decimal ym) => DecimalBitsEqual(xm, ym),
                _ => x.GetType() == y.GetType() && x.Equals(y),
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
