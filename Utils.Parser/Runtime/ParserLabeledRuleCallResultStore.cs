using System.Collections.ObjectModel;

namespace Utils.Parser.Runtime;

/// <summary>
/// Stores immutable snapshots of successful labeled child parser-rule call results for one invocation frame.
/// Assignment labels and list labels use separate namespaces because grammar ingestion currently permits the
/// same lexical label with both operators; callers must use the helper matching the call-site label kind.
/// </summary>
public sealed class ParserLabeledRuleCallResultStore : ICloneable, IParserExecutionStateHashable
{
    private static readonly IReadOnlyList<ParserRuleCallResult> EmptyList = Array.Empty<ParserRuleCallResult>();
    private readonly IReadOnlyDictionary<string, ParserRuleCallResult> _assignments;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ParserRuleCallResult>> _lists;

    /// <summary>
    /// Gets the shared empty labeled-result store.
    /// </summary>
    public static ParserLabeledRuleCallResultStore Empty { get; } = new(
        new Dictionary<string, ParserRuleCallResult>(StringComparer.Ordinal),
        new Dictionary<string, IReadOnlyList<ParserRuleCallResult>>(StringComparer.Ordinal));

    /// <summary>
    /// Initializes a labeled-result store from immutable assignment and list snapshots.
    /// </summary>
    /// <param name="assignments">Assignment-label results.</param>
    /// <param name="lists">List-label result collections.</param>
    private ParserLabeledRuleCallResultStore(
        IReadOnlyDictionary<string, ParserRuleCallResult> assignments,
        IReadOnlyDictionary<string, IReadOnlyList<ParserRuleCallResult>> lists)
    {
        _assignments = assignments;
        _lists = lists;
    }

    /// <summary>
    /// Attempts to retrieve the last successful result retained for an assignment label.
    /// </summary>
    /// <param name="labelName">Assignment label name.</param>
    /// <param name="result">Receives the retained result when present.</param>
    /// <returns><c>true</c> when an assignment result is present; otherwise, <c>false</c>.</returns>
    public bool TryGetAssignment(string labelName, out ParserRuleCallResult result)
    {
        ArgumentNullException.ThrowIfNull(labelName);
        return _assignments.TryGetValue(labelName, out result!);
    }

    /// <summary>
    /// Gets the successful results retained for a list label in execution order.
    /// </summary>
    /// <param name="labelName">List label name.</param>
    /// <returns>An immutable ordered result list, or a shared empty list when absent.</returns>
    public IReadOnlyList<ParserRuleCallResult> GetList(string labelName)
    {
        ArgumentNullException.ThrowIfNull(labelName);
        return _lists.TryGetValue(labelName, out IReadOnlyList<ParserRuleCallResult>? results)
            ? results
            : EmptyList;
    }

    /// <summary>
    /// Returns a new store whose assignment label contains the supplied successful result.
    /// A repeated assignment label uses last-successful-result-wins semantics.
    /// </summary>
    /// <param name="labelName">Assignment label name.</param>
    /// <param name="result">Successful completed call result.</param>
    /// <returns>A new immutable store snapshot.</returns>
    public ParserLabeledRuleCallResultStore SetAssignment(string labelName, ParserRuleCallResult result)
    {
        ArgumentNullException.ThrowIfNull(labelName);
        ArgumentNullException.ThrowIfNull(result);

        var assignments = new Dictionary<string, ParserRuleCallResult>(_assignments, StringComparer.Ordinal)
        {
            [labelName] = result,
        };
        return new ParserLabeledRuleCallResultStore(
            new ReadOnlyDictionary<string, ParserRuleCallResult>(assignments),
            _lists);
    }

    /// <summary>
    /// Returns a new store with the supplied successful result appended to a list label.
    /// </summary>
    /// <param name="labelName">List label name.</param>
    /// <param name="result">Successful completed call result.</param>
    /// <returns>A new immutable store snapshot.</returns>
    public ParserLabeledRuleCallResultStore AppendList(string labelName, ParserRuleCallResult result)
    {
        ArgumentNullException.ThrowIfNull(labelName);
        ArgumentNullException.ThrowIfNull(result);

        var lists = new Dictionary<string, IReadOnlyList<ParserRuleCallResult>>(_lists, StringComparer.Ordinal);
        IReadOnlyList<ParserRuleCallResult> existing = GetList(labelName);
        var appended = new ParserRuleCallResult[existing.Count + 1];
        for (int index = 0; index < existing.Count; index++)
        {
            appended[index] = existing[index];
        }
        appended[^1] = result;
        lists[labelName] = Array.AsReadOnly(appended);

        return new ParserLabeledRuleCallResultStore(
            _assignments,
            new ReadOnlyDictionary<string, IReadOnlyList<ParserRuleCallResult>>(lists));
    }

    /// <summary>
    /// Returns this immutable store instance.
    /// </summary>
    /// <returns>The current immutable store.</returns>
    public object Clone() => this;

    /// <summary>
    /// Computes a deterministic state hash for assignment labels, list labels, and ordered call results.
    /// Call results conservatively produce volatile hashes when they contain unsupported arbitrary return objects.
    /// </summary>
    /// <returns>The managed execution-state hash.</returns>
    public ulong GetParserExecutionStateHash()
    {
        ulong hash = 14695981039346656037UL;
        foreach (KeyValuePair<string, ParserRuleCallResult> assignment in _assignments.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            AddText(ref hash, assignment.Key);
            AddUInt64(ref hash, assignment.Value.GetParserExecutionStateHash());
        }

        AddUInt64(ref hash, (ulong)_assignments.Count);
        foreach (KeyValuePair<string, IReadOnlyList<ParserRuleCallResult>> list in _lists.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            AddText(ref hash, list.Key);
            AddUInt64(ref hash, (ulong)list.Value.Count);
            foreach (ParserRuleCallResult result in list.Value)
            {
                AddUInt64(ref hash, result.GetParserExecutionStateHash());
            }
        }

        AddUInt64(ref hash, (ulong)_lists.Count);
        return hash;
    }

    /// <summary>
    /// Adds text to the deterministic hash stream.
    /// </summary>
    /// <param name="hash">Current FNV-1a hash.</param>
    /// <param name="text">Text to add.</param>
    private static void AddText(ref ulong hash, string text)
    {
        AddUInt64(ref hash, (ulong)text.Length);
        foreach (char character in text)
        {
            AddUInt64(ref hash, character);
        }
    }

    /// <summary>
    /// Adds an unsigned integer to the deterministic hash stream.
    /// </summary>
    /// <param name="hash">Current FNV-1a hash.</param>
    /// <param name="value">Value to add.</param>
    private static void AddUInt64(ref ulong hash, ulong value)
    {
        for (int shift = 0; shift < 64; shift += 8)
        {
            hash = (hash ^ (byte)(value >> shift)) * 1099511628211UL;
        }
    }
}
