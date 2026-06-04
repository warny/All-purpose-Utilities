namespace Utils.Parser.Runtime;

/// <summary>
/// No-op parser execution-state manager used by conservative runtime policies.
/// </summary>
public sealed class NullParserExecutionStateManager : IParserExecutionStateManager
{
    /// <summary>
    /// Gets the singleton no-op parser execution-state manager instance.
    /// </summary>
    public static NullParserExecutionStateManager Instance { get; } = new();

    /// <summary>
    /// Initializes the singleton no-op parser execution-state manager.
    /// </summary>
    private NullParserExecutionStateManager()
    {
    }

    /// <summary>
    /// Captures a non-null no-op snapshot without recording parser execution state.
    /// </summary>
    /// <returns>The singleton no-op manager instance.</returns>
    public object Capture()
    {
        return this;
    }

    /// <summary>
    /// Validates that a snapshot was supplied without restoring any parser execution state.
    /// </summary>
    /// <param name="snapshot">Snapshot object to validate.</param>
    public void Restore(object snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
    }

    /// <summary>
    /// Gets the stable stateless memoization key for the no-op manager.
    /// </summary>
    /// <returns><see cref="ParserExecutionStateKey.Stateless"/>.</returns>
    public ParserExecutionStateKey GetCurrentStateKey()
    {
        return ParserExecutionStateKey.Stateless;
    }
}
