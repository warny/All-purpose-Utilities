using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Utils.Expressions;
using Utils.Objects;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// The shape of the factor part of an additive term (mission C3b, <c>docs/expressions/SimplifierFlatten.md</c> §12): what
/// remains of the term once its numeric coefficient, if any, is set aside. Data only.
/// </summary>
internal enum AdditiveTermFamily : byte
{
    /// <summary>A numeric constant: the term is its own coefficient and has no factor.</summary>
    Constant = 0,

    /// <summary>A single parameter (<c>x</c>, <c>3 * x</c>).</summary>
    Parameter = 1,

    /// <summary>At least two factors once the coefficient is set aside (<c>a * x</c>, <c>(a + 3) * x</c>).</summary>
    Product = 2,

    /// <summary>A single ordinary power (<c>x²</c>, <c>2 * x²</c>), kept whole as one factor.</summary>
    Power = 3,

    /// <summary>A single call (<c>sin(x)</c>, <c>3 * cos(x)</c>), kept whole as one factor.</summary>
    Function = 4,

    /// <summary>
    /// Any other node, kept whole and indivisible: a negation, a division, a conversion, a custom operator, an extension
    /// node. It is never traversed: only type tests classify it (ETC R4).
    /// </summary>
    Opaque = 5,
}

/// <summary>
/// One analyzed term of an additive chain (mission C3b): the prepared term, its sign and position, its numeric coefficient,
/// the location of its factors in the analysis that owns it (<see cref="ExpressionSimplifier.AdditiveTermAnalysis"/>), the
/// family of its factor part and the metadata of the whole term. Data only.
/// </summary>
/// <param name="Expression">The prepared term (a collected additive term: never an ordinary sum, difference or negation).</param>
/// <param name="IsNegative">Whether the term is subtracted.</param>
/// <param name="SourceIndex">Its index in the collected terms of the chain, in source order.</param>
/// <param name="Coefficient">
/// Its numeric coefficient: the single numeric constant factor of an ordinary product, of exactly the term's type (the
/// constant node itself, never converted), or the term itself for a constant term; <see langword="null"/> when the term has
/// no constant factor (an implicit coefficient of one) or more than one (they then stay factors, nothing is folded).
/// </param>
/// <param name="FactorStart">The index of its first factor in the factor arrays of the analysis.</param>
/// <param name="FactorCount">The number of its factors (zero for a constant term).</param>
/// <param name="Family">The family of its factor part.</param>
/// <param name="IsDecomposable">
/// Whether the term was decomposed: <see langword="false"/> for an <see cref="AdditiveTermFamily.Opaque"/> term, which is
/// its own single, indivisible factor.
/// </param>
/// <param name="IsFunctionLike">
/// The classification of the whole term for the additive grouping order
/// (<see cref="ExpressionSimplifier"/>'s <c>ClassifyForAdditiveGrouping</c>): a call, or a power of a call.
/// </param>
/// <param name="CategoryOrder">The function category of a function-like term (an order only, never an identity).</param>
/// <param name="StructuralHash">The raw structural hash of the whole term.</param>
/// <param name="FactorHash">
/// An order-independent combination of the raw structural hashes of its factors: two terms with the same factors, in any
/// order, have the same value; equal values are never taken as equal factors without a structural comparison.
/// </param>
/// <param name="State">What the analyzing simplifier instance may assume about further simplifications of the term.</param>
internal readonly record struct AnalyzedAdditiveTerm(
    Expression Expression,
    bool IsNegative,
    int SourceIndex,
    ConstantExpression? Coefficient,
    int FactorStart,
    int FactorCount,
    AdditiveTermFamily Family,
    bool IsDecomposable,
    bool IsFunctionLike,
    int CategoryOrder,
    int StructuralHash,
    int FactorHash,
    CanonicalizationState State);

