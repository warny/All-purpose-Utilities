using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Represents the state of a single parse branch during alternative exploration.
/// This is a local, descriptive snapshot — not a global parse result or an invocation frame.
/// </summary>
internal sealed record ParseBranch
{
    /// <summary>The grammar rule being parsed in this branch.</summary>
    public required Rule Rule { get; init; }

    /// <summary>The specific alternative of the rule being explored.</summary>
    public required Alternative Alternative { get; init; }

    /// <summary>The input position at which this branch started.</summary>
    public required int InputPosition { get; init; }

    /// <summary>The cursor tracking the current element within the alternative's content.</summary>
    public required RuleContentCursor Cursor { get; init; }

    /// <summary>The partially built parse node accumulated so far.</summary>
    public required ParseNode PartialNode { get; init; }

    /// <summary>The input position reached at the end of this branch.</summary>
    public required int EndPosition { get; init; }

    /// <summary>Whether all required elements of the alternative have been successfully matched.</summary>
    public bool IsComplete { get; init; }
}
