namespace Utils.Parser.Runtime;

/// <summary>
/// Immutable input used to prepare one continuation descriptor.
/// This model is structural-only and carries no runtime execution authority.
/// </summary>
/// <param name="RuleName">Owning rule name.</param>
/// <param name="AlternativeIndex">Ordered alternative index.</param>
/// <param name="SequencePosition">Normalized structural sequence position.</param>
/// <param name="ExpectedTokenNames">Shallow expected token names from look-ahead probe metadata.</param>
/// <param name="IsSharedPrefixCandidate">Indicates whether this continuation belongs to a detected shared-prefix candidate.</param>
internal readonly record struct ParserContinuationPreparationInput(
    string RuleName,
    int AlternativeIndex,
    int SequencePosition,
    IReadOnlyList<string>? ExpectedTokenNames,
    bool IsSharedPrefixCandidate);
