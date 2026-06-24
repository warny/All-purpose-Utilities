using System.Collections.ObjectModel;

namespace Utils.Parser.Runtime;

/// <summary>
/// Captures rollback-safe return values for one active parser rule invocation frame.
/// </summary>
public sealed class ParserRuleReturnSnapshot : IParserExecutionStateHashable
{
    /// <summary>
    /// Shared empty returns dictionary used by snapshots without return values.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, object?> EmptyReturnsValue =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(StringComparer.Ordinal));

    /// <summary>
    /// Initializes a new active-rule return snapshot.
    /// </summary>
    /// <param name="ruleName">Name of the parser rule whose frame was captured.</param>
    /// <param name="inputPosition">Token-stream position at the time of rule entry.</param>
    /// <param name="depth">Call-stack depth of the captured frame.</param>
    /// <param name="returns">Return values present on the captured frame.</param>
    public ParserRuleReturnSnapshot(string ruleName, int inputPosition, int depth, IReadOnlyDictionary<string, object?> returns)
    {
        RuleName = ruleName ?? throw new ArgumentNullException(nameof(ruleName));
        ArgumentNullException.ThrowIfNull(returns);
        InputPosition = inputPosition;
        Depth = depth;
        Returns = returns.Count == 0
            ? EmptyReturnsValue
            : new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(returns, StringComparer.Ordinal));
    }

    /// <summary>Gets the parser rule name for the captured invocation frame.</summary>
    public string RuleName { get; }

    /// <summary>Gets the token-stream position at the time of captured rule entry.</summary>
    public int InputPosition { get; }

    /// <summary>Gets the zero-based call-stack depth of the captured frame.</summary>
    public int Depth { get; }

    /// <summary>Gets the immutable return values captured from the frame.</summary>
    public IReadOnlyDictionary<string, object?> Returns { get; }

    /// <summary>
    /// Returns whether this snapshot belongs to <paramref name="frame"/>.
    /// </summary>
    /// <param name="frame">Frame to compare against this snapshot identity.</param>
    /// <returns><c>true</c> when rule name, input position, and depth match.</returns>
    public bool Matches(ParserRuleInvocationFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return string.Equals(RuleName, frame.RuleName, StringComparison.Ordinal)
            && InputPosition == frame.InputPosition
            && Depth == frame.Depth;
    }

    /// <summary>
    /// Computes a managed-state hash using the completed-call result hashing policy,
    /// including volatile markers for arbitrary unsupported return objects.
    /// </summary>
    /// <returns>The parser execution-state hash.</returns>
    public ulong GetParserExecutionStateHash()
    {
        return new ParserRuleCallResult
        {
            RuleName = RuleName,
            InputPosition = InputPosition,
            Depth = Depth,
            Returns = Returns,
        }.GetParserExecutionStateHash();
    }
}
