namespace Utils.Parser.Runtime;

/// <summary>
/// Records parser runtime observations in arrival order for tooling-oriented exports.
/// </summary>
public sealed class RuntimeObservationRecorder : IParserRuntimeObserver
{
    private readonly List<AlternativeRuntimeObservation> observations = new();

    /// <summary>
    /// Gets a defensive immutable snapshot of recorded observations.
    /// </summary>
    public IReadOnlyList<AlternativeRuntimeObservation> Observations => this.observations.ToArray();

    /// <summary>
    /// Clears all currently recorded observations.
    /// </summary>
    public void Clear()
    {
        this.observations.Clear();
    }

    /// <inheritdoc />
    public void OnAlternativeStarted(AlternativeRuntimeObservation observation)
    {
        this.Record(observation);
    }

    /// <inheritdoc />
    public void OnAlternativeCompleted(AlternativeRuntimeObservation observation)
    {
        this.Record(observation);
    }

    /// <inheritdoc />
    public void OnAlternativeFailed(AlternativeRuntimeObservation observation)
    {
        this.Record(observation);
    }

    /// <inheritdoc />
    public void OnAlternativePruned(AlternativeRuntimeObservation observation)
    {
        this.Record(observation);
    }

    /// <inheritdoc />
    public void OnAlternativeSelected(AlternativeRuntimeObservation observation)
    {
        this.Record(observation);
    }

    /// <summary>
    /// Stores one immutable observation instance in deterministic sequence order.
    /// </summary>
    /// <param name="observation">Observation payload produced by the parser runtime.</param>
    private void Record(AlternativeRuntimeObservation observation)
    {
        this.observations.Add(observation);
    }
}
