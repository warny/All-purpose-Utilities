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
    /// </remarks>
    public partial class ExpressionSimplifier
    {
        /// <summary>Minimum comparison depth at which the activation signal is evaluated.</summary>
        private static readonly int ComparisonMemoMinimumDepth = ReadMemoKnob("P9_MEMO_DEPTH", 6);

        /// <summary>Minimum continuous ordinary-arithmetic depth (1 or 2) required at activation.</summary>
        private static readonly int ComparisonMemoMinimumArithmeticDepth = ReadMemoKnob("P9_MEMO_ARITH", 2);

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

        /// <summary>Reads an experiment knob from the environment (experimental build only).</summary>
        /// <param name="name">The environment variable name.</param>
        /// <param name="defaultValue">The value used when the variable is absent or invalid.</param>
        /// <returns>The knob value.</returns>
        private static int ReadMemoKnob(string name, int defaultValue)
            => int.TryParse(Environment.GetEnvironmentVariable(name), out int value) ? value : defaultValue;

        /// <summary>Gets whether the current thread holds an active comparison memo session (focused tests).</summary>
        internal static bool IsComparisonMemoActive => _comparisonMemoActive;

        /// <summary>Gets whether the current thread holds an allocated comparison memo (focused tests).</summary>
        internal static bool HasComparisonMemo => _comparisonMemo is not null;

        /// <summary>Gets the current comparison memo nesting depth (focused tests).</summary>
        internal static int ComparisonMemoDepth => _comparisonMemoDepth;

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
            try
            {
                P9MemoDiagnostics.Request(depth);
                if (!_comparisonMemoActive)
                {
                    if (depth < ComparisonMemoMinimumDepth || !HasMinimumArithmeticDepth(expression))
                    {
                        Expression inactiveResult = ExecuteComparisonSimplification(simplifier, expression);

                        // The session may have been activated by a nested comparison while this one was in
                        // progress: its completed result can still serve later siblings of the session.
                        if (_comparisonMemoActive) RecordComparisonResult(expression, inactiveResult);
                        return inactiveResult;
                    }

                    _comparisonMemoActive = true;
                    P9MemoDiagnostics.Activation(depth);
                }

                if (!ComparisonMemoKey.TryCreate(expression, out ComparisonMemoKey key))
                {
                    return ExecuteComparisonSimplification(simplifier, expression);
                }

                Dictionary<ComparisonMemoKey, Expression>? memo = _comparisonMemo;
                if (memo is not null && memo.TryGetValue(key, out Expression? cached))
                {
                    P9MemoDiagnostics.Hit();
                    return cached;
                }

                Expression result = ExecuteComparisonSimplification(simplifier, expression);

                // Re-read the shared state: the recursive simplification above may have allocated or populated
                // the memo (P6's re-entrancy failure), so nothing observed before the call is assumed unchanged.
                memo = _comparisonMemo;
                if (memo is null)
                {
                    memo = new Dictionary<ComparisonMemoKey, Expression>(ComparisonMemoKeyComparer.Instance);
                    _comparisonMemo = memo;
                    P9MemoDiagnostics.DictionaryAllocation();
                }

                if (memo.TryAdd(key, result)) P9MemoDiagnostics.Insert(memo.Count);
                return result;
            }
            finally
            {
                if (--_comparisonMemoDepth == 0)
                {
                    _comparisonMemoActive = false;
                    _comparisonMemo = null;
                }
            }
        }

        /// <summary>Runs one underlying comparer simplification inside the current comparison session.</summary>
        /// <param name="simplifier">The comparer's shared exact built-in simplifier.</param>
        /// <param name="expression">The operand to simplify.</param>
        /// <returns>The simplified operand.</returns>
        private static Expression ExecuteComparisonSimplification(ExpressionSimplifier simplifier, Expression expression)
        {
            P9MemoDiagnostics.Execution();
            return simplifier.TransformWithinBoundary(expression);
        }

        /// <summary>Records a completed result in the active session's memo when the operand is eligible.</summary>
        /// <param name="expression">The completed operand.</param>
        /// <param name="result">Its successful simplification.</param>
        private static void RecordComparisonResult(Expression expression, Expression result)
        {
            if (!ComparisonMemoKey.TryCreate(expression, out ComparisonMemoKey key)) return;

            Dictionary<ComparisonMemoKey, Expression>? memo = _comparisonMemo;
            if (memo is null)
            {
                memo = new Dictionary<ComparisonMemoKey, Expression>(ComparisonMemoKeyComparer.Instance);
                _comparisonMemo = memo;
                P9MemoDiagnostics.DictionaryAllocation();
            }

            if (memo.TryAdd(key, result)) P9MemoDiagnostics.Insert(memo.Count);
        }

        /// <summary>
        /// Determines in constant time whether <paramref name="expression"/>'s continuous ordinary-arithmetic depth
        /// reaches <see cref="ComparisonMemoMinimumArithmeticDepth"/>: depth 1 means the root is ordinary
        /// arithmetic, depth 2 additionally requires one ordinary-arithmetic child. No subtree is traversed.
        /// </summary>
        /// <param name="expression">The operand at the activation candidate.</param>
        /// <returns><see langword="true"/> when the signal's arithmetic condition holds.</returns>
        private static bool HasMinimumArithmeticDepth(Expression expression)
        {
            if (!IsOrdinaryArithmeticNode(expression)) return false;
            if (ComparisonMemoMinimumArithmeticDepth <= 1) return true;

            return expression switch
            {
                UnaryExpression unary => IsOrdinaryArithmeticNode(unary.Operand),
                BinaryExpression binary => IsOrdinaryArithmeticNode(binary.Left) || IsOrdinaryArithmeticNode(binary.Right),
                _ => false,
            };
        }

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
            /// ineligible, as soon as an unsupported node is met or the per-path node budget is exhausted (which
            /// bounds the cost of strongly shared DAGs).
            /// </summary>
            /// <param name="expression">The operand.</param>
            /// <param name="key">The key when the method returns <see langword="true"/>.</param>
            /// <returns><see langword="true"/> when <paramref name="expression"/> is eligible.</returns>
            public static bool TryCreate(Expression expression, out ComparisonMemoKey key)
            {
                P9MemoDiagnostics.KeyComputation();
                int budget = ComparisonMemoNodeBudget;
                if (TryHash(expression, ref budget, out int hash))
                {
                    key = new ComparisonMemoKey(expression, hash);
                    P9MemoDiagnostics.EligibleKey();
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
            {
                P9MemoDiagnostics.EqualityCheck();
                return x.Hash == y.Hash && StrictEquals(x.Expression, y.Expression);
            }

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

    /// <summary>
    /// Experiment-only internal counters for the P9 memo. Compiled to empty methods unless the
    /// <c>P9_MEMO_COUNTERS</c> symbol is defined, so counter-free builds carry no instrumentation.
    /// </summary>
    internal static class P9MemoDiagnostics
    {
#if P9_MEMO_COUNTERS
        [ThreadStatic] private static long _requests, _executions, _hits, _keys, _eligible, _equalities, _activations, _dictionaries, _inserts, _peak;
        [ThreadStatic] private static int[]? _activationDepths;
#endif

        /// <summary>Resets every counter of the current thread.</summary>
        internal static void Reset()
        {
#if P9_MEMO_COUNTERS
            _requests = _executions = _hits = _keys = _eligible = _equalities = _activations = _dictionaries = _inserts = _peak = 0;
            _activationDepths = new int[64];
#endif
        }

        /// <summary>Gets the CSV header matching <see cref="Snapshot"/>.</summary>
        /// <returns>The header.</returns>
        internal static string Header() => "requests,executions,hits,keyComputations,eligibleKeys,equalityChecks,activations,dictionaries,inserts,peakEntries,activationDepths";

        /// <summary>Gets the current thread's counters as CSV.</summary>
        /// <returns>The counters.</returns>
        internal static string Snapshot()
        {
#if P9_MEMO_COUNTERS
            var depths = new System.Text.StringBuilder();
            for (int depth = 0; _activationDepths is not null && depth < _activationDepths.Length; depth++)
            {
                if (_activationDepths[depth] > 0) depths.Append($"d{depth}:{_activationDepths[depth]} ");
            }
            return $"{_requests},{_executions},{_hits},{_keys},{_eligible},{_equalities},{_activations},{_dictionaries},{_inserts},{_peak},{depths}";
#else
            return "counters-disabled";
#endif
        }

        /// <summary>Counts a comparer-triggered simplification request.</summary>
        /// <param name="depth">The request's comparison depth.</param>
        [System.Diagnostics.Conditional("P9_MEMO_COUNTERS")]
        internal static void Request(int depth)
        {
#if P9_MEMO_COUNTERS
            _requests++;
#endif
        }

        /// <summary>Counts an actual underlying simplification execution.</summary>
        [System.Diagnostics.Conditional("P9_MEMO_COUNTERS")]
        internal static void Execution()
        {
#if P9_MEMO_COUNTERS
            _executions++;
#endif
        }

        /// <summary>Counts a memo hit.</summary>
        [System.Diagnostics.Conditional("P9_MEMO_COUNTERS")]
        internal static void Hit()
        {
#if P9_MEMO_COUNTERS
            _hits++;
#endif
        }

        /// <summary>Counts a strict key computation (eligibility check).</summary>
        [System.Diagnostics.Conditional("P9_MEMO_COUNTERS")]
        internal static void KeyComputation()
        {
#if P9_MEMO_COUNTERS
            _keys++;
#endif
        }

        /// <summary>Counts an eligible key.</summary>
        [System.Diagnostics.Conditional("P9_MEMO_COUNTERS")]
        internal static void EligibleKey()
        {
#if P9_MEMO_COUNTERS
            _eligible++;
#endif
        }

        /// <summary>Counts a strict structural equality check.</summary>
        [System.Diagnostics.Conditional("P9_MEMO_COUNTERS")]
        internal static void EqualityCheck()
        {
#if P9_MEMO_COUNTERS
            _equalities++;
#endif
        }

        /// <summary>Counts a session activation at <paramref name="depth"/>.</summary>
        /// <param name="depth">The activating request's comparison depth.</param>
        [System.Diagnostics.Conditional("P9_MEMO_COUNTERS")]
        internal static void Activation(int depth)
        {
#if P9_MEMO_COUNTERS
            _activations++;
            if (_activationDepths is not null && depth < _activationDepths.Length) _activationDepths[depth]++;
#endif
        }

        /// <summary>Counts a memo dictionary allocation.</summary>
        [System.Diagnostics.Conditional("P9_MEMO_COUNTERS")]
        internal static void DictionaryAllocation()
        {
#if P9_MEMO_COUNTERS
            _dictionaries++;
#endif
        }

        /// <summary>Counts an insertion and tracks the peak session entry count.</summary>
        /// <param name="count">The memo entry count after insertion.</param>
        [System.Diagnostics.Conditional("P9_MEMO_COUNTERS")]
        internal static void Insert(int count)
        {
#if P9_MEMO_COUNTERS
            _inserts++;
            if (count > _peak) _peak = count;
#endif
        }
    }
}