public partial class ExpressionSimplifier
{
    /// <summary>
    /// The analysis of the signed terms of an additive chain (mission C3b, <c>docs/expressions/SimplifierFlatten.md</c> §12):
    /// each term decomposed into a numeric coefficient and a list of factors, classified, and described by the metadata of
    /// mission C3a, so that a later stage (C3c) can find the terms sharing factors and merge their coefficients without
    /// extracting anything again from the raw expressions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Layout.</b> The terms are small values held in one array; their factors and the raw hashes of the factors are held
    /// in two shared arrays, each term pointing to its slice. Sorting or grouping the terms should permute indices, not copy
    /// the values. Canonical-order keys are built on demand only (<see cref="GetKey"/>), since most uses compare hashes.
    /// </para>
    /// <para>
    /// <b>Metadata.</b> Hashes, keys and states come from the accessors of C3a, which handle the three situations of a node:
    /// a value stored by a canonicalization of the running public operation is reused without any walk; a node with no stored
    /// value is computed (and stored only when <c>record</c> is set); a key stored under other lexical scope frames is never
    /// reused: only the key is rebuilt, the hash, independent of the scopes, still being reused. A
    /// <see cref="CanonicalizationState.LocallySimplified"/> state proves nothing beyond the end of a local treatment: the
    /// analysis reports it and never derives a decision from it.
    /// </para>
    /// <para>
    /// <b>Safety.</b> Decomposition uses type tests and framework properties only: an extension node is classified by its
    /// runtime type and never read (ETC R4), the operator of a product is classified only once it is known to be a framework
    /// node, and the members of a call whose method is not a runtime reflection object are never read (S5 A-10): such a call
    /// is function-like with the last category. Hashes are the raw structural hashes, which run no user code.
    /// </para>
    /// </remarks>
    internal sealed class AdditiveTermAnalysis
    {
        /// <summary>The lexical scope snapshot the keys are built under.</summary>
        private readonly ParameterExpression[][] _scopes;

        /// <summary>Whether computed hashes and keys are stored in the operation's metadata table.</summary>
        private readonly bool _record;

        /// <summary>The analyzed terms, in source order.</summary>
        private readonly AnalyzedAdditiveTerm[] _terms;

        /// <summary>The factors of every term, each term owning a contiguous slice.</summary>
        private readonly Expression[] _factors;

        /// <summary>The raw structural hash of each factor, at the same index.</summary>
        private readonly int[] _factorHashes;

        /// <summary>The keys built so far, by term index; <see langword="null"/> until the first request.</summary>
        private ExpressionCanonicalOrder.KeyNode?[]? _keys;

        /// <summary>Initializes an analysis from its arrays.</summary>
        /// <param name="scopes">The scope snapshot of the keys.</param>
        /// <param name="record">Whether computed values are stored.</param>
        /// <param name="terms">The analyzed terms.</param>
        /// <param name="factors">The factors.</param>
        /// <param name="factorHashes">The hashes of the factors.</param>
        private AdditiveTermAnalysis(ParameterExpression[][] scopes, bool record, AnalyzedAdditiveTerm[] terms, Expression[] factors, int[] factorHashes)
        {
            _scopes = scopes;
            _record = record;
            _terms = terms;
            _factors = factors;
            _factorHashes = factorHashes;
        }

        /// <summary>Gets the number of analyzed terms.</summary>
        public int Count => _terms.Length;

        /// <summary>Gets the analyzed term at <paramref name="index"/>.</summary>
        /// <param name="index">The source index of the term.</param>
        /// <returns>A reference to the term, valid as long as the analysis.</returns>
        public ref readonly AnalyzedAdditiveTerm this[int index] => ref _terms[index];

        /// <summary>
        /// Analyzes the signed terms of an ordinary additive chain of <paramref name="type"/>, in their order.
        /// </summary>
        /// <param name="owner">The simplifier instance whose states are reported.</param>
        /// <param name="terms">The collected signed terms (<see cref="CollectAdditiveTerms"/>), in source order.</param>
        /// <param name="type">The type of the chain, hence of every term.</param>
        /// <param name="scopes">The lexical scope snapshot of the keys (<see cref="CaptureLexicalScopeSnapshot"/>).</param>
        /// <param name="record">Whether computed hashes and keys are stored for the next canonicalizations of the operation.</param>
        /// <returns>The analysis.</returns>
        /// <remarks>
        /// One pass per term: its factors are collected through ordinary products only (<see cref="CollectMultiplicativeFactors"/>,
        /// the collection of the multiplicative canonicalization), and each factor and the whole term are hashed once,
        /// through the operation's metadata table. Nothing is simplified, converted or evaluated.
        /// </remarks>
        public static AdditiveTermAnalysis Analyze(ExpressionSimplifier owner, IReadOnlyList<(Expression Term, bool IsNegative)> terms, Type type, ParameterExpression[][] scopes, bool record)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(terms);
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(scopes);

