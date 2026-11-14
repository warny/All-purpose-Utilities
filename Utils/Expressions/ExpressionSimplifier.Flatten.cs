using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Utils.Expressions;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// The flatten rule of <see cref="ExpressionSimplifier"/> (see <c>docs/expressions/SimplifierFlatten.md</c>): a sum or
/// difference of distinct linear terms is canonicalized once, from the terms of the whole chain, instead of once per level.
/// </summary>
public partial class ExpressionSimplifier
{
    /// <summary>
    /// Canonicalizes a whole <em>linear chain</em> at once: each term is prepared once, in source order, and the prepared
    /// terms are given to the code that canonicalizes the root of the chain level by level.
    /// </summary>
    /// <param name="source">The raw root of the chain: an ordinary <c>Add</c> or <c>Subtract</c> node.</param>
    /// <param name="terms">The raw signed terms of the chain, in source order.</param>
    /// <returns>The canonical additive form of the chain; never <see langword="null"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Admission.</b> The rule only receives the chains <see cref="FlattenAdmission"/> admitted, the linear chains
    /// (<see cref="IsLinearChain"/>): the engine asks the admission of every chain root before it extracts anything, at the
    /// single place that invokes a flatten rule. The rule therefore never declines, and nothing is prepared before the
    /// decision. Every other chain is simplified level by level, exactly as before the rule existed.
    /// </para>
    /// <para>
    /// <b>Why a restricted family.</b> Level by level, the result of a chain is not the canonicalization of its terms in
    /// general: the rules of every inner node see the canonical form of its two operands, so the result depends on the
    /// grouping of the source (<c>a + a</c> is <c>2 * a</c> but <c>a + (b + a)</c> is <c>(a + a) + b</c>;
    /// <c>2 * a + 2 * b</c> is <c>(a + b) * 2</c>; <c>a - (b + c)</c> is <c>(a - b) - c</c>), and terms whose keys tie keep
    /// an order that depends on it too. On a linear chain no rule applies at any level and the per-level stable sorts compose
    /// into the stable sort of the source order, so the level by level result is the canonicalization of the terms.
    /// </para>
    /// <para>
    /// The <see cref="FlattenAttribute"/> declares <c>Add</c> and <c>Subtract</c> only: a negation is not crossed, because
    /// a chain rooted at a negation is not canonicalized level by level (<c>-(a + b)</c> stays as is) and a negated operand
    /// goes through <see cref="AdditionWithNegate(BinaryExpression, Expression, UnaryExpression)"/>, whose result depends
    /// on the grouping. A negation is therefore an operand, which is not a linear term.
    /// </para>
    /// <para>
    /// <b>Preparation.</b> Each term goes through <see cref="PrepareExpression"/> (the transformation followed by the
    /// <c>NaN</c> fold), exactly as its parent node prepares it level by level, and through
    /// <see cref="CollectAdditiveTerms"/>, as the canonicalization of the root collects it.
    /// </para>
    /// <para>
    /// <b>Derived simplifiers.</b> The rule is <see langword="private"/>: the plan of a derived simplifier does not hold
    /// it, so a subclass keeps the level by level treatment, with its own <see cref="PrepareExpression"/>,
    /// <see cref="FinalizeExpression"/> and rules.
    /// </para>
    /// <para>
    /// <b>Policies.</b> The operands of a linear chain are inert, so the effect gate admits every rule on it under every
    /// <see cref="Options"/> and the result does not depend on the policy.
    /// </para>
    /// </remarks>
    [Flatten(ExpressionType.Add, ExpressionType.Subtract)]
    private Expression? FlattenAdditiveChain(Expression source, FlattenedTerm[] terms)
    {
        var signedTerms = new List<(Expression Term, bool IsNegative)>(terms.Length);
        foreach (FlattenedTerm term in terms)
        {
            CollectAdditiveTerms(signedTerms, PrepareExpression(term.Operand), term.Operation == ExpressionType.Subtract);
        }

        return CanonicalizeAdditiveTerms(signedTerms, source.Type);
    }

