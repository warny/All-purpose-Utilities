using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Utils.Expressions;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// Grouped quotient chains (<c>docs/expressions/SimplifierFlatten.md</c> §16): the additive chains of linear terms and linear
/// quotients that subtract a crossed group, such as <c>1 - (x1/x0 - (x2/x0 - x3/x0))</c> or
/// <c>1 - ((x1/x0 + x2/x0) + (x3/x0 + x4/x0))</c>, canonicalized once from all their signed terms.
/// </summary>
public partial class ExpressionSimplifier
{
    /// <summary>Backing field of <see cref="GroupedQuotientChainsDisabled"/>.</summary>
    [ThreadStatic]
    private static bool t_groupedQuotientChainsDisabled;

    /// <summary>
    /// Gets or sets, for this thread, whether the admission skips the grouped quotient family (§16): audits and tests only, so
    /// that the historical level by level treatment of such a chain stays available, from the built-in simplifier itself, as
    /// the reference of the family. <see langword="false"/> by default.
    /// </summary>
    internal static bool GroupedQuotientChainsDisabled
    {
        get => t_groupedQuotientChainsDisabled;
        set => t_groupedQuotientChainsDisabled = value;
    }

    /// <summary>
    /// Tests a chain the linear family refused for a subtracted group as a grouped quotient chain, after a lookup of the table
    /// of known grouped refusals, and records the root of an admitted chain for the flatten rule invoked right after it.
    /// </summary>
    /// <param name="root">The raw root of the chain.</param>
    /// <param name="session">The session of the running operation.</param>
    /// <param name="statistics">The opt-in counters of the thread, or <see langword="null"/>.</param>
    /// <param name="refusalDepth">
    /// Receives, for a refused chain, the spine index down to which every crossed node is proven refused by the family
    /// (<see cref="int.MaxValue"/> when every sub-chain is), so that the known refusals of every family stay proven; <c>0</c>
    /// for an admitted chain or a lookup.
    /// </param>
    /// <returns><see langword="true"/> when the chain is admitted.</returns>
    /// <remarks>
    /// <para>
    /// The refusal of the family is inherited the way the linear family's is (<see cref="IsLinearChain"/>): a local cause by every
    /// chain that crosses its node, a repetition by every chain that holds both occurrences, a second constant by every chain that
    /// holds both constants. <see cref="RecordRefusedSpine"/> records the spine nodes that contain the cause in the family's own
    /// table (<see cref="ExpressionResourceBudget.Session.FlattenGroupedRefusals"/>), which only ever skips this test, and
    /// reports how deep it went: the caller lowers the depth of the shared table of refusals to it, so that a sub-chain the family
    /// may admit is never refused by one lookup. The type of the chain and the absence of any quotient are inherited by every
    /// sub-chain (the operands of an intrinsic operator have its type), so they do not restrict that depth.
    /// </para>
    /// </remarks>
    private bool TryAdmitGroupedQuotientChain(BinaryExpression root, ExpressionResourceBudget.Session session, FlattenAdmissionStatistics? statistics, out int refusalDepth)
    {
        refusalDepth = 0;
        if (session.FlattenGroupedRefusals is { } known && known.Contains(root))
        {
            // The lookup replaces a walk of at least one node: it is counted as one.
            ExpressionResourceBudget.ConsumeStructuralVisit();
            if (statistics is not null) statistics.KnownGroupedRefusals++;
            return false;
        }

        if (IsGroupedLinearQuotientChain(root, statistics, out ChainRefusal refusal))
        {
            if (statistics is not null) statistics.GroupedQuotient++;
            session.AdmittedGroupedQuotientRoot = root;
            return true;
        }

        if (statistics is not null) statistics.GroupedCauses[(int)refusal.Cause]++;
        if (refusal.Cause is FlattenRefusalCause.ChainType or FlattenRefusalCause.NoQuotient)
        {
            refusalDepth = int.MaxValue;
            return false;
        }

        if (refusal.Depth > 0) refusalDepth = RecordRefusedSpine(root, refusal, ref session.FlattenGroupedRefusals, statistics);
        return false;
    }

