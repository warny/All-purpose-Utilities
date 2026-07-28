using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Parser.Antlr4.Common.Composition;

namespace Utils.Parser.Generators.Internal;

/// <summary>Projects final decisions from the shared import-composition plan into the generator emission model.</summary>
internal static class EffectiveG4GrammarProjection
{
    /// <summary>Creates an emission-only grammar without rebuilding or reconsidering dependency graph decisions.</summary>
    internal static G4Grammar Create(GrammarImportCompositionPlan plan)
    {
        var entry = (G4Grammar)plan.Entry.Payload;
        var result = new G4Grammar
        {
            Name = entry.Name,
            Kind = entry.Kind,
            HasTokensBlock = entry.HasTokensBlock,
            HasChannelsBlock = entry.HasChannelsBlock,
            RootRule = (G4Rule?)plan.RootRulePayload,
            AllowExternalLexerRules = entry.Kind == G4GrammarKind.Parser
                && plan.EffectiveRules.Any(rule => rule.Rule.Domain == GrammarRuleDomain.Lexer && !rule.Origin.Equals(plan.Entry.Identity))
        };
        foreach (KeyValuePair<string, string> option in entry.Options) result.Options.Add(option.Key, option.Value);
        result.Actions.AddRange(entry.Actions);
        result.Imports.AddRange(entry.Imports);
        foreach (IGrammarCompositionSource source in plan.Grammars)
        {
            var grammar = (G4Grammar)source.Payload;
            AddDistinct(result.DeclaredTokens, grammar.DeclaredTokens);
            AddDistinct(result.DeclaredChannels, grammar.DeclaredChannels);
            foreach (G4LexerMode sourceMode in grammar.ExtraModes)
            {
                if (!result.ExtraModes.Any(mode => string.Equals(mode.Name, sourceMode.Name, StringComparison.Ordinal)))
                {
                    result.ExtraModes.Add(new G4LexerMode { Name = sourceMode.Name });
                }
            }
        }
        foreach (EffectiveGrammarRule effective in plan.EffectiveRules)
        {
            var rule = (G4Rule)effective.Rule.Payload;
            if (effective.Rule.Domain == GrammarRuleDomain.Parser) { result.ParserRules.Add(rule); continue; }
            string modeName = effective.Rule.LexerMode ?? "DEFAULT_MODE";
            if (string.Equals(modeName, "DEFAULT_MODE", StringComparison.Ordinal)) { result.LexerRules.Add(rule); continue; }
            G4LexerMode? mode = result.ExtraModes.FirstOrDefault(candidate => string.Equals(candidate.Name, modeName, StringComparison.Ordinal));
            if (mode is null) { mode = new G4LexerMode { Name = modeName }; result.ExtraModes.Add(mode); }
            mode.Rules.Add(rule);
        }
        return result;
    }

    /// <summary>Adds names once while preserving their first deterministic declaration order.</summary>
    private static void AddDistinct(List<string> target, IEnumerable<string> source)
    {
        foreach (string value in source) if (!target.Contains(value, StringComparer.Ordinal)) target.Add(value);
    }
}
