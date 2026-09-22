using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Utils.Objects;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// Provides a custom equality comparer for <see cref="Expression"/> objects.
/// The comparison involves simplifying each expression via an <see cref="ExpressionSimplifier"/>
/// before checking for structural and semantic equivalence.
/// </summary>
/// <remarks>
/// This comparer is a correctness primitive: <see cref="ExpressionSimplifier"/> uses it to decide whether
/// algebraic rewrites (common-term cancellation, power combination, trigonometric identities, ...) are
/// legal. A false positive here can silently change the mathematical result of a simplified expression, so
/// equality and hashing are implemented structurally, with explicit, scope-aware parameter binding for
/// lambda alpha-equivalence and an exact (not floating-point-approximate) numeric model for cross-type
/// numeric constants. See <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>, stage S1, for the
/// audit this hardening addresses and for remaining known limitations.
///
/// Only the node kinds already recognized before this hardening participate in structural equality:
/// <see cref="LambdaExpression"/>, <see cref="ConstantExpression"/>, <see cref="ParameterExpression"/>,
/// <see cref="UnaryExpression"/>, <see cref="BinaryExpression"/>, <see cref="MethodCallExpression"/> and
/// <see cref="MemberExpression"/>. Two distinct instances of any other node kind (for example
/// <see cref="NewExpression"/> or <see cref="ConditionalExpression"/>) are conservatively treated as
/// unequal; broadening node coverage is left to a future S1 follow-up.
/// </remarks>
public class ExpressionComparer : IEqualityComparer<Expression>
{
    /// <summary>
    /// A shared <see cref="ExpressionSimplifier"/> instance used to simplify expressions
    /// before they are compared.
    /// </summary>
    private static readonly ExpressionSimplifier _expressionSimplifier = new ExpressionSimplifier();

    /// <summary>
    /// Prevents direct instantiation outside of this class. Use <see cref="Default"/> instead.
    /// </summary>
    private ExpressionComparer() { }

    /// <summary>
    /// Gets the default <see cref="ExpressionComparer"/> instance for global usage.
    /// </summary>
    public static ExpressionComparer Default { get; } = new ExpressionComparer();

    /// <summary>
    /// Determines whether two <see cref="Expression"/> objects are equal by simplifying
    /// and comparing them structurally.
    /// </summary>
    /// <param name="x">The first expression to compare.</param>
    /// <param name="y">The second expression to compare.</param>
    /// <returns>
    /// <see langword="true"/> if both expressions are considered equivalent after simplification;
    /// otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <see cref="ReferenceEquals(object?, object?)"/> is checked first, before either side is simplified.
    /// This both implements conventional null semantics (<c>null</c>/<c>null</c> is equal, exactly one
    /// <c>null</c> is not) and guarantees reflexivity for expression kinds the structural comparer does not
    /// understand: <see cref="ExpressionSimplifier"/> may rebuild an unsupported node into a new,
    /// no-longer-reference-equal instance, and the comparer itself does not claim structural equality for
    /// such kinds, so without this early check the very same source object could compare unequal to itself.
    /// Neither operand is ever passed to <see cref="ExpressionSimplifier.Simplify"/> while <see langword="null"/>.
    /// This top-level check is intentionally the <em>only</em> place a bare <see cref="ReferenceEquals(object?, object?)"/>
    /// shortcut is used: at this point <paramref name="x"/> and <paramref name="y"/> are being compared with
    /// themselves under a single, unambiguous binding context, so "same object" trivially means "same
    /// meaning". <see cref="EqualsCore"/>'s internal recursion deliberately has no equivalent shortcut,
    /// because a shared sub-expression object reached through two lambdas can carry two different meanings
    /// depending on how each lambda binds its parameters - see <see cref="EqualsCore"/>'s remarks.
    /// </remarks>
    public bool Equals(Expression? x, Expression? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        // The base ExpressionTransformer.PrepareLambda rebuilds every lambda it visits via the
        // type-inferring Expression.Lambda(Transform(le.Body), expressionParameters) overload, which
        // preserves neither the original TailCall flag (it always comes back false) nor a custom delegate
        // type (it always infers a Func<...>/Action<...>). The exact built-in ExpressionSimplifier no
        // longer has this gap: its RebuildLambdaExpression override (see the S1 reconstruction-fidelity
        // fix) preserves TailCall, Type and Name at every nesting depth, not only at the root. A derived
        // ExpressionSimplifier subclass that does not override RebuildLambdaExpression itself still keeps
        // the historical erasing behavior, and _expressionSimplifier above is always the exact built-in
        // type, so simplification inside this comparer never erases either value. Comparing both here, on
        // the two ORIGINAL, not-yet-simplified root expressions, remains useful anyway: it protects
        // root-level metadata even for an expression tree this comparer's structural walk does not
        // otherwise understand (see this method's XML remarks on the earlier ReferenceEquals check).
        if (x is LambdaExpression xRoot && y is LambdaExpression yRoot
            && (xRoot.TailCall != yRoot.TailCall || xRoot.Type != yRoot.Type))
        {
            return false;
        }

        Expression simplifiedX = _expressionSimplifier.Simplify(x);
        Expression simplifiedY = _expressionSimplifier.Simplify(y);

        return EqualsCore(simplifiedX, simplifiedY, new ParameterBindingContext());
    }

    /// <summary>
    /// Recursively compares two already-simplified expressions for structural equivalence, resolving
    /// <see cref="ParameterExpression"/> references through <paramref name="context"/> rather than by name.
    /// </summary>
    /// <param name="x">The first (already simplified) expression, or a null sub-expression such as a static call's receiver.</param>
    /// <param name="y">The second (already simplified) expression, or a null sub-expression.</param>
    /// <param name="context">The per-comparison-call parameter binding stack. Never shared across calls to the public <see cref="Equals(Expression?, Expression?)"/>.</param>
    /// <returns><see langword="true"/> if the two sub-expressions are structurally equivalent.</returns>
    /// <remarks>
    /// <see cref="ParameterExpression"/> is resolved through <paramref name="context"/>, never by a generic
    /// <see cref="ReferenceEquals(object?, object?)"/> shortcut: the very same <see cref="ParameterExpression"/>
    /// instance can be declared at different positions in two lambdas being compared (for example
    /// <c>(p, q) => p</c> versus <c>(q, p) => p</c>), and only its binding position, not object identity,
    /// determines which argument it means on each side. This is also why <em>no other</em> node kind here
    /// gets a bare <c>ReferenceEquals(x, y)</c> shortcut either: a larger shared sub-expression object (for
    /// example a <see cref="MemberExpression"/> that both lambdas use as their body) can still contain a
    /// <see cref="ParameterExpression"/> whose meaning differs between the two binding contexts, so treating
    /// the shared object as trivially equal to itself would skip exactly the check that matters. Recursing
    /// structurally even into identical shared objects is the only safe option; the one place object
    /// identity is a valid shortcut is the outermost, single-binding-context comparison in the public
    /// <see cref="Equals(Expression?, Expression?)"/>.
    /// </remarks>
    private static bool EqualsCore(Expression? x, Expression? y, ParameterBindingContext context)
    {
        if (x is null || y is null) return ReferenceEquals(x, y);
        if (x.NodeType != y.NodeType) return false;

        if (x is ParameterExpression xp && y is ParameterExpression yp)
        {
            return context.AreEquivalent(xp, yp);
        }

        if (x is ConstantExpression xc && y is ConstantExpression yc)
        {
            return ConstantsEqual(xc, yc, context.SafeConstantsOnly);
        }

        if (x.Type != y.Type) return false;

        return (x, y) switch
        {
            (LambdaExpression xl, LambdaExpression yl) => LambdasEqual(xl, yl, context),
            (UnaryExpression xu, UnaryExpression yu) => UnaryEqual(xu, yu, context),
            (BinaryExpression xb, BinaryExpression yb) => BinaryEqual(xb, yb, context),
            (MethodCallExpression xm, MethodCallExpression ym) => MethodCallsEqual(xm, ym, context),
            (MemberExpression xme, MemberExpression yme) => MemberEqual(xme, yme, context),

            // Unsupported node kind: conservatively unequal, even for a shared/reused instance - see this
            // method's remarks on why no ReferenceEquals shortcut is used here.
            _ => false,
        };
    }

