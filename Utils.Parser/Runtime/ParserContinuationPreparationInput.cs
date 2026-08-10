using System.Collections.Immutable;
namespace Utils.Parser.Runtime;
/// <summary>Immutable input used to prepare one continuation descriptor.</summary>
internal readonly record struct ParserContinuationPreparationInput
{
    /// <summary>Initializes an input and captures its optional expected-token snapshot.</summary>
    /// <param name="ruleName">Owning rule name.</param>
    /// <param name="alternativeIndex">Ordered alternative index.</param>
    /// <param name="sequencePosition">Normalized structural sequence position.</param>
    /// <param name="expectedTokenNames">Optional shallow expected-token names.</param>
    /// <param name="isSharedPrefixCandidate">Whether the continuation belongs to a shared-prefix candidate.</param>
    public ParserContinuationPreparationInput(string ruleName, int alternativeIndex, int sequencePosition, IReadOnlyList<string>? expectedTokenNames, bool isSharedPrefixCandidate) { RuleName = ruleName; AlternativeIndex = alternativeIndex; SequencePosition = sequencePosition; ExpectedTokenNames = expectedTokenNames?.ToImmutableArray(); IsSharedPrefixCandidate = isSharedPrefixCandidate; }
    public string RuleName { get; }
    public int AlternativeIndex { get; }
    public int SequencePosition { get; }
    public IReadOnlyList<string>? ExpectedTokenNames { get; }
    public bool IsSharedPrefixCandidate { get; }
}
