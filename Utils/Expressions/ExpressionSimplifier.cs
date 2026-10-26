using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using Utils.Expressions;
using Utils.Objects;

namespace Utils.Mathematics.Expressions
{
    /// <summary>
    /// Provides methods to simplify or rewrite expression trees by applying various transformations,
    /// typically related to arithmetic identities or constant folding.
    /// This partial class works alongside other parts of <see cref="ExpressionSimplifier"/> to compose
    /// a complete transformation pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Symbolic-equivalence contract (stage S3).</b> <see cref="ExpressionSimplifier"/> is a
    /// <em>symbolic algebra</em> simplifier. It is <b>not</b> a strict CLR/IEEE-754 execution-preserving
    /// optimizer. Reassociation/reordering of addition and multiplication, and identities such as
    /// <c>x * 0 -&gt; 0</c> or <c>x^0 -&gt; 1</c>, are algebraic identities over the assumed mathematical
    /// domain; they are not promised to be bit-for-bit identical to the original CLR evaluation for every
    /// floating-point/decimal input. In particular, <see cref="Simplify(Expression)"/> does not guarantee
    /// identical rounding, reassociation order, <c>NaN</c> propagation, infinities, signed-zero behavior,
    /// overflow, or exception timing across a rewrite. Consumers that require exact CLR execution semantics
    /// must not rely on a simplified expression evaluating identically to the source expression in every
    /// corner case; they must rely only on symbolic/mathematical equivalence over the identity's domain.
    /// </para>
    /// <para>
    /// <b>Purity assumption.</b> Every algebraic rewrite (cancellation, factoring, canonical reordering,
    /// constant folding, and <see cref="InvokeExpression"/>'s direct beta-substitution) assumes its operand
    /// sub-expressions are pure/referentially transparent. Evaluation count and order are not guaranteed to
    /// be preserved for an expression tree with observable side effects (a method call with a side effect,
    /// a property with an observable getter, etc.): a rewrite may evaluate a sub-expression zero, one, or
    /// more times relative to the source. This stage deliberately does not add a general side-effect/purity
    /// analyzer; callers who need side-effecting sub-expressions to run in the original count/order must not
    /// pass such expressions through this simplifier.
    /// </para>
    /// <para>
    /// <b>Domain-sensitive identities.</b> Rules with a mathematical precondition are valid only on their
    /// common domain. <c>Log(x) + Log(y) -&gt; Log(x*y)</c> (and the <c>Log10</c>/subtraction variants)
    /// assume <c>x</c> and <c>y</c> lie in the supported positive finite real domain; power identities such
    /// as <c>0^x -&gt; 0</c> and <c>x^a * x^b -&gt; x^(a+b)</c> likewise have base/exponent preconditions not
    /// represented in the expression tree. This stage does not introduce a general domain/constraint solver:
    /// it characterizes and preserves these identities on their intended domain rather than restricting or
    /// broadening it.
    /// </para>
    /// <para>
    /// <b>What the symbolic contract does NOT authorize.</b> The above does not license treating a
    /// custom/user-defined operator as ordinary commutative arithmetic, collapsing lifted
    /// (<see cref="BinaryExpression.IsLifted"/>/<see cref="BinaryExpression.IsLiftedToNull"/>) nullable
    /// semantics, or turning truncating integer division into field-style division. Those are production
    /// correctness bugs, not accepted symbolic-vs-exact differences, and every algebraic rule that folds,
    /// reorders, factors, or discards a node must first prove the node is ordinary, non-lifted,
    /// predefined-operator arithmetic via <see cref="IsOrdinaryUnaryNegate(UnaryExpression)"/>,
    /// <see cref="IsOrdinaryBinaryArithmetic(BinaryExpression)"/>, or the narrower
    /// <see cref="IsOrdinaryFieldDivision(BinaryExpression)"/> for identities that require field (not ring)
    /// division. See those methods' remarks for how "ordinary" is determined structurally rather than by a
    /// fragile hard-coded assumption.
    /// </para>
    /// </remarks>
    public partial class ExpressionSimplifier : ExpressionTransformer
    {
        /// <summary>Stores structural metrics by expression reference for the active diagnostic lifetime.</summary>
        [ThreadStatic]
        private static Dictionary<Expression, ExpressionMetrics>? _expressionMetricsCache;

        /// <summary>Tracks nested structural-metric lifetimes on the current thread.</summary>
        [ThreadStatic]
        private static int _expressionMetricsScopeDepth;

        /// <summary>Counts structural cache misses during the active diagnostic lifetime on this thread.</summary>
        [ThreadStatic]
        private static int _expressionMetricAnalysisCount;

        /// <summary>
        /// The exact built-in <see cref="ExpressionSimplifier"/> requires no extra preparation beyond
        /// what <see cref="ExpressionTransformer.TransformCore(Expression)"/> already does, so this
        /// simply delegates to it - except that it also establishes a fresh, empty ambient lexical-scope
        /// boundary around the call. See <see cref="_lexicalScopeStack"/>'s remarks ("Independent top-level
        /// calls") for why this boundary exists here rather than nowhere, and why it does not affect the
        /// ordinary, same-tree recursive descent that runs through <see cref="TransformCore"/> directly.
        /// </summary>
        /// <param name="expression">The expression to transform.</param>
        /// <returns>A possibly rewritten expression.</returns>
        /// <remarks>
        /// Unlike most other S4 integration points in this class, this boundary is NOT gated to the exact
        /// built-in runtime type (S4 review, round 7): the ambient scope stack it manages is consulted only
        /// by <see cref="OnEnterLambdaScope"/>/<see cref="OnExitLambdaScope"/>/<see cref="CaptureLexicalScopeSnapshot"/>,
        /// which - as of round 7 - are themselves also unconditional, so any <see cref="ExpressionSimplifier"/>
        /// subclass gets correct, isolated ambient-scope tracking too, rather than either leaking scope
        /// across independent calls or (round 6's now-superseded approach) silently losing bound-parameter
        /// canonicalization altogether. See those members' remarks for the round-6/round-7 history.
        /// </remarks>
        public override Expression Transform(Expression expression)
        {
            // Detach the caller's comparison-memo session (S5 P9): an independent Transform call never shares
            // memo entries with, nor activates or releases, a session that is in progress on this thread.
            int callerMemoDepth = _comparisonMemoDepth;
            bool callerMemoActive = _comparisonMemoActive;
            Dictionary<ComparisonMemoKey, Expression>? callerMemo = _comparisonMemo;
            _comparisonMemoDepth = 0;
            _comparisonMemoActive = false;
            _comparisonMemo = null;
            try
            {
                return TransformWithinBoundary(expression);
            }
            finally
            {
                _comparisonMemoDepth = callerMemoDepth;
                _comparisonMemoActive = callerMemoActive;
                _comparisonMemo = callerMemo;
            }
        }

        /// <summary>
        /// Runs <see cref="ExpressionTransformer.TransformCore(Expression)"/> inside a fresh lexical-scope (and,
        /// when capture is active, diagnostic-metrics) boundary, without detaching the current comparison-memo
        /// session. Called by <see cref="Transform(Expression)"/> after it has detached the caller's session, and
        /// directly by <see cref="SimplifyForComparisonInSession"/> so that the comparer's own nested pass joins
        /// the session it belongs to.
        /// </summary>
        /// <param name="expression">The expression to transform.</param>
        /// <returns>A possibly rewritten expression.</returns>
        private Expression TransformWithinBoundary(Expression expression)
        {
            bool diagnosticsActive = ExpressionComparer.IsDiagnosticCaptureActive;
            ExpressionMetricsScope metricsScope = diagnosticsActive ? BeginExpressionMetricsScope() : default;
            List<ParameterExpression[]>? callerScope = _lexicalScopeStack;
            _lexicalScopeStack = null;
            try
            {
                return FoldNaNOperand(TransformCore(expression));
            }
            finally
            {
                _lexicalScopeStack = callerScope;
                if (diagnosticsActive) metricsScope.Dispose();
            }
        }

        /// <summary>
        /// Immutable structural measurements for one expression object.
        /// </summary>
        internal readonly struct ExpressionMetrics
        {
            /// <summary>Initializes a structural measurement.</summary>
            /// <param name="nodeCount">The logical structural-work magnitude.</param>
            /// <param name="arithmeticNodeCount">The ordinary-arithmetic-work magnitude.</param>
            /// <param name="arithmeticDepth">The continuous ordinary-arithmetic depth at the root.</param>
            /// <param name="mightInvokeUserCode">Whether simplification may invoke user code.</param>
            internal ExpressionMetrics(float nodeCount, float arithmeticNodeCount, int arithmeticDepth, bool mightInvokeUserCode)
            {
                NodeCount = nodeCount;
                ArithmeticNodeCount = arithmeticNodeCount;
                ArithmeticDepth = arithmeticDepth;
                MightInvokeUserCode = mightInvokeUserCode;
            }

            /// <summary>
            /// Gets the logical structural-work magnitude. A shared sub-expression reached through two
            /// branches is counted twice because this measures potential traversal work, not distinct CLR
            /// <see cref="Expression"/> instances. Small exactly representable values are exact; very large
            /// shared DAGs may lose low-order precision. The value is intended for magnitude and threshold
            /// comparisons, for which <see cref="float.PositiveInfinity"/> is also valid.
            /// </summary>
            internal float NodeCount { get; }

            /// <summary>
            /// Gets the logical ordinary-arithmetic-work magnitude. Shared arithmetic reached through two
            /// branches is counted twice rather than deduplicated by object identity. Small exactly
            /// representable values are exact; very large shared DAGs may be approximate. The value is a
            /// threshold signal, not an exact count of distinct <see cref="Expression"/> instances.
            /// </summary>
            internal float ArithmeticNodeCount { get; }

            /// <summary>Gets the continuous ordinary-arithmetic depth at the root.</summary>
            internal int ArithmeticDepth { get; }

            /// <summary>Gets whether simplification may invoke arbitrary user code.</summary>
            internal bool MightInvokeUserCode { get; }
        }

        /// <summary>Represents one nested lifetime of the thread-local structural-metric cache.</summary>
        internal readonly struct ExpressionMetricsScope : IDisposable
        {
            /// <summary>Ends this structural-metric cache lifetime.</summary>
            public void Dispose()
            {
                _expressionMetricsScopeDepth--;
                if (_expressionMetricsScopeDepth == 0)
                {
                    _expressionMetricsCache = null;
                    _expressionMetricAnalysisCount = 0;
                }
            }
        }

        /// <summary>Begins a nested thread-local structural-metric cache lifetime.</summary>
        /// <returns>A scope which releases the cache after the outermost operation.</returns>
        internal static ExpressionMetricsScope BeginExpressionMetricsScope()
        {
            if (_expressionMetricsScopeDepth++ == 0)
            {
                _expressionMetricsCache = null;
                _expressionMetricAnalysisCount = 0;
            }

            return new ExpressionMetricsScope();
        }

        /// <summary>Gets structural metrics, reusing the current reference-keyed diagnostic cache.</summary>
        /// <param name="expression">The expression to measure.</param>
        /// <returns>The immutable metrics for <paramref name="expression"/>.</returns>
        internal static ExpressionMetrics GetExpressionMetrics(Expression expression)
        {
            ArgumentNullException.ThrowIfNull(expression);
            if (_expressionMetricsScopeDepth == 0)
            {
                using ExpressionMetricsScope scope = BeginExpressionMetricsScope();
                return GetExpressionMetrics(expression);
            }

            Dictionary<Expression, ExpressionMetrics> cache = _expressionMetricsCache ??=
                new Dictionary<Expression, ExpressionMetrics>(ReferenceEqualityComparer.Instance);

            if (cache.TryGetValue(expression, out ExpressionMetrics cached)) return cached;

            _expressionMetricAnalysisCount++;
            ExpressionMetrics metrics = AnalyzeExpression(expression);
            cache.Add(expression, metrics);
            return metrics;
        }

        /// <summary>Gets the current thread's number of cache-miss analyses for focused diagnostics.</summary>
        internal static int ExpressionMetricAnalysisCount => _expressionMetricAnalysisCount;

        /// <summary>Gets whether the current thread retains a structural-metric cache.</summary>
        internal static bool HasExpressionMetricsCache => _expressionMetricsCache is not null;

        /// <summary>Analyzes one node and its conservatively traversed children without executing user code.</summary>
        /// <remarks>
        /// The traversal deliberately recognizes only parameters, constants, lambdas, unary and binary
        /// expressions, method calls, and member expressions. Unknown nodes are conservatively unsafe and
        /// are not reduced or visited through <see cref="ExpressionVisitor"/>. Non-safe constants retain the
        /// historical P3 classification based on numeric and known-safe constant types.
        /// </remarks>
        /// <param name="expression">The non-null expression node to analyze.</param>
        /// <returns>The combined safety and structural metrics.</returns>
        private static ExpressionMetrics AnalyzeExpression(Expression expression)
        {
            ExpressionMetrics children = expression switch
            {
                ParameterExpression => default,
                ConstantExpression constant => new ExpressionMetrics(0f, 0f, 0,
                    constant.Value is not null
                        && !Types.Number.Contains(constant.Type)
                        && !ExpressionComparer.IsKnownSafeConstantValue(constant.Value)),
                LambdaExpression lambda => GetExpressionMetrics(lambda.Body),
                UnaryExpression unary => GetExpressionMetrics(unary.Operand),
                BinaryExpression binary => CombineMetrics(
                    GetExpressionMetrics(binary.Left),
                    GetExpressionMetrics(binary.Right),
                    GetOptionalExpressionMetrics(binary.Conversion)),
                MethodCallExpression call => AnalyzeMethodCallChildren(call),
                MemberExpression member => GetOptionalExpressionMetrics(member.Expression),
                _ => new ExpressionMetrics(0f, 0f, 0, true),
            };

            float arithmeticNodeCount = children.ArithmeticNodeCount;

            int arithmeticDepth = 0;
            if (expression is UnaryExpression unaryArithmetic && IsOrdinaryUnaryNegate(unaryArithmetic))
            {
                arithmeticNodeCount += 1f;
                arithmeticDepth = 1 + GetExpressionMetrics(unaryArithmetic.Operand).ArithmeticDepth;
            }
            else if (expression is BinaryExpression binaryArithmetic && IsOrdinaryBinaryArithmetic(binaryArithmetic))
            {
                arithmeticNodeCount += 1f;
                arithmeticDepth = 1 + int.Max(
                    GetExpressionMetrics(binaryArithmetic.Left).ArithmeticDepth,
                    GetExpressionMetrics(binaryArithmetic.Right).ArithmeticDepth);
            }

            return new ExpressionMetrics(
                1f + children.NodeCount,
                arithmeticNodeCount,
                arithmeticDepth,
                children.MightInvokeUserCode);
        }

        /// <summary>Gets metrics for an optional expression, returning zero metrics when it is absent.</summary>
        /// <param name="expression">The optional expression.</param>
        /// <returns>The expression metrics or zero metrics.</returns>
        private static ExpressionMetrics GetOptionalExpressionMetrics(Expression? expression)
            => expression is null ? default : GetExpressionMetrics(expression);

        /// <summary>Combines child metrics without propagating arithmetic depth through their parent.</summary>
        /// <param name="first">The first child metrics.</param>
        /// <param name="second">The second child metrics.</param>
        /// <param name="third">The optional third child metrics.</param>
        /// <returns>The aggregate child metrics.</returns>
        private static ExpressionMetrics CombineMetrics(ExpressionMetrics first, ExpressionMetrics second, ExpressionMetrics third = default)
            => new(
                first.NodeCount + second.NodeCount + third.NodeCount,
                first.ArithmeticNodeCount + second.ArithmeticNodeCount + third.ArithmeticNodeCount,
                0,
                first.MightInvokeUserCode || second.MightInvokeUserCode || third.MightInvokeUserCode);

        /// <summary>Aggregates a method call's optional receiver and arguments without temporary collections.</summary>
        /// <param name="call">The method call to analyze.</param>
        /// <returns>The aggregate metrics of the call's children.</returns>
        private static ExpressionMetrics AnalyzeMethodCallChildren(MethodCallExpression call)
        {
            ExpressionMetrics result = GetOptionalExpressionMetrics(call.Object);
            for (int i = 0; i < call.Arguments.Count; i++)
            {
                result = CombineMetrics(result, GetExpressionMetrics(call.Arguments[i]));
            }

            return result;
        }

        /// <summary>
        /// Simplifies the given <paramref name="e"/> by calling <see cref="Transform(Expression)"/>.
        /// </summary>
        /// <param name="e">The <see cref="Expression"/> to simplify.</param>
        /// <returns>A simplified version of <paramref name="e"/>, if any transformation rules match.</returns>
        /// <remarks>
        /// See the "Symbolic-equivalence contract" section of this type's remarks: the result is
        /// symbolically/mathematically equivalent to <paramref name="e"/> over the algebraic identities'
        /// assumed domain and under the assumption that <paramref name="e"/>'s sub-expressions are pure. It
        /// is not guaranteed to be a bit-for-bit CLR/IEEE-754-identical evaluator, and it never treats a
        /// custom operator, lifted nullable arithmetic, or truncating integer division as ordinary algebra.
        /// </remarks>
        public Expression Simplify(Expression e)
        {
            return Transform(e);
        }