    /// <summary>Compares two <see cref="UnaryExpression"/> nodes: operator method, lifting flags, then the operand.</summary>
    /// <param name="x">The first unary expression.</param>
    /// <param name="y">The second unary expression.</param>
    /// <param name="context">The active parameter binding context.</param>
    /// <returns><see langword="true"/> if the unary expressions are structurally equivalent.</returns>
    private static bool UnaryEqual(UnaryExpression x, UnaryExpression y, ParameterBindingContext context)
        => object.Equals(x.Method, y.Method)
            && x.IsLifted == y.IsLifted
            && x.IsLiftedToNull == y.IsLiftedToNull
            && EqualsCore(x.Operand, y.Operand, context);

    /// <summary>
    /// Compares two <see cref="BinaryExpression"/> nodes: operator method, lifting flags, then operands and
    /// the coalesce conversion lambda.
    /// </summary>
    /// <param name="x">The first binary expression.</param>
    /// <param name="y">The second binary expression.</param>
    /// <param name="context">The active parameter binding context.</param>
    /// <returns><see langword="true"/> if the binary expressions are structurally equivalent.</returns>
    /// <remarks>
    /// For an ordinary (non-lifted, predefined-operator) <see cref="ExpressionType.Add"/> or
    /// <see cref="ExpressionType.Multiply"/> node — see <see cref="ExpressionSimplifier.IsOrdinaryBinaryArithmetic(BinaryExpression)"/> —
    /// operands are also compared SWAPPED as a fallback when the positional comparison fails, since those
    /// operators are mathematically commutative. This matters specifically for two distinct FREE
    /// <see cref="ParameterExpression"/> operands (an expression not wrapped in an enclosing
    /// <see cref="LambdaExpression"/>): <see cref="ExpressionSimplifier"/>'s stage-S4 canonicalization
    /// deliberately does not invent a cross-tree order between two such parameters (see the roadmap's "Free
    /// parameters" policy), so <c>Simplify(Add(a, b))</c> and <c>Simplify(Add(b, a))</c> can legitimately
    /// keep their different source operand order — without this commutative fallback, this comparer would
    /// then report <c>Equals(Add(a, b), Add(b, a))</c> as <see langword="false"/> for such free parameters,
    /// a regression from this comparer's pre-S4 observable behavior (a real bug this narrowly-scoped
    /// correction fixes, per the S4 roadmap entry's own carve-out for one). Every already-supported case
    /// (bound parameters, which S4 keeps canonicalizing deterministically by position; non-commutative
    /// operators; custom/lifted operators, excluded by <c>IsOrdinaryBinaryArithmetic</c>) is unaffected: the
    /// positional comparison already succeeds for those, so the swapped fallback is never reached.
    /// <para>
    /// <b>Public compatibility decision (S4 review, round 4).</b> This IS a deliberate, accepted widening of
    /// this type's public <see cref="Equals(Expression?, Expression?)"/>/<see cref="GetHashCode(Expression)"/>
    /// contract, not only an internal S4 correctness fix: two distinct FREE parameters sharing the same
    /// <see cref="ParameterExpression.Name"/> (e.g. both literally named <c>"v"</c>) previously compared
    /// unequal under <c>Add</c>/swapped-<c>Add</c> (the pre-S4 textual key tied on identical name text, the
    /// then-positional-only comparison found the operands reference-unequal); this fallback now matches
    /// them, and any external caller using <see cref="Default"/> as a <see cref="Dictionary{TKey, TValue}"/>/
    /// <see cref="HashSet{T}"/> key comparer for <see cref="Expression"/> trees containing such same-named
    /// distinct free parameters observes that change. The project's batched-2.0.0 versioning plan (see
    /// <c>Utils/TODO-2026-09-12-expression-simplifier-roadmap.md</c>) treats this kind of accumulating,
    /// individually-reasoned public-behavior change as expected between major releases rather than a
    /// per-change compatibility blocker; reverting to the old, narrower behavior is not adopted here because
    /// it was itself an accidental side effect of the pre-S4 textual-key implementation (ordinary commutative
    /// addition failing to compare two of its own re-orderings equal, purely because of how two UNRELATED
    /// operands happened to be spelled) rather than a deliberately designed contract, and reintroducing it
    /// would reintroduce that same non-commutativity inconsistency rather than removing one.
    /// </para>
    /// <para>
    /// <b>Explicit scope boundary (S4 review, round 5): NOT a general n-ary restoration.</b> This fallback
    /// swaps only the two operands of a SINGLE <see cref="BinaryExpression"/> node; it is not a general
    /// associative-commutative MULTISET match over an entire chain of ordinary <c>Add</c>/<c>Multiply</c>
    /// nodes. Before S4, three (or more) free parameters chained through ordinary addition/multiplication
    /// converged to one canonical order via the old <see cref="ParameterExpression.Name"/>-based textual
    /// sort regardless of source association - e.g. <c>Simplify((a+b)+c)</c> and <c>Simplify((c+b)+a)</c>
    /// both produced the same tree, so this comparer trivially agreed on them too. Post-S4, each side keeps
    /// its own source association/order instead (see the "Free parameters" policy referenced above), and
    /// this two-operand fallback does not bridge a 3+-term permutation: at the root, comparing
    /// <c>(a+b)+c</c> against <c>(c+b)+a</c> either positionally or swapped requires an <c>Add</c> subtree
    /// to match a bare parameter, which fails structurally either way, so
    /// <c>Equals((a+b)+c, (c+b)+a)</c> is <see langword="false"/> for three distinct free parameters even
    /// though they are algebraically the same sum. Recognizing this would require comparing/hashing such
    /// chains as associative-commutative collections of terms - a materially larger capability than this
    /// narrow regression fix, deliberately NOT attempted here; see the roadmap's round 5 decision.
    /// Characterized (not merely left unspecified) by
    /// <c>ExpressionComparerTests.FreeParameters_ThreeTermAdditionPermutation_PreservesPreS4ComparerBehavior</c>
    /// and its multiplicative counterpart.
    /// </para>
    /// </remarks>
    private static bool BinaryEqual(BinaryExpression x, BinaryExpression y, ParameterBindingContext context)
    {
        if (!object.Equals(x.Method, y.Method) || x.IsLifted != y.IsLifted || x.IsLiftedToNull != y.IsLiftedToNull)
        {
            return false;
        }

        if (EqualsCore(x.Left, y.Left, context) && EqualsCore(x.Right, y.Right, context) && EqualsCore(x.Conversion, y.Conversion, context))
        {
            return true;
        }

        return IsCommutative(x)
            && IsCommutative(y)
            && EqualsCore(x.Left, y.Right, context)
            && EqualsCore(x.Right, y.Left, context)
            && EqualsCore(x.Conversion, y.Conversion, context);
    }

