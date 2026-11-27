using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Utils.Expressions;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// The consistency of the finalization and of the orientation of factorings (mission K/N/F,
/// <c>docs/expressions/SimplifierFlatten.md</c> §26): the finishing of a growing factored coefficient (K), the orientation of a
/// negated sum (N) and the orientation of an extracted numeric factor (F).
/// </summary>
public partial class ExpressionSimplifier
{
    /// <summary>
    /// The number of terms up to which the coefficient of a binary factoring whose terms are all added is canonicalized (K,
    /// <see cref="CanonicalizeFactoredCoefficient"/>). A longer coefficient keeps the historical node, so a chain whose coefficient
    /// grows by one term per level (the <c>dagfactor</c> family of §14.8) pays at most this many terms per level, never the whole
    /// coefficient: the added work is bounded by a constant per factoring.
    /// </summary>
    internal const int FinishedCoefficientTerms = 16;

    /// <summary>Backing field of <see cref="FactoredCoefficientFinishingDisabled"/>.</summary>
    [ThreadStatic]
    private static bool t_factoredCoefficientFinishingDisabled;

    /// <summary>Backing field of <see cref="NegatedSumOrientationDisabled"/>.</summary>
    [ThreadStatic]
    private static bool t_negatedSumOrientationDisabled;

    /// <summary>Backing field of <see cref="FactorOrientationDisabled"/>.</summary>
    [ThreadStatic]
    private static bool t_factorOrientationDisabled;

    /// <summary>
    /// Gets or sets, for this thread, whether the coefficient of a binary factoring whose terms are all added keeps the historical
    /// node whatever its size (K off, as on <c>master</c> <c>b44a1c07</c>). Audits and tests only. <see langword="false"/> by default.
    /// </summary>
    internal static bool FactoredCoefficientFinishingDisabled
    {
        get => t_factoredCoefficientFinishingDisabled;
        set => t_factoredCoefficientFinishingDisabled = value;
    }

    /// <summary>
    /// Gets or sets, for this thread, whether the negation of a sum keeps its node whatever the orientation of its terms (N off, as
    /// on <c>master</c> <c>b44a1c07</c>). Audits and tests only. <see langword="false"/> by default.
    /// </summary>
    internal static bool NegatedSumOrientationDisabled
    {
        get => t_negatedSumOrientationDisabled;
        set => t_negatedSumOrientationDisabled = value;
    }

    /// <summary>
    /// Gets or sets, for this thread, whether a factored numeric factor keeps the sign of its holders (F off, as on <c>master</c>
    /// <c>b44a1c07</c>). Audits and tests only. <see langword="false"/> by default.
    /// </summary>
    internal static bool FactorOrientationDisabled
    {
        get => t_factorOrientationDisabled;
        set => t_factorOrientationDisabled = value;
    }

    /// <summary>
    /// Gets or sets, for this thread, every switch of the mission at once: <see langword="true"/> gives the engine of
    /// <c>master</c> <c>b44a1c07</c>, the reference of the <c>reference</c> campaign. Reads <see langword="true"/> only when the
    /// three switches are set.
    /// </summary>
    internal static bool FactoringSignConsistencyDisabled
    {
        get => t_factoredCoefficientFinishingDisabled && t_negatedSumOrientationDisabled && t_factorOrientationDisabled;
        set
        {
            t_factoredCoefficientFinishingDisabled = value;
            t_negatedSumOrientationDisabled = value;
            t_factorOrientationDisabled = value;
        }
    }

