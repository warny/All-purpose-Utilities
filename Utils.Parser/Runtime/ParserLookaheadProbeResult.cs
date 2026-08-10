using System.Collections.Immutable;

namespace Utils.Parser.Runtime;

/// <summary>Stores an immutable lightweight structured look-ahead probe observation.</summary>
internal readonly record struct ParserLookaheadProbeResult
{
    /// <summary>Initializes a probe result and captures its optional expected-token snapshot.</summary>
    /// <param name="kind">Observed probe kind.</param>
    /// <param name="tokenRuleName">Optional observed token rule name.</param>
    /// <param name="tokenText">Optional observed token text.</param>
    /// <param name="expectedTokenNames">Optional expected-token names; <see langword="null"/> when unavailable.</param>
    public ParserLookaheadProbeResult(ParserLookaheadProbeKind kind, string? tokenRuleName, string? tokenText, IReadOnlyList<string>? expectedTokenNames = null)
    { Kind = kind; TokenRuleName = tokenRuleName; TokenText = tokenText; ExpectedTokenNames = expectedTokenNames?.ToImmutableArray(); }
    public ParserLookaheadProbeKind Kind { get; }
    public string? TokenRuleName { get; }
    public string? TokenText { get; }
    public IReadOnlyList<string>? ExpectedTokenNames { get; }
}
