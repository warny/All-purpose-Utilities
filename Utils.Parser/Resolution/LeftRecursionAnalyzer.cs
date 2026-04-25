using Utils.Parser.Diagnostics;
using Utils.Parser.Model;

namespace Utils.Parser.Resolution;

/// <summary>
/// Analyses parser rules to identify direct left recursion and unsupported
/// indirect left-recursive cycles.
/// </summary>
internal static class LeftRecursionAnalyzer
{
    /// <summary>
    /// Builds direct left-recursion metadata for parser rules.
    /// </summary>
    /// <param name="definition">Resolved parser definition.</param>
    /// <param name="diagnostics">Optional diagnostics sink.</param>
    /// <returns>Dictionary keyed by rule name.</returns>
    /// <exception cref="GrammarValidationException">
    /// Thrown when a direct left-recursive rule has no base alternative or when
    /// an indirect left-recursive cycle is found.
    /// </exception>
    public static IReadOnlyDictionary<string, LeftRecursiveRuleInfo> Analyze(
        ParserDefinition definition,
        DiagnosticBag? diagnostics = null)
    {
        var map = new Dictionary<string, LeftRecursiveRuleInfo>(StringComparer.Ordinal);

        foreach (var rule in definition.ParserRules)
        {
            var baseAlternatives = new List<Alternative>();
            var recursiveAlternatives = new List<Alternative>();

            foreach (var alternative in rule.Content.Alternatives)
            {
                var leading = GetLeadingRuleRef(alternative.Content);
                if (leading is not null && string.Equals(leading.RuleName, rule.Name, StringComparison.Ordinal))
                {
                    recursiveAlternatives.Add(alternative);
                }
                else
                {
                    baseAlternatives.Add(alternative);
                }
            }

            if (recursiveAlternatives.Count == 0)
            {
                continue;
            }

            diagnostics?.AddWithContext(
                ParserDiagnostics.DirectLeftRecursionDetected,
                null,
                null,
                rule.Name,
                null,
                rule.Name);

            if (baseAlternatives.Count == 0)
            {
                diagnostics?.AddWithContext(
                    ParserDiagnostics.LeftRecursiveRuleWithoutBaseAlternative,
                    null,
                    null,
                    rule.Name,
                    null,
                    rule.Name);
                throw new GrammarValidationException(
                    $"Left-recursive rule '{rule.Name}' has no base alternative.");
            }

            map[rule.Name] = new LeftRecursiveRuleInfo
            {
                Rule = rule,
                BaseAlternatives = baseAlternatives,
                RecursiveAlternatives = recursiveAlternatives
            };
        }

        DetectIndirectLeftRecursion(definition, map, diagnostics);
        return map;
    }

    private static void DetectIndirectLeftRecursion(
        ParserDefinition definition,
        IReadOnlyDictionary<string, LeftRecursiveRuleInfo> directRules,
        DiagnosticBag? diagnostics)
    {
        var firstReferenceGraph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var rule in definition.ParserRules)
        {
            var targets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var alternative in rule.Content.Alternatives)
            {
                var leading = GetLeadingRuleRef(alternative.Content);
                if (leading is not null &&
                    definition.AllRules.TryGetValue(leading.RuleName, out var referenced) &&
                    referenced.Kind == RuleKind.Parser)
                {
                    targets.Add(leading.RuleName);
                }
            }

            firstReferenceGraph[rule.Name] = targets;
        }

        foreach (var rule in definition.ParserRules)
        {
            if (directRules.ContainsKey(rule.Name))
            {
                continue;
            }

            if (!firstReferenceGraph.TryGetValue(rule.Name, out var targets))
            {
                continue;
            }

            foreach (var target in targets)
            {
                if (target == rule.Name)
                {
                    continue;
                }

                if (HasPath(firstReferenceGraph, target, rule.Name))
                {
                    diagnostics?.AddWithContext(
                        ParserDiagnostics.IndirectLeftRecursionNotSupported,
                        null,
                        null,
                        rule.Name,
                        null,
                        rule.Name);
                    throw new GrammarValidationException(
                        $"Indirect left recursion is not supported yet. Cycle includes '{rule.Name}'.");
                }
            }
        }
    }

    private static bool HasPath(
        IReadOnlyDictionary<string, HashSet<string>> graph,
        string start,
        string goal)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            if (string.Equals(current, goal, StringComparison.Ordinal))
            {
                return true;
            }

            if (!graph.TryGetValue(current, out var next))
            {
                continue;
            }

            foreach (var child in next)
            {
                stack.Push(child);
            }
        }

        return false;
    }

    private static RuleRef? GetLeadingRuleRef(RuleContent content)
    {
        return content switch
        {
            RuleRef direct => direct,
            Sequence { Items.Count: > 0 } sequence => GetLeadingRuleRef(sequence.Items[0]),
            Alternative alternative => GetLeadingRuleRef(alternative.Content),
            _ => null
        };
    }
}
