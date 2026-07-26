using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utils.Parser.Antlr4.Common.Composition;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Resolution;
using Utils.Parser.Runtime;

namespace Utils.Parser.ProjectCompilation;

/// <summary>Compiles ANTLR4 projects spanning multiple imported grammars.</summary>
public static class Antlr4GrammarProjectCompiler
{
    /// <summary>Parses and resolves an entry grammar and all its dependencies.</summary>
    public static ParserDefinition Parse(string entryGrammarName, IGrammarSourceResolver resolver, DiagnosticBag? diagnostics = null)
    {
        var loader = new RuntimeCompositionSourceLoader(resolver, diagnostics);
        IReadOnlyList<IGrammarCompositionSource> entries = loader.Resolve(entryGrammarName);
        if (entries.Count == 0)
        {
            AddMissingGrammarDiagnostic(entryGrammarName, diagnostics);
            throw new GrammarValidationException($"Unable to resolve grammar '{entryGrammarName}'.");
        }
        if (entries.Count > 1)
        {
            AddAmbiguousGrammarDiagnostic(entryGrammarName, entries, diagnostics);
            throw new GrammarValidationException($"Grammar '{entryGrammarName}' resolves to multiple sources.");
        }

        GrammarImportCompositionPlan plan = GrammarImportCompositionPlanner.Build(entries[0], loader.Resolve);
        ValidatePlan(plan, diagnostics);
        return BuildMergedDefinition(plan, diagnostics);
    }

    /// <summary>Compiles an entry grammar and all its dependencies into a runnable instance.</summary>
    public static CompiledGrammar Compile(string entryGrammarName, IGrammarSourceResolver resolver, DiagnosticBag? diagnostics = null) =>
        new(Parse(entryGrammarName, resolver, diagnostics));

    /// <summary>Parses and resolves a grammar project from an entry <c>.g4</c> file.</summary>
    public static ParserDefinition ParseFromFile(string entryFilePath, DiagnosticBag? diagnostics = null)
    {
        string rootDirectory = Path.GetDirectoryName(Path.GetFullPath(entryFilePath)) ?? Directory.GetCurrentDirectory();
        return Parse(Path.GetFileNameWithoutExtension(entryFilePath), new FileSystemGrammarSourceResolver(rootDirectory), diagnostics);
    }

    /// <summary>Compiles a grammar project from an entry <c>.g4</c> file.</summary>
    public static CompiledGrammar CompileFromFile(string entryFilePath, DiagnosticBag? diagnostics = null) =>
        new(ParseFromFile(entryFilePath, diagnostics));

    /// <summary>Projects effective plan declarations into the runtime parser model.</summary>
    private static ParserDefinition BuildMergedDefinition(GrammarImportCompositionPlan plan, DiagnosticBag? diagnostics)
    {
        var entry = (RuntimeCompositionSource)plan.Entry;
        ParserDefinition entryDefinition = entry.Definition;
        ValidateEntryGrammarTypeConstraints(entryDefinition, diagnostics);

        var modeOrder = new List<string>();
        foreach (RuntimeCompositionSource source in plan.Grammars.Cast<RuntimeCompositionSource>())
        {
            foreach (LexerMode mode in source.Definition.Modes)
            {
                if (!modeOrder.Contains(mode.Name, StringComparer.Ordinal))
                {
                    modeOrder.Add(mode.Name);
                }
            }
        }
        if (!modeOrder.Contains("DEFAULT_MODE", StringComparer.Ordinal))
        {
            modeOrder.Insert(0, "DEFAULT_MODE");
        }

        var lexerRules = plan.EffectiveRules
            .Where(item => item.Rule.Domain == GrammarRuleDomain.Lexer)
            .GroupBy(item => item.Rule.LexerMode ?? "DEFAULT_MODE", StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(item => (Rule)item.Rule.Payload).ToList(), StringComparer.Ordinal);
        var modes = modeOrder.Select(name => new LexerMode(name, lexerRules.TryGetValue(name, out List<Rule>? rules) ? rules : [])).ToList();
        var parserRules = plan.EffectiveRules
            .Where(item => item.Rule.Domain == GrammarRuleDomain.Parser)
            .Select(item => (Rule)item.Rule.Payload)
            .ToList();

        foreach (MaskedGrammarRule masked in plan.MaskedRules)
        {
            diagnostics?.AddWithContext(ParserDiagnostics.ImportedRuleIgnoredBecauseAlreadyDefined, null, null, entryDefinition.Name, null, masked.Rule.Rule.Name);
        }

        var declaredTokens = new HashSet<string>(StringComparer.Ordinal);
        var declaredChannels = new HashSet<string>(StringComparer.Ordinal);
        var extensionBindings = new List<GrammarExtensionBinding>();
        foreach (RuntimeCompositionSource source in plan.Grammars.Cast<RuntimeCompositionSource>())
        {
            declaredTokens.UnionWith(source.Definition.DeclaredTokens);
            declaredChannels.UnionWith(source.Definition.DeclaredChannels);
            extensionBindings.AddRange(source.Definition.ExtensionBindings);
        }

        ReorderRules(modes, parserRules);
        ParserDefinition merged = entryDefinition with
        {
            Modes = modes,
            ParserRules = parserRules,
            DeclaredTokens = declaredTokens,
            DeclaredChannels = declaredChannels,
            ExtensionBindings = extensionBindings,
            RootRule = entryDefinition.RootRule,
            AllowExternalLexerRules = true
        };
        return RuleResolver.Resolve(merged, diagnostics);
    }

