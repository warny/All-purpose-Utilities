using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Utils.Parser.Antlr4.Common.Composition;

/// <summary>Identifies a grammar by its declared name and stable logical source identifier.</summary>
internal readonly record struct GrammarIdentity(string DeclaredName, string SourceId);

/// <summary>Identifies the visibility supplied by a grammar dependency.</summary>
internal enum GrammarDependencyKind
{
    /// <summary>Imports parser and lexer declarations.</summary>
    FullImport,

    /// <summary>Imports lexer declarations only.</summary>
    TokenVocab
}

/// <summary>Identifies the grammar domain in which a rule is declared.</summary>
internal enum GrammarRuleDomain
{
    /// <summary>A parser rule.</summary>
    Parser,

    /// <summary>A lexer rule.</summary>
    Lexer
}

/// <summary>Describes one ordered dependency declaration.</summary>
internal sealed record GrammarDependency(string GrammarName, string? Alias, GrammarDependencyKind Kind, object? Payload);

/// <summary>Describes one source rule without converting its consumer-specific payload.</summary>
internal sealed record GrammarRuleDescriptor(string Name, GrammarRuleDomain Domain, string? LexerMode, object Payload);

/// <summary>Provides the grammar data required by composition planning.</summary>
internal interface IGrammarCompositionSource
{
    /// <summary>Gets the stable source identity.</summary>
    GrammarIdentity Identity { get; }

    /// <summary>Gets dependencies in declaration order.</summary>
    IReadOnlyList<GrammarDependency> Dependencies { get; }

    /// <summary>Gets rules in declaration order.</summary>
    IReadOnlyList<GrammarRuleDescriptor> Rules { get; }

    /// <summary>Gets the consumer-specific grammar payload.</summary>
    object Payload { get; }

    /// <summary>Gets the local parser root payload, when present.</summary>
    object? RootRulePayload { get; }
}

/// <summary>Describes one dependency edge, its declared and effective kinds, and its import path.</summary>
internal sealed record GrammarDependencyEdge
{
    /// <summary>Initializes a dependency edge and captures its ordered import path.</summary>
    internal GrammarDependencyEdge(GrammarIdentity importer, GrammarDependency declaredDependency, GrammarDependencyKind effectiveKind, GrammarIdentity? imported, IReadOnlyList<GrammarIdentity> importPath) { Importer = importer; DeclaredDependency = declaredDependency; EffectiveKind = effectiveKind; Imported = imported; ImportPath = importPath.ToImmutableArray(); }
    internal GrammarIdentity Importer { get; }
    internal GrammarDependency DeclaredDependency { get; }
    internal GrammarDependencyKind EffectiveKind { get; }
    internal GrammarIdentity? Imported { get; init; }
    internal IReadOnlyList<GrammarIdentity> ImportPath { get; }
}

/// <summary>Describes an unresolved dependency.</summary>
internal sealed record MissingGrammarDependency(GrammarDependencyEdge Edge);

/// <summary>Describes a dependency that resolved to multiple stable sources.</summary>
internal sealed record AmbiguousGrammarDependency
{
    /// <summary>Initializes an ambiguous dependency and captures its ordered candidates.</summary>
    internal AmbiguousGrammarDependency(GrammarDependencyEdge edge, IReadOnlyList<GrammarIdentity> candidates) { Edge = edge; Candidates = candidates.ToImmutableArray(); }
    internal GrammarDependencyEdge Edge { get; }
    internal IReadOnlyList<GrammarIdentity> Candidates { get; }
}

/// <summary>Describes a deterministic cycle path.</summary>
internal sealed record GrammarImportCycle
{
    /// <summary>Initializes a cycle and captures its ordered diagnostic path.</summary>
    internal GrammarImportCycle(IReadOnlyList<GrammarIdentity> path) => Path = path.ToImmutableArray();
    internal IReadOnlyList<GrammarIdentity> Path { get; }
}

/// <summary>Describes an effective rule and its provenance.</summary>
internal sealed record EffectiveGrammarRule
{
    /// <summary>Initializes an effective rule and captures its ordered provenance path.</summary>
    internal EffectiveGrammarRule(GrammarIdentity origin, GrammarRuleDescriptor rule, IReadOnlyList<GrammarIdentity> importPath, GrammarDependencyKind? introducedBy) { Origin = origin; Rule = rule; ImportPath = importPath.ToImmutableArray(); IntroducedBy = introducedBy; }
    internal GrammarIdentity Origin { get; }
    internal GrammarRuleDescriptor Rule { get; }
    internal IReadOnlyList<GrammarIdentity> ImportPath { get; }
    internal GrammarDependencyKind? IntroducedBy { get; }
}