    /// <summary>
    /// Whether the coefficients <paramref name="left"/> and <paramref name="right"/> of a binary factoring hold together at most
    /// <see cref="FinishedCoefficientTerms"/> terms through ordinary additions (K), counted without allocating.
    /// </summary>
    /// <param name="left">The left coefficient, a prepared operand or the coefficient of an earlier factoring.</param>
    /// <param name="right">The right coefficient.</param>
    /// <returns><see langword="true"/> when the coefficient is small enough to be canonicalized.</returns>
    /// <remarks>
    /// The walk stops as soon as the bound is passed, so it visits at most <see cref="FinishedCoefficientTerms"/> + 1 terms and as
    /// many additions whatever the size of the coefficients, and it reads only node types and runtime reflection objects
    /// (<see cref="IsOrdinaryBinaryArithmetic(BinaryExpression)"/>); a shared subtree is counted once per occurrence, as the
    /// canonicalization collects it. It needs no stack guard: it recurses only on the left operand, whose depth is bounded by the
    /// count.
    /// </remarks>
    private static bool HasFewCoefficientTerms(Expression left, Expression right)
    {
        int count = CountCoefficientTerms(left, 0);
        return count <= FinishedCoefficientTerms && CountCoefficientTerms(right, count) <= FinishedCoefficientTerms;
    }

    /// <summary>Counts the terms of an ordinary sum, stopping past <see cref="FinishedCoefficientTerms"/>.</summary>
    /// <param name="expression">A coefficient.</param>
    /// <param name="count">The running count.</param>
    /// <returns>The updated count, or a value above the bound.</returns>
    private static int CountCoefficientTerms(Expression expression, int count)
    {
        // The right spine is followed by the loop, the left operands by the recursion; every term found raises the count, so
        // both stop past the bound.
        while (count <= FinishedCoefficientTerms)
        {
            if (expression is not BinaryExpression { NodeType: ExpressionType.Add } addition || !IsOrdinaryBinaryArithmetic(addition)) return count + 1;
            count = addition.Left is BinaryExpression { NodeType: ExpressionType.Add }
                ? CountCoefficientTerms(addition.Left, count)
                : count + 1;
            expression = addition.Right;
        }

        return count;
    }

    /// <summary>
    /// The orientation of an extracted numeric factor (F): the magnitude of a negative factorable coefficient, or
    /// <see langword="null"/> when <paramref name="factor"/> keeps its sign.
    /// </summary>
    /// <param name="factor">The common factor a binary factoring recognized.</param>
    /// <returns>The positive factor, of the same type, or <see langword="null"/>.</returns>
    /// <remarks>
    /// <para>
    /// A binary factoring takes its factor from the physical pair it receives: <c>(-2*x) - (-2*y)</c> gave <c>-2 * (x - y)</c>
    /// while <c>(-2*x) + (2*y)</c>, the same value with the sign carried elsewhere, gave <c>2 * (y - x)</c> through the global
    /// factoring, which already indexes the coefficients by their magnitude. The factored numeric factor is now always the magnitude
    /// (<see cref="CoefficientMagnitude"/>), and the sign of every holder moves to its residue: the orientation of the result then
    /// depends on the logical signs of the terms only, decided by the canonicalization of the residues (a sum whose residues are
    /// all subtracted is negated as a whole, <see cref="IsSubtractedCoefficient"/>, as the global factoring negates a group of
    /// subtracted residues).
    /// </para>
    /// <para>
    /// Admitted: a finite factorable constant (<see cref="IsFactorableCoefficient"/>) of an inert arithmetic type whose magnitude
    /// differs from it, that is a negative <see cref="int"/>, <see cref="long"/>, <see cref="float"/> or <see cref="double"/> other
    /// than a minimal integer. <see cref="short"/> is not: <see cref="CoefficientMagnitude"/> does not index it, so the global
    /// factoring never equalizes <c>2</c> and <c>-2</c> in that type either, and both factorings keep the sign there. Only the
    /// boxed primitive is read.
    /// </para>
    /// </remarks>
    private ConstantExpression? OrientFactor(Expression factor)
    {
        if (t_factorOrientationDisabled || !StabilizesRebuiltForms) return null;
        if (factor is not ConstantExpression { Value: { } value } constant || !IsInertArithmeticType(constant.Type) || value.GetType() != constant.Type) return null;
        if (!IsFactorableCoefficient(value)) return null;
        object magnitude = CoefficientMagnitude(value);
        return Equals(magnitude, value) ? null : Expression.Constant(magnitude, constant.Type);
    }

