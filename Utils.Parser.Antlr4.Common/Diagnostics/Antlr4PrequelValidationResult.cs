namespace Utils.Parser.Antlr4.Common.Diagnostics;

/// <summary>
/// Holds neutral validation facts derived from an ANTLR4 prequel model.
/// </summary>
/// <param name="Diagnostics">Validation facts in deterministic emission order.</param>
public sealed record Antlr4PrequelValidationResult(
    IReadOnlyList<Antlr4PrequelDiagnostic> Diagnostics);
