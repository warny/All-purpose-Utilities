using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
            List<ParameterExpression[]>? callerScope = _lexicalScopeStack;
            _lexicalScopeStack = null;
            try
            {
                return TransformCore(expression);
            }
            finally
            {
                _lexicalScopeStack = callerScope;
            }
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
        /// overrides this method itself.
        /// </remarks>
        protected override Expression PrepareExpression(Expression e)
        {
            if (e is null && GetType() == typeof(ExpressionSimplifier))
            {
                return null;
            }

            return TransformCore(e);
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
        internal override LambdaExpression RebuildLambdaExpression(LambdaExpression expression, Expression body, ParameterExpression[] parameters)
        {
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
        /// Captures an immutable snapshot of the currently-open lambda parameter scopes (outermost first)
        /// for use by <see cref="ExpressionCanonicalOrder.BuildKey"/>/<see cref="ExpressionCanonicalOrder.Compare"/>.
        /// Taken once per canonicalization call: every term/factor being ordered in that one call is a
        /// sibling sub-expression of the same node, so they all share the same enclosing scope.
        /// </summary>
        /// <returns>The snapshot; empty when no lambda currently encloses the node being canonicalized.</returns>
        private static IReadOnlyList<ParameterExpression[]> CaptureLexicalScopeSnapshot()
            => LexicalScopeStack.Count == 0 ? [] : LexicalScopeStack.ToArray();

        #endregion

        #region Operations with 0 and 1

        /// <summary>
        /// Simplifies <c>left + 0</c> to <c>left</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Add)]
        public Expression AdditionWithZero(BinaryExpression e, Expression left, [ConstantNumeric(0)] ConstantExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            if (NumberUtils.CompareNumeric(right.Value, 0) == 0) return TransformCore(left);
            return null;
        }

        /// <summary>
        /// Simplifies <c>0 + right</c> to <c>right</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Add)]
        public Expression AdditionWithZero(BinaryExpression e, [ConstantNumeric(0)] ConstantExpression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            if (NumberUtils.CompareNumeric(left.Value, 0) == 0) return right;
            return null;
        }

        /// <summary>
        /// Simplifies <c>left - 0</c> to <c>left</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Subtract)]
        public Expression SubstractionWithZero(BinaryExpression e, Expression left, [ConstantNumeric(0)] ConstantExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            if (NumberUtils.CompareNumeric(right.Value, 0) == 0) return left;
            return null;
        }

        /// <summary>
        /// Simplifies <c>0 - right</c> to <c>-right</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Subtract)]
        public Expression SubstractionWithZero(BinaryExpression e, [ConstantNumeric(0)] ConstantExpression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            if (NumberUtils.CompareNumeric(left.Value, 0) == 0) return TransformCore(Expression.Negate(right));
            return null;
        }

        /// <summary>
        /// Simplifies multiplication by 0, 1, or -1 (e.g., <c>x * 0</c> to 0, <c>x * 1</c> to <c>x</c>, etc.).
        /// </summary>
        [ExpressionSignature(ExpressionType.Multiply)]
        public Expression MultiplicationWithZeroOrOne(BinaryExpression e, Expression left, [ConstantNumeric(0, 1, -1)] ConstantExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            if (NumberUtils.CompareNumeric(right.Value, 0) == 0) return right;  // x * 0 => 0
            if (NumberUtils.CompareNumeric(right.Value, 1) == 0) return left;   // x * 1 => x
            if (NumberUtils.CompareNumeric(right.Value, -1) == 0) return Expression.Negate(left); // x * -1 => -x
            return null;
        }

        /// <summary>
        /// Simplifies multiplication by 0, 1, or -1 (e.g., <c>0 * x</c>, <c>1 * x</c>, <c>-1 * x</c>).
        /// </summary>
        [ExpressionSignature(ExpressionType.Multiply)]
        public Expression MultiplicationWithZeroOrOne(BinaryExpression e, [ConstantNumeric(0, 1, -1)] ConstantExpression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            if (NumberUtils.CompareNumeric(left.Value, 0) == 0) return left;
            if (NumberUtils.CompareNumeric(left.Value, 1) == 0) return right;
            if (NumberUtils.CompareNumeric(left.Value, -1) == 0) return Expression.Negate(right);
            return null;
        }

        /// <summary>
        /// Simplifies division by 0, 1, or -1 (throwing if 0).
        /// </summary>
        [ExpressionSignature(ExpressionType.Divide)]
        public Expression DivideWithZeroOrOne(BinaryExpression e, Expression left, ConstantExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            if (NumberUtils.CompareNumeric(right.Value, 0) == 0) throw new DivideByZeroException();
            if (NumberUtils.CompareNumeric(right.Value, 1) == 0) return left;
            if (NumberUtils.CompareNumeric(right.Value, -1) == 0) return Expression.Negate(left);
            return null;
        }

        /// <summary>
        /// Simplifies <c>0 / x</c> to <c>0</c>.
        /// </summary>
        [ExpressionSignature(ExpressionType.Divide)]
        public Expression DivideWithZero(BinaryExpression e, [ConstantNumeric(0)] ConstantExpression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            if (NumberUtils.CompareNumeric(left.Value, 0) == 0) return left;
            return null;
        }

        /// <summary>
        /// Simplifies <c>0^x</c> or <c>1^x</c> to 0 or 1 respectively.
        /// </summary>
        [ExpressionSignature(ExpressionType.Power)]
        public Expression PowerOfZeroOrOne(BinaryExpression e, [ConstantNumeric(0, 1)] ConstantExpression left, Expression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            if (NumberUtils.CompareNumeric(left.Value, 0) == 0) return left; // 0^x => 0
            if (NumberUtils.CompareNumeric(left.Value, 1) == 0) return left; // 1^x => 1
            return null;
        }

        /// <summary>
        /// Simplifies <c>x^0</c> or <c>x^1</c> to <c>1</c> or <c>x</c> respectively.
        /// </summary>
        [ExpressionSignature(ExpressionType.Power)]
        public Expression PowerByZeroOrOne(BinaryExpression e, Expression left, ConstantExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            if (NumberUtils.CompareNumeric(right.Value, 0) == 0) return Expression.Constant(Convert.ChangeType(1, right.Type));
            if (NumberUtils.CompareNumeric(right.Value, 1) == 0) return TransformCore(left);
            if (NumberUtils.CompareNumeric(right.Value, -1) == 0) return TransformCore(Expression.Divide(Expression.Constant(Convert.ChangeType(1, left.Type)), left));
            if (NumberUtils.CompareNumeric(right.Value, 0) == -1) return Expression.Divide(Expression.Constant(Convert.ChangeType(1, left.Type)), TransformCore(Expression.Power(left, Expression.Constant(-(double)Convert.ChangeType(right.Value, typeof(double))))));
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
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            return Expression.Constant((object)((dynamic)left.Value + (dynamic)right.Value));
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
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            return Expression.Constant((object)((dynamic)left.Value - (dynamic)right.Value));
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

            // Check if the single factor is truly 1
            if (leftAugmented && leftright is ConstantExpression leftRightConst && NumberUtils.CompareNumeric(leftRightConst.Value, 1) == 0)
                return null;
            if (rightAugmented && rightright is ConstantExpression rightRightConst && NumberUtils.CompareNumeric(rightRightConst.Value, 1) == 0)
                return null;

            // Attempt to unify or swap factors for factoring out
            if (!leftAugmented
                && !rightAugmented
                && ExpressionComparer.Default.Equals(leftleft, rightleft)
                && !ExpressionComparer.Default.Equals(leftright, rightright))
            {
                ObjectUtils.Swap(ref leftleft, ref leftright);
                ObjectUtils.Swap(ref rightleft, ref rightright);
            }
            else if (ExpressionComparer.Default.Equals(leftleft, rightright))
            {
                ObjectUtils.Swap(ref leftleft, ref leftright);
            }
            else if (ExpressionComparer.Default.Equals(leftright, rightleft))
            {
                ObjectUtils.Swap(ref rightleft, ref rightright);
            }
            else if (ExpressionComparer.Default.Equals(leftright, rightright))
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

            // Attempt to unify or swap factors
            if ((!leftAugmented && !rightAugmented)
                && ExpressionComparer.Default.Equals(leftleft, rightleft)
                && !ExpressionComparer.Default.Equals(leftright, rightright))
            {
                ObjectUtils.Swap(ref leftleft, ref leftright);
                ObjectUtils.Swap(ref rightleft, ref rightright);
            }
            else if (ExpressionComparer.Default.Equals(leftleft, rightright))
            {
                ObjectUtils.Swap(ref leftleft, ref leftright);
            }
            else if (ExpressionComparer.Default.Equals(leftright, rightleft))
            {
                ObjectUtils.Swap(ref rightleft, ref rightright);
            }
            else if (ExpressionComparer.Default.Equals(leftright, rightright))
            {
                // do nothing
            }
            else
            {
                return null;
            }

            if (ExpressionComparer.Default.Equals(leftleft, rightleft))
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
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            return Expression.Constant((object)((dynamic)left.Value * (dynamic)right.Value));
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

            if (right.Left is ConstantExpression rightLeftConst)
            {
                return Expression.Multiply(
                    Expression.Constant((object)((dynamic)left.Value * (dynamic)rightLeftConst.Value)),
                    right.Right
                );
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

            if (left.Left is ConstantExpression leftLeft && right.Left is ConstantExpression rightLeft)
            {
                return Expression.Multiply(
                    Expression.Constant((object)((dynamic)leftLeft.Value * (dynamic)rightLeft.Value)),
                    TransformCore(Expression.Multiply(left.Right, right.Right))
                );
            }
            return null;
        }

        /// <summary>
        /// Simplifies constant division <c>(a / b)</c> when both are numeric constants.
        /// </summary>
        [ExpressionSignature(ExpressionType.Divide)]
        protected Expression DivisionOfConstants(
            BinaryExpression e,
            [ConstantNumeric] ConstantExpression left,
            [ConstantNumeric] ConstantExpression right)
        {
            if (!IsOrdinaryBinaryArithmetic(e)) return null;
            return Expression.Constant((object)((dynamic)left.Value / (dynamic)right.Value));
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

            if (ExpressionComparer.Default.Equals(leftleft, rightleft))
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
        private Expression CanonicalizeAdditiveExpression(ExpressionType nodeType, Expression left, Expression right)
        {
            var terms = new List<(Expression Term, bool IsNegative)>();
            CollectAdditiveTerms(terms, left, false);
            CollectAdditiveTerms(terms, right, nodeType == ExpressionType.Subtract);

            if (terms.Count == 0)
            {
                return Expression.Constant(Convert.ChangeType(0, left.Type), left.Type);
            }

            IReadOnlyList<ParameterExpression[]> scopes = CaptureLexicalScopeSnapshot();

            var annotatedTerms = new List<AnnotatedAdditiveTerm>(terms.Count);
            foreach ((Expression term, bool isNegative) in terms)
            {
                AdditiveGroupClass group = ClassifyForAdditiveGrouping(term);

                // Precompute every structural key CompareAdditiveGroupingOrder will need for this term
                // exactly once here (roadmap S5, P1), instead of letting the O(n log n) sort below rebuild
                // them from scratch on every pairwise comparison. An opaque term's own complete key (built
                // right below) IS ExpressionCanonicalOrder.BuildKey(group.Opaque, scopes) - group.Opaque is
                // this same term - so CompareAdditiveGroupingOrder reuses it directly instead of a second
                // ArgumentKeys array. A function-like term additionally needs each ARGUMENT's own key (the
                // primary sort's function-like branch orders by argument-list identity, not by the whole
                // term/exponent), computed once via BuildKeys so every argument shares one working scope list.
                //
                // group.Arguments/group.Opaque themselves are NOT retained on the annotation below (S5
                // review round 3): only the two scalar fields CompareAdditiveGroupingOrder actually still
                // needs post-annotation (IsFunctionLike, CategoryOrder) are copied out here, keeping
                // AnnotatedAdditiveTerm - copied repeatedly through OrderBy/GroupBy - smaller than carrying
                // the whole AdditiveGroupClass (with its two now-unused reference-type fields) would.
                IReadOnlyList<ExpressionCanonicalOrder.KeyNode>? argumentKeys = group.IsFunctionLike
                    ? ExpressionCanonicalOrder.BuildKeys(group.Arguments!, scopes)
                    : null;

                annotatedTerms.Add(new AnnotatedAdditiveTerm(
                    term,
                    isNegative,
                    group.IsFunctionLike,
                    group.CategoryOrder,
                    ExpressionCanonicalOrder.BuildKey(term, scopes),
                    argumentKeys));
            }

            var orderedTerms = annotatedTerms
                .OrderBy(static term => term, AdditiveGroupingOrderComparer)
                .ThenBy(static term => term.IsNegative ? 0 : 1)
                .ThenBy(static term => term.Key)
                .ToList();

            var rebuiltTerms = new List<Expression>();
            foreach (var termGroup in orderedTerms.GroupBy(static term => term.Term, AdditiveGroupingEqualityComparer.Instance))
            {
                var signedTerms = termGroup.Select(static term => (term.Term, term.IsNegative)).ToList();
                rebuiltTerms.Add(BuildAdditiveExpression(signedTerms, left.Type));
            }

            return BuildRightAssociative(rebuiltTerms, Expression.Add);
        }

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

            IReadOnlyList<ParameterExpression[]> scopes = CaptureLexicalScopeSnapshot();

            var orderedFactors = factors
                .OrderBy(factor => ExpressionCanonicalOrder.BuildKey(factor, scopes))
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
        /// <see cref="AdditiveGroupClass"/> value (which also carries <c>Arguments</c>/<c>Opaque"</c> - needed
        /// only transiently, while building <see cref="ArgumentKeys"/>/<see cref="Key"/> in the annotation
        /// loop, never afterward). Keeping this struct's per-element footprint - copied repeatedly through
        /// <c>List&lt;T&gt;</c>/<c>OrderBy</c>/<c>GroupBy</c> - as small as the fields actually still needed
        /// require is itself part of this stage's construction-cost goal.
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
        /// (8 bytes) far more cheaply. See the S5
        /// roadmap progress notes for the exact benchmark numbers.
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
