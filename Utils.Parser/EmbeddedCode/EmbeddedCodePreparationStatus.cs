namespace Utils.Parser.EmbeddedCode;

/// <summary>
/// Identifies the outcome category returned by embedded-code preparation.
/// </summary>
public enum EmbeddedCodePreparationStatus
{
    /// <summary>Preparation succeeded and produced an artifact.</summary>
    Succeeded,

    /// <summary>The embedded-code construct or requested preparation path is not supported.</summary>
    Unsupported,

    /// <summary>The requested preparation path requires a compiler that was not configured.</summary>
    CompilerNotConfigured,

    /// <summary>The configured preparation path attempted compilation and failed.</summary>
    CompilationFailed,

    /// <summary>The source was intentionally preserved as metadata without compilation.</summary>
    PreservedNotCompiled
}