            var analyzed = new AnalyzedAdditiveTerm[terms.Count];
            var factors = new List<Expression>(terms.Count);
            var termFactors = new List<Expression>(4);
            for (int index = 0; index < terms.Count; index++)
            {
                (Expression term, bool isNegative) = terms[index];
                termFactors.Clear();
                AdditiveTermFamily family = Decompose(owner, term, type, termFactors, out ConstantExpression? coefficient);
                int start = factors.Count;
                factors.AddRange(termFactors);
                (bool isFunctionLike, int categoryOrder) = Classify(term);
                analyzed[index] = new AnalyzedAdditiveTerm(
                    term,
                    isNegative,
                    index,
                    coefficient,
                    start,
                    termFactors.Count,
                    family,
                    family != AdditiveTermFamily.Opaque,
                    isFunctionLike,
                    categoryOrder,
                    GetStructuralHash(term, record),
                    0,
                    owner.GetCanonicalizationState(term));
            }

            var factorArray = factors.ToArray();
            var factorHashes = new int[factorArray.Length];
            for (int index = 0; index < factorArray.Length; index++) factorHashes[index] = GetStructuralHash(factorArray[index], record);
            for (int index = 0; index < analyzed.Length; index++)
            {
                ref AnalyzedAdditiveTerm term = ref analyzed[index];
                term = term with { FactorHash = CombineFactorHashes(factorHashes.AsSpan(term.FactorStart, term.FactorCount)) };
            }

            return new AdditiveTermAnalysis(scopes, record, analyzed, factorArray, factorHashes);
        }

        /// <summary>Gets the factors of the term at <paramref name="index"/>, in the order of the term.</summary>
        /// <param name="index">The source index of the term.</param>
        /// <returns>Its factors; empty for a constant term.</returns>
        public ReadOnlySpan<Expression> GetFactors(int index)
        {
            ref readonly AnalyzedAdditiveTerm term = ref _terms[index];
            return _factors.AsSpan(term.FactorStart, term.FactorCount);
        }

        /// <summary>Gets the raw structural hashes of the factors of the term at <paramref name="index"/>.</summary>
        /// <param name="index">The source index of the term.</param>
        /// <returns>The hashes, in the order of <see cref="GetFactors"/>.</returns>
        public ReadOnlySpan<int> GetFactorHashes(int index)
        {
            ref readonly AnalyzedAdditiveTerm term = ref _terms[index];
            return _factorHashes.AsSpan(term.FactorStart, term.FactorCount);
        }

        /// <summary>
        /// Gets the canonical-order key of the term at <paramref name="index"/> under the scopes of the analysis, building it
        /// on the first request only (through <see cref="GetCanonicalKey"/>, so a key stored under the same scope frames is
        /// reused and a key stored under other frames is rebuilt).
        /// </summary>
        /// <param name="index">The source index of the term.</param>
        /// <returns>Its key.</returns>
        public ExpressionCanonicalOrder.KeyNode GetKey(int index)
        {
            ExpressionCanonicalOrder.KeyNode?[] keys = _keys ??= new ExpressionCanonicalOrder.KeyNode?[_terms.Length];
            return keys[index] ??= GetCanonicalKey(_terms[index].Expression, _scopes, _record);
        }

        /// <summary>Describes the whole term at <paramref name="index"/> with the metadata of mission C3a.</summary>
        /// <param name="index">The source index of the term.</param>
        /// <returns>The description: the hash and the state of the analysis, and the key (built on demand).</returns>
        public SimplifiedExpressionInfo Describe(int index)
        {
            ref readonly AnalyzedAdditiveTerm term = ref _terms[index];
            return new SimplifiedExpressionInfo(term.Expression, term.StructuralHash, GetKey(index), term.State);
        }

        /// <summary>
        /// Whether the terms at <paramref name="first"/> and <paramref name="second"/> have the same factors, in any order
        /// (the same multiset): their factor hashes match, then each factor of one is matched with a distinct, structurally
        /// equal factor of the other (<see cref="ExpressionComparer.StructuralEqualsRaw(Expression?, Expression?)"/>).
        /// </summary>
        /// <param name="first">The source index of a term.</param>
        /// <param name="second">The source index of another term.</param>
        /// <returns><see langword="true"/> for the same factors; two constant terms have the same (empty) factors.</returns>
        /// <remarks>
        /// The coefficients are not compared: <c>x</c>, <c>2 * x</c> and <c>3 * x</c> have the same factors. A hash match is
        /// never taken as equality on its own.
        /// </remarks>
        public bool HaveSameFactors(int first, int second)
        {
            ref readonly AnalyzedAdditiveTerm x = ref _terms[first];
            ref readonly AnalyzedAdditiveTerm y = ref _terms[second];
            if (x.FactorCount != y.FactorCount || x.FactorHash != y.FactorHash) return false;
            if (x.FactorCount == 0) return true;

            ReadOnlySpan<Expression> xFactors = GetFactors(first);
            ReadOnlySpan<Expression> yFactors = GetFactors(second);
            ReadOnlySpan<int> xHashes = GetFactorHashes(first);
            ReadOnlySpan<int> yHashes = GetFactorHashes(second);

            // A factor of the second term matched once is never matched again (a multiset, not a set): at most a few factors.
            Span<bool> used = yFactors.Length <= 16 ? stackalloc bool[yFactors.Length] : new bool[yFactors.Length];
            for (int i = 0; i < xFactors.Length; i++)
            {
                int match = -1;
                for (int j = 0; j < yFactors.Length && match < 0; j++)
                {
                    if (!used[j] && xHashes[i] == yHashes[j] && GroupEquals(xFactors[i], yFactors[j])) match = j;
                }

                if (match < 0) return false;
                used[match] = true;
            }

            return true;
        }

