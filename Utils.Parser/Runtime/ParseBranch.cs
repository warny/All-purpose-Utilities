using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

internal sealed record ParseBranch
{
    public required Rule Rule { get; init; }

    public required Alternative Alternative { get; init; }

    public required int InputPosition { get; init; }

    public required RuleContentCursor Cursor { get; init; }

    public required ParseNode PartialNode { get; init; }

    public required int EndPosition { get; init; }

    public bool IsComplete { get; init; }
}
