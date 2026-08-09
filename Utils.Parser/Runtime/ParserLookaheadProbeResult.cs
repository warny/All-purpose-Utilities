using System.Collections.Immutable;

namespace Utils.Parser.Runtime;

/// <summary>Stores an immutable lightweight structured look-ahead probe observation.</summary>
internal readonly record struct ParserLookaheadProbeResult
{
    /// <summary>Initializes a probe result and captures its optional expected-token snapshot.</summary>
    public ParserLookaheadProbeResult(ParserLookaheadProbeKind kind, string? tokenRuleName, string? tokenText, IReadOnlyList<string>? expectedTokenNames = null)
    { Kind = kind; TokenRuleName = tokenRuleName; TokenText = tokenText; ExpectedTokenNames = expectedTokenNames?.ToImmutableArray(); }
    public ParserLookaheadProbeKind Kind { get; }
    public string? TokenRuleName { get; }
    public string? TokenText { get; }
    public IReadOnlyList<string>? ExpectedTokenNames { get; }
}