    /// <summary>Whether <paramref name="binary"/> is an ordinary <see cref="ExpressionType.Add"/> or <see cref="ExpressionType.Multiply"/> node, and therefore mathematically commutative.</summary>
    /// <param name="binary">The binary expression to classify.</param>
    /// <returns><see langword="true"/> if <paramref name="binary"/>'s operands may be compared in either order.</returns>
    private static bool IsCommutative(BinaryExpression binary)
        => (binary.NodeType == ExpressionType.Add || binary.NodeType == ExpressionType.Multiply)
            && ExpressionSimplifier.IsOrdinaryBinaryArithmetic(binary);

    /// <summary>Compares two <see cref="MemberExpression"/> nodes: member identity, then the receiver.</summary>
    /// <param name="x">The first member access.</param>
    /// <param name="y">The second member access.</param>
    /// <param name="context">The active parameter binding context.</param>
    /// <returns><see langword="true"/> if the member accesses are structurally equivalent.</returns>
    private static bool MemberEqual(MemberExpression x, MemberExpression y, ParameterBindingContext context)
        => object.Equals(x.Member, y.Member) && EqualsCore(x.Expression, y.Expression, context);

    /// <summary>Compares two <see cref="LambdaExpression"/> nodes: metadata, parameter types (positionally), then the body under a pushed binding scope.</summary>
    /// <param name="x">The first lambda.</param>
    /// <param name="y">The second lambda.</param>
    /// <param name="context">The active parameter binding context.</param>
    /// <returns><see langword="true"/> if the lambdas are alpha-equivalent.</returns>
    /// <remarks>
    /// <see cref="LambdaExpression.Type"/> (the delegate type) is already required equal by the caller's
    /// blanket <c>x.Type != y.Type</c> check. <see cref="LambdaExpression.Name"/> is debug metadata and
    /// deliberately excluded from equality, as documented in the S1 roadmap entry for this PR. Since the S1
    /// reconstruction-fidelity fix, the exact built-in <see cref="ExpressionSimplifier"/> - the only runtime
    /// type <see cref="_expressionSimplifier"/> ever is - preserves <see cref="LambdaExpression.TailCall"/>
    /// and <see cref="LambdaExpression.Type"/> for every lambda it rebuilds, at every nesting depth, not
    /// only at the root: both checks here are therefore live, useful comparisons for a NESTED lambda too, not
    /// merely defense in depth. A derived <see cref="ExpressionSimplifier"/> subclass that does not override
    /// <c>RebuildLambdaExpression</c> still erases both at every depth below the root, exactly like the
    /// historical <c>ExpressionTransformer.PrepareLambda</c> behavior; the root level stays protected for
    /// such a subclass too, via <see cref="Equals(Expression?, Expression?)"/> comparing the original,
    /// not-yet-simplified lambdas before this method ever runs - but this comparer always simplifies through
    /// <see cref="_expressionSimplifier"/> (the exact built-in type), so that fallback is not exercised here.
    /// </remarks>
    private static bool LambdasEqual(LambdaExpression x, LambdaExpression y, ParameterBindingContext context)
    {
        if (x.TailCall != y.TailCall) return false;

        var xParameters = x.Parameters;
        var yParameters = y.Parameters;
        if (xParameters.Count != yParameters.Count) return false;

        for (int i = 0; i < xParameters.Count; i++)
        {
            if (xParameters[i].Type != yParameters[i].Type) return false;
        }

        context.PushScope(xParameters, yParameters);
        try
        {
            return EqualsCore(x.Body, y.Body, context);
        }
        finally
        {
            context.PopScope(xParameters.Count);
        }
    }

    /// <summary>Compares two <see cref="MethodCallExpression"/> nodes: method identity, argument count, receiver, then arguments in order.</summary>
    /// <param name="x">The first method call.</param>
    /// <param name="y">The second method call.</param>
    /// <param name="context">The active parameter binding context.</param>
    /// <returns><see langword="true"/> if the calls are structurally equivalent.</returns>
    private static bool MethodCallsEqual(MethodCallExpression x, MethodCallExpression y, ParameterBindingContext context)
    {
        if (!object.Equals(x.Method, y.Method)) return false;
        if (x.Arguments.Count != y.Arguments.Count) return false;
        if (!EqualsCore(x.Object, y.Object, context)) return false;

        for (int i = 0; i < x.Arguments.Count; i++)
        {
            if (!EqualsCore(x.Arguments[i], y.Arguments[i], context)) return false;
        }

        return true;
    }

    /// <summary>
    /// Whether <paramref name="value"/>'s runtime type is one whose <see cref="object.Equals(object?)"/> and
    /// <see cref="object.GetHashCode"/> — and, for <see cref="bool"/>/<see cref="char"/>/<see cref="Enum"/>
    /// specifically, whose <see cref="IComparable"/> — implementations are fixed, non-user-overridable BCL
    /// behavior, never culture-dependent. Shared between this comparer's <c>safeConstantsOnly</c> policy
    /// (<see cref="ConstantsEqual"/>/<see cref="HashConstant"/>) and
    /// <c>ExpressionCanonicalOrder.ConstantKey</c>'s structural ORDER comparison, so both agree on exactly
    /// the same "known safe to compare directly" set instead of drifting independently. <see cref="string"/>
    /// is included here (its default <see cref="object.Equals(object?)"/>/<see cref="object.GetHashCode"/>
    /// are ordinal, not culture-aware — only its default <see cref="IComparable{T}"/> is culture-aware, which
    /// is why the order side uses <see cref="string.CompareOrdinal(string?, string?)"/> explicitly rather
    /// than calling this method for ordering). Every other reference/value type (including a struct that
    /// implements <see cref="IComparable"/>) is treated as opaque: this method deliberately does not attempt
    /// to enumerate every "probably safe" framework type, only the small set the S4 roadmap entry names.
    /// </summary>
    /// <param name="value">The boxed constant value to classify; never <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/>'s type is in the known-safe set.</returns>
    internal static bool IsKnownSafeConstantValue(object value) => value is string or bool or char or Enum;

