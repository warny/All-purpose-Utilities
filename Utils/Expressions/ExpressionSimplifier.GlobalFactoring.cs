using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Utils.Expressions;
using Utils.Objects;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// The global factoring of the additive chains (mission C3c, <c>docs/expressions/SimplifierFlatten.md</c> §13): the terms of a
/// canonicalized sum that hold the same factors are merged into one, and the terms of a whole factorable chain are grouped
/// by their common factors, from the analysis of the terms (<see cref="AdditiveTermAnalysis"/>) instead of a succession of
/// binary factorings.
/// </summary>
public partial class ExpressionSimplifier
{
    /// <summary>
    /// The number of revisits of shared crossed nodes the factorable test always accepts (<see cref="IsFactorableChain"/>);
    /// beyond it, the revisits may not exceed the distinct shared nodes.
    /// </summary>
    private const int SharedRevisitAllowance = 16;

    /// <summary>What the additive canonicalization of the built-in type merges or factors (mission C3c).</summary>
    private enum AdditiveFactoring
    {
        /// <summary>Nothing: the terms are proven distinct (a linear or an anchored chain).</summary>
        None,

        /// <summary>The terms holding the same factors (<see cref="MergeIdenticalMonomials"/>): every other canonicalization.</summary>
        Monomials,

        /// <summary>The identical monomials, then the common factors of a whole chain (<see cref="FactorCommonFactors"/>).</summary>
        CommonFactors,
    }

    /// <summary>
    /// The names of the functions a factorable term may call (<see cref="IsFactorableChain"/>): static methods of
    /// <see cref="Math"/> or <see cref="double"/> that no additive signature rule reads. The logarithms are left out
    /// (<see cref="LogarithmSimplificationAddNumber"/> combines two of them), and so is <c>Pow</c>, which a rule turns into a
    /// power node.
    /// </summary>
    private static readonly HashSet<string> FactorableFunctionNames =
    [
        nameof(Math.Sin), nameof(Math.Cos), nameof(Math.Tan),
        nameof(Math.Sinh), nameof(Math.Cosh), nameof(Math.Tanh),
        nameof(Math.Asin), nameof(Math.Acos), nameof(Math.Atan),
        nameof(Math.Exp), nameof(Math.Sqrt), nameof(Math.Cbrt), nameof(Math.Abs),
    ];

    /// <summary>
    /// Whether the additive canonicalization of this instance merges the terms that hold the same factors (mission C3c): the
    /// built-in type only. A derived simplifier keeps the historical canonicalization, which its own rules and finalization
    /// may rely on. Internal and virtual only so that the level by level references of the tests can merge as the built-in
    /// type does.
    /// </summary>
    internal virtual bool MergesMonomials => GetType() == typeof(ExpressionSimplifier);

    /// <summary>
    /// Merges the terms of a canonicalized sum that hold the same factors, in any order, into one term whose coefficient is
    /// the sum of theirs (mission C3c, phase A): <c>x + 2*x + 3*x</c> gives <c>6*x</c>, <c>2*a*b + 3*b*a</c> gives
    /// <c>5*(a*b)</c>, <c>x - x</c> gives nothing.
    /// </summary>
    /// <param name="terms">The inert signed terms of the sum (zeros dropped, constants aggregated); updated in place.</param>
    /// <param name="type">The type of the sum, one of <see cref="Types.Number"/>.</param>
    /// <param name="scopes">The lexical scope snapshot of the canonicalization.</param>
    /// <param name="record">Whether computed hashes are stored in the operation's metadata table.</param>
    /// <remarks>
    /// <para>
    /// <b>Grouping.</b> The terms are decomposed once (<see cref="AdditiveTermAnalysis"/>) and bucketed by the
    /// order-independent hash of their factors (<see cref="AdditiveTermAnalysis.GroupByFactors"/>); two terms join only after
    /// a structural comparison of their factors as multisets, so a collision never merges anything and <c>x*x</c> never meets
    /// <c>x</c>. The coefficient of a term is its single numeric constant factor, of the type of the sum, or an implicit
    /// <c>1</c>; a power stays one indivisible factor, so <c>x²</c> and <c>2*x²</c> merge and <c>x²</c> and <c>x</c> do not.
    /// </para>
    /// <para>
    /// <b>The identity.</b> Merging is the factoring <see cref="AdditionOfEqualsElements"/> and
    /// <see cref="SubstractionOfEqualsElements"/> already apply to two terms, followed by the constant folding of
    /// <see cref="TryFoldFactoredCoefficient"/>, applied to the whole group at once: the coefficients are summed by
    /// <see cref="SumCoefficients"/> with <see cref="FoldConstants"/>, in source order, exactly as successive binary factorings
    /// fold them (modular for the integral types, so exact, an unsigned difference wrapping; IEEE-754 for the floating-point
    /// types, the reassociation latitude of the symbolic-equivalence contract, an overflow giving the infinity the binary
    /// factoring gives). A coefficient summing to <c>1</c> gives the factors alone, <c>-1</c> the factors with
    /// the opposite sign, <c>0</c> drops the group, which <see cref="CollectUncancelledTerms"/> already does for <c>t - t</c>:
    /// the terms are inert (the pinned ones were split off before), so dropping, merging or moving them is unobservable.
    /// </para>
    /// <para>
    /// <b>Safety.</b> Only the terms whose factor part is a parameter, a product, an ordinary power or a call of a runtime
    /// method are merged: an opaque term (a division, a conversion, a custom operator, an extension node) keeps the
    /// historical grouping of <see cref="CollectUncancelledTerms"/>. The merged term reuses the factor part of its first
    /// occurrence (the right operand of a product whose left operand is the coefficient, the canonical shape) and is built by
    /// the framework factory from framework nodes only (ETC R4, S5 A-10). Nothing is simplified or evaluated beyond the boxed
    /// coefficients.
    /// </para>
    /// </remarks>
    private void MergeIdenticalMonomials(List<(Expression Term, bool IsNegative)> terms, Type type, ParameterExpression[][] scopes, bool record)
    {
        if (terms.Count < 2 || !Types.Number.Contains(type)) return;

        // Without two terms other than constants nothing can merge: no analysis is built.
        int candidates = 0;
        foreach ((Expression term, _) in terms)
        {
            if (term is not ConstantExpression && ++candidates == 2) break;
        }

        if (candidates < 2) return;

        AdditiveTermAnalysis analysis = AdditiveTermAnalysis.Analyze(this, terms, type, scopes, record);
        int[] representatives = analysis.GroupByFactors();
        int count = analysis.Count;

        // The eligible members of each group, counted on the group's representative, and the first of them.
        bool[] eligible = new bool[count];
        int[] eligibleMembers = new int[count];
        int[] firstMember = new int[count];
        bool anyGroup = false;
        for (int index = 0; index < count; index++)
        {
            if (!IsMergeableMonomial(analysis, index)) continue;
            eligible[index] = true;
            int representative = representatives[index];
            if (eligibleMembers[representative]++ == 0) firstMember[representative] = index;
            else anyGroup = true;
        }

        if (!anyGroup) return;

        FlattenAdmissionStatistics? statistics = t_admissionStatistics;
        var merged = new (Expression? Term, bool IsNegative, bool Merged)[count];
        var coefficients = new List<(Expression Term, bool IsNegative)>();
        ConstantExpression? one = null;
        for (int representative = 0; representative < count; representative++)
        {
            if (eligibleMembers[representative] < 2) continue;

            coefficients.Clear();
            for (int index = firstMember[representative]; index < count; index++)
            {
                if (!eligible[index] || representatives[index] != representative) continue;
                ref readonly AnalyzedAdditiveTerm member = ref analysis[index];
                coefficients.Add((member.Coefficient ?? (one ??= Expression.Constant(Convert.ChangeType(1, type), type)), member.IsNegative));
            }

            if (SumCoefficients(coefficients, type) is not { } sum) continue;

            merged[representative] = (BuildMergedMonomial(analysis, firstMember[representative], sum, out bool isNegative), isNegative, true);
            if (statistics is not null)
            {
                statistics.MonomialGroups++;
                statistics.MergedTerms += coefficients.Count;
            }
        }

        terms.Clear();
        for (int index = 0; index < count; index++)
        {
            ref readonly AnalyzedAdditiveTerm term = ref analysis[index];
            int representative = representatives[index];
            if (eligible[index] && merged[representative].Merged)
            {
                // The merged term takes the place of the group's first eligible member; a zero sum leaves nothing.
                if (index == firstMember[representative] && merged[representative].Term is { } mergedTerm) terms.Add((mergedTerm, merged[representative].IsNegative));
                continue;
            }

            terms.Add((term.Expression, term.IsNegative));
        }
    }