/// <summary>Describes a rule hidden by a local declaration.</summary>
internal sealed record MaskedGrammarRule(EffectiveGrammarRule Rule, EffectiveGrammarRule MaskedBy);

/// <summary>Describes distinct imported declarations competing for one unqualified rule name.</summary>
internal sealed record GrammarRuleCollision
{
    /// <summary>Initializes a rule collision and captures its ordered candidates.</summary>
    internal GrammarRuleCollision(string ruleName, IReadOnlyList<EffectiveGrammarRule> candidates) { RuleName = ruleName; Candidates = candidates.ToImmutableArray(); }
    internal string RuleName { get; }
    internal IReadOnlyList<EffectiveGrammarRule> Candidates { get; }
}

/// <summary>Contains the immutable result of dependency graph construction and effective-rule selection.</summary>
internal sealed record GrammarImportCompositionPlan
{
    /// <summary>Initializes the immutable result of grammar import composition.</summary>
    internal GrammarImportCompositionPlan(IGrammarCompositionSource entry, IReadOnlyList<IGrammarCompositionSource> grammars, IReadOnlyList<GrammarDependencyEdge> dependencies, IReadOnlyList<GrammarImportCycle> cycles, IReadOnlyList<MissingGrammarDependency> missingDependencies, IReadOnlyList<AmbiguousGrammarDependency> ambiguousDependencies, IReadOnlyList<EffectiveGrammarRule> effectiveRules, IReadOnlyList<MaskedGrammarRule> maskedRules, IReadOnlyList<EffectiveGrammarRule> ignoredRules, IReadOnlyList<GrammarRuleCollision> collisions, object? rootRulePayload, IReadOnlyList<EffectiveGrammarRule> tokenVocabLexerRules) { Entry = entry; Grammars = grammars.ToImmutableArray(); Dependencies = dependencies.ToImmutableArray(); Cycles = cycles.ToImmutableArray(); MissingDependencies = missingDependencies.ToImmutableArray(); AmbiguousDependencies = ambiguousDependencies.ToImmutableArray(); EffectiveRules = effectiveRules.ToImmutableArray(); MaskedRules = maskedRules.ToImmutableArray(); IgnoredRules = ignoredRules.ToImmutableArray(); Collisions = collisions.ToImmutableArray(); RootRulePayload = rootRulePayload; TokenVocabLexerRules = tokenVocabLexerRules.ToImmutableArray(); }
    internal IGrammarCompositionSource Entry { get; }
    internal IReadOnlyList<IGrammarCompositionSource> Grammars { get; }
    internal IReadOnlyList<GrammarDependencyEdge> Dependencies { get; }
    internal IReadOnlyList<GrammarImportCycle> Cycles { get; }
    internal IReadOnlyList<MissingGrammarDependency> MissingDependencies { get; }
    internal IReadOnlyList<AmbiguousGrammarDependency> AmbiguousDependencies { get; }
    internal IReadOnlyList<EffectiveGrammarRule> EffectiveRules { get; }
    internal IReadOnlyList<MaskedGrammarRule> MaskedRules { get; }
    internal IReadOnlyList<EffectiveGrammarRule> IgnoredRules { get; }
    internal IReadOnlyList<GrammarRuleCollision> Collisions { get; }
    internal object? RootRulePayload { get; }
    internal IReadOnlyList<EffectiveGrammarRule> TokenVocabLexerRules { get; }
}

/// <summary>Builds a deterministic, consumer-neutral grammar import composition plan.</summary>
internal static class GrammarImportCompositionPlanner
{
    /// <summary>Builds a plan without mutating source grammars or their rule payloads.</summary>
    /// <param name="entry">Entry grammar.</param>
    /// <param name="resolve">Resolves a declared grammar name to zero, one, or several candidates.</param>
    /// <returns>The complete composition plan.</returns>
    internal static GrammarImportCompositionPlan Build(
        IGrammarCompositionSource entry,
        Func<string, IReadOnlyList<IGrammarCompositionSource>> resolve)
    {
        var state = new PlanningState(entry, resolve);
        state.Visit(entry, null, [entry.Identity]);
        return state.CreatePlan();
    }

    /// <summary>Owns mutable state used only while constructing one immutable plan.</summary>
    private sealed class PlanningState
    {
        private readonly IGrammarCompositionSource _entry;
        private readonly Func<string, IReadOnlyList<IGrammarCompositionSource>> _resolve;
        private readonly List<IGrammarCompositionSource> _grammars = [];
        private readonly List<GrammarDependencyEdge> _edges = [];
        private readonly List<GrammarImportCycle> _cycles = [];
        private readonly List<MissingGrammarDependency> _missing = [];
        private readonly List<AmbiguousGrammarDependency> _ambiguous = [];
        private readonly Dictionary<GrammarIdentity, Visibility> _visibility = [];
        private readonly Dictionary<GrammarIdentity, Visibility> _expanded = [];
        private readonly Dictionary<GrammarIdentity, VisibilityPaths> _paths = [];

