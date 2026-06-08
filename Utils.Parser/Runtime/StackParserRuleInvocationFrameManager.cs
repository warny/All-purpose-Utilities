using System;

namespace Utils.Parser.Runtime;

/// <summary>
/// Stack-aware parser rule invocation-frame manager that tracks the active call chain.
/// Each <see cref="Enter"/> call creates a child frame whose <see cref="ParserRuleInvocationFrame.Parent"/>
/// is the previously current frame, and each matching <see cref="Exit"/> call restores the parent as current.
/// On successful child exit, a <see cref="ParserRuleCallResult"/> snapshot is captured from the child frame
/// and stored on the parent frame's <see cref="ParserRuleInvocationFrame.LastCompletedChildCall"/>; an optional
/// callback is also invoked so the managed execution-state mechanism can include the call result in rollback snapshots.
/// Returns and parameters remain metadata-only and are not propagated or executed automatically.
/// </summary>
public sealed class StackParserRuleInvocationFrameManager : IParserRuleInvocationFrameManager
{
    /// <summary>
    /// Optional callback invoked when a child rule exits successfully and a call result is captured.
    /// Used by the managed execution-state mechanism to include the result in rollback snapshots.
    /// </summary>
    private readonly Action<ParserRuleCallResult?>? _onChildCallResult;

    /// <summary>
    /// Top frame of the active call stack, or <c>null</c> when no rule is being parsed.
    /// </summary>
    private ParserRuleInvocationFrame? _current;

    /// <summary>
    /// Initializes a new stack-aware invocation-frame manager.
    /// </summary>
    /// <param name="onChildCallResult">
    /// Optional callback invoked after a child rule exits successfully.
    /// Receives the captured call result so the managed execution-state mechanism can snapshot it
    /// for rollback-safe call-result propagation. When <c>null</c>, call results are stored on the
    /// parent frame but are not included in managed execution-state snapshots.
    /// </param>
    public StackParserRuleInvocationFrameManager(Action<ParserRuleCallResult?>? onChildCallResult = null)
    {
        _onChildCallResult = onChildCallResult;
    }

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
        IReadOnlyDictionary<string, object?> parameters;
        if (_current is not null)
        {
            _current.TryConsumePendingChildParameters(ruleName, out parameters);
        }
        else
        {
            parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        var frame = new ParserRuleInvocationFrame(ruleName, inputPosition, parameters, descriptor, _current);
        _current = frame;
        return frame;
    }

    /// <summary>
    /// Gets the pending child-parameter seed store from the current frame, or <c>null</c> when no frame is active or no seeds are pending.
    /// Used by the managed execution-state mechanism to capture seed state in backtracking snapshots.
    /// </summary>
    public ParserRuleParameterSeedStore? GetCurrentPendingSeeds() => _current?.PendingChildSeeds;

    /// <summary>
    /// Syncs <paramref name="seeds"/> to the current frame's <see cref="ParserRuleInvocationFrame.PendingChildSeeds"/>.
    /// Called by the managed execution-state restore mechanism after restoring a snapshot, so stale seeds do not
    /// leak across failed parser alternatives.
    /// </summary>
    public void SyncPendingSeedsToCurrentFrame(ParserRuleParameterSeedStore? seeds)
    {
        if (_current is not null)
        {
            _current.PendingChildSeeds = seeds;
        }
    }

    /// <summary>
    /// Pops <paramref name="frame"/> from the call stack and restores its parent as current.
    /// On successful exit when a parent frame exists, captures the child frame's return values into a
    /// <see cref="ParserRuleCallResult"/> and stores it on the parent frame's
    /// <see cref="ParserRuleInvocationFrame.LastCompletedChildCall"/>; the optional callback is also invoked
    /// so the managed execution-state mechanism can include the result in rollback snapshots.
    /// On failed exit, the parent frame's last call result is not updated.
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

    /// <summary>
    /// Called by <c>ParserEngine</c> just before the post-rule execution-state snapshot is captured.
    /// On successful exit with a parent frame, captures the child frame's current return values into a
    /// <see cref="ParserRuleCallResult"/> and stores it on the parent frame's
    /// <see cref="ParserRuleInvocationFrame.LastCompletedChildCall"/>; the optional callback is also invoked
    /// so the managed execution-state snapshot includes the result, enabling rollback-safe memoization hits.
    /// On failed exit or root-level exit (no parent), no call result is captured.
    /// </summary>
    /// <param name="frame">The current invocation frame about to be snapshotted.</param>
    /// <param name="succeeded">Whether the parser rule produced a parse node.</param>
    public void PrepareCallResultForSnapshot(ParserRuleInvocationFrame frame, bool succeeded)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (succeeded && frame.Parent is not null)
        {
            var result = ParserRuleCallResult.FromFrame(frame);
            frame.Parent.LastCompletedChildCall = result;
            _onChildCallResult?.Invoke(result);
        }
    }

    /// <summary>
    /// Syncs <paramref name="result"/> to the current frame's <see cref="ParserRuleInvocationFrame.LastCompletedChildCall"/>.
    /// Called by the managed execution-state restore mechanism after restoring a snapshot, to ensure
    /// frame state reflects the restored execution context and stale call results do not leak across
    /// failed parser alternatives.
    /// </summary>
    /// <param name="result">The restored call result, or <c>null</c> when the snapshot predates any child call.</param>
    public void SyncCallResultToCurrentFrame(ParserRuleCallResult? result)
    {
        if (_current is not null)
        {
            _current.LastCompletedChildCall = result;
        }
    }
}
