using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Creates structural parser continuation metadata descriptors.
/// Produced descriptors are transport metadata only and never executable parser runtime frames.
/// </summary>
internal sealed class ParserContinuationFactory
{
    /// <summary>
    /// Creates a continuation descriptor from shallow rule/alternative location metadata.
    /// The output is observational metadata and can be discarded without changing parse-authoritative outcomes.
    /// </summary>
    /// <param name="rule">Owning rule for the continuation point.</param>
    /// <param name="alternative">Owning alternative for the continuation point.</param>
    /// <param name="alternativeIndex">Structural alternative index within the owning rule.</param>
    /// <param name="sequencePosition">Structural position within the alternative sequence.</param>
    /// <param name="expectedTokenNames">Optional shallow expected token names for this point.</param>
    /// <param name="isSharedPrefixCandidate">Whether this point was observed from a shared-prefix candidate.</param>
    /// <returns>A structural continuation descriptor.</returns>
    public ParserContinuationDescriptor Create(
        ParserContinuationPreparationInput input)
    {
        var category = ClassifyContinuation(input.ExpectedTokenNames, input.IsSharedPrefixCandidate);
        return new ParserContinuationDescriptor(
            new ParserContinuationKey(input.RuleName, input.AlternativeIndex, input.SequencePosition),
            category,
            input.ExpectedTokenNames?.ToArray(),
            input.IsSharedPrefixCandidate);
    }

    /// <summary>
    /// Classifies a continuation descriptor with conservative descriptive categories.
    /// Classification remains metadata-only and does not imply replay, execution, or resume capabilities.
    /// </summary>
    /// <param name="normalizedSequencePosition">Normalized sequence depth.</param>
    /// <param name="expectedTokenNames">Shallow expected token names.</param>
    /// <param name="isSharedPrefixCandidate">Whether shared-prefix candidate metadata was detected.</param>
    /// <returns>A deterministic descriptive category.</returns>
    private static ParserContinuationCategory ClassifyContinuation(
        IReadOnlyList<string>? expectedTokenNames,
        bool isSharedPrefixCandidate)
    {
        if (isSharedPrefixCandidate)
        {
            return ParserContinuationCategory.SharedPrefixCandidate;
        }

        if (expectedTokenNames is null || expectedTokenNames.Count == 0)
        {
            return ParserContinuationCategory.Terminal;
        }

        return ParserContinuationCategory.Sequential;
    }
}
