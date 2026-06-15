namespace Utils.Parser.Antlr4.Common.Diagnostics;

/// <summary>
/// Represents one neutral prequel diagnostic fact.
/// </summary>
/// <param name="Code">Diagnostic fact code.</param>
/// <param name="Subject">Optional subject payload for the fact.</param>
public sealed record Antlr4PrequelDiagnostic(
    Antlr4PrequelDiagnosticCode Code,
    string? Subject);