    /// <summary>
    /// Compares two <see cref="ConstantExpression"/> nodes, using an exact cross-type numeric model when
    /// both are native numeric constants, and null-safe/type-safe value comparison otherwise.
    /// </summary>
    /// <param name="x">The first constant.</param>
    /// <param name="y">The second constant.</param>
    /// <param name="safeConstantsOnly">
    /// When <see langword="true"/>, a non-numeric constant whose value is NOT a known-safe type (see
    /// <see cref="IsKnownSafeConstantValue(object)"/>) is compared by reference identity of its boxed
    /// <see cref="ConstantExpression.Value"/> instead of by calling the value's own possibly user-defined
    /// <see cref="object.Equals(object?)"/> override — see <see cref="ParameterBindingContext.SafeConstantsOnly"/>.
    /// A known-safe value (<see cref="string"/>, <see cref="bool"/>, <see cref="char"/>, <see cref="Enum"/>)
    /// still compares by value even when <paramref name="safeConstantsOnly"/> is <see langword="true"/>,
    /// since its <see cref="object.Equals(object?)"/> is known-safe to call.
    /// </param>
    /// <returns><see langword="true"/> if the constants represent the same value.</returns>
    private static bool ConstantsEqual(ConstantExpression x, ConstantExpression y, bool safeConstantsOnly)
    {
        if (TryGetExactNumericValue(x, out ExactNumericValue xn) && TryGetExactNumericValue(y, out ExactNumericValue yn))
        {
            return xn.Equals(yn);
        }

        if (x.Type != y.Type) return false;
        if (x.Value is null || y.Value is null) return x.Value is null && y.Value is null;

        if (safeConstantsOnly && !IsKnownSafeConstantValue(x.Value))
        {
            return ReferenceEquals(x.Value, y.Value);
        }

        return x.Value.Equals(y.Value);
    }

    /// <summary>Attempts to build an exact rational/NaN/infinity key for a constant whose declared <see cref="Expression.Type"/> is one of the native numeric types in <see cref="Types.Number"/>.</summary>
    /// <param name="constant">The constant to inspect.</param>
    /// <param name="value">The resulting exact numeric value, when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="constant"/>'s declared type is a native numeric type.</returns>
    private static bool TryGetExactNumericValue(ConstantExpression constant, out ExactNumericValue value)
    {
        if (!Types.Number.Contains(constant.Type))
        {
            value = default;
            return false;
        }

        value = ExactNumericValue.FromBoxed(constant.Type, constant.Value!);
        return true;
    }

    /// <summary>
    /// Structurally compares two expressions exactly like <see cref="Equals(Expression?, Expression?)"/>,
    /// EXCEPT that neither operand is ever passed through <see cref="ExpressionSimplifier.Simplify(Expression)"/>
    /// first, and no top-level <see cref="ReferenceEquals(object?, object?)"/> shortcut is applied.
    /// </summary>
    /// <remarks>
    /// This is the "raw, already-simplified expression" helper the S4 structural canonicalization key
    /// (<c>ExpressionSimplifier</c>'s additive grouping) shares with this comparer, per the roadmap's
    /// explicit instruction not to call the public, simplifying <see cref="Equals(Expression?, Expression?)"/>
    /// from inside canonical-key generation: <see cref="ExpressionSimplifier"/> already calls this method
    /// from deep inside its own <c>Simplify</c> recursion (building an additive-grouping key while
    /// finalizing an <c>Add</c>/<c>Subtract</c> node), and re-entering <see cref="ExpressionSimplifier.Simplify(Expression)"/>
    /// there would recursively re-simplify an expression that is already mid-simplification. Both operands
    /// passed here are therefore expected to already be simplified (or otherwise already in the exact
    /// structural shape the caller wants compared) — this method performs no simplification of its own.
    /// A free <see cref="ParameterExpression"/> (not bound by a <see cref="LambdaExpression"/> encountered
    /// during this call's own recursive descent) is compared by reference, exactly like
    /// <see cref="Equals(Expression?, Expression?)"/>'s own free-parameter policy: <paramref name="x"/> and
    /// <paramref name="y"/> are expected to be sibling sub-expressions of the same original tree, so a
    /// parameter free relative to both sides that is actually bound by a still-enclosing lambda resolves
    /// correctly via that shared reference, without this method needing to know about that enclosing scope.
    /// <para>
    /// A non-numeric <see cref="ConstantExpression"/> whose value is NOT a known-safe type (see
    /// <see cref="IsKnownSafeConstantValue(object)"/>: <see cref="string"/>, <see cref="bool"/>,
    /// <see cref="char"/>, <see cref="Enum"/>) is compared by REFERENCE identity of its boxed
    /// <see cref="ConstantExpression.Value"/>, never by calling the value's own possibly user-defined
    /// <see cref="object.Equals(object?)"/> — see <see cref="ParameterBindingContext.SafeConstantsOnly"/>. A
    /// known-safe value still compares by value, since calling its <see cref="object.Equals(object?)"/> is
    /// known not to execute user code. This is a deliberate, narrower policy than the public
    /// <see cref="Equals(Expression?, Expression?)"/> uses only for the opaque case: this raw entry point's
    /// only caller, <see cref="ExpressionSimplifier"/>'s additive-grouping equality, runs automatically as
    /// part of ordinary <c>Simplify()</c> calls, so it must never execute arbitrary user code (which an
    /// opaque constant's own <see cref="object.Equals(object?)"/>/<see cref="object.GetHashCode"/> override
    /// could contain, with side effects or a thrown exception) merely to decide whether two constant terms
    /// belong in the same additive group.
    /// </para>
    /// </remarks>
    /// <param name="x">The first (already simplified, or otherwise final) expression to compare.</param>
    /// <param name="y">The second (already simplified, or otherwise final) expression to compare.</param>
    /// <returns><see langword="true"/> if the two sub-expressions are structurally equivalent.</returns>
    internal static bool StructuralEqualsRaw(Expression? x, Expression? y) => EqualsCore(x, y, new ParameterBindingContext(safeConstantsOnly: true));

    /// <summary>
    /// Structurally hashes an expression exactly like <see cref="GetHashCode(Expression)"/>, EXCEPT that
    /// <paramref name="e"/> is never passed through <see cref="ExpressionSimplifier.Simplify(Expression)"/>
    /// first. See <see cref="StructuralEqualsRaw(Expression?, Expression?)"/>'s remarks for why this raw
    /// entry point exists and is consistent with it (a hash mirroring an equality contract must agree with
    /// that exact equality, not with the simplifying public <see cref="Equals(Expression?, Expression?)"/>).
    /// </summary>
    /// <param name="e">The (already simplified, or otherwise final) expression to hash, or <see langword="null"/>.</param>
    /// <returns>A hash code consistent with <see cref="StructuralEqualsRaw(Expression?, Expression?)"/>.</returns>
    internal static int StructuralHashRaw(Expression? e) => Hash(e, new ParameterScopeStack(safeConstantsOnly: true));