    /// <summary>
    /// Whether the additive chain rooted at <paramref name="root"/> is a <em>grouped quotient chain</em> (§16): a chain of
    /// <see cref="double"/> or <see cref="float"/> whose terms satisfy every condition of a linear chain (<see cref="IsLinearChain"/>)
    /// but one, a subtraction may have a crossed right operand, with at most one constant term and at least one linear quotient.
    /// </summary>
    /// <param name="root">The raw root of the chain: a crossed <c>Add</c> or <c>Subtract</c> node.</param>
    /// <param name="statistics">The opt-in counters of the thread, or <see langword="null"/>.</param>
    /// <param name="refusal">
    /// Receives, for a refused chain, its cause and the spine index of the node that holds it (as <see cref="IsLinearChain"/>
    /// reports it, the repeated key included); the default value for an admitted chain.
    /// </param>
    /// <returns><see langword="true"/> for a grouped quotient chain.</returns>
    /// <remarks>
    /// <para>
    /// <b>The conditions.</b> Every crossed node is an ordinary <c>Add</c> or <c>Subtract</c> of the chain's type (no custom,
    /// lifted or checked operator: a checked node is not crossed, so it is a term, and no term is one) and no crossed node has two
    /// constant operands. Every term is a parameter, a finite non-zero constant, a product of a parameter by another parameter or
    /// by a finite constant other than <c>0</c>, <c>1</c> and <c>-1</c>, or an ordinary floating-point division of a parameter
    /// by a parameter (<see cref="IsLinearChainTerm"/>): no call, no invocation, no member access, no conversion, no negation, no
    /// nested division, no extension or dynamic node, nothing that runs user code or reads state. The checks of
    /// <see cref="LinearChainOperands"/> hold over the whole tree, every branch included: a parameter occurs once, as a term, a
    /// factor or a numerator, a denominator divides one or several quotients and is nothing else, the coefficients differ from
    /// each other and from the constant term, and the chain holds at most one constant term and at least one quotient.
    /// </para>
    /// <para>
    /// <b>Why the level by level result is the canonicalization of the signed terms.</b> Level by level, a crossed node sees two
    /// prepared operands, each a prepared term or the canonical form of a sub-chain. Preparing a term gives the term itself (a
    /// parameter, the constant, a product or a quotient: no rule applies to it, §15.5). A level <c>A - B</c> whose prepared
    /// <c>B</c> is a sum is reassociated (<see cref="SubstractionWithAddition"/>, <see cref="SubstractionWithSubstraction"/>,
    /// finished by <see cref="FinishReassociation"/>), its subtracted sub-sums dissolved term by term; the sign rules
    /// (<see cref="SubstractionWithZero(BinaryExpression, ConstantExpression, Expression)"/>,
    /// <see cref="AdditionWithNegate(BinaryExpression, UnaryExpression, Expression)"/>, <see cref="SubstractionWithNegate(BinaryExpression, UnaryExpression, Expression)"/>)
    /// only move signs, and each finalization canonicalizes the signed terms it collects. These are the identities the global
    /// canonicalization applies. Nothing else can apply: two operands of a level always hold a parameter the other does not hold,
    /// the simplifier never introduces a parameter and never removes one from such a chain, so every factoring comparison of
    /// <see cref="AdditionOfEqualsElements"/>, <see cref="SubstractionOfEqualsElements"/> and of the additive canonicalization fails
    /// and nothing cancels or merges; with a single constant term no constant pair is ever folded, and the only zeros are the
    /// symbolic <c>0</c> of a subtracted canonical term (<c>0 - t</c>), which the sign rules consume. Terms of equal canonical
    /// keys (free parameters tie, §14.7) keep their source order at every level: every rewrite above keeps the relative order of
    /// the terms of one sign, the left operand's before the right one's, so the stable sorts compose into the stable sort of the
    /// source order of the signed terms. The equality of the two results is pinned, for every shape and size of the tests and of
    /// the seeded corpus, against the historical treatment of the built-in simplifier (<see cref="GroupedQuotientChainsDisabled"/>).
    /// </para>
    /// <para>
    /// <b>Effects and policies.</b> Every operand is inert (ordinary floating-point arithmetic over parameters and primitive
    /// constants: nothing throws, nothing calls user code), so the effect gate admits every rule on it under every policy and the
    /// result does not depend on the policy, as for a linear chain.
    /// </para>
    /// <para>
    /// One iterative walk of the crossed nodes, one structural visit per node, which stops at the first refusal and allocates its
    /// pending stack only for a branched tree. A shared sub-chain holds a parameter, refused at its second occurrence, or
    /// constants only, refused at its first node with two constant operands, so the walk is linear in the distinct nodes even on a
    /// shared DAG. Type tests come first, so an extension node is never read (ETC R4).
    /// </para>
    /// </remarks>
    private bool IsGroupedLinearQuotientChain(BinaryExpression root, FlattenAdmissionStatistics? statistics, out ChainRefusal refusal)
    {
        Type type = root.Type;
        if (!ReferenceEquals(type, typeof(double)) && !ReferenceEquals(type, typeof(float)))
        {
            refusal = new ChainRefusal(FlattenRefusalCause.ChainType, 0, null);
            return false;
        }

        if (statistics is not null) statistics.GroupedWalks++;
        var operands = LinearChainOperands.ForGroupedChain();
        Stack<BinaryExpression>? pending = null;
        BinaryExpression? node = root;
        int depth = 0;
        bool onSpine = true;
        while (node is not null)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            if (statistics is not null) statistics.NodesExamined++;

            BinaryExpression? left = AsCrossedAdditiveNode(node.Left);
            BinaryExpression? right = AsCrossedAdditiveNode(node.Right);
            if (node.Left is ConstantExpression && node.Right is ConstantExpression)
            {
                refusal = new ChainRefusal(FlattenRefusalCause.ConstantPair, depth, null);
                return false;
            }

            // The terms first, by type tests: the operator of the node is classified only once both operands are known to be
            // framework nodes of an accepted shape.
            if (left is null && !IsLinearChainTerm(node.Left, type))
            {
                refusal = new ChainRefusal(NonLinearTermCause(node.Left), depth, null);
                return false;
            }

            if (right is null && !IsLinearChainTerm(node.Right, type))
            {
                refusal = new ChainRefusal(NonLinearTermCause(node.Right), depth, null);
                return false;
            }

            if (!IsOrdinaryBinaryArithmetic(node))
            {
                refusal = new ChainRefusal(FlattenRefusalCause.Operator, depth, null);
                return false;
            }

            if (left is null && !operands.Add(node.Left, depth, out refusal)) return false;
            if (right is null && !operands.Add(node.Right, depth, out refusal)) return false;

            if (left is not null && right is not null)
            {
                onSpine = false;
                (pending ??= new Stack<BinaryExpression>()).Push(right);
            }

            node = left ?? right ?? (pending is { Count: > 0 } ? pending.Pop() : null);
            if (onSpine) depth++;
        }