        /// <summary>Initializes planning state.</summary>
        internal PlanningState(IGrammarCompositionSource entry, Func<string, IReadOnlyList<IGrammarCompositionSource>> resolve)
        {
            _entry = entry;
            _resolve = resolve;
        }

        /// <summary>Visits one source using dependency declaration order and depth-first traversal.</summary>
        internal void Visit(IGrammarCompositionSource source, GrammarDependencyKind? introducedBy, IReadOnlyList<GrammarIdentity> path)
        {
            Visibility requested = introducedBy == GrammarDependencyKind.TokenVocab ? Visibility.LexerOnly : Visibility.Full;
            RecordPath(source.Identity, requested, path);
            if (!_visibility.TryGetValue(source.Identity, out Visibility existing))
            {
                _visibility[source.Identity] = requested;
                _grammars.Add(source);
            }
            else if (requested > existing)
            {
                _visibility[source.Identity] = requested;
            }

            if (_expanded.TryGetValue(source.Identity, out Visibility expanded) && expanded >= requested)
            {
                return;
            }
            _expanded[source.Identity] = requested;

            foreach (GrammarDependency dependency in source.Dependencies)
            {
                GrammarDependencyKind effectiveKind = requested == Visibility.LexerOnly
                    ? GrammarDependencyKind.TokenVocab
                    : dependency.Kind;
                IReadOnlyList<IGrammarCompositionSource> candidates = _resolve(dependency.GrammarName)
                    .OrderBy(candidate => candidate.Identity.SourceId, StringComparer.Ordinal)
                    .ThenBy(candidate => candidate.Identity.DeclaredName, StringComparer.Ordinal)
                    .ToArray();
                var unresolvedEdge = new GrammarDependencyEdge(source.Identity, dependency, effectiveKind, null, path.ToArray());
                if (candidates.Count == 0)
                {
                    _edges.Add(unresolvedEdge);
                    _missing.Add(new MissingGrammarDependency(unresolvedEdge));
                    continue;
                }

                if (candidates.Count > 1)
                {
                    _edges.Add(unresolvedEdge);
                    _ambiguous.Add(new AmbiguousGrammarDependency(unresolvedEdge, candidates.Select(candidate => candidate.Identity).ToArray()));
                    continue;
                }

                IGrammarCompositionSource imported = candidates[0];
                var edge = unresolvedEdge with { Imported = imported.Identity };
                _edges.Add(edge);
                int cycleStart = IndexOf(path, imported.Identity);
                if (cycleStart >= 0)
                {
                    _cycles.Add(new GrammarImportCycle(path.Skip(cycleStart).Concat([imported.Identity]).ToArray()));
                    continue;
                }

                Visit(imported, effectiveKind, path.Concat([imported.Identity]).ToArray());
            }
        }