        /// <summary>
        /// Prepares an expression for transformation by calling <see cref="ExpressionTransformer.TransformCore(Expression)"/>
        /// Subclasses can override for custom logic, but here it simply re-applies <see cref="ExpressionTransformer.TransformCore(Expression)"/>
        /// </summary>
        /// <param name="e">The expression to prepare, or <see langword="null"/> for an absent optional sub-expression such as <see cref="Expression.Rethrow()"/>'s <see langword="null"/> operand.</param>
        /// <returns>The transformed expression, or <see langword="null"/> unchanged, for the exact built-in type.</returns>
        /// <remarks>
        /// The null short-circuit is gated to the exact built-in runtime type for the same reason as
        /// <see cref="RebuildUnaryExpression"/>/<see cref="RebuildLambdaExpression"/>: without it,
        /// <see cref="ExpressionTransformer.TransformCore(Expression)"/> dereferences a null
        /// <c>context.Expression</c> and throws <see cref="NullReferenceException"/> — historically true for
        /// every runtime type, including a derived subclass, which therefore still observes it unless it
        /// overrides this method itself. The transformed operand then goes through <see cref="FoldNaNOperand"/>
        /// (S5 P11b), for every runtime type.
        /// </remarks>
        protected override Expression PrepareExpression(Expression e)
        {
            if (e is null && GetType() == typeof(ExpressionSimplifier))
            {
                return null;
            }

            return FoldNaNOperand(TransformCore(e));
        }

        /// <summary>
        /// For the exact built-in <see cref="ExpressionSimplifier"/> runtime type, reconstructs a unary node
        /// via <see cref="Expression.MakeUnary(ExpressionType, Expression, Type, System.Reflection.MethodInfo)"/>, preserving
        /// <see cref="UnaryExpression.Method"/> and <see cref="Expression.Type"/> (notably a typed
        /// <see cref="Expression.Throw(Expression, Type)"/>'s declared result type) instead of the
        /// historical, metadata-dropping per-node-type factories. A derived <see cref="ExpressionSimplifier"/>
        /// subclass keeps the historical <see cref="ExpressionTransformer"/> behavior unless it explicitly
        /// overrides this itself, mirroring <see cref="FinalizeExpression"/>'s own exact-runtime-type guard.
        /// </summary>
        /// <param name="expression">The original unary expression being rebuilt.</param>
        /// <param name="operand">The (already prepared) operand.</param>
        /// <returns>A unary expression preserving the original node's metadata, for the exact built-in type; the historical reconstruction otherwise.</returns>
        internal override UnaryExpression RebuildUnaryExpression(UnaryExpression expression, Expression operand)
        {
            if (GetType() != typeof(ExpressionSimplifier))
            {
                return base.RebuildUnaryExpression(expression, operand);
            }

            return (UnaryExpression)Expression.MakeUnary(expression.NodeType, operand, expression.Type, expression.Method);
        }

        /// <summary>
        /// For the exact built-in <see cref="ExpressionSimplifier"/> runtime type, reconstructs a lambda
        /// preserving its original <see cref="LambdaExpression.Type"/> (delegate type),
        /// <see cref="LambdaExpression.Name"/>, and <see cref="LambdaExpression.TailCall"/>, at every nesting
        /// depth (this method runs recursively, once per lambda encountered, on the same simplifier
        /// instance). A derived <see cref="ExpressionSimplifier"/> subclass keeps the historical,
        /// metadata-dropping <see cref="ExpressionTransformer"/> reconstruction unless it explicitly
        /// overrides this itself.
        /// </summary>
        /// <param name="expression">The original lambda expression being rebuilt.</param>
        /// <param name="body">The (already prepared/transformed) body.</param>
        /// <param name="parameters">The (already prepared) parameters, in declaration order.</param>
        /// <returns>A lambda expression preserving the original node's metadata, for the exact built-in type; the historical reconstruction otherwise.</returns>
        /// <remarks>The body, transformed without <see cref="PrepareExpression"/>, goes through <see cref="FoldNaNOperand"/> first (S5 P11b), for every runtime type.</remarks>
        internal override LambdaExpression RebuildLambdaExpression(LambdaExpression expression, Expression body, ParameterExpression[] parameters)
        {
            body = FoldNaNOperand(body);
            if (GetType() != typeof(ExpressionSimplifier))
            {
                return base.RebuildLambdaExpression(expression, body, parameters);
            }

            return Expression.Lambda(expression.Type, body, expression.Name, expression.TailCall, parameters);
        }

        #region Structural canonicalization (S4) — ambient lexical scope tracking

        /// <summary>
        /// Per-thread stack of currently-open lambda parameter scopes, outermost first. Populated
        /// exclusively by <see cref="OnEnterLambdaScope"/>/<see cref="OnExitLambdaScope"/>, which
        /// <see cref="ExpressionTransformer.PrepareLambda"/> calls (via its private caller) strictly
        /// around the single synchronous recursive descent into a lambda's body — see those hooks' remarks
        /// on <see cref="ExpressionTransformer"/> for why this exists at all. As of S4 review round 7,
        /// those two hooks (and this field they share) are unconditional — not gated to the exact built-in
        /// <see cref="ExpressionSimplifier"/> runtime type — so a subclass populates and consults the same
        /// tracking, not a separate or absent one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why a <see cref="ThreadStaticAttribute"/> field, not per-invocation explicit context.</b>
        /// <see cref="ExpressionSimplifier.Simplify(Expression)"/>, <see cref="ExpressionExtensions.Simplify(Expression)"/>
        /// and <see cref="ExpressionComparer"/> all recurse through the same generic, public/protected
        /// virtual <see cref="ExpressionTransformer"/> engine (<c>TransformCore</c>,
        /// <c>PrepareExpression</c>, <c>FinalizeExpression</c>, ...). Threading an explicit scope parameter
        /// through that entire engine would require changing the signature of
        /// <see cref="ExpressionTransformer.PrepareExpression(Expression)"/> and
        /// <see cref="ExpressionTransformer.FinalizeExpression(Expression, Expression[])"/> — both
        /// <see langword="protected virtual"/> extensibility points a third-party subclass in another
        /// assembly can already override — which the S4 roadmap entry explicitly rules out ("do not
        /// introduce a new public extensibility contract unnecessarily"). A per-thread stack, populated and
        /// consumed only through the narrow <see langword="internal"/> hooks below, avoids that: it is
        /// consulted only by <see cref="CaptureLexicalScopeSnapshot"/>, called once per canonicalization
        /// (see <see cref="CanonicalizeAdditiveExpression"/>/<see cref="CanonicalizeMultiplicativeExpression"/>),
        /// and never stored as shared mutable INSTANCE state on this type (which is shared: see
        /// <see cref="ExpressionExtensions"/>' and <see cref="ExpressionComparer"/>'s own static
        /// <see cref="ExpressionSimplifier"/> instances).
        /// </para>
        /// <para>
        /// <b>Concurrency.</b> <see cref="ThreadStaticAttribute"/> gives every OS thread its own
        /// independent list, so two threads calling <see cref="Simplify(Expression)"/> concurrently —
        /// including through the very same shared <see cref="ExpressionSimplifier"/> instance — never
        /// observe or mutate each other's scope stack.
        /// </para>
        /// <para>
        /// <b>Re-entrancy within the SAME traversal.</b> A rule can call back into <c>TransformCore</c>
        /// directly (never through <see cref="Transform"/> - see "Independent top-level calls" below).
        /// Every such nested call still runs on the SAME thread, synchronously, fully nested within the
        /// outer call's own call stack (it returns before the outer call resumes). Because
        /// <see cref="OnEnterLambdaScope"/>/<see cref="OnExitLambdaScope"/> are always paired via a
        /// <c>try</c>/<c>finally</c> in <see cref="ExpressionTransformer.PrepareLambda"/>, any such nested
        /// traversal pushes and pops exactly the frames IT opens, leaving the stack exactly as the outer
        /// traversal left it once the nested call returns.
        /// </para>
        /// <para>
        /// <b>Independent top-level calls (S4 review, round 4).</b> <see cref="Transform"/> - the only entry
        /// point <see cref="Simplify(Expression)"/>, <see cref="ExpressionExtensions.Simplify(Expression)"/>
        /// and <see cref="ExpressionComparer"/>'s own internal re-simplification of its operands
        /// (<c>ExpressionComparer.Equals(Expression?, Expression?)</c>/<c>GetHashCode(Expression)</c>, which
        /// simplify each operand before comparing/hashing it) ever reach this stack through - establishes a
        /// fresh, empty scope boundary around its call to <c>TransformCore</c>, saving and restoring
        /// whatever the CALLER's own ambient stack was. Without this boundary, a <see cref="Transform"/> call
        /// re-entered from arbitrary code the simplifier does not control (a constant value's own
        /// <see cref="object.Equals(object?)"/> override, reachable from <c>AdditionOfEqualsElements</c>'s
        /// non-safe <see cref="ExpressionComparer.Default"/> comparison, calling back into a brand-new,
        /// unrelated <c>new ExpressionSimplifier().Simplify(...)</c> on its own thread-shared stack) would
        /// incorrectly inherit the OUTER call's still-open lambda frames merely because it happens to run on
        /// the same OS thread while those frames are open — even though the inner call's expression has no
        /// actual relationship to the outer lambda. Concretely: reusing the outer lambda's own bound
        /// <see cref="ParameterExpression"/> instances (same object references) inside such an independent
        /// inner call would make <see cref="ExpressionCanonicalOrder.BuildKey"/> see them as bound at the
        /// outer lambda's depth/position (its scope search is by object reference - see
        /// <c>ExpressionCanonicalOrder.BuildParameter</c>) and reorder them accordingly, instead of treating
        /// them as free (their only correct classification from the independent inner call's own,
        /// self-contained point of view). Each call to the public <see cref="Transform"/>/
        /// <see cref="Simplify(Expression)"/> API is therefore self-contained: it never depends on, and
        /// never affects, any concurrently-in-progress ambient state elsewhere on the same thread. This does
        /// mean <see cref="ExpressionComparer"/>'s own internal re-simplification of its operands no longer
        /// sees an outer, still-open lambda scope either (a change from the pre-round-4 behavior); this is
        /// intentional, for the same self-containment reason, and does not affect the correctness of
        /// <see cref="ExpressionComparer.Equals(Expression?, Expression?)"/>'s subsequent structural
        /// comparison, which resolves parameter binding independently via its own
        /// <c>ParameterBindingContext</c>, not via this ambient stack. Exercised by
        /// <c>ExpressionSimplifierStructuralCanonicalizationTests</c>' concurrency/re-entrancy coverage.
        /// </para>
        /// </remarks>
        [ThreadStatic]
        private static List<ParameterExpression[]>? _lexicalScopeStack;

        private static List<ParameterExpression[]> LexicalScopeStack => _lexicalScopeStack ??= [];

        /// <inheritdoc/>
        /// <remarks>
        /// Unconditional as of S4 review round 7: unlike most other S4 integration points in this class,
        /// this hook is NOT gated to the exact built-in <see cref="ExpressionSimplifier"/> runtime type.
        /// There is no pre-S4 historical behavior at stake here (this hook is new to S4, not a
        /// reconstruction-fidelity concern like <see cref="RebuildLambdaExpression"/>/
        /// <see cref="RebuildUnaryExpression"/>), and being <see langword="internal"/>, only a same-assembly
        /// (or <c>InternalsVisibleTo</c>) subclass could ever reach this override at all - gating it further
        /// to the exact type only meant such a subclass's OWN bound parameters were silently misclassified
        /// as free during its own canonicalization (round 6 initially worked around this by skipping
        /// canonicalization for a subclass entirely instead; round 7 fixes the actual cause here, so a
        /// subclass regains full, correct canonicalization instead of losing it).
        /// </remarks>
        internal override void OnEnterLambdaScope(LambdaExpression original, ParameterExpression[] parameters)
        {
            LexicalScopeStack.Add(parameters);
        }

        /// <inheritdoc/>
        /// <remarks>See <see cref="OnEnterLambdaScope"/>'s remarks: unconditional as of S4 review round 7, for the same reason.</remarks>
        internal override void OnExitLambdaScope(LambdaExpression original, ParameterExpression[] parameters)
        {
            List<ParameterExpression[]> stack = LexicalScopeStack;
            stack.RemoveAt(stack.Count - 1);
        }

        /// <summary>
        /// Captures an immutable snapshot of the currently-open lambda parameter scopes (outermost first) for
        /// use by <see cref="ExpressionCanonicalOrder.BuildKeyFromSnapshot"/>/<see cref="ExpressionCanonicalOrder.BuildKeysFromSnapshot"/>.
        /// Taken once per canonicalization call: every term/factor being ordered in that one call is a
        /// sibling sub-expression of the same node, so they all share the same enclosing scope. Declared to
        /// return the concrete array type (not <see cref="IReadOnlyList{T}"/>) so callers can reach
        /// <c>ExpressionCanonicalOrder</c>'s zero-copy snapshot overloads - this array is fresh (or the shared
        /// <see cref="Array.Empty{T}"/> instance) and nothing else retains a reference to it, which is exactly
        /// what those overloads require (PR #606 review round 2).
        /// </summary>
        /// <returns>The snapshot; empty when no lambda currently encloses the node being canonicalized.</returns>
        private static ParameterExpression[][] CaptureLexicalScopeSnapshot()
            => LexicalScopeStack.Count == 0 ? [] : LexicalScopeStack.ToArray();

        #endregion

        #region Numeric constant classification

        /// <summary>
        /// Determines whether <paramref name="value"/> is a native numeric primitive (one of <see cref="Types.Number"/>)
        /// numerically equal to the small integer <paramref name="target"/>.
        /// </summary>
        /// <param name="value">The boxed constant value.</param>
        /// <param name="target">The integer to compare with (the rules use <c>0</c>, <c>1</c> and <c>-1</c>).</param>
        /// <returns><see langword="true"/> when the value equals <paramref name="target"/> exactly.</returns>
        /// <remarks>
        /// The single numeric-identity test of this class (S5 P11). Each primitive is compared in its own type, so
        /// <c>NaN</c> equals nothing, infinities equal no finite value, <c>-0.0</c> equals zero, a decimal compares at
        /// any scale, and no conversion can overflow or underflow. <see cref="NumberUtils.CompareNumeric"/>, used before
        /// P11, converted floating-point values to <see cref="decimal"/>: that threw <see cref="OverflowException"/> on
        /// <c>NaN</c>, infinities and magnitudes beyond the decimal range, and rounded <see cref="double.Epsilon"/> to
        /// zero. Any other runtime type returns <see langword="false"/>, so no third-party numeric code runs; ordinary
        /// arithmetic is only defined on <see cref="Types.Number"/>, so a rule never sees such a value anyway.
        /// The per-type comparisons are dispatched through <see cref="NumericValueComparers"/> (AGENTS.md: no switch
        /// of more than ten cases).
        /// </remarks>
        private static bool NumericValueEquals(object? value, int target)
            => value is not null
                && NumericValueComparers.TryGetValue(value.GetType(), out Func<object, int, bool>? comparer)
                && comparer(value, target);

        /// <summary>
        /// Exact comparison of a boxed native numeric primitive with a small integer, indexed by the primitive's
        /// runtime type; used by <see cref="NumericValueEquals"/>.
        /// </summary>
        /// <remarks>
        /// Each entry compares in the primitive's own type, so no conversion can overflow or lose precision. Read-only
        /// after the type initializer, hence safe for concurrent lookups.
        /// </remarks>
        private static readonly Dictionary<Type, Func<object, int, bool>> NumericValueComparers = new()
        {
            [typeof(double)] = static (value, target) => (double)value == target,
            [typeof(float)] = static (value, target) => (float)value == target,
            [typeof(decimal)] = static (value, target) => (decimal)value == target,
            [typeof(int)] = static (value, target) => (int)value == target,
            [typeof(long)] = static (value, target) => (long)value == target,
            [typeof(uint)] = static (value, target) => (uint)value == target,
            [typeof(ulong)] = static (value, target) => target >= 0 && (ulong)value == (ulong)target,
            [typeof(short)] = static (value, target) => (short)value == target,
            [typeof(ushort)] = static (value, target) => (ushort)value == target,
            [typeof(sbyte)] = static (value, target) => (sbyte)value == target,
            [typeof(byte)] = static (value, target) => (byte)value == target,
        };


        /// <summary>
        /// Determines whether <paramref name="expression"/> is a <see cref="double"/> or <see cref="float"/> constant
        /// holding <c>NaN</c> or an infinity.
        /// </summary>
        /// <param name="expression">The operand to test.</param>
        /// <returns><see langword="true"/> for a non-finite floating-point constant.</returns>
        /// <remarks>
        /// The multiplicative zero/one identities decline on such an operand (S5 P11), so the exact constant folding
        /// that follows them applies instead: <c>inf * 0</c> is <c>NaN</c>, not <c>0</c>. Identities on a non-constant
        /// operand keep the symbolic treatment of the "Symbolic-equivalence contract". The division and power
        /// identities decline on any constant operand instead (S5 P12/P13, see <see cref="DivideWithZero"/> and
        /// <see cref="PowerOfZeroOrOne"/>).
        /// </remarks>
        private static bool IsNonFiniteFloatingConstant(Expression expression)
            => expression is ConstantExpression constant
                && constant.Value switch
                {
                    double doubleValue => !double.IsFinite(doubleValue),
                    float floatValue => !float.IsFinite(floatValue),
                    _ => false,
                };

        #endregion

        #region Typed constant folding

