using System;

namespace Utils.Parser.Runtime;

/// <summary>
/// Stack-aware parser rule invocation-frame manager that tracks the active call chain.
/// Each <see cref="Enter"/> call creates a child frame whose <see cref="ParserRuleInvocationFrame.Parent"/>
/// is the previously current frame, and each matching <see cref="Exit"/> call restores the parent as current.
/// On successful child exit, a <see cref="ParserRuleCallResult"/> snapshot is captured from the child frame
/// and stored on the parent frame's <see cref="ParserRuleInvocationFrame.LastCompletedChildCall"/>; an optional
/// callback is also invoked so the managed execution-state mechanism can include the call result in rollback snapshots.
/// Returns and parameters remain untyped metadata and are not assigned automatically; labeled completed results
/// are retained only in the parent frame's explicit managed store.
/// </summary>
public sealed class StackParserRuleInvocationFrameManager : IParserRuleInvocationFrameManager
{
    /// <summary>
    /// Optional callback invoked when a child rule exits successfully and a call result is captured.
    /// Used by the managed execution-state mechanism to include the result in rollback snapshots.
    /// </summary>
    private readonly Action<ParserRuleCallResult?>? _onChildCallResult;

    /// <summary>Optional callback that mirrors the current frame's immutable labeled-result store into managed state.</summary>
    private readonly Action<ParserLabeledRuleCallResultStore>? _onLabeledCallResults;

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
    /// <param name="onLabeledCallResults">Optional callback that mirrors immutable labeled-result snapshots into managed execution state.</param>
    public StackParserRuleInvocationFrameManager(
        Action<ParserRuleCallResult?>? onChildCallResult = null,
        Action<ParserLabeledRuleCallResultStore>? onLabeledCallResults = null)
    {
        _onChildCallResult = onChildCallResult;
        _onLabeledCallResults = onLabeledCallResults;
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
    /// Attempts to atomically merge pending child-rule parameter seeds on the current frame.
    /// </summary>
    /// <param name="ruleName">Name of the child rule that will receive the seeds when next entered.</param>
    /// <param name="values">Parameter metadata names and untyped values to seed.</param>
    /// <returns><c>true</c> when an active frame retained every seed; otherwise, <c>false</c>.</returns>
    public bool TrySetPendingChildParameters(
        string ruleName,
        IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(ruleName);
        ArgumentNullException.ThrowIfNull(values);
        if (_current is null)
        {
            return false;
        }

        _current.SetPendingChildParameters(ruleName, values);
        return true;
    }

    /// <summary>
    /// Sets a pending child-rule parameter seed on the current frame when one is active.
    /// This compatibility helper is used by generated explicit-seeding APIs; callers that need
    /// availability reporting should use <see cref="TrySetPendingChildParameters"/>.
    /// </summary>
    /// <param name="ruleName">Name of the child rule that will receive the seed when next entered.</param>
    /// <param name="parameterName">Parameter metadata name as declared in the child rule.</param>
    /// <param name="value">Untyped value to seed.</param>
    public void SetPendingChildParameter(string ruleName, string parameterName, object? value)
    {
        _current?.SetPendingChildParameter(ruleName, parameterName, value);
    }

    /// <summary>
    /// Clears pending child-rule parameter seeds from the current frame.
    /// Delegates to <see cref="ParserRuleInvocationFrame.ClearPendingChildParameters"/> on the current frame.
    /// No-op when no frame is active.
    /// </summary>
    /// <param name="ruleName">
    /// Name of the child rule whose seeds to clear, or <c>null</c> to clear all pending seeds.
    /// </param>
    public void ClearPendingChildParameters(string? ruleName = null)
    {
        _current?.ClearPendingChildParameters(ruleName);
    }

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
        if (frame.Parent is null)
        {
            return;
        }

        if (succeeded)
        {
            var result = ParserRuleCallResult.FromFrame(frame);
            frame.Parent.LastCompletedChildCall = result;
            _onChildCallResult?.Invoke(result);
        }

        // The generated managed-state mirror must represent the parent that will become current after exit,
        // not labeled calls internal to the child frame being completed. This also clears failed-child state.
        _onLabeledCallResults?.Invoke(frame.Parent.LabeledCallResults);
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

    /// <summary>
    /// Annotates the current frame's <see cref="ParserRuleInvocationFrame.LastCompletedChildCall"/> with
    /// the raw argument text from the current call site, replacing any stale <see cref="ParserRuleCallResult.RawArguments"/>
    /// that may have been restored from a memoized execution-state snapshot.
    /// Invokes the optional callback so the managed execution-state mechanism reflects the updated metadata.
    /// No-op when the current frame has no completed child call.
    /// </summary>
    /// <param name="rawArguments">
    /// Raw call-site argument text from <c>RuleRef.RawArguments</c>, or <c>null</c> when the call site
    /// carries no argument clause. Not evaluated, not bound to child parameters.
    /// </param>
    public void AnnotateLastChildCallRawArguments(string? rawArguments)
    {
        if (_current?.LastCompletedChildCall is not { } existing) return;

        ParserRuleCallResult updated = existing.WithRawArguments(rawArguments);
        _current.LastCompletedChildCall = updated;
        _onChildCallResult?.Invoke(updated);
    }

    /// <summary>
    /// Annotates the current frame's <see cref="ParserRuleInvocationFrame.LastCompletedChildCall"/> with
    /// label metadata from the current call site, replacing any stale label data from a memoized snapshot.
    /// Invokes the optional callback so the managed execution-state mechanism reflects the updated metadata.
    /// No-op when the current frame has no completed child call.
    /// </summary>
    /// <param name="labelName">Label name from the current call site, or <c>null</c>.</param>
    /// <param name="labelKind">Label kind from the current call site.</param>
    public void AnnotateLastChildCallLabel(string? labelName, ParserRuleReferenceLabelKind labelKind)
    {
        if (_current?.LastCompletedChildCall is not { } existing) return;

        ParserRuleCallResult updated = existing.WithLabel(labelName, labelKind);
        _current.LastCompletedChildCall = updated;
        _onChildCallResult?.Invoke(updated);
    }

    /// <summary>
    /// Gets the current frame's immutable labeled-result store, or the shared empty store when no frame is active.
    /// </summary>
    /// <returns>The current labeled-result snapshot.</returns>
    public ParserLabeledRuleCallResultStore GetCurrentLabeledCallResults()
        => _current?.LabeledCallResults ?? ParserLabeledRuleCallResultStore.Empty;

    /// <summary>
    /// Synchronizes a restored labeled-result snapshot to the active frame.
    /// </summary>
    /// <param name="results">Restored immutable labeled-result store.</param>
    public void SyncLabeledCallResultsToCurrentFrame(ParserLabeledRuleCallResultStore? results)
    {
        if (_current is not null)
        {
            _current.LabeledCallResults = results ?? ParserLabeledRuleCallResultStore.Empty;
        }
    }

    /// <summary>
    /// Binds the final completed child call result into the active parent frame's assignment or list label store.
    /// Unlabeled results and calls without an active frame are not retained.
    /// </summary>
    /// <returns><c>true</c> when a labeled result was retained; otherwise, <c>false</c>.</returns>
    public bool TryBindLastCompletedChildCallToCurrentLabel()
    {
        if (_current?.LastCompletedChildCall is not { LabelName: { } labelName } result)
        {
            return false;
        }

        ParserLabeledRuleCallResultStore updated;
        if (result.LabelKind == ParserRuleReferenceLabelKind.Assignment)
        {
            updated = _current.LabeledCallResults.SetAssignment(labelName, result);
        }
        else if (result.LabelKind == ParserRuleReferenceLabelKind.List)
        {
            updated = _current.LabeledCallResults.AppendList(labelName, result);
        }
        else
        {
            return false;
        }

        _current.LabeledCallResults = updated;
        _onLabeledCallResults?.Invoke(updated);
        return true;
    }

}