    /// <summary>
    /// Whether the analyzed term at <paramref name="index"/> may be merged by <see cref="MergeIdenticalMonomials"/> or factored
    /// by <see cref="FactorCommonFactors"/>: its factor part is a parameter, a product, an ordinary power or a call, and each
    /// factor is a framework node whose type the factories may read.
    /// </summary>
    /// <param name="analysis">The analysis of the sum.</param>
    /// <param name="index">The index of the term.</param>
    /// <returns><see langword="true"/> for a mergeable term.</returns>
    private static bool IsMergeableMonomial(AdditiveTermAnalysis analysis, int index)
    {
        ref readonly AnalyzedAdditiveTerm term = ref analysis[index];
        if (term.Family is not (AdditiveTermFamily.Parameter or AdditiveTermFamily.Product or AdditiveTermFamily.Power or AdditiveTermFamily.Function)) return false;
        foreach (Expression factor in analysis.GetFactors(index))
        {
            if (!IsMergeableFactor(factor)) return false;
        }

        return true;
    }

    /// <summary>
    /// Whether <paramref name="factor"/> is a framework node a merged or factored term may hold: a parameter, a constant, a
    /// binary or unary node, or a call of a runtime method. By type tests only, the provenance of a method being checked
    /// before its return type is read by a factory (S5 A-10); an extension node is never one (ETC R4).
    /// </summary>
    /// <param name="factor">A factor of an analyzed term.</param>
    /// <returns><see langword="true"/> for an accepted factor.</returns>
    private static bool IsMergeableFactor(Expression factor)
        => factor is ParameterExpression or ConstantExpression or BinaryExpression or UnaryExpression
            || (factor is MethodCallExpression call && ExpressionReflectionProvenance.IsRuntime(call.Method));

    /// <summary>Whether a boxed coefficient is finite: always for an integral value, by IEEE-754 for a floating-point one.</summary>
    /// <param name="value">A boxed primitive.</param>
    /// <returns><see langword="false"/> for <c>NaN</c> and the infinities only.</returns>
    private static bool IsFiniteValue(object value) => value switch
    {
        double doubleValue => double.IsFinite(doubleValue),
        float floatValue => float.IsFinite(floatValue),
        _ => true,
    };

    /// <summary>
    /// Sums the signed coefficients of a group in source order with <see cref="FoldConstants"/>, as the factoring of two terms
    /// folds its coefficient (<see cref="TryFoldFactoredCoefficient"/>) and as repeated factorings of the pairs would: the
    /// first coefficient (negated from <c>0</c> when subtracted), then each other one added or subtracted, with the unchecked
    /// semantics of the type (an unsigned difference wraps: <c>3*x - 4*x</c> is <c>4294967295*x</c> for <see cref="uint"/>).
    /// </summary>
    /// <param name="coefficients">The coefficients of the group (constants of <paramref name="type"/>) with their signs.</param>
    /// <param name="type">The type of the sum; never <see cref="decimal"/> (whose chains are not canonicalized).</param>
    /// <returns>The boxed sum, or <see langword="null"/> when a fold is refused.</returns>
    private static object? SumCoefficients(List<(Expression Term, bool IsNegative)> coefficients, Type type)
    {
        object? sum = null;
        try
        {
            foreach ((Expression term, bool isNegative) in coefficients)
            {
                object value = ((ConstantExpression)term).Value!;
                sum = sum is null
                    ? isNegative ? FoldConstants(ExpressionType.Subtract, type, Convert.ChangeType(0, type), value)?.Value : value
                    : FoldConstants(isNegative ? ExpressionType.Subtract : ExpressionType.Add, type, sum, value)?.Value;
                if (sum is null) return null;
            }
        }
        catch (OverflowException)
        {
            return null;
        }

        return sum;
    }

    /// <summary>
    /// Replaces the subtracted constant of a canonicalized sum (the constants are aggregated, so it holds one at most) by its
    /// negation, for a type with an ordinary negation (mission C3c): <c>y - 1</c> gives <c>-1 + y</c> instead of
    /// <c>(0 - 1) + y</c>, the form the next call folded (<see cref="SubstractionOfConstants"/> on the constant pair
    /// <c>0 - 1</c>), so the canonical sum is a fixed point.
    /// </summary>
    /// <param name="terms">The inert signed terms of the sum; updated in place.</param>
    /// <param name="type">The type of the sum.</param>
    /// <remarks>
    /// The negation is folded with <see cref="FoldConstants"/>, as the next call folds <c>0 - c</c>: exact for a
    /// floating-point constant, modular for a signed integral one (<c>0 - MinValue</c> is <c>MinValue</c>, as at run time).
    /// An unsigned type keeps <c>0 - c</c>: its negation is the large wrapped value that the aggregation of the constants
    /// avoids on purpose (<see cref="AggregateAdditiveConstants"/>).
    /// </remarks>
    private static void NegateSubtractedConstant(List<(Expression Term, bool IsNegative)> terms, Type type)
    {
        if (!_ordinaryNegateMethods.ContainsKey(type)) return;
        for (int index = 0; index < terms.Count; index++)
        {
            (Expression term, bool isNegative) = terms[index];
            if (!isNegative || term is not ConstantExpression constant || !IsAggregatableConstant(constant, type)) continue;
            if (FoldConstants(ExpressionType.Subtract, type, Convert.ChangeType(0, type), constant.Value) is { } negated) terms[index] = (negated, false);
        }
    }