        /// <summary>
        /// Evaluates an ordinary addition, subtraction, multiplication or division of two constants exactly as the
        /// compiled node would, and returns the result as a constant of the node's type.
        /// </summary>
        /// <param name="nodeType">The operator: <see cref="ExpressionType.Add"/>, <see cref="ExpressionType.Subtract"/>,
        /// <see cref="ExpressionType.Multiply"/> or <see cref="ExpressionType.Divide"/>.</param>
        /// <param name="type">The node's type, which is also the type of both operands.</param>
        /// <param name="left">The left constant.</param>
        /// <param name="right">The right constant.</param>
        /// <returns>The folded constant, or <see langword="null"/> when an operand is <see langword="null"/> or the
        /// (operator, type) pair has no folder.</returns>
        /// <remarks>
        /// <para>
        /// The caller must have proven the node ordinary (<see cref="IsOrdinaryBinaryArithmetic(BinaryExpression)"/>):
        /// both operands then have exactly <paramref name="type"/>, and their boxed values are of that type.
        /// </para>
        /// <para>
        /// Replaces the <c>dynamic</c> arithmetic used before S5 P12, which applied the C# binary numeric promotions:
        /// <see cref="short"/> and <see cref="ushort"/> operands were widened to <see cref="int"/>, so <c>(short)0 / (short)3</c>
        /// folded to an <see cref="int"/> constant (a type mismatch for the parent node or the lambda) and
        /// <c>(short)32767 + (short)1</c> to <c>32768</c> instead of the wrapped <c>-32768</c>. The folders of
        /// <see cref="ConstantFolders"/> keep the operand type and the unchecked semantics of the ordinary node, raise
        /// the exceptions its evaluation raises (<see cref="DivideByZeroException"/> for an integral or decimal zero
        /// divisor, <see cref="OverflowException"/> for <c>int.MinValue / -1</c>, <c>long.MinValue / -1</c> and a
        /// decimal overflow), and follow IEEE-754 for <see cref="float"/> and <see cref="double"/> (<c>NaN</c>,
        /// infinities and signed zeros).
        /// </para>
        /// </remarks>
        private static ConstantExpression? FoldConstants(ExpressionType nodeType, Type type, object? left, object? right)
            => left is not null
                && right is not null
                && ConstantFolders.TryGetValue((nodeType, type), out Func<object, object, object>? folder)
                ? Expression.Constant(folder(left, right), type)
                : null;

        /// <summary>
        /// Typed evaluators of the ordinary binary operators, indexed by operator and operand type; used by
        /// <see cref="FoldConstants"/>.
        /// </summary>
        /// <remarks>
        /// Covers the four operators on the nine types for which <see cref="_ordinaryBinaryOperatorMethods"/> defines
        /// them (every <see cref="Types.Number"/> entry but <see cref="byte"/> and <see cref="sbyte"/>). Each entry is a
        /// generic-math operator call specialized by the JIT for its value type: no reflection, no dynamic binding and
        /// no implicit conversion. Read-only after the type initializer, hence safe for concurrent lookups.
        /// </remarks>
        private static readonly Dictionary<(ExpressionType NodeType, Type Type), Func<object, object, object>> ConstantFolders = BuildConstantFolders();

        /// <summary>Builds <see cref="ConstantFolders"/> once.</summary>
        /// <returns>The table of typed evaluators.</returns>
        private static Dictionary<(ExpressionType NodeType, Type Type), Func<object, object, object>> BuildConstantFolders()
        {
            var table = new Dictionary<(ExpressionType NodeType, Type Type), Func<object, object, object>>();
            AddConstantFolders<double>(table);
            AddConstantFolders<float>(table);
            AddConstantFolders<decimal>(table);
            AddConstantFolders<int>(table);
            AddConstantFolders<long>(table);
            AddConstantFolders<uint>(table);
            AddConstantFolders<ulong>(table);
            AddConstantFolders<short>(table);
            AddConstantFolders<ushort>(table);
            return table;
        }

        /// <summary>Registers the unchecked addition, subtraction, multiplication and division of <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">The operand type.</typeparam>
        /// <param name="table">The table being built.</param>
        /// <remarks>
        /// The generic-math operators of the primitive types compute in the operand type: <see cref="short"/> and
        /// <see cref="ushort"/> results are truncated exactly like the <c>conv</c> the expression compiler emits after
        /// the widened IL operation.
        /// </remarks>
        private static void AddConstantFolders<T>(Dictionary<(ExpressionType NodeType, Type Type), Func<object, object, object>> table)
            where T : INumber<T>
        {
            table[(ExpressionType.Add, typeof(T))] = static (left, right) => unchecked((T)left + (T)right);
            table[(ExpressionType.Subtract, typeof(T))] = static (left, right) => unchecked((T)left - (T)right);
            table[(ExpressionType.Multiply, typeof(T))] = static (left, right) => unchecked((T)left * (T)right);
            table[(ExpressionType.Divide, typeof(T))] = static (left, right) => unchecked((T)left / (T)right);
        }

        #endregion

        #region NaN propagation

        /// <summary>
        /// Replaces an ordinary <see cref="double"/>/<see cref="float"/> addition, subtraction, multiplication or
        /// division that has a <c>NaN</c> constant operand by that constant, when the other operand can be dropped
        /// without changing anything observable (S5 P11b).
        /// </summary>
        /// <param name="expression">A fully simplified expression.</param>
        /// <returns>The <c>NaN</c> operand, or <paramref name="expression"/> unchanged.</returns>
        /// <remarks>
        /// <para>
        /// IEEE-754 makes these four operations return <c>NaN</c> whenever an operand is <c>NaN</c>, whatever the
        /// other operand is, so the result is numerically inevitable. Dropping the other operand is a separate
        /// question, answered by <see cref="IsEliminableFloatingOperand"/>: only operands whose evaluation cannot
        /// throw, run user code or have any other effect qualify. The returned constant is the operand itself, so its
        /// type is the node's; its payload may differ from the one the hardware would produce, which the
        /// "Symbolic-equivalence contract" does not promise anyway. <see cref="ExpressionType.Power"/> is excluded:
        /// <c>Math.Pow(NaN, 0)</c> and <c>Math.Pow(1, NaN)</c> are <c>1</c>.
        /// </para>
        /// <para>
        /// Applied to the result of every node, after its rules, by <see cref="PrepareExpression"/> (each operand) and
        /// <see cref="TransformWithinBoundary"/> (the root), rather than as a transformation rule: a rule is a dispatch
        /// candidate of every arithmetic node, and the extra candidates cost up to 16% CPU on small multiplicative and
        /// factored trees (see the S5 P11 roadmap entry). Here the common path is two type tests. Folding after the
        /// rules is equivalent: a rewrite of the node keeps the <c>NaN</c> operand, and the folded result reaches the
        /// parent's own rules as a constant.
        /// </para>
        /// </remarks>
        private static Expression FoldNaNOperand(Expression expression)
        {
            if (expression is not BinaryExpression binary) return expression;
            ConstantExpression nan;
            Expression other;
            if (TryGetNaNOperand(binary.Left, out nan)) other = binary.Right;
            else if (TryGetNaNOperand(binary.Right, out nan)) other = binary.Left;
            else return expression;

            return binary.NodeType is ExpressionType.Add or ExpressionType.Subtract or ExpressionType.Multiply or ExpressionType.Divide
                && IsOrdinaryBinaryArithmetic(binary)
                && IsEliminableFloatingOperand(other)
                ? nan
                : expression;
        }

        /// <summary>
        /// Recognizes an operand that is, or immediately wraps, a <c>NaN</c> constant: the constant itself, its
        /// ordinary negation, or <c>c - NaN</c> with a constant <c>c</c>.
        /// </summary>
        /// <param name="operand">An operand of the node being folded.</param>
        /// <param name="nan">Receives the <c>NaN</c> constant.</param>
        /// <returns><see langword="true"/> when the operand evaluates to that <c>NaN</c> without any effect.</returns>
        /// <remarks>
        /// The two wrapped shapes are the negative-term forms <see cref="CanonicalizeAdditiveExpression"/> and
        /// <see cref="SubstractionWithZero(BinaryExpression, ConstantExpression, Expression)"/> build without passing
        /// them back through <see cref="PrepareExpression"/> (<c>x - NaN</c> becomes <c>(0 - NaN) + x</c>). The look-ahead
        /// is one level deep, so the common path stays a few type tests.
        /// </remarks>
        private static bool TryGetNaNOperand(Expression operand, out ConstantExpression nan)
        {
            // Shape and value tests first; the ordinary-operator table is only consulted once a NaN is found.
            nan = operand switch
            {
                ConstantExpression constant when IsNaNConstant(constant) => constant,
                UnaryExpression { Operand: ConstantExpression constant } unary when IsNaNConstant(constant) && IsOrdinaryUnaryNegate(unary) => constant,
                BinaryExpression { NodeType: ExpressionType.Subtract, Left: ConstantExpression, Right: ConstantExpression constant } binary
                    when IsNaNConstant(constant) && IsOrdinaryBinaryArithmetic(binary) => constant,
                _ => null,
            };
            return nan is not null;
        }

        /// <summary>Determines whether <paramref name="expression"/> is a <see cref="double"/> or <see cref="float"/> <c>NaN</c> constant.</summary>
        /// <param name="expression">The operand to test.</param>
        /// <returns><see langword="true"/> for a <c>NaN</c> constant, whatever its payload.</returns>
        private static bool IsNaNConstant(Expression expression)
            => expression is ConstantExpression constant
                && constant.Value switch
                {
                    double doubleValue => double.IsNaN(doubleValue),
                    float floatValue => float.IsNaN(floatValue),
                    _ => false,
                };

        /// <summary>
        /// Determines whether evaluating <paramref name="expression"/> is free of observable effects and cannot throw,
        /// so that a rewrite may drop it from the compiled expression (S5 P11b).
        /// </summary>
        /// <param name="expression">An operand of an ordinary <see cref="double"/>/<see cref="float"/> operation.</param>
        /// <returns><see langword="true"/> when the operand may be dropped.</returns>
        /// <remarks>
        /// <para>
        /// A whitelist, checked recursively: a <see cref="double"/>/<see cref="float"/> constant, a
        /// <see cref="double"/>/<see cref="float"/> parameter or variable read, and an ordinary (non-lifted,
        /// predefined, unchecked) addition, subtraction, multiplication, division or negation of such operands.
        /// IEEE-754 arithmetic on these types never throws: division by zero and overflow produce infinities.
        /// Everything else is rejected, including method calls, member accesses, custom or lifted operators,
        /// invocations, assignments, checked arithmetic, conversions (a conversion can throw or run a user-defined
        /// operator), integral or decimal sub-expressions (division by zero and overflow throw there), other node
        /// kinds, and extension nodes.
        /// </para>
        /// <para>
        /// This is about running the simplified expression, not about simplifying it: it is distinct from
        /// <see cref="MightInvokeUserCodeWhenSimplified"/>, which only predicts whether simplification reaches user
        /// code. It only inspects node types and static types, so it runs no user code itself and allocates nothing.
        /// </para>
        /// </remarks>
        private static bool IsEliminableFloatingOperand(Expression expression)
        {
            if (expression.Type != typeof(double) && expression.Type != typeof(float)) return false;
            return expression switch
            {
                ConstantExpression or ParameterExpression => true,
                BinaryExpression binary => binary.NodeType is ExpressionType.Add or ExpressionType.Subtract or ExpressionType.Multiply or ExpressionType.Divide
                    && IsOrdinaryBinaryArithmetic(binary)
                    && IsEliminableFloatingOperand(binary.Left)
                    && IsEliminableFloatingOperand(binary.Right),
                UnaryExpression unary => IsOrdinaryUnaryNegate(unary) && IsEliminableFloatingOperand(unary.Operand),
                _ => false,
            };
        }

        #endregion

        #region Operations with 0 and 1

        /// <summary>
        /// Simplifies <c>left + 0</c> to <c>left</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Add)]
        public Expression AdditionWithZero(BinaryExpression e, Expression left, [ConstantNumeric(0)] ConstantExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            if (NumericValueEquals(right.Value, 0)) return TransformCore(left);
            return null;
        }

        /// <summary>
        /// Simplifies <c>0 + right</c> to <c>right</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Add)]
        public Expression AdditionWithZero(BinaryExpression e, [ConstantNumeric(0)] ConstantExpression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            if (NumericValueEquals(left.Value, 0)) return right;
            return null;
        }

        /// <summary>
        /// Simplifies <c>left - 0</c> to <c>left</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Subtract)]
        public Expression SubstractionWithZero(BinaryExpression e, Expression left, [ConstantNumeric(0)] ConstantExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            if (NumericValueEquals(right.Value, 0)) return left;
            return null;
        }

        /// <summary>
        /// Simplifies <c>0 - right</c> to <c>-right</c>.
        /// </summary>
        /// <remarks>
        /// Not applied to a type without an ordinary unary negation (the unsigned integers), where
        /// <see cref="Expression.Negate(Expression)"/> would throw; <c>0 - right</c> is then already the simplest
        /// form and is also what <see cref="CanonicalizeAdditiveExpression"/> produces for such a type (S5 P10).
        /// </remarks>
        [ExpressionSignature(ExpressionType.Subtract)]
        public Expression SubstractionWithZero(BinaryExpression e, [ConstantNumeric(0)] ConstantExpression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || !_ordinaryNegateMethods.ContainsKey(right.Type)) return null;
            if (NumericValueEquals(left.Value, 0)) return TransformCore(Expression.Negate(right));
            return null;
        }