    /// <summary>
    /// Builds the factored product of a binary factoring with the orientation of F: <c>(leftCoefficient op rightCoefficient) *
    /// factor</c>, written with the magnitude of a negative numeric factor and the opposite coefficient.
    /// </summary>
    /// <param name="nodeType"><see cref="ExpressionType.Add"/> or <see cref="ExpressionType.Subtract"/>.</param>
    /// <param name="leftCoefficient">The coefficient of the left term.</param>
    /// <param name="rightCoefficient">The coefficient of the right term.</param>
    /// <param name="factor">The common factor.</param>
    /// <returns>The factored product.</returns>
    /// <remarks>
    /// <c>(-m) * (l - r)</c> is <c>m * (r - l)</c> and <c>(-m) * (l + r)</c> is <c>m * ((-l) - r)</c>: the negation of the factor
    /// and of the coefficient are exact (two's complement integers form a ring, IEEE-754 negation only flips a sign bit and the
    /// rounding of a sum is symmetric), so only the representation changes. The negated left coefficient goes through
    /// <see cref="BuildNegation"/>, which folds a constant and removes a double negation; the coefficient is then built and
    /// finalized by <see cref="BuildFactoredProduct"/> exactly as before. Nothing is evaluated, compared or traversed here.
    /// </remarks>
    private Expression BuildOrientedFactoredProduct(ExpressionType nodeType, Expression leftCoefficient, Expression rightCoefficient, Expression factor)
    {
        if (OrientFactor(factor) is not { } magnitude) return BuildFactoredProduct(nodeType, leftCoefficient, rightCoefficient, factor);
        return nodeType == ExpressionType.Subtract
            ? BuildFactoredProduct(ExpressionType.Subtract, rightCoefficient, leftCoefficient, magnitude)
            : BuildFactoredProduct(ExpressionType.Subtract, BuildNegation(leftCoefficient), rightCoefficient, magnitude);
    }