    /// <summary>
    /// Returns a hash code for the specified <see cref="Expression"/> by simplifying it and computing a
    /// structural hash that mirrors <see cref="Equals(Expression?, Expression?)"/>: alpha-equivalent
    /// lambdas and exact cross-type numeric constants hash identically, satisfying the
    /// <see cref="IEqualityComparer{T}"/> contract that equal objects have equal hash codes.
    /// </summary>
    /// <param name="obj">The <see cref="Expression"/> for which to get a hash code.</param>
    /// <returns>An integer hash code consistent with <see cref="Equals(Expression?, Expression?)"/>.</returns>
    public int GetHashCode(Expression obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        Expression simplified = _expressionSimplifier.Simplify(obj);
        return Hash(simplified, new ParameterScopeStack());
    }

    /// <summary>Computes a structural hash for <paramref name="e"/>, mirroring the metadata inspected by <see cref="EqualsCore"/>.</summary>
    /// <param name="e">The (already simplified) expression to hash, or <see langword="null"/> for an absent optional sub-expression.</param>
    /// <param name="scopes">The active lambda parameter scope stack for the single tree being hashed.</param>
    /// <returns>A hash code such that structurally/alpha-equivalent expressions produce the same value.</returns>
    private static int Hash(Expression? e, ParameterScopeStack scopes) => e switch
    {
        null => 0,
        LambdaExpression le => HashLambda(le, scopes),
        ParameterExpression pe => HashParameter(pe, scopes),
        ConstantExpression ce => HashConstant(ce, scopes.SafeConstantsOnly),
        UnaryExpression ue => HashUnary(ue, scopes),
        BinaryExpression be => HashBinary(be, scopes),
        MethodCallExpression mce => HashMethodCall(mce, scopes),
        MemberExpression me => HashMember(me, scopes),
        _ => HashUnsupported(e),
    };

    /// <summary>Hashes a <see cref="LambdaExpression"/>: metadata, parameter types, then the body under a pushed scope.</summary>
    /// <param name="le">The lambda to hash.</param>
    /// <param name="scopes">The active parameter scope stack.</param>
    /// <returns>A hash code mirroring the metadata <see cref="LambdasEqual"/> compares.</returns>
    private static int HashLambda(LambdaExpression le, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(ExpressionType.Lambda);
        hc.Add(le.Type);
        hc.Add(le.TailCall);
        hc.Add(le.Parameters.Count);
        foreach (var p in le.Parameters) hc.Add(p.Type);

        scopes.Push(le.Parameters);
        hc.Add(Hash(le.Body, scopes));
        scopes.Pop();

        return hc.ToHashCode();
    }

    /// <summary>Hashes a <see cref="ParameterExpression"/> by its relative binding depth and declaration position when bound, or by reference identity when free.</summary>
    /// <param name="pe">The parameter to hash.</param>
    /// <param name="scopes">The active parameter scope stack.</param>
    /// <returns>A hash code such that alpha-equivalent bound parameters, or the same free parameter, produce the same value.</returns>
    private static int HashParameter(ParameterExpression pe, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(ExpressionType.Parameter);
        if (scopes.TryLocate(pe, out int depth, out int position))
        {
            hc.Add(depth);
            hc.Add(position);
        }
        else
        {
            hc.Add(RuntimeHelpers.GetHashCode(pe));
        }
        hc.Add(pe.Type);

        return hc.ToHashCode();
    }

    /// <summary>Hashes a <see cref="ConstantExpression"/>, using the exact numeric key (without the original CLR type) for native numeric constants, and <see cref="Expression.Type"/> plus a value-derived hash otherwise.</summary>
    /// <param name="ce">The constant to hash.</param>
    /// <param name="safeConstantsOnly">
    /// When <see langword="true"/>, a non-numeric constant whose value is NOT a known-safe type (see
    /// <see cref="IsKnownSafeConstantValue(object)"/>) is hashed by
    /// <see cref="RuntimeHelpers.GetHashCode(object?)"/> (reference identity) instead of the value's own
    /// possibly user-defined <see cref="object.GetHashCode"/> override, mirroring
    /// <see cref="ConstantsEqual"/>'s <c>safeConstantsOnly</c> policy so equal keys still hash equally. A
    /// known-safe value (<see cref="string"/>, <see cref="bool"/>, <see cref="char"/>, <see cref="Enum"/>)
    /// still hashes by value even when <paramref name="safeConstantsOnly"/> is <see langword="true"/>. This
    /// use of <see cref="RuntimeHelpers.GetHashCode(object?)"/> is only ever a <c>Dictionary</c>/<c>GroupBy</c>
    /// lookup accelerator (see <see cref="StructuralHashRaw(Expression?)"/>'s caller,
    /// <see cref="ExpressionSimplifier"/>'s additive grouping) — never the final canonical ORDER, which the
    /// roadmap requires to never be hash-based.
    /// </param>
    /// <returns>A hash code mirroring the value comparison <see cref="ConstantsEqual"/> performs.</returns>
    private static int HashConstant(ConstantExpression ce, bool safeConstantsOnly)
    {
        var hc = new HashCode();
        hc.Add(ExpressionType.Constant);
        if (TryGetExactNumericValue(ce, out ExactNumericValue numeric))
        {
            hc.Add(true);
            hc.Add(numeric);
        }
        else
        {
            hc.Add(false);
            hc.Add(ce.Type);
            hc.Add(ce.Value switch
            {
                null => 0,
                var value when safeConstantsOnly && !IsKnownSafeConstantValue(value) => RuntimeHelpers.GetHashCode(value),
                var value => value.GetHashCode(),
            });
        }

        return hc.ToHashCode();
    }

    /// <summary>Hashes a <see cref="UnaryExpression"/>: node type, result type, operator method, lifting flags, then the operand.</summary>
    /// <param name="ue">The unary expression to hash.</param>
    /// <param name="scopes">The active parameter scope stack.</param>
    /// <returns>A hash code mirroring the metadata <see cref="EqualsCore"/>'s unary case compares.</returns>
    private static int HashUnary(UnaryExpression ue, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(ue.NodeType);
        hc.Add(ue.Type);
        hc.Add(ue.Method);
        hc.Add(ue.IsLifted);
        hc.Add(ue.IsLiftedToNull);
        hc.Add(Hash(ue.Operand, scopes));

        return hc.ToHashCode();
    }