    /// <summary>
    /// Builds the term of a merged group from the factor part of its first eligible member and the sum of the coefficients.
    /// </summary>
    /// <param name="analysis">The analysis of the sum.</param>
    /// <param name="member">The index of the group's first eligible member.</param>
    /// <param name="sum">The sum of the coefficients.</param>
    /// <param name="isNegative">Receives the sign of the merged term: subtracted for a sum of <c>-1</c> only.</param>
    /// <returns>The merged term, or <see langword="null"/> when the coefficients cancel.</returns>
    private static Expression? BuildMergedMonomial(AdditiveTermAnalysis analysis, int member, object sum, out bool isNegative)
    {
        isNegative = false;
        if (NumericValueEquals(sum, 0)) return null;

        Expression factorPart = FactorPartOf(analysis, member);
        if (NumericValueEquals(sum, 1)) return factorPart;
        if (NumericValueEquals(sum, -1))
        {
            isNegative = true;
            return factorPart;
        }

        return Expression.Multiply(Expression.Constant(sum), factorPart);
    }

    /// <summary>
    /// The factor part of the analyzed term at <paramref name="index"/>: the term itself without a coefficient, the right
    /// operand of a product whose left operand is the coefficient (the canonical shape), otherwise its factors other than the
    /// coefficient, right-associated in their order.
    /// </summary>
    /// <param name="analysis">The analysis of the sum.</param>
    /// <param name="index">The index of the term.</param>
    /// <returns>The factor part.</returns>
    private static Expression FactorPartOf(AdditiveTermAnalysis analysis, int index)
    {
        ref readonly AnalyzedAdditiveTerm term = ref analysis[index];
        if (term.Coefficient is null) return term.Expression;
        if (term.Expression is BinaryExpression product)
        {
            if (ReferenceEquals(product.Left, term.Coefficient)) return product.Right;
            if (ReferenceEquals(product.Right, term.Coefficient)) return product.Left;
        }

        ReadOnlySpan<Expression> factors = analysis.GetFactors(index);
        Expression result = factors[^1];
        for (int i = factors.Length - 2; i >= 0; i--) result = Expression.Multiply(factors[i], result);
        return result;
    }

    /// <summary>
    /// Groups the terms of a whole factorable chain by their common factors (mission C3c, phase B): <c>a*x + 3*x</c> gives
    /// <c>x*(3 + a)</c>, <c>2*a*x + 3*b*x</c> gives <c>x*(2*a + 3*b)</c>, <c>a*b*c + a*b*d</c> gives <c>a*b*(c + d)</c>,
    /// <c>2*a + 2*b</c> gives <c>2*(a + b)</c>.
    /// </summary>
    /// <param name="terms">The inert signed terms of the chain, monomials already merged; updated in place.</param>
    /// <param name="type">The type of the chain.</param>
    /// <param name="scopes">The lexical scope snapshot of the canonicalization.</param>
    /// <remarks>
    /// <para>
    /// <b>Candidates.</b> A candidate is a factor (compared structurally, as a multiset member) or a numeric coefficient other
    /// than <c>0</c>, <c>1</c> and <c>-1</c> (compared by value, a constant term counting as its own coefficient, as
    /// <see cref="AdditionOfEqualsElements"/> factors <c>2*a + 2</c>). Each round indexes the candidates of every mergeable
    /// term once (a dictionary keyed by the structural hash, every hit confirmed by a structural comparison, one structural
    /// visit per indexed factor), so finding the common factors is linear in the number of factors, never all terms against
    /// all terms.
    /// </para>
    /// <para>
    /// <b>Choice.</b> The candidate held by the most terms is factored first, which removes the most terms; a tie prefers a
    /// symbolic factor to a coefficient, then the factor that comes first in the canonical order of the keys
    /// (<see cref="ExpressionCanonicalOrder"/>). The choice therefore depends on the structure of the terms only: neither on
    /// their source order nor on the names of the parameters beyond what the canonical order already reads.
    /// </para>
    /// <para>
    /// <b>Factoring.</b> The terms holding the candidate are replaced by one product: the candidate times the sum of their
    /// residues (each term without one occurrence of the factor, or without its coefficient, with its sign), and that sum is
    /// the canonical form of the residues (<see cref="CanonicalizeAdditiveTerms"/>, factored in turn), whose constants are
    /// aggregated once (<see cref="AggregateAdditiveConstants"/>): <c>2*x + a*x + x</c> gives <c>x*(3 + a)</c> directly, never
    /// <c>((2 + a) + 1)*x</c>. The product is canonical (<see cref="BuildCanonicalProduct"/>), with its numeric factors folded
    /// into one coefficient. A residue sum of <c>0</c> drops the group, <c>1</c> leaves the factor alone. Every round removes
    /// at least one term, so the rounds end; the result holds no candidate shared by two terms.
    /// </para>
    /// <para>
    /// <b>Limits.</b> Only the factors already present in the terms are considered: a sum is never recognized as a factor of
    /// another term (<c>x + y + a*(x + y)</c> stays), and a power is never split.
    /// </para>
    /// </remarks>
    private void FactorCommonFactors(List<(Expression Term, bool IsNegative)> terms, Type type, ParameterExpression[][] scopes)
    {
        // Recursive through the canonicalization of the residues: guard the execution stack (ETC R1a-Security).
        ExpressionResourceBudget.EnsureStack();
        if (terms.Count < 2 || !Types.Number.Contains(type)) return;

        FlattenAdmissionStatistics? statistics = t_admissionStatistics;
        var monomials = new List<FactoringMonomial>(terms.Count);
        foreach ((Expression term, bool isNegative) in terms) monomials.Add(FactoringMonomial.Create(this, term, isNegative, type));

        var rejected = new HashSet<FactoringCandidate>();
        ConstantExpression? one = null;
        while (monomials.Count >= 2)
        {
            if (statistics is not null) statistics.FactoringRounds++;
            FactoringCandidate? best = FindBestCandidate(monomials, scopes, rejected, statistics);
            if (best is null) break;

            var residues = new List<(Expression Term, bool IsNegative)>();
            int first = -1;
            bool allNegative = true;
            for (int index = 0; index < monomials.Count; index++)
            {
                FactoringMonomial monomial = monomials[index];
                if (!monomial.Holds(best, statistics)) continue;
                if (first < 0) first = index;
                residues.Add((monomial.Residue(best, type, ref one, this), monomial.IsNegative));
                allNegative &= monomial.IsNegative;
            }

            // Terms that are all subtracted give a subtracted product of a positive sum (-a*x - b*x is -(x*(a + b))), never a
            // product of a sum of negations.
            if (allNegative)
            {
                for (int index = 0; index < residues.Count; index++) residues[index] = (residues[index].Term, false);
            }

            Expression sum = CanonicalizeAdditiveTerms(residues, type, recordMetadata: false, AdditiveFactoring.CommonFactors);
            if (!TryBuildFactoredTerm(best, sum, type, out Expression? factored, out bool factoredIsNegative))
            {
                rejected.Add(best);
                continue;
            }

            factoredIsNegative ^= allNegative;

            if (statistics is not null) statistics.CommonFactorings++;
            var next = new List<FactoringMonomial>(monomials.Count - residues.Count + 1);
            for (int index = 0; index < monomials.Count; index++)
            {
                if (index == first && factored is not null) next.Add(FactoringMonomial.Create(this, factored, factoredIsNegative, type));
                if (!monomials[index].Holds(best, null)) next.Add(monomials[index]);
            }

            monomials = next;
        }

        terms.Clear();
        foreach (FactoringMonomial monomial in monomials) terms.Add((monomial.Term, monomial.IsNegative));
    }