    /// <summary>
    /// Admits to <see cref="FlattenAdditiveChain"/> the linear chains only (<see cref="IsLinearChain"/>), for the built-in
    /// simplifier; a derived simplifier, whose plan does not hold that rule, keeps the effect gate for its own flatten rules.
    /// </summary>
    /// <param name="root">The raw root of a chain.</param>
    /// <returns>
    /// <see cref="ExpressionRuleRejection.None"/> for a linear chain, <see cref="ExpressionRuleRejection.ChainNotAdmitted"/>
    /// for any other chain of the built-in type; the answer of the effect gate for a derived simplifier.
    /// </returns>
    /// <remarks>
    /// The effect gate is not asked for the built-in type: a linear chain only holds parameters, primitive constants and
    /// ordinary products of them under ordinary operators of a type that cannot throw, a subset of what
    /// <see cref="ExpressionEffectAnalysis.HasInertOperands"/> proves inert. The gate would analyze the operands of the root
    /// recursively (one more walk per chain, and a chain depth bounded by the execution stack), whereas the test of a linear
    /// chain is iterative, stops at the first operand that is not linear, and reads framework operators and leaves only.
    /// Deciding here, before the extraction, also keeps a chain that is not linear from being extracted at all: a shared
    /// sub-chain, which holds a parameter twice, is refused when the second occurrence is met.
    /// <para>
    /// <b>Known refusals (mission C1).</b> A refused chain is transformed level by level, and each crossed node below its
    /// root is offered in turn as the root of a chain of its own: when the cause of the refusal lies deep in a long chain,
    /// every level walked down to it again (quadratic in the depth). A refusal is inherited by every chain that contains its
    /// cause (<see cref="IsLinearChain"/>), so the walk that finds it records, in the operation's table
    /// (<see cref="ExpressionResourceBudget.Session.FlattenRefusals"/>), the crossed nodes below the root that contain the
    /// cause; such a node, offered later, is refused by one lookup instead of a walk. Nothing else is recorded: an admitted
    /// chain says nothing about a larger one, and a node that does not contain the cause is walked as before. The table
    /// lives as long as the outermost operation of the thread, like the effect table.
    /// </para>
    /// </remarks>
    internal override ExpressionRuleRejection FlattenAdmission(Expression root)
    {
        if (GetType() != typeof(ExpressionSimplifier)) return base.FlattenAdmission(root);
        if (root is not BinaryExpression binary) return ExpressionRuleRejection.ChainNotAdmitted;

        FlattenAdmissionStatistics? statistics = t_admissionStatistics;
        if (statistics is not null) statistics.Admissions++;
        ExpressionResourceBudget.Session? session = ExpressionResourceBudget.ActiveSession;
        if (session?.FlattenRefusals is { } refusals && refusals.Contains(binary))
        {
            // The lookup replaces a walk of at least one node: it is counted as one.
            ExpressionResourceBudget.ConsumeStructuralVisit();
            if (statistics is not null)
            {
                statistics.KnownRefusals++;
                statistics.Causes[(int)FlattenRefusalCause.KnownRefusal]++;
            }

            return ExpressionRuleRejection.ChainNotAdmitted;
        }

        bool linear = IsLinearChain(binary, statistics, out ChainRefusal refusal);
        if (statistics is not null)
        {
            if (linear) statistics.Accepted++;
            else statistics.Causes[(int)refusal.Cause]++;
        }

        if (!linear && refusal.Depth > 0 && session is not null) RecordRefusedSpine(binary, refusal, session, statistics);
        return linear ? ExpressionRuleRejection.None : ExpressionRuleRejection.ChainNotAdmitted;
    }