    /// <summary>Hashes a <see cref="BinaryExpression"/>: node type, result type, operator method, lifting flags, operands, then the coalesce conversion lambda.</summary>
    /// <param name="be">The binary expression to hash.</param>
    /// <param name="scopes">The active parameter scope stack.</param>
    /// <returns>A hash code mirroring the metadata <see cref="EqualsCore"/>'s binary case (including its commutative fallback, see <see cref="IsCommutative(BinaryExpression)"/>) compares.</returns>
    private static int HashBinary(BinaryExpression be, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(be.NodeType);
        hc.Add(be.Type);
        hc.Add(be.Method);
        hc.Add(be.IsLifted);
        hc.Add(be.IsLiftedToNull);

        int leftHash = Hash(be.Left, scopes);
        int rightHash = Hash(be.Right, scopes);
        if (IsCommutative(be))
        {
            // Order-independent combination: BinaryEqual accepts a swapped-operand match for an ordinary
            // Add/Multiply node, so two such nodes it considers equal (e.g. Add(a, b) and Add(b, a) for two
            // distinct free parameters) must also hash identically regardless of which operand is Left vs
            // Right, per the IEqualityComparer<T> "equal objects have equal hash codes" contract.
            hc.Add(unchecked(leftHash + rightHash));
        }
        else
        {
            hc.Add(leftHash);
            hc.Add(rightHash);
        }

        hc.Add(Hash(be.Conversion, scopes));

        return hc.ToHashCode();
    }

    /// <summary>Hashes a <see cref="MethodCallExpression"/>: method identity, receiver, then arguments in order.</summary>
    /// <param name="mce">The method call to hash.</param>
    /// <param name="scopes">The active parameter scope stack.</param>
    /// <returns>A hash code mirroring the metadata <see cref="MethodCallsEqual"/> compares.</returns>
    private static int HashMethodCall(MethodCallExpression mce, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(ExpressionType.Call);
        hc.Add(mce.Type);
        hc.Add(mce.Method);
        hc.Add(Hash(mce.Object, scopes));
        hc.Add(mce.Arguments.Count);
        foreach (var a in mce.Arguments) hc.Add(Hash(a, scopes));

        return hc.ToHashCode();
    }

    /// <summary>Hashes a <see cref="MemberExpression"/>: member identity, then the receiver.</summary>
    /// <param name="me">The member access to hash.</param>
    /// <param name="scopes">The active parameter scope stack.</param>
    /// <returns>A hash code mirroring the metadata <see cref="EqualsCore"/>'s member case compares.</returns>
    private static int HashMember(MemberExpression me, ParameterScopeStack scopes)
    {
        var hc = new HashCode();
        hc.Add(ExpressionType.MemberAccess);
        hc.Add(me.Type);
        hc.Add(me.Member);
        hc.Add(Hash(me.Expression, scopes));

        return hc.ToHashCode();
    }

    /// <summary>Computes a coarse, conservative hash for an unsupported node kind: collisions between distinct unequal nodes are acceptable, but this must never disagree with <see cref="EqualsCore"/>'s "false" for that node kind.</summary>
    /// <param name="e">The unsupported expression to hash.</param>
    /// <returns>A hash code based only on <see cref="Expression.NodeType"/> and <see cref="Expression.Type"/>.</returns>
    private static int HashUnsupported(Expression e) => HashCode.Combine(e.NodeType, e.Type);

    /// <summary>
    /// A per-<see cref="Equals(Expression?, Expression?)"/>-call, mutable stack of paired
    /// (<c>left</c>, <c>right</c>) <see cref="ParameterExpression"/> bindings, used to resolve lambda
    /// alpha-equivalence by declaration position/instance rather than by <see cref="ParameterExpression.Name"/>.
    /// Never stored on <see cref="ExpressionComparer.Default"/>, so concurrent comparisons never interfere.
    /// </summary>
    private sealed class ParameterBindingContext
    {
        private readonly List<(ParameterExpression Left, ParameterExpression Right)> _bindings = [];

        /// <summary>
        /// When <see langword="true"/>, a non-numeric <see cref="ConstantExpression"/> compared through
        /// this context is compared by reference identity of its boxed value instead of the value's own
        /// possibly user-defined <see cref="object.Equals(object?)"/> override. Used only by
        /// <see cref="StructuralEqualsRaw(Expression?, Expression?)"/>: the public, simplifying
        /// <see cref="Equals(Expression?, Expression?)"/> always uses <see langword="false"/>, preserving
        /// its historical behavior unchanged.
        /// </summary>
        public bool SafeConstantsOnly { get; }

        /// <summary>Initializes a new, empty <see cref="ParameterBindingContext"/>.</summary>
        /// <param name="safeConstantsOnly">The value of <see cref="SafeConstantsOnly"/>.</param>
        public ParameterBindingContext(bool safeConstantsOnly = false)
        {
            SafeConstantsOnly = safeConstantsOnly;
        }

        /// <summary>Pushes one binding pair per positionally-corresponding parameter when entering a lambda's body.</summary>
        /// <param name="leftParameters">The left lambda's parameters, in declaration order.</param>
        /// <param name="rightParameters">The right lambda's parameters, in declaration order; must have the same count as <paramref name="leftParameters"/>.</param>
        public void PushScope(IReadOnlyList<ParameterExpression> leftParameters, IReadOnlyList<ParameterExpression> rightParameters)
        {
            for (int i = 0; i < leftParameters.Count; i++)
            {
                _bindings.Add((leftParameters[i], rightParameters[i]));
            }
        }

        /// <summary>Removes the most recently pushed <paramref name="count"/> bindings when leaving a lambda's body.</summary>
        /// <param name="count">The number of bindings to remove; must match the count passed to the corresponding <see cref="PushScope"/> call.</param>
        public void PopScope(int count)
        {
            _bindings.RemoveRange(_bindings.Count - count, count);
        }

        /// <summary>
        /// Determines whether <paramref name="x"/> (from the left tree) and <paramref name="y"/> (from the
        /// right tree) refer to the same bound position, searching the most recently pushed bindings first
        /// so inner (shadowing) declarations win over outer ones.
        /// </summary>
        /// <param name="x">The left-hand parameter reference.</param>
        /// <param name="y">The right-hand parameter reference.</param>
        /// <returns>
        /// <see langword="true"/> if both are bound to exactly the same pair, or if neither is currently
        /// bound and they are the same free-parameter instance.
        /// </returns>
        public bool AreEquivalent(ParameterExpression x, ParameterExpression y)
        {
            int leftIndex = -1;
            int rightIndex = -1;
            for (int i = _bindings.Count - 1; i >= 0; i--)
            {
                if (leftIndex < 0 && ReferenceEquals(_bindings[i].Left, x)) leftIndex = i;
                if (rightIndex < 0 && ReferenceEquals(_bindings[i].Right, y)) rightIndex = i;
                if (leftIndex >= 0 && rightIndex >= 0) break;
            }

            if (leftIndex < 0 && rightIndex < 0) return ReferenceEquals(x, y);
            return leftIndex == rightIndex;
        }
    }

