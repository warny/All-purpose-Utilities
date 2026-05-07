using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Creates structural parser continuation metadata descriptors.
/// </summary>
internal sealed class ParserContinuationFactory
{
    /// <summary>
    /// Creates a continuation descriptor from shallow rule/alternative location metadata.
    /// </summary>
    /// <param name="rule">Owning rule for the continuation point.</param>
    /// <param name="alternative">Owning alternative for the continuation point.</param>
    /// <param name="alternativeIndex">Structural alternative index within the owning rule.</param>
    /// <param name="sequencePosition">Structural position within the alternative sequence.</param>
    /// <param name="expectedTokenNames">Optional shallow expected token names for this point.</param>
    /// <param name="isSharedPrefixCandidate">Whether this point was observed from a shared-prefix candidate.</param>
    /// <returns>A structural continuation descriptor.</returns>
    public ParserContinuationDescriptor Create(
        Rule rule,
        Alternative alternative,
        int alternativeIndex,
        int sequencePosition,
        IReadOnlyList<string>? expectedTokenNames,
        bool isSharedPrefixCandidate)
    {
        var normalizedSequencePosition = ComputeSequencePosition(alternative, sequencePosition);
        return new ParserContinuationDescriptor(
            new ParserContinuationKey(rule.Name, alternativeIndex, normalizedSequencePosition),
            expectedTokenNames?.ToArray(),
            isSharedPrefixCandidate);
    }

    /// <summary>
    /// Computes a shallow sequence position for continuation identity.
    /// </summary>
    /// <param name="alternative">Alternative that owns the continuation point.</param>
    /// <param name="sequencePosition">Raw sequence item index in the alternative content.</param>
    /// <returns>Normalized index among meaningful sequence items, or zero for non-sequence alternatives.</returns>
    private static int ComputeSequencePosition(Alternative alternative, int sequencePosition)
    {
        if (alternative.Content is not Sequence sequence)
        {
            return 0;
        }

        if (sequencePosition <= 0)
        {
            return 0;
        }

        var meaningfulIndex = 0;
        var maxIndex = sequence.Items.Count - 1;
        var boundedPosition = sequencePosition > maxIndex ? maxIndex : sequencePosition;

        for (var index = 0; index < boundedPosition; index++)
        {
            var item = sequence.Items[index];
            if (item is EmbeddedAction || item is LexerCommand)
            {
                continue;
            }

            meaningfulIndex++;
        }

        return meaningfulIndex;
    }
}