        if (!operands.HasQuotients)
        {
            refusal = new ChainRefusal(FlattenRefusalCause.NoQuotient, 0, null);
            return false;
        }

        refusal = default;
        return true;
    }

    /// <summary>
    /// Simplifies a grouped quotient chain admitted by <see cref="FlattenAdmission"/> (§16): each term is prepared once, in
    /// source order, and the prepared signed terms are canonicalized once (<see cref="CanonicalizeAdditiveTerms"/>, nothing
    /// merged: the terms are proven distinct).
    /// </summary>
    /// <param name="source">The raw root of the chain.</param>
    /// <param name="terms">The raw signed terms of the chain, in source order (the extraction applies the inverted sign of every subtracted group).</param>
    /// <param name="dispatcher">The dispatcher of the additive group, which runs the additive signature rules of a level of the fallback.</param>
    /// <returns>The result the level by level treatment gives for <paramref name="source"/>.</returns>
    /// <remarks>
    /// <para>
    /// See <see cref="IsGroupedLinearQuotientChain"/> for why the canonicalization is the level by level result. The preparation
    /// order is the level by level one (the traversal prepares the terms of a chain from left to right), and preparing such a term
    /// runs no user code, so interleaving the levels' canonicalizations differently is not observable.
    /// </para>
    /// <para>
    /// <b>Fallback.</b> The proof requires every prepared term to be its raw term, or the same product with its factors
    /// reordered (<see cref="IsPreparedGroupedTerm"/>). Otherwise (no rule is known to rewrite such a term, so this only guards a
    /// later change of the rules) the levels are run one by one on the prepared terms (<see cref="TransformGroupedLevelByLevel"/>),
    /// exactly as the traversal runs them, without preparing anything twice.
    /// </para>
    /// </remarks>
    private Expression FlattenGroupedQuotientChain(BinaryExpression source, FlattenedTerm[] terms, FlattenRuleDispatcher dispatcher)
    {
        FlattenAdmissionStatistics? statistics = t_admissionStatistics;
        Type type = source.Type;
        var prepared = new Expression[terms.Length];
        bool proven = statistics is not { ForceGroupedFallback: true };
        for (int i = 0; i < prepared.Length; i++)
        {
            // Mission C3a: the prepared term is the end of this instance's treatment of the operand (LocallySimplified).
            prepared[i] = PrepareExpression(terms[i].Operand);
            RecordLocallySimplified(prepared[i]);
            proven &= IsPreparedGroupedTerm(terms[i].Operand, prepared[i]);
        }

        if (proven)
        {
            if (statistics is not null) statistics.GroupedChains++;
            var signedTerms = new List<(Expression Term, bool IsNegative)>(prepared.Length);
            for (int i = 0; i < prepared.Length; i++) CollectAdditiveTerms(signedTerms, prepared[i], terms[i].Operation == ExpressionType.Subtract);
            return CanonicalizeAdditiveTerms(signedTerms, type, recordMetadata: false, AdditiveFactoring.None);
        }

        if (statistics is not null) statistics.GroupedFallbacks++;
        return TransformGroupedLevelByLevel(source, prepared, dispatcher);
    }

    /// <summary>
    /// Whether <paramref name="prepared"/>, the prepared form of the raw term <paramref name="raw"/> of a grouped quotient chain,
    /// is what the proof of the family requires: the raw term itself, a constant node holding the same boxed value, a quotient of
    /// the same numerator and denominator, or a product of the same two factors, in either order. By type tests and references
    /// only.
    /// </summary>
    /// <param name="raw">A raw term accepted by <see cref="IsLinearChainTerm"/>.</param>
    /// <param name="prepared">Its prepared form.</param>
    /// <returns><see langword="true"/> when the prepared form keeps the invariant.</returns>
    private static bool IsPreparedGroupedTerm(Expression raw, Expression prepared)
    {
        if (ReferenceEquals(raw, prepared)) return true;

        // A finalization recreates a constant node around the same boxed value.
        if (raw is ConstantExpression rawConstant)
        {
            return prepared is ConstantExpression preparedConstant
                && ReferenceEquals(rawConstant.Type, preparedConstant.Type)
                && ReferenceEquals(rawConstant.Value, preparedConstant.Value);
        }

        if (raw is not BinaryExpression source || prepared is not BinaryExpression result || source.NodeType != result.NodeType) return false;
        if (!ReferenceEquals(source.Type, result.Type) || result.Method is not null) return false;
        return result.NodeType switch
        {
            ExpressionType.Divide => ReferenceEquals(source.Left, result.Left) && ReferenceEquals(source.Right, result.Right),
            ExpressionType.Multiply => (ReferenceEquals(source.Left, result.Left) && ReferenceEquals(source.Right, result.Right))
                || (ReferenceEquals(source.Left, result.Right) && ReferenceEquals(source.Right, result.Left)),
            _ => false,
        };
    }

    /// <summary>
    /// The fallback of <see cref="FlattenGroupedQuotientChain"/>: runs every crossed node of the chain on its prepared operands,
    /// children before their parent, as the traversal runs them once their operands are prepared (<see cref="TransformLevel"/>,
    /// then the <c>NaN</c> fold that the parent's <see cref="PrepareExpression"/> applies to each child result).
    /// </summary>
    /// <param name="root">The raw root of the chain.</param>
    /// <param name="prepared">The prepared terms, in source order (the order of the extraction, left to right).</param>
    /// <param name="dispatcher">The dispatcher of the chain's group.</param>
    /// <returns>The result of the root.</returns>
    /// <remarks>
    /// An explicit stack of frames, so that a deep chain never recurses here; the rules a level runs recurse as they do in the
    /// traversal. Each frame walk counts one structural visit per crossed node, like every structural walk of the engine.
    /// </remarks>
    private Expression TransformGroupedLevelByLevel(BinaryExpression root, Expression[] prepared, FlattenRuleDispatcher dispatcher)
    {
        var frames = new List<GroupedLevelFrame> { new(root) };
        int next = 0;
        Expression? returned = null;
        while (frames.Count > 0)
        {
            int top = frames.Count - 1;
            GroupedLevelFrame frame = frames[top];
            if (frame.State == 0)
            {
                ExpressionResourceBudget.ConsumeStructuralVisit();
                frame.State = 1;
                if (AsCrossedAdditiveNode(frame.Node.Left) is { } left)
                {
                    frames[top] = frame;
                    frames.Add(new GroupedLevelFrame(left));
                    continue;
                }

                frame.Left = prepared[next++];
            }
            else if (frame.State == 1)
            {
                // The left child just returned.
                frame.Left = FoldNaNOperand(returned!);
            }

            if (frame.State == 1)
            {
                frame.State = 2;
                if (AsCrossedAdditiveNode(frame.Node.Right) is { } right)
                {
                    frames[top] = frame;
                    frames.Add(new GroupedLevelFrame(right));
                    continue;
                }

                frame.Right = prepared[next++];
            }
            else
            {
                // The right child just returned.
                frame.Right = FoldNaNOperand(returned!);
            }

            returned = TransformLevel(dispatcher, frame.Node, frame.Left!, frame.Right!);
            frames.RemoveAt(top);
        }

        return returned!;
    }

    /// <summary>
    /// A crossed node of <see cref="TransformGroupedLevelByLevel"/> and its progress: data only, a mutable value kept in the
    /// list of frames.
    /// </summary>
    private struct GroupedLevelFrame
    {
        /// <summary>Creates the frame of a node not entered yet.</summary>
        /// <param name="node">The crossed node.</param>
        public GroupedLevelFrame(BinaryExpression node) => Node = node;

        /// <summary>The crossed node.</summary>
        public BinaryExpression Node;

        /// <summary><c>0</c> before its left operand, <c>1</c> while its left child runs, <c>2</c> while its right child runs.</summary>
        public int State;

        /// <summary>The prepared left operand, once known.</summary>
        public Expression? Left;

        /// <summary>The prepared right operand, once known.</summary>
        public Expression? Right;
    }
}
