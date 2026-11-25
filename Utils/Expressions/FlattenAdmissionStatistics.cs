using System.Collections.Generic;
using System.Linq.Expressions;

namespace Utils.Mathematics.Expressions;

/// <summary>
/// Why the admission of <see cref="ExpressionSimplifier"/>'s flatten rule refused a chain (see
/// <c>docs/expressions/SimplifierFlatten.md</c> §9). The first five are <em>local</em>: they belong to one crossed node or one
/// term and refuse every chain that crosses that node. The next four are <em>global</em>: they depend on the set of terms,
/// and refuse every chain that holds the terms involved.
/// </summary>
internal enum FlattenRefusalCause
{
    /// <summary>No refusal: the chain is linear.</summary>
    None = 0,

    /// <summary>Local: the type of the chain is not one of the flattened types.</summary>
    ChainType,

    /// <summary>Local: a crossed node is not an ordinary operator.</summary>
    Operator,

    /// <summary>Local: a term that is neither linear nor a call (a negation, a power, a division, a conversion, …).</summary>
    NonLinearTerm,

    /// <summary>Local: a call or an invocation term, the potential effect boundaries of a chain.</summary>
    CallTerm,

    /// <summary>Local: a crossed node with two constant operands.</summary>
    ConstantPair,

    /// <summary>Local: a subtraction whose right operand is crossed.</summary>
    SubtractedGroup,

    /// <summary>Global: a parameter occurs twice, as a term or as a factor.</summary>
    RepeatedParameter,

    /// <summary>Global: two constant coefficients have the same value.</summary>
    RepeatedCoefficient,

    /// <summary>Global: a constant coefficient equals a constant term.</summary>
    CoefficientEqualsConstant,

    /// <summary>Global: the chain holds a third constant term (a second one for a grouped quotient chain, §16).</summary>
    TooManyConstants,

    /// <summary>
    /// Global, grouped quotient chains only (docs/expressions/SimplifierFlatten.md §16): the chain holds no linear quotient. Every
    /// sub-chain holds none either.
    /// </summary>
    NoQuotient,

    /// <summary>
    /// An anchored chain (Flatten mission C2), refused by an instance of an explicit policy, whose audited rules may run
    /// outside the effect gate.
    /// </summary>
    AnchoredUnderPolicy,

    /// <summary>The root was recorded as refused by the walk of an enclosing chain of the same operation.</summary>
    KnownRefusal,
}

/// <summary>
/// Opt-in counters of the admission of <see cref="ExpressionSimplifier"/>'s flatten rule, for audits and benchmarks only
/// (<see cref="ExpressionSimplifier.AdmissionStatistics"/>, one instance per thread). Data only: the admission updates the
/// fields when an instance is installed, and does nothing else when none is.
/// </summary>
internal sealed class FlattenAdmissionStatistics
{
    /// <summary>The number of chain roots offered to the admission of the built-in type.</summary>
    public long Admissions;

    /// <summary>The number of linear chain roots admitted.</summary>
    public long Accepted;

    /// <summary>The number of anchored chain roots admitted (Flatten mission C2).</summary>
    public long Anchored;

    /// <summary>The number of anchored chains simplified by one canonicalization.</summary>
    public long AnchoredCanonicalizations;

    /// <summary>The number of anchored chains whose prepared anchor sent them to the level by level fallback.</summary>
    public long AnchoredFallbacks;

    /// <summary>The number of spine chain roots admitted (mission C3b).</summary>
    public long Spine;

    /// <summary>The number of walks of the spine test, each after a linear refusal for a repetition (mission C3b).</summary>
    public long SpineWalks;

    /// <summary>The number of spine tests answered by the table of known spine refusals, without any walk.</summary>
    public long KnownSpineRefusals;

    /// <summary>The number of levels of admitted spine chains finalized one by one above their linear sub-chain.</summary>
    public long SpineLevels;

    /// <summary>The number of spine refusals per cause, indexed by <see cref="FlattenSpineRefusalCause"/>.</summary>
    public long[] SpineCauses = new long[(int)FlattenSpineRefusalCause.TooManyConstants + 1];

    /// <summary>The number of factorable chain roots admitted (mission C3c).</summary>
    public long Factorable;

    /// <summary>The number of walks of the factorable test, each after a linear refusal (mission C3c).</summary>
    public long FactorableWalks;

