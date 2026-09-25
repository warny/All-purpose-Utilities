using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Utils.Collections;
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
/// parameter/return types, static/instance) compared ordinally, with a final tie-break
/// (<see cref="CompareFinalMemberTiebreak"/>) using assembly identity, then module name, then
/// <see cref="Module.ModuleVersionId"/>, then <see cref="MemberInfo.MetadataToken"/> for the case where
/// every other dimension ties - a real, reachable case (e.g. two different assembly builds/versions each
/// defining a type or member with an identical namespace/name/signature loaded side by side; see that
/// method's remarks), not merely a theoretical one. None of <see cref="object.GetHashCode"/>,
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
        var scopes = new ScopeState(enclosingScopes);
        return Build(expression, ref scopes);
    }

    /// <summary>
    /// Zero-copy counterpart of <see cref="BuildKey(Expression?, IReadOnlyList{ParameterExpression[]})"/> for a
    /// caller that already holds a trusted, exclusively-owned snapshot array - in practice, only
    /// <see cref="ExpressionSimplifier"/>'s own <c>CaptureLexicalScopeSnapshot</c> result. Overload resolution,
    /// not a runtime type check, is what selects this path: a caller must declare its variable as the concrete
    /// <see cref="ParameterExpression"/><c>[][]</c> array type to reach it, so an ordinary
    /// <see cref="IReadOnlyList{T}"/>-typed caller (including one that happens to hold an array at runtime)
    /// always goes through the defensive-copying overload above instead - see this class's <see cref="ScopeState"/>
    /// remarks for why a runtime-type check alone was not a safe way to make this distinction (PR #606 review
    /// round 2).
    /// </summary>
    /// <param name="expression">The (already simplified) expression to key, or <see langword="null"/>.</param>
    /// <param name="enclosingScopes">A snapshot array the caller guarantees nothing else can reach and mutate.</param>
    /// <returns>A comparable, deterministic structural key.</returns>
    internal static KeyNode BuildKeyFromSnapshot(Expression? expression, ParameterExpression[][] enclosingScopes)
    {
        var scopes = new ScopeState(enclosingScopes);
        return Build(expression, ref scopes);
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

    /// <summary>
    /// Builds the complete structural canonical-order key for each of <paramref name="expressions"/> under
    /// one shared enclosing-scope snapshot, sharing a single working scope list across every element instead
    /// of allocating and copying a fresh one per call (roadmap stage S5) — the caller is expected to build
    /// each key exactly once (for example, once per additive/multiplicative term at annotation time) and
    /// reuse the result across any later pairwise comparisons, rather than rebuilding it on every comparison.
    /// </summary>
    /// <param name="expressions">The (already simplified) sub-expressions to key, in order.</param>
    /// <param name="enclosingScopes">See <see cref="BuildKey(Expression, IReadOnlyList{ParameterExpression[]})"/>.</param>
    /// <returns>One comparable, deterministic structural key per element of <paramref name="expressions"/>, in the same order.</returns>
    /// <remarks>
    /// Fast-paths an empty <paramref name="expressions"/> (a niladic method call, e.g. <c>Guid.NewGuid()</c>)
    /// to <see cref="Array.Empty{T}"/> without allocating the working scope list at all: the pre-S5 baseline
    /// (<c>CompareArgumentLists</c> over an empty argument list) built zero argument keys for this shape too,
    /// so this call must not become new, unamortized work for it.
    /// </remarks>
    internal static KeyNode[] BuildKeys(IReadOnlyList<Expression> expressions, IReadOnlyList<ParameterExpression[]> enclosingScopes)
    {
        if (expressions.Count == 0)
        {
            return [];
        }

        var scopes = new ScopeState(enclosingScopes);
        return BuildKeysCore(expressions, ref scopes);
    }

    /// <summary>
    /// Zero-copy counterpart of <see cref="BuildKeys(IReadOnlyList{Expression}, IReadOnlyList{ParameterExpression[]})"/>
    /// for a caller that already holds a trusted, exclusively-owned snapshot array - see
    /// <see cref="BuildKeyFromSnapshot(Expression?, ParameterExpression[][])"/> for why overload resolution on the
    /// concrete array type, not a runtime check, is what gates this path.
    /// </summary>
    /// <param name="expressions">The (already simplified) sub-expressions to key, in order.</param>
    /// <param name="enclosingScopes">A snapshot array the caller guarantees nothing else can reach and mutate.</param>
    /// <returns>One comparable, deterministic structural key per element of <paramref name="expressions"/>, in the same order.</returns>
    internal static KeyNode[] BuildKeysFromSnapshot(IReadOnlyList<Expression> expressions, ParameterExpression[][] enclosingScopes)
    {
        if (expressions.Count == 0)
        {
            return [];
        }

        var scopes = new ScopeState(enclosingScopes);
        return BuildKeysCore(expressions, ref scopes);
    }

    /// <summary>Shared per-element loop backing both <see cref="BuildKeys"/> and <see cref="BuildKeysFromSnapshot"/> once their <see cref="ScopeState"/> has been constructed. Named distinctly from both (rather than overloaded) so reflection-based test lookups of <c>BuildKeys</c> by name alone stay unambiguous.</summary>
    /// <param name="expressions">The (already simplified), non-empty sub-expressions to key, in order.</param>
    /// <param name="scopes">The working scope state, shared across every element.</param>
    /// <returns>One comparable, deterministic structural key per element of <paramref name="expressions"/>, in the same order.</returns>
    private static KeyNode[] BuildKeysCore(IReadOnlyList<Expression> expressions, ref ScopeState scopes)
    {
        var keys = new KeyNode[expressions.Count];
        for (int i = 0; i < expressions.Count; i++)
        {
            keys[i] = Build(expressions[i], ref scopes);
        }

        return keys;
    }

    /// <summary>Dispatches to the node-family-specific <c>Build*</c> helper, or <see cref="UnsupportedKey"/> for any node kind not among the seven this class understands.</summary>
    /// <param name="e">The (already simplified) sub-expression to key, or <see langword="null"/>.</param>
    /// <param name="scopes">The local, per-<see cref="BuildKey(Expression, IReadOnlyList{ParameterExpression[]})"/>-call (or per-<see cref="BuildKeys"/>-call) scope-lookup state (ambient enclosing snapshot plus any lambda encountered so far during this walk). Threaded by <see langword="ref"/> so a lazily-allocated nested-scope frame pushed deeper in the recursion remains visible to the rest of the walk and to later sibling calls.</param>
    /// <returns>The resulting key.</returns>
    private static KeyNode Build(Expression? e, ref ScopeState scopes) => e switch
    {
        null => NullKey.Instance,
        ConstantExpression ce => BuildConstant(ce),
        ParameterExpression pe => BuildParameter(pe, ref scopes),
        UnaryExpression ue => BuildUnary(ue, ref scopes),
        BinaryExpression be => BuildBinary(be, ref scopes),
        MethodCallExpression mce => BuildMethodCall(mce, ref scopes),
        MemberExpression me => BuildMember(me, ref scopes),
        LambdaExpression le => BuildLambda(le, ref scopes),
        _ => UnsupportedKey.Instance,
    };

    /// <summary>
    /// Local, per-<see cref="BuildKey(Expression?, IReadOnlyList{ParameterExpression[]})"/>-call (or per-
    /// <see cref="BuildKeys"/>-call) mutable scope-lookup state, threaded by <see langword="ref"/> through
    /// every <c>Build*</c> helper (roadmap stage S5, P4). Deliberately a <see langword="ref struct"/>: it
    /// must never escape the stack of the walk that builds one key (or one shared batch of sibling keys),
    /// preserving the pre-existing contract that the scope workspace is entirely local to key construction,
    /// never shared/global mutable state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Combines two logically distinct scope sources without ever materializing them into one concatenated
    /// list: <see cref="EnclosingScopes"/>, an immutable snapshot (outermost first), never mutated by this
    /// type; and <see cref="NestedScopes"/>, a lazily-allocated stack of scope frames pushed by
    /// <c>BuildLambda</c> for a <see cref="LambdaExpression"/> encountered while walking the term itself
    /// (innermost/most-recently-pushed last). <see cref="NestedScopes"/> stays <see langword="null"/>,
    /// allocating nothing, unless the term being keyed actually contains a nested lambda - which is why an
    /// ordinary no-nested-lambda <see cref="BuildKey(Expression?, IReadOnlyList{ParameterExpression[]})"/>
    /// call now performs no scope-management heap allocation at all, regardless of how many enclosing scopes
    /// were supplied.
    /// </para>
    /// <para>
    /// <b>Snapshot contract preserved via overload resolution, not a runtime check (PR #606 review round 2).</b>
    /// The pre-P4 baseline unconditionally defensive-copied whatever <c>BuildKey</c>/<c>BuildKeys</c> received
    /// (via <c>List{T}.AddRange</c>) before returning control to the caller - a real behavioral guarantee for
    /// ANY caller, including this PR's own tests (which pass a plain mutable <see cref="List{T}"/> literal).
    /// Review round 1 restored that guarantee for non-array inputs, but did so with an <c>as ParameterExpression[][]</c>
    /// runtime-type check: any caller happening to hold an actual (mutable, not-necessarily-owned)
    /// <c>ParameterExpression[][]</c> array - not just <see cref="ExpressionSimplifier"/>'s own trusted
    /// snapshot - would silently skip the defensive copy too, since the check cannot distinguish "this exact
    /// runtime type" from "this exact runtime type AND I exclusively own it". Round 2 replaces the runtime
    /// check with two constructor overloads instead: this <see cref="IReadOnlyList{T}"/>-typed constructor
    /// ALWAYS defensive-copies (used by <c>BuildKey</c>/<c>BuildKeys</c>, the generic entry points), while a
    /// second, <c>ParameterExpression[][]</c>-typed constructor below skips the copy and is reachable only via
    /// <c>BuildKeyFromSnapshot</c>/<c>BuildKeysFromSnapshot</c> - internal entry points <see cref="ExpressionSimplifier"/>
    /// alone calls, with its own <c>CaptureLexicalScopeSnapshot</c> array (either <see cref="Array.Empty{T}"/>
    /// or a fresh <c>List{ParameterExpression[]}.ToArray()</c>, so never aliased by any other live reference).
    /// Trust is now expressed at the call site by which method the caller chose to declare/call, not inferred
    /// from what the runtime happens to hand back.
    /// </para>
    /// <para>
    /// <b>Bound-parameter depth is unchanged.</b> A bound-parameter lookup (see <c>BuildParameter</c>)
    /// searches <see cref="NestedScopes"/> innermost-to-outermost first, then <see cref="EnclosingScopes"/>
    /// innermost-to-outermost, with the nested frames counted as strictly deeper than every enclosing frame -
    /// exactly reproducing the depth/position result the pre-S5-P4 single concatenated list produced (see
    /// this class's remarks on the bound-parameter depth invariant, and the worked example there: with
    /// enclosing scopes <c>outer0, outer1</c> and nested scopes <c>inner0, inner1</c>, the effective stack is
    /// <c>outer0, outer1, inner0, inner1</c>, so <c>inner1</c> is depth 0, <c>inner0</c> depth 1, <c>outer1</c>
    /// depth 2, <c>outer0</c> depth 3).
    /// </para>
    /// </remarks>
    private ref struct ScopeState
    {
        /// <summary>
        /// The ambient lexical scopes enclosing the term being keyed, outermost first - either the exact
        /// trusted-snapshot array the caller supplied (the zero-allocation constructor below) or a defensive
        /// one-time copy of an arbitrary <see cref="IReadOnlyList{T}"/> (see this type's remarks). Never
        /// mutated or re-copied by this type after construction.
        /// </summary>
        public readonly ParameterExpression[][] EnclosingScopes;

        /// <summary>
        /// Lazily-allocated stack of scope frames for a <see cref="LambdaExpression"/> encountered while
        /// walking the term itself, innermost (most-recently-pushed) last. <see langword="null"/> until the
        /// first nested lambda is pushed by <c>BuildLambda</c>, so a term with no nested lambda never
        /// allocates this list. Pre-sized to 2 on first allocation (rather than <see cref="List{T}"/>'s
        /// default capacity of 4): the vast majority of nested-lambda terms this class ever sees are at most
        /// one or two levels deep (see this file's own benchmarked scenarios), and the smaller initial
        /// backing array measurably closed a small (+8 byte/call) regression PR #606 review round 1 found
        /// against the pre-P4 baseline for these cases.
        /// </summary>
        public List<ParameterExpression[]>? NestedScopes;

        /// <summary>Initializes a new <see cref="ScopeState"/> with no nested scopes pushed yet, always defensively copying <paramref name="enclosingScopes"/> - the generic, not-necessarily-trusted entry point (see this type's remarks).</summary>
        /// <param name="enclosingScopes">The ambient lexical scopes enclosing the term being keyed; always copied once into a fresh array.</param>
        public ScopeState(IReadOnlyList<ParameterExpression[]> enclosingScopes)
        {
            EnclosingScopes = CopySnapshot(enclosingScopes);
            NestedScopes = null;
        }

        /// <summary>
        /// Initializes a new <see cref="ScopeState"/> from an array the caller guarantees is a trusted,
        /// exclusively-owned snapshot - the zero-allocation entry point, reachable only through
        /// <c>BuildKeyFromSnapshot</c>/<c>BuildKeysFromSnapshot</c> (see this type's remarks). Stores
        /// <paramref name="enclosingScopes"/> directly with no copy.
        /// </summary>
        /// <param name="enclosingScopes">A snapshot array nothing else can reach and mutate.</param>
        public ScopeState(ParameterExpression[][] enclosingScopes)
        {
            EnclosingScopes = enclosingScopes;
            NestedScopes = null;
        }

        /// <summary>Defensively copies an arbitrary <see cref="IReadOnlyList{T}"/> of scopes into a fresh array, restoring the pre-P4 snapshot guarantee for a caller that did not supply the exact production array type.</summary>
        /// <param name="enclosingScopes">The scopes to copy.</param>
        /// <returns>A fresh array holding the same scope-frame references, in the same order.</returns>
        private static ParameterExpression[][] CopySnapshot(IReadOnlyList<ParameterExpression[]> enclosingScopes)
        {
            var copy = new ParameterExpression[enclosingScopes.Count][];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = enclosingScopes[i];
            }

            return copy;
        }
    }

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

    /// <summary>
    /// Builds the key for a <see cref="ParameterExpression"/>: a bound (depth, position, type) triple if
    /// found in <paramref name="scopes"/> (its <c>NestedScopes</c> searched innermost first, then its
    /// <c>EnclosingScopes</c> innermost first - see <see cref="ScopeState"/>'s remarks for why this two-part
    /// search reproduces the exact same depth a single concatenated list would), or a free-parameter key
    /// otherwise.
    /// </summary>
    /// <param name="pe">The parameter to key.</param>
    /// <param name="scopes">The active scope-lookup state.</param>
    /// <returns>The resulting <see cref="ParameterKey"/>.</returns>
    private static KeyNode BuildParameter(ParameterExpression pe, ref ScopeState scopes)
    {
        List<ParameterExpression[]>? nested = scopes.NestedScopes;
        int nestedCount = nested?.Count ?? 0;
        if (nested is not null)
        {
            for (int i = nestedCount - 1; i >= 0; i--)
            {
                int index = Array.IndexOf(nested[i], pe);
                if (index >= 0)
                {
                    return ParameterKey.Bound(nestedCount - 1 - i, index, pe.Type);
                }
            }
        }

        ParameterExpression[][] enclosing = scopes.EnclosingScopes;
        for (int i = enclosing.Length - 1; i >= 0; i--)
        {
            int index = Array.IndexOf(enclosing[i], pe);
            if (index >= 0)
            {
                return ParameterKey.Bound(nestedCount + (enclosing.Length - 1 - i), index, pe.Type);
            }
        }

        return ParameterKey.Free(pe.Type);
    }

    /// <summary>Builds the key for a <see cref="UnaryExpression"/>: node type, result type, operator method, lifting flags, then the operand's key.</summary>
    /// <param name="ue">The unary expression to key.</param>
    /// <param name="scopes">The active scope-lookup state.</param>
    /// <returns>The resulting <see cref="UnaryKey"/>.</returns>
    private static KeyNode BuildUnary(UnaryExpression ue, ref ScopeState scopes)
        => new UnaryKey(ue.NodeType, ue.Type, ue.Method, ue.IsLifted, ue.IsLiftedToNull, Build(ue.Operand, ref scopes));

    /// <summary>Builds the key for a <see cref="BinaryExpression"/>: node type, result type, operator method, lifting flags, left/right/conversion keys.</summary>
    /// <param name="be">The binary expression to key.</param>
    /// <param name="scopes">The active scope-lookup state.</param>
    /// <returns>The resulting <see cref="BinaryKey"/>.</returns>
    private static KeyNode BuildBinary(BinaryExpression be, ref ScopeState scopes)
        => new BinaryKey(
            be.NodeType,
            be.Type,
            be.Method,
            be.IsLifted,
            be.IsLiftedToNull,
            Build(be.Left, ref scopes),
            Build(be.Right, ref scopes),
            Build(be.Conversion, ref scopes));

    /// <summary>Builds the key for a <see cref="MethodCallExpression"/>: exact method, receiver key, then each argument's key in order.</summary>
    /// <param name="mce">The method call to key.</param>
    /// <param name="scopes">The active scope-lookup state.</param>
    /// <returns>The resulting <see cref="MethodCallKey"/>.</returns>
    private static KeyNode BuildMethodCall(MethodCallExpression mce, ref ScopeState scopes)
    {
        var arguments = new KeyNode[mce.Arguments.Count];
        for (int i = 0; i < arguments.Length; i++)
        {
            arguments[i] = Build(mce.Arguments[i], ref scopes);
        }

        return new MethodCallKey(mce.Method, Build(mce.Object, ref scopes), arguments);
    }

    /// <summary>Builds the key for a <see cref="MemberExpression"/>: exact member, then the receiver's key.</summary>
    /// <param name="me">The member access to key.</param>
    /// <param name="scopes">The active scope-lookup state.</param>
    /// <returns>The resulting <see cref="MemberKey"/>.</returns>
    private static KeyNode BuildMember(MemberExpression me, ref ScopeState scopes)
        => new MemberKey(me.Member, Build(me.Expression, ref scopes));

    /// <summary>Builds the key for a <see cref="LambdaExpression"/>: delegate type, <see cref="LambdaExpression.TailCall"/>, parameter types, then the body's key under a pushed local scope frame for this lambda's own parameters.</summary>
    /// <param name="le">The lambda to key.</param>
    /// <param name="scopes">The active scope-lookup state; its <c>NestedScopes</c> stack is lazily allocated here on the first nested lambda, then this lambda's parameters are pushed before, and popped after, keying the body.</param>
    /// <returns>The resulting <see cref="LambdaKey"/>.</returns>
    private static KeyNode BuildLambda(LambdaExpression le, ref ScopeState scopes)
    {
        ParameterExpression[] parameters = le.Parameters.ToArray();
        Type[] parameterTypes = new Type[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            parameterTypes[i] = parameters[i].Type;
        }

        List<ParameterExpression[]> nested = scopes.NestedScopes ??= new List<ParameterExpression[]>(2);
        nested.Add(parameters);
        KeyNode body;
        try
        {
            body = Build(le.Body, ref scopes);
        }
        finally
        {
            nested.RemoveAt(nested.Count - 1);
        }

        return new LambdaKey(le.Type, le.TailCall, parameterTypes, body);
    }

    /// <summary>
    /// Deterministically compares two <see cref="Type"/> values by reflection metadata: namespace/name
    /// text, then (for a constructed generic type) its generic arguments recursively, then (only if every
    /// other dimension ties) <see cref="CompareFinalMemberTiebreak"/>'s own assembly/module-name/module-version/token dimensions.
    /// Never uses <see cref="object.GetHashCode"/>, <see cref="Type.ToString"/>, or object identity/reference
    /// order as part of the ordering decision itself - reference equality is checked only as an initial
    /// same-instance short-circuit (line 1 below), never as a tie-break: two distinct instances that tie on
    /// every dimension this method and <see cref="CompareFinalMemberTiebreak"/> inspect conservatively
    /// compare as equal (<c>0</c>), relying on the caller's stable sort to preserve source order, rather than
    /// fabricating an order from reference/hash identity.
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

        // Every other dimension ties: two genuinely distinct Type instances that otherwise share namespace,
        // name and generic shape. This is NOT unreachable in practice (S4 review, round 8 correction of an
        // earlier, overstated claim here): loading two builds/versions of "the same" assembly - each
        // defining a type with an identical namespace/name - side by side is possible without even needing
        // separate AssemblyLoadContexts (e.g. Assembly.LoadFile, unlike Load/LoadFrom, does not participate
        // in the default identity-based binding cache). System.Type derives from MemberInfo, so the shared,
        // non-throwing tie-break below applies here too - see its own remarks for how it distinguishes such
        // types when possible (assembly identity, then module identity/version, then metadata token).
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
    /// <remarks>
    /// <para>
    /// <b>Fixed dimension order (S4 review, round 7; extended round 8).</b> The four dimensions below -
    /// assembly, then module NAME, then module VERSION (<see cref="Module.ModuleVersionId"/>), then metadata
    /// token - are evaluated in this SAME fixed order for every pair, each one comparing "is the dimension
    /// available" before "what is its value" and falling through to the next dimension only on an exact tie
    /// (both available and equal, or both unavailable). An earlier version let each PAIR independently
    /// decide which dimension actually distinguished it (token first if both had one and they differed, else
    /// module if both had one, else assembly), which is not a consistent total preorder: three members A
    /// (token-bearing, module "Z"), B (token-less <see cref="System.Reflection.Emit.DynamicMethod"/>, module
    /// "M"), C (token-bearing, module "A", a higher token than A) could compare <c>A &lt; C</c> via tokens,
    /// <c>A &gt; B</c> via modules (A has no token to compare against B), and <c>B &gt; C</c> via modules
    /// again - a cycle (<c>A &lt; C</c> but <c>A &gt; B &gt; C</c>), which breaks the total order
    /// <see cref="Enumerable.OrderBy{TSource, TKey}(IEnumerable{TSource}, Func{TSource, TKey})"/> assumes.
    /// Ordering assembly before module before module version before token also matches metadata reality: a
    /// <see cref="MemberInfo.MetadataToken"/> is only meaningful WITHIN its own module, so it must never be
    /// compared across two members before their module identity is already known to match (or both lack
    /// one) - comparing tokens first, as that earlier version did, could otherwise compare token values
    /// belonging to entirely different metadata spaces as if they shared one.
    /// </para>
    /// <para>
    /// <b>Why module VERSION, not just module NAME (S4 review, round 8).</b> A <see cref="MemberInfo.MetadataToken"/>
    /// identifies a member only in combination with its actual <see cref="Module"/> - not with that module's
    /// display <see cref="Module.Name"/> text, which is not guaranteed unique (every dynamically-created
    /// module reports the identical literal <c>"&lt;In Memory Module&gt;"</c>, for one - see
    /// <c>ExpressionSimplifierStructuralCanonicalizationTests.CompareMethod_MetadataTiebreak_ModuleVersionTakesPriorityOverSameNameSameToken</c>).
    /// Two different modules (from two different builds, say) could therefore share an identical
    /// assembly-name string, an identical module-name string, AND an identical token value while genuinely
    /// representing different metadata - module name text alone under-distinguishes them.
    /// <see cref="Module.ModuleVersionId"/> is a GUID the runtime generates to uniquely identify a module's
    /// VERSION/BUILD - stored in the module's own metadata, which is exactly why every runtime load of the
    /// SAME build shares the SAME MVID (see the residual limit below) - so comparing it (after module name,
    /// before token) closes the module-name-text gap. <see cref="Guid"/> does not order lexicographically by
    /// its displayed hex text, but <see cref="Guid.CompareTo(Guid)"/> is still a valid, deterministic total
    /// order over its raw bytes - sufficient here, since only determinism/transitivity is required, not a
    /// "meaningful" ordering.
    /// </para>
    /// <para>
    /// <b>A residual, documented limit: same-binary reloads are still indistinguishable by metadata alone.</b>
    /// The SAME physical assembly file loaded twice - e.g. into two different
    /// <see cref="System.Runtime.Loader.AssemblyLoadContext"/>s - produces two distinct <see cref="MemberInfo"/>
    /// instances that share an identical assembly name, module name, <see cref="Module.ModuleVersionId"/>
    /// AND metadata token, since the MVID is embedded in the file's own metadata and travels with every copy
    /// of the same build. This tie-break's contract is therefore "distinguish two members by their AVAILABLE
    /// STRUCTURAL METADATA", not "always distinguish the exact runtime <see cref="MemberInfo"/> identity" -
    /// two such same-binary-reload members conservatively tie (<c>0</c>) here, exactly like any other pair
    /// that ties on every dimension this method inspects, relying on the caller's stable sort to preserve
    /// source order rather than inventing one from runtime identity.
    /// </para>
    /// </remarks>
    private static int CompareFinalMemberTiebreak(MemberInfo a, MemberInfo b)
    {
        bool aHasAssembly = TryGetAssemblyFullName(a, out string? aAssembly);
        bool bHasAssembly = TryGetAssemblyFullName(b, out string? bAssembly);
        if (aHasAssembly != bHasAssembly) return aHasAssembly ? -1 : 1;
        if (aHasAssembly)
        {
            int assemblyCompare = string.CompareOrdinal(aAssembly, bAssembly);
            if (assemblyCompare != 0) return assemblyCompare;
        }

        bool aHasModule = TryGetModuleName(a, out string? aModule);
        bool bHasModule = TryGetModuleName(b, out string? bModule);
        if (aHasModule != bHasModule) return aHasModule ? -1 : 1;
        if (aHasModule)
        {
            int moduleCompare = string.CompareOrdinal(aModule, bModule);
            if (moduleCompare != 0) return moduleCompare;
        }

        bool aHasModuleVersion = TryGetModuleVersionId(a, out Guid aModuleVersion);
        bool bHasModuleVersion = TryGetModuleVersionId(b, out Guid bModuleVersion);
        if (aHasModuleVersion != bHasModuleVersion) return aHasModuleVersion ? -1 : 1;
        if (aHasModuleVersion)
        {
            int moduleVersionCompare = aModuleVersion.CompareTo(bModuleVersion);
            if (moduleVersionCompare != 0) return moduleVersionCompare;
        }

        bool aHasToken = TryGetMetadataToken(a, out int aToken);
        bool bHasToken = TryGetMetadataToken(b, out int bToken);
        if (aHasToken != bHasToken) return aHasToken ? -1 : 1;
        if (aHasToken)
        {
            int tokenCompare = aToken.CompareTo(bToken);
            if (tokenCompare != 0) return tokenCompare;
        }

        // No further safe, non-throwing, deterministic dimension distinguishes two members that already
        // tied on every earlier structural dimension (declaring type, name, generic shape,
        // parameter/return types, assembly, module name, module version, metadata token) - conservatively
        // tie, relying on the caller's stable sort to preserve source order, rather than fabricating an
        // order from runtime identity (object reference/hash), which the roadmap explicitly forbids.
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

    /// <summary>
    /// Safely reads <see cref="Module.ModuleVersionId"/> for <paramref name="member"/>'s declaring module,
    /// which can throw for some dynamically-generated members. Unlike <see cref="Module.Name"/> (a display
    /// string, not guaranteed unique - every dynamically-created module reports the same literal text), this
    /// GUID uniquely identifies a module's VERSION/BUILD (stored in the module's own metadata - see
    /// <see cref="CompareFinalMemberTiebreak"/>'s remarks for why two loads of the SAME build therefore share
    /// the SAME value), making it the dimension that actually resolves which module a
    /// <see cref="MemberInfo.MetadataToken"/> belongs to.
    /// </summary>
    /// <param name="member">The member or type to inspect.</param>
    /// <param name="moduleVersionId">The module version ID, when available.</param>
    /// <returns><see langword="true"/> if <paramref name="moduleVersionId"/> was read successfully.</returns>
    private static bool TryGetModuleVersionId(MemberInfo member, out Guid moduleVersionId)
    {
        try
        {
            moduleVersionId = member.Module.ModuleVersionId;
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            moduleVersionId = default;
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

            // Lexicographic element comparison plus a length tie-break (S4 review, round 6): reuses the
            // shared, already-tested EnumerableComparer<T> instead of a hand-rolled duplicate of the exact
            // same algorithm - KeyNode's own IComparable<KeyNode> makes it directly usable here.
            return EnumerableComparer<KeyNode>.Default.Compare(_arguments, o._arguments);
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
