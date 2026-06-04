namespace Utils.Parser.Runtime;

/// <summary>
/// Captures and restores opaque parser execution state for future transactional parser attempts.
/// </summary>
public interface IParserExecutionStateManager
{
    /// <summary>
    /// Captures an opaque snapshot of the current parser execution state.
    /// </summary>
    /// <returns>An opaque snapshot object that can be supplied to <see cref="Restore"/>.</returns>
    object Capture();

    /// <summary>
    /// Restores parser execution state from a previously captured snapshot.
    /// </summary>
    /// <param name="snapshot">Opaque snapshot previously returned by <see cref="Capture"/>.</param>
    void Restore(object snapshot);

    /// <summary>
    /// Gets the opaque semantic state key used to isolate rule-result memoization entries.
    /// </summary>
    /// <returns>The current parser execution-state key.</returns>
    ParserExecutionStateKey GetCurrentStateKey();
}