    /// <summary>
    /// Indexes the candidates of the mergeable monomials and returns the one to factor (see <see cref="FactorCommonFactors"/>),
    /// or <see langword="null"/> when no candidate is held by two terms.
    /// </summary>
    /// <param name="monomials">The current terms.</param>
    /// <param name="scopes">The lexical scope snapshot, for the canonical keys of a tie.</param>
    /// <param name="rejected">The candidates whose factoring was abandoned.</param>
    /// <param name="statistics">The opt-in counters of the thread, or <see langword="null"/>.</param>
    /// <returns>The chosen candidate.</returns>
    private static FactoringCandidate? FindBestCandidate(List<FactoringMonomial> monomials, ParameterExpression[][] scopes, HashSet<FactoringCandidate> rejected, FlattenAdmissionStatistics? statistics)
    {
        var symbolic = new Dictionary<Expression, FactoringCandidate>(FactorEqualityComparer.Instance);
        var numeric = new Dictionary<object, FactoringCandidate>();
        for (int index = 0; index < monomials.Count; index++)
        {
            FactoringMonomial monomial = monomials[index];
            if (!monomial.IsMergeable) continue;
            foreach (Expression factor in monomial.Factors)
            {
                // One structural visit per indexed factor: the rounds are bounded by the operation's budget.
                ExpressionResourceBudget.ConsumeStructuralVisit();
                if (statistics is not null) statistics.FactorComparisons++;
                if (!symbolic.TryGetValue(factor, out FactoringCandidate? candidate)) symbolic.Add(factor, new FactoringCandidate(factor, null, index));
                else candidate.Count(index);
            }

            if (monomial.NumericValue is { } value && IsFactorableCoefficient(value))
            {
                if (!numeric.TryGetValue(value, out FactoringCandidate? candidate)) numeric.Add(value, new FactoringCandidate(monomial.NumericConstant!, value, index));
                else candidate.Count(index);
            }
        }

        FactoringCandidate? best = null;
        foreach (FactoringCandidate candidate in symbolic.Values) best = Better(candidate, best, scopes, rejected);
        foreach (FactoringCandidate candidate in numeric.Values) best = Better(candidate, best, scopes, rejected);
        return best;
    }

    /// <summary>Whether a numeric coefficient may be factored: finite, other than <c>0</c>, <c>1</c> and <c>-1</c>.</summary>
    /// <param name="value">A boxed primitive.</param>
    /// <returns><see langword="true"/> for a factorable coefficient.</returns>
    private static bool IsFactorableCoefficient(object value)
        => IsFiniteValue(value) && !NumericValueEquals(value, 0) && !NumericValueEquals(value, 1) && !NumericValueEquals(value, -1);

    /// <summary>Returns the better of two candidates for the factoring (see <see cref="FactorCommonFactors"/>).</summary>
    /// <param name="candidate">A candidate.</param>
    /// <param name="best">The best candidate so far, or <see langword="null"/>.</param>
    /// <param name="scopes">The lexical scope snapshot, for the canonical keys of a tie.</param>
    /// <param name="rejected">The candidates whose factoring was abandoned.</param>
    /// <returns>The better one; a candidate held by fewer than two terms, or rejected, never wins.</returns>
    private static FactoringCandidate? Better(FactoringCandidate candidate, FactoringCandidate? best, ParameterExpression[][] scopes, HashSet<FactoringCandidate> rejected)
    {
        if (candidate.Terms < 2 || rejected.Contains(candidate)) return best;
        if (best is null) return candidate;
        if (candidate.Terms != best.Terms) return candidate.Terms > best.Terms ? candidate : best;
        if (candidate.IsNumeric != best.IsNumeric) return candidate.IsNumeric ? best : candidate;
        return candidate.KeyOf(scopes).CompareTo(best.KeyOf(scopes)) < 0 ? candidate : best;
    }

