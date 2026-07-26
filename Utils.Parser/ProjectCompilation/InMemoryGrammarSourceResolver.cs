using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Utils.Parser.ProjectCompilation;

/// <summary>
/// Resolves grammar sources from an in-memory collection.
/// </summary>
public sealed class InMemoryGrammarSourceResolver : IGrammarSourceResolver, IGrammarSourceCandidateResolver
{
    /// <summary>Grammar sources indexed by name and by file base name for case-insensitive resolution.</summary>
    private readonly Dictionary<string, GrammarSource> _sources;
    /// <summary>Grammar sources grouped by declared or file name without discarding ambiguous candidates.</summary>
    private readonly Dictionary<string, List<GrammarSource>> _candidates;

    /// <summary>
    /// Initialises a new resolver from preloaded grammar sources.
    /// </summary>
    /// <param name="sources">Grammar source collection.</param>
    public InMemoryGrammarSourceResolver(IEnumerable<GrammarSource> sources)
    {
        _sources = new Dictionary<string, GrammarSource>(StringComparer.OrdinalIgnoreCase);
        _candidates = new Dictionary<string, List<GrammarSource>>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            _sources[source.Name] = source;
            AddCandidate(source.Name, source);

            if (!string.IsNullOrWhiteSpace(source.Path))
            {
                var fileName = Path.GetFileNameWithoutExtension(source.Path);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    _sources[fileName] = source;
                    AddCandidate(fileName, source);
                }
            }
        }
    }

    /// <inheritdoc />
    public bool TryResolve(string grammarName, out GrammarSource source)
    {
        return _sources.TryGetValue(grammarName, out source!);
    }

    /// <inheritdoc />
    IReadOnlyList<GrammarSource> IGrammarSourceCandidateResolver.ResolveCandidates(string grammarName) =>
        _candidates.TryGetValue(grammarName, out List<GrammarSource>? sources)
            ? sources.Distinct().OrderBy(source => source.Path ?? source.Name, StringComparer.Ordinal).ToArray()
            : [];

    /// <summary>Adds one candidate under a lookup name.</summary>
    private void AddCandidate(string name, GrammarSource source)
    {
        if (!_candidates.TryGetValue(name, out List<GrammarSource>? candidates))
        {
            candidates = [];
            _candidates[name] = candidates;
        }
        candidates.Add(source);
    }
}
