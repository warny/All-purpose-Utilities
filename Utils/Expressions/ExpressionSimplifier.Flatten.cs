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
    /// </remarks>
    internal override ExpressionRuleRejection FlattenAdmission(Expression root)
    {
        if (GetType() != typeof(ExpressionSimplifier)) return base.FlattenAdmission(root);
        if (root is not BinaryExpression binary) return ExpressionRuleRejection.ChainNotAdmitted;

        FlattenAdmissionStatistics? statistics = t_admissionStatistics;
        bool linear = IsLinearChain(binary, statistics, out FlattenRefusalCause cause);
        if (statistics is not null)
        {
            statistics.Admissions++;
            if (linear) statistics.Accepted++;
            else statistics.Causes[(int)cause]++;
        }

        return linear ? ExpressionRuleRejection.None : ExpressionRuleRejection.ChainNotAdmitted;
    }

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
    /// <param name="cause">Receives why the chain is refused, or <see cref="FlattenRefusalCause.None"/>.</param>
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
    /// <see cref="object.Equals(object?)"/> or <see cref="object.GetHashCode"/> runs.
    /// </para>
    /// </remarks>
    private bool IsLinearChain(BinaryExpression root, FlattenAdmissionStatistics? statistics, out FlattenRefusalCause cause)
    {
        Type type = root.Type;
        if (!IsFlattenedChainType(type))
        {
            cause = FlattenRefusalCause.ChainType;
            return false;
        }

        if (statistics is not null) statistics.Walks++;
        LinearChainOperands? operands = null;
        Stack<BinaryExpression>? pending = null;
        BinaryExpression? node = root;
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
                cause = FlattenRefusalCause.SubtractedGroup;
                return false;
            }

            if (node.Left is ConstantExpression && node.Right is ConstantExpression)
            {
                cause = FlattenRefusalCause.ConstantPair;
                return false;
            }

            // The terms first, by type tests: the operator of the node is classified (which makes the framework read the
            // types of its operands) only once both operands are known to be framework nodes.
            if (left is null && !IsLinearTerm(node.Left, type))
            {
                cause = NonLinearTermCause(node.Left);
                return false;
            }

            if (right is null && !IsLinearTerm(node.Right, type))
            {
                cause = NonLinearTermCause(node.Right);
                return false;
            }

            if (!IsOrdinaryBinaryArithmetic(node))
            {
                cause = FlattenRefusalCause.Operator;
                return false;
            }

            if (left is null && (cause = (operands ??= new LinearChainOperands()).Add(node.Left)) != FlattenRefusalCause.None) return false;
            if (right is null && (cause = (operands ??= new LinearChainOperands()).Add(node.Right)) != FlattenRefusalCause.None) return false;

            // A left-associated chain, the common shape, never allocates the stack.
            if (left is not null && right is not null) (pending ??= new Stack<BinaryExpression>()).Push(right);
            node = left ?? right ?? (pending is { Count: > 0 } ? pending.Pop() : null);
        }

        cause = FlattenRefusalCause.None;
        return operands is not null;
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
    /// two constant terms.
    /// </summary>
    private sealed class LinearChainOperands
    {
        /// <summary>The parameters met, as terms or as factors.</summary>
        private readonly HashSet<ParameterExpression> _parameters = [];

        /// <summary>The values of the constant coefficients met; created on first use.</summary>
        private HashSet<object>? _coefficients;

        /// <summary>The values of the constant terms met; created on first use.</summary>
        private HashSet<object>? _constants;

        /// <summary>The number of constant terms met.</summary>
        private int _constantCount;

        /// <summary>Records a linear term.</summary>
        /// <param name="term">A term accepted by <see cref="IsLinearTerm"/>.</param>
        /// <returns>
        /// <see cref="FlattenRefusalCause.None"/>, or the cause when the term repeats a parameter or a coefficient, makes a
        /// coefficient equal to a constant term, or is the third constant term of the chain.
        /// </returns>
        public FlattenRefusalCause Add(Expression term)
        {
            switch (term)
            {
                case ParameterExpression parameter:
                    return _parameters.Add(parameter) ? FlattenRefusalCause.None : FlattenRefusalCause.RepeatedParameter;
                case ConstantExpression constant:
                    if (++_constantCount > 2) return FlattenRefusalCause.TooManyConstants;
                    (_constants ??= []).Add(constant.Value!);
                    return _coefficients is null || !_coefficients.Contains(constant.Value!) ? FlattenRefusalCause.None : FlattenRefusalCause.CoefficientEqualsConstant;
                default:
                    var product = (BinaryExpression)term;
                    FlattenRefusalCause cause = AddFactor(product.Left);
                    return cause != FlattenRefusalCause.None ? cause : AddFactor(product.Right);
            }
        }

        /// <summary>Records a factor of a product term: a parameter of the chain, or a constant coefficient.</summary>
        /// <param name="factor">A parameter or a constant.</param>
        /// <returns><see cref="FlattenRefusalCause.None"/>, or the cause when the parameter or the coefficient value was already met, or when the coefficient equals a constant term.</returns>
        private FlattenRefusalCause AddFactor(Expression factor)
        {
            if (factor is ParameterExpression parameter) return _parameters.Add(parameter) ? FlattenRefusalCause.None : FlattenRefusalCause.RepeatedParameter;
            object value = ((ConstantExpression)factor).Value!;
            if (!(_coefficients ??= []).Add(value)) return FlattenRefusalCause.RepeatedCoefficient;
            return _constants is null || !_constants.Contains(value) ? FlattenRefusalCause.None : FlattenRefusalCause.CoefficientEqualsConstant;
        }
    }
}
