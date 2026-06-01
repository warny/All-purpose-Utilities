using Utils.Parser.Diagnostics;

namespace Utils.Parser.EmbeddedCode;

/// <summary>
/// Represents the result of preparing ANTLR embedded-code source for a specific execution or generation path.
/// </summary>
/// <typeparam name="TArtifact">Type of the prepared artifact produced on success.</typeparam>
public sealed record EmbeddedCodePreparationResult<TArtifact>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedCodePreparationResult{TArtifact}"/> record.
    /// </summary>
    /// <param name="status">Preparation outcome category.</param>
    /// <param name="artifact">Optional prepared artifact when preparation succeeded.</param>
    /// <param name="diagnosticDescriptor">Optional diagnostic descriptor associated with the outcome.</param>
    /// <param name="exception">Optional exception associated with a failed preparation attempt.</param>
    /// <param name="diagnosticArguments">Optional diagnostic message arguments associated with the outcome.</param>
    public EmbeddedCodePreparationResult(
        EmbeddedCodePreparationStatus status,
        TArtifact? artifact = default,
        ParserDiagnosticDescriptor? diagnosticDescriptor = null,
        Exception? exception = null,
        IReadOnlyList<object?>? diagnosticArguments = null)
    {
        if (status == EmbeddedCodePreparationStatus.Succeeded && artifact is null)
        {
            throw new ArgumentException("A successful embedded-code preparation result must include an artifact.", nameof(artifact));
        }

        Status = status;
        Artifact = artifact;
        DiagnosticDescriptor = diagnosticDescriptor;
        Exception = exception;
        DiagnosticArguments = diagnosticArguments?.ToArray() ?? Array.Empty<object?>();
    }

    /// <summary>
    /// Gets the preparation outcome category.
    /// </summary>
    public EmbeddedCodePreparationStatus Status { get; }

    /// <summary>
    /// Gets the optional prepared artifact when preparation succeeded.
    /// </summary>
    public TArtifact? Artifact { get; }

    /// <summary>
    /// Gets the optional diagnostic descriptor associated with the outcome.
    /// </summary>
    public ParserDiagnosticDescriptor? DiagnosticDescriptor { get; }

    /// <summary>
    /// Gets the optional exception associated with a failed preparation attempt.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets the optional diagnostic message arguments associated with the outcome.
    /// </summary>
    public IReadOnlyList<object?> DiagnosticArguments { get; }

    /// <summary>
    /// Creates a successful preparation result with an artifact.
    /// </summary>
    /// <param name="artifact">Prepared artifact produced by the preparation path.</param>
    /// <returns>A successful preparation result.</returns>
    public static EmbeddedCodePreparationResult<TArtifact> Success(TArtifact artifact) =>
        new(EmbeddedCodePreparationStatus.Succeeded, artifact);

    /// <summary>
    /// Creates an unsupported preparation result.
    /// </summary>
    /// <param name="diagnosticArguments">Optional diagnostic message arguments associated with the outcome.</param>
    /// <returns>An unsupported preparation result.</returns>
    public static EmbeddedCodePreparationResult<TArtifact> Unsupported(IEnumerable<object?>? diagnosticArguments = null) =>
        new(EmbeddedCodePreparationStatus.Unsupported, diagnosticDescriptor: ParserDiagnostics.EmbeddedCodeLanguageUnsupported, diagnosticArguments: ToDiagnosticArgumentList(diagnosticArguments));

    /// <summary>
    /// Creates a compiler-not-configured preparation result.
    /// </summary>
    /// <param name="diagnosticArguments">Optional diagnostic message arguments associated with the outcome.</param>
    /// <returns>A compiler-not-configured preparation result.</returns>
    public static EmbeddedCodePreparationResult<TArtifact> CompilerNotConfigured(IEnumerable<object?>? diagnosticArguments = null) =>
        new(EmbeddedCodePreparationStatus.CompilerNotConfigured, diagnosticDescriptor: ParserDiagnostics.EmbeddedCodeCompilerNotConfigured, diagnosticArguments: ToDiagnosticArgumentList(diagnosticArguments));

    /// <summary>
    /// Creates a failed compilation preparation result.
    /// </summary>
    /// <param name="exception">Optional exception associated with the failed compilation.</param>
    /// <param name="diagnosticArguments">Optional diagnostic message arguments associated with the outcome.</param>
    /// <returns>A failed compilation preparation result.</returns>
    public static EmbeddedCodePreparationResult<TArtifact> CompilationFailed(Exception? exception = null, IEnumerable<object?>? diagnosticArguments = null) =>
        new(EmbeddedCodePreparationStatus.CompilationFailed, diagnosticDescriptor: ParserDiagnostics.EmbeddedCodeCompilationFailed, exception: exception, diagnosticArguments: ToDiagnosticArgumentList(diagnosticArguments));

    /// <summary>
    /// Creates a preserved-without-compilation preparation result.
    /// </summary>
    /// <param name="diagnosticArguments">Optional diagnostic message arguments associated with the outcome.</param>
    /// <returns>A preserved-without-compilation preparation result.</returns>
    public static EmbeddedCodePreparationResult<TArtifact> PreservedNotCompiled(IEnumerable<object?>? diagnosticArguments = null) =>
        new(EmbeddedCodePreparationStatus.PreservedNotCompiled, diagnosticDescriptor: ParserDiagnostics.EmbeddedCodePreservedNotCompiled, diagnosticArguments: ToDiagnosticArgumentList(diagnosticArguments));

    /// <summary>
    /// Materializes optional diagnostic arguments exactly once.
    /// </summary>
    /// <param name="diagnosticArguments">Optional diagnostic arguments to materialize.</param>
    /// <returns>A read-only list of diagnostic arguments.</returns>
    private static IReadOnlyList<object?> ToDiagnosticArgumentList(IEnumerable<object?>? diagnosticArguments) =>
        diagnosticArguments?.ToArray() ?? Array.Empty<object?>();
}
