using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Utils.Expressions;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// Why <see cref="ExpressionSimplifier"/>'s admission refused a chain as a spine chain (mission C3b,
/// <c>docs/expressions/SimplifierFlatten.md</c> §12), once its linear test had refused it for a repetition. Each cause lies at
/// one spine node and refuses every chain rooted above it. Data only (audits).
/// </summary>
internal enum FlattenSpineRefusalCause
{
    /// <summary>No refusal.</summary>
    None = 0,

    /// <summary>A crossed node has two crossed operands: the chain is not a comb.</summary>
    NotComb,

    /// <summary>A subtraction whose right operand is crossed.</summary>
    SubtractedGroup,

    /// <summary>A crossed node with two constant operands.</summary>
    ConstantPair,

    /// <summary>A term that is not a linear term, or the square <c>x * x</c> of a parameter.</summary>
    Term,

    /// <summary>A crossed node is not an ordinary operator.</summary>
    Operator,

    /// <summary>The two terms of the deepest node do not form a linear chain: a rule may apply to them.</summary>
    DeepestPair,

    /// <summary>
    /// The sub-chain below a node does not hold two monomials that are certainly not zero: the node's factoring rules could
    /// match.
    /// </summary>
    TooFewMonomials,

    /// <summary>The chain holds a third constant term.</summary>
    TooManyConstants,
}

public partial class ExpressionSimplifier
{
    /// <summary>Whether <paramref name="cause"/> is a repetition, the refusal a spine chain may overcome.</summary>
    /// <param name="cause">A refusal of <see cref="IsLinearChain"/>.</param>
    /// <returns><see langword="true"/> for a repeated parameter or coefficient, or a coefficient equal to a constant term.</returns>
    private static bool IsRepetition(FlattenRefusalCause cause)
        => cause is FlattenRefusalCause.RepeatedParameter or FlattenRefusalCause.RepeatedCoefficient or FlattenRefusalCause.CoefficientEqualsConstant;

    /// <summary>
    /// Whether the chain rooted at <paramref name="root"/>, refused as a linear chain for a repetition, is a <em>spine
    /// chain</em> (mission C3b): a chain on which no signature rule can apply at any level although terms repeat, so that its
    /// level by level result is the sequence of the finalizations of its levels (<see cref="FlattenSpineChain"/>).
    /// </summary>
    /// <param name="root">The raw root of the chain.</param>
    /// <param name="statistics">The opt-in counters of the thread, or <see langword="null"/>.</param>
    /// <param name="refusalDepth">
    /// Receives, for a refused chain, the spine index of the node where the cause lies: every chain rooted at a spine node
    /// above it, down to it, is refused for the same cause; <c>-1</c> for an admitted chain.
    /// </param>
    /// <returns><see langword="true"/> for a spine chain.</returns>
    /// <remarks>
    /// <para>The conditions:</para>
    /// <list type="bullet">
    /// <item>the chain is a <em>comb</em>: every crossed node has at most one crossed operand, so every level is a node
    /// whose operands are the result of the level below and one term;</item>
    /// <item>the local conditions of a linear chain hold (ordinary operators, no subtracted group, no constant pair), every
    /// term is a linear term other than the square <c>x * x</c> of a parameter (which its preparation turns into a power), and
    /// the chain holds at most two constant terms;</item>
    /// <item>the two terms of the deepest node form a linear chain (<see cref="LinearChainOperands"/>): no rule applies to
    /// them (§3.2);</item>
    /// <item>above it, the sub-chain below every node holds at least two <em>monomials</em> whose coefficient is certainly
    /// not zero (<see cref="SpineMonomials"/>).</item>
    /// </list>
    /// <para>
    /// The last condition is the one that tolerates repetitions. The prepared operand of such a node that comes from below is
    /// then a canonical sum of at least two groups, an ordinary <c>Add</c> node, and its other operand a prepared linear term:
    /// a parameter, a product or a non-zero constant. No rule can match: the zero, constant and negation rules need a zero, a
    /// pair of constants or a negation; the subtraction rules need a sum or a difference as their right operand; the
    /// logarithm and trigonometric rules need calls; the factoring rules augment the sum to <c>1 * sum</c> and only compare
    /// it, or <c>1</c>, with a factor of the term (a parameter or a constant), and a sum of two monomials with non-zero
    /// coefficients is equal neither to a parameter nor to a constant, whatever the comparer simplifies it to.
    /// </para>
    /// <para>
    /// One iterative walk of the spine (one structural visit per node), with type tests before any property of a node is
    /// read, as <see cref="IsLinearChain"/> does, then one pass over the collected terms from the deepest node up. Only
    /// parameter references and boxed primitives are compared: no user code runs.
    /// </para>
    /// </remarks>
    private bool IsSpineChain(BinaryExpression root, FlattenAdmissionStatistics? statistics, out int refusalDepth)
    {
        if (statistics is not null) statistics.SpineWalks++;
        Type type = root.Type;
        var nodes = new List<BinaryExpression>();
        BinaryExpression node = root;
        while (true)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            if (statistics is not null) statistics.NodesExamined++;
            int depth = nodes.Count;
            BinaryExpression? left = AsCrossedAdditiveNode(node.Left);
            BinaryExpression? right = AsCrossedAdditiveNode(node.Right);
            FlattenSpineRefusalCause cause = FlattenSpineRefusalCause.None;
            if (left is not null && right is not null) cause = FlattenSpineRefusalCause.NotComb;
            else if (right is not null && node.NodeType == ExpressionType.Subtract) cause = FlattenSpineRefusalCause.SubtractedGroup;
            else if (node.Left is ConstantExpression && node.Right is ConstantExpression) cause = FlattenSpineRefusalCause.ConstantPair;
            else if ((left is null && !IsSpineTerm(node.Left, type)) || (right is null && !IsSpineTerm(node.Right, type))) cause = FlattenSpineRefusalCause.Term;
            else if (!IsOrdinaryBinaryArithmetic(node)) cause = FlattenSpineRefusalCause.Operator;

            if (cause != FlattenSpineRefusalCause.None) return RefuseSpine(cause, depth, statistics, out refusalDepth);
            nodes.Add(node);
            if ((left ?? right) is not { } next) break;
            node = next;
        }