    /// <summary>
    /// Builds the term that replaces the terms holding <paramref name="candidate"/>: the candidate times the sum of their
    /// residues, as a canonical product whose numeric factors are folded into one coefficient.
    /// </summary>
    /// <param name="candidate">The factored candidate.</param>
    /// <param name="sum">The canonical sum of the residues.</param>
    /// <param name="type">The type of the chain.</param>
    /// <param name="term">Receives the term, or <see langword="null"/> when the residues cancel.</param>
    /// <param name="isNegative">Receives the sign of the term.</param>
    /// <returns><see langword="false"/> when the coefficient cannot be folded (a non-finite product): the candidate is abandoned.</returns>
    private bool TryBuildFactoredTerm(FactoringCandidate candidate, Expression sum, Type type, out Expression? term, out bool isNegative)
    {
        term = null;
        isNegative = false;
        var factors = new List<Expression>(4);
        object? coefficient = candidate.Value;
        if (!candidate.IsNumeric) factors.Add(candidate.Factor);

        if (sum is ConstantExpression constant)
        {
            if (!IsAggregatableConstant(constant, type)) return false;
            coefficient = coefficient is null ? constant.Value : FoldConstants(ExpressionType.Multiply, type, coefficient, constant.Value)?.Value;
            if (coefficient is null) return false;
        }
        else
        {
            CollectMultiplicativeFactors(factors, sum);
            for (int index = factors.Count - 1; index >= 0; index--)
            {
                if (factors[index] is not ConstantExpression factor || !IsAggregatableConstant(factor, type)) continue;
                coefficient = coefficient is null ? factor.Value : FoldConstants(ExpressionType.Multiply, type, coefficient, factor.Value)?.Value;
                factors.RemoveAt(index);
                if (coefficient is null) return false;
            }
        }

        if (coefficient is not null)
        {
            if (!IsFiniteValue(coefficient)) return false;
            if (NumericValueEquals(coefficient, 0)) return true;
            if (NumericValueEquals(coefficient, -1) && !Types.UnsignedNumber.Contains(type)) isNegative = true;
            else if (!NumericValueEquals(coefficient, 1)) factors.Add(Expression.Constant(coefficient));
        }

        if (factors.Count == 0)
        {
            // A numeric candidate whose residues summed to a constant: a constant term.
            term = Expression.Constant(coefficient ?? Convert.ChangeType(1, type), type);
            isNegative = false;
            return true;
        }

        term = factors.Count == 1 ? factors[0] : BuildCanonicalProduct(factors, false);
        return true;
    }

    /// <summary>
    /// One term of a chain being factored (<see cref="FactorCommonFactors"/>): the term, its sign, its numeric coefficient and
    /// its factors. A mutable working value, private to one factoring.
    /// </summary>
    private sealed class FactoringMonomial
    {
        /// <summary>The term.</summary>
        public required Expression Term { get; init; }

        /// <summary>Whether the term is subtracted.</summary>
        public required bool IsNegative { get; init; }

        /// <summary>Its numeric coefficient, or <see langword="null"/>.</summary>
        public required ConstantExpression? Coefficient { get; init; }

        /// <summary>Its factors, the coefficient set aside (empty for a constant term).</summary>
        public required Expression[] Factors { get; init; }

        /// <summary>Whether its factors may be factored (see <see cref="IsMergeableMonomial"/>); a constant term is not.</summary>
        public required bool IsMergeable { get; init; }

        /// <summary>Whether the term is a numeric constant of the chain's type.</summary>
        public required bool IsConstant { get; init; }

        /// <summary>The numeric value a coefficient candidate compares: the coefficient, or the constant term itself.</summary>
        public object? NumericValue => IsConstant ? ((ConstantExpression)Term).Value : IsMergeable ? Coefficient?.Value : null;

        /// <summary>The constant node of <see cref="NumericValue"/>.</summary>
        public ConstantExpression? NumericConstant => IsConstant ? (ConstantExpression)Term : Coefficient;

        /// <summary>Decomposes a term.</summary>
        /// <param name="owner">The simplifier whose factor collection is used.</param>
        /// <param name="term">The term.</param>
        /// <param name="isNegative">Its sign.</param>
        /// <param name="type">The type of the chain.</param>
        /// <returns>The monomial.</returns>
        public static FactoringMonomial Create(ExpressionSimplifier owner, Expression term, bool isNegative, Type type)
        {
            var factors = new List<Expression>(4);
            AdditiveTermFamily family = AdditiveTermAnalysis.Decompose(owner, term, type, factors, out ConstantExpression? coefficient);
            bool mergeable = family is AdditiveTermFamily.Parameter or AdditiveTermFamily.Product or AdditiveTermFamily.Power or AdditiveTermFamily.Function
                && (coefficient is null || IsFiniteValue(coefficient.Value!));
            if (mergeable)
            {
                foreach (Expression factor in factors) mergeable &= IsMergeableFactor(factor);
            }

            bool constant = family == AdditiveTermFamily.Constant;
            return new FactoringMonomial
            {
                Term = term,
                IsNegative = isNegative,
                Coefficient = coefficient,
                Factors = mergeable ? factors.ToArray() : [],
                IsMergeable = mergeable,
                IsConstant = constant && coefficient is not null && IsFiniteValue(coefficient.Value!),
            };
        }

        /// <summary>Whether this term holds <paramref name="candidate"/>.</summary>
        /// <param name="candidate">A candidate.</param>
        /// <param name="statistics">The opt-in counters of the thread, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when it holds it.</returns>
        public bool Holds(FactoringCandidate candidate, FlattenAdmissionStatistics? statistics)
        {
            if (candidate.IsNumeric) return NumericValue is { } value && IsFactorableCoefficient(value) && Equals(value, candidate.Value);
            return IndexOf(candidate.Factor, statistics) >= 0;
        }

        /// <summary>The index of the first factor structurally equal to <paramref name="factor"/>, or <c>-1</c>.</summary>
        /// <param name="factor">A factor.</param>
        /// <param name="statistics">The opt-in counters of the thread, or <see langword="null"/>.</param>
        /// <returns>The index.</returns>
        private int IndexOf(Expression factor, FlattenAdmissionStatistics? statistics)
        {
            if (!IsMergeable) return -1;
            for (int index = 0; index < Factors.Length; index++)
            {
                if (statistics is not null) statistics.FactorComparisons++;
                if (FactorEqualityComparer.Instance.Equals(Factors[index], factor)) return index;
            }

            return -1;
        }

        /// <summary>
        /// The residue of this term once <paramref name="candidate"/> is set aside: its other factors and, for a symbolic
        /// candidate, its coefficient, as a canonical product; <c>1</c> when nothing is left.
        /// </summary>
        /// <param name="candidate">A candidate this term holds.</param>
        /// <param name="type">The type of the chain.</param>
        /// <param name="one">The constant <c>1</c> of the chain's type, created on first use.</param>
        /// <param name="owner">The simplifier that builds canonical products.</param>
        /// <returns>The residue.</returns>
        public Expression Residue(FactoringCandidate candidate, Type type, ref ConstantExpression? one, ExpressionSimplifier owner)
        {
            var rest = new List<Expression>(Factors.Length + 1);
            if (candidate.IsNumeric)
            {
                rest.AddRange(Factors);
            }
            else
            {
                int skip = IndexOf(candidate.Factor, null);
                for (int index = 0; index < Factors.Length; index++)
                {
                    if (index != skip) rest.Add(Factors[index]);
                }

                if (Coefficient is not null) rest.Add(Coefficient);
            }

            return rest.Count switch
            {
                0 => one ??= Expression.Constant(Convert.ChangeType(1, type), type),
                1 => rest[0],
                _ => owner.BuildCanonicalProduct(rest, false),
            };
        }
    }

