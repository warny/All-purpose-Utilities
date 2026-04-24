namespace Utils.Parser.ProjectCompilation;

/// <summary>
/// Resolves grammar sources by logical grammar name.
/// </summary>
public interface IGrammarSourceResolver
{
    /// <summary>
    /// Attempts to resolve a grammar source.
    /// </summary>
    /// <param name="grammarName">Grammar name to resolve.</param>
    /// <param name="source">Resolved source when found.</param>
    /// <returns><c>true</c> when a source is found; otherwise <c>false</c>.</returns>
    bool TryResolve(string grammarName, out GrammarSource source);
}
