using System;
using System.Linq;
using Utils.Parser.Antlr4.Common.Composition;

namespace Utils.Parser.Generators.Internal;

/// <summary>Temporary rule-resolution facade that delegates all graph and composition decisions to the shared plan.</summary>
internal sealed class G4ImportedRuleResolver
{
    private readonly G4GrammarProjectIndex _index;

    /// <summary>Initializes a resolver over a deterministic project grammar index.</summary>
    public G4ImportedRuleResolver(G4GrammarProjectIndex index) => _index = index;

    /// <summary>Resolves a parser-rule reference from the perspective of one caller grammar.</summary>
    internal G4RuleResolution Resolve(G4Grammar caller, string ruleName)
    {
        G4GrammarNameResolution callerResolution = _index.ResolveGrammar(caller.Name);
        if (callerResolution.Kind != G4GrammarNameResolutionKind.Resolved || callerResolution.Entry is not G4GrammarProjectEntry entry)
        {
            return G4RuleResolution.Ambiguous(ruleName);
        }

        GrammarImportCompositionPlan plan = new G4GrammarCompositionAdapter(_index).Build(entry);
        EffectiveGrammarRule[] matches = plan.EffectiveRules
            .Where(item => item.Rule.Domain == GrammarRuleDomain.Parser && string.Equals(item.Rule.Name, ruleName, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            return matches.Length == 0 && plan.Collisions.All(collision => !string.Equals(collision.RuleName, ruleName, StringComparison.Ordinal))
                ? G4RuleResolution.Unresolved(ruleName)
                : G4RuleResolution.Ambiguous(ruleName);
        }

        var rule = (G4Rule)matches[0].Rule.Payload;
        return matches[0].Origin.Equals(plan.Entry.Identity)
            ? G4RuleResolution.Local(rule)
            : G4RuleResolution.Imported(rule);
    }
}

/// <summary>Describes how a parser-rule reference resolved for static binding validation.</summary>
internal readonly record struct G4RuleResolution
{
    /// <summary>Initializes a structured rule resolution result.</summary>
    private G4RuleResolution(G4RuleResolutionKind kind, string ruleName, G4Rule? rule) { Kind = kind; RuleName = ruleName; Rule = rule; }
    /// <summary>Gets the rule resolution state.</summary>
    internal G4RuleResolutionKind Kind { get; }
    /// <summary>Gets the requested rule name.</summary>
    internal string RuleName { get; }
    /// <summary>Gets the unique target rule.</summary>
    internal G4Rule? Rule { get; }
    /// <summary>Creates a local result.</summary>
    internal static G4RuleResolution Local(G4Rule rule) => new(G4RuleResolutionKind.Local, rule.Name, rule);
    /// <summary>Creates an imported result.</summary>
    internal static G4RuleResolution Imported(G4Rule rule) => new(G4RuleResolutionKind.Imported, rule.Name, rule);
    /// <summary>Creates an unresolved result.</summary>
    internal static G4RuleResolution Unresolved(string ruleName) => new(G4RuleResolutionKind.Unresolved, ruleName, null);
    /// <summary>Creates an ambiguous result.</summary>
    internal static G4RuleResolution Ambiguous(string ruleName) => new(G4RuleResolutionKind.Ambiguous, ruleName, null);
}

/// <summary>Identifies parser-rule resolution states used by static generated-binding validation.</summary>
internal enum G4RuleResolutionKind
{
    /// <summary>The target is local.</summary>
    Local,
    /// <summary>The target is imported.</summary>
    Imported,
    /// <summary>No target is available.</summary>
    Unresolved,
    /// <summary>Multiple targets are possible.</summary>
    Ambiguous
}