        /// <summary>
        /// Finds a factor common to the terms at <paramref name="first"/> and <paramref name="second"/>: the first factor of
        /// the first term (in its order) that is structurally equal to a factor of the second, both hashes matching first.
        /// </summary>
        /// <param name="first">The source index of a term.</param>
        /// <param name="second">The source index of another term.</param>
        /// <param name="firstFactor">Receives the index of the common factor within the factors of the first term, or <c>-1</c>.</param>
        /// <param name="secondFactor">Receives the index of the common factor within the factors of the second term, or <c>-1</c>.</param>
        /// <returns><see langword="true"/> when a common factor exists (<c>a * x</c> and <c>3 * x</c> share <c>x</c>).</returns>
        /// <remarks>
        /// The factor found depends on the order of the factors of the first term, which is the order of its prepared form,
        /// never the name or the source position of a variable: choosing which common factor to factor out is left to C3c.
        /// </remarks>
        public bool TryFindCommonFactor(int first, int second, out int firstFactor, out int secondFactor)
        {
            ReadOnlySpan<Expression> xFactors = GetFactors(first);
            ReadOnlySpan<Expression> yFactors = GetFactors(second);
            ReadOnlySpan<int> xHashes = GetFactorHashes(first);
            ReadOnlySpan<int> yHashes = GetFactorHashes(second);
            for (int i = 0; i < xFactors.Length; i++)
            {
                for (int j = 0; j < yFactors.Length; j++)
                {
                    if (xHashes[i] == yHashes[j] && GroupEquals(xFactors[i], yFactors[j]))
                    {
                        firstFactor = i;
                        secondFactor = j;
                        return true;
                    }
                }
            }

            firstFactor = -1;
            secondFactor = -1;
            return false;
        }

        /// <summary>
        /// Groups the terms by their factors (<see cref="HaveSameFactors"/>): the terms are bucketed by factor hash, and a term
        /// joins the first group of its bucket whose representative has the same factors, so terms are only compared within a
        /// bucket, never all against all.
        /// </summary>
        /// <returns>
        /// For each term, the source index of the first term of its group (its representative): <c>x</c>, <c>2 * x</c> and
        /// <c>3 * x</c> share one representative, <c>a * x</c> has its own.
        /// </returns>
        public int[] GroupByFactors()
        {
            // Mission C3d: a bucket is the chain of its representatives in insertion order (first one in the dictionary, the
            // next ones linked through nextRepresentative), so no list is allocated per bucket.
            var representatives = new int[_terms.Length];
            var nextRepresentative = new int[_terms.Length];
            var buckets = new Dictionary<int, int>(_terms.Length);
            for (int index = 0; index < _terms.Length; index++)
            {
                nextRepresentative[index] = -1;
                int hash = _terms[index].FactorHash;
                if (!buckets.TryGetValue(hash, out int candidate))
                {
                    buckets.Add(hash, index);
                    representatives[index] = index;
                    continue;
                }

                int representative = -1;
                int last = candidate;
                for (; candidate >= 0; candidate = nextRepresentative[candidate])
                {
                    if (HaveSameFactors(candidate, index))
                    {
                        representative = candidate;
                        break;
                    }

                    last = candidate;
                }

                if (representative < 0)
                {
                    nextRepresentative[last] = index;
                    representative = index;
                }

                representatives[index] = representative;
            }

            return representatives;
        }

