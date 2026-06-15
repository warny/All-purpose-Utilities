namespace Utils.Parser.EmbeddedCode;

/// <summary>
/// Describes one embedded-code item discovered with parser-runtime-compatible indexing metadata.
/// </summary>
public sealed record EmbeddedCodeRuntimeEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedCodeRuntimeEntry"/> record.
    /// </summary>
    /// <param name="source">Raw source metadata, including runtime-compatible indexes when available.</param>
    /// <param name="isRuntimeExecutable">Whether the embedded-code item is executable by parser runtime hooks.</param>
    /// <param name="unsupportedReason">Reason why the item is not executable, or <c>null</c>/<see cref="EmbeddedCodeUnsupportedReason.None"/> when executable.</param>
    public EmbeddedCodeRuntimeEntry(
        EmbeddedCodeSource source,
        bool isRuntimeExecutable,
        EmbeddedCodeUnsupportedReason? unsupportedReason = null)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        IsRuntimeExecutable = isRuntimeExecutable;
        UnsupportedReason = isRuntimeExecutable ? EmbeddedCodeUnsupportedReason.None : unsupportedReason ?? EmbeddedCodeUnsupportedReason.UnsupportedEmbeddedCodeKind;
        RuntimeKey = isRuntimeExecutable ? EmbeddedCodeRuntimeKey.FromSource(source) : null;
    }

    /// <summary>Gets the raw source metadata, including runtime-compatible indexes when available.</summary>
    public EmbeddedCodeSource Source { get; }

    /// <summary>Gets the embedded-code kind.</summary>
    public EmbeddedCodeKind Kind => Source.Kind;

    /// <summary>Gets the optional owning parser rule name.</summary>
    public string? RuleName => Source.RuleName;

    /// <summary>Gets the optional runtime alternative index.</summary>
    public int? AlternativeIndex => Source.AlternativeIndex;

    /// <summary>Gets the optional runtime element index.</summary>
    public int? ElementIndex => Source.ElementIndex;

    /// <summary>Gets parser-runtime dispatch metadata for executable entries.</summary>
    public EmbeddedCodeRuntimeKey? RuntimeKey { get; }

    /// <summary>Gets a value indicating whether the embedded-code item is executable by parser runtime hooks.</summary>
    public bool IsRuntimeExecutable { get; }

    /// <summary>Gets the unsupported reason for skipped entries, or <see cref="EmbeddedCodeUnsupportedReason.None"/> for executable entries.</summary>
    public EmbeddedCodeUnsupportedReason UnsupportedReason { get; }
}
