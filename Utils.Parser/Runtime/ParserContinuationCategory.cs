namespace Utils.Parser.Runtime;

/// <summary>
/// Descriptive continuation categories produced during metadata preparation.
/// Categories are conservative and non-authoritative: they never imply execution semantics.
/// </summary>
internal enum ParserContinuationCategory
{
    /// <summary>
    /// Continuation has no shallow expected tokens and is treated as terminal metadata.
    /// </summary>
    Terminal,

    /// <summary>
    /// Continuation follows a simple sequential structural path.
    /// </summary>
    Sequential,

    /// <summary>
    /// Continuation belongs to a shared-prefix candidate grouping.
    /// </summary>
    SharedPrefixCandidate,

    // Intentionally no deferred placeholder category: keep only categories that are
    // currently produced by deterministic metadata classification.
}