    /// <summary>Rejects graph failures represented by the plan before runtime projection.</summary>
    private static void ValidatePlan(GrammarImportCompositionPlan plan, DiagnosticBag? diagnostics)
    {
        if (plan.Cycles.Count > 0)
        {
            string cycle = string.Join(" -> ", plan.Cycles[0].Path.Select(identity => identity.DeclaredName));
            diagnostics?.Add(ParserDiagnostics.ImportCycleDetected, cycle);
            throw new GrammarValidationException($"Import cycle detected: {cycle}");
        }
        if (plan.MissingDependencies.Count > 0)
        {
            string name = plan.MissingDependencies[0].Edge.DeclaredDependency.GrammarName;
            AddMissingGrammarDiagnostic(name, diagnostics);
            throw new GrammarValidationException($"Unable to resolve grammar '{name}'.");
        }
        if (plan.AmbiguousDependencies.Count > 0)
        {
            AmbiguousGrammarDependency ambiguity = plan.AmbiguousDependencies[0];
            AddAmbiguousGrammarDiagnostic(ambiguity.Edge.DeclaredDependency.GrammarName, ambiguity.Candidates, diagnostics);
            throw new GrammarValidationException($"Grammar '{ambiguity.Edge.DeclaredDependency.GrammarName}' resolves to multiple sources.");
        }
        if (plan.Collisions.Count > 0)
        {
            GrammarRuleCollision collision = plan.Collisions[0];
            string origins = string.Join(", ", collision.Candidates.Select(candidate => candidate.Origin.SourceId));
            diagnostics?.Add(ParserDiagnostics.ImportedRuleCollision, collision.RuleName, origins);
            throw new GrammarValidationException($"Imported rule '{collision.RuleName}' is ambiguous between: {origins}.");
        }
    }

    /// <summary>Validates grammar-type constraints before project-level projection.</summary>
    private static void ValidateEntryGrammarTypeConstraints(ParserDefinition entry, DiagnosticBag? diagnostics)
    {
        if (entry.Type != GrammarType.Parser)
        {
            return;
        }
        Rule? offending = entry.Modes.SelectMany(mode => mode.Rules).FirstOrDefault();
        if (offending is null)
        {
            return;
        }
        diagnostics?.AddWithContext(ParserDiagnostics.LexerRuleNotAllowedInParserGrammar, null, null, offending.Name, null, offending.Name);
        throw new GrammarValidationException($"Lexer rule '{offending.Name}' is not allowed in a parser grammar.");
    }

    /// <summary>Reassigns sequential declaration order after plan projection.</summary>
    private static void ReorderRules(List<LexerMode> modes, List<Rule> parserRules)
    {
        int order = 0;
        for (int modeIndex = 0; modeIndex < modes.Count; modeIndex++)
        {
            modes[modeIndex] = modes[modeIndex] with { Rules = modes[modeIndex].Rules.Select(rule => rule with { DeclarationOrder = order++ }).ToArray() };
        }
        for (int ruleIndex = 0; ruleIndex < parserRules.Count; ruleIndex++)
        {
            parserRules[ruleIndex] = parserRules[ruleIndex] with { DeclarationOrder = order++ };
        }
    }

    /// <summary>Adds a missing-grammar diagnostic.</summary>
    private static void AddMissingGrammarDiagnostic(string grammarName, DiagnosticBag? diagnostics) =>
        diagnostics?.Add(ParserDiagnostics.ImportedGrammarNotFound, grammarName);

    /// <summary>Adds a deterministic ambiguous-source diagnostic.</summary>
    private static void AddAmbiguousGrammarDiagnostic(string grammarName, IEnumerable<IGrammarCompositionSource> candidates, DiagnosticBag? diagnostics) =>
        AddAmbiguousGrammarDiagnostic(grammarName, candidates.Select(candidate => candidate.Identity), diagnostics);

