using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Parser.Antlr4.Common.Composition;

namespace Utils.Parser.Generators.Internal;

/// <summary>Projects generator AST payloads into the shared Roslyn-free composition planner.</summary>
internal sealed class G4GrammarCompositionAdapter
{
    private readonly G4GrammarProjectIndex _index;
    private readonly Dictionary<G4GrammarProjectEntry, G4CompositionSource> _sources = [];

    /// <summary>Initializes an adapter over a deterministic project index.</summary>
    internal G4GrammarCompositionAdapter(G4GrammarProjectIndex index) => _index = index;

    /// <summary>Builds a shared composition plan while retaining all original G4 payload objects.</summary>
    internal GrammarImportCompositionPlan Build(G4GrammarProjectEntry entry) =>
        GrammarImportCompositionPlanner.Build(GetSource(entry), Resolve);

    /// <summary>Resolves a declared grammar name through the existing project index.</summary>
    private IReadOnlyList<IGrammarCompositionSource> Resolve(string grammarName)
    {
        G4GrammarNameResolution resolution = _index.ResolveGrammar(grammarName);
        return resolution.Kind switch
        {
            G4GrammarNameResolutionKind.Resolved when resolution.Entry is G4GrammarProjectEntry entry => [GetSource(entry)],
            G4GrammarNameResolutionKind.Ambiguous => resolution.Candidates.Select(candidate => (IGrammarCompositionSource)GetSource(candidate)).ToArray(),
            _ => []
        };
    }

    /// <summary>Gets or creates the stable adapter for one project entry.</summary>
    private G4CompositionSource GetSource(G4GrammarProjectEntry entry)
    {
        if (!_sources.TryGetValue(entry, out G4CompositionSource? source))
        {
            source = new G4CompositionSource(entry);
            _sources[entry] = source;
        }
        return source;
    }

    /// <summary>Adapts one G4 grammar and retains rule, mode, import, and grammar payloads.</summary>
    private sealed class G4CompositionSource : IGrammarCompositionSource
    {
        /// <summary>Initializes a G4 source adapter.</summary>
        internal G4CompositionSource(G4GrammarProjectEntry entry)
        {
            Entry = entry;
            Identity = new GrammarIdentity(entry.Grammar.Name, entry.Path);
            var dependencies = entry.Grammar.Imports.Select(import => new GrammarDependency(import.GrammarName, import.Alias, GrammarDependencyKind.FullImport, import)).ToList();
            if (entry.Grammar.Options.TryGetValue("tokenVocab", out string? tokenVocab) && !string.IsNullOrWhiteSpace(tokenVocab))
            {
                dependencies.Add(new GrammarDependency(tokenVocab, null, GrammarDependencyKind.TokenVocab, entry.Grammar.Options));
            }
            Dependencies = dependencies;
            Rules = entry.Grammar.LexerRules.Select(rule => new GrammarRuleDescriptor(rule.Name, GrammarRuleDomain.Lexer, "DEFAULT_MODE", rule))
                .Concat(entry.Grammar.ExtraModes.SelectMany(mode => mode.Rules.Select(rule => new GrammarRuleDescriptor(rule.Name, GrammarRuleDomain.Lexer, mode.Name, rule))))
                .Concat(entry.Grammar.ParserRules.Select(rule => new GrammarRuleDescriptor(rule.Name, GrammarRuleDomain.Parser, null, rule)))
                .ToArray();
        }

        /// <summary>Gets the original project entry.</summary>
        internal G4GrammarProjectEntry Entry { get; }

        /// <inheritdoc />
        public GrammarIdentity Identity { get; }

        /// <inheritdoc />
        public IReadOnlyList<GrammarDependency> Dependencies { get; }

        /// <inheritdoc />
        public IReadOnlyList<GrammarRuleDescriptor> Rules { get; }

        /// <inheritdoc />
        public object Payload => Entry.Grammar;

        /// <inheritdoc />
        public object? RootRulePayload => Entry.Grammar.ParserRules.FirstOrDefault();
    }
}