    /// <summary>
    /// A factor or a numeric coefficient shared by terms of a chain being factored (<see cref="FactorCommonFactors"/>), with the
    /// number of distinct terms that hold it. A mutable working value, private to one round.
    /// </summary>
    private sealed class FactoringCandidate : IEquatable<FactoringCandidate>
    {
        /// <summary>The index of the last term counted, so that a factor repeated in one term counts that term once.</summary>
        private int _lastTerm;

        /// <summary>The canonical-order key of the factor, built for a tie only.</summary>
        private ExpressionCanonicalOrder.KeyNode? _key;

        /// <summary>Initializes a candidate met in the term at <paramref name="term"/>.</summary>
        /// <param name="factor">The factor, or the constant node of a coefficient.</param>
        /// <param name="value">The boxed value of a coefficient, or <see langword="null"/> for a factor.</param>
        /// <param name="term">The index of the term.</param>
        public FactoringCandidate(Expression factor, object? value, int term)
        {
            Factor = factor;
            Value = value;
            Terms = 1;
            _lastTerm = term;
        }

        /// <summary>The factor, or the constant node of a coefficient.</summary>
        public Expression Factor { get; }

        /// <summary>The boxed value of a coefficient, or <see langword="null"/> for a factor.</summary>
        public object? Value { get; }

        /// <summary>Whether the candidate is a numeric coefficient.</summary>
        public bool IsNumeric => Value is not null;

        /// <summary>The number of distinct terms that hold it.</summary>
        public int Terms { get; private set; }

        /// <summary>Counts the term at <paramref name="term"/>, once.</summary>
        /// <param name="term">The index of a term holding the candidate.</param>
        public void Count(int term)
        {
            if (term == _lastTerm) return;
            _lastTerm = term;
            Terms++;
        }

        /// <summary>The canonical-order key of the candidate's node.</summary>
        /// <param name="scopes">The lexical scope snapshot.</param>
        /// <returns>The key.</returns>
        public ExpressionCanonicalOrder.KeyNode KeyOf(ParameterExpression[][] scopes) => _key ??= GetCanonicalKey(Factor, scopes, record: false);

        /// <inheritdoc/>
        public bool Equals(FactoringCandidate? other)
            => other is not null && IsNumeric == other.IsNumeric && (IsNumeric ? Equals(Value, other.Value) : FactorEqualityComparer.Instance.Equals(Factor, other.Factor));

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as FactoringCandidate);

