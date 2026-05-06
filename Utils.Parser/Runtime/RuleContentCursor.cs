namespace Utils.Parser.Runtime;

internal sealed record RuleContentCursor
{
    public required int Index { get; init; }

    public required string Kind { get; init; }
}
