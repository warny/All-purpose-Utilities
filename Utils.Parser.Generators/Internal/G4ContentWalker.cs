using System.Collections.Generic;

namespace Utils.Parser.Generators.Internal;

/// <summary>Provides deterministic source-order traversal helpers for generator grammar content.</summary>
internal static class G4ContentWalker
{
    /// <summary>Enumerates parser or lexer rule references found under a content node in source order.</summary>
    internal static IEnumerable<G4RuleRef> EnumerateRuleReferences(G4Content content)
    {
        if (content is G4RuleRef ruleRef) yield return ruleRef;
        else if (content is G4Alternation alternation)
        {
            foreach (var alternative in alternation.Alternatives)
            foreach (var nested in EnumerateRuleReferences(alternative))
                yield return nested;
        }
        else if (content is G4Alternative alternative)
        {
            foreach (var item in alternative.Items)
            foreach (var nested in EnumerateRuleReferences(item))
                yield return nested;
        }
        else if (content is G4Sequence sequence)
        {
            foreach (var item in sequence.Items)
            foreach (var nested in EnumerateRuleReferences(item))
                yield return nested;
        }
        else if (content is G4Quantifier quantifier)
        {
            foreach (var nested in EnumerateRuleReferences(quantifier.Inner)) yield return nested;
        }
        else if (content is G4Negation negation)
        {
            foreach (var nested in EnumerateRuleReferences(negation.Inner)) yield return nested;
        }
    }
}