        /// <summary>
        /// Decomposes <paramref name="term"/> into its numeric coefficient and its factors, and classifies its factor part.
        /// </summary>
        /// <param name="owner">The simplifier whose factor collection is used.</param>
        /// <param name="term">A collected additive term.</param>
        /// <param name="type">The type of the chain.</param>
        /// <param name="factors">Receives the factors, in the order of the term.</param>
        /// <param name="coefficient">Receives the numeric coefficient (see <see cref="AnalyzedAdditiveTerm.Coefficient"/>).</param>
        /// <returns>The family of the factor part.</returns>
        internal static AdditiveTermFamily Decompose(ExpressionSimplifier owner, Expression term, Type type, List<Expression> factors, out ConstantExpression? coefficient)
        {
            coefficient = null;
            if (term is ConstantExpression constant)
            {
                if (IsNumericCoefficient(constant, type))
                {
                    coefficient = constant;
                    return AdditiveTermFamily.Constant;
                }

                factors.Add(term);
                return AdditiveTermFamily.Opaque;
            }

            if (term is not BinaryExpression { NodeType: ExpressionType.Multiply } product || !IsOrdinaryBinaryArithmetic(product))
            {
                factors.Add(term);
                return FactorFamily(term);
            }

            owner.CollectMultiplicativeFactors(factors, product);
            int constantIndex = -1;
            for (int index = 0; index < factors.Count; index++)
            {
                if (factors[index] is not ConstantExpression factor || !IsNumericCoefficient(factor, type)) continue;
                if (constantIndex >= 0)
                {
                    // Two constant factors: nothing is folded, they all stay factors.
                    constantIndex = -1;
                    break;
                }

                constantIndex = index;
            }

            if (constantIndex >= 0)
            {
                coefficient = (ConstantExpression)factors[constantIndex];
                factors.RemoveAt(constantIndex);
            }

            return factors.Count == 1 ? FactorFamily(factors[0]) : AdditiveTermFamily.Product;
        }

        /// <summary>The family of a single factor, by type tests only.</summary>
        /// <param name="factor">A factor that is not a product.</param>
        /// <returns>Its family.</returns>
        private static AdditiveTermFamily FactorFamily(Expression factor) => factor switch
        {
            ParameterExpression => AdditiveTermFamily.Parameter,
            MethodCallExpression => AdditiveTermFamily.Function,
            BinaryExpression { NodeType: ExpressionType.Power } power when IsOrdinaryBinaryArithmetic(power) => AdditiveTermFamily.Power,
            _ => AdditiveTermFamily.Opaque,
        };

        /// <summary>Whether <paramref name="constant"/> may be a numeric coefficient of a term of <paramref name="type"/>: a non-null boxed primitive of exactly that type.</summary>
        /// <param name="constant">A constant term or factor.</param>
        /// <param name="type">The type of the chain.</param>
        /// <returns><see langword="true"/> for a coefficient.</returns>
        private static bool IsNumericCoefficient(ConstantExpression constant, Type type)
            => constant.Value is { } value
                && ReferenceEquals(constant.Type, type)
                && ReferenceEquals(value.GetType(), type)
                && Types.Number.Contains(type);

        /// <summary>
        /// Classifies the whole term for the additive grouping order as the canonicalization does
        /// (<see cref="ClassifyForAdditiveGrouping"/>), except that the method of a call that is not a runtime reflection
        /// object is never read (S5 A-10): such a call is function-like with the last category.
        /// </summary>
        /// <param name="term">A collected additive term.</param>
        /// <returns>Whether it is function-like, and its category order.</returns>
        private static (bool IsFunctionLike, int CategoryOrder) Classify(Expression term)
        {
            MethodCallExpression? call = term switch
            {
                MethodCallExpression direct => direct,
                BinaryExpression { NodeType: ExpressionType.Power, Left: MethodCallExpression powered } => powered,
                _ => null,
            };

            if (call is null) return (false, 0);
            if (!ExpressionReflectionProvenance.IsRuntime(call.Method)) return (true, GetFunctionCategoryOrder(string.Empty));
            AdditiveGroupClass group = ClassifyForAdditiveGrouping(term);
            return (group.IsFunctionLike, group.CategoryOrder);
        }

        /// <summary>Combines raw factor hashes independently of their order (a sum of mixed values, plus the count).</summary>
        /// <param name="hashes">The hashes of the factors of one term.</param>
        /// <returns>The combined hash; the same for any permutation of <paramref name="hashes"/>.</returns>
        private static int CombineFactorHashes(ReadOnlySpan<int> hashes)
        {
            uint sum = (uint)hashes.Length * 0x9E3779B9u;
            foreach (int hash in hashes)
            {
                // A mix per factor before the commutative sum, so that two factors swapping bits do not cancel out.
                uint mixed = (uint)hash * 0x85EBCA6Bu;
                sum += mixed ^ (mixed >> 13);
            }

            return (int)sum;
        }
    }
}
