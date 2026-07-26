using System.Collections.Generic;

namespace Utils.Parser.ProjectCompilation;

/// <summary>Exposes all candidates when a grammar name is ambiguous.</summary>
internal interface IGrammarSourceCandidateResolver
{
    /// <summary>Resolves candidates in deterministic source-identity order.</summary>
    IReadOnlyList<GrammarSource> ResolveCandidates(string grammarName);
}
