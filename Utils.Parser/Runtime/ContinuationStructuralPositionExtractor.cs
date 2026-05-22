using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Extracts conservative structural continuation positions from grammar alternatives.
/// This component only inspects shallow structure and does not classify or create metadata descriptors.
/// </summary>
internal sealed class ContinuationStructuralPositionExtractor
{
    /// <summary>
    /// Computes the structural continuation position associated with a shared-token candidate.
    /// Returns zero when no supported conservative position is available.
    /// </summary>
    public int ExtractSharedPrefixSequencePosition(Alternative alternative, string sharedTokenName)
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

        return NormalizeMeaningfulSequencePosition(sequence, firstMeaningfulItemIndex + 1);
    }

    private static int NormalizeMeaningfulSequencePosition(Sequence sequence, int sequencePosition)
    {
        if (sequencePosition <= 0)
        {
            return 0;
        }

        var meaningfulIndex = 0;
        var boundedPosition = sequencePosition > sequence.Items.Count ? sequence.Items.Count : sequencePosition;

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
