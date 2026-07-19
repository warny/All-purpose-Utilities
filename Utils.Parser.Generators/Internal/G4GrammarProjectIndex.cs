using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Utils.Parser.Generators.Internal;

/// <summary>
/// Provides deterministic, Roslyn-free lookup for parsed project grammars by their declared grammar name.
/// </summary>
internal sealed class G4GrammarProjectIndex
{
    private readonly ImmutableDictionary<string, ImmutableArray<G4GrammarProjectEntry>> _entriesByGrammarName;

    /// <summary>Initializes a new project grammar index.</summary>
    /// <param name="entries">Parsed grammar entries available to the current generator project.</param>
    public G4GrammarProjectIndex(IEnumerable<G4GrammarProjectEntry> entries)
    {
        _entriesByGrammarName = entries
            .OrderBy(static entry => entry.Grammar.Name, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Path, StringComparer.Ordinal)
            .GroupBy(static entry => entry.Grammar.Name, StringComparer.Ordinal)
            .ToImmutableDictionary(
                static group => group.Key,
                static group => group.ToImmutableArray(),
                StringComparer.Ordinal);
    }

    /// <summary>Resolves a grammar by declared name only when exactly one project entry declares that name.</summary>
    /// <param name="grammarName">Declared grammar name to resolve.</param>
    /// <returns>The grammar-name resolution result.</returns>
    internal G4GrammarNameResolution ResolveGrammar(string grammarName)
    {
        if (!_entriesByGrammarName.TryGetValue(grammarName, out var entries))
        {
            return G4GrammarNameResolution.Unresolved(grammarName);
        }

        return entries.Length == 1
            ? G4GrammarNameResolution.Resolved(entries[0])
            : G4GrammarNameResolution.Ambiguous(grammarName, entries);
    }
}

/// <summary>Represents one parsed grammar entry available to the project resolver.</summary>
/// <param name="Path">Additional file path used only for deterministic tie-breaking and diagnostics.</param>
/// <param name="Grammar">Parsed grammar AST for the file.</param>
internal readonly record struct G4GrammarProjectEntry(string Path, G4Grammar Grammar);

/// <summary>Describes the result of resolving a declared grammar name in the project index.</summary>
internal readonly record struct G4GrammarNameResolution
{
    /// <summary>Initializes a new grammar-name resolution result.</summary>
    private G4GrammarNameResolution(G4GrammarNameResolutionKind kind, string grammarName, G4GrammarProjectEntry? entry, ImmutableArray<G4GrammarProjectEntry> candidates)
    {
        Kind = kind;
        GrammarName = grammarName;
        Entry = entry;
        Candidates = candidates;
    }

    /// <summary>Gets the resolution state.</summary>
    internal G4GrammarNameResolutionKind Kind { get; }

    /// <summary>Gets the requested grammar name.</summary>
    internal string GrammarName { get; }

    /// <summary>Gets the resolved entry when <see cref="Kind"/> is <see cref="G4GrammarNameResolutionKind.Resolved"/>.</summary>
    internal G4GrammarProjectEntry? Entry { get; }

    /// <summary>Gets ambiguous candidates in deterministic order.</summary>
    internal ImmutableArray<G4GrammarProjectEntry> Candidates { get; }

    /// <summary>Creates a resolved grammar-name result.</summary>
    internal static G4GrammarNameResolution Resolved(G4GrammarProjectEntry entry) => new(G4GrammarNameResolutionKind.Resolved, entry.Grammar.Name, entry, ImmutableArray<G4GrammarProjectEntry>.Empty);

    /// <summary>Creates an unresolved grammar-name result.</summary>
    internal static G4GrammarNameResolution Unresolved(string grammarName) => new(G4GrammarNameResolutionKind.Unresolved, grammarName, null, ImmutableArray<G4GrammarProjectEntry>.Empty);

    /// <summary>Creates an ambiguous grammar-name result.</summary>
    internal static G4GrammarNameResolution Ambiguous(string grammarName, ImmutableArray<G4GrammarProjectEntry> candidates) => new(G4GrammarNameResolutionKind.Ambiguous, grammarName, null, candidates);
}

/// <summary>Identifies whether a declared grammar name resolved uniquely in the project index.</summary>
internal enum G4GrammarNameResolutionKind
{
    /// <summary>The grammar name was found exactly once.</summary>
    Resolved,

    /// <summary>The grammar name was not available in the current project AdditionalFiles.</summary>
    Unresolved,

    /// <summary>More than one project grammar declared the same grammar name.</summary>
    Ambiguous
}