    /// <summary>
    /// Records as refused the crossed nodes of the spine of <paramref name="root"/> (see <see cref="IsLinearChain"/>) whose
    /// chain contains the cause of <paramref name="refusal"/>: every spine node from index <c>1</c> down to the smallest
    /// spine index involved in the refusal, so that each of them, offered later as a root, is refused by one lookup.
    /// </summary>
    /// <param name="root">The refused root.</param>
    /// <param name="refusal">The refusal reported by the walk; its depth is at least one.</param>
    /// <param name="session">The session of the running operation, which owns the table.</param>
    /// <param name="statistics">The opt-in counters of the thread, or <see langword="null"/>.</param>
    /// <remarks>
    /// <para>
    /// The pass follows the spine from the root, with one structural visit per node, and stops at the first of: the spine
    /// index reported by the walk (the refusing node for a local cause or a repetition, the first constant for a third one);
    /// the first node that holds an earlier occurrence of the repeated key (<see cref="HoldsOccurrence"/>; the node at index
    /// <c>i</c> is recorded before its own terms are tested, since an occurrence at an index at least <c>i</c> lies in its
    /// chain); the first node with two crossed operands, whose chain holds everything the walk visited afterwards. A node
    /// with a single crossed operand leads to it, as in the walk.
    /// </para>
    /// <para>
    /// Finding the earlier occurrence here, only on a refusal, keeps the sets of the walk free of any bookkeeping (an
    /// admitted chain allocates exactly what it did before). Only spine nodes and the linear terms the walk already accepted
    /// are read, by type tests and framework properties. Every recorded node is a proven refusal, so an exception (a budget
    /// refusal) in the middle of the pass leaves a correct table.
    /// </para>
    /// </remarks>
    private void RecordRefusedSpine(BinaryExpression root, in ChainRefusal refusal, ExpressionResourceBudget.Session session, FlattenAdmissionStatistics? statistics)
    {
        if (refusal.Key is not null && HoldsOccurrence(root, refusal)) return;

        ExpressionNodeSet refusals = session.FlattenRefusals ??= new ExpressionNodeSet();
        BinaryExpression node = root;
        for (int index = 1; index <= refusal.Depth; index++)
        {
            BinaryExpression? left = AsCrossedAdditiveNode(node.Left);
            BinaryExpression? right = AsCrossedAdditiveNode(node.Right);
            if (left is not null && right is not null) return;

            ExpressionResourceBudget.ConsumeStructuralVisit();
            node = (left ?? right)!;
            bool added = refusals.Add(node);
            if (statistics is not null)
            {
                statistics.RecordingVisits++;
                if (added) statistics.RecordedRefusals++;
            }

            if (refusal.Key is not null && HoldsOccurrence(node, refusal)) return;
        }
    }

    /// <summary>
    /// Whether a term of <paramref name="node"/> (an operand that is not crossed) holds an occurrence of the key of a
    /// repetition: the parameter, as a term or as a factor; the coefficient value; or, for a coefficient equal to a constant,
    /// that value as a coefficient or as a constant term.
    /// </summary>
    /// <param name="node">A spine node whose terms the walk accepted as linear.</param>
    /// <param name="refusal">A refusal by repetition, with its key.</param>
    /// <returns><see langword="true"/> when one of its terms holds the key.</returns>
    private bool HoldsOccurrence(BinaryExpression node, in ChainRefusal refusal)
        => (AsCrossedAdditiveNode(node.Left) is null && TermHolds(node.Left, refusal))
            || (AsCrossedAdditiveNode(node.Right) is null && TermHolds(node.Right, refusal));

    /// <summary>Whether the linear term <paramref name="term"/> holds the key of <paramref name="refusal"/> (see <see cref="HoldsOccurrence"/>).</summary>
    /// <param name="term">A term accepted by <see cref="IsLinearTerm"/>.</param>
    /// <param name="refusal">A refusal by repetition, with its key.</param>
    /// <returns><see langword="true"/> when the term holds the key.</returns>
    /// <remarks>Keys are compared as the sets of the walk compare them (<see cref="object.Equals(object?, object?)"/>).</remarks>
    private static bool TermHolds(Expression term, in ChainRefusal refusal)
    {
        object key = refusal.Key!;
        return (refusal.Cause, term) switch
        {
            (FlattenRefusalCause.RepeatedParameter, ParameterExpression parameter) => ReferenceEquals(parameter, key),
            (FlattenRefusalCause.RepeatedParameter, BinaryExpression product) => ReferenceEquals(product.Left, key) || ReferenceEquals(product.Right, key),
            (FlattenRefusalCause.RepeatedCoefficient, BinaryExpression product) => FactorHolds(product.Left, key) || FactorHolds(product.Right, key),
            (FlattenRefusalCause.CoefficientEqualsConstant, ConstantExpression constant) => Equals(constant.Value, key),
            (FlattenRefusalCause.CoefficientEqualsConstant, BinaryExpression product) => FactorHolds(product.Left, key) || FactorHolds(product.Right, key),
            _ => false,
        };
    }

