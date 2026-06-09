using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Utils.Parser.Runtime;

/// <summary>
/// Captures a passive snapshot of return values from a successfully completed child parser rule invocation.
/// The snapshot is immutable: return values are copied at the time of capture and the child frame is not retained.
/// Call results are not propagated automatically to caller frames, do not implement typed returns,
/// and do not support <c>$rule.value</c> or labeled rule-reference access.
/// </summary>
public sealed class ParserRuleCallResult : IParserExecutionStateHashable
{
    /// <summary>
    /// Gets the name of the parser rule that produced this call result.
    /// </summary>
    public required string RuleName { get; init; }

    /// <summary>
    /// Gets the token-stream position at the time of rule entry.
    /// </summary>
    public required int InputPosition { get; init; }

    /// <summary>
    /// Gets the zero-based call-stack depth of the completed rule invocation.
    /// </summary>
    public required int Depth { get; init; }

    /// <summary>
    /// Gets a read-only snapshot of the return values that were present on the child frame at rule completion.
    /// Values are copied from the child frame dictionary; mutating the child frame after capture does not affect this snapshot.
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Returns { get; init; }

    /// <summary>
    /// Gets the passive rule metadata descriptor that was attached to the completed invocation, when available.
    /// </summary>
    public ParserRuleInvocationDescriptor? Descriptor { get; init; }

    /// <summary>
    /// Gets the raw call-site argument text preserved from a <c>callee[...]</c> argument clause at the call site,
    /// or <c>null</c> when the call site carried no argument clause.
    /// The outer brackets are excluded. This text is not evaluated, not bound to child rule parameters,
    /// and does not populate invocation-frame parameters.
    /// Use <c>SetNextRuleParameter(...)</c> for explicit parameter seeding.
    /// </summary>
    public string? RawArguments { get; init; }

    /// <summary>
    /// Gets the label name from the call-site rule reference (<c>x=child</c>, <c>xs+=child</c>),
    /// or <c>null</c> when the call site carried no label.
    /// Metadata only: no implicit variable, typed field, or ANTLR-compatible label access is generated.
    /// </summary>
    public string? LabelName { get; init; }

    /// <summary>
    /// Gets the label kind from the call-site rule reference, or
    /// <see cref="ParserRuleReferenceLabelKind.None"/> when the call site carried no label.
    /// Metadata only.
    /// </summary>
    public ParserRuleReferenceLabelKind LabelKind { get; init; }

    /// <summary>
    /// Computes a deterministic hash reflecting this call result's rule name, depth, and return values,
    /// so it can participate in managed parser execution-state keys stored in generated execution contexts.
    /// </summary>
    public ulong GetParserExecutionStateHash()
    {
        ulong hash = 14695981039346656037UL;
        foreach (char c in RuleName)
        {
            hash = (hash ^ (ulong)c) * 1099511628211UL;
        }

        hash = (hash ^ (ulong)(uint)InputPosition) * 1099511628211UL;
        hash = (hash ^ (ulong)(uint)Depth) * 1099511628211UL;
        hash = (hash ^ (ulong)(uint)Returns.Count) * 1099511628211UL;

        foreach (var kvp in Returns)
        {
            foreach (char c in kvp.Key)
            {
                hash = (hash ^ (ulong)c) * 1099511628211UL;
            }

            if (kvp.Value is not null)
            {
                hash = (hash ^ (ulong)(uint)kvp.Value.GetHashCode()) * 1099511628211UL;
            }
        }

        if (RawArguments is not null)
        {
            foreach (char c in RawArguments)
            {
                hash = (hash ^ (ulong)c) * 1099511628211UL;
            }
        }

        hash = (hash ^ (ulong)(int)LabelKind) * 1099511628211UL;

        if (LabelName is not null)
        {
            foreach (char c in LabelName)
            {
                hash = (hash ^ (ulong)c) * 1099511628211UL;
            }
        }

        return hash;
    }

    /// <summary>
    /// Creates a call result by copying the return values from the supplied completed invocation frame.
    /// </summary>
    /// <param name="frame">The completed child invocation frame whose returns are captured.</param>
    /// <returns>An immutable call result snapshot.</returns>
    internal static ParserRuleCallResult FromFrame(ParserRuleInvocationFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return new ParserRuleCallResult
        {
            RuleName = frame.RuleName,
            InputPosition = frame.InputPosition,
            Depth = frame.Depth,
            Returns = new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>(frame.Returns, StringComparer.Ordinal)),
            Descriptor = frame.Descriptor,
        };
    }
}
