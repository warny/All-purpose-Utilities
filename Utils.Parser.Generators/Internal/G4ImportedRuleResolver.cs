using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Utils.Parser.Generators.Internal;

/// <summary>Resolves parser-rule references through local rules and conservatively resolvable grammar imports.</summary>
internal sealed class G4ImportedRuleResolver
{
    private readonly G4GrammarProjectIndex _index;

    /// <summary>Initializes a resolver over a deterministic project grammar index.</summary>
    /// <param name="index">Project grammar index keyed by declared grammar names.</param>
    public G4ImportedRuleResolver(G4GrammarProjectIndex index) => _index = index;

    /// <summary>Resolves a parser-rule reference from the perspective of one caller grammar.</summary>
    /// <param name="caller">Caller grammar containing the rule reference.</param>
    /// <param name="ruleName">Parser-rule name referenced at the call site.</param>
    /// <returns>A structured rule resolution result.</returns>
    internal G4RuleResolution Resolve(G4Grammar caller, string ruleName)
    {
        var local = FindParserRules(caller, ruleName).ToImmutableArray();
        if (local.Length == 1)
        {
            return G4RuleResolution.Local(local[0]);
        }

        if (local.Length > 1)
        {
            return G4RuleResolution.Ambiguous(ruleName);
        }

        var candidates = new HashSet<G4Rule>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { caller.Name };
        CollectImportedCandidates(caller, ruleName, visited, candidates);

        return candidates.Count switch
        {
            0 => G4RuleResolution.Unresolved(ruleName),
            1 => G4RuleResolution.Imported(candidates.Single()),
            _ => G4RuleResolution.Ambiguous(ruleName)
        };
    }

    /// <summary>Collects unique imported parser-rule declaration candidates reachable through direct and transitive imports.</summary>
    private void CollectImportedCandidates(G4Grammar grammar, string ruleName, HashSet<string> visited, HashSet<G4Rule> candidates)
    {
        foreach (var import in grammar.Imports.OrderBy(static import => import.GrammarName, StringComparer.Ordinal).ThenBy(static import => import.Alias ?? string.Empty, StringComparer.Ordinal))
        {
            if (import.Alias is not null)
            {
                continue;
            }

            var resolution = _index.ResolveGrammar(import.GrammarName);
            if (resolution.Kind != G4GrammarNameResolutionKind.Resolved || resolution.Entry is null)
            {
                continue;
            }

            var importedGrammar = resolution.Entry.Value.Grammar;
            if (!visited.Add(importedGrammar.Name))
            {
                continue;
            }

            AddParserRules(importedGrammar, ruleName, candidates);
            CollectImportedCandidates(importedGrammar, ruleName, visited, candidates);
            visited.Remove(importedGrammar.Name);
        }
    }

    /// <summary>Adds parser-domain rules by declaration identity without considering lexer rules.</summary>
    private static void AddParserRules(G4Grammar grammar, string ruleName, HashSet<G4Rule> candidates)
    {
        foreach (var rule in FindParserRules(grammar, ruleName))
        {
            candidates.Add(rule);
        }
    }

    /// <summary>Finds parser-domain rules by name without considering lexer rules.</summary>
    private static IEnumerable<G4Rule> FindParserRules(G4Grammar grammar, string ruleName) => grammar.ParserRules.Where(rule => string.Equals(rule.Name, ruleName, StringComparison.Ordinal));
}

/// <summary>Describes how a parser-rule reference resolved for static binding validation.</summary>
internal readonly record struct G4RuleResolution
{
    /// <summary>Initializes a structured rule resolution result.</summary>
    private G4RuleResolution(G4RuleResolutionKind kind, string ruleName, G4Rule? rule)
    {
        Kind = kind;
        RuleName = ruleName;
        Rule = rule;
    }

    /// <summary>Gets the rule resolution state.</summary>
    internal G4RuleResolutionKind Kind { get; }

    /// <summary>Gets the requested rule name.</summary>
    internal string RuleName { get; }

    /// <summary>Gets the unique target rule for local and imported resolutions.</summary>
    internal G4Rule? Rule { get; }

    /// <summary>Creates a local rule resolution.</summary>
    internal static G4RuleResolution Local(G4Rule rule) => new(G4RuleResolutionKind.Local, rule.Name, rule);

    /// <summary>Creates an imported rule resolution.</summary>
    internal static G4RuleResolution Imported(G4Rule rule) => new(G4RuleResolutionKind.Imported, rule.Name, rule);

    /// <summary>Creates an unresolved rule resolution.</summary>
    internal static G4RuleResolution Unresolved(string ruleName) => new(G4RuleResolutionKind.Unresolved, ruleName, null);

    /// <summary>Creates an ambiguous rule resolution.</summary>
    internal static G4RuleResolution Ambiguous(string ruleName) => new(G4RuleResolutionKind.Ambiguous, ruleName, null);
}

/// <summary>Identifies parser-rule resolution states used by static generated-binding validation.</summary>
internal enum G4RuleResolutionKind
{
    /// <summary>The target is the caller grammar's unique parser rule.</summary>
    Local,

    /// <summary>The target is a unique parser rule from a resolvable imported grammar chain.</summary>
    Imported,

    /// <summary>No certain parser-rule target is available.</summary>
    Unresolved,

    /// <summary>Multiple possible parser-rule targets are available.</summary>
    Ambiguous
}