    /// <summary>Whether a factor of a linear product is a constant coefficient of value <paramref name="key"/>.</summary>
    /// <param name="factor">A parameter or a constant.</param>
    /// <param name="key">A boxed primitive value.</param>
    /// <returns><see langword="true"/> for a constant of that value.</returns>
    private static bool FactorHolds(Expression factor, object key) => factor is ConstantExpression constant && Equals(constant.Value, key);

    /// <summary>
    /// Why <see cref="IsLinearChain"/> refused a chain, and where: data only.
    /// </summary>
    /// <param name="Cause">The cause.</param>
    /// <param name="Depth">
    /// The spine index of the deepest node known to contain the cause: the refusing node for a local cause, the first
    /// constant for a third one, and for a repetition the refusing node, an upper bound of the earlier occurrence; <c>0</c>
    /// when only the root is known to be refused.
    /// </param>
    /// <param name="Key">The repeated key (a parameter or a boxed coefficient value) for a repetition, otherwise <see langword="null"/>.</param>
    private readonly record struct ChainRefusal(FlattenRefusalCause Cause, int Depth, object? Key);

    /// <summary>The opt-in admission counters of this thread (<see cref="AdmissionStatistics"/>).</summary>
    [ThreadStatic]
    private static FlattenAdmissionStatistics? t_admissionStatistics;

    /// <summary>
    /// Gets or sets the admission counters of this thread: audits and benchmarks only. While <see langword="null"/> (the
    /// default), the admission reads this field once per chain root and records nothing.
    /// </summary>
    internal static FlattenAdmissionStatistics? AdmissionStatistics
    {
        get => t_admissionStatistics;
        set => t_admissionStatistics = value;
    }

