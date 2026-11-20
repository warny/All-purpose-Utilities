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

    /// <summary>Initializes a probe result around an expected-token snapshot captured by another result, without copying or re-wrapping it.</summary>
    /// <param name="kind">Observed probe kind.</param>
    /// <param name="tokenRuleName">Optional observed token rule name.</param>
    /// <param name="tokenText">Optional observed token text.</param>
    /// <param name="capturedExpectedTokenNames">The <see cref="ExpectedTokenNames"/> instance of an existing result.</param>
    /// <param name="captured">Distinguishes this constructor; ignored.</param>
    private ParserLookaheadProbeResult(ParserLookaheadProbeKind kind, string? tokenRuleName, string? tokenText, IReadOnlyList<string>? capturedExpectedTokenNames, bool captured)
    { Kind = kind; TokenRuleName = tokenRuleName; TokenText = tokenText; ExpectedTokenNames = capturedExpectedTokenNames; }

    public ParserLookaheadProbeKind Kind { get; }
    public string? TokenRuleName { get; }
    public string? TokenText { get; }
    public IReadOnlyList<string>? ExpectedTokenNames { get; }

    /// <summary>
    /// Rebuilds a stored result from its fields, keeping the expected-token snapshot instance of the original result, so
    /// the rebuilt result equals the original one and exposes the same instances (used by <see cref="ParserLookaheadCache"/>).
    /// </summary>
    /// <param name="kind">Observed probe kind.</param>
    /// <param name="tokenRuleName">Optional observed token rule name.</param>
    /// <param name="tokenText">Optional observed token text.</param>
    /// <param name="capturedExpectedTokenNames">The <see cref="ExpectedTokenNames"/> instance of the original result.</param>
    /// <returns>The rebuilt result.</returns>
    internal static ParserLookaheadProbeResult FromCapturedSnapshot(ParserLookaheadProbeKind kind, string? tokenRuleName, string? tokenText, IReadOnlyList<string>? capturedExpectedTokenNames)
        => new(kind, tokenRuleName, tokenText, capturedExpectedTokenNames, captured: true);
}