    /// <summary>
    /// A per-<see cref="GetHashCode(Expression)"/>-call stack of lambda parameter lists, used to hash a
    /// <see cref="ParameterExpression"/> by its relative binding depth and declaration position (a
    /// De Bruijn-style index) rather than by name or object identity, so alpha-equivalent expressions hash
    /// identically regardless of which <see cref="ParameterExpression"/> instances or names they use.
    /// </summary>
    private sealed class ParameterScopeStack
    {
        private readonly List<IReadOnlyList<ParameterExpression>> _scopes = [];

        /// <summary>Mirrors <see cref="ParameterBindingContext.SafeConstantsOnly"/> for the hashing side; see its remarks.</summary>
        public bool SafeConstantsOnly { get; }

        /// <summary>Initializes a new, empty <see cref="ParameterScopeStack"/>.</summary>
        /// <param name="safeConstantsOnly">The value of <see cref="SafeConstantsOnly"/>.</param>
        public ParameterScopeStack(bool safeConstantsOnly = false)
        {
            SafeConstantsOnly = safeConstantsOnly;
        }

        /// <summary>Pushes a lambda's parameter list when descending into its body.</summary>
        /// <param name="parameters">The lambda's parameters, in declaration order.</param>
        public void Push(IReadOnlyList<ParameterExpression> parameters) => _scopes.Add(parameters);

        /// <summary>Pops the innermost lambda's parameter list when leaving its body.</summary>
        public void Pop() => _scopes.RemoveAt(_scopes.Count - 1);

        /// <summary>Locates <paramref name="parameter"/> among the currently active scopes, innermost first.</summary>
        /// <param name="parameter">The parameter reference to locate.</param>
        /// <param name="relativeDepth">The number of enclosing scopes between the innermost scope and the one declaring <paramref name="parameter"/> (0 = innermost), when found.</param>
        /// <param name="position">The parameter's declaration position within its scope, when found.</param>
        /// <returns><see langword="true"/> if <paramref name="parameter"/> is bound in an active scope; <see langword="false"/> if it is a free parameter.</returns>
        public bool TryLocate(ParameterExpression parameter, out int relativeDepth, out int position)
        {
            for (int i = _scopes.Count - 1; i >= 0; i--)
            {
                var scope = _scopes[i];
                for (int j = 0; j < scope.Count; j++)
                {
                    if (ReferenceEquals(scope[j], parameter))
                    {
                        relativeDepth = _scopes.Count - 1 - i;
                        position = j;
                        return true;
                    }
                }
            }

            relativeDepth = -1;
            position = -1;
            return false;
        }
    }