    /// <summary>Adds a deterministic ambiguous-source diagnostic from structural identities.</summary>
    private static void AddAmbiguousGrammarDiagnostic(string grammarName, IEnumerable<GrammarIdentity> candidates, DiagnosticBag? diagnostics) =>
        diagnostics?.Add(ParserDiagnostics.AmbiguousImportedGrammar, grammarName, string.Join(", ", candidates.Select(candidate => candidate.SourceId)));

    /// <summary>Loads and adapts runtime grammar sources independently from graph planning.</summary>
    private sealed class RuntimeCompositionSourceLoader
    {
        private readonly IGrammarSourceResolver _resolver;
        private readonly DiagnosticBag? _diagnostics;
        private readonly Dictionary<string, RuntimeCompositionSource> _cache = new(StringComparer.Ordinal);

        /// <summary>Initializes the loader.</summary>
        internal RuntimeCompositionSourceLoader(IGrammarSourceResolver resolver, DiagnosticBag? diagnostics)
        {
            _resolver = resolver;
            _diagnostics = diagnostics;
        }

        /// <summary>Resolves and parses all candidates exposed by the source resolver.</summary>
        internal IReadOnlyList<IGrammarCompositionSource> Resolve(string name)
        {
            IReadOnlyList<GrammarSource> sources = _resolver is IGrammarSourceCandidateResolver candidates
                ? candidates.ResolveCandidates(name)
                : _resolver.TryResolve(name, out GrammarSource source) ? [source] : [];
            return sources.Select(Load).Cast<IGrammarCompositionSource>().ToArray();
        }

        /// <summary>Parses one source once and preserves its stable logical identity.</summary>
        private RuntimeCompositionSource Load(GrammarSource source)
        {
            string sourceId = source.Path ?? source.Name;
            if (_cache.TryGetValue(sourceId, out RuntimeCompositionSource? cached))
            {
                return cached;
            }
            var converterDiagnostics = new DiagnosticBag();
            ParserDefinition definition;
            try
            {
                definition = Antlr4GrammarConverter.ParseUnresolved(source.Text, converterDiagnostics);
            }
            finally
            {
                foreach (ParserDiagnostic diagnostic in converterDiagnostics)
                {
                    if (diagnostic.Code != ParserDiagnostics.ImportParsedButNotResolved.Code)
                    {
                        _diagnostics?.Add(diagnostic);
                    }
                }
            }
            var result = new RuntimeCompositionSource(sourceId, definition);
            _cache[sourceId] = result;
            return result;
        }
    }

    /// <summary>Adapts a runtime definition to the common composition source contract.</summary>
    private sealed class RuntimeCompositionSource : IGrammarCompositionSource
    {
        /// <summary>Initializes a runtime composition source.</summary>
        internal RuntimeCompositionSource(string sourceId, ParserDefinition definition)
        {
            Definition = definition;
            Identity = new GrammarIdentity(definition.Name, sourceId);
            var dependencies = new List<GrammarDependency>();
            if (definition.Options?.Values.TryGetValue("tokenVocab", out string? tokenVocab) == true && !string.IsNullOrWhiteSpace(tokenVocab))
            {
                dependencies.Add(new GrammarDependency(tokenVocab!, null, GrammarDependencyKind.TokenVocab, definition.Options));
            }
            dependencies.AddRange(definition.Imports.Select(import => new GrammarDependency(import.GrammarName, import.Alias, GrammarDependencyKind.FullImport, import)));
            Dependencies = dependencies;
            Rules = definition.Modes.SelectMany(mode => mode.Rules.Select(rule => new GrammarRuleDescriptor(rule.Name, GrammarRuleDomain.Lexer, mode.Name, rule)))
                .Concat(definition.ParserRules.Select(rule => new GrammarRuleDescriptor(rule.Name, GrammarRuleDomain.Parser, null, rule)))
                .ToArray();
        }

        /// <summary>Gets the runtime definition payload.</summary>
        internal ParserDefinition Definition { get; }

        /// <inheritdoc />
        public GrammarIdentity Identity { get; }

        /// <inheritdoc />
        public IReadOnlyList<GrammarDependency> Dependencies { get; }

        /// <inheritdoc />
        public IReadOnlyList<GrammarRuleDescriptor> Rules { get; }

        /// <inheritdoc />
        public object Payload => Definition;

        /// <inheritdoc />
        public object? RootRulePayload => Definition.RootRule;
    }
}
