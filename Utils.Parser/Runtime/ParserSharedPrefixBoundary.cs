using System.Collections.Immutable;
namespace Utils.Parser.Runtime;
/// <summary>Represents an immutable structural continuation boundary.</summary>
internal readonly record struct ParserSharedPrefixBoundary
{
    /// <summary>Initializes a boundary and captures its optional expected-token snapshot.</summary>
    /// <param name="sequencePosition">Conservative structural sequence position.</param>
    /// <param name="expectedTokenNames">Optional expected-token names; <see langword="null"/> when unavailable.</param>
    public ParserSharedPrefixBoundary(int sequencePosition, IReadOnlyList<string>? expectedTokenNames) { SequencePosition = sequencePosition; ExpectedTokenNames = expectedTokenNames?.ToImmutableArray(); }
    public int SequencePosition { get; }
    public IReadOnlyList<string>? ExpectedTokenNames { get; }
}
