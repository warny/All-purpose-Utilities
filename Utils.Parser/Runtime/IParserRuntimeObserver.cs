using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Defines a passive runtime observation contract for parser scheduling events.
/// Implementations must remain non-authoritative and must not attempt to influence parser behavior.
/// Observer callback exceptions are isolated by the runtime scheduler and do not alter execution semantics.
/// </summary>
public interface IParserRuntimeObserver
{
    /// <summary>
    /// Observes that an alternative started local scheduled execution.
    /// </summary>
    /// <param name="observation">Immutable observation payload.</param>
    void OnAlternativeStarted(AlternativeRuntimeObservation observation);

    /// <summary>
    /// Observes that an alternative completed local scheduled execution.
    /// </summary>
    /// <param name="observation">Immutable observation payload.</param>
    void OnAlternativeCompleted(AlternativeRuntimeObservation observation);

    /// <summary>
    /// Observes that an alternative failed local scheduled execution.
    /// </summary>
    /// <param name="observation">Immutable observation payload.</param>
    void OnAlternativeFailed(AlternativeRuntimeObservation observation);

    /// <summary>
    /// Observes that an alternative was pruned by scheduler-level equivalence.
    /// </summary>
    /// <param name="observation">Immutable observation payload.</param>
    void OnAlternativePruned(AlternativeRuntimeObservation observation);

    /// <summary>
    /// Observes the selected local winner state for a scheduling pass.
    /// </summary>
    /// <param name="observation">Immutable observation payload.</param>
    void OnAlternativeSelected(AlternativeRuntimeObservation observation);
}
