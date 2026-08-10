using System.Collections.Immutable;

namespace Utils.Parser.Runtime;

/// <summary>
/// Represents a deterministic, read-only summary of a runtime observation sequence.
/// </summary>
/// <param name="TotalObservations">Total number of observations in the analyzed sequence.</param>
/// <param name="EventDistribution">Count per observation kind.</param>
/// <param name="StatusDistribution">Count per observation status.</param>
/// <param name="RuleDistribution">Count per observed rule name.</param>
/// <param name="AlternativeDistribution">Count per observed alternative index.</param>
public sealed record RuntimeTraceSummary(
    int TotalObservations,
    IReadOnlyDictionary<ParserRuntimeObservationKind, int> EventDistribution,
    IReadOnlyDictionary<ParserRuntimeObservationStatus, int> StatusDistribution,
    IReadOnlyDictionary<string, int> RuleDistribution,
    IReadOnlyDictionary<int, int> AlternativeDistribution)
{
    /// <summary>Event distribution captured as an immutable snapshot.</summary>
    private IReadOnlyDictionary<ParserRuntimeObservationKind, int> _eventDistribution =
        EventDistribution.ToImmutableDictionary();

    /// <summary>Status distribution captured as an immutable snapshot.</summary>
    private IReadOnlyDictionary<ParserRuntimeObservationStatus, int> _statusDistribution =
        StatusDistribution.ToImmutableDictionary();

    /// <summary>Rule distribution captured as an immutable snapshot.</summary>
    private IReadOnlyDictionary<string, int> _ruleDistribution =
        RuleDistribution.ToImmutableDictionary(StringComparer.Ordinal);

    /// <summary>Alternative distribution captured as an immutable snapshot.</summary>
    private IReadOnlyDictionary<int, int> _alternativeDistribution =
        AlternativeDistribution.ToImmutableDictionary();

    /// <summary>Gets the immutable event distribution.</summary>
    public IReadOnlyDictionary<ParserRuntimeObservationKind, int> EventDistribution
    {
        get => _eventDistribution;
        init => _eventDistribution = value.ToImmutableDictionary();
    }

    /// <summary>Gets the immutable status distribution.</summary>
    public IReadOnlyDictionary<ParserRuntimeObservationStatus, int> StatusDistribution
    {
        get => _statusDistribution;
        init => _statusDistribution = value.ToImmutableDictionary();
    }

    /// <summary>Gets the immutable ordinal rule distribution.</summary>
    public IReadOnlyDictionary<string, int> RuleDistribution
    {
        get => _ruleDistribution;
        init => _ruleDistribution = value.ToImmutableDictionary(StringComparer.Ordinal);
    }

    /// <summary>Gets the immutable alternative distribution.</summary>
    public IReadOnlyDictionary<int, int> AlternativeDistribution
    {
        get => _alternativeDistribution;
        init => _alternativeDistribution = value.ToImmutableDictionary();
    }
}