    /// <summary>
    /// The orientation of a subtracted factored product (F): every inert subtracted product holding exactly one ordinary sum among
    /// its factors, a sum that holds both an added and a subtracted term, becomes the added product of the opposite sum
    /// (<c>0 - (2 * (c + (0 - d)))</c> gives <c>2 * ((0 - c) + d)</c>).
    /// </summary>
    /// <param name="terms">The inert signed terms of a canonicalization; updated in place.</param>
    /// <param name="type">The type of the sum.</param>
    /// <remarks>
    /// <para>
    /// <b>Why.</b> The global factoring of a whole chain writes a group of residues that holds both signs as an added product of
    /// their sum (<c>-2*c + 2*d</c> gives <c>2 * ((0 - c) + d)</c>), the residues carrying the signs. A group factored on its own
    /// and then subtracted by its parent (<c>a - ((2*c) + (-2*d))</c>) gave the subtracted product <c>0 - (2 * (c + (0 - d)))</c>
    /// instead: the same value, another structure, chosen by the association (the factoring-sign residue of the <c>signs</c>
    /// campaign, 25.6 and 25.12). A subtracted product of a mixed sum is now rebuilt with the opposite sum, so the sign of such a
    /// term is carried by its residues whatever placement produced it. The representative depends only on the value of the term:
    /// the inversion of the group by a parent (<c>N</c> of §25.5) gives a subtracted product, which is rebuilt into the same
    /// added product, so <c>ν ∘ N ∘ ν = ν ∘ N</c> holds for these terms.
    /// </para>
    /// <para>
    /// <b>Scope.</b> Built-in type only (<see cref="StabilizesRebuiltForms"/>), terms already inert (pinned terms never reach this
    /// method), a product of ordinary multiplications whose single additive factor is an ordinary sum of an operator that does not
    /// throw (<see cref="CanonicalizesOperator"/>), with no <c>NaN</c> term. A sum whose terms are all added keeps the subtracted
    /// product (its opposite is a negated sum, so the product would come back subtracted), and a product with two additive factors
    /// is left as it is (no factor is preferred). The opposite sum is the canonicalization of the opposite terms, and the product
    /// is the canonical product of the factors (<see cref="BuildCanonicalProduct"/>): exact, since <c>-(m * s)</c> and
    /// <c>m * (-s)</c> are the same ring element on the integers and the same value on IEEE-754 numbers, the canonical
    /// <c>0 - t</c> taking only the signed-zero latitude every canonical sum takes.
    /// </para>
    /// <para>
    /// <b>Cost.</b> The factors of a subtracted product are collected once (one structural visit per product, as every
    /// multiplicative collection), and the sum is canonicalized once; the rebuilt term is added, so an enclosing canonicalization
    /// that collects it again does nothing more.
    /// </para>
    /// </remarks>
    private void OrientSubtractedFactoredProducts(List<(Expression Term, bool IsNegative)> terms, Type type)
    {
        if (t_factorOrientationDisabled || !StabilizesRebuiltForms) return;
        for (int index = 0; index < terms.Count; index++)
        {
            (Expression term, bool isNegative) = terms[index];
            if (!isNegative || term is not BinaryExpression { NodeType: ExpressionType.Multiply } product || !IsOrdinaryBinaryArithmetic(product)) continue;

            // One ordinary sum among the factors of a canonical product (right-associated, at most OrientedProductFactors factors,
            // no product as a left operand): a bounded walk, so a shared multiplicative DAG is never unfolded (§24).
            if (!TryCollectCanonicalFactors(product, out List<Expression>? factors)) continue;
            int sumIndex = -1;
            for (int factor = 0; factor < factors.Count; factor++)
            {
                if (factors[factor] is not BinaryExpression { NodeType: ExpressionType.Add or ExpressionType.Subtract } candidate || !IsOrdinaryBinaryArithmetic(candidate)) continue;
                if (sumIndex >= 0 || !CanonicalizesOperator(candidate)) { sumIndex = -2; break; }
                sumIndex = factor;
            }

            if (sumIndex < 0) continue;

            // Its opposite terms; a sum whose terms are all added (its opposite is a negated sum) or holding NaN is kept.
            var opposite = new List<(Expression Term, bool IsNegative)>();
            CollectAdditiveTerms(opposite, factors[sumIndex], true);
            bool added = false;
            bool nan = false;
            foreach ((Expression oppositeTerm, bool oppositeIsNegative) in opposite)
            {
                added |= !oppositeIsNegative;
                nan |= IsNaNConstant(oppositeTerm);
            }

            if (!added || nan) continue;
            Expression sum = CanonicalizeAdditiveTerms(opposite, factors[sumIndex].Type, recordMetadata: false);
            if (sum is UnaryExpression) continue;

            factors[sumIndex] = sum;
            Expression rebuilt = BuildCanonicalProduct(factors, record: false);
            terms[index] = rebuilt is UnaryExpression negation && IsOrdinaryUnaryNegate(negation) ? (negation.Operand, true) : (rebuilt, false);
        }
    }

    /// <summary>The largest number of factors of a product <see cref="OrientSubtractedFactoredProducts"/> reads.</summary>
    private const int OrientedProductFactors = 8;

    /// <summary>
    /// Collects the factors of a product in the canonical shape <c>f0 * (f1 * (… * fn))</c>, at most
    /// <see cref="OrientedProductFactors"/> of them, without following any left operand.
    /// </summary>
    /// <param name="product">An ordinary product.</param>
    /// <param name="factors">Receives the factors in order, or <see langword="null"/>.</param>
    /// <returns><see langword="false"/> when the product is longer, or holds an ordinary product as a left operand.</returns>
    private static bool TryCollectCanonicalFactors(BinaryExpression product, [NotNullWhen(true)] out List<Expression>? factors)
    {
        factors = null;
        var collected = new List<Expression>(4);
        Expression node = product;
        while (node is BinaryExpression { NodeType: ExpressionType.Multiply } multiply && IsOrdinaryBinaryArithmetic(multiply))
        {
            if (multiply.Left is BinaryExpression { NodeType: ExpressionType.Multiply } || collected.Count == OrientedProductFactors - 1) return false;
            collected.Add(multiply.Left);
            node = multiply.Right;
        }

        collected.Add(node);
        factors = collected;
        return true;
    }

