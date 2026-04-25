using System;
using System.Collections.Generic;
using System.IO;

namespace Utils.Parser.ProjectCompilation;

/// <summary>
/// Resolves grammar sources from an in-memory collection.
/// </summary>
public sealed class InMemoryGrammarSourceResolver : IGrammarSourceResolver
{
    private readonly Dictionary<string, GrammarSource> _sources;

    /// <summary>
    /// Initialises a new resolver from preloaded grammar sources.
    /// </summary>
    /// <param name="sources">Grammar source collection.</param>
    public InMemoryGrammarSourceResolver(IEnumerable<GrammarSource> sources)
    {
        _sources = new Dictionary<string, GrammarSource>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            _sources[source.Name] = source;

            if (!string.IsNullOrWhiteSpace(source.Path))
            {
                var fileName = Path.GetFileNameWithoutExtension(source.Path);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    _sources[fileName] = source;
                }
            }
        }
    }

    /// <inheritdoc />
    public bool TryResolve(string grammarName, out GrammarSource source)
    {
        return _sources.TryGetValue(grammarName, out source!);
    }
}