    /// <summary>The number of factorable chains simplified by the global factoring.</summary>
    public long FactorableChains;

    /// <summary>
    /// The number of results of refused chain boundaries tested as factorable chains (mission C3c, consistency of the level and
    /// whole-chain factoring: <c>ExpressionSimplifier.FinishRefusedChain</c>).
    /// </summary>
    public long BoundaryTests;

    /// <summary>The number of those results that were factorable chains, canonicalized by the global factoring.</summary>
    public long BoundaryFactorings;

    /// <summary>
    /// The number of refused boundary results whose right spine was searched for a factorable suffix (mission M4,
    /// <c>ExpressionSimplifier.FinishRefusedSuffix</c>): one per result that is neither linear nor factorable as a whole.
    /// </summary>
    public long SuffixSearches;

    /// <summary>The number of candidate suffixes tested as linear, then factorable, chains (at most one per search and round).</summary>
    public long SuffixTests;

    /// <summary>The number of factorable suffixes whose global factoring changed their terms, and whose prefix levels were run again.</summary>
    public long SuffixFactorings;

    /// <summary>The number of spine nodes of a prefix run again on top of a factored suffix.</summary>
    public long SuffixPrefixLevels;

    /// <summary>The number of grouped quotient chain roots admitted (docs/expressions/SimplifierFlatten.md §16).</summary>
    public long GroupedQuotient;

    /// <summary>The number of walks of the grouped-quotient test, each after a linear refusal for a subtracted group.</summary>
    public long GroupedWalks;

    /// <summary>The number of grouped-quotient tests answered by the table of known grouped refusals, without any walk.</summary>
    public long KnownGroupedRefusals;

    /// <summary>The number of grouped-quotient refusals per cause, indexed by <see cref="FlattenRefusalCause"/>.</summary>
    public long[] GroupedCauses = new long[(int)FlattenRefusalCause.KnownRefusal + 1];

    /// <summary>The number of grouped quotient chains simplified by one canonicalization.</summary>
    public long GroupedChains;

    /// <summary>The number of grouped quotient chains whose prepared terms sent them to the level by level fallback.</summary>
    public long GroupedFallbacks;

    /// <summary>The number of groups of identical monomials merged by the additive canonicalization (mission C3c, phase A).</summary>
    public long MonomialGroups;

    /// <summary>The number of terms those groups held.</summary>
    public long MergedTerms;

    /// <summary>The number of rounds of the common-factor search (mission C3c, phase B).</summary>
    public long FactoringRounds;

    /// <summary>The number of common factors (or coefficients) factored out.</summary>
    public long CommonFactorings;

    /// <summary>The number of factors indexed or compared by the common-factor search.</summary>
    public long FactorComparisons;

    /// <summary>The number of walks of a chain (admissions not answered by the table of known refusals).</summary>
    public long Walks;

    /// <summary>The number of crossed nodes visited by the walks.</summary>
    public long NodesExamined;

    /// <summary>The number of crossed nodes visited to record proven refusals.</summary>
    public long RecordingVisits;

    /// <summary>The number of crossed nodes recorded as proven refusals.</summary>
    public long RecordedRefusals;

    /// <summary>The number of admissions answered by the table of known refusals, without any walk.</summary>
    public long KnownRefusals;

    /// <summary>The number of refusals per cause, indexed by <see cref="FlattenRefusalCause"/>.</summary>
    public long[] Causes = new long[(int)FlattenRefusalCause.KnownRefusal + 1];

    /// <summary>
    /// When not <see langword="null"/>, every crossed node a walk visits is added to it, so that
    /// <see cref="NodesExamined"/> minus its count is the number of repeated examinations.
    /// </summary>
    public HashSet<Expression>? DistinctNodes;

    /// <summary>
    /// Audits only: when <see langword="true"/>, every anchored chain takes the level by level fallback of the flatten rule,
    /// whatever its prepared anchor, so that the fallback can be compared with the historical treatment on any chain.
    /// </summary>
    public bool ForceAnchoredFallback;

    /// <summary>
    /// Audits only: when <see langword="true"/>, every grouped quotient chain takes the level by level fallback of the flatten
    /// rule, whatever its prepared terms, so that the fallback can be compared with the historical treatment on any chain.
    /// </summary>
    public bool ForceGroupedFallback;
}