    /// <summary>
    /// The orientation of a negated sum (N): the canonical sum of the opposite terms of <paramref name="operand"/>, when it is
    /// not itself a negated sum.
    /// </summary>
    /// <param name="operand">The prepared operand of an ordinary <c>Negate</c> node.</param>
    /// <param name="distributed">Receives the sum, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the negation is replaced by <paramref name="distributed"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Why.</b> The canonicalization writes a sum whose terms are all subtracted as the negation of their sum
    /// (<see cref="IsNegatedSum"/>), and every other sum flat. A negation can however be built around a sum that holds an added
    /// term: <see cref="SubstractionWithNegate(BinaryExpression, UnaryExpression, Expression)"/> rewrites <c>(-q) - s</c> as
    /// <c>-(q + s)</c> whatever the signs of <c>s</c>, and a source may hold <c>-(x - y)</c>-like groups. When an additive parent
    /// collects that negation, it inverts its terms and the result is flat; at the root, or as the operand of a product, it stayed
    /// <c>-((0 - a) + …)</c>, while the same leaves associated otherwise gave the flat sum: the structure depended on the
    /// association (the negated-sum residue of the <c>signs</c> campaign, 25.6 and 25.12). The negation of such a sum is now
    /// finalized as its parent would collect it: the opposite terms, canonicalized. A sum whose opposite terms are all subtracted
    /// gives back the negation (<c>-(x + y)</c> stays), so the decision is the one <see cref="IsNegatedSum"/> takes for the same
    /// terms, wherever they come from.
    /// </para>
    /// <para>
    /// <b>Termination.</b> The result is the canonical form of the terms; it is never transformed again at this level, and the
    /// next call finds either a negation that this method keeps (its opposite terms are all subtracted) or a canonical sum. No
    /// rule builds a negation of a flat sum from a canonical sum, so no cycle can alternate the two forms.
    /// </para>
    /// <para>
    /// <b>Safety.</b> Built-in type only (<see cref="StabilizesRebuiltForms"/>), ordinary additive nodes of an operator that does
    /// not throw (<see cref="CanonicalizesOperator"/>, so never <see cref="decimal"/>), every term inert (no call, no observable
    /// read: nothing is moved across an evaluation) and no <c>NaN</c> term (left to the policy-dependent <c>NaN</c> fold, as
    /// <see cref="CanonicalizeReassociation"/> leaves it). <c>-(a + b)</c> and <c>(-a) + (-b)</c> are the same ring element on the
    /// integers and the same value on IEEE-754 numbers (negation is exact and rounding symmetric); the canonical <c>0 - t</c> of a
    /// subtracted term may change the sign of a zero, the latitude every canonical sum already takes. The walk is the collection
    /// of the canonicalization, charged as the canonicalization of a level.
    /// </para>
    /// </remarks>
    private bool TryOrientNegatedSum(Expression operand, [NotNullWhen(true)] out Expression? distributed)
    {
        distributed = null;
        if (t_negatedSumOrientationDisabled || !StabilizesRebuiltForms) return false;
        if (operand is not BinaryExpression { NodeType: ExpressionType.Add or ExpressionType.Subtract } sum
            || !CanCanonicalizeCommutativeBinary(sum)
            || !CanonicalizesOperator(sum))
        {
            return false;
        }

        // The terms of the operand, inverted: what an additive parent collects from the negation.
        var terms = new List<(Expression Term, bool IsNegative)>();
        CollectAdditiveTerms(terms, sum, true);
        bool added = false;
        foreach ((Expression term, bool isNegative) in terms)
        {
            if (IsNaNConstant(term) || !ExpressionEffectAnalysis.IsInert(ExpressionEffectAnalysis.Analyze(term))) return false;

            // An inverted term that is added, or a constant (whose sign the canonicalization folds into it), may give a flat sum.
            added |= !isNegative || term is ConstantExpression;
        }

        // Every inverted term is subtracted: the canonicalization gives the negation back (-(x + y) stays as it is).
        if (!added) return false;

        Expression result = CanonicalizeAdditiveTerms(terms, sum.Type, recordMetadata: false);
        if (result is UnaryExpression negation && IsOrdinaryUnaryNegate(negation)) return false;
        distributed = result;
        return true;
    }
}
