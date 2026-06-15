using Utils.Parser.Diagnostics;
using Utils.Parser.EmbeddedCode;

namespace Utils.Parser.Expressions;

/// <summary>
/// Describes one embedded-code preparation or skip decision made while building a prepared expression registry.
/// </summary>
public sealed record PreparedExpressionEmbeddedCodeRegistryBuildEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PreparedExpressionEmbeddedCodeRegistryBuildEntry"/> record.
    /// </summary>
    /// <param name="source">Source metadata for the embedded code item.</param>
    /// <param name="key">Registry key used for successful artifacts, or <c>null</c> when no artifact was registered.</param>
    /// <param name="ruleName">Owning parser rule name, or <c>null</c> for grammar-level items.</param>
    /// <param name="status">Preparation status associated with this entry.</param>
    /// <param name="diagnosticDescriptor">Optional diagnostic metadata returned by the preparer.</param>
    /// <param name="exception">Optional exception metadata returned by the preparer.</param>
    /// <param name="diagnosticArguments">Optional diagnostic arguments returned by the preparer.</param>
    /// <param name="wasAddedToRegistry">Whether the prepared artifact was registered.</param>
    /// <param name="isDuplicate">Whether a successful preparation collided with an existing registry key.</param>
    /// <param name="isSkipped">Whether the item was intentionally skipped without invoking the preparer.</param>
    /// <param name="skipReason">Human-readable reason for skipped items.</param>
    /// <param name="unsupportedReason">Common unsupported reason for skipped items.</param>
    public PreparedExpressionEmbeddedCodeRegistryBuildEntry(
        EmbeddedCodeSource source,
        PreparedExpressionEmbeddedCodeKey? key,
        string? ruleName,
        EmbeddedCodePreparationStatus status,
        ParserDiagnosticDescriptor? diagnosticDescriptor = null,
        Exception? exception = null,
        IReadOnlyList<object?>? diagnosticArguments = null,
        bool wasAddedToRegistry = false,
        bool isDuplicate = false,
        bool isSkipped = false,
        string? skipReason = null,
        EmbeddedCodeUnsupportedReason? unsupportedReason = null)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Key = key;
        RuleName = ruleName;
        Status = status;
        DiagnosticDescriptor = diagnosticDescriptor;
        Exception = exception;
        DiagnosticArguments = diagnosticArguments?.ToArray() ?? [];
        WasAddedToRegistry = wasAddedToRegistry;
        IsDuplicate = isDuplicate;
        IsSkipped = isSkipped;
        SkipReason = skipReason;
        UnsupportedReason = unsupportedReason;
    }

    /// <summary>
    /// Gets the source metadata for the embedded code item.
    /// </summary>
    public EmbeddedCodeSource Source { get; }

    /// <summary>
    /// Gets the registry key used for successful artifacts, or <c>null</c> when none was registered.
    /// </summary>
    public PreparedExpressionEmbeddedCodeKey? Key { get; }

    /// <summary>
    /// Gets the owning parser rule name, or <c>null</c> for grammar-level items.
    /// </summary>
    public string? RuleName { get; }

    /// <summary>
    /// Gets the preparation status associated with this entry.
    /// </summary>
    public EmbeddedCodePreparationStatus Status { get; }

    /// <summary>
    /// Gets optional diagnostic metadata returned by the preparer.
    /// </summary>
    public ParserDiagnosticDescriptor? DiagnosticDescriptor { get; }

    /// <summary>
    /// Gets optional exception metadata returned by the preparer.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets optional diagnostic arguments returned by the preparer.
    /// </summary>
    public IReadOnlyList<object?> DiagnosticArguments { get; }

    /// <summary>
    /// Gets a value indicating whether the prepared artifact was registered.
    /// </summary>
    public bool WasAddedToRegistry { get; }

    /// <summary>
    /// Gets a value indicating whether a successful preparation collided with an existing registry key.
    /// </summary>
    public bool IsDuplicate { get; }

    /// <summary>
    /// Gets a value indicating whether the item was intentionally skipped without invoking the preparer.
    /// </summary>
    public bool IsSkipped { get; }

    /// <summary>
    /// Gets the human-readable reason for skipped items.
    /// </summary>
    public string? SkipReason { get; }

    /// <summary>
    /// Gets the common unsupported reason for skipped items.
    /// </summary>
    public EmbeddedCodeUnsupportedReason? UnsupportedReason { get; }
}
