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

    /// <summary>Global: the chain holds a third constant term.</summary>
    TooManyConstants,

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

    /// <summary>The number of chain roots admitted.</summary>
    public long Accepted;

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
}
