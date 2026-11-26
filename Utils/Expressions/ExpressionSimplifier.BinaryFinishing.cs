using System.Collections.Generic;
using System.Linq.Expressions;
using Utils.Expressions;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// The binary pair finishing of refused chains (mission B, <c>docs/expressions/SimplifierFlatten.md</c> §23): the factorings that
/// <see cref="AdditionOfEqualsElements"/> and <see cref="SubstractionOfEqualsElements"/> apply, on the next call, to a pair that
/// the last canonicalization of a refused chain built, applied once, at the boundary of that chain.
/// </summary>
public partial class ExpressionSimplifier
{
    /// <summary>
    /// Runs again, as the next call runs it, the deepest node of a refused boundary result whose pair a binary factoring rule of the
    /// next call may factor, then the levels of the prefix above it (mission B, <c>docs/expressions/SimplifierFlatten.md</c> §23).
    /// </summary>
    /// <param name="chain">The result of the levels of a refused chain, neither linear nor factorable as a whole, without a suffix to factor.</param>
    /// <param name="statistics">The opt-in counters of the thread, or <see langword="null"/>.</param>
    /// <returns>The new result of the chain, or <see langword="null"/> when no node passes the pre-test or its new treatment changes nothing.</returns>
    /// <remarks>
    /// <para>
    /// <b>What the next call does.</b> The levels of a refused chain canonicalize at every level: the last canonicalization
    /// (<see cref="CanonicalizeAdditiveTerms"/>) sorts every term of the chain and rebuilds a right spine <c>g0 + (g1 + (… + gn))</c>
    /// in which two terms may meet for the first time, either as the two operands of the last spine node (<c>-2*x</c> and
    /// <c>x*(2 + z)</c>, sorted next to each other after a quotient), or inside a group of identical terms that are no monomials
    /// (<c>(z/z) + (z/z)</c>, <c>(0 - (x/z)) - (x/z)</c>: the identical monomials are merged, not the identical quotients). No rule
    /// is offered those nodes during the call that builds them. The next call prepares each of them, offers it to the admission,
    /// and runs its levels, where <see cref="AdditionOfEqualsElements"/> or <see cref="SubstractionOfEqualsElements"/> factors it;
    /// the levels of the prefix then rebuild the chain on top of the new node.
    /// </para>
    /// <para>
    /// <b>Pre-test.</b> Each spine node is tested once, bottom-up, without any rule or comparer call: a group operand (two or more
    /// non-zero terms) holding two adjacent terms that share a factor, or, for the last node, its two operands sharing a factor
    /// (<see cref="SharesBinaryFactor"/>: the four comparisons of the factoring rules, by raw structural equality). Raw equality
    /// implies the equality of the rules' comparer, so a node that passes may be factored by the rules; a pair that only the
    /// comparer's simplifications make equal is left to the next call, as before. The work is linear in the size of the spine and
    /// of its groups.
    /// </para>
    /// <para>
    /// <b>Treatment.</b> The deepest such node is transformed again exactly as the next call reaches it, as a crossed operand of a
    /// refused chain (<see cref="ExpressionTransformer.PrepareExpression"/> with the refused-chain marker set): admission, rules
    /// and finalization of the historical engine, nothing else. When the result is structurally the node itself, nothing changes;
    /// otherwise the prefix is run again on top of it (<see cref="RunPrefixLevels"/>), as in mission M4.
    /// </para>
    /// <para>
    /// <b>Comparer simplifications.</b> A simplification the comparer makes for a factor probe (<see cref="FactorEqualityProbe"/>,
    /// <see cref="ComparisonMemoDepth"/> above zero) is not finished: its result decides which factors the binary rules see as
    /// equal, and a stronger result there changes those historical decisions (a coefficient sum equal to <c>1</c> made the rule
    /// factor a whole chain by it, a form no call of <c>master</c> builds); it also keeps the re-run of a node from nesting
    /// inside the probes of the rules it runs.
    /// </para>
    /// <para>
    /// <b>Safety.</b> No new identity is applied: every node is rebuilt by the rules, the admission and the finalization of the
    /// engine, under the effect gate and the resource budget, on the operands the next call prepares. Nothing is admitted to the
    /// global factoring (<see cref="IsFactorableChain"/> is unchanged): a quotient or a sum stays a factor of the binary rules
    /// only. The pre-test reads type tests and raw structure only (<see cref="ExpressionComparer.StructuralEqualsRaw"/>, which
    /// never runs user code), so a hostile node is never read through a virtual member before the engine itself prepares it.
    /// </para>
    /// </remarks>
    private Expression? FinishRefusedBinaryPair(BinaryExpression chain, FlattenAdmissionStatistics? statistics)
    {
        // A simplification made by the comparer for a factor probe keeps the historical result: the decisions of the factoring
        // rules that compare with it stay those of master, and no rule re-run nests inside the probes of another one.
        if (_comparisonMemoDepth > 0 || !IsFlattenedChainType(chain.Type)) return null;
        if (statistics is not null) statistics.BinarySearches++;

        // The right spine, as in FinishRefusedSuffix: a subtraction of a crossed operand may start a grouped quotient chain the
        // next call admits, so no node is searched below it.
        var spine = new List<BinaryExpression>();
        for (BinaryExpression? node = chain; node is not null; node = AsCrossedAdditiveNode(node.Right))
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            if (node.NodeType == ExpressionType.Subtract && AsCrossedAdditiveNode(node.Right) is not null) return null;
            spine.Add(node);
        }

