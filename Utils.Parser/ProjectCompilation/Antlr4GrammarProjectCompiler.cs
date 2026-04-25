using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Utils.Parser.Bootstrap;
using Utils.Parser.Diagnostics;
using Utils.Parser.Model;
using Utils.Parser.Resolution;
using Utils.Parser.Runtime;

namespace Utils.Parser.ProjectCompilation;

/// <summary>
/// Compiles ANTLR4 projects spanning multiple imported grammars.
/// </summary>
public static class Antlr4GrammarProjectCompiler
{
    /// <summary>
    /// Parses and resolves an entry grammar and all its dependencies.
    /// </summary>
    /// <param name="entryGrammarName">Entry grammar logical name.</param>
    /// <param name="resolver">Source resolver used to locate grammar files.</param>
    /// <param name="diagnostics">Optional diagnostics collection.</param>
    /// <returns>Resolved merged parser definition.</returns>
    public static ParserDefinition Parse(string entryGrammarName, IGrammarSourceResolver resolver, DiagnosticBag? diagnostics = null)
    {
        if (!resolver.TryResolve(entryGrammarName, out var entrySource))
        {
            AddMissingGrammarDiagnostic(entryGrammarName, diagnostics);
            throw new GrammarValidationException($"Unable to resolve grammar '{entryGrammarName}'.");
        }

        var state = new CompilationState(resolver, diagnostics);
        var graph = state.LoadDependencies(entrySource.Name, DependencyResolutionMode.FullImport);
        return BuildMergedDefinition(graph, entrySource.Name, diagnostics);
    }

    /// <summary>
    /// Compiles an entry grammar and all its dependencies into a runnable instance.
    /// </summary>
    /// <param name="entryGrammarName">Entry grammar logical name.</param>
    /// <param name="resolver">Source resolver used to locate grammar files.</param>
    /// <param name="diagnostics">Optional diagnostics collection.</param>
    /// <returns>Compiled grammar instance.</returns>
    public static CompiledGrammar Compile(string entryGrammarName, IGrammarSourceResolver resolver, DiagnosticBag? diagnostics = null)
    {
        return new CompiledGrammar(Parse(entryGrammarName, resolver, diagnostics));
    }

    /// <summary>
    /// Parses and resolves a grammar project from an entry <c>.g4</c> file.
    /// </summary>
    /// <param name="entryFilePath">Entry grammar file path.</param>
    /// <param name="diagnostics">Optional diagnostics collection.</param>
    /// <returns>Resolved merged parser definition.</returns>
    public static ParserDefinition ParseFromFile(string entryFilePath, DiagnosticBag? diagnostics = null)
    {
        var rootDirectory = Path.GetDirectoryName(Path.GetFullPath(entryFilePath)) ?? Directory.GetCurrentDirectory();
        var resolver = new FileSystemGrammarSourceResolver(rootDirectory);
        return Parse(Path.GetFileNameWithoutExtension(entryFilePath), resolver, diagnostics);
    }

    /// <summary>
    /// Compiles a grammar project from an entry <c>.g4</c> file.
    /// </summary>
    /// <param name="entryFilePath">Entry grammar file path.</param>
    /// <param name="diagnostics">Optional diagnostics collection.</param>
    /// <returns>Compiled grammar instance.</returns>
    public static CompiledGrammar CompileFromFile(string entryFilePath, DiagnosticBag? diagnostics = null)
    {
        return new CompiledGrammar(ParseFromFile(entryFilePath, diagnostics));
    }

    /// <summary>
    /// Builds a merged parser definition from loaded grammar definitions.
    /// </summary>
    /// <param name="graph">Loaded grammar graph.</param>
    /// <param name="entryGrammarName">Entry grammar name.</param>
    /// <param name="diagnostics">Optional diagnostics collection.</param>
    /// <returns>Merged and resolved parser definition.</returns>
    private static ParserDefinition BuildMergedDefinition(IReadOnlyDictionary<string, LoadedGrammarDefinition> graph, string entryGrammarName, DiagnosticBag? diagnostics)
    {
        var entry = graph[entryGrammarName].Definition;

        var modes = new List<LexerMode>();
        var parserRules = new List<Rule>();
        var existingRuleNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var mode in entry.Modes)
        {
            modes.Add(new LexerMode(mode.Name, mode.Rules.ToList()));
            foreach (var rule in mode.Rules)
            {
                existingRuleNames.Add(rule.Name);
            }
        }

        foreach (var rule in entry.ParserRules)
        {
            parserRules.Add(rule);
            existingRuleNames.Add(rule.Name);
        }

        foreach (var loadedDefinition in graph.Values)
        {
            var definition = loadedDefinition.Definition;
            if (ReferenceEquals(definition, entry))
            {
                continue;
            }

            MergeModes(modes, definition.Modes, existingRuleNames, diagnostics, entry.Name);
            if (loadedDefinition.IncludeParserRules)
            {
                MergeParserRules(parserRules, definition.ParserRules, existingRuleNames, diagnostics, entry.Name);
            }
        }

