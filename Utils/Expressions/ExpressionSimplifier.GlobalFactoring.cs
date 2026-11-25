using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
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
    /// Whether the rules of this instance finish the nodes they rebuild (mission C3d): a reassociation rule transforms the
    /// nodes it builds from its prepared operands, as a second call would, instead of returning them as built. The built-in
    /// type only, like <see cref="MergesMonomials"/> (which it follows, so that the level by level references of the tests
    /// keep matching the built-in type); a derived simplifier keeps the historical rules.
    /// </summary>
    internal virtual bool StabilizesRebuiltForms => MergesMonomials;

    /// <summary>
    /// Merges the terms of a canonicalized sum that hold the same factors, in any order, into one term whose coefficient is
    /// the sum of theirs (mission C3c, phase A): <c>x + 2*x + 3*x</c> gives <c>6*x</c>, <c>2*a*b + 3*b*a</c> gives
    /// <c>5*(a*b)</c>, <c>x - x</c> gives nothing.
    /// </summary>
    /// <param name="terms">The inert signed terms of the sum (zeros dropped, constants aggregated); updated in place.</param>
    /// <param name="type">The type of the sum, one of <see cref="Types.Number"/>.</param>
    /// <param name="scopes">The lexical scope snapshot of the canonicalization.</param>
    /// <param name="record">Whether computed hashes are stored in the operation's metadata table.</param>
    /// <param name="analyzed">
    /// Receives the analysis of the terms, or <see langword="null"/> when none was built (fewer than two non-constant terms), so
    /// that <see cref="FactorCommonFactors"/> does not decompose the same terms again (mission C3d).
    /// </param>
    /// <param name="sources">
    /// Receives, for each term of the updated list, the index of its analyzed term, <c>-1</c> for a merged term; <see langword="null"/>
    /// when the list is unchanged (every term is its own analyzed term).
    /// </param>
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
    private void MergeIdenticalMonomials(List<(Expression Term, bool IsNegative)> terms, Type type, ParameterExpression[][] scopes, bool record, out AdditiveTermAnalysis? analyzed, out int[]? sources)
    {
        analyzed = null;
        sources = null;
        if (terms.Count < 2 || !Types.Number.Contains(type)) return;

        // Without two terms other than constants nothing can merge: no analysis is built.
        int candidates = 0;
        foreach ((Expression term, _) in terms)
        {
            if (term is not ConstantExpression && ++candidates == 2) break;
        }

        if (candidates < 2) return;

        AdditiveTermAnalysis analysis = AdditiveTermAnalysis.Analyze(this, terms, type, scopes, record);
        analyzed = analysis;
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
        var kept = new int[count];
        for (int index = 0; index < count; index++)
        {
            ref readonly AnalyzedAdditiveTerm term = ref analysis[index];
            int representative = representatives[index];
            if (eligible[index] && merged[representative].Merged)
            {
                // The merged term takes the place of the group's first eligible member; a zero sum leaves nothing.
                if (index == firstMember[representative] && merged[representative].Term is { } mergedTerm)
                {
                    kept[terms.Count] = -1;
                    terms.Add((mergedTerm, merged[representative].IsNegative));
                }

                continue;
            }

            kept[terms.Count] = index;
            terms.Add((term.Expression, term.IsNegative));
        }

        sources = kept;
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
        // A declined fold (FoldConstants, S5A-04; unreachable here, since the type is never decimal) refuses the merge of the
        // group, whose terms are kept.
        object? sum = null;
        foreach ((Expression term, bool isNegative) in coefficients)
        {
            object value = ((ConstantExpression)term).Value!;
            sum = sum is null
                ? isNegative ? FoldConstants(ExpressionType.Subtract, type, Convert.ChangeType(0, type), value)?.Value : value
                : FoldConstants(isNegative ? ExpressionType.Subtract : ExpressionType.Add, type, sum, value)?.Value;
            if (sum is null) return null;
        }

        return sum;
    }

    /// <summary>
    /// Replaces the subtracted constant of a canonicalized sum (the constants are aggregated, so it holds one at most) by its
    /// negation (mission C3c): <c>y - 1</c> gives <c>-1 + y</c> instead of <c>(0 - 1) + y</c>, the form the next call folded
    /// (<see cref="SubstractionOfConstants"/> on the constant pair <c>0 - 1</c>), so the canonical sum is a fixed point.
    /// </summary>
    /// <param name="terms">The inert signed terms of the sum; updated in place.</param>
    /// <param name="type">The type of the sum.</param>
    /// <param name="unsigned">
    /// Whether a type without an ordinary negation (the unsigned integers) folds too (mission C3d,
    /// <see cref="StabilizesRebuiltForms"/>): <c>p - 9ul</c> gives <c>18446744073709551607 + p</c>, the modular value the next
    /// call folded <c>(0 - 9) + p</c> to. C3c kept <c>0 - c</c> there, which no call ever left alone.
    /// </param>
    /// <remarks>
    /// The negation is folded with <see cref="FoldConstants"/>, as the next call folds <c>0 - c</c>: exact for a
    /// floating-point constant, modular for an integral one (<c>0 - MinValue</c> is <c>MinValue</c>, <c>0 - 1u</c> is
    /// <c>4294967295</c>, as at run time).
    /// </remarks>
    private static void NegateSubtractedConstant(List<(Expression Term, bool IsNegative)> terms, Type type, bool unsigned)
    {
        if (!unsigned && !_ordinaryNegateMethods.ContainsKey(type)) return;
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
    /// <param name="analyzed">The phase A analysis of the terms, or <see langword="null"/> (see <see cref="MergeIdenticalMonomials"/>).</param>
    /// <param name="sources">The analyzed term of each term, <c>-1</c> for a merged one; <see langword="null"/> for the identity.</param>
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
    private void FactorCommonFactors(List<(Expression Term, bool IsNegative)> terms, Type type, ParameterExpression[][] scopes, AdditiveTermAnalysis? analyzed = null, int[]? sources = null)
    {
        // Recursive through the canonicalization of the residues: guard the execution stack (ETC R1a-Security).
        ExpressionResourceBudget.EnsureStack();
        if (terms.Count < 2 || !Types.Number.Contains(type)) return;

        FlattenAdmissionStatistics? statistics = t_admissionStatistics;
        var monomials = new List<FactoringMonomial>(terms.Count);
        for (int index = 0; index < terms.Count; index++)
        {
            // Mission C3d: a term the phase A analysis already decomposed is not decomposed again.
            int source = analyzed is null ? -1 : sources is null ? index : sources[index];
            (Expression term, bool isNegative) = terms[index];
            monomials.Add(source >= 0 ? FactoringMonomial.FromAnalysis(analyzed!, source) : FactoringMonomial.Create(this, term, isNegative, type));
        }

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

                // A holder of the opposite coefficient keeps its residue with the opposite sign: (-c)*t is -(c*t) exactly.
                bool isNegative = monomial.IsNegative ^ best.FlipsSign(monomial.NumericValue!);
                residues.Add((monomial.Residue(best, type, ref one, this), isNegative));
                allNegative &= isNegative;
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
                if (!symbolic.TryGetValue(factor, out FactoringCandidate? candidate)) symbolic.Add(factor, new FactoringCandidate(factor, index));
                else candidate.Count(index);
            }

            // Coefficients are indexed by their magnitude: c and -c are one candidate (see FactoringCandidate.FactoredValue).
            if (monomial.NumericValue is { } value && IsFactorableCoefficient(value))
            {
                object magnitude = CoefficientMagnitude(value);
                if (!numeric.TryGetValue(magnitude, out FactoringCandidate? candidate)) numeric.Add(magnitude, new FactoringCandidate(monomial.NumericConstant!, value, magnitude, index));
                else candidate.Count(index, value);
            }
        }

        FactoringCandidate? best = null;
        foreach (FactoringCandidate candidate in symbolic.Values) best = Better(candidate, best, scopes, rejected);
        foreach (FactoringCandidate candidate in numeric.Values) best = Better(candidate, best, scopes, rejected);
        return best;
    }

    /// <summary>
    /// The magnitude of a factorable coefficient, under which <see cref="FindBestCandidate"/> indexes it: the value itself when
    /// it is positive, of an unsigned type, or a signed integral minimum (whose negation is itself); otherwise its exact negation
    /// (IEEE-754 for the floating-point types, so bit for bit). So <c>2</c> and <c>-2</c> are one candidate, and
    /// <c>2*x + (-2)*y</c> is factored as <c>2*x - 2*y</c> is: the term analysis already treats the sign of a coefficient and
    /// the sign of a term as one sign (<c>docs/expressions/SimplifierFlatten.md</c> §20.2, §21.3).
    /// </summary>
    /// <param name="value">A boxed factorable coefficient (<see cref="IsFactorableCoefficient"/>).</param>
    /// <returns>The boxed magnitude, of the same type.</returns>
    private static object CoefficientMagnitude(object value) => value switch
    {
        int intValue when intValue < 0 && intValue != int.MinValue => -intValue,
        long longValue when longValue < 0 && longValue != long.MinValue => -longValue,
        double doubleValue when doubleValue < 0 => -doubleValue,
        float floatValue when floatValue < 0 => -floatValue,
        _ => value,
    };

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
    /// <returns>
    /// <see langword="false"/> when the coefficient cannot be folded (a non-finite product, or a fold <see cref="FoldConstants"/>
    /// declines): the candidate is abandoned and the terms holding it are kept; the factors collected here are local.
    /// </returns>
    private bool TryBuildFactoredTerm(FactoringCandidate candidate, Expression sum, Type type, out Expression? term, out bool isNegative)
    {
        term = null;
        isNegative = false;
        var factors = new List<Expression>(4);
        object? coefficient = candidate.FactoredValue;
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
            return Build(term, isNegative, family, coefficient, CollectionsMarshal.AsSpan(factors));
        }

        /// <summary>
        /// Builds the monomial of a term the analysis of phase A already decomposed (mission C3d): the same family, coefficient
        /// and factors as <see cref="Create"/>, which runs the same <see cref="AdditiveTermAnalysis.Decompose"/>.
        /// </summary>
        /// <param name="analysis">The analysis of the canonicalized sum.</param>
        /// <param name="index">The index of the analyzed term.</param>
        /// <returns>The monomial.</returns>
        public static FactoringMonomial FromAnalysis(AdditiveTermAnalysis analysis, int index)
        {
            ref readonly AnalyzedAdditiveTerm term = ref analysis[index];
            return Build(term.Expression, term.IsNegative, term.Family, term.Coefficient, analysis.GetFactors(index));
        }

        /// <summary>Builds a monomial from the decomposition of its term.</summary>
        /// <param name="term">The term.</param>
        /// <param name="isNegative">Its sign.</param>
        /// <param name="family">The family of its factor part.</param>
        /// <param name="coefficient">Its numeric coefficient, or <see langword="null"/>.</param>
        /// <param name="factors">Its factors, the coefficient set aside.</param>
        /// <returns>The monomial.</returns>
        private static FactoringMonomial Build(Expression term, bool isNegative, AdditiveTermFamily family, ConstantExpression? coefficient, ReadOnlySpan<Expression> factors)
        {
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
            if (candidate.IsNumeric) return NumericValue is { } value && IsFactorableCoefficient(value) && Equals(CoefficientMagnitude(value), candidate.Magnitude);
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

        /// <summary>Whether two holders of a coefficient candidate have opposite values (<c>c</c> and <c>-c</c>).</summary>
        private bool _mixedSigns;

        /// <summary>Initializes a factor candidate met in the term at <paramref name="term"/>.</summary>
        /// <param name="factor">The factor.</param>
        /// <param name="term">The index of the term.</param>
        public FactoringCandidate(Expression factor, int term)
        {
            Factor = factor;
            Terms = 1;
            _lastTerm = term;
        }

        /// <summary>Initializes a coefficient candidate met in the term at <paramref name="term"/>.</summary>
        /// <param name="constant">The constant node of the coefficient.</param>
        /// <param name="value">The boxed value of the coefficient in that term.</param>
        /// <param name="magnitude">Its magnitude (<see cref="CoefficientMagnitude"/>), the key of the candidate.</param>
        /// <param name="term">The index of the term.</param>
        public FactoringCandidate(ConstantExpression constant, object value, object magnitude, int term)
            : this(constant, term)
        {
            Value = value;
            Magnitude = magnitude;
        }

        /// <summary>The factor, or the constant node of a coefficient.</summary>
        public Expression Factor { get; }

        /// <summary>The boxed value of the coefficient in its first holder, or <see langword="null"/> for a factor.</summary>
        public object? Value { get; }

        /// <summary>The magnitude of a coefficient, the key of the candidate, or <see langword="null"/> for a factor.</summary>
        public object? Magnitude { get; }

        /// <summary>Whether the candidate is a numeric coefficient.</summary>
        public bool IsNumeric => Value is not null;

        /// <summary>
        /// The coefficient factored out: the common value when every holder has the same one (the historical factoring, so
        /// <c>-2*x + -2*y</c> still gives <c>-2 * (x + y)</c>), the magnitude when holders have opposite values (<c>2*x + -2*y</c>
        /// gives <c>2 * (x - y)</c>, as <c>2*x - 2*y</c> does); <see langword="null"/> for a factor.
        /// </summary>
        public object? FactoredValue => _mixedSigns ? Magnitude : Value;

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

        /// <summary>Counts the term at <paramref name="term"/>, once, holding the coefficient with <paramref name="value"/>.</summary>
        /// <param name="term">The index of a term holding the candidate.</param>
        /// <param name="value">The boxed value of its coefficient, of the candidate's magnitude.</param>
        public void Count(int term, object value)
        {
            if (!Equals(value, Value)) _mixedSigns = true;
            Count(term);
        }

        /// <summary>
        /// Whether a holder of value <paramref name="value"/> changes its sign once <see cref="FactoredValue"/> is factored out: its
        /// value is the opposite of the factored one.
        /// </summary>
        /// <param name="value">The boxed value of a holder's coefficient.</param>
        /// <returns><see langword="true"/> for the holders of the opposite value.</returns>
        public bool FlipsSign(object value) => IsNumeric && !Equals(value, FactoredValue);

        /// <summary>
        /// The canonical-order key of the candidate's node: of the factor, or of the magnitude of a coefficient, so that a tie
        /// between coefficients never depends on which holder came first.
        /// </summary>
        /// <param name="scopes">The lexical scope snapshot.</param>
        /// <returns>The key.</returns>
        public ExpressionCanonicalOrder.KeyNode KeyOf(ParameterExpression[][] scopes)
            => _key ??= GetCanonicalKey(IsNumeric && !Equals(Value, Magnitude) ? Expression.Constant(Magnitude, Factor.Type) : Factor, scopes, record: false);

        /// <inheritdoc/>
        public bool Equals(FactoringCandidate? other)
            => other is not null && IsNumeric == other.IsNumeric && (IsNumeric ? Equals(Magnitude, other.Magnitude) : FactorEqualityComparer.Instance.Equals(Factor, other.Factor));

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as FactoringCandidate);

        /// <inheritdoc/>
        public override int GetHashCode() => IsNumeric ? Magnitude!.GetHashCode() : FactorEqualityComparer.Instance.GetHashCode(Factor);
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
    /// Gives the result of a refused chain boundary to the global factoring when that result is a factorable chain (mission
    /// C3c, <c>docs/expressions/SimplifierFlatten.md</c> §21): the factoring the next call applied to it, applied once, now.
    /// </summary>
    /// <param name="root">The raw root of the refused chain, the boundary of its chain.</param>
    /// <param name="result">The result of its level by level treatment.</param>
    /// <returns>
    /// The canonical factored form of <paramref name="result"/>, or <paramref name="result"/> itself when it is not a
    /// factorable chain (or the negation of one), when it is a linear chain, outside an operation, for a derived simplifier and
    /// while the factorable family is switched off.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Why.</b> The admission decides on the raw root (<see cref="FlattenAdmission"/>): a chain holding a constant pair, the
    /// negation of a constant, a product by a sum the levels fold, or a factor of <c>1</c>, is refused and simplified level by
    /// level, where each canonicalization merges identical monomials but never extracts common factors (the factors would
    /// depend on how the source groups the terms). The result is often a factorable chain, which the next call admitted and
    /// factored: <c>((2 - 10) - z) - (x * z)</c> gave <c>-8 + ((0 - z) + (0 - (x * z)))</c>, then
    /// <c>-8 + (0 - (z * (1 + x)))</c>.
    /// </para>
    /// <para>
    /// <b>Where.</b> Only at the boundary of the refused chain (<see cref="ExpressionTransformer.FinishRefusedChain"/>), once its
    /// whole treatment is done: never on a crossed operand or on a node a rule of the chain builds, whose result an enclosing
    /// canonicalization collects again, so the factoring is applied to all the terms of the chain at once, as the next call's
    /// admission applied it, and once per chain.
    /// </para>
    /// <para>
    /// <b>Safety.</b> The result is tested exactly as the next call tested it: a linear chain is left alone (its terms are
    /// distinct), and a chain is factored only when <see cref="IsFactorableChain"/> admits it, so every proof of that family
    /// holds (§13.5): every term is inert and factorable, every crossed node an ordinary operator, no constant pair is
    /// crossed. The levels already folded the constants the raw admission refused, exactly; no admission condition is widened.
    /// The terms are the terms of the result, finished by the levels: they are not prepared again (the next call prepared each
    /// term, which rewrites no factorable term the levels finished; §21.4). Both walks consume the
    /// structural budget and stop at the first refusal; the factoring is the one of <see cref="FlattenFactorableChain"/>.
    /// </para>
    /// <para>
    /// <b>Suffixes (mission M4).</b> A result that stays refused as a whole (a quotient, a call that is not inert) is searched
    /// for the factorable suffix the next call admits on its own (<see cref="FinishRefusedSuffix"/>); when its factoring changes
    /// it, the levels of the prefix are run again on top of it, and the new result is finished in turn, as the next call
    /// finishes it, for at most <see cref="MaximumSuffixRounds"/> rounds.
    /// </para>
    /// </remarks>
    internal override Expression FinishRefusedChain(Expression root, Expression result)
    {
        if (GetType() != typeof(ExpressionSimplifier) || t_factorableChainsDisabled || t_refusedChainFinishingDisabled || ExpressionResourceBudget.ActiveSession is null) return result;

        FlattenAdmissionStatistics? statistics = t_admissionStatistics;
        for (int round = 0; ; round++)
        {
            // The negation of a sum (all its terms subtracted, C3d): the next call's chain is its operand.
            bool negated = result is UnaryExpression negation && IsOrdinaryUnaryNegate(negation);
            Expression candidate = negated ? ((UnaryExpression)result).Operand : result;
            if (AsCrossedAdditiveNode(candidate) is not BinaryExpression chain) return result;

            if (statistics is not null) statistics.BoundaryTests++;
            if (IsLinearChain(chain, null, out _, out _)) return result;
            if (IsFactorableChain(chain, null, out _))
            {
                if (statistics is not null) statistics.BoundaryFactorings++;
                var terms = new List<(Expression Term, bool IsNegative)>();
                CollectAdditiveTermsIteratively(terms, chain, new Stack<(Expression Node, bool IsNegative)>());
                Expression factored = NegateSubtractedSum(CanonicalizeAdditiveTerms(terms, chain.Type, recordMetadata: false, AdditiveFactoring.CommonFactors), chain.Type);
                return negated ? BuildNegation(factored) : factored;
            }

            // Mission M4: the chain stays refused; the next call would still factor a factorable suffix of it.
            if (t_refusedSuffixFinishingDisabled || round == MaximumSuffixRounds) return result;
            Expression? finished = FinishRefusedSuffix(chain, statistics);

            // Mission B: no factoring of the whole chain or of a suffix; a binary rule of the next call may still factor a pair the
            // last canonicalization built.
            if (finished is null && !t_refusedBinaryFinishingDisabled) finished = FinishRefusedBinaryPair(chain, statistics);
            if (finished is null) return result;
            result = negated ? BuildNegation(finished) : finished;
        }
    }

    /// <summary>
    /// The number of suffix rounds (<see cref="FinishRefusedSuffix"/>) the boundary finishing of one refused chain may run: each
    /// round removes at least one term of the chain, and the stability campaign never needs a second one (see
    /// <c>docs/expressions/SimplifierFlatten.md</c> §22); the bound keeps the work of a boundary linear in its result whatever
    /// the prefix levels rebuild.
    /// </summary>
    private const int MaximumSuffixRounds = 2;

    /// <summary>
    /// Factors the factorable suffix of a refused boundary result, as the next call factors it (mission M4,
    /// <c>docs/expressions/SimplifierFlatten.md</c> §22): the longest suffix of its right spine whose terms are all factorable,
    /// when the global factoring changes it, then the levels of the prefix on top of the factored suffix.
    /// </summary>
    /// <param name="chain">The result of the levels of a refused chain, neither linear nor factorable as a whole.</param>
    /// <param name="statistics">The opt-in counters of the thread, or <see langword="null"/>.</param>
    /// <returns>The new result of the chain, or <see langword="null"/> when there is no such suffix or its factoring changes nothing.</returns>
    /// <remarks>
    /// <para>
    /// <b>What the next call does.</b> The canonical reconstruction of a refused chain is a right-associated sum
    /// <c>g0 + (g1 + (… + gn))</c>; nothing offers its suffixes to the admission during the call that builds them. The next call
    /// offers the root (refused again), then, top-down, each crossed right operand as a chain of its own: the first one the
    /// admission accepts is treated by its family, every one above it is refused and simplified level by level on top of that
    /// result. A suffix is refused as long as it holds a term that is not factorable, so the first factorable suffix the next
    /// call meets is the longest suffix of factorable terms; any suffix admitted above it by another family is a linear or an
    /// anchored chain, whose sub-chains are linear chains too, and a subtracted crossed operand (the only start of a grouped
    /// quotient chain) stops the search. When that suffix is a linear chain, the next call's linear canonicalization changes
    /// nothing; otherwise its factorable rule gives the global factoring of its terms, computed here exactly as that rule and
    /// <see cref="FinishRefusedChain"/> compute it.
    /// </para>
    /// <para>
    /// <b>The prefix.</b> Each prefix node is then run as the next call runs it: its left operand prepared again, in source
    /// order, while the chain is marked as refused (so that no node below is a boundary, as during the levels), then the
    /// additive signature rules of the level and its finalization (<see cref="TransformLevel"/>) on the prepared operand and
    /// the result below, after the <c>NaN</c> fold of the parent's preparation. A binary rule that factors a prefix term with
    /// the factored suffix is therefore applied exactly as on the next call; nothing is factored that the next call does not
    /// factor.
    /// </para>
    /// <para>
    /// <b>Safety.</b> Only the suffix is given to the global factoring, and it is admitted by <see cref="IsFactorableChain"/>
    /// itself, so every proof of that family holds on it (§13.5): its terms are inert, so no evaluation moves, and no quotient,
    /// pinned term or call that is not inert enters the factoring; the terms of the prefix are only seen by the rules and the
    /// canonicalization of the levels, under the effect gate, as on every level. The search is one bottom-up pass over the
    /// spine (one structural visit per spine node, each term or group tested once), then one linear and one factorable walk of
    /// the candidate, so a long refused result costs a linear number of visits, never one walk per suffix.
    /// </para>
    /// </remarks>
    private Expression? FinishRefusedSuffix(BinaryExpression chain, FlattenAdmissionStatistics? statistics)
    {
        if (statistics is not null) statistics.SuffixSearches++;
        Type type = chain.Type;
        if (!IsFlattenedChainType(type)) return null;

        // The right spine. A subtraction of a crossed operand may start a grouped quotient chain the next call admits above the
        // suffix: no suffix is searched below it.
        var spine = new List<BinaryExpression>();
        for (BinaryExpression? node = chain; node is not null; node = AsCrossedAdditiveNode(node.Right))
        {
            ExpressionResourceBudget.ConsumeStructuralVisit();
            if (node.NodeType == ExpressionType.Subtract && AsCrossedAdditiveNode(node.Right) is not null) return null;
            spine.Add(node);
        }

        // Bottom-up: the first node of the longest suffix whose own operands are all factorable (a proper suffix only).
        int start = -1;
        for (int index = spine.Count - 1; index >= 1; index--)
        {
            if (!IsFactorableSuffixNode(spine[index], index == spine.Count - 1, type)) break;
            start = index;
        }

        if (start < 1) return null;
        BinaryExpression suffix = spine[start];
        if (statistics is not null) statistics.SuffixTests++;
        if (IsLinearChain(suffix, null, out _, out _) || !IsFactorableChain(suffix, null, out _)) return null;

        var terms = new List<(Expression Term, bool IsNegative)>();
        var pending = new Stack<(Expression Node, bool IsNegative)>();
        CollectAdditiveTermsIteratively(terms, suffix, pending);
        int termCount = CountNonZeroTerms(terms);
        if (termCount < 2) return null;

        // The suffix comes out of a canonicalization of the levels, which merged its identical monomials and aggregated its
        // constants: only a common factor can still remove a term. Its candidates are indexed once (no key, no sort) before the
        // whole canonicalization runs, so a suffix without any common factor costs one indexing pass.
        if (!HasFactoringCandidate(terms, type, statistics)) return null;
        Expression factored =NegateSubtractedSum(CanonicalizeAdditiveTerms(terms, type, recordMetadata: false, AdditiveFactoring.CommonFactors), type);

        // Only a factoring or a merge removes terms. A suffix that keeps all its terms (no common factor, or only a new sign
        // representation such as 0 - a + (0 - b) as -(a + b)) is rebuilt by the levels of the prefix into the historical result:
        // the prefix is not run again.
        Expression factoredSum = factored is UnaryExpression negation && IsOrdinaryUnaryNegate(negation) ? negation.Operand : factored;
        terms.Clear();
        if (AsCrossedAdditiveNode(factoredSum) is BinaryExpression rebuilt) CollectAdditiveTermsIteratively(terms, rebuilt, pending);
        else terms.Add((factoredSum, false));
        if (CountNonZeroTerms(terms) >= termCount) return null;

        if (FlattenRuleDispatcherOf(chain) is not FlattenRuleDispatcher dispatcher) return null;
        if (statistics is not null)
        {
            statistics.SuffixFactorings++;
            statistics.SuffixPrefixLevels += start;
        }

        bool callerWithinRefusedChain = WithinRefusedChain;
        WithinRefusedChain = true;
        try
        {
            return RunPrefixLevels(spine, start, factored, dispatcher);
        }
        finally
        {
            WithinRefusedChain = callerWithinRefusedChain;
        }
    }

    /// <summary>
    /// Runs the spine nodes above <paramref name="start"/> of a refused boundary result as the next call runs them, on top of the
    /// new result of the node <paramref name="start"/> (missions M4 and B): the left operand of each prefix node prepared again, in
    /// source order, then, bottom-up, the additive signature rules of its level and its finalization (<see cref="TransformLevel"/>)
    /// on that operand and the <c>NaN</c>-folded result below.
    /// </summary>
    /// <param name="spine">The right spine of the result, root first.</param>
    /// <param name="start">The index of the node whose new result is <paramref name="below"/>; the nodes before it are the prefix.</param>
    /// <param name="below">The new result of <c>spine[start]</c>.</param>
    /// <param name="dispatcher">The dispatcher of the chain's group.</param>
    /// <returns>The new result of the root.</returns>
    /// <remarks>The caller sets the refused-chain marker, so that no node prepared here is the boundary of a chain.</remarks>
    private Expression RunPrefixLevels(List<BinaryExpression> spine, int start, Expression below, FlattenRuleDispatcher dispatcher)
    {
        var prepared = new Expression[start];
        for (int index = 0; index < start; index++) prepared[index] = PrepareExpression(spine[index].Left);

        Expression result = below;
        for (int index = start - 1; index >= 0; index--)
        {
            result = TransformLevel(dispatcher, spine[index], prepared[index], FoldNaNOperand(result));
        }

        return result;
    }

    /// <summary>
    /// Whether the own operands of a spine node of a refused result are those of a factorable chain (see
    /// <see cref="FinishRefusedSuffix"/>, <see cref="IsFactorableChain"/>): its left operand, and for the last node its right
    /// operand too, a factorable term or a factorable crossed group; an ordinary operator; not two constant operands.
    /// </summary>
    /// <param name="node">A spine node.</param>
    /// <param name="last">Whether it is the last node of the spine (its right operand is not crossed).</param>
    /// <param name="type">The type of the chain.</param>
    /// <returns><see langword="true"/> when nothing in the node refuses a factorable suffix that contains it.</returns>
    /// <remarks>Type tests first, then the operator, as in <see cref="IsFactorableChain"/> (ETC R4).</remarks>
    private bool IsFactorableSuffixNode(BinaryExpression node, bool last, Type type)
    {
        if (node.Left is ConstantExpression && node.Right is ConstantExpression) return false;
        if (!IsFactorableSuffixOperand(node.Left, type)) return false;
        if (last && !IsFactorableSuffixOperand(node.Right, type)) return false;
        return IsOrdinaryBinaryArithmetic(node);
    }

    /// <summary>
    /// Whether two of <paramref name="terms"/> share a candidate of the common factoring (<see cref="FindBestCandidate"/>): a
    /// necessary condition for <see cref="FactorCommonFactors"/> to remove a term. A candidate whose factoring is later abandoned
    /// only costs the canonicalization that finds out.
    /// </summary>
    /// <param name="terms">The collected signed terms of a factorable suffix; not modified.</param>
    /// <param name="type">The type of the chain.</param>
    /// <param name="statistics">The opt-in counters of the thread, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a candidate is held by two terms.</returns>
    private bool HasFactoringCandidate(List<(Expression Term, bool IsNegative)> terms, Type type, FlattenAdmissionStatistics? statistics)
    {
        if (!Types.Number.Contains(type)) return false;
        var monomials = new List<FactoringMonomial>(terms.Count);
        foreach ((Expression term, bool isNegative) in terms)
        {
            if (!IsOrdinaryZeroConstant(term)) monomials.Add(FactoringMonomial.Create(this, term, isNegative, type));
        }

        return monomials.Count >= 2 && FindBestCandidate(monomials, CaptureLexicalScopeSnapshot(), [], statistics) is not null;
    }

    /// <summary>Counts the collected terms that are not ordinary zero constants (<see cref="IsOrdinaryZeroConstant"/>).</summary>
    /// <param name="terms">The collected signed terms.</param>
    /// <returns>The number of terms a canonicalization keeps before merging.</returns>
    private static int CountNonZeroTerms(List<(Expression Term, bool IsNegative)> terms)
    {
        int count = 0;
        foreach ((Expression term, _) in terms)
        {
            if (!IsOrdinaryZeroConstant(term)) count++;
        }

        return count;
    }

    /// <summary>Whether an operand of a spine node is a factorable term or a crossed group that is a factorable chain.</summary>
    /// <param name="operand">The operand.</param>
    /// <param name="type">The type of the chain.</param>
    /// <returns><see langword="true"/> for a factorable operand.</returns>
    private bool IsFactorableSuffixOperand(Expression operand, Type type)
        => AsCrossedAdditiveNode(operand) is BinaryExpression group
            ? IsFactorableChain(group, null, out _)
            : IsFactorableTerm(operand, type);

    /// <summary>
    /// Whether the inert terms of a canonicalization are all subtracted, in a type with an ordinary negation (mission C3d):
    /// the canonicalization then builds the negation of their sum, as <see cref="NegateSubtractedSum"/> does for a factorable
    /// chain.
    /// </summary>
    /// <param name="terms">The inert signed terms, zeros dropped, constants aggregated and subtracted constants folded.</param>
    /// <param name="type">The type of the sum.</param>
    /// <returns><see langword="true"/> when there is at least one term and none is added.</returns>
    private static bool IsNegatedSum(List<(Expression Term, bool IsNegative)> terms, Type type)
    {
        if (terms.Count == 0 || !_ordinaryNegateMethods.ContainsKey(type)) return false;
        foreach ((_, bool isNegative) in terms)
        {
            if (!isNegative) return false;
        }

        return true;
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
        return BuildNegation(positive);
    }
}