        /// <inheritdoc/>
        public override int GetHashCode() => IsNumeric ? Value!.GetHashCode() : FactorEqualityComparer.Instance.GetHashCode(Factor);
    }

    /// <summary>
    /// The structural equality of two factors, as the additive grouping compares them (<see cref="GroupEquals"/>, with the
    /// raw structural hash of the operation's metadata table): a thin adapter for the dictionaries of
    /// <see cref="FactorCommonFactors"/>, not a comparer of its own.
    /// </summary>
    private sealed class FactorEqualityComparer : IEqualityComparer<Expression>
    {
        /// <summary>The shared instance (stateless).</summary>
        public static readonly FactorEqualityComparer Instance = new();

        /// <inheritdoc/>
        public bool Equals(Expression? x, Expression? y)
            => x is null || y is null ? ReferenceEquals(x, y) : GroupEquals(x, y);

        /// <inheritdoc/>
        public int GetHashCode(Expression obj) => GetStructuralHash(obj, record: false);
    }

    /// <summary>
    /// Whether the additive chain rooted at <paramref name="root"/> is a <em>factorable chain</em> (mission C3c), which the
    /// flatten rule simplifies at once by the global factoring (<see cref="FlattenFactorableChain"/>): every crossed node is an
    /// ordinary operator of a flattened type and every term is a factorable term.
    /// </summary>
    /// <param name="root">The raw root of the chain.</param>
    /// <param name="statistics">The opt-in counters of the thread, or <see langword="null"/>.</param>
    /// <param name="refusalDepth">
    /// Receives, for a refused chain, the spine index of the node holding the refusing term or operator (as
    /// <see cref="IsLinearChain"/> reports a local cause: every chain that crosses that node is refused too); <c>0</c> for an
    /// admitted chain.
    /// </param>
    /// <returns><see langword="true"/> for a factorable chain.</returns>
    /// <remarks>
    /// <para>
    /// A factorable term (<see cref="IsFactorableTerm"/>) is a finite numeric constant, a parameter, an ordinary negation of a
    /// product, or an ordinary product of factors, each a parameter, a finite constant, an ordinary power of a parameter by a
    /// finite constant, or a call of an inert runtime function of <see cref="FactorableFunctionNames"/> on parameters and
    /// constants, at most one call per term. Preparing such a term runs no user code and gives a constant, a parameter, a
    /// negation, a power or a product of such factors: never a sum, so no factor of a prepared term is a sum.
    /// </para>
    /// <para>
    /// <b>The additive signature rules.</b> Level by level, the rules of a crossed node see two prepared operands: two terms, a
    /// term and a canonical sub-sum, or two sub-sums. The zero, constant, negation and reassociation rules are identities the
    /// canonicalization of the whole chain applies (zeros dropped, constants aggregated, signs collected). The factoring rules
    /// compare the operands of two products, a sub-sum counting as a single factor: since no factor of a term is a sum, a
    /// sub-sum only matches another sub-sum, and two terms match on a factor or a coefficient, which the global factoring finds
    /// among all the terms at once (<see cref="MergeIdenticalMonomials"/>, <see cref="FactorCommonFactors"/>). The other
    /// additive rules read two logarithms (<see cref="LogarithmSimplificationAddNumber"/>, never called by a factorable term)
    /// or two powers of calls (<see cref="AdditionOfCos2andSin2Number"/>: a power's base is a parameter, and a single call per
    /// product never becomes the square of a call). The global treatment therefore applies every identity the levels apply,
    /// among all the terms instead of the pairs the grouping of the source happens to form, and its result no longer depends
    /// on that grouping.
    /// </para>
    /// <para>
    /// <b>Effects.</b> Every operand of such a chain is inert (the functions are inert runtime methods, called on parameters
    /// and constants), so the effect gate admits every rule on it under every policy and no term is pinned.
    /// </para>
    /// <para>
    /// <b>Refusals that keep the levels.</b> A crossed node with two constant operands is refused, as by the linear family: the
    /// levels fold it exactly (<c>-0.0 + -0.0</c> is <c>-0.0</c>), where the canonicalization drops zeros symbolically. A
    /// shared sub-chain is accepted while its revisits stay below the distinct nodes (a tree and its rebuilt copy then give the
    /// same result); a DAG whose paths multiply is refused, after a number of visits linear in its distinct nodes, since its
    /// terms would be extracted once per path. Every refusal is local, so it is inherited by every chain that crosses the refusing node, and reported
    /// as such (<paramref name="refusalDepth"/>).
    /// </para>
    /// <para>
    /// One iterative walk of the crossed nodes and of the products of the terms, one structural visit per crossed node and per
    /// product node, which stops at the first refusal. Type tests come first, so an extension node is never read (ETC R4),
    /// and the provenance of a method is checked before any of its members is read (S5 A-10).
    /// </para>
    /// </remarks>
    private bool IsFactorableChain(BinaryExpression root, FlattenAdmissionStatistics? statistics, out int refusalDepth)
    {
        refusalDepth = 0;
        Type type = root.Type;
        if (!IsFlattenedChainType(type)) return false;

        if (statistics is not null) statistics.FactorableWalks++;
        Stack<BinaryExpression>? pending = null;
        HashSet<BinaryExpression>? branchNodes = null;
        int revisits = 0;
        BinaryExpression? node = root;
        int depth = 0;
        bool onSpine = true;
        while (node is not null)
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();

            // A crossed node reached again is a shared sub-chain, whose terms are extracted once per path. A few shared
            // occurrences are accepted, so that a tree and its rebuilt copy are treated alike (the comparer's reflexivity relies
            // on it: t - t with t shared or copied both cancel); a DAG whose paths multiply (2^n terms over n levels) is refused
            // once the revisits exceed the distinct nodes, after a number of visits linear in them. Only the nodes below the
            // first node with two crossed operands can be reached twice: a comb allocates nothing.
            if (!onSpine
                && !(branchNodes ??= new HashSet<BinaryExpression>(ReferenceEqualityComparer.Instance)).Add(node)
                && ++revisits > Math.Max(SharedRevisitAllowance, branchNodes.Count))
            {
                refusalDepth = depth;
                return false;
            }

            BinaryExpression? left = AsCrossedAdditiveNode(node.Left);
            BinaryExpression? right = AsCrossedAdditiveNode(node.Right);

            // Two constant operands are folded exactly by the levels (AdditionOfConstants keeps -0.0 + -0.0 = -0.0, where the
            // canonicalization drops zeros symbolically), as the linear family refuses them.
            if ((node.Left is ConstantExpression && node.Right is ConstantExpression)
                || (left is null && !IsFactorableTerm(node.Left, type))
                || (right is null && !IsFactorableTerm(node.Right, type))
                || !IsOrdinaryBinaryArithmetic(node))
            {
                refusalDepth = depth;
                return false;
            }

            if (left is not null && right is not null)
            {
                onSpine = false;
                (pending ??= new Stack<BinaryExpression>()).Push(right);
            }

            node = left ?? right ?? (pending is { Count: > 0 } ? pending.Pop() : null);
            if (onSpine) depth++;
        }

        return true;
    }

    /// <summary>Whether <paramref name="operand"/> is a factorable term of a chain of <paramref name="type"/> (see <see cref="IsFactorableChain"/>).</summary>
    /// <param name="operand">A raw operand of the chain.</param>
    /// <param name="type">The type of the chain.</param>
    /// <returns><see langword="true"/> for a factorable term.</returns>
    private static bool IsFactorableTerm(Expression operand, Type type) => operand switch
    {
        ParameterExpression parameter => ReferenceEquals(parameter.Type, type),
        ConstantExpression constant => IsFactorableConstant(constant, type),
        // The operand first (a framework product), then the operator, so that an extension operand is never read.
        UnaryExpression { NodeType: ExpressionType.Negate, Operand: BinaryExpression or ParameterExpression } negation
            => IsFactorableProduct(negation.Operand, type) && IsOrdinaryUnaryNegate(negation) && ReferenceEquals(negation.Type, type),
        _ => IsFactorableProduct(operand, type),
    };

    /// <summary>
    /// Whether <paramref name="expression"/> is a factor of a chain of <paramref name="type"/> or an ordinary product of such
    /// factors, at most one of them a call (see <see cref="IsFactorableChain"/>). Iterative over the products.
    /// </summary>
    /// <param name="expression">A term, or the operand of a negated term.</param>
    /// <param name="type">The type of the chain.</param>
    /// <returns><see langword="true"/> for a factorable product.</returns>
    private static bool IsFactorableProduct(Expression expression, Type type)
    {
        int calls = 0;
        Stack<Expression>? pending = null;
        Expression? current = expression;
        while (current is not null)
        {
            if (current is BinaryExpression { NodeType: ExpressionType.Multiply } product)
            {
                ExpressionResourceBudget.ConsumeStructuralVisit();

                // Both operands are framework nodes of a factor shape before the operator is classified (ETC R4).
                if (!IsFactorShape(product.Left) || !IsFactorShape(product.Right)) return false;
                if (!IsOrdinaryBinaryArithmetic(product) || !ReferenceEquals(product.Type, type)) return false;
                (pending ??= new Stack<Expression>()).Push(product.Right);
                current = product.Left;
                continue;
            }

            if (!IsFactorableFactor(current, type, ref calls)) return false;
            current = pending is { Count: > 0 } ? pending.Pop() : null;
        }

        return true;
    }

    /// <summary>Whether <paramref name="expression"/> has the class of a factorable factor or product, by type tests only.</summary>
    /// <param name="expression">An operand of a product.</param>
    /// <returns><see langword="true"/> for a parameter, a constant, a binary node or a call.</returns>
    private static bool IsFactorShape(Expression expression)
        => expression is ParameterExpression or ConstantExpression or BinaryExpression or MethodCallExpression;

    /// <summary>Whether <paramref name="factor"/>, which is not a product, is a factorable factor (see <see cref="IsFactorableChain"/>).</summary>
    /// <param name="factor">The factor.</param>
    /// <param name="type">The type of the chain.</param>
    /// <param name="calls">The number of calls met in the term so far; incremented for a call.</param>
    /// <returns><see langword="true"/> for a factorable factor.</returns>
    private static bool IsFactorableFactor(Expression factor, Type type, ref int calls) => factor switch
    {
        ParameterExpression parameter => ReferenceEquals(parameter.Type, type),
        ConstantExpression constant => IsFactorableConstant(constant, type),
        BinaryExpression { NodeType: ExpressionType.Power, Left: ParameterExpression power, Right: ConstantExpression exponent } node
            => ReferenceEquals(power.Type, type) && IsFactorableConstant(exponent, exponent.Type) && IsOrdinaryBinaryArithmetic(node) && ReferenceEquals(node.Type, type),
        MethodCallExpression call => ++calls <= 1 && IsFactorableCall(call, type),
        _ => false,
    };

    /// <summary>
    /// Whether <paramref name="call"/> is a factorable call: a static call of a runtime method of <see cref="Math"/> or
    /// <see cref="double"/> named in <see cref="FactorableFunctionNames"/>, inert for the effect analysis, of the chain's type,
    /// whose arguments are parameters and finite constants of their declared types.
    /// </summary>
    /// <param name="call">The call.</param>
    /// <param name="type">The type of the chain.</param>
    /// <returns><see langword="true"/> for a factorable call.</returns>
    private static bool IsFactorableCall(MethodCallExpression call, Type type)
    {
        // S5 A-10: the provenance first, before the name, the declaring type or the return type are read.
        if (call.Object is not null || !ExpressionReflectionProvenance.IsRuntime(call.Method)) return false;
        Type? declaringType = call.Method.DeclaringType;
        if ((declaringType != typeof(Math) && declaringType != typeof(double)) || !FactorableFunctionNames.Contains(call.Method.Name)) return false;
        if (!ReferenceEquals(call.Type, type) || !ExpressionEffectAnalysis.IsInert(ExpressionEffectAnalysis.MethodEffects(call.Method))) return false;
        foreach (Expression argument in call.Arguments)
        {
            bool accepted = argument switch
            {
                ParameterExpression parameter => ReferenceEquals(parameter.Type, typeof(double)),
                ConstantExpression constant => IsFactorableConstant(constant, typeof(double)),
                _ => false,
            };
            if (!accepted) return false;
        }

        return true;
    }

    /// <summary>Whether <paramref name="constant"/> is a finite boxed primitive of exactly <paramref name="type"/>, a <see cref="Types.Number"/> type.</summary>
    /// <param name="constant">A constant term, factor, exponent or argument.</param>
    /// <param name="type">Its expected type.</param>
    /// <returns><see langword="true"/> for such a constant; zero included.</returns>
    private static bool IsFactorableConstant(ConstantExpression constant, Type type)
        => constant.Value is { } value
            && ReferenceEquals(constant.Type, type)
            && ReferenceEquals(value.GetType(), type)
            && Types.Number.Contains(type)
            && IsFiniteValue(value);

    /// <summary>
    /// Simplifies a factorable chain admitted by <see cref="FlattenAdmission"/> (mission C3c): each term is prepared once, in
    /// source order, and the prepared terms are canonicalized once, with the global factoring (<see cref="CanonicalizeAdditiveTerms"/>).
    /// </summary>
    /// <param name="source">The raw root of the chain.</param>
    /// <param name="terms">The raw signed terms of the chain, in source order.</param>
    /// <returns>The canonical factored form of the chain.</returns>
    /// <remarks>See <see cref="IsFactorableChain"/> for why the result keeps every identity the levels apply.</remarks>
    private Expression FlattenFactorableChain(Expression source, FlattenedTerm[] terms)
    {
        FlattenAdmissionStatistics? statistics = t_admissionStatistics;
        if (statistics is not null) statistics.FactorableChains++;

        var signedTerms = new List<(Expression Term, bool IsNegative)>(terms.Length);
        foreach (FlattenedTerm term in terms)
        {
            Expression prepared = PrepareExpression(term.Operand);
            RecordLocallySimplified(prepared);
            CollectAdditiveTerms(signedTerms, prepared, term.Operation == ExpressionType.Subtract);
        }

        Expression result = CanonicalizeAdditiveTerms(signedTerms, source.Type, recordMetadata: false, AdditiveFactoring.CommonFactors);
        return NegateSubtractedSum(result, source.Type);
    }

    /// <summary>
    /// Rewrites a canonical sum whose terms are all subtracted as the negation of the sum of those terms, as the levels do:
    /// <c>0 - t</c> gives <c>-t</c> (<see cref="SubstractionWithZero(BinaryExpression, ConstantExpression, Expression)"/>)
    /// and <c>(-a) - b</c> gives <c>-(a + b)</c> (<see cref="SubstractionWithNegate(BinaryExpression, UnaryExpression, Expression)"/>).
    /// </summary>
    /// <param name="sum">The canonical sum of a factorable chain.</param>
    /// <param name="type">The type of the chain.</param>
    /// <returns>
    /// The negation, or <paramref name="sum"/> when a term is added or the type has no ordinary negation (an unsigned type keeps
    /// <c>0 - t</c>). A lone subtracted constant is folded instead, in every type, as
    /// <see cref="SubstractionOfConstants"/> folds <c>0 - c</c>: <c>0 - 1</c> gives <c>-1</c>, and <c>4294967295</c> for
    /// <see cref="uint"/>.
    /// </returns>
    private Expression NegateSubtractedSum(Expression sum, Type type)
    {
        if (sum is not BinaryExpression { NodeType: ExpressionType.Add or ExpressionType.Subtract }) return sum;

        // The canonical sum is a right-associated chain of groups, a subtracted group starting with 0 - t: a group that does not
        // ends the test at once, without walking the rest of a long chain. The walk is iterative (a chain may hold 10^5 terms).
        for (Expression group = sum; ; group = ((BinaryExpression)group).Right)
        {
            Expression head = group is BinaryExpression { NodeType: ExpressionType.Add } add ? add.Left : group;
            while (head is BinaryExpression { NodeType: ExpressionType.Subtract } difference && !IsOrdinaryZeroConstant(difference.Left)) head = difference.Left;
            if (head is not BinaryExpression { NodeType: ExpressionType.Subtract, Left: var zero } || !IsOrdinaryZeroConstant(zero)) return sum;
            if (group is not BinaryExpression { NodeType: ExpressionType.Add }) break;
        }

        var terms = new List<(Expression Term, bool IsNegative)>();
        CollectAdditiveTermsIteratively(terms, sum, new Stack<(Expression Node, bool IsNegative)>());
        terms.RemoveAll(static term => IsOrdinaryZeroConstant(term.Term));
        if (terms.Count == 0) return sum;
        foreach ((_, bool isNegative) in terms)
        {
            if (!isNegative) return sum;
        }

        if (terms.Count == 1 && terms[0].Term is ConstantExpression constant && IsAggregatableConstant(constant, type)
            && FoldConstants(ExpressionType.Subtract, type, Convert.ChangeType(0, type), constant.Value) is { } folded)
        {
            return folded;
        }

        if (!_ordinaryNegateMethods.ContainsKey(type)) return sum;

        for (int index = 0; index < terms.Count; index++) terms[index] = (terms[index].Term, false);
        Expression positive = terms.Count == 1 ? terms[0].Term : CanonicalizeAdditiveTerms(terms, type, recordMetadata: false);
        return Expression.Negate(positive);
    }
}