        // A last subtracted term is written 0 - t (BuildAdditiveExpression): a term of the node above, not a pair.
        if (spine[^1] is { NodeType: ExpressionType.Subtract } lastTerm && IsOrdinaryZeroConstant(lastTerm.Left)) spine.RemoveAt(spine.Count - 1);

        int start = -1;
        var pending = new Stack<Expression>();
        for (int index = spine.Count - 1; index >= 0 && start < 0; index--)
        {
            BinaryExpression node = spine[index];
            bool last = index == spine.Count - 1;

            // Type tests first (ETC R4): a node holding an opaque operand is never read, and no node is searched above it.
            if (ExpressionOpacity.IsOpaque(node.Left) || ExpressionOpacity.IsOpaque(node.Right) || !IsOrdinaryBinaryArithmetic(node)) return null;
            if (HasFactorableGroup(node.Left, pending)
                || last && (HasFactorableGroup(node.Right, pending) || SharesBinaryFactor(node.Left, node.Right, pending)))
            {
                start = index;
            }
        }

        if (start < 0 || !IsSmallTree(spine[start], pending, MaximumCandidateNodes) || !HasSmallPrefix(spine, start, pending)
            || FlattenRuleDispatcherOf(chain) is not FlattenRuleDispatcher dispatcher) return null;
        if (statistics is not null) statistics.BinaryCandidates++;

