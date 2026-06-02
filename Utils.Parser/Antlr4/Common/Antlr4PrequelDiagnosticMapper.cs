using System;
using System.Collections.Generic;
using Utils.Parser.Antlr4.Common.Diagnostics;
using Utils.Parser.Diagnostics;

namespace Utils.Parser.Antlr4.Common;

/// <summary>
/// Maps shared ANTLR4 prequel diagnostic facts to runtime parser diagnostics.
/// </summary>
internal static class Antlr4PrequelDiagnosticMapper
{
    /// <summary>
    /// Converts shared neutral prequel diagnostics into runtime parser diagnostics.
    /// </summary>
    /// <param name="diagnostics">Shared diagnostics to map.</param>
    /// <returns>Mapped parser diagnostics.</returns>
    public static IReadOnlyList<ParserDiagnostic> ToParserDiagnostics(IReadOnlyList<Antlr4PrequelDiagnostic> diagnostics)
    {
        if (diagnostics is null)
        {
            throw new ArgumentNullException(nameof(diagnostics));
        }

        var result = new List<ParserDiagnostic>(diagnostics.Count);
        foreach (var diagnostic in diagnostics)
        {
            result.Add(MapSingle(diagnostic));
        }

        return result;
    }

    /// <summary>Maps one <see cref="Antlr4PrequelDiagnostic"/> to the corresponding <see cref="ParserDiagnostic"/>.</summary>
    private static ParserDiagnostic MapSingle(Antlr4PrequelDiagnostic diagnostic)
    {
        return diagnostic.Code switch
        {
            Antlr4PrequelDiagnosticCode.ImportParsedButNotResolved =>
                new ParserDiagnostic(ParserDiagnostics.ImportParsedButNotResolved, ParserDiagnostics.ImportParsedButNotResolved.FormatMessage(diagnostic.Subject ?? string.Empty)),
            Antlr4PrequelDiagnosticCode.TokensBlockIgnored =>
                new ParserDiagnostic(ParserDiagnostics.TokensBlockIgnored, ParserDiagnostics.TokensBlockIgnored.FormatMessage()),
            Antlr4PrequelDiagnosticCode.ChannelsBlockIgnored =>
                new ParserDiagnostic(ParserDiagnostics.ChannelsBlockIgnored, ParserDiagnostics.ChannelsBlockIgnored.FormatMessage()),
            Antlr4PrequelDiagnosticCode.GrammarActionIgnored =>
                new ParserDiagnostic(ParserDiagnostics.ActionIgnored, ParserDiagnostics.ActionIgnored.FormatMessage(diagnostic.Subject ?? "@action")),
            _ => throw new ArgumentOutOfRangeException(nameof(diagnostic), diagnostic.Code, "Unsupported prequel diagnostic code."),
        };
    }
}