        /// <summary>
        /// Simplifies multiplication by 0, 1, or -1 (e.g., <c>x * 0</c> to 0, <c>x * 1</c> to <c>x</c>, etc.).
        /// </summary>
        /// <remarks>Declines when <paramref name="left"/> is a non-finite constant: see <see cref="IsNonFiniteFloatingConstant"/>.</remarks>
        [ExpressionSignature(ExpressionType.Multiply)]
        public Expression MultiplicationWithZeroOrOne(BinaryExpression e, Expression left, [ConstantNumeric(0, 1, -1)] ConstantExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || IsNonFiniteFloatingConstant(left)) return null;
            if (NumericValueEquals(right.Value, 0)) return right;  // x * 0 => 0
            if (NumericValueEquals(right.Value, 1)) return left;   // x * 1 => x
            if (NumericValueEquals(right.Value, -1)) return Expression.Negate(left); // x * -1 => -x
            return null;
        }

        /// <summary>
        /// Simplifies multiplication by 0, 1, or -1 (e.g., <c>0 * x</c>, <c>1 * x</c>, <c>-1 * x</c>).
        /// </summary>
        /// <remarks>Declines when <paramref name="right"/> is a non-finite constant: see <see cref="IsNonFiniteFloatingConstant"/>.</remarks>
        [ExpressionSignature(ExpressionType.Multiply)]
        public Expression MultiplicationWithZeroOrOne(BinaryExpression e, [ConstantNumeric(0, 1, -1)] ConstantExpression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || IsNonFiniteFloatingConstant(right)) return null;
            if (NumericValueEquals(left.Value, 0)) return left;
            if (NumericValueEquals(left.Value, 1)) return right;
            if (NumericValueEquals(left.Value, -1)) return Expression.Negate(right);
            return null;
        }

        /// <summary>
        /// Simplifies division by 1 or -1, and throws on an integral or decimal zero divisor.
        /// </summary>
        /// <remarks>
        /// Any other divisor, including <c>NaN</c>, an infinity or a value beyond the decimal range, leaves the node to
        /// the following rules (S5 P11; it threw <see cref="OverflowException"/> before). An integral or
        /// <see cref="decimal"/> zero divisor throws <see cref="DivideByZeroException"/>, as evaluating the node would.
        /// A <see cref="float"/> or <see cref="double"/> zero divisor is declined instead (S5 P12; it threw before):
        /// IEEE-754 division by <c>±0</c> is defined (an infinity or <c>NaN</c>), so <see cref="DivisionOfConstants"/>
        /// folds a constant dividend exactly and a non-constant dividend keeps the division node.
        /// </remarks>
        [ExpressionSignature(ExpressionType.Divide)]
        public Expression DivideWithZeroOrOne(BinaryExpression e, Expression left, ConstantExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            if (NumericValueEquals(right.Value, 0))
            {
                if (e.Type != typeof(double) && e.Type != typeof(float)) throw new DivideByZeroException();
            }
            else if (NumericValueEquals(right.Value, 1)) return left;
            else if (NumericValueEquals(right.Value, -1)) return Expression.Negate(left);
            return null;
        }

        /// <summary>
        /// Simplifies <c>0 / x</c> to <c>0</c> for a non-constant <c>x</c>.
        /// </summary>
        /// <remarks>
        /// Declines when <paramref name="right"/> is a constant, which <see cref="DivisionOfConstants"/> then folds
        /// exactly (S5 P12): <c>0 / -3</c> is <c>-0</c>, <c>0 / 0.0</c> is <c>NaN</c> and <c>0 / inf</c> is <c>0</c>,
        /// whereas this identity returned the dividend (S5 P11 already declined on a non-finite constant). On a
        /// non-constant divisor it is the symbolic identity of the "Symbolic-equivalence contract" (the sign of the
        /// zero and a <c>NaN</c> for <c>x = 0</c> or <c>x = NaN</c> are not preserved).
        /// </remarks>
        [ExpressionSignature(ExpressionType.Divide)]
        public Expression DivideWithZero(BinaryExpression e, [ConstantNumeric(0)] ConstantExpression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || right is ConstantExpression) return null;
            if (NumericValueEquals(left.Value, 0)) return left;
            return null;
        }

        /// <summary>
        /// Simplifies <c>0^x</c> or <c>1^x</c> to 0 or 1 respectively, for a non-constant <c>x</c>.
        /// </summary>
        /// <remarks>
        /// Declines when <paramref name="right"/> is a constant, which <see cref="PowerOfConstants"/> then folds with
        /// <see cref="double.Pow(double, double)"/> (S5 P13): <c>0^0</c> is <c>1</c>, <c>0^-1</c> is <c>+inf</c>,
        /// <c>(-0)^2</c> is <c>+0</c> and <c>(-0)^-3</c> is <c>-inf</c>, whereas this identity returned the base
        /// (S5 P11 already declined on a non-finite constant). On a non-constant exponent <c>0^x -&gt; 0</c> stays the
        /// domain-sensitive identity of the "Symbolic-equivalence contract" (valid for <c>x &gt; 0</c>; the sign of a
        /// <c>-0</c> base is not preserved).
        /// </remarks>
        [ExpressionSignature(ExpressionType.Power)]
        public Expression PowerOfZeroOrOne(BinaryExpression e, [ConstantNumeric(0, 1)] ConstantExpression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || right is ConstantExpression) return null;
            if (NumericValueEquals(left.Value, 0)) return left; // 0^x => 0
            if (NumericValueEquals(left.Value, 1)) return left; // 1^x => 1
            return null;
        }

        /// <summary>
        /// Simplifies <c>x^0</c> or <c>x^1</c> to <c>1</c> or <c>x</c> respectively.
        /// </summary>
        /// <remarks>
        /// Ordinary <see cref="ExpressionType.Power"/> is only defined on <see cref="double"/>. A non-finite exponent
        /// (<c>NaN</c> or an infinity) is left to <see cref="PowerOfConstants"/> or kept as is (S5 P11; it threw
        /// <see cref="OverflowException"/> before), and an exponent is compared exactly, so <see cref="double.Epsilon"/>
        /// is no longer taken for zero (<c>x^Epsilon</c> was rewritten to <c>1</c>).
        /// <para>
        /// Declines when <paramref name="left"/> is also a constant, which <see cref="PowerOfConstants"/> then folds
        /// exactly (S5 P13). The reciprocal rewrites <c>x^-1 -&gt; 1/x</c> and <c>x^-n -&gt; 1/x^n</c> are not
        /// bit-exact: <c>Pow(x, -n)</c> and <c>1 / Pow(x, n)</c> round independently (one unit in the last place on
        /// about a quarter of finite bases) and part ways where <c>x^n</c> overflows while <c>x^-n</c> is still a
        /// subnormal value. That is the rounding and overflow latitude of the "Symbolic-equivalence contract" for a
        /// symbolic base, but a constant base has an exact value, so it is folded instead (<c>2^-2</c> was left as the
        /// unevaluated <c>1 / 4</c>, and <c>0^-2</c> became <c>0</c> through <see cref="PowerOfZeroOrOne"/>).
        /// </para>
        /// </remarks>
        [ExpressionSignature(ExpressionType.Power)]
        public Expression PowerByZeroOrOne(BinaryExpression e, Expression left, ConstantExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || left is ConstantExpression || right.Value is not double exponent || !double.IsFinite(exponent)) return null;
            if (exponent == 0) return Expression.Constant(Convert.ChangeType(1, right.Type));
            if (exponent == 1) return TransformCore(left);
            if (exponent == -1) return TransformCore(Expression.Divide(Expression.Constant(Convert.ChangeType(1, left.Type)), left));
            if (exponent < 0) return Expression.Divide(Expression.Constant(Convert.ChangeType(1, left.Type)), TransformCore(Expression.Power(left, Expression.Constant(-exponent))));
            return null;
        }

        #endregion

        #region Addition

        /// <summary>
        /// Simplifies constant addition <c>(a + b)</c> where both are numeric constants.
        /// </summary>
        [ExpressionSignature(ExpressionType.Add)]
        protected Expression AdditionOfConstants(BinaryExpression e, [ConstantNumeric] ConstantExpression left, [ConstantNumeric] ConstantExpression right)
        {
            return IsOrdinaryBinaryArithmetic(e) ? FoldConstants(ExpressionType.Add, e.Type, left.Value, right.Value) : null;
        }

        /// <summary>
        /// Rewrites <c>left + (-right)</c> as <c>left - right</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Add)]
        protected Expression AdditionWithNegate(BinaryExpression e, Expression left, [ExpressionSignature(ExpressionType.Negate)] UnaryExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || !IsOrdinaryUnaryNegate(right)) return null;

            if (ExpressionComparer.Default.Equals(left, right.Operand))
            {
                return Expression.Constant(Convert.ChangeType(0, left.Type), left.Type);
            }

            return TransformCore(Expression.Subtract(left, right.Operand));
        }

        /// <summary>
        /// Simplifies <c>(-left) + right</c> to <c>0</c> when both operands are equal.
        /// Otherwise rewrites it as <c>right - left</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Add)]
        protected Expression AdditionWithNegate(BinaryExpression e, [ExpressionSignature(ExpressionType.Negate)] UnaryExpression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || !IsOrdinaryUnaryNegate(left)) return null;

            if (ExpressionComparer.Default.Equals(left.Operand, right))
            {
                return Expression.Constant(Convert.ChangeType(0, right.Type), right.Type);
            }

            return TransformCore(Expression.Subtract(right, left.Operand));
        }

        /// <summary>
        /// Rewrites <c>left - (-right)</c> as <c>left + right</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Subtract)]
        protected Expression SubstractionWithNegate(BinaryExpression e, Expression left, [ExpressionSignature(ExpressionType.Negate)] UnaryExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || !IsOrdinaryUnaryNegate(right)) return null;

            return TransformCore(Expression.Add(left, right.Operand));
        }

        /// <summary>
        /// Rewrites <c>(-left) - right</c> as <c>-(left + right)</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Subtract)]
        protected Expression SubstractionWithNegate(BinaryExpression e, [ExpressionSignature(ExpressionType.Negate)] UnaryExpression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || !IsOrdinaryUnaryNegate(left)) return null;

            return TransformCore(Expression.Negate(Expression.Add(left.Operand, right)));
        }

        /// <summary>
        /// Rewrites <c>-(left - right)</c> as <c>-right + left</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Negate)]
        protected Expression NegateWithSubstraction(UnaryExpression e, [ExpressionSignature(ExpressionType.Subtract)] BinaryExpression operand)
        {
            if (!IsOrdinaryUnaryNegate(e) || !IsOrdinaryBinaryArithmetic(operand)) return null;

            return TransformCore(Expression.Add(Expression.Negate(operand.Left), operand.Right));
        }

        /// <summary>
        /// Simplifies <c>left - (right1 + right2)</c> to <c>(left - right1) - right2</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Subtract)]
        protected Expression SubstractionWithAddition(BinaryExpression e, Expression left, [ExpressionSignature(ExpressionType.Add)] BinaryExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || !IsOrdinaryBinaryArithmetic(right)) return null;

            return Expression.Subtract(
                Expression.Subtract(left, right.Left),
                right.Right
            );
        }

        /// <summary>
        /// Simplifies <c>left - (right1 - right2)</c> to <c>(left + right2) - right1</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Subtract)]
        protected Expression SubstractionWithSubstraction(BinaryExpression e, Expression left, [ExpressionSignature(ExpressionType.Subtract)] BinaryExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || !IsOrdinaryBinaryArithmetic(right)) return null;

            return Expression.Subtract(
                Expression.Add(left, right.Right),
                right.Left
            );
        }

        /// <summary>
        /// Simplifies constant subtraction <c>(a - b)</c> where both are numeric constants.
        /// </summary>
        [ExpressionSignature(ExpressionType.Subtract)]
        protected Expression SubstractionOfConstants(BinaryExpression e, [ConstantNumeric] ConstantExpression left, [ConstantNumeric] ConstantExpression right)
        {
            return IsOrdinaryBinaryArithmetic(e) ? FoldConstants(ExpressionType.Subtract, e.Type, left.Value, right.Value) : null;
        }

        /// <summary>
        /// A per-invocation cache reused by the high-fan-out factoring rules
        /// (<see cref="AdditionOfEqualsElements"/>, <see cref="SubstractionOfEqualsElements"/>) to avoid
        /// calling <see cref="ExpressionSimplifier.Simplify(Expression)"/> more than once for the same
        /// candidate operand while it is probed against the (up to three) other candidates considered for
        /// the SAME factoring decision.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why this exists (S5).</b> Each of the two rules above compares up to four candidate
        /// sub-expressions (<c>leftleft</c>/<c>leftright</c>/<c>rightleft</c>/<c>rightright</c>) pairwise,
        /// up to five or six times per rule invocation, all via the PUBLIC <see cref="ExpressionComparer.Equals(Expression?, Expression?)"/>
        /// - which itself calls <see cref="ExpressionSimplifier.Simplify(Expression)"/> on BOTH operands
        /// every time it runs, per its own documented contract. Before this type existed, the same candidate
        /// operand was therefore re-simplified from scratch on every comparison it participated in within one
        /// rule call - a real, measured end-to-end construction-cost multiplier for left-associated additive
        /// chains (see <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>, stage S5, "P3"). This
        /// type makes that caching explicit and scoped to exactly one rule invocation, instead of either
        /// leaving the redundant work in place or reaching for a broader, longer-lived cache the roadmap's S5
        /// entry explicitly rules out ("introduce no global/static mutable memoization cache... no cache may
        /// survive the comparison/rule operation whose semantics justified it").
        /// </para>
        /// <para>
        /// <b>Why this preserves the public <see cref="ExpressionComparer"/> contract exactly.</b>
        /// <see cref="Equals"/> below performs, in order: <see cref="ExpressionComparer.TryFastPathEquals"/>
        /// (the same top-level <see cref="object.ReferenceEquals(object?, object?)"/>/null/root-lambda-metadata
        /// checks the public <see cref="ExpressionComparer.Equals(Expression?, Expression?)"/> itself performs
        /// before ever simplifying), then - only when that does not already decide the comparison -
        /// <see cref="ExpressionComparer.SimplifyForComparison"/> (the SAME exact built-in simplifier instance,
        /// establishing the same independent, self-contained top-level lexical-scope boundary every call to
        /// <see cref="ExpressionSimplifier.Simplify(Expression)"/> establishes) for each operand, cached by
        /// object reference so a given operand is simplified at most once per instance of this type, then
        /// <see cref="ExpressionComparer.StructuralEqualsAfterSimplification"/> (the same non-safe-constant,
        /// post-simplification structural comparison the public comparer itself performs). This is
        /// deliberately NOT <see cref="ExpressionComparer.StructuralEqualsRaw(Expression?, Expression?)"/>:
        /// that entry point never simplifies its operands and uses the safe-constant policy, which would
        /// silently change both which factoring decisions fire (see the roadmap's second-pass-dependency
        /// characterization, <c>ExpressionSimplifierComparerBatchingTests</c>) and which constants' own
        /// <see cref="object.Equals(object?)"/>/<see cref="object.GetHashCode"/> may run.
        /// </para>
        /// <para>
        /// <b>Why caching by object reference is safe here.</b> The operands
        /// passed to <see cref="Equals"/> are always one of exactly four candidate <see cref="Expression"/>
        /// instances captured once at the top of a single <c>AdditionOfEqualsElements</c>/
        /// <c>SubstractionOfEqualsElements</c> invocation (<c>leftleft</c>/<c>leftright</c>/<c>rightleft</c>/
        /// <c>rightright</c>); the caller's subsequent <c>ObjectUtils.Swap</c> calls only reassign which LOCAL
        /// VARIABLE refers to which of those four already-captured objects, never mutate an
        /// <see cref="Expression"/> instance itself (expression trees are immutable) or introduce a new
        /// object needing simplification. <see cref="ExpressionSimplifier.Simplify(Expression)"/> is a pure
        /// function of its input's RESULT SHAPE for any single top-level call (see the roadmap's "Purity
        /// assumption" and S4's "Independent top-level calls" remarks: every call establishes and restores
        /// its own ambient lexical-scope boundary, so the shape it returns never depends on when, or how many
        /// times, it was previously called) - but it is not necessarily side-effect-free DURING that call:
        /// see "Side-effect safety" below for why caching is therefore restricted to operands proven not to
        /// trigger user code while being simplified, not merely to operands whose simplified shape is stable.
        /// </para>
        /// <para>
        /// <b>Side-effect safety (S5 P3 review round 6).</b> Simplifying a candidate operand can itself
        /// dispatch a NESTED rule invocation - including a nested <c>AdditionOfEqualsElements</c>/
        /// <c>SubstractionOfEqualsElements</c> call, with its own <see cref="ExpressionComparer.Equals(Expression?, Expression?)"/>
        /// probes - reachable purely because that operand happens to contain its own <c>Add</c>/<c>Subtract</c>/
        /// <c>Multiply</c> structure. If such a nested probe reaches a non-safe (opaque, non-numeric,
        /// non-known-safe) <see cref="ConstantExpression"/>, it invokes that constant's own possibly
        /// user-defined <see cref="object.Equals(object?)"/>/<see cref="object.GetHashCode"/> - exactly the
        /// PUBLIC comparer's documented, deliberately-preserved non-safe policy (see
        /// <see cref="ExpressionComparer.StructuralEqualsAfterSimplification"/>'s remarks). Before this
        /// review round, caching a candidate's simplified form unconditionally meant such user code ran ONCE
        /// per DISTINCT candidate reference instead of once per COMPARISON that candidate participated in -
        /// an observable reduction in invocation count for arbitrary user code, confirmed experimentally
        /// (see the roadmap's S5 P3 round-6 entry: a stateful/counting constant nested two levels inside one
        /// candidate's own subtree was invoked twice by the pre-P3 baseline but only once by the
        /// then-shipped, unconditionally-caching implementation). <see cref="MightInvokeUserCodeWhenSimplified"/>
        /// conservatively predicts this risk from a candidate's STATIC SHAPE alone - it never itself
        /// simplifies or compares anything, so it never runs the very user code it is trying to avoid running
        /// an extra time - and <see cref="GetOrSimplify"/> only caches (and therefore only
        /// deduplicates re-simplification of) a candidate <see cref="IsCacheable"/> has cleared. A candidate
        /// that is not cacheable falls back, on EVERY comparison it participates in, to calling
        /// <see cref="ExpressionComparer.Default"/>'s public <see cref="ExpressionComparer.Equals(Expression?, Expression?)"/>
        /// directly - the exact call, and therefore the exact user-code invocation count, this type replaces
        /// for a cacheable candidate - so this type never reduces how many times user code observably runs
        /// relative to the pre-existing per-comparison behavior, for any candidate.
        /// </para>
        /// <para>
        /// <b>Ordering (S5 P3 review round 7).</b> <see cref="Equals"/> classifies BOTH operands, via
        /// <see cref="IsCacheable"/>, before simplifying EITHER one. Round 6 shipped a version that simplified
        /// each operand as it was classified (interleaved), which is observably different from the public
        /// <see cref="ExpressionComparer.Equals(Expression?, Expression?)"/> contract whenever exactly one
        /// operand is not cacheable: given non-cacheable <c>x</c> and cacheable <c>y</c>, the round-6 code
        /// simplified <c>y</c> (a side-effecting operation - see above) while classifying it, BEFORE the
        /// not-cacheable-so-fall-back decision was even reached, so if simplifying <c>y</c> itself throws,
        /// that exception surfaces before <c>x</c> is ever touched - whereas the public contract, and this
        /// type's own fallback call, always simplify <c>x</c> first and never reach <c>y</c> if that throws.
        /// Classifying both operands first (<see cref="MightInvokeUserCodeWhenSimplified"/>, called from
        /// <see cref="IsCacheable"/>) is safe to do unconditionally because classification never simplifies
        /// anything; simplification (<see cref="GetOrSimplify"/>) then only ever runs, in `x`-then-`y` order,
        /// once both operands are already known cacheable - exactly reproducing
        /// <see cref="ExpressionComparer.Equals(Expression?, Expression?)"/>'s own <c>Simplify(x)</c>-then-
        /// <c>Simplify(y)</c> sequence for that case, and never simplifying anything at all (deferring fully to
        /// the fallback) for every other case.
        /// </para>
        /// <para>
        /// <b>Lifetime.</b> A fresh instance is created at the top of each
        /// <c>AdditionOfEqualsElements</c>/<c>SubstractionOfEqualsElements</c> call and discarded when that
        /// call returns; nothing here is stored on <see langword="this"/> <see cref="ExpressionSimplifier"/>
        /// instance, a <see langword="static"/> field, or a thread-local slot, so there is no cross-call,
        /// cross-thread, or cross-rule sharing of any kind.
        /// </para>
        /// </remarks>
        private sealed class FactorEqualityProbe
        {
            /// <summary>
            /// At most four distinct operands ever participate in one factoring decision
            /// (<c>leftleft</c>/<c>leftright</c>/<c>rightleft</c>/<c>rightright</c>), so a small
            /// fixed-capacity list - linearly scanned by reference equality - avoids a
            /// <see cref="Dictionary{TKey, TValue}"/>'s hashing/bucket overhead for so few entries.
            /// <c>CanCache</c> records, once computed, whether <c>Simplified</c> holds a reusable cached form
            /// (<see langword="true"/>) or is meaningless/<see langword="null"/> because
            /// <see cref="MightInvokeUserCodeWhenSimplified"/> ruled the operand out (<see langword="false"/>).
            /// </summary>
            private readonly List<(Expression Original, bool CanCache, Expression? Simplified)> _cache = new(4);

            /// <summary>
            /// Compares <paramref name="x"/> and <paramref name="y"/> exactly like the public
            /// <see cref="ExpressionComparer.Equals(Expression?, Expression?)"/>, reusing a previously
            /// simplified form for either operand when this instance has already computed one AND that
            /// operand was proven safe to cache (see "Side-effect safety" above) - otherwise falling back,
            /// for this one comparison, to calling <see cref="ExpressionComparer.Default"/> directly, exactly
            /// as every comparison did before this type existed.
            /// </summary>
            /// <param name="x">The first candidate operand.</param>
            /// <param name="y">The second candidate operand.</param>
            /// <returns><see langword="true"/> if the two candidates are equal under the public <see cref="ExpressionComparer"/> contract.</returns>
            public bool Equals(Expression x, Expression y)
            {
                if (!ExpressionComparer.IsDiagnosticCaptureActive)
                {
                    return EqualsCore(x, y);
                }

                ExpressionComparer.ComparisonOrigin previousOrigin = ExpressionComparer.CurrentComparisonOrigin;
                ExpressionComparer.CurrentComparisonOrigin = ExpressionComparer.ComparisonOrigin.FactorEqualityProbe;
                try
                {
                    return EqualsCore(x, y);
                }
                finally
                {
                    ExpressionComparer.CurrentComparisonOrigin = previousOrigin;
                }
            }

            /// <summary>Performs the factor comparison after any diagnostic context has been established.</summary>
            /// <param name="x">The first candidate operand.</param>
            /// <param name="y">The second candidate operand.</param>
            /// <returns><see langword="true"/> when the operands compare equal.</returns>
            private bool EqualsCore(Expression x, Expression y)
            {
                if (ExpressionComparer.TryFastPathEquals(x, y, out bool fastResult)) return fastResult;

                // Classify BOTH operands before simplifying EITHER one (S5 P3 review round 7): classification
                // alone (MightInvokeUserCodeWhenSimplified) never simplifies anything and is therefore
                // side-effect-free, but ExpressionComparer.SimplifyForComparison is not. Simplifying a
                // cacheable operand here, before knowing whether the OTHER operand will force a fallback,
                // would run that operand's simplification (and any user code it can reach) before the
                // fallback's own Simplify(x)-then-Simplify(y) sequence does - an observable reordering (e.g. of
                // which operand's simplification throws first) relative to ExpressionComparer.Equals, which
                // this type must reproduce exactly, not merely approximate.
                bool canCacheX = IsCacheable(x);
                bool canCacheY = IsCacheable(y);

                if (!canCacheX || !canCacheY)
                {
                    return ExpressionComparer.Default.Equals(x, y);
                }

                Expression simplifiedX = GetOrSimplify(x);
                Expression simplifiedY = GetOrSimplify(y);

                return ExpressionComparer.StructuralEqualsAfterSimplification(simplifiedX, simplifiedY);
            }

            /// <summary>
            /// Determines, and caches, whether <paramref name="original"/> is safe to simplify-and-cache (see
            /// <see cref="MightInvokeUserCodeWhenSimplified"/>) - WITHOUT simplifying it. Kept separate from
            /// <see cref="GetOrSimplify"/> so <see cref="Equals"/> can classify both operands of one comparison
            /// before simplifying either (see <see cref="Equals"/>'s remarks on ordering).
            /// </summary>
            /// <param name="original">The candidate operand to classify.</param>
            /// <returns><see langword="true"/> if <paramref name="original"/> is safe to simplify and cache.</returns>
            private bool IsCacheable(Expression original)
            {
                for (int i = 0; i < _cache.Count; i++)
                {
                    if (ReferenceEquals(_cache[i].Original, original))
                    {
                        return _cache[i].CanCache;
                    }
                }

                bool canCache;
                if (ExpressionComparer.IsDiagnosticCaptureActive)
                {
                    ExpressionMetrics metrics = GetExpressionMetrics(original);
                    canCache = !metrics.MightInvokeUserCode;
                }
                else
                {
                    canCache = !MightInvokeUserCodeWhenSimplified(original);
                }

                _cache.Add((original, canCache, null));
                return canCache;
            }

            /// <summary>
            /// Returns <paramref name="original"/>'s simplified form, computing and caching it on first use.
            /// The caller MUST have already established, via <see cref="IsCacheable"/>, that
            /// <paramref name="original"/> is safe to simplify; this method never itself decides that.
            /// </summary>
            /// <param name="original">A candidate operand <see cref="IsCacheable"/> has already proven safe to simplify.</param>
            /// <returns><paramref name="original"/>'s simplified form.</returns>
            private Expression GetOrSimplify(Expression original)
            {
                for (int i = 0; i < _cache.Count; i++)
                {
                    if (ReferenceEquals(_cache[i].Original, original))
                    {
                        Expression? simplified = _cache[i].Simplified;
                        if (simplified is not null) return simplified;

                        simplified = ExpressionComparer.SimplifyForComparison(original);
                        _cache[i] = (original, true, simplified);
                        return simplified;
                    }
                }

                throw new InvalidOperationException(
                    $"{nameof(GetOrSimplify)} was called for an operand that {nameof(IsCacheable)} had not already classified as cacheable.");
            }

            /// <summary>
            /// Conservatively determines whether simplifying <paramref name="expression"/> could invoke
            /// arbitrary user-defined code (a non-safe constant's own <see cref="object.Equals(object?)"/>/
            /// <see cref="object.GetHashCode"/>, reached through some nested rule's own equality probe) -
            /// see this type's "Side-effect safety" remarks for why this must be checked before caching.
            /// </summary>
            /// <remarks>
            /// This walks <paramref name="expression"/>'s ENTIRE subtree by construction - not merely the
            /// seven node kinds <see cref="ExpressionComparer"/> understands structurally - since a nested
            /// rule invocation reachable during simplification is not limited to those seven kinds either
            /// (for example, a <see cref="ConditionalExpression"/>'s branches are still simplified even
            /// though <see cref="ExpressionComparer"/> treats the whole node as structurally opaque). Two
            /// deliberate conservatism choices keep this safe rather than merely optimistic:
            /// <list type="bullet">
            /// <item>a <see cref="ConstantExpression"/> is "risky" unless its value is either a native
            /// numeric type in <see cref="Types.Number"/> (compared via the exact rational/NaN/infinity
            /// model, which never calls user code - see <c>ExpressionComparer.ExactNumericValue</c>) or a
            /// known-safe type (see <see cref="ExpressionComparer.IsKnownSafeConstantValue(object)"/>);</item>
            /// <item>any node kind this method does not explicitly recognize is treated as "risky" - this is
            /// NOT <see cref="ExpressionVisitor"/>-based traversal, deliberately: <see cref="ExpressionVisitor"/>'s
            /// default <c>VisitExtension</c>/<c>Expression.VisitChildren</c> throws
            /// <see cref="ArgumentException"/> on a non-reducible <see cref="ExpressionType.Extension"/> node
            /// (<see cref="Expression.CanReduce"/> <see langword="false"/>), which this safety CHECK must
            /// never do even for adversarial/test-double node kinds (see
            /// <c>ExpressionSimplifierAdditiveSortScaleTests</c>'s own such adversarial coverage) - a
            /// conservative "true" for an unrecognized node kind achieves the same safe outcome without ever
            /// touching that node's internals.</item>
            /// </list>
            /// </remarks>
            /// <param name="expression">The candidate operand's subtree to scan, or <see langword="null"/> for an absent optional sub-expression.</param>
            /// <returns><see langword="true"/> if simplifying <paramref name="expression"/> might invoke user-defined code; <see langword="false"/> only when this is proven impossible.</returns>
            private static bool MightInvokeUserCodeWhenSimplified(Expression? expression) => expression switch
            {
                null => false,
                ParameterExpression => false,
                ConstantExpression constant => constant.Value is not null
                    && !Types.Number.Contains(constant.Type)
                    && !ExpressionComparer.IsKnownSafeConstantValue(constant.Value),
                LambdaExpression lambda => MightInvokeUserCodeWhenSimplified(lambda.Body),
                UnaryExpression unary => MightInvokeUserCodeWhenSimplified(unary.Operand),
                BinaryExpression binary => MightInvokeUserCodeWhenSimplified(binary.Left)
                    || MightInvokeUserCodeWhenSimplified(binary.Right)
                    || MightInvokeUserCodeWhenSimplified(binary.Conversion),
                MethodCallExpression call => MightInvokeUserCodeWhenSimplified(call.Object)
                    || AnyArgumentMightInvokeUserCodeWhenSimplified(call.Arguments),
                MemberExpression member => MightInvokeUserCodeWhenSimplified(member.Expression),
                _ => true,
            };

            /// <summary>Determines whether any method-call argument may invoke user code when simplified.</summary>
            /// <param name="arguments">The arguments to inspect without LINQ allocation.</param>
            /// <returns><see langword="true"/> when any argument is conservatively unsafe.</returns>
            private static bool AnyArgumentMightInvokeUserCodeWhenSimplified(IReadOnlyList<Expression> arguments)
            {
                for (int i = 0; i < arguments.Count; i++)
                {
                    if (MightInvokeUserCodeWhenSimplified(arguments[i])) return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Attempts to factor out common elements in <c>left + right</c> if possible.
        /// E.g., rewriting <c>a*x + b*x</c> as <c>(a+b)*x</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Add)]
        protected Expression AdditionOfEqualsElements(BinaryExpression e, Expression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            if (!Types.Number.Contains(left.Type) || !Types.Number.Contains(right.Type))
            {
                return null;
            }

            bool leftAugmented = false;
            Expression leftleft;
            Expression leftright;
            if (left is BinaryExpression leftBinary && leftBinary.NodeType == ExpressionType.Multiply && IsOrdinaryBinaryArithmetic(leftBinary))
            {
                leftleft = leftBinary.Left;
                leftright = leftBinary.Right;
            }
            else
            {
                leftAugmented = true;
                leftleft = Expression.Constant(Convert.ChangeType(1, left.Type));
                leftright = left;
            }

            bool rightAugmented = false;
            Expression rightleft;
            Expression rightright;
            if (right is BinaryExpression rightBinary && rightBinary.NodeType == ExpressionType.Multiply && IsOrdinaryBinaryArithmetic(rightBinary))
            {
                rightleft = rightBinary.Left;
                rightright = rightBinary.Right;
            }
            else
            {
                rightAugmented = true;
                rightleft = Expression.Constant(Convert.ChangeType(1, right.Type));
                rightright = right;
            }

            // Check if the single factor is truly 1 (exactly: a NaN, infinite or out-of-decimal-range constant is
            // simply not 1, S5 P11 - the decimal comparison used before threw OverflowException on it).
            if (leftAugmented && leftright is ConstantExpression leftRightConst && NumericValueEquals(leftRightConst.Value, 1))
                return null;
            if (rightAugmented && rightright is ConstantExpression rightRightConst && NumericValueEquals(rightRightConst.Value, 1))
                return null;

            // Attempt to unify or swap factors for factoring out. A single FactorEqualityProbe is reused
            // for every comparison below (S5): each of the up to four candidate operands is simplified via
            // the public ExpressionComparer contract at most once per call, rather than once per comparison
            // it participates in - see FactorEqualityProbe's remarks for why this is semantics-preserving.
            var equalityProbe = new FactorEqualityProbe();
            if (!leftAugmented
                && !rightAugmented
                && equalityProbe.Equals(leftleft, rightleft)
                && !equalityProbe.Equals(leftright, rightright))
            {
                ObjectUtils.Swap(ref leftleft, ref leftright);
                ObjectUtils.Swap(ref rightleft, ref rightright);
            }
            else if (equalityProbe.Equals(leftleft, rightright))
            {
                ObjectUtils.Swap(ref leftleft, ref leftright);
            }
            else if (equalityProbe.Equals(leftright, rightleft))
            {
                ObjectUtils.Swap(ref rightleft, ref rightright);
            }
            else if (equalityProbe.Equals(leftright, rightright))
            {
                // do nothing
            }
            else
            {
                return null;
            }

            // Factor out without recursively re-entering this rule on the same shape.
            return Expression.Multiply(
                Expression.Add(leftleft, rightleft),
                leftright
            );
        }

        /// <summary>
        /// Attempts to factor out common elements in <c>left - right</c> if possible.
        /// E.g., rewriting <c>a*x - b*x</c> as <c>(a-b)*x</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Subtract)]
        protected Expression SubstractionOfEqualsElements(BinaryExpression e, Expression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            if (!Types.Number.Contains(left.Type) || !Types.Number.Contains(right.Type))
            {
                return null;
            }

            bool leftAugmented = false;
            Expression leftleft;
            Expression leftright;
            if (left is BinaryExpression leftBinary && leftBinary.NodeType == ExpressionType.Multiply && IsOrdinaryBinaryArithmetic(leftBinary))
            {
                leftleft = leftBinary.Left;
                leftright = leftBinary.Right;
            }
            else
            {
                leftAugmented = true;
                leftleft = Expression.Constant(Convert.ChangeType(1, left.Type));
                leftright = left;
            }

            bool rightAugmented = false;
            Expression rightleft;
            Expression rightright;
            if (right is BinaryExpression rightBinary && rightBinary.NodeType == ExpressionType.Multiply && IsOrdinaryBinaryArithmetic(rightBinary))
            {
                rightleft = rightBinary.Left;
                rightright = rightBinary.Right;
            }
            else
            {
                rightAugmented = true;
                rightleft = Expression.Constant(Convert.ChangeType(1, right.Type));
                rightright = right;
            }

            // Attempt to unify or swap factors. A single FactorEqualityProbe is reused for every comparison
            // below, including the final cancellation check (S5) - see FactorEqualityProbe's remarks for
            // why this is semantics-preserving.
            var equalityProbe = new FactorEqualityProbe();
            if ((!leftAugmented && !rightAugmented)
                && equalityProbe.Equals(leftleft, rightleft)
                && !equalityProbe.Equals(leftright, rightright))
            {
                ObjectUtils.Swap(ref leftleft, ref leftright);
                ObjectUtils.Swap(ref rightleft, ref rightright);
            }
            else if (equalityProbe.Equals(leftleft, rightright))
            {
                ObjectUtils.Swap(ref leftleft, ref leftright);
            }
            else if (equalityProbe.Equals(leftright, rightleft))
            {
                ObjectUtils.Swap(ref rightleft, ref rightright);
            }
            else if (equalityProbe.Equals(leftright, rightright))
            {
                // do nothing
            }
            else
            {
                return null;
            }

            if (equalityProbe.Equals(leftleft, rightleft))
            {
                return Expression.Constant(Convert.ChangeType(0, e.Type), e.Type);
            }

            // Factor out without recursively re-entering this rule on the same shape.
            return Expression.Multiply(
                Expression.Subtract(leftleft, rightleft),
                leftright
            );
        }

        #endregion

        #region Multiplication

        /// <summary>
        /// Simplifies constant multiplication <c>(a * b)</c> where both are numeric constants.
        /// </summary>
        [ExpressionSignature(ExpressionType.Multiply)]
        protected Expression MultiplicationOfConstants(
            BinaryExpression e,
            [ConstantNumeric] ConstantExpression left,
            [ConstantNumeric] ConstantExpression right)
        {
            return IsOrdinaryBinaryArithmetic(e) ? FoldConstants(ExpressionType.Multiply, e.Type, left.Value, right.Value) : null;
        }

        /// <summary>
        /// Commutes <c>left * constant</c> to <c>constant * left</c> for consistent transformations.
        /// </summary>
        [ExpressionSignature(ExpressionType.Multiply)]
        protected Expression Multiplication(BinaryExpression e, Expression left, [ConstantNumeric] ConstantExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            return Expression.Multiply(right, left);
        }

        /// <summary>
        /// Combines a constant with another multiply expression, e.g., <c>c * (c2 * rest)</c> =&gt; <c>(c*c2) * rest</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Multiply)]
        protected Expression Multiplication(
            BinaryExpression e,
            [ConstantNumeric] ConstantExpression left,
            [ExpressionSignature(ExpressionType.Multiply)] BinaryExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || !IsOrdinaryBinaryArithmetic(right)) return null;

            if (right.Left is ConstantExpression rightLeftConst
                && FoldConstants(ExpressionType.Multiply, e.Type, left.Value, rightLeftConst.Value) is ConstantExpression product)
            {
                return Expression.Multiply(product, right.Right);
            }
            return null;
        }

        /// <summary>
        /// Combines two multiply expressions if both have a constant factor,
        /// e.g. <c>(c1 * x) * (c2 * y)</c> =&gt; <c>(c1*c2) * (x*y)</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Multiply)]
        protected Expression Multiplication(
            BinaryExpression e,
            [ExpressionSignature(ExpressionType.Multiply)] BinaryExpression left,
            [ExpressionSignature(ExpressionType.Multiply)] BinaryExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || !IsOrdinaryBinaryArithmetic(left) || !IsOrdinaryBinaryArithmetic(right)) return null;

            if (left.Left is ConstantExpression leftLeft && right.Left is ConstantExpression rightLeft
                && FoldConstants(ExpressionType.Multiply, e.Type, leftLeft.Value, rightLeft.Value) is ConstantExpression product)
            {
                return Expression.Multiply(product, TransformCore(Expression.Multiply(left.Right, right.Right)));
            }
            return null;
        }

        /// <summary>
        /// Simplifies constant division <c>(a / b)</c> when both are numeric constants.
        /// </summary>
        /// <remarks>
        /// Folded by <see cref="FoldConstants"/> in the node's own type: an integral or decimal zero divisor and
        /// <c>int.MinValue / -1</c> throw as the compiled node would, and a floating-point division follows IEEE-754.
        /// </remarks>
        [ExpressionSignature(ExpressionType.Divide)]
        protected Expression DivisionOfConstants(
            BinaryExpression e,
            [ConstantNumeric] ConstantExpression left,
            [ConstantNumeric] ConstantExpression right)
        {
            return IsOrdinaryBinaryArithmetic(e) ? FoldConstants(ExpressionType.Divide, e.Type, left.Value, right.Value) : null;
        }

        /// <summary>
        /// Rewrites <c>left * (-right)</c> as <c>-(left * right)</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Multiply)]
        protected Expression MultiplicationWithNegate(BinaryExpression e, Expression left, [ExpressionSignature(ExpressionType.Negate)] UnaryExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || !IsOrdinaryUnaryNegate(right)) return null;

            return Expression.Negate(
                TransformCore(Expression.Multiply(left, right.Operand))
            );
        }

        /// <summary>
        /// Rewrites <c>(-left) * right</c> as <c>-(left * right)</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Multiply)]
        protected Expression MultiplicationWithNegate(BinaryExpression e, [ExpressionSignature(ExpressionType.Negate)] UnaryExpression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || !IsOrdinaryUnaryNegate(left)) return null;

            return Expression.Negate(
                TransformCore(Expression.Multiply(left.Operand, right))
            );
        }

        /// <summary>
        /// Rewrites <c>left / (-right)</c> as <c>-(left / right)</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Divide)]
        protected Expression DivisionWithNegate(BinaryExpression e, Expression left, [ExpressionSignature(ExpressionType.Negate)] UnaryExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || !IsOrdinaryUnaryNegate(right)) return null;

            return Expression.Negate(
                TransformCore(Expression.Divide(left, right.Operand))
            );
        }

        /// <summary>
        /// Rewrites <c>(-left) / right</c> as <c>-(left / right)</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Divide)]
        protected Expression DivisionWithNegate(BinaryExpression e, [ExpressionSignature(ExpressionType.Negate)] UnaryExpression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || !IsOrdinaryUnaryNegate(left)) return null;

            return Expression.Negate(
                TransformCore(Expression.Divide(left.Operand, right))
            );
        }

        /// <summary>
        /// Distributes multiplication if one side is <c>(constant * part)</c>, e.g.,
        /// <c>(c * x) * y</c> =&gt; <c>c * (x * y)</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Multiply)]
        protected Expression MultiplicationOfEqualsElements(BinaryExpression e, [ExpressionSignature(ExpressionType.Multiply)] BinaryExpression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || !IsOrdinaryBinaryArithmetic(left)) return null;

            Expression constant;
            Expression leftpart;

            if (left.Left.NodeType == ExpressionType.Constant)
            {
                constant = left.Left;
                leftpart = left.Right;
            }
            else if (left.Right.NodeType == ExpressionType.Constant)
            {
                constant = left.Right;
                leftpart = left.Left;
            }
            else
            {
                return null;
            }

            return TransformCore(
                Expression.Multiply(
                    constant,
                    Expression.Multiply(leftpart, right)
                )
            );
        }

        /// <summary>
        /// Distributes multiplication if one side is <c>(constant * part)</c>, e.g.,
        /// <c>x * (c * y)</c> =&gt; <c>c * (x * y)</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Multiply)]
        protected Expression MultiplicationOfEqualsElements(BinaryExpression e, Expression left, [ExpressionSignature(ExpressionType.Multiply)] BinaryExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e) || !IsOrdinaryBinaryArithmetic(right)) return null;

            Expression constant;
            Expression rightpart;

            if (right.Left.NodeType == ExpressionType.Constant)
            {
                constant = right.Left;
                rightpart = right.Right;
            }
            else if (right.Right.NodeType == ExpressionType.Constant)
            {
                constant = right.Right;
                rightpart = right.Left;
            }
            else
            {
                return null;
            }

            return TransformCore(
                Expression.Multiply(
                    constant,
                    Expression.Multiply(left, rightpart)
                )
            );
        }

        /// <summary>
        /// Attempts to combine exponent factors if they share a common base, e.g.
        /// <c>(x^a) * (x^b)</c> =&gt; <c>x^(a+b)</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Multiply)]
        protected Expression MultiplicationOfEqualsElements(BinaryExpression e, Expression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;

            Expression leftleft;
            Expression leftright;
            if (left is BinaryExpression leftPower && leftPower.NodeType == ExpressionType.Power && IsOrdinaryBinaryArithmetic(leftPower))
            {
                leftleft = leftPower.Left;
                leftright = leftPower.Right;
            }
            else
            {
                leftleft = left;
                leftright = Expression.Constant(Convert.ChangeType(1, left.Type));
            }

            Expression rightleft;
            Expression rightright;
            if (right is BinaryExpression rightPower && rightPower.NodeType == ExpressionType.Power && IsOrdinaryBinaryArithmetic(rightPower))
            {
                rightleft = rightPower.Left;
                rightright = rightPower.Right;
            }
            else
            {
                rightleft = right;
                rightright = Expression.Constant(Convert.ChangeType(1, right.Type));
            }

            bool equalBases;
            if (ExpressionComparer.IsDiagnosticCaptureActive)
            {
                ExpressionComparer.ComparisonOrigin previousOrigin = ExpressionComparer.CurrentComparisonOrigin;
                ExpressionComparer.CurrentComparisonOrigin = ExpressionComparer.ComparisonOrigin.Multiplication;
                try
                {
                    equalBases = ExpressionComparer.Default.Equals(leftleft, rightleft);
                }
                finally
                {
                    ExpressionComparer.CurrentComparisonOrigin = previousOrigin;
                }
            }
            else
            {
                equalBases = ExpressionComparer.Default.Equals(leftleft, rightleft);
            }

            if (equalBases)
            {
                // Expression.Power only resolves to Math.Pow(double,double); for other
                // numeric types it throws. Skip the x^(a+b) rewrite in that case so
                // the expression stays as repeated multiplications, which all handlers support.
                if (leftleft.Type != typeof(double)) return null;

                return TransformCore(
                    Expression.Power(
                        leftleft,
                        TransformCore(Expression.Add(leftright, rightright))
                    )
                );
            }
            return null;
        }

        /// <summary>
        /// Simplifies nested divisions, e.g. <c>(x / y) / (z / w)</c> =&gt; <c>(x*w) / (y*z)</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Divide)]
        protected Expression DivisionOfDivision(BinaryExpression e,
            [ExpressionSignature(ExpressionType.Divide)] BinaryExpression left,
            [ExpressionSignature(ExpressionType.Divide)] BinaryExpression right)
        {
            if (!IsOrdinaryFieldDivision(e) || !IsOrdinaryFieldDivision(left) || !IsOrdinaryFieldDivision(right)) return null;

            return Expression.Divide(
                TransformCore(Expression.Multiply(left.Left, right.Right)),
                TransformCore(Expression.Multiply(left.Right, right.Left))
            );
        }

        /// <summary>
        /// Simplifies nested divisions, e.g. <c>x / (y / z)</c> =&gt; <c>(x*z) / y</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Divide)]
        protected Expression DivisionOfDivision(BinaryExpression e, Expression left, [ExpressionSignature(ExpressionType.Divide)] BinaryExpression right)
        {
            if (!IsOrdinaryFieldDivision(e) || !IsOrdinaryFieldDivision(right)) return null;

            return Expression.Divide(
                TransformCore(Expression.Multiply(left, right.Right)),
                right.Left
            );
        }

        /// <summary>
        /// Simplifies nested divisions, e.g. <c>(x / y) / z</c> =&gt; <c>x / (y*z)</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Divide)]
        protected Expression DivisionOfDivision(BinaryExpression e,
            [ExpressionSignature(ExpressionType.Divide)] BinaryExpression left,
            Expression right)
        {
            if (!IsOrdinaryFieldDivision(e) || !IsOrdinaryFieldDivision(left)) return null;

            return Expression.Divide(
                left.Left,
                TransformCore(Expression.Multiply(left.Right, right))
            );
        }

        #endregion

        #region Power

        /// <summary>
        /// Simplifies power expressions when both base and exponent are numeric constants,
        /// e.g. <c>(2)^(3)</c> =&gt; <c>8</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Power)]
        protected Expression PowerOfConstants(BinaryExpression e, [ConstantNumeric] ConstantExpression left, [ConstantNumeric] ConstantExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            var result = double.Pow((double)left.Value, (double)right.Value);
            return Expression.Constant(result);
        }

        #endregion

        #region Lambda/Invoke

        /// <summary>
        /// Inlines or transforms an <see cref="InvocationExpression"/> that calls a lambda
        /// by substituting the lambda parameters with the invocation arguments and re-transforming the result.
        /// </summary>
        [ExpressionSignature(ExpressionType.Invoke)]
        protected Expression InvokeExpression(InvocationExpression expression)
        {
            if (expression.Expression is LambdaExpression le)
            {
                // le.Parameters and expression.Arguments are already indexable ReadOnlyCollection<T>
                // instances; ReplaceArgumentsCore accepts them directly, so no array copy is needed
                // just to adapt them to the historical array-based ReplaceArguments signature.
                return TransformCore(ReplaceArgumentsCore(le.Body, le.Parameters, expression.Arguments));
            }
            return expression;
        }

        #endregion

        /// <summary>
        /// Called when no custom transformation method matches. In this partial class, it
        /// copies the expression structure using <see cref="ExpressionTransformer.CopyExpression"/> as the default final step.
        /// </summary>
        /// <param name="e">The expression to finalize.</param>
        /// <param name="parameters">Sub-expressions or operands.</param>
        /// <returns>A copy of <paramref name="e"/> with sub-expressions replaced by <paramref name="parameters"/>.</returns>
        protected override Expression FinalizeExpression(Expression e, Expression[] parameters)
        {
            ArgumentNullException.ThrowIfNull(e);
            ArgumentNullException.ThrowIfNull(parameters);

            // NOT gated to the exact built-in runtime type (S4 review round 6 initially added such a gate
            // here, reasoning that CanonicalizeAdditiveExpression/CanonicalizeMultiplicativeExpression's use
            // of CaptureLexicalScopeSnapshot() only worked correctly for the exact type, since
            // OnEnterLambdaScope/OnExitLambdaScope were themselves exact-type-only at the time - round 7
            // instead made those two hooks (and Transform's ambient-scope boundary) unconditional, which
            // fixes the actual root cause: any ExpressionSimplifier subclass now gets correct, isolated
            // scope tracking too, so this dispatch is safe and correct to leave unconditional, exactly as it
            // was before round 6 and, ultimately, before S4 (pre-S4 ToString()-based ordering needed no
            // ambient state at all, so it also worked identically for every subclass).
            if (e is BinaryExpression binaryExpression)
            {
                if ((binaryExpression.NodeType == ExpressionType.Add || binaryExpression.NodeType == ExpressionType.Subtract)
                    && CanCanonicalizeCommutativeBinary(binaryExpression))
                {
                    return CanonicalizeAdditiveExpression(binaryExpression.NodeType, parameters[0], parameters[1]);
                }

                if (binaryExpression.NodeType == ExpressionType.Multiply
                    && CanCanonicalizeCommutativeBinary(binaryExpression))
                {
                    return CanonicalizeMultiplicativeExpression(parameters[0], parameters[1]);
                }
            }

            // The exact-runtime-type check deliberately excludes subclasses. FinalizeExpression is
            // protected virtual, and a derived simplifier can observe that base.FinalizeExpression
            // historically performs another CopyExpression after Prepare* has rebuilt the node. Only
            // the built-in concrete ExpressionSimplifier can safely reuse that private intermediate
            // node because no external override can observe it; a subclass calling
            // base.FinalizeExpression must keep receiving the second, historically-distinct copy.
            if (e is UnaryExpression or BinaryExpression or MethodCallExpression or ConditionalExpression
                && GetType() == typeof(ExpressionSimplifier))
            {
                return e;
            }

            return CopyExpression(e, parameters);
        }

        /// <summary>
        /// Creates a canonical additive form from a binary add or subtract expression by flattening, sorting,
        /// and right-associating terms.
        /// </summary>
        /// <param name="nodeType">The additive node type that produced this operation.</param>
        /// <param name="left">Left expression branch.</param>
        /// <param name="right">Right expression branch.</param>
        /// <returns>A deterministic additive expression tree preserving semantics.</returns>
        /// <remarks>
        /// <para>
        /// <b>Canonical additive form (S5 P10).</b> No collected term is an ordinary numeric zero constant (see
        /// <see cref="IsOrdinaryZeroConstant"/>), opposite occurrences of the same opaque term cancel (see
        /// <see cref="CollectUncancelledTerms"/>), a group that cancels entirely is omitted, and a sum in which
        /// everything cancels is the zero constant of the result type. Before P10, a negative group rebuilt as
        /// <c>0 - term</c> (see <see cref="BuildAdditiveExpression"/>) handed its zero back to the enclosing
        /// canonicalization as a term of its own, and all such zeros were grouped and kept as
        /// <c>0 + (0 + ...)</c>: a left-associated subtraction chain of n terms accumulated
        /// (n - 1)(n - 2)/2 of them. Dropping them is the identity
        /// <see cref="AdditionWithZero(BinaryExpression, Expression, ConstantExpression)"/> already applies, and
        /// cancellation is the one <see cref="SubstractionOfEqualsElements"/> already applies, so the
        /// symbolic-equivalence contract is not widened.
        /// </para>
        /// <para>
        /// The right-associated shape and the <c>0 - term</c> representation of a negative group are kept on
        /// purpose. A left-associated <c>((p - n1) - n2)</c> form would be a fixed point of the subtraction rules,
        /// but every comparer probe at an enclosing level then re-simplifies the whole left sub-chain; when that
        /// sub-chain can reach user code (so it is neither cached nor memoized) the work grows as 3^n (measured
        /// on a 12-term chain: 59,049 user <see cref="object.Equals(object?)"/> calls instead of 3).
        /// </para>
        /// </remarks>
        private Expression CanonicalizeAdditiveExpression(ExpressionType nodeType, Expression left, Expression right)
        {
            var terms = new List<(Expression Term, bool IsNegative)>();
            CollectAdditiveTerms(terms, left, false);
            CollectAdditiveTerms(terms, right, nodeType == ExpressionType.Subtract);
            terms.RemoveAll(static term => IsOrdinaryZeroConstant(term.Term));

            if (terms.Count == 0)
            {
                return Expression.Constant(Convert.ChangeType(0, left.Type), left.Type);
            }

            ParameterExpression[][] scopes = CaptureLexicalScopeSnapshot();

            var annotatedTerms = new List<AnnotatedAdditiveTerm>(terms.Count);
            foreach ((Expression term, bool isNegative) in terms)
            {
                AdditiveGroupClass group = ClassifyForAdditiveGrouping(term);

                // Precompute every structural key CompareAdditiveGroupingOrder will need for this term
                // exactly once here (roadmap S5, P1), instead of letting the O(n log n) sort below rebuild
                // them from scratch on every pairwise comparison. An opaque term's own complete key (built
                // right below) IS ExpressionCanonicalOrder.BuildKeyFromSnapshot(group.Opaque, scopes) -
                // group.Opaque is this same term - so CompareAdditiveGroupingOrder reuses it directly instead
                // of a second ArgumentKeys array. A function-like term additionally needs each ARGUMENT's own
                // key (the primary sort's function-like branch orders by argument-list identity, not by the
                // whole term/exponent), computed once via BuildKeysFromSnapshot so every argument shares one
                // working scope list.
                //
                // group.Arguments/group.Opaque themselves are NOT retained on the annotation below (S5
                // review round 3): only the two scalar fields CompareAdditiveGroupingOrder actually still
                // needs post-annotation (IsFunctionLike, CategoryOrder) are copied out here, keeping
                // AnnotatedAdditiveTerm - copied repeatedly through OrderBy/GroupBy - smaller than carrying
                // the whole AdditiveGroupClass (with its two now-unused reference-type fields) would.
                IReadOnlyList<ExpressionCanonicalOrder.KeyNode>? argumentKeys = group.IsFunctionLike
                    ? ExpressionCanonicalOrder.BuildKeysFromSnapshot(group.Arguments!, scopes)
                    : null;

                annotatedTerms.Add(new AnnotatedAdditiveTerm(
                    term,
                    isNegative,
                    group.IsFunctionLike,
                    group.CategoryOrder,
                    ExpressionCanonicalOrder.BuildKeyFromSnapshot(term, scopes),
                    argumentKeys));
            }

            var orderedTerms = annotatedTerms
                .OrderBy(static term => term, AdditiveGroupingOrderComparer)
                .ThenBy(static term => term.IsNegative ? 0 : 1)
                .ThenBy(static term => term.Key)
                .ToList();

            var rebuiltTerms = new List<Expression>();
            var signedTerms = new List<(Expression Term, bool IsNegative)>();
            foreach (var termGroup in orderedTerms.GroupBy(static term => term.Term, AdditiveGroupingEqualityComparer.Instance))
            {
                signedTerms.Clear();
                CollectUncancelledTerms(termGroup, signedTerms);
                if (signedTerms.Count > 0)
                {
                    rebuiltTerms.Add(BuildAdditiveExpression(signedTerms, left.Type));
                }
            }

            if (rebuiltTerms.Count == 0)
            {
                return Expression.Constant(Convert.ChangeType(0, left.Type), left.Type);
            }

            return BuildRightAssociative(rebuiltTerms, Expression.Add);
        }

        /// <summary>
        /// Determines whether <paramref name="term"/> is a numeric zero constant of an ordinary numeric type, i.e. a
        /// neutral additive term (both <c>+0.0</c> and <c>-0.0</c>, any decimal scale).
        /// </summary>
        /// <param name="term">The collected additive term.</param>
        /// <returns><see langword="true"/> if the term can be dropped from an ordinary additive chain.</returns>
        /// <remarks>
        /// Applies the same zero test as <see cref="AdditionWithZero(BinaryExpression, Expression, ConstantExpression)"/>
        /// (<see cref="NumericValueEquals"/>), so dropping the term is the identity that rule already applies,
        /// including its symbolic, not bit-exact, treatment of signed zero and decimal scale. Only constants whose
        /// static type is one of <see cref="Types.Number"/> qualify; their values are boxed primitives, so no user
        /// code runs.
        /// </remarks>
        private static bool IsOrdinaryZeroConstant(Expression term)
            => term is ConstantExpression { Value: not null } constant
                && Types.Number.Contains(constant.Type)
                && NumericValueEquals(constant.Value, 0);

        /// <summary>
        /// Copies one additive group's signed terms, in order, into <paramref name="signedTerms"/>, leaving out the
        /// opposite occurrences of an opaque term that cancel each other.
        /// </summary>
        /// <param name="termGroup">The ordered terms of one group.</param>
        /// <param name="signedTerms">Receives the group's remaining signed terms; empty when the group cancels entirely.</param>
        /// <remarks>
        /// Every member of an opaque group is structurally equal to the others (the group was formed by
        /// <see cref="AdditiveGroupingEqualityComparer"/>), so <c>t - t</c> cancels, the identity
        /// <see cref="SubstractionOfEqualsElements"/> already applies; no further comparison is made, so no extra
        /// user code can run. A function-like group clusters distinct calls (for example <c>sin(x)</c> and
        /// <c>cos(x)</c>), so it is never cancelled here. Neither is a non-finite floating-point constant group:
        /// <c>NaN - NaN</c> and <c>inf - inf</c> are not zero even symbolically.
        /// </remarks>
        private static void CollectUncancelledTerms(IGrouping<Expression, AnnotatedAdditiveTerm> termGroup, List<(Expression Term, bool IsNegative)> signedTerms)
        {
            // A single enumeration of the group (as before P10): enumerating an IGrouping allocates an enumerator.
            int positiveCount = 0;
            int negativeCount = 0;
            foreach (AnnotatedAdditiveTerm term in termGroup)
            {
                signedTerms.Add((term.Term, term.IsNegative));
                if (term.IsNegative) negativeCount++;
                else positiveCount++;
            }

            if (positiveCount == 0 || negativeCount == 0 || ClassifyForAdditiveGrouping(termGroup.Key).IsFunctionLike || IsNonFiniteConstant(termGroup.Key))
            {
                return;
            }

            // Drop the first min(positive, negative) occurrences of each sign in place, keeping the others in order.
            int positivesToSkip = Math.Min(positiveCount, negativeCount);
            int negativesToSkip = positivesToSkip;
            int kept = 0;
            for (int index = 0; index < signedTerms.Count; index++)
            {
                (Expression Term, bool IsNegative) signedTerm = signedTerms[index];
                if (signedTerm.IsNegative ? negativesToSkip-- > 0 : positivesToSkip-- > 0) continue;
                signedTerms[kept++] = signedTerm;
            }

            signedTerms.RemoveRange(kept, signedTerms.Count - kept);
        }

        /// <summary>Determines whether <paramref name="expression"/> is a <c>NaN</c> or infinite <see cref="double"/>/<see cref="float"/> constant.</summary>
        /// <param name="expression">The group's representative term.</param>
        /// <returns><see langword="true"/> for a non-finite floating-point constant.</returns>
        private static bool IsNonFiniteConstant(Expression expression)
            => expression is ConstantExpression { Value: double doubleValue } ? !double.IsFinite(doubleValue)
                : expression is ConstantExpression { Value: float floatValue } && !float.IsFinite(floatValue);

        /// <summary>
        /// Creates a canonical multiplicative form by flattening factors, sorting them, and right-associating.
        /// </summary>
        /// <param name="left">Left expression branch.</param>
        /// <param name="right">Right expression branch.</param>
        /// <returns>A deterministic multiplicative expression tree preserving semantics.</returns>
        private Expression CanonicalizeMultiplicativeExpression(Expression left, Expression right)
        {
            var factors = new List<Expression>();
            CollectMultiplicativeFactors(factors, left);
            CollectMultiplicativeFactors(factors, right);

            ParameterExpression[][] scopes = CaptureLexicalScopeSnapshot();

            var orderedFactors = factors
                .OrderBy(factor => ExpressionCanonicalOrder.BuildKeyFromSnapshot(factor, scopes))
                .ToList();

            return BuildRightAssociative(orderedFactors, Expression.Multiply);
        }

        /// <summary>
        /// Recursively flattens additive and subtractive nodes into signed terms.
        /// </summary>
        /// <param name="terms">Destination list containing additive terms and their sign.</param>
        /// <param name="expression">Current expression being processed.</param>
        /// <param name="isNegative">Whether the current branch sign is negative.</param>
        /// <remarks>
        /// A nested <see cref="ExpressionType.Add"/>/<see cref="ExpressionType.Subtract"/>/<see cref="ExpressionType.Negate"/>
        /// node is only flattened when <see cref="IsOrdinaryBinaryArithmetic(BinaryExpression)"/>/
        /// <see cref="IsOrdinaryUnaryNegate(UnaryExpression)"/> proves it is ordinary, non-lifted,
        /// predefined-operator arithmetic (see the "Symbolic-equivalence contract" section on
        /// <see cref="ExpressionSimplifier"/>). A custom-operator node is kept as one atomic term instead:
        /// flattening it would silently erase its <see cref="BinaryExpression.Method"/>/
        /// <see cref="UnaryExpression.Method"/> and rebuild it with ordinary <see cref="Expression.Add(Expression, Expression)"/>/
        /// <see cref="Expression.Subtract(Expression, Expression)"/> factories, changing its evaluated result.
        /// </remarks>
        private void CollectAdditiveTerms(List<(Expression Term, bool IsNegative)> terms, Expression expression, bool isNegative)
        {
            if (expression is BinaryExpression binaryExpression && IsOrdinaryBinaryArithmetic(binaryExpression))
            {
                if (binaryExpression.NodeType == ExpressionType.Add)
                {
                    CollectAdditiveTerms(terms, binaryExpression.Left, isNegative);
                    CollectAdditiveTerms(terms, binaryExpression.Right, isNegative);
                    return;
                }

                if (binaryExpression.NodeType == ExpressionType.Subtract)
                {
                    CollectAdditiveTerms(terms, binaryExpression.Left, isNegative);
                    CollectAdditiveTerms(terms, binaryExpression.Right, !isNegative);
                    return;
                }
            }

            if (expression is UnaryExpression unaryExpression && IsOrdinaryUnaryNegate(unaryExpression))
            {
                CollectAdditiveTerms(terms, unaryExpression.Operand, !isNegative);
                return;
            }

            terms.Add((expression, isNegative));
        }

        /// <summary>
        /// Recursively flattens multiplication nodes into a linear factors list.
        /// </summary>
        /// <param name="factors">Destination list receiving factors.</param>
        /// <param name="expression">Current expression being processed.</param>
        /// <remarks>
        /// Mirrors <see cref="CollectAdditiveTerms"/>'s reasoning: a nested <see cref="ExpressionType.Multiply"/>
        /// node is only flattened when <see cref="IsOrdinaryBinaryArithmetic(BinaryExpression)"/> proves it
        /// ordinary; otherwise it is kept as one atomic factor so its custom
        /// <see cref="BinaryExpression.Method"/> survives canonicalization.
        /// </remarks>
        private void CollectMultiplicativeFactors(List<Expression> factors, Expression expression)
        {
            if (expression is BinaryExpression binaryExpression && binaryExpression.NodeType == ExpressionType.Multiply && IsOrdinaryBinaryArithmetic(binaryExpression))
            {
                CollectMultiplicativeFactors(factors, binaryExpression.Left);
                CollectMultiplicativeFactors(factors, binaryExpression.Right);
                return;
            }

            factors.Add(expression);
        }

        /// <summary>
        /// Builds a right-associated expression chain from an ordered list.
        /// </summary>
        /// <param name="expressions">Ordered expression items.</param>
        /// <param name="combine">Binary node factory used to combine two expressions.</param>
        /// <returns>The right-associated expression chain.</returns>
        private static Expression BuildRightAssociative(IReadOnlyList<Expression> expressions, Func<Expression, Expression, BinaryExpression> combine)
        {
            ArgumentNullException.ThrowIfNull(expressions);
            ArgumentNullException.ThrowIfNull(combine);

            if (expressions.Count == 0)
            {
                throw new ArgumentException("At least one expression is required.", nameof(expressions));
            }

            Expression result = expressions[^1];
            for (int index = expressions.Count - 2; index >= 0; index--)
            {
                result = combine(expressions[index], result);
            }

            return result;
        }

        /// <summary>
        /// One term/factor annotated with everything <see cref="CanonicalizeAdditiveExpression"/> needs to
        /// order and group it. Not a cross-call cache: a fresh list of these is built on every
        /// <see cref="CanonicalizeAdditiveExpression"/> call, matching the roadmap's S4/S5 boundary (S4 is
        /// a correctness/structure stage; a caching layer that survives beyond one canonicalization call is
        /// left to S5).
        /// </summary>
        /// <remarks>
        /// <b>S5 (roadmap P1):</b> every structural key <see cref="CompareAdditiveGroupingOrder"/> needs is
        /// now computed exactly once per term, here, rather than being rebuilt from scratch on every pairwise
        /// comparison during the O(n log n) sort in <see cref="CanonicalizeAdditiveExpression"/>. For an
        /// "opaque" (non-function-like) term, <see cref="Key"/> already IS
        /// <c>ExpressionCanonicalOrder.BuildKey(term, scopes)</c> for this same term - so the primary sort's
        /// opaque branch reuses <see cref="Key"/> directly instead of a second, redundant build. For a
        /// function-like term, the primary sort orders by argument-list identity (not by the whole term,
        /// which would also fold in the ignored exponent for a power-wrapped call), so
        /// <see cref="ArgumentKeys"/> holds each argument's own key, built once via
        /// <see cref="ExpressionCanonicalOrder.BuildKeys"/>.
        /// </remarks>
        /// <remarks>
        /// <b>S5 review round 3:</b> only carries the two <see cref="AdditiveGroupClass"/> fields
        /// <see cref="CompareAdditiveGroupingOrder"/> still reads after annotation
        /// (<see cref="IsFunctionLike"/>, <see cref="CategoryOrder"/>) rather than the whole
        /// <see cref="AdditiveGroupClass"/> value (which also carries <c>Arguments</c>/<c>Opaque</c> - needed
        /// only transiently, while building <see cref="ArgumentKeys"/>/<see cref="Key"/> in the annotation
        /// loop, never afterward). Keeping this struct's per-element footprint small - it is copied repeatedly
        /// through <c>List&lt;T&gt;</c>/<c>OrderBy</c>/<c>GroupBy</c> - is itself part of this stage's
        /// construction-cost goal.
        /// </remarks>
        private readonly struct AnnotatedAdditiveTerm(
            Expression term,
            bool isNegative,
            bool isFunctionLike,
            int categoryOrder,
            ExpressionCanonicalOrder.KeyNode key,
            IReadOnlyList<ExpressionCanonicalOrder.KeyNode>? argumentKeys)
        {
            public Expression Term { get; } = term;
            public bool IsNegative { get; } = isNegative;

            /// <summary>Whether this term classified as function-like (see <see cref="AdditiveGroupClass.IsFunctionLike"/>).</summary>
            public bool IsFunctionLike { get; } = isFunctionLike;

            /// <summary>This term's function-category order (see <see cref="AdditiveGroupClass.CategoryOrder"/>); meaningful only when <see cref="IsFunctionLike"/> is <see langword="true"/>.</summary>
            public int CategoryOrder { get; } = categoryOrder;

            public ExpressionCanonicalOrder.KeyNode Key { get; } = key;

            /// <summary>Each function-like term's argument keys, precomputed once (see this type's remarks); <see langword="null"/> for an opaque term.</summary>
            public IReadOnlyList<ExpressionCanonicalOrder.KeyNode>? ArgumentKeys { get; } = argumentKeys;
        }

        /// <summary>
        /// The coarse additive-grouping classification of one term: either "function-like" (a
        /// <see cref="MethodCallExpression"/>, or a <see cref="ExpressionType.Power"/> node whose base is
        /// one), clustering by function family and argument identity while deliberately ignoring the
        /// exponent; or "opaque", ordered/grouped by full structural identity. See
        /// <see cref="ClassifyForAdditiveGrouping"/> and this class's "Additive grouping" remarks.
        /// </summary>
        private readonly struct AdditiveGroupClass
        {
            public required bool IsFunctionLike { get; init; }
            public int CategoryOrder { get; init; }
            public IReadOnlyList<Expression>? Arguments { get; init; }
            public Expression? Opaque { get; init; }
        }

        /// <summary>
        /// Classifies <paramref name="expression"/> for additive grouping: <see cref="MethodCallExpression"/>
        /// and <c>Power(MethodCall, exponent)</c> cluster by function family/argument identity (deliberately
        /// coarser than full structural identity — see this class's "Additive grouping" remarks); every
        /// other node is classified opaque and grouped/ordered by full structural identity.
        /// </summary>
        /// <param name="expression">Expression to classify.</param>
        /// <returns>The resulting <see cref="AdditiveGroupClass"/>.</returns>
        private static AdditiveGroupClass ClassifyForAdditiveGrouping(Expression expression)
        {
            if (expression is BinaryExpression powerExpression
                && powerExpression.NodeType == ExpressionType.Power
                && powerExpression.Left is MethodCallExpression powerMethodCallExpression)
            {
                return new AdditiveGroupClass
                {
                    IsFunctionLike = true,
                    CategoryOrder = GetFunctionCategoryOrder(powerMethodCallExpression.Method.Name),
                    Arguments = powerMethodCallExpression.Arguments,
                };
            }

            if (expression is MethodCallExpression methodCallExpression)
            {
                return new AdditiveGroupClass
                {
                    IsFunctionLike = true,
                    CategoryOrder = GetFunctionCategoryOrder(methodCallExpression.Method.Name),
                    Arguments = methodCallExpression.Arguments,
                };
            }

            return new AdditiveGroupClass { IsFunctionLike = false, Opaque = expression };
        }

        /// <summary>
        /// Orders two annotated additive terms by their coarse grouping classification: opaque terms sort
        /// before function-like terms; within the same classification, function-like terms order by
        /// structural argument-list identity then function category, and opaque terms order by full
        /// structural identity. This is a RELATIVE ORDER only — ties are expected and resolved by the
        /// caller's stable sort — never the grouping EQUALITY itself, which is decided separately by
        /// <see cref="AdditiveGroupingEqualityComparer"/>.
        /// </summary>
        /// <param name="x">The first term.</param>
        /// <param name="y">The second term.</param>
        /// <returns>A negative value if <paramref name="x"/> sorts before <paramref name="y"/>, zero if tied, positive otherwise.</returns>
        /// <remarks>
        /// <b>S5 (roadmap P1):</b> consumes only the structural keys <see cref="AnnotatedAdditiveTerm"/>
        /// already precomputed once per term (<see cref="AnnotatedAdditiveTerm.Key"/> for the opaque branch,
        /// <see cref="AnnotatedAdditiveTerm.ArgumentKeys"/> for the function-like branch) instead of taking a
        /// lexical scope snapshot and rebuilding a <see cref="ExpressionCanonicalOrder.KeyNode"/> tree from
        /// scratch for both operands on every pairwise comparison the sort performs, which is what this
        /// method did before S5. The comparison RESULT is unchanged: for an opaque term,
        /// <c>AnnotatedAdditiveTerm.Key</c> already equals what rebuilding
        /// <c>ExpressionCanonicalOrder.BuildKey(term, scopes)</c> here would produce, since <c>Key</c> was
        /// built from that same term.
        /// </remarks>
        /// <remarks>
        /// <b>S5 review round 3:</b> does not capture any lexical scope or other outer state, so
        /// the delegate wrapping it (<see cref="AdditiveGroupingOrderComparer"/>) is a <c>static readonly</c>
        /// field built once per process rather than a fresh <c>Comparer&lt;AnnotatedAdditiveTerm&gt;.Create(...)</c>
        /// call (and its backing delegate/adapter allocation) on every <see cref="CanonicalizeAdditiveExpression"/> call.
        /// </remarks>
        private static int CompareAdditiveGroupingOrder(AnnotatedAdditiveTerm x, AnnotatedAdditiveTerm y)
        {
            if (x.IsFunctionLike != y.IsFunctionLike)
            {
                return x.IsFunctionLike ? 1 : -1;
            }

            if (!x.IsFunctionLike)
            {
                return x.Key.CompareTo(y.Key);
            }

            int argumentsCompare = CompareArgumentKeyLists(x.ArgumentKeys!, y.ArgumentKeys!);
            return argumentsCompare != 0 ? argumentsCompare : x.CategoryOrder.CompareTo(y.CategoryOrder);
        }

        /// <summary>
        /// Cached <see cref="IComparer{T}"/> wrapping <see cref="CompareAdditiveGroupingOrder"/>, reused across
        /// every <see cref="CanonicalizeAdditiveExpression"/> call (S5 review round 3) since the method it
        /// wraps is stateless (<see langword="static"/>, no captured scope or other per-call state).
        /// </summary>
        private static readonly IComparer<AnnotatedAdditiveTerm> AdditiveGroupingOrderComparer =
            Comparer<AnnotatedAdditiveTerm>.Create(CompareAdditiveGroupingOrder);

        /// <summary>Lexicographically compares two function argument lists' precomputed complete structural order keys.</summary>
        /// <param name="x">The first argument list's keys.</param>
        /// <param name="y">The second argument list's keys.</param>
        /// <returns>A negative value if <paramref name="x"/> sorts before <paramref name="y"/>, zero if tied, positive otherwise.</returns>
        private static int CompareArgumentKeyLists(IReadOnlyList<ExpressionCanonicalOrder.KeyNode> x, IReadOnlyList<ExpressionCanonicalOrder.KeyNode> y)
        {
            int minCount = Math.Min(x.Count, y.Count);
            for (int i = 0; i < minCount; i++)
            {
                int c = x[i].CompareTo(y[i]);
                if (c != 0) return c;
            }
            return x.Count.CompareTo(y.Count);
        }

        /// <summary>Structural, scope-independent grouping equality for one additive-term operand: same instance, or structurally equal per <see cref="ExpressionComparer.StructuralEqualsRaw(Expression?, Expression?)"/>.</summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns><see langword="true"/> if the operands belong in the same additive group.</returns>
        /// <remarks>
        /// No enclosing-scope snapshot is needed here: <paramref name="x"/> and <paramref name="y"/> are
        /// sibling sub-expressions of the same original tree, so any parameter free relative to both
        /// resolves through <see cref="ExpressionComparer"/>'s own free-parameter reference-equality policy
        /// to the exact same enclosing binding, without this method needing to observe that scope itself.
        /// A same-instance shortcut is applied first so a literally-repeated unsupported-kind term still
        /// groups with itself, while <see cref="ExpressionComparer.StructuralEqualsRaw(Expression?, Expression?)"/>'s
        /// own conservative "never structurally equal" answer for an unsupported node kind still applies to
        /// two genuinely distinct unsupported instances.
        /// </remarks>
        private static bool GroupEquals(Expression x, Expression y)
            => ReferenceEquals(x, y) || ExpressionComparer.StructuralEqualsRaw(x, y);

        /// <summary>Hash mirroring <see cref="GroupEquals"/>, via <see cref="ExpressionComparer.StructuralHashRaw(Expression?)"/>.</summary>
        /// <param name="e">The operand to hash.</param>
        /// <returns>A hash code consistent with <see cref="GroupEquals"/>.</returns>
        private static int GroupHash(Expression e) => ExpressionComparer.StructuralHashRaw(e);

        /// <summary>
        /// Additive-grouping equality over whole terms, used by <c>GroupBy</c> to cluster the (already
        /// ordered) term sequence: two terms belong to the same group only when their
        /// <see cref="ClassifyForAdditiveGrouping"/> classification and, within it, their structural
        /// content (per <see cref="GroupEquals"/>/<see cref="GroupHash"/>) match exactly. This is
        /// deliberately a SEPARATE mechanism from <see cref="ExpressionCanonicalOrder.KeyNode"/>'s ordering
        /// ties (which are stable-sort placeholders, not claims of equality) — see this class's "Additive
        /// grouping" remarks.
        /// </summary>
        /// <remarks>
        /// <b>S5 (roadmap P2) - measured and rejected.</b> An `IEqualityComparer&lt;AnnotatedAdditiveTerm&gt;`
        /// variant that reused the classification <see cref="AnnotatedAdditiveTerm"/> already carried at the
        /// time of this experiment (avoiding this method's re-classification, which itself is only a cheap
        /// re-run of the same NodeType pattern match) was benchmarked and measurably REGRESSED both time and
        /// allocations at n=32/128 versus the version kept here - copying the larger
        /// <see cref="AnnotatedAdditiveTerm"/> struct (as it existed at the time: five fields, including a
        /// whole nested <see cref="AdditiveGroupClass"/> - since narrowed by S5 review round 3 to just the
        /// two scalar fields still needed, see that type's own remarks) through <c>GroupBy</c>'s internal
        /// lookup/grouping storage cost more than the cheap re-classification it avoided, since
        /// <c>GroupBy</c>'s key/element storage already handles a plain <see cref="Expression"/> reference
        /// (8 bytes) far more cheaply. Re-tested (S5 review round 4) against the round-3-shrunk struct, since
        /// the struct round 1 measured no longer exists: the regression roughly HALVED (~24 bytes/term instead
        /// of ~48) but did not reverse - still rejected. See the S5 roadmap progress notes for the exact
        /// benchmark numbers from both rounds.
        /// </remarks>
        private sealed class AdditiveGroupingEqualityComparer : IEqualityComparer<Expression>
        {
            public static readonly AdditiveGroupingEqualityComparer Instance = new();
            private AdditiveGroupingEqualityComparer() { }

            /// <summary>Determines whether <paramref name="x"/> and <paramref name="y"/> belong to the same additive group, per this type's own remarks.</summary>
            /// <param name="x">The first term to compare.</param>
            /// <param name="y">The second term to compare.</param>
            /// <returns><see langword="true"/> if both terms classify into the same additive group.</returns>
            /// <remarks>
            /// Must stay exactly consistent with <see cref="GetHashCode(Expression)"/>'s own classification
            /// decision - two terms this method groups together must always hash identically, and vice versa,
            /// or <c>GroupBy</c>'s bucketing (this comparer's sole caller) silently corrupts.
            /// </remarks>
            public bool Equals(Expression? x, Expression? y)
            {
                if (x is null || y is null) return ReferenceEquals(x, y);

                AdditiveGroupClass gx = ClassifyForAdditiveGrouping(x);
                AdditiveGroupClass gy = ClassifyForAdditiveGrouping(y);
                if (gx.IsFunctionLike != gy.IsFunctionLike) return false;

                if (gx.IsFunctionLike)
                {
                    if (gx.CategoryOrder != gy.CategoryOrder) return false;
                    if (gx.Arguments!.Count != gy.Arguments!.Count) return false;
                    for (int i = 0; i < gx.Arguments.Count; i++)
                    {
                        if (!GroupEquals(gx.Arguments[i], gy.Arguments[i])) return false;
                    }
                    return true;
                }

                return GroupEquals(gx.Opaque!, gy.Opaque!);
            }

            /// <summary>Returns a hash code consistent with <see cref="Equals(Expression?, Expression?)"/>'s additive-group classification.</summary>
            /// <param name="obj">The term to hash.</param>
            /// <returns>A hash code such that two terms <see cref="Equals(Expression?, Expression?)"/> groups together always hash identically.</returns>
            public int GetHashCode(Expression obj)
            {
                AdditiveGroupClass g = ClassifyForAdditiveGrouping(obj);
                var hc = new HashCode();
                hc.Add(g.IsFunctionLike);
                if (g.IsFunctionLike)
                {
                    hc.Add(g.CategoryOrder);
                    foreach (Expression argument in g.Arguments!)
                    {
                        hc.Add(GroupHash(argument));
                    }
                }
                else
                {
                    hc.Add(GroupHash(g.Opaque!));
                }
                return hc.ToHashCode();
            }
        }

        /// <summary>
        /// Returns an ordering bucket for mathematical function names.
        /// </summary>
        /// <param name="functionName">Function name to classify.</param>
        /// <returns>An integer order where lower values are sorted first.</returns>
        private static int GetFunctionCategoryOrder(string functionName)
        {
            return functionName switch
            {
                nameof(double.Sin) or nameof(double.Cos) or nameof(double.Tan) or nameof(double.Asin) or nameof(double.Acos) or nameof(double.Atan) => 0,
                nameof(double.Sinh) or nameof(double.Cosh) or nameof(double.Tanh) or nameof(double.Asinh) or nameof(double.Acosh) or nameof(double.Atanh) => 1,
                nameof(double.Exp) or nameof(double.Log) or nameof(double.Log2) or nameof(double.Log10) or nameof(double.Pow) => 2,
                _ => 3
            };
        }

        /// <summary>
        /// Rebuilds a list of signed additive terms without relying on unary negation.
        /// </summary>
        /// <param name="signedTerms">Ordered signed terms to rebuild.</param>
        /// <param name="resultType">Result type of the additive expression.</param>
        /// <returns>An equivalent additive expression.</returns>
        private static Expression BuildAdditiveExpression(IReadOnlyList<(Expression Term, bool IsNegative)> signedTerms, Type resultType)
        {
            ArgumentNullException.ThrowIfNull(signedTerms);
            ArgumentNullException.ThrowIfNull(resultType);

            if (signedTerms.Count == 0)
            {
                throw new ArgumentException("At least one term is required.", nameof(signedTerms));
            }

            Expression result = signedTerms[^1].IsNegative
                ? Expression.Subtract(Expression.Constant(Convert.ChangeType(0, resultType), resultType), signedTerms[^1].Term)
                : signedTerms[^1].Term;

            for (int index = signedTerms.Count - 2; index >= 0; index--)
            {
                result = signedTerms[index].IsNegative
                    ? Expression.Subtract(result, signedTerms[index].Term)
                    : Expression.Add(signedTerms[index].Term, result);
            }

            return result;
        }

        /// <summary>
        /// Determines whether binary canonicalization is safe for the current operator and type.
        /// </summary>
        /// <param name="binaryExpression">Binary expression candidate.</param>
        /// <returns><see langword="true"/> when canonicalization is safe; otherwise <see langword="false"/>.</returns>
        private static bool CanCanonicalizeCommutativeBinary(BinaryExpression binaryExpression)
            => IsOrdinaryBinaryArithmetic(binaryExpression);

        #region Operator safety (S3 symbolic-equivalence contract)

        /// <summary>The binary arithmetic operators covered by <see cref="_ordinaryBinaryOperatorMethods"/>.</summary>
        private static readonly ExpressionType[] OrdinaryBinaryArithmeticOperators =
        [
            ExpressionType.Add,
            ExpressionType.Subtract,
            ExpressionType.Multiply,
            ExpressionType.Divide,
            ExpressionType.Power,
        ];

        /// <summary>
        /// Caches, for each numeric type in <see cref="Types.Number"/> and each operator in
        /// <see cref="OrdinaryBinaryArithmeticOperators"/>, the exact <see cref="BinaryExpression.Method"/>
        /// a freshly-built, non-lifted <see cref="Expression"/> factory call produces for that (operator,
        /// type) pair. Built once, structurally, by actually calling <see cref="Expression.Add(Expression, Expression)"/>
        /// (and the sibling factories) with two same-typed parameters and reading back <c>.Method</c>,
        /// instead of hard-coding an assumption. This matters because the "default" method is not
        /// uniformly <see langword="null"/>: it is <see langword="null"/> for the CLR-intrinsic primitives
        /// (<see cref="int"/>, <see cref="double"/>, ...), but a genuine CLR operator method for
        /// <see cref="decimal"/> (for example <c>Decimal.op_Addition</c>) - and <see cref="ExpressionType.Power"/>
        /// resolves to a concrete <see cref="Math.Pow(double, double)"/> method even for the one type
        /// (<see cref="double"/>) that supports it at all. A (operator, type) pair absent from this table
        /// means the <see cref="Expression"/> factories do not support that operator for that type without
        /// an explicit custom method (for example <see cref="ExpressionType.Power"/> for every
        /// <see cref="Types.Number"/> entry except <see cref="double"/>, or any operator at all for
        /// <see cref="byte"/>/<see cref="sbyte"/>, which the CLR does not define arithmetic operators on
        /// directly), so no <see cref="BinaryExpression"/> built from it can ever be classified as ordinary.
        /// </summary>
        private static readonly IReadOnlyDictionary<(ExpressionType NodeType, Type Type), MethodInfo?> _ordinaryBinaryOperatorMethods
            = BuildOrdinaryBinaryOperatorMethods();

        /// <summary>
        /// Caches, for each type in <see cref="Types.Number"/> that supports it, the exact
        /// <see cref="UnaryExpression.Method"/> a freshly-built, non-lifted <see cref="Expression.Negate(Expression)"/>
        /// call produces - mirrors <see cref="_ordinaryBinaryOperatorMethods"/>'s reasoning and structural
        /// derivation for unary negation (<see langword="null"/> for CLR-intrinsic primitives,
        /// <c>Decimal.op_UnaryNegation</c> for <see cref="decimal"/>, absent for types such as
        /// <see cref="byte"/>/<see cref="sbyte"/> that have no unary minus operator at all).
        /// </summary>
        private static readonly IReadOnlyDictionary<Type, MethodInfo?> _ordinaryNegateMethods
            = BuildOrdinaryNegateMethods();

        /// <summary>Builds <see cref="_ordinaryBinaryOperatorMethods"/> once by probing every (operator, type) combination.</summary>
        private static IReadOnlyDictionary<(ExpressionType, Type), MethodInfo?> BuildOrdinaryBinaryOperatorMethods()
        {
            var table = new Dictionary<(ExpressionType, Type), MethodInfo?>();
            foreach (Type type in Types.Number)
            {
                ParameterExpression left = Expression.Parameter(type);
                ParameterExpression right = Expression.Parameter(type);
                foreach (ExpressionType nodeType in OrdinaryBinaryArithmeticOperators)
                {
                    try
                    {
                        BinaryExpression probe = nodeType switch
                        {
                            ExpressionType.Add => Expression.Add(left, right),
                            ExpressionType.Subtract => Expression.Subtract(left, right),
                            ExpressionType.Multiply => Expression.Multiply(left, right),
                            ExpressionType.Divide => Expression.Divide(left, right),
                            ExpressionType.Power => Expression.Power(left, right),
                            _ => throw new InvalidOperationException($"Unsupported probe operator {nodeType}."),
                        };
                        table[(nodeType, type)] = probe.Method;
                    }
                    catch (InvalidOperationException)
                    {
                        // The CLR/Expression factories do not define this operator for this type at all:
                        // leave the pair absent from the table so it is never classified as ordinary.
                    }
                }
            }
            return table;
        }

        /// <summary>Builds <see cref="_ordinaryNegateMethods"/> once by probing every type in <see cref="Types.Number"/>.</summary>
        private static IReadOnlyDictionary<Type, MethodInfo?> BuildOrdinaryNegateMethods()
        {
            var table = new Dictionary<Type, MethodInfo?>();
            foreach (Type type in Types.Number)
            {
                ParameterExpression operand = Expression.Parameter(type);
                try
                {
                    table[type] = Expression.Negate(operand).Method;
                }
                catch (InvalidOperationException)
                {
                    // No unary minus operator for this type: leave it absent from the table.
                }
            }
            return table;
        }

        /// <summary>
        /// Determines whether <paramref name="unary"/> represents ordinary, non-lifted, predefined numeric
        /// negation - never a user/custom <see cref="UnaryExpression.Method"/> and never lifted nullable
        /// arithmetic. Algebraic rules must call this (or <see cref="IsOrdinaryBinaryArithmetic(BinaryExpression)"/>)
        /// before treating a node as safe to fold, reorder, flatten, or discard - see the "Symbolic-equivalence
        /// contract" section on <see cref="ExpressionSimplifier"/>.
        /// </summary>
        /// <param name="unary">The unary expression to classify.</param>
        /// <returns><see langword="true"/> when <paramref name="unary"/> is ordinary built-in negation; otherwise <see langword="false"/>.</returns>
        internal static bool IsOrdinaryUnaryNegate(UnaryExpression unary)
        {
            ArgumentNullException.ThrowIfNull(unary);

            return unary.NodeType == ExpressionType.Negate
                && !unary.IsLifted
                && !unary.IsLiftedToNull
                && _ordinaryNegateMethods.TryGetValue(unary.Type, out MethodInfo? expectedMethod)
                && unary.Method == expectedMethod;
        }

        /// <summary>
        /// Determines whether <paramref name="binary"/> represents an ordinary, non-lifted, predefined
        /// numeric <see cref="ExpressionType.Add"/>, <see cref="ExpressionType.Subtract"/>,
        /// <see cref="ExpressionType.Multiply"/>, <see cref="ExpressionType.Divide"/> or
        /// <see cref="ExpressionType.Power"/> operation - never a user/custom
        /// <see cref="BinaryExpression.Method"/> and never lifted nullable arithmetic. Algebraic rules
        /// (zero/one identities, constant folding, factoring, multiplicative reassociation, power
        /// combination, canonicalization, and the outer operator of a logarithm/trigonometric identity)
        /// must call this before folding, reordering, flattening or discarding a node - see the
        /// "Symbolic-equivalence contract" section on <see cref="ExpressionSimplifier"/>.
        /// </summary>
        /// <param name="binary">The binary expression to classify.</param>
        /// <returns><see langword="true"/> when <paramref name="binary"/> is ordinary built-in arithmetic; otherwise <see langword="false"/>.</returns>
        internal static bool IsOrdinaryBinaryArithmetic(BinaryExpression binary)
        {
            ArgumentNullException.ThrowIfNull(binary);

            return !binary.IsLifted
                && !binary.IsLiftedToNull
                && _ordinaryBinaryOperatorMethods.TryGetValue((binary.NodeType, binary.Type), out MethodInfo? expectedMethod)
                && binary.Method == expectedMethod;
        }

        /// <summary>
        /// Narrower than <see cref="IsOrdinaryBinaryArithmetic(BinaryExpression)"/>: additionally requires
        /// <paramref name="binary"/>'s type to be a field (<see cref="Types.FloatingPointNumber"/> -
        /// floating point or <see cref="decimal"/>) rather than a ring with truncating integer division.
        /// Identities that divide by a divisor and later multiply back (for example
        /// <c>x / (y / z) -&gt; (x*z) / y</c>) are only valid under field division; applying them to
        /// truncating integer division can change the result - see the "Symbolic-equivalence contract"
        /// section on <see cref="ExpressionSimplifier"/> and the S3 roadmap entry for the discriminating
        /// counterexample <c>8 / (3 / 2)</c> (source: <c>8</c>; field rewrite <c>(8*2)/3</c>: <c>5</c>).
        /// </summary>
        /// <param name="binary">The division expression to classify.</param>
        /// <returns><see langword="true"/> when <paramref name="binary"/> is safe to reassociate as field division; otherwise <see langword="false"/>.</returns>
        internal static bool IsOrdinaryFieldDivision(BinaryExpression binary)
        {
            ArgumentNullException.ThrowIfNull(binary);

            return binary.NodeType == ExpressionType.Divide
                && IsOrdinaryBinaryArithmetic(binary)
                && Types.FloatingPointNumber.Contains(binary.Type);
        }

        #endregion
    }
}