        /// <summary>Creates the final rule selection after graph traversal is complete.</summary>
        internal GrammarImportCompositionPlan CreatePlan()
        {
            var candidates = new List<EffectiveGrammarRule>();
            foreach (IGrammarCompositionSource grammar in _grammars)
            {
                Visibility visibility = _visibility[grammar.Identity];
                GrammarDependencyKind? introducedBy = grammar.Identity.Equals(_entry.Identity)
                    ? null
                    : visibility == Visibility.Full ? GrammarDependencyKind.FullImport : GrammarDependencyKind.TokenVocab;
                IReadOnlyList<GrammarIdentity> path = GetPath(grammar.Identity, visibility);
                foreach (GrammarRuleDescriptor rule in grammar.Rules)
                {
                    if (visibility == Visibility.LexerOnly && rule.Domain == GrammarRuleDomain.Parser)
                    {
                        continue;
                    }

                    candidates.Add(new EffectiveGrammarRule(grammar.Identity, rule, path, introducedBy));
                }
            }

            EffectiveGrammarRule[] local = candidates.Where(rule => rule.Origin.Equals(_entry.Identity)).ToArray();
            var localByName = local.GroupBy(rule => rule.Rule.Name, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var effective = new List<EffectiveGrammarRule>(local);
            var masked = new List<MaskedGrammarRule>();
            var ignored = new List<EffectiveGrammarRule>();
            var collisions = new List<GrammarRuleCollision>();

            foreach (IGrouping<string, EffectiveGrammarRule> group in candidates.Where(rule => !rule.Origin.Equals(_entry.Identity)).GroupBy(rule => rule.Rule.Name, StringComparer.Ordinal))
            {
                EffectiveGrammarRule[] distinct = group.GroupBy(rule => (rule.Origin, rule.Rule.Domain, rule.Rule.LexerMode), RuleIdentityComparer.Instance).Select(item => item.First()).ToArray();
                if (localByName.TryGetValue(group.Key, out EffectiveGrammarRule? localRule))
                {
                    foreach (EffectiveGrammarRule rule in distinct)
                    {
                        masked.Add(new MaskedGrammarRule(rule, localRule));
                        ignored.Add(rule);
                    }
                }
                else if (distinct.Length == 1)
                {
                    effective.Add(distinct[0]);
                }
                else
                {
                    collisions.Add(new GrammarRuleCollision(group.Key, distinct));
                    effective.Add(distinct[0]);
                    ignored.AddRange(distinct.Skip(1));
                }
            }

            return new GrammarImportCompositionPlan(
                _entry,
                _grammars.ToArray(),
                _edges.ToArray(),
                _cycles.ToArray(),
                _missing.ToArray(),
                _ambiguous.ToArray(),
                effective.ToArray(),
                masked.ToArray(),
                ignored.ToArray(),
                collisions.ToArray(),
                _entry.RootRulePayload,
                effective.Where(rule => rule.IntroducedBy == GrammarDependencyKind.TokenVocab && rule.Rule.Domain == GrammarRuleDomain.Lexer).ToArray());
        }

        /// <summary>Records the first deterministic path that establishes a requested visibility.</summary>
        private void RecordPath(GrammarIdentity identity, Visibility visibility, IReadOnlyList<GrammarIdentity> path)
        {
            if (!_paths.TryGetValue(identity, out VisibilityPaths? paths))
            {
                paths = new VisibilityPaths();
                _paths[identity] = paths;
            }

            if (visibility == Visibility.Full)
            {
                paths.FullPath ??= path.ToArray();
            }
            else
            {
                paths.LexerOnlyPath ??= path.ToArray();
            }
        }

        /// <summary>Gets a path that actually establishes the grammar's selected effective visibility.</summary>
        private IReadOnlyList<GrammarIdentity> GetPath(GrammarIdentity identity, Visibility visibility)
        {
            VisibilityPaths paths = _paths[identity];
            return visibility == Visibility.Full
                ? paths.FullPath ?? throw new InvalidOperationException($"No full-import path was recorded for grammar '{identity.DeclaredName}'.")
                : paths.LexerOnlyPath ?? throw new InvalidOperationException($"No token-vocabulary path was recorded for grammar '{identity.DeclaredName}'.");
        }

        /// <summary>Finds an identity in a path using explicit ordinal identity semantics.</summary>
        private static int IndexOf(IReadOnlyList<GrammarIdentity> path, GrammarIdentity identity)
        {
            for (int index = 0; index < path.Count; index++)
            {
                if (path[index].Equals(identity))
                {
                    return index;
                }
            }
            return -1;
        }

        /// <summary>Tracks whether a grammar contributes lexer-only or full declarations.</summary>
        private enum Visibility
        {
            LexerOnly,
            Full
        }

        /// <summary>Stores deterministic provenance paths independently for lexer-only and full visibility.</summary>
        private sealed class VisibilityPaths
        {
            /// <summary>Gets or sets the first path that established lexer-only visibility.</summary>
            internal GrammarIdentity[]? LexerOnlyPath { get; set; }

            /// <summary>Gets or sets the first path that established full visibility.</summary>
            internal GrammarIdentity[]? FullPath { get; set; }
        }

        /// <summary>Compares structural rule identities without using payload object identity.</summary>
        private sealed class RuleIdentityComparer : IEqualityComparer<(GrammarIdentity Origin, GrammarRuleDomain Domain, string? LexerMode)>
        {
            internal static RuleIdentityComparer Instance { get; } = new();

            /// <inheritdoc />
            public bool Equals((GrammarIdentity Origin, GrammarRuleDomain Domain, string? LexerMode) x, (GrammarIdentity Origin, GrammarRuleDomain Domain, string? LexerMode) y) =>
                x.Origin.Equals(y.Origin) && x.Domain == y.Domain && string.Equals(x.LexerMode, y.LexerMode, StringComparison.Ordinal);

            /// <inheritdoc />
            public int GetHashCode((GrammarIdentity Origin, GrammarRuleDomain Domain, string? LexerMode) obj) =>
                (obj.Origin.GetHashCode() * 397) ^ ((int)obj.Domain * 31) ^ (obj.LexerMode is null ? 0 : StringComparer.Ordinal.GetHashCode(obj.LexerMode));
        }
    }
}
