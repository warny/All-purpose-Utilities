namespace Utils.Parser.EmbeddedCode;

/// <summary>
/// Contains embedded-code runtime discovery entries and filtered views for executable and unsupported items.
/// </summary>
public sealed record EmbeddedCodeRuntimeDiscoveryResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedCodeRuntimeDiscoveryResult"/> record.
    /// </summary>
    /// <param name="entries">All discovery entries in deterministic traversal order.</param>
    public EmbeddedCodeRuntimeDiscoveryResult(IReadOnlyList<EmbeddedCodeRuntimeEntry> entries)
    {
        Entries = entries?.ToArray() ?? throw new ArgumentNullException(nameof(entries));
        ExecutableEntries = Entries.Where(static entry => entry.IsRuntimeExecutable).ToArray();
        UnsupportedEntries = Entries.Where(static entry => !entry.IsRuntimeExecutable).ToArray();
    }

    /// <summary>Gets all discovery entries in deterministic traversal order.</summary>
    public IReadOnlyList<EmbeddedCodeRuntimeEntry> Entries { get; }

    /// <summary>Gets entries that are executable by parser runtime hooks.</summary>
    public IReadOnlyList<EmbeddedCodeRuntimeEntry> ExecutableEntries { get; }

    /// <summary>Gets entries that were discovered but are outside parser runtime hook support.</summary>
    public IReadOnlyList<EmbeddedCodeRuntimeEntry> UnsupportedEntries { get; }
}