        ReorderRules(modes, parserRules);

        var mergedDefinition = entry with
        {
            Modes = modes,
            ParserRules = parserRules,
            RootRule = entry.RootRule,
            AllowExternalLexerRules = true
        };

        return RuleResolver.Resolve(mergedDefinition, diagnostics);
    }

    /// <summary>
    /// Merges lexer modes from an imported grammar into the accumulated mode list.
    /// </summary>
    /// <param name="targetModes">Target mode collection.</param>
    /// <param name="sourceModes">Source mode collection.</param>
    /// <param name="existingRuleNames">Known rule names.</param>
    /// <param name="diagnostics">Optional diagnostics collection.</param>
    /// <param name="entryGrammarName">Entry grammar name.</param>
    private static void MergeModes(
        List<LexerMode> targetModes,
        IReadOnlyList<LexerMode> sourceModes,
        HashSet<string> existingRuleNames,
        DiagnosticBag? diagnostics,
        string entryGrammarName)
    {
        foreach (var sourceMode in sourceModes)
        {
            var existingMode = targetModes.FirstOrDefault(mode => string.Equals(mode.Name, sourceMode.Name, StringComparison.Ordinal));
            if (existingMode is null)
            {
                var newRules = new List<Rule>();
                foreach (var sourceRule in sourceMode.Rules)
                {
                    if (TryAddRule(sourceRule, existingRuleNames, diagnostics, entryGrammarName))
                    {
                        newRules.Add(sourceRule);
                    }
                }

                targetModes.Add(new LexerMode(sourceMode.Name, newRules));
                continue;
            }

            var mergedRules = existingMode.Rules.ToList();
            foreach (var sourceRule in sourceMode.Rules)
            {
                if (TryAddRule(sourceRule, existingRuleNames, diagnostics, entryGrammarName))
                {
                    mergedRules.Add(sourceRule);
                }
            }

            targetModes[targetModes.IndexOf(existingMode)] = existingMode with { Rules = mergedRules };
        }
    }

    /// <summary>
    /// Merges parser rules from imported grammars into the entry parser rule list.
    /// </summary>
    /// <param name="targetRules">Target parser rule collection.</param>
    /// <param name="sourceRules">Source parser rule collection.</param>
    /// <param name="existingRuleNames">Known rule names.</param>
    /// <param name="diagnostics">Optional diagnostics collection.</param>
    /// <param name="entryGrammarName">Entry grammar name.</param>
    private static void MergeParserRules(
        List<Rule> targetRules,
        IReadOnlyList<Rule> sourceRules,
        HashSet<string> existingRuleNames,
        DiagnosticBag? diagnostics,
        string entryGrammarName)
    {
        foreach (var sourceRule in sourceRules)
        {
            if (TryAddRule(sourceRule, existingRuleNames, diagnostics, entryGrammarName))
            {
                targetRules.Add(sourceRule);
            }
        }
    }

    /// <summary>
    /// Attempts to add a rule name to the known-name set and emits a diagnostic on duplicates.
    /// </summary>
    /// <param name="rule">Rule to register.</param>
    /// <param name="existingRuleNames">Known rule names.</param>
    /// <param name="diagnostics">Optional diagnostics collection.</param>
    /// <param name="entryGrammarName">Entry grammar name.</param>
    /// <returns><c>true</c> when the rule is new and can be merged.</returns>
    private static bool TryAddRule(Rule rule, HashSet<string> existingRuleNames, DiagnosticBag? diagnostics, string entryGrammarName)
    {
        if (existingRuleNames.Add(rule.Name))
        {
            return true;
        }

        diagnostics?.AddWithContext(ParserDiagnostics.ImportedRuleIgnoredBecauseAlreadyDefined, null, null, entryGrammarName, null, rule.Name);
        return false;
    }

    /// <summary>
    /// Reassigns declaration order sequentially for all merged rules.
    /// </summary>
    /// <param name="modes">Lexer modes.</param>
    /// <param name="parserRules">Parser rules.</param>
    private static void ReorderRules(List<LexerMode> modes, List<Rule> parserRules)
    {
        var order = 0;

        for (var modeIndex = 0; modeIndex < modes.Count; modeIndex++)
        {
            var rules = new List<Rule>();
            foreach (var rule in modes[modeIndex].Rules)
            {
                rules.Add(rule with { DeclarationOrder = order++ });
            }

            modes[modeIndex] = modes[modeIndex] with { Rules = rules };
        }

        for (var parserRuleIndex = 0; parserRuleIndex < parserRules.Count; parserRuleIndex++)
        {
            parserRules[parserRuleIndex] = parserRules[parserRuleIndex] with { DeclarationOrder = order++ };
        }
    }

    /// <summary>
    /// Adds a missing-grammar diagnostic.
    /// </summary>
    /// <param name="grammarName">Unresolved grammar name.</param>
    /// <param name="diagnostics">Optional diagnostics collection.</param>
    private static void AddMissingGrammarDiagnostic(string grammarName, DiagnosticBag? diagnostics)
    {
        diagnostics?.Add(ParserDiagnostics.ImportedGrammarNotFound, grammarName);
    }

    /// <summary>
    /// Describes how dependencies should be loaded for a reference.
    /// </summary>
    private enum DependencyResolutionMode
    {
        /// <summary>Loads full imports and token vocabularies.</summary>
        FullImport,

        /// <summary>Loads only lexer-related dependencies.</summary>
        TokenVocabOnly,
    }

    /// <summary>
    /// Holds mutable state while loading a project grammar graph.
    /// </summary>
    private sealed class CompilationState
    {
        private readonly IGrammarSourceResolver _resolver;
        private readonly DiagnosticBag? _diagnostics;
        private readonly Dictionary<string, LoadedGrammarDefinition> _definitionsByName = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _stack = [];

        /// <summary>
        /// Initialises a compilation state.
        /// </summary>
        /// <param name="resolver">Source resolver.</param>
        /// <param name="diagnostics">Optional diagnostics bag.</param>
        public CompilationState(IGrammarSourceResolver resolver, DiagnosticBag? diagnostics)
        {
            _resolver = resolver;
            _diagnostics = diagnostics;
        }

        /// <summary>
        /// Loads grammar definitions starting from an entry grammar.
        /// </summary>
        /// <param name="grammarName">Entry grammar name.</param>
        /// <param name="resolutionMode">Dependency mode for the entry.</param>
        /// <returns>Loaded grammar definitions keyed by grammar name.</returns>
        public IReadOnlyDictionary<string, LoadedGrammarDefinition> LoadDependencies(string grammarName, DependencyResolutionMode resolutionMode)
        {
            Visit(grammarName, resolutionMode);
            return _definitionsByName;
        }

        /// <summary>
        /// Visits a grammar and recursively loads dependencies.
        /// </summary>
        /// <param name="grammarName">Grammar name to visit.</param>
        /// <param name="resolutionMode">Dependency mode for the current edge.</param>
        private void Visit(string grammarName, DependencyResolutionMode resolutionMode)
        {
            if (_stack.Contains(grammarName, StringComparer.OrdinalIgnoreCase))
            {
                var cycleStartIndex = _stack.FindIndex(name => string.Equals(name, grammarName, StringComparison.OrdinalIgnoreCase));
                var cycle = _stack.Skip(cycleStartIndex).Concat([grammarName]).ToList();
                var cycleText = string.Join(" -> ", cycle);
                _diagnostics?.Add(ParserDiagnostics.ImportCycleDetected, cycleText);
                throw new GrammarValidationException($"Import cycle detected: {cycleText}");
            }

            if (_definitionsByName.ContainsKey(grammarName))
            {
                if (resolutionMode == DependencyResolutionMode.FullImport)
                {
                    var existing = _definitionsByName[grammarName];
                    _definitionsByName[grammarName] = existing with { IncludeParserRules = true };
                }
                return;
            }

            if (!_resolver.TryResolve(grammarName, out var source))
            {
                AddMissingGrammarDiagnostic(grammarName, _diagnostics);
                throw new GrammarValidationException($"Unable to resolve grammar '{grammarName}'.");
            }

            _stack.Add(grammarName);
            var definition = Antlr4GrammarConverter.ParseUnresolved(source.Text, _diagnostics);
            _definitionsByName[definition.Name] = new LoadedGrammarDefinition(
                definition,
                resolutionMode == DependencyResolutionMode.FullImport);

            if (resolutionMode == DependencyResolutionMode.FullImport)
            {
                foreach (var import in definition.Imports)
                {
                    Visit(import.GrammarName, DependencyResolutionMode.FullImport);
                }
            }

            var tokenVocab = TryGetTokenVocab(definition);
            if (!string.IsNullOrWhiteSpace(tokenVocab))
            {
                Visit(tokenVocab!, DependencyResolutionMode.TokenVocabOnly);
            }

            _stack.RemoveAt(_stack.Count - 1);
        }

        /// <summary>
        /// Extracts the <c>tokenVocab</c> option from an unresolved grammar definition.
        /// </summary>
        /// <param name="definition">Grammar definition.</param>
        /// <returns>Resolved token vocabulary value or <c>null</c>.</returns>
        private static string? TryGetTokenVocab(ParserDefinition definition)
        {
            return definition.Options?.Values.TryGetValue("tokenVocab", out var tokenVocab) == true
                ? tokenVocab
                : null;
        }
    }

    /// <summary>
    /// Represents one loaded grammar with parser-rule import policy.
    /// </summary>
    /// <param name="Definition">Resolved grammar definition.</param>
    /// <param name="IncludeParserRules"><c>true</c> when parser rules must be merged.</param>
    private sealed record LoadedGrammarDefinition(ParserDefinition Definition, bool IncludeParserRules);
}