        bool callerWithinRefusedChain = WithinRefusedChain;
        WithinRefusedChain = true;
        try
        {
            BinaryExpression candidate = spine[start];
            Expression rebuilt = PrepareExpression(candidate);

            // Unchanged: the historical result stays. The comparison is made on small trees only (a large or shared candidate is
            // taken as changed, and its prefix is run again as the next call runs it).
            if (ReferenceEquals(rebuilt, candidate) || BoundedRawEquals(rebuilt, candidate, pending)) return null;

            if (statistics is not null)
            {
                statistics.BinaryFinishings++;
                statistics.BinaryPrefixLevels += start;
            }

            return RunPrefixLevels(spine, start, rebuilt, dispatcher);
        }
        finally
        {
            WithinRefusedChain = callerWithinRefusedChain;
        }
    }

    /// <summary>
    /// The number of nodes a tree compared by <see cref="BoundedRawEquals"/> may hold, counted with their repetitions: the
    /// factors of the binary rules are small (a parameter, a constant, a quotient, a short sum), and a larger or shared one is
    /// never compared, so that a DAG whose paths multiply costs a bounded number of visits.
    /// </summary>
    private const int MaximumComparedNodes = 32;

    /// <summary>
    /// The number of nodes the candidate of the binary finishing may hold, counted with their repetitions (<see cref="IsSmallTree"/>):
    /// only a candidate made of framework nodes of runtime provenance is transformed again, so that no impersonating reflection
    /// object and no opaque node is ever rebuilt by the finishing; a larger candidate is left to the next call, as before.
    /// </summary>
    private const int MaximumCandidateNodes = 256;

    /// <summary>
    /// Whether the left operands of the prefix above <paramref name="start"/>, which <see cref="RunPrefixLevels"/> prepares again,
    /// are small trees of runtime provenance (<see cref="IsSmallTree"/>) holding together at most <see cref="MaximumCandidateNodes"/>
    /// nodes counted with their repetitions: the re-run of the prefix is then bounded in work and in depth (a shared DAG whose
    /// paths multiply, a long prefix or a prefix holding a hostile node keeps the historical result).
    /// </summary>
    /// <param name="spine">The right spine of the result, root first.</param>
    /// <param name="start">The index of the candidate.</param>
    /// <param name="pending">A reusable stack.</param>
    /// <returns><see langword="true"/> when the prefix may be run again.</returns>
    private static bool HasSmallPrefix(List<BinaryExpression> spine, int start, Stack<Expression> pending)
    {
        int remaining = MaximumCandidateNodes;
        for (int index = 0; index < start; index++)
        {
            int size = SmallTreeSize(spine[index].Left, pending, remaining);
            if (size < 0) return false;
            remaining -= size;
        }

        return true;
    }

    /// <summary>
    /// Whether <paramref name="operand"/> is a crossed group whose first two non-zero terms share a factor
    /// (<see cref="SharesBinaryFactor"/>): the group of identical terms the canonicalization rebuilt, which the next call's factoring
    /// rules merge. Only the first terms are read, by a walk bounded by <see cref="MaximumComparedNodes"/> nodes.
    /// </summary>
    /// <param name="operand">An operand of a spine node.</param>
    /// <param name="pending">A reusable stack for the walks.</param>
    /// <returns><see langword="true"/> when the group may be factored by a binary rule.</returns>
    private bool HasFactorableGroup(Expression operand, Stack<Expression> pending)
    {
        if (AsCrossedAdditiveNode(operand) is not BinaryExpression group) return false;

        // The terms in source order: left operands first. A group of identical terms repeats its first term.
        pending.Clear();
        pending.Push(group);
        Expression? first = null;
        for (int visits = 0; pending.Count > 0 && visits < MaximumComparedNodes; visits++)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            Expression node = pending.Pop();
            if (AsCrossedAdditiveNode(node) is BinaryExpression crossed)
            {
                pending.Push(crossed.Right);
                pending.Push(crossed.Left);
                continue;
            }

            if (IsOrdinaryZeroConstant(node)) continue;
            if (first is null)
            {
                first = node;
                continue;
            }

            // The walk ends here, so its stack is reused by the comparisons.
            return SharesBinaryFactor(first, node, pending);
        }

        return false;
    }

    /// <summary>
    /// Whether two additive operands share a factor as <see cref="AdditionOfEqualsElements"/> and
    /// <see cref="SubstractionOfEqualsElements"/> decompose them: each operand, once an ordinary <c>0 - t</c> or <c>-t</c> is
    /// removed (the forms the next call prepares and the negation rules move out), is an ordinary product <c>a * b</c> or the
    /// product <c>1 * t</c>; the four comparisons of the rules (<c>a</c> with <c>c</c>, <c>a</c> with <c>d</c>, <c>b</c> with
    /// <c>c</c>, <c>b</c> with <c>d</c>) are made by raw structural equality (<see cref="BoundedRawEquals"/>), the implied
    /// coefficient <c>1</c> never compared.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="pending">A reusable stack for the bounded walks.</param>
    /// <returns>
    /// <see langword="true"/> when a factor is structurally shared and no factor is a <c>NaN</c>, infinite or negative zero
    /// floating-point constant.
    /// </returns>
    /// <remarks>
    /// A necessary condition only for the pairs it compares: the rules compare with the public comparer, which equals every pair
    /// that is raw equal, and decide themselves; a pair it does not compare (a large factor) is left to the next call, as before.
    /// Type tests first, then the operator (ETC R4); every visit consumes the structural budget.
    /// </remarks>
    private static bool SharesBinaryFactor(Expression left, Expression right, Stack<Expression> pending)
    {
        DecomposeBinaryFactors(left, out Expression? a, out Expression b);
        DecomposeBinaryFactors(right, out Expression? c, out Expression d);

        // A non-finite or negatively signed zero coefficient is never finished (the global factoring refuses it too): the binary
        // rule's factoring of it is the IEEE latitude of S3, left to the next call as before.
        if (IsSpecialFloatingConstant(a) || IsSpecialFloatingConstant(b) || IsSpecialFloatingConstant(c) || IsSpecialFloatingConstant(d)) return false;
        return a is not null && c is not null && BoundedRawEquals(a, c, pending)
            || a is not null && BoundedRawEquals(a, d, pending)
            || c is not null && BoundedRawEquals(b, c, pending)
            || BoundedRawEquals(b, d, pending);
    }

    /// <summary>
    /// Raw structural equality (<see cref="ExpressionComparer.StructuralEqualsRaw"/>, which runs no user code) of two trees that
    /// are the same instance or both hold at most <see cref="MaximumComparedNodes"/> nodes (<see cref="IsSmallTree"/>).
    /// </summary>
    /// <param name="x">The first tree.</param>
    /// <param name="y">The second tree.</param>
    /// <param name="pending">A reusable stack for the bounded walks.</param>
    /// <returns>
    /// <see langword="true"/> for the same instance that is not opaque, or two equal small trees; <see langword="false"/> otherwise.
    /// </returns>
    private static bool BoundedRawEquals(Expression x, Expression y, Stack<Expression> pending)
    {
        ExpressionResourceBudget.ConsumeStructuralVisit();

        // An opaque node (ETC R4) is never a shared factor: only the engine's own treatment reads it, never the pre-test.
        if (ReferenceEquals(x, y)) return !ExpressionOpacity.IsOpaque(x);
        return IsSmallTree(x, pending) && IsSmallTree(y, pending) && ExpressionComparer.StructuralEqualsRaw(x, y);
    }

    /// <summary>
    /// Whether <paramref name="tree"/> is made of parameters, constants, framework unary and binary nodes without a conversion lambda
    /// and static calls only, every operator or called method of runtime provenance, with at most <paramref name="limit"/> nodes
    /// counted with their repetitions. By type tests and runtime reflection only (S5 A-10, ETC R4): the provenance of a method is
    /// checked before anything else is read of it, and any other node (an extension node, a lambda, a member) makes the tree
    /// rejected without being read.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="pending">A reusable stack (cleared here).</param>
    /// <param name="limit">The maximum number of nodes.</param>
    /// <returns><see langword="true"/> for an accepted tree.</returns>
    private static bool IsSmallTree(Expression tree, Stack<Expression> pending, int limit = MaximumComparedNodes)
        => SmallTreeSize(tree, pending, limit) >= 0;

    /// <summary>
    /// The number of nodes of <paramref name="tree"/>, counted with their repetitions, when it is a small tree in the sense of
    /// <see cref="IsSmallTree"/> with at most <paramref name="limit"/> nodes; <c>-1</c> otherwise.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="pending">A reusable stack (cleared here).</param>
    /// <param name="limit">The maximum number of nodes.</param>
    /// <returns>The number of nodes, or <c>-1</c>.</returns>
    private static int SmallTreeSize(Expression tree, Stack<Expression> pending, int limit)
    {
        pending.Clear();
        pending.Push(tree);
        int visits = 0;
        while (pending.Count > 0)
        {
            if (++visits > limit) return -1;
            ExpressionResourceBudget.ConsumeStructuralVisit();
            switch (pending.Pop())
            {
                case ParameterExpression or ConstantExpression:
                    break;
                case BinaryExpression { Conversion: null } binary when binary.Method is null || ExpressionReflectionProvenance.IsRuntime(binary.Method):
                    pending.Push(binary.Right);
                    pending.Push(binary.Left);
                    break;
                case UnaryExpression unary when unary.Method is null || ExpressionReflectionProvenance.IsRuntime(unary.Method):
                    if (unary.Operand is not null) pending.Push(unary.Operand);
                    break;
                case MethodCallExpression { Object: null } call when ExpressionReflectionProvenance.IsRuntime(call.Method):
                    for (int index = call.Arguments.Count - 1; index >= 0; index--) pending.Push(call.Arguments[index]);
                    break;
                default:
                    return -1;
            }
        }

        return visits;
    }

    /// <summary>
    /// Whether <paramref name="factor"/> is a <see cref="double"/> or <see cref="float"/> constant that is <c>NaN</c>, infinite or a
    /// negative zero: the coefficients whose factoring changes IEEE results (S3 latitude), which the global factoring refuses.
    /// </summary>
    /// <param name="factor">A decomposed factor, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> for such a constant.</returns>
    private static bool IsSpecialFloatingConstant(Expression? factor)
        => factor is ConstantExpression { Value: double d } ? !double.IsFinite(d) || d == 0 && double.IsNegative(d)
            : factor is ConstantExpression { Value: float f } && (!float.IsFinite(f) || f == 0 && float.IsNegative(f));

    /// <summary>
    /// Decomposes an additive operand as the binary factoring rules do (<see cref="SharesBinaryFactor"/>).
    /// </summary>
    /// <param name="operand">The operand.</param>
    /// <param name="first">The left factor of an ordinary product, or <see langword="null"/> for the implied coefficient <c>1</c>.</param>
    /// <param name="second">The right factor of an ordinary product, or the operand itself.</param>
    private static void DecomposeBinaryFactors(Expression operand, out Expression? first, out Expression second)
    {
        // Type tests first (ETC R4): an operator is only asked about once its operands are known not to be opaque.
        Expression term = operand;
        if (term is BinaryExpression { NodeType: ExpressionType.Subtract } difference && !HasOpaqueOperand(difference)
            && IsOrdinaryBinaryArithmetic(difference) && IsOrdinaryZeroConstant(difference.Left))
        {
            term = difference.Right;
        }
        else if (term is UnaryExpression { NodeType: ExpressionType.Negate } negation && !ExpressionOpacity.IsOpaque(negation.Operand)
            && IsOrdinaryUnaryNegate(negation))
        {
            term = negation.Operand;
        }

        if (term is BinaryExpression { NodeType: ExpressionType.Multiply } product && !HasOpaqueOperand(product) && IsOrdinaryBinaryArithmetic(product))
        {
            first = product.Left;
            second = product.Right;
        }
        else
        {
            first = null;
            second = term;
        }

        static bool HasOpaqueOperand(BinaryExpression binary) => ExpressionOpacity.IsOpaque(binary.Left) || ExpressionOpacity.IsOpaque(binary.Right);
    }
}
