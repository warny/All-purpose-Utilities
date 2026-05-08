using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Creates structural parser continuation metadata descriptors.
/// </summary>
internal sealed class ParserContinuationFactory
{
    /// <summary>
    /// Computes a conservative sequence position for shared-token continuation metadata.
    /// This shallow analysis is preparatory only and intentionally avoids deep parser semantics.
    /// </summary>
    /// <param name="alternative">Alternative containing the shared token.</param>
    /// <param name="sharedTokenName">Shared token identifier from look-ahead metadata.</param>
    /// <returns>Raw sequence-item index after the shared token, or zero when unsupported/ambiguous.</returns>
    public int ComputeSharedPrefixSequencePosition(Alternative alternative, string sharedTokenName)
    {
        if (alternative.Content is not Sequence sequence)
        {
            return 0;
        }

        var firstMeaningfulItemIndex = -1;
        for (var index = 0; index < sequence.Items.Count; index++)
        {
            if (sequence.Items[index] is EmbeddedAction or LexerCommand)
            {
                continue;
            }

            firstMeaningfulItemIndex = index;
            break;
        }

        if (firstMeaningfulItemIndex < 0 || !IsSharedTokenMatchFromProbeMetadata(sequence.Items[firstMeaningfulItemIndex], sharedTokenName))
        {
            return 0;
        }

        return firstMeaningfulItemIndex + 1;
    }

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

    /// <summary>
    /// Determines whether a shallow sequence item matches a shared token name
    /// coming from look-ahead probe metadata.
    /// This helper intentionally performs no rule-kind resolution and therefore
    /// must only be used with probe metadata that already represents shallow token candidates.
    /// </summary>
    private static bool IsSharedTokenMatchFromProbeMetadata(RuleContent content, string sharedTokenName)
    {
        return content switch
        {
            LiteralMatch literal => string.Equals(literal.Value, sharedTokenName, StringComparison.Ordinal),
            RuleRef ruleRef => string.Equals(ruleRef.RuleName, sharedTokenName, StringComparison.Ordinal),
            _ => false
        };
    }
}