    /// <summary>
    /// Whether the additive chain rooted at <paramref name="root"/> is a linear chain, the family on which the level by
    /// level treatment provably ends with the canonicalization of all the terms:
    /// </summary>
    /// <param name="root">The raw root of the chain: a crossed <c>Add</c> or <c>Subtract</c> node.</param>
    /// <param name="statistics">The opt-in counters of the thread, or <see langword="null"/>.</param>
    /// <param name="refusal">
    /// Receives, for a refused chain, its cause and where it lies on the spine (see the remarks); the default value for an
    /// admitted chain.
    /// </param>
    /// <returns><see langword="true"/> for a linear chain.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>the chain has one of the types of <see cref="IsFlattenedChainType"/> and every crossed node is an ordinary
    /// operator;</item>
    /// <item>no subtraction has a crossed right operand (<see cref="SubstractionWithAddition"/> and
    /// <see cref="SubstractionWithSubstraction"/> return an uncanonicalized difference and reorder its terms), and no
    /// crossed node has two constant operands (constant folding, and the zero identities that follow a zero sum);</item>
    /// <item>every term is a parameter, a finite non-zero constant, or the product of a parameter by another parameter or
    /// by a finite constant other than <c>0</c>, <c>1</c> and <c>-1</c> (<see cref="IsLinearTerm"/>): preparing such a
    /// term runs no user code and gives a parameter, a constant or a product, never a sum, a negation or a zero;</item>
    /// <item>no parameter occurs twice in the chain, the constant coefficients differ from each other and from the constant
    /// terms: the factor comparisons of <see cref="AdditionOfEqualsElements"/> and
    /// <see cref="SubstractionOfEqualsElements"/> then fail at every level (two operands never hold the same parameter,
    /// and the simplifier never introduces one), nothing cancels and every group holds one term;</item>
    /// <item>the chain holds at most two constant terms: level by level, constants are summed only when a level holds two of
    /// them, so with three or more the result depends on the grouping (a floating-point sum rounds according to it, and
    /// when a partial sum is zero the next constant stays alone: <c>((a + 5) - 5) - 3</c> keeps <c>0 - 3</c> where the sum
    /// of the three constants is <c>-3</c>).</item>
    /// </list>
    /// <para>
    /// One iterative walk of the crossed nodes, one structural visit per node, which stops at the first refusal: a chain
    /// whose first operands are not linear costs a few type tests and allocates nothing. The walk is linear in the number of
    /// distinct nodes even on a shared DAG: a shared sub-chain holds a parameter, refused at its second occurrence, or
    /// constants only, refused at its first node with two constant operands. The sets hold
    /// <see cref="ParameterExpression"/> references and boxed primitive values only, so no user
    /// <see cref="object.Equals(object?)"/> or <see cref="object.GetHashCode"/> runs; the first four entries of each are kept
    /// inline, so a chain refused within its first few terms allocates nothing.
    /// </para>
    /// <para>
    /// <b>Inherited refusals (mission C1).</b> Every reason of a refusal is inherited by every chain that contains it: a
    /// <em>local</em> reason (the type, an operator, a term, a constant pair, a subtracted group) belongs to one crossed node,
    /// which a larger chain crosses the same way (the operands of an intrinsic operator have its type); a <em>global</em>
    /// reason (a repeated parameter or coefficient, a coefficient equal to a constant, a third constant) involves terms that a
    /// larger chain holds too, and the sets only grow. The walk therefore reports where the cause lies. The <em>spine</em> of
    /// the chain is the path the walk follows from the root while every node has a single crossed operand, up to the first
    /// node with two (whose subtree holds everything the walk visits afterwards); the spine index of a node is its position
    /// on that path, and every node visited past the first branching node gets the index of that node. A spine node at index
    /// <c>i</c> contains every operand met at an index at least <c>i</c>, so the spine nodes down to the smallest index
    /// involved in a refusal have a chain that contains its cause: the index of the refusing node for a local reason, of the
    /// first constant for a third one, of the earlier occurrence for a repetition (which <see cref="RecordRefusedSpine"/>
    /// finds, the walk reporting the repeated key and the index of the refusing node).
    /// </para>
    /// </remarks>
    private bool IsLinearChain(BinaryExpression root, FlattenAdmissionStatistics? statistics, out ChainRefusal refusal)
    {
        Type type = root.Type;
        if (!IsFlattenedChainType(type))
        {
            refusal = new ChainRefusal(FlattenRefusalCause.ChainType, 0, null);
            return false;
        }

        if (statistics is not null) statistics.Walks++;
        var operands = new LinearChainOperands();
        Stack<BinaryExpression>? pending = null;
        BinaryExpression? node = root;
        int depth = 0;
        bool onSpine = true;
        while (node is not null)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            if (statistics is not null)
            {
                statistics.NodesExamined++;
                statistics.DistinctNodes?.Add(node);
            }

            BinaryExpression? left = AsCrossedAdditiveNode(node.Left);
            BinaryExpression? right = AsCrossedAdditiveNode(node.Right);
            if (right is not null && node.NodeType == ExpressionType.Subtract)
            {
                refusal = new ChainRefusal(FlattenRefusalCause.SubtractedGroup, depth, null);
                return false;
            }

            if (node.Left is ConstantExpression && node.Right is ConstantExpression)
            {
                refusal = new ChainRefusal(FlattenRefusalCause.ConstantPair, depth, null);
                return false;
            }

            // The terms first, by type tests: the operator of the node is classified (which makes the framework read the
            // types of its operands) only once both operands are known to be framework nodes.
            if (left is null && !IsLinearTerm(node.Left, type))
            {
                refusal = new ChainRefusal(NonLinearTermCause(node.Left), depth, null);
                return false;
            }

            if (right is null && !IsLinearTerm(node.Right, type))
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

            // A left-associated chain, the common shape, never allocates the stack. The spine ends at the first node with two
            // crossed operands: everything the walk visits afterwards belongs to that node.
            if (left is not null && right is not null)
            {
                onSpine = false;
                (pending ??= new Stack<BinaryExpression>()).Push(right);
            }

            node = left ?? right ?? (pending is { Count: > 0 } ? pending.Pop() : null);
            if (onSpine) depth++;
        }