        int deepest = nodes.Count - 1;
        BinaryExpression bottom = nodes[deepest];
        var pair = new LinearChainOperands();
        if (!pair.Add(bottom.Left, deepest, out _) || !pair.Add(bottom.Right, deepest, out _))
        {
            return RefuseSpine(FlattenSpineRefusalCause.DeepestPair, deepest, statistics, out refusalDepth);
        }

        var monomials = new SpineMonomials(type);
        monomials.Add(bottom.Left, false);
        monomials.Add(bottom.Right, bottom.NodeType == ExpressionType.Subtract);
        for (int index = deepest - 1; index >= 0; index--)
        {
            if (monomials.CertainlyNonZero < 2) return RefuseSpine(FlattenSpineRefusalCause.TooFewMonomials, index, statistics, out refusalDepth);
            (Expression term, bool isNegative) = SpineTermOf(nodes[index]);
            if (!monomials.Add(term, isNegative)) return RefuseSpine(FlattenSpineRefusalCause.TooManyConstants, index, statistics, out refusalDepth);
        }

        refusalDepth = -1;
        return true;
    }

    /// <summary>
    /// Records the spine nodes of a refused spine root from index <c>1</c> down to the node where the cause lies
    /// (<paramref name="depth"/>): each of them roots a chain holding that cause, which the spine test refuses too.
    /// </summary>
    /// <param name="root">The refused root.</param>
    /// <param name="depth">The spine index of the cause (at least one).</param>
    /// <param name="session">The session of the running operation, which owns the table.</param>
    /// <param name="statistics">The opt-in counters of the thread, or <see langword="null"/>.</param>
    /// <remarks>
    /// The nodes from the root down to the cause have a single crossed operand (the walk stopped at the first node with two),
    /// so the pass follows it, with one structural visit per node; a refusal in the middle of it leaves a correct table, since
    /// every recorded node is a proven spine refusal.
    /// </remarks>
    private void RecordSpineRefusals(BinaryExpression root, int depth, ExpressionResourceBudget.Session session, FlattenAdmissionStatistics? statistics)
    {
        ExpressionNodeSet refusals = session.FlattenSpineRefusals ??= new ExpressionNodeSet();
        BinaryExpression node = root;
        for (int index = 1; index <= depth; index++)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            node = AsCrossedAdditiveNode(node.Left) ?? AsCrossedAdditiveNode(node.Right)!;
            refusals.Add(node);
            if (statistics is not null) statistics.RecordingVisits++;
        }
    }

    /// <summary>Reports a refusal of <see cref="IsSpineChain"/>.</summary>
    /// <param name="cause">The cause.</param>
    /// <param name="depth">The spine index of the node where it lies.</param>
    /// <param name="statistics">The opt-in counters of the thread, or <see langword="null"/>.</param>
    /// <param name="refusalDepth">Receives <paramref name="depth"/>.</param>
    /// <returns><see langword="false"/>.</returns>
    private static bool RefuseSpine(FlattenSpineRefusalCause cause, int depth, FlattenAdmissionStatistics? statistics, out int refusalDepth)
    {
        if (statistics is not null) statistics.SpineCauses[(int)cause]++;
        refusalDepth = depth;
        return false;
    }

    /// <summary>Whether <paramref name="operand"/> may be a term of a spine chain: a linear term other than the square of a parameter.</summary>
    /// <param name="operand">A raw operand that is not crossed.</param>
    /// <param name="type">The type of the chain.</param>
    /// <returns><see langword="true"/> for an accepted term.</returns>
    private static bool IsSpineTerm(Expression operand, Type type)
        => IsLinearTerm(operand, type)
            && !(operand is BinaryExpression { Left: ParameterExpression left, Right: ParameterExpression right } && ReferenceEquals(left, right));

    /// <summary>The term of a spine node that is not its deepest node, and its sign: the operand that is not crossed.</summary>
    /// <param name="node">A spine node with exactly one crossed operand.</param>
    /// <returns>The term, and whether it is subtracted (the right operand of a subtraction).</returns>
    private (Expression Term, bool IsNegative) SpineTermOf(BinaryExpression node)
        => AsCrossedAdditiveNode(node.Left) is not null
            ? (node.Right, node.NodeType == ExpressionType.Subtract)
            : (node.Left, false);

    /// <summary>
    /// Simplifies a spine chain admitted by <see cref="IsSpineChain"/> (mission C3b): its terms are prepared once each, in
    /// source order; its deepest linear sub-chain is canonicalized at once; every level above is its finalization, the
    /// canonicalization of its two prepared operands, without its signature rules, which provably do not apply.
    /// </summary>
    /// <param name="root">The raw root of the chain.</param>
    /// <returns>The result the level by level treatment gives for <paramref name="root"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Why it is the level by level result.</b> The sub-chain rooted at the deepest node from which every term is still a
    /// distinct linear term (<see cref="LinearChainOperands"/>) is a linear chain: level by level it ends with the
    /// canonicalization of its prepared terms in source order (§3), which is computed here directly. Every node above it has
    /// a prepared operand from below that is a canonical sum of at least two groups and a prepared linear term as its other
    /// operand, on which no signature rule applies (<see cref="IsSpineChain"/>): the level by level treatment of that node is
    /// <see cref="FinalizeExpression"/>, the canonicalization of the terms collected from its two operands in their order,
    /// which is what each level runs here, on the very operands the level by level treatment gives it (the child result
    /// through the <c>NaN</c> fold of its parent's preparation). No composition of canonicalizations is assumed: the result
    /// of every level is computed as level by level, so the repeated terms are grouped and cancelled level by level too.
    /// </para>
    /// <para>
    /// <b>What is saved.</b> The admission walks of the sub-chains, the rule dispatch of every level and the factor
    /// comparisons of the factoring rules (each of them simplified the sub-chain again through the comparer). What remains
    /// is the preparation of each term once and one canonicalization per level above the linear sub-chain, whose keys and
    /// hashes are stored in the operation's metadata table for the next level (mission C3a); the root stores nothing.
    /// </para>
    /// <para>
    /// <b>Order, user code, budgets.</b> The terms are inert linear terms: their preparations and the canonicalizations run
    /// no user code and cannot throw, so the order is only that of the traces. One transformation visit is counted for the
    /// linear sub-chain and for every level below the root (the engine counted the root's), as the level by level treatment
    /// counts one per node treatment; the walk that collects the spine counts one structural visit per node.
    /// </para>
    /// </remarks>
    private Expression FlattenSpineChain(BinaryExpression root)
    {
        Type type = root.Type;
        var nodes = new List<BinaryExpression>();
        var operands = new List<(Expression Operand, bool IsNegative, int Node)>();
        var suffix = new List<(Expression Operand, bool IsNegative, int Node)>();
        BinaryExpression node = root;
        while (true)
        {
            // A walk of its own (the admission and the extraction counted theirs): one structural visit per spine node.
            ExpressionResourceBudget.ConsumeStructuralVisit();
            int index = nodes.Count;
            nodes.Add(node);
            bool negativeRight = node.NodeType == ExpressionType.Subtract;
            BinaryExpression? left = AsCrossedAdditiveNode(node.Left);
            BinaryExpression? right = left is null ? AsCrossedAdditiveNode(node.Right) : null;
            if (left is null && right is null)
            {
                operands.Add((node.Left, false, index));
                operands.Add((node.Right, negativeRight, index));
                break;
            }

            // A spine node: its term comes before the deeper operands when the spine goes right, after them when it goes left.
            if (left is not null)
            {
                suffix.Add((node.Right, negativeRight, index));
                node = left;
            }
            else
            {
                operands.Add((node.Left, false, index));
                node = right!;
            }
        }

        for (int i = suffix.Count - 1; i >= 0; i--) operands.Add(suffix[i]);

        // The deepest linear sub-chain: the terms from the deepest node up, while they are distinct linear terms.
        int deepest = nodes.Count - 1;
        var linear = new LinearChainOperands();
        linear.Add(nodes[deepest].Left, deepest, out _);
        linear.Add(nodes[deepest].Right, deepest, out _);
        int bottom = 0;
        for (int index = deepest - 1; index >= 0; index--)
        {
            if (!linear.Add(SpineTermOf(nodes[index]).Term, index, out _))
            {
                bottom = index + 1;
                break;
            }
        }

        var prepared = new Expression[operands.Count];
        var termOf = new int[bottom];
        for (int i = 0; i < prepared.Length; i++)
        {
            // Mission C3a: the prepared term is the end of this instance's treatment of the operand (LocallySimplified).
            prepared[i] = PrepareExpression(operands[i].Operand);
            RecordLocallySimplified(prepared[i]);
            if (operands[i].Node < bottom) termOf[operands[i].Node] = i;
        }

        // The linear sub-chain, as the level by level treatment hands it to this rule; its keys and hashes are stored for the
        // first level above it, which collects the same term instances again.
        var signedTerms = new List<(Expression Term, bool IsNegative)>(operands.Count + 1);
        for (int i = 0; i < prepared.Length; i++)
        {
            if (operands[i].Node >= bottom) CollectAdditiveTerms(signedTerms, prepared[i], operands[i].IsNegative);
        }

        if (bottom > 0) ExpressionResourceBudget.ConsumeTransformVisit();
        Expression result = CanonicalizeAdditiveTerms(signedTerms, type, recordMetadata: bottom > 0);

        FlattenAdmissionStatistics? statistics = t_admissionStatistics;
        if (statistics is not null) statistics.SpineLevels += bottom;
        var pending = new Stack<(Expression Node, bool IsNegative)>();
        for (int index = bottom - 1; index >= 0; index--)
        {
            if (index > 0) ExpressionResourceBudget.ConsumeTransformVisit();
            Expression child = FoldNaNOperand(result);
            BinaryExpression level = nodes[index];
            int term = termOf[index];
            signedTerms.Clear();
            if (ReferenceEquals(level.Left, nodes[index + 1]))
            {
                CollectAdditiveTermsIteratively(signedTerms, child, pending);
                CollectAdditiveTerms(signedTerms, prepared[term], operands[term].IsNegative);
            }
            else
            {
                CollectAdditiveTerms(signedTerms, prepared[term], false);
                CollectAdditiveTermsIteratively(signedTerms, child, pending);
            }

            // The finalization of the level (CanonicalizeAdditiveExpression of its two operands); the root stores nothing.
            result = CanonicalizeAdditiveTerms(signedTerms, type, recordMetadata: index > 0);
        }

        return result;
    }

    /// <summary>
    /// <see cref="CollectAdditiveTerms"/> of a positive <paramref name="expression"/> without recursion: the same terms, signs
    /// and order (an in-order walk through ordinary <c>Add</c>, <c>Subtract</c> and <c>Negate</c> nodes), with an explicit
    /// stack, so that collecting the canonical result of a level, a right-associated chain as long as the chain, does not use
    /// one frame of the execution stack per term.
    /// </summary>
    /// <param name="terms">Receives the signed terms.</param>
    /// <param name="expression">The canonical result of the level below.</param>
    /// <param name="pending">An empty work stack, reused between levels; left empty.</param>
    private static void CollectAdditiveTermsIteratively(List<(Expression Term, bool IsNegative)> terms, Expression expression, Stack<(Expression Node, bool IsNegative)> pending)
    {
        pending.Push((expression, false));
        while (pending.Count > 0)
        {
            (Expression node, bool isNegative) = pending.Pop();
            if (node is BinaryExpression binary && IsOrdinaryBinaryArithmetic(binary))
            {
                if (binary.NodeType == ExpressionType.Add)
                {
                    pending.Push((binary.Right, isNegative));
                    pending.Push((binary.Left, isNegative));
                    continue;
                }

                if (binary.NodeType == ExpressionType.Subtract)
                {
                    pending.Push((binary.Right, !isNegative));
                    pending.Push((binary.Left, isNegative));
                    continue;
                }
            }

            if (node is UnaryExpression unary && IsOrdinaryUnaryNegate(unary))
            {
                pending.Push((unary.Operand, !isNegative));
                continue;
            }

            terms.Add((node, isNegative));
        }
    }

    /// <summary>
    /// The monomials of the sub-chain below the current level of <see cref="IsSpineChain"/>, and how many of them certainly
    /// have a non-zero coefficient. A monomial is a parameter, an unordered pair of parameters (the product <c>x * y</c>), or
    /// the constant monomial; the coefficient of a term is its constant factor, or one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A monomial is <em>certainly non-zero</em> while all its occurrences have the same effective sign (the sign of the term
    /// times the sign of its coefficient): their sum cannot be zero. For an integral type, whose arithmetic wraps, the sum of
    /// their magnitudes must also stay below <c>2^(bits - 1)</c>; for a floating-point type a sum of same-sign values is never
    /// zero. A monomial that loses the property never gets it back. The constant monomial counts only while the sub-chain
    /// holds a single constant term (non-zero by the admission); two constants may cancel.
    /// </para>
    /// <para>
    /// Parameters are compared by reference and coefficients are boxed primitives of the chain type, read without any
    /// conversion that could throw: no user code runs.
    /// </para>
    /// </remarks>
    private sealed class SpineMonomials
    {
        /// <summary>The state of each monomial met.</summary>
        private readonly Dictionary<MonomialKey, MonomialState> _monomials = [];

        /// <summary>The magnitude bound of an integral type; <see cref="Int128.MaxValue"/> for a floating-point type.</summary>
        private readonly Int128 _bound;

        /// <summary>The number of constant terms met.</summary>
        private int _constants;

        /// <summary>The number of non-constant monomials whose coefficient is certainly not zero.</summary>
        private int _certain;

        /// <summary>Initializes an empty set for a chain of <paramref name="type"/>.</summary>
        /// <param name="type">A flattened chain type.</param>
        public SpineMonomials(Type type)
        {
            _bound = ReferenceEquals(type, typeof(int)) || ReferenceEquals(type, typeof(uint)) ? (Int128)1 << 31
                : ReferenceEquals(type, typeof(long)) || ReferenceEquals(type, typeof(ulong)) ? (Int128)1 << 63
                : Int128.MaxValue;
        }

        /// <summary>Gets the number of monomials whose coefficient is certainly not zero, the constant monomial included.</summary>
        public int CertainlyNonZero => _certain + (_constants == 1 ? 1 : 0);

        /// <summary>Records a term of the sub-chain.</summary>
        /// <param name="term">A spine term (<see cref="IsSpineTerm"/>).</param>
        /// <param name="isNegative">Whether it is subtracted.</param>
        /// <returns><see langword="false"/> when it is the third constant term.</returns>
        public bool Add(Expression term, bool isNegative)
        {
            MonomialKey key;
            object? coefficient = null;
            switch (term)
            {
                case ConstantExpression:
                    return ++_constants <= 2;
                case ParameterExpression parameter:
                    key = new MonomialKey(parameter, null);
                    break;
                default:
                    var product = (BinaryExpression)term;
                    if (product.Left is ConstantExpression leftCoefficient)
                    {
                        coefficient = leftCoefficient.Value;
                        key = new MonomialKey((ParameterExpression)product.Right, null);
                    }
                    else if (product.Right is ConstantExpression rightCoefficient)
                    {
                        coefficient = rightCoefficient.Value;
                        key = new MonomialKey((ParameterExpression)product.Left, null);
                    }
                    else
                    {
                        key = new MonomialKey((ParameterExpression)product.Left, (ParameterExpression)product.Right);
                    }

                    break;
            }

            Magnitude(coefficient, out bool negativeCoefficient, out Int128 magnitude);
            bool negative = isNegative != negativeCoefficient;
            if (!_monomials.TryGetValue(key, out MonomialState state))
            {
                bool certain = magnitude < _bound;
                if (certain) _certain++;
                _monomials.Add(key, new MonomialState(negative, magnitude, !certain));
                return true;
            }

            if (state.Broken) return true;
            Int128 total = state.Magnitude + magnitude;
            if (negative != state.Negative || total >= _bound)
            {
                _certain--;
                _monomials[key] = state with { Broken = true };
            }
            else
            {
                _monomials[key] = state with { Magnitude = total };
            }

            return true;
        }

        /// <summary>The sign and the magnitude of a coefficient.</summary>
        /// <param name="coefficient">A boxed primitive of the chain type, or <see langword="null"/> for an implicit one.</param>
        /// <param name="negative">Receives whether it is negative.</param>
        /// <param name="magnitude">Receives its absolute value for an integral type, otherwise zero.</param>
        private static void Magnitude(object? coefficient, out bool negative, out Int128 magnitude)
        {
            switch (coefficient)
            {
                case null:
                    negative = false;
                    magnitude = 1;
                    return;
                case double value:
                    negative = value < 0;
                    magnitude = 0;
                    return;
                case float value:
                    negative = value < 0;
                    magnitude = 0;
                    return;
                case int value:
                    negative = value < 0;
                    magnitude = Int128.Abs(value);
                    return;
                case long value:
                    negative = value < 0;
                    magnitude = Int128.Abs(value);
                    return;
                case uint value:
                    negative = false;
                    magnitude = value;
                    return;
                default:
                    negative = false;
                    magnitude = (ulong)coefficient;
                    return;
            }
        }

        /// <summary>The state of one monomial.</summary>
        /// <param name="Negative">The effective sign of its occurrences.</param>
        /// <param name="Magnitude">The sum of the magnitudes of their coefficients (integral types).</param>
        /// <param name="Broken">Whether its coefficient may be zero.</param>
        private readonly record struct MonomialState(bool Negative, Int128 Magnitude, bool Broken);

        /// <summary>A monomial: one parameter, or an unordered pair of parameters, compared by reference.</summary>
        /// <param name="First">The first parameter.</param>
        /// <param name="Second">The second parameter of a product, or <see langword="null"/>.</param>
        private readonly record struct MonomialKey(ParameterExpression First, ParameterExpression? Second)
        {
            /// <summary>Whether both keys are the same monomial: the same parameters by reference, in either order.</summary>
            /// <param name="other">The other key.</param>
            /// <returns><see langword="true"/> for the same monomial.</returns>
            public bool Equals(MonomialKey other)
                => (ReferenceEquals(First, other.First) && ReferenceEquals(Second, other.Second))
                    || (Second is not null && ReferenceEquals(First, other.Second) && ReferenceEquals(Second, other.First));

            /// <summary>A hash consistent with <see cref="Equals(MonomialKey)"/>, from the identity hashes of the parameters.</summary>
            /// <returns>The hash.</returns>
            public override int GetHashCode()
                => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(First)
                    ^ (Second is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Second));
        }
    }
}