    /// <summary>
    /// An exact representation of a native numeric constant's mathematical value, used to compare and hash
    /// numeric constants across different CLR numeric types without floating-point or decimal rounding.
    /// Finite values are stored as a normalized (reduced, positive-denominator) exact rational; special
    /// IEEE values collapse to one of three categories. Constructed directly from each type's exact bit
    /// representation (integer value, <see cref="decimal.GetBits(decimal)"/>, or IEEE 754 sign/exponent/
    /// mantissa decomposition) - never via <see cref="object.ToString"/> or a lossy round trip through
    /// another numeric type.
    /// </summary>
    internal readonly struct ExactNumericValue : IEquatable<ExactNumericValue>, IComparable<ExactNumericValue>
    {
        /// <summary>The category of an <see cref="ExactNumericValue"/>: an exact finite rational, or one of the three non-finite IEEE categories.</summary>
        private enum NumericKind
        {
            /// <summary>A finite value, stored as an exact <c>numerator / denominator</c> rational.</summary>
            Finite,

            /// <summary>Any not-a-number value; all NaN bit patterns collapse to this single category.</summary>
            NaN,

            /// <summary>Positive infinity.</summary>
            PositiveInfinity,

            /// <summary>Negative infinity.</summary>
            NegativeInfinity,
        }

        private readonly NumericKind _kind;
        private readonly BigInteger _numerator;
        private readonly BigInteger _denominator;

        /// <summary>Initializes an already-normalized exact numeric value. Use <see cref="Normalize"/> rather than calling this directly for a <see cref="NumericKind.Finite"/> value.</summary>
        /// <param name="kind">The value's category.</param>
        /// <param name="numerator">The exact numerator; meaningful only when <paramref name="kind"/> is <see cref="NumericKind.Finite"/>.</param>
        /// <param name="denominator">The exact, positive denominator; meaningful only when <paramref name="kind"/> is <see cref="NumericKind.Finite"/>.</param>
        private ExactNumericValue(NumericKind kind, BigInteger numerator, BigInteger denominator)
        {
            _kind = kind;
            _numerator = numerator;
            _denominator = denominator;
        }

        /// <summary>Builds the exact value of a boxed native numeric constant.</summary>
        /// <param name="type">The constant's declared CLR type; must be one of the types in <see cref="Types.Number"/>.</param>
        /// <param name="value">The boxed value.</param>
        /// <returns>The exact numeric representation of <paramref name="value"/>.</returns>
        public static ExactNumericValue FromBoxed(Type type, object value)
        {
            if (type == typeof(float)) return FromSingle((float)value);
            if (type == typeof(double)) return FromDouble((double)value);
            if (type == typeof(decimal)) return FromDecimal((decimal)value);
            if (type == typeof(byte)) return FromInteger((byte)value);
            if (type == typeof(ushort)) return FromInteger((ushort)value);
            if (type == typeof(uint)) return FromInteger((uint)value);
            if (type == typeof(ulong)) return FromInteger((ulong)value);
            if (type == typeof(sbyte)) return FromInteger((sbyte)value);
            if (type == typeof(short)) return FromInteger((short)value);
            if (type == typeof(int)) return FromInteger((int)value);
            if (type == typeof(long)) return FromInteger((long)value);

            throw new NotSupportedException($"Unsupported numeric constant type '{type}'.");
        }

        /// <summary>Builds the exact value of a native signed or unsigned integer, widened to <see cref="BigInteger"/> without loss.</summary>
        /// <param name="value">The integer value.</param>
        /// <returns>The exact value, as <c>value / 1</c>.</returns>
        private static ExactNumericValue FromInteger(BigInteger value) => Normalize(NumericKind.Finite, value, BigInteger.One);

        /// <summary>Builds the exact value of a <see cref="decimal"/> from its 96-bit significand, scale and sign, per <see cref="decimal.GetBits(decimal)"/>.</summary>
        /// <param name="value">The decimal value.</param>
        /// <returns>The exact value, as <c>±significand / 10^scale</c>.</returns>
        private static ExactNumericValue FromDecimal(decimal value)
        {
            int[] bits = decimal.GetBits(value);
            BigInteger significand = (BigInteger)(uint)bits[0]
                | ((BigInteger)(uint)bits[1] << 32)
                | ((BigInteger)(uint)bits[2] << 64);
            bool negative = (bits[3] & int.MinValue) != 0;
            int scale = (bits[3] >> 16) & 0xFF;

            BigInteger numerator = negative ? -significand : significand;
            BigInteger denominator = BigInteger.Pow(10, scale);
            return Normalize(NumericKind.Finite, numerator, denominator);
        }

        /// <summary>Builds the exact value of a <see cref="float"/> from its IEEE 754 sign/exponent/mantissa bits, handling subnormals; never via <see cref="object.ToString"/> or a round trip through another numeric type.</summary>
        /// <param name="value">The single-precision value.</param>
        /// <returns>The exact value, or a <see cref="NumericKind.NaN"/>/infinity category for non-finite input.</returns>
        private static ExactNumericValue FromSingle(float value)
        {
            if (float.IsNaN(value)) return new ExactNumericValue(NumericKind.NaN, default, default);
            if (float.IsPositiveInfinity(value)) return new ExactNumericValue(NumericKind.PositiveInfinity, default, default);
            if (float.IsNegativeInfinity(value)) return new ExactNumericValue(NumericKind.NegativeInfinity, default, default);

            int bits = BitConverter.SingleToInt32Bits(value);
            bool negative = bits < 0;
            int exponent = (bits >> 23) & 0xFF;
            int mantissa = bits & 0x7FFFFF;

            BigInteger significand;
            int binaryExponent;
            if (exponent == 0)
            {
                significand = mantissa;
                binaryExponent = -149; // subnormal: value = mantissa * 2^(1 - 127 - 23)
            }
            else
            {
                significand = mantissa | (1 << 23);
                binaryExponent = exponent - 127 - 23; // normal: value = (2^23 + mantissa) * 2^(exponent - 127 - 23)
            }

            if (negative) significand = -significand;
            return FromBinary(significand, binaryExponent);
        }

        /// <summary>Builds the exact value of a <see cref="double"/> from its IEEE 754 sign/exponent/mantissa bits, handling subnormals; never via <see cref="object.ToString"/> or a round trip through another numeric type.</summary>
        /// <param name="value">The double-precision value.</param>
        /// <returns>The exact value, or a <see cref="NumericKind.NaN"/>/infinity category for non-finite input.</returns>
        private static ExactNumericValue FromDouble(double value)
        {
            if (double.IsNaN(value)) return new ExactNumericValue(NumericKind.NaN, default, default);
            if (double.IsPositiveInfinity(value)) return new ExactNumericValue(NumericKind.PositiveInfinity, default, default);
            if (double.IsNegativeInfinity(value)) return new ExactNumericValue(NumericKind.NegativeInfinity, default, default);

            long bits = BitConverter.DoubleToInt64Bits(value);
            bool negative = bits < 0;
            int exponent = (int)((bits >> 52) & 0x7FF);
            long mantissa = bits & 0xFFFFFFFFFFFFFL;

            BigInteger significand;
            int binaryExponent;
            if (exponent == 0)
            {
                significand = mantissa;
                binaryExponent = -1074; // subnormal: value = mantissa * 2^(1 - 1023 - 52)
            }
            else
            {
                significand = mantissa | (1L << 52);
                binaryExponent = exponent - 1023 - 52; // normal: value = (2^52 + mantissa) * 2^(exponent - 1023 - 52)
            }

            if (negative) significand = -significand;
            return FromBinary(significand, binaryExponent);
        }

        /// <summary>Converts a <c>significand * 2^binaryExponent</c> pair (as produced by <see cref="FromSingle"/>/<see cref="FromDouble"/>) into a normalized exact rational.</summary>
        /// <param name="significand">The signed integer significand.</param>
        /// <param name="binaryExponent">The base-2 exponent applied to <paramref name="significand"/>; may be negative.</param>
        /// <returns>The normalized exact finite value.</returns>
        private static ExactNumericValue FromBinary(BigInteger significand, int binaryExponent)
        {
            if (binaryExponent >= 0)
            {
                return Normalize(NumericKind.Finite, significand * BigInteger.Pow(2, binaryExponent), BigInteger.One);
            }

            return Normalize(NumericKind.Finite, significand, BigInteger.Pow(2, -binaryExponent));
        }

        /// <summary>Reduces a <c>numerator / denominator</c> pair to its canonical form (zero collapses to <c>0/1</c>; otherwise divided by their greatest common divisor), so equal values always compare and hash identically.</summary>
        /// <param name="kind">The value's category; passed through unchanged.</param>
        /// <param name="numerator">The signed numerator before reduction.</param>
        /// <param name="denominator">The positive denominator before reduction.</param>
        /// <returns>The normalized exact numeric value.</returns>
        private static ExactNumericValue Normalize(NumericKind kind, BigInteger numerator, BigInteger denominator)
        {
            if (numerator.IsZero) return new ExactNumericValue(kind, BigInteger.Zero, BigInteger.One);

            BigInteger gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            return new ExactNumericValue(kind, numerator / gcd, denominator / gcd);
        }

        /// <inheritdoc/>
        public bool Equals(ExactNumericValue other)
        {
            if (_kind != other._kind) return false;
            return _kind != NumericKind.Finite || (_numerator == other._numerator && _denominator == other._denominator);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is ExactNumericValue other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
            => _kind == NumericKind.Finite ? HashCode.Combine(_kind, _numerator, _denominator) : HashCode.Combine(_kind);

        /// <summary>
        /// Deterministically orders exact numeric values: <see cref="NumericKind.NegativeInfinity"/> first,
        /// then <see cref="NumericKind.Finite"/> values by their exact rational value, then
        /// <see cref="NumericKind.PositiveInfinity"/>, then <see cref="NumericKind.NaN"/> last. Used by the
        /// S4 structural canonical-order key (<see cref="Utils.Mathematics.Expressions.ExpressionCanonicalOrder"/>)
        /// to order numeric <see cref="ConstantExpression"/> terms without ever formatting the value as text.
        /// </summary>
        /// <param name="other">The value to compare against.</param>
        /// <returns>A negative value if this value sorts before <paramref name="other"/>, zero if equal, positive otherwise.</returns>
        public int CompareTo(ExactNumericValue other)
        {
            int kindRank = KindRank(_kind);
            int otherKindRank = KindRank(other._kind);
            if (kindRank != otherKindRank) return kindRank.CompareTo(otherKindRank);
            if (_kind != NumericKind.Finite) return 0;

            // Denominators are always positive after Normalize, so cross-multiplication preserves order.
            BigInteger left = _numerator * other._denominator;
            BigInteger right = other._numerator * _denominator;
            return left.CompareTo(right);
        }

        /// <summary>Fixed ordering rank for each <see cref="NumericKind"/>, used by <see cref="CompareTo(ExactNumericValue)"/>: negative infinity first, then finite values (by exact rational value), then positive infinity, then NaN last.</summary>
        /// <param name="kind">The category to rank.</param>
        /// <returns>A fixed, deterministic integer rank.</returns>
        private static int KindRank(NumericKind kind) => kind switch
        {
            NumericKind.NegativeInfinity => 0,
            NumericKind.Finite => 1,
            NumericKind.PositiveInfinity => 2,
            NumericKind.NaN => 3,
            _ => 4,
        };
    }
}