        refusal = default;
        return operands.HasOperands;
    }

    /// <summary>Classifies a term refused by <see cref="IsLinearTerm"/>, by type tests only (an extension node is never read).</summary>
    /// <param name="term">The refused term.</param>
    /// <returns><see cref="FlattenRefusalCause.CallTerm"/> for a call or an invocation, otherwise <see cref="FlattenRefusalCause.NonLinearTerm"/>.</returns>
    private static FlattenRefusalCause NonLinearTermCause(Expression term)
        => term is MethodCallExpression or InvocationExpression ? FlattenRefusalCause.CallTerm : FlattenRefusalCause.NonLinearTerm;

    /// <summary>
    /// Returns <paramref name="node"/> when the chains of <see cref="FlattenAdditiveChain"/> cross it, as the extraction
    /// decides it: a framework <c>Add</c> or <c>Subtract</c> node that <see cref="ExpressionTransformer.CanFlattenThrough"/>
    /// accepts.
    /// </summary>
    /// <param name="node">An operand of a crossed node.</param>
    /// <returns>The crossed node, or <see langword="null"/> when <paramref name="node"/> is a term.</returns>
    private BinaryExpression? AsCrossedAdditiveNode(Expression node)
        => node is BinaryExpression { NodeType: ExpressionType.Add or ExpressionType.Subtract } binary && CanFlattenThrough(binary)
            ? binary
            : null;

    /// <summary>
    /// Whether <paramref name="type"/> is, by reference, a type whose additive chains <see cref="FlattenAdditiveChain"/>
    /// treats: <see cref="double"/>, <see cref="float"/>, <see cref="int"/>, <see cref="long"/>, <see cref="uint"/> and
    /// <see cref="ulong"/>. Their additions and subtractions never throw (<see cref="CanonicalizesOperator"/>).
    /// </summary>
    /// <param name="type">The result type of the root of a chain.</param>
    /// <returns><see langword="true"/> for an accepted type.</returns>
    private static bool IsFlattenedChainType(Type type)
        => ReferenceEquals(type, typeof(double))
            || ReferenceEquals(type, typeof(int))
            || ReferenceEquals(type, typeof(long))
            || ReferenceEquals(type, typeof(float))
            || ReferenceEquals(type, typeof(uint))
            || ReferenceEquals(type, typeof(ulong));

    /// <summary>
    /// Whether <paramref name="operand"/> is a linear term of a chain of <paramref name="type"/>: a parameter, a finite
    /// non-zero constant, or an ordinary product of a parameter by another parameter or by a finite constant other than
    /// <c>0</c>, <c>1</c> and <c>-1</c>. Type tests come first, so an extension node is never read (ETC R4), not even through
    /// the operator of a product: its factors are tested before the operator is classified.
    /// </summary>
    /// <param name="operand">A raw operand of the chain.</param>
    /// <param name="type">The type of the chain.</param>
    /// <returns><see langword="true"/> for a linear term.</returns>
    private static bool IsLinearTerm(Expression operand, Type type) => operand switch
    {
        ParameterExpression parameter => ReferenceEquals(parameter.Type, type),
        ConstantExpression constant => IsLinearConstant(constant, type),
        BinaryExpression { NodeType: ExpressionType.Multiply } product => (product.Left, product.Right) switch
            {
                (ParameterExpression, ParameterExpression) => true,
                (ParameterExpression, ConstantExpression coefficient) => IsLinearCoefficient(coefficient, type),
                (ConstantExpression coefficient, ParameterExpression) => IsLinearCoefficient(coefficient, type),
                _ => false,
            }
            && IsOrdinaryBinaryArithmetic(product)
            && ReferenceEquals(product.Type, type),
        _ => false,
    };

    /// <summary>
    /// Whether <paramref name="constant"/> is a finite, non-zero boxed primitive of exactly <paramref name="type"/>: a
    /// constant that neither a zero identity nor the <c>NaN</c> fold rewrites.
    /// </summary>
    /// <param name="constant">A constant term or factor.</param>
    /// <param name="type">The type of the chain.</param>
    /// <returns><see langword="true"/> for such a constant.</returns>
    private static bool IsLinearConstant(ConstantExpression constant, Type type)
        => constant.Value is { } value
            && ReferenceEquals(constant.Type, type)
            && ReferenceEquals(value.GetType(), type)
            && value switch
            {
                double doubleValue => double.IsFinite(doubleValue) && doubleValue != 0,
                float floatValue => float.IsFinite(floatValue) && floatValue != 0,
                _ => !NumericValueEquals(value, 0),
            };

    /// <summary>
    /// Whether <paramref name="constant"/> is a coefficient a product term may hold: a linear constant
    /// (<see cref="IsLinearConstant"/>) other than <c>1</c> and <c>-1</c>, the identities of
    /// <see cref="MultiplicationWithZeroOrOne(BinaryExpression, ConstantExpression, Expression)"/>.
    /// </summary>
    /// <param name="constant">A constant factor.</param>
    /// <param name="type">The type of the chain.</param>
    /// <returns><see langword="true"/> for an accepted coefficient.</returns>
    private static bool IsLinearCoefficient(ConstantExpression constant, Type type)
        => IsLinearConstant(constant, type) && !NumericValueEquals(constant.Value, 1) && !NumericValueEquals(constant.Value, -1);

    /// <summary>
    /// The parameters, constant coefficients and constant terms of a chain whose every term is a linear term
    /// (<see cref="IsLinearTerm"/>), recorded to check that the chain repeats nothing a rule could match: no parameter
    /// occurs twice, no coefficient value occurs twice, no coefficient equals a constant term, and the chain holds at most
    /// two constant terms. A mutable value: it lives in the walk's frame and is only used through that local.
    /// </summary>
    private struct LinearChainOperands
    {
        /// <summary>The parameters met, as terms or as factors (compared by reference, as a parameter has no other equality).</summary>
        private SmallOperandSet _parameters;

        /// <summary>The values of the constant coefficients met (boxed primitives, compared by value).</summary>
        private SmallOperandSet _coefficients;

        /// <summary>The values of the constant terms met (boxed primitives, compared by value).</summary>
        private SmallOperandSet _constants;

        /// <summary>The number of constant terms met.</summary>
        private int _constantCount;

        /// <summary>The spine index of the node that holds the first constant term met.</summary>
        private int _firstConstantDepth;

        /// <summary>Whether a term was recorded.</summary>
        private bool _recorded;

        /// <summary>Gets whether at least one term was recorded.</summary>
        public readonly bool HasOperands => _recorded;

        /// <summary>Records a linear term.</summary>
        /// <param name="term">A term accepted by <see cref="IsLinearTerm"/>.</param>
        /// <param name="depth">The spine index of the node that holds the term.</param>
        /// <param name="refusal">
        /// Receives, when the term repeats a parameter or a coefficient, makes a coefficient equal to a constant term, or is
        /// the third constant term of the chain, the cause, the spine index to report and the repeated key (see
        /// <see cref="ChainRefusal"/>); otherwise the default value.
        /// </param>
        /// <returns><see langword="true"/> when the term is accepted.</returns>
        public bool Add(Expression term, int depth, out ChainRefusal refusal)
        {
            _recorded = true;
            switch (term)
            {
                case ParameterExpression parameter:
                    return AddParameter(parameter, depth, out refusal);
                case ConstantExpression constant:
                    if (++_constantCount > 2)
                    {
                        refusal = new ChainRefusal(FlattenRefusalCause.TooManyConstants, _firstConstantDepth, null);
                        return false;
                    }

                    if (_constantCount == 1) _firstConstantDepth = depth;
                    object value = constant.Value!;
                    _constants.Add(value);
                    return Accept(!_coefficients.Contains(value), FlattenRefusalCause.CoefficientEqualsConstant, depth, value, out refusal);
                default:
                    var product = (BinaryExpression)term;
                    return AddFactor(product.Left, depth, out refusal) && AddFactor(product.Right, depth, out refusal);
            }
        }

        /// <summary>Records a factor of a product term: a parameter of the chain, or a constant coefficient.</summary>
        /// <param name="factor">A parameter or a constant.</param>
        /// <param name="depth">The spine index of the node that holds the product.</param>
        /// <param name="refusal">Receives the refusal when the parameter or the coefficient value was already met, or when the coefficient equals a constant term.</param>
        /// <returns><see langword="true"/> when the factor is accepted.</returns>
        private bool AddFactor(Expression factor, int depth, out ChainRefusal refusal)
        {
            if (factor is ParameterExpression parameter) return AddParameter(parameter, depth, out refusal);
            object value = ((ConstantExpression)factor).Value!;
            return Accept(_coefficients.Add(value), FlattenRefusalCause.RepeatedCoefficient, depth, value, out refusal)
                && Accept(!_constants.Contains(value), FlattenRefusalCause.CoefficientEqualsConstant, depth, value, out refusal);
        }

        /// <summary>Records a parameter, as a term or as a factor.</summary>
        /// <param name="parameter">The parameter.</param>
        /// <param name="depth">The spine index of the node that holds it.</param>
        /// <param name="refusal">Receives the refusal when the parameter was already met.</param>
        /// <returns><see langword="true"/> when the parameter is new.</returns>
        private bool AddParameter(ParameterExpression parameter, int depth, out ChainRefusal refusal)
            => Accept(_parameters.Add(parameter), FlattenRefusalCause.RepeatedParameter, depth, parameter, out refusal);

        /// <summary>Builds the outcome of one check of a repetition.</summary>
        /// <param name="accepted">Whether the check passed.</param>
        /// <param name="cause">The cause reported when it did not.</param>
        /// <param name="depth">The spine index of the refusing node.</param>
        /// <param name="key">The repeated key.</param>
        /// <param name="refusal">Receives the refusal, or the default value.</param>
        /// <returns><paramref name="accepted"/>.</returns>
        private static bool Accept(bool accepted, FlattenRefusalCause cause, int depth, object key, out ChainRefusal refusal)
        {
            refusal = accepted ? default : new ChainRefusal(cause, depth, key);
            return accepted;
        }
    }

    /// <summary>
    /// A small set of operand keys (<see cref="LinearChainOperands"/>): the first four keys are kept inline, the others in a
    /// set created on first use, so that a chain refused within its first few terms allocates nothing. Keys are compared with
    /// <see cref="object.Equals(object?, object?)"/>, as the default comparer of the set does: by reference for a
    /// <see cref="ParameterExpression"/> (a framework class that does not override it), by value for a boxed primitive. A
    /// mutable value, used through its field only.
    /// </summary>
    private struct SmallOperandSet
    {
        /// <summary>The number of inline keys.</summary>
        private const int InlineCapacity = 4;

        /// <summary>The inline keys.</summary>
        private object? _key0, _key1, _key2, _key3;

        /// <summary>The number of inline keys in use.</summary>
        private int _count;

        /// <summary>The keys past the inline ones; <see langword="null"/> until needed.</summary>
        private HashSet<object>? _overflow;

        /// <summary>Adds <paramref name="key"/> unless it is already present.</summary>
        /// <param name="key">The key.</param>
        /// <returns><see langword="true"/> when the key was not present.</returns>
        public bool Add(object key)
        {
            if (Contains(key)) return false;
            switch (_count)
            {
                case 0: _key0 = key; break;
                case 1: _key1 = key; break;
                case 2: _key2 = key; break;
                case 3: _key3 = key; break;
                default: return (_overflow ??= []).Add(key);
            }

            _count++;
            return true;
        }

        /// <summary>Determines whether <paramref name="key"/> is present.</summary>
        /// <param name="key">The key.</param>
        /// <returns><see langword="true"/> when the key is present.</returns>
        public readonly bool Contains(object key)
            => (_count > 0 && Equals(_key0, key))
                || (_count > 1 && Equals(_key1, key))
                || (_count > 2 && Equals(_key2, key))
                || (_count > 3 && Equals(_key3, key))
                || (_count == InlineCapacity && _overflow is not null && _overflow.Contains(key));
    }
}