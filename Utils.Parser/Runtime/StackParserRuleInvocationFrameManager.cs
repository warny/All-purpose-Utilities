namespace Utils.Parser.Runtime;

/// <summary>
/// Stack-aware parser rule invocation-frame manager that tracks the active call chain.
/// Each <see cref="Enter"/> call creates a child frame whose <see cref="ParserRuleInvocationFrame.Parent"/>
/// is the previously current frame, and each matching <see cref="Exit"/> call restores the parent as current.
/// This manager is preparatory infrastructure for future rule return and argument support.
/// Returns and parameters remain metadata-only and are not propagated or executed.
/// </summary>
public sealed class StackParserRuleInvocationFrameManager : IParserRuleInvocationFrameManager
{
    /// <summary>
    /// Top frame of the active call stack, or <c>null</c> when no rule is being parsed.
    /// </summary>
    private ParserRuleInvocationFrame? _current;

    /// <summary>
    /// Gets the current invocation frame at the top of the active call stack, or <c>null</c> when outside any rule.
    /// </summary>
    public ParserRuleInvocationFrame? Current => _current;

    /// <summary>
    /// Creates a new invocation frame whose parent is the current frame, pushes it onto the call stack, and returns it.
    /// The new frame's <see cref="ParserRuleInvocationFrame.Depth"/> is one greater than the parent's depth,
    /// or 0 when there is no current frame.
    /// </summary>
    /// <param name="ruleName">Name of the parser rule being entered.</param>
    /// <param name="inputPosition">Token-stream position at the time of rule entry.</param>
    /// <param name="descriptor">Passive rule metadata descriptor for the invocation, when available.</param>
    /// <returns>The new invocation frame, which is now <see cref="Current"/>.</returns>
    public ParserRuleInvocationFrame Enter(string ruleName, int inputPosition, ParserRuleInvocationDescriptor? descriptor = null)
    {
        var frame = new ParserRuleInvocationFrame(ruleName, inputPosition, new Dictionary<string, object?>(), descriptor, _current);
        _current = frame;
        return frame;
    }

    /// <summary>
    /// Pops <paramref name="frame"/> from the call stack and restores its parent as current.
    /// </summary>
    /// <param name="frame">Invocation frame returned by the matching <see cref="Enter"/> call.</param>
    /// <param name="succeeded">Whether the parser rule produced a parse node before leaving.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="frame"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="frame"/> is not the current top-of-stack frame.
    /// Mismatched exits indicate a bug in the call-stack maintenance logic.
    /// </exception>
    public void Exit(ParserRuleInvocationFrame frame, bool succeeded)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!ReferenceEquals(frame, _current))
        {
            throw new InvalidOperationException(
                $"Mismatched parser rule invocation frame exit: expected to exit frame for rule " +
                $"'{_current?.RuleName ?? "<none>"}' at depth {_current?.Depth.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<none>"}, " +
                $"but received frame for rule '{frame.RuleName}' at depth {frame.Depth.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");
        }

        _current = frame.Parent;
    }
}
