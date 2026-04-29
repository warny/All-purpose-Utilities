using System;

namespace Utils.Parser.Diagnostics;

/// <summary>
/// Provides severity mapping from diagnostic code prefixes.
/// </summary>
public static class ParserDiagnosticSeverityMapper
{
    /// <summary>
    /// Resolves severity from a code using the first digit after <c>UP</c>.
    /// Codes using the <c>PARSERxxx</c> convention map to warning severity.
    /// </summary>
    /// <param name="code">Diagnostic code in the form <c>UPxxxx</c> or <c>PARSERxxx</c>.</param>
    /// <returns>Mapped diagnostic severity.</returns>
    /// <exception cref="ArgumentException">Thrown when the code prefix is invalid.</exception>
    public static DiagnosticSeverity FromCode(string code)
    {
        if (code is null)
        {
            throw new ArgumentNullException(nameof(code));
        }

        if (code.StartsWith("PARSER", StringComparison.Ordinal) &&
            code.Length == 9 &&
            char.IsDigit(code[6]) &&
            char.IsDigit(code[7]) &&
            char.IsDigit(code[8]))
        {
            return DiagnosticSeverity.Warning;
        }

        if (code.Length != 6 || !code.StartsWith("UP", StringComparison.Ordinal) || !char.IsDigit(code[2]))
        {
            throw new ArgumentException($"Invalid diagnostic code '{code}'.", nameof(code));
        }

        return code[2] switch
        {
            '0' => DiagnosticSeverity.Error,
            '1' or '2' or '3' or '4' or '5' or '6' or '7' => DiagnosticSeverity.Warning,
            '8' => DiagnosticSeverity.Info,
            '9' => DiagnosticSeverity.Debug,
            _ => throw new ArgumentException($"Invalid diagnostic code '{code}'.", nameof(code))
        };
    }
}
