namespace Utils.Parser.Runtime;

/// <summary>
/// Provides an explicit structural hash contract for user objects stored inside parser execution contexts.
/// </summary>
public interface IParserExecutionStateHashable
{
    /// <summary>
    /// Computes the object contribution to a parser execution-state key.
    /// </summary>
    /// <returns>A deterministic hash for the state represented by the object.</returns>
    ulong GetParserExecutionStateHash();
}
