using Utils.Parser.Model;

namespace Utils.Parser.Runtime;

/// <summary>
/// Extracts conservative structural token prefixes from grammar alternatives.
/// This class encapsulates all grammar-model traversal required for shared-prefix metadata preparation,
/// keeping grammar inspection out of the scheduler and plan factory paths.
/// Extraction is deterministic, runtime-independent, and scheduler-independent.
/// </summary>
internal sealed class AlternativeStructuralPrefixExtractor
{
    /// <summary>
    /// Extracts a structural descriptor for every alternative in the ordered list.
    /// The resulting descriptors carry only token name strings; callers receive no grammar model references.
    /// </summary>
    /// <param name="alternatives">Ordered alternatives to inspect.</param>
    /// <returns>
    /// One descriptor per alternative, with the same ordering as the input list.
    /// Each descriptor's <see cref="AlternativeStructuralDescriptor.AlternativeIndex"/> matches
    /// the position in the input list.
    /// </returns>
    public IReadOnlyList<AlternativeStructuralDescriptor> ExtractAll(IReadOnlyList<Alternative> alternatives)
    {
        var descriptors = new AlternativeStructuralDescriptor[alternatives.Count];
        for (var index = 0; index < alternatives.Count; index++)
        {
            descriptors[index] = new AlternativeStructuralDescriptor(
                index,
                ExtractStructuralTokens(alternatives[index].Content));
        }
        return descriptors;
    }

    /// <summary>Extracts the ordered leading structural token names from a single alternative's content.</summary>
    private static IReadOnlyList<string> ExtractStructuralTokens(RuleContent content)
    {
        if (content is Sequence sequence)
        {
            var tokens = new List<string>();
            for (var index = 0; index < sequence.Items.Count; index++)
            {
                if (!TryGetStructuralToken(sequence.Items[index], out var token))
                {
                    break;
                }

                tokens.Add(token);
            }

            return tokens.AsReadOnly();
        }

        return TryGetStructuralToken(content, out var singleToken)
            ? Array.AsReadOnly(new[] { singleToken })
            : [];
    }

    /// <summary>Extracts a single structural token name from a rule-content node that is a direct literal or rule reference; returns <see langword="false"/> for composite content.</summary>
    private static bool TryGetStructuralToken(RuleContent content, out string tokenName)
    {
        switch (content)
        {
            case RuleRef reference:
                tokenName = reference.RuleName;
                return true;
            case LiteralMatch literal:
                tokenName = literal.Value;
                return true;
            default:
                tokenName = string.Empty;
                return false;
        }
    }
}
