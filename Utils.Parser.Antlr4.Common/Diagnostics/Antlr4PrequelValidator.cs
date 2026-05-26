using System;
using System.Collections.Generic;

namespace Utils.Parser.Antlr4.Common.Diagnostics;

/// <summary>
/// Validates shared ANTLR4 prequel metadata and emits neutral diagnostic facts.
/// </summary>
public static class Antlr4PrequelValidator
{
    /// <summary>
    /// Validates the provided prequel model.
    /// </summary>
    /// <param name="model">Model to validate.</param>
    /// <returns>Validation result containing shared neutral facts.</returns>
    public static Antlr4PrequelValidationResult Validate(Antlr4PrequelModel model)
    {
        if (model is null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        var diagnostics = new List<Antlr4PrequelDiagnostic>();

        foreach (var import in model.Imports)
        {
            diagnostics.Add(new Antlr4PrequelDiagnostic(
                Antlr4PrequelDiagnosticCode.ImportParsedButNotResolved,
                import.GrammarName));
        }

        if (model.DeclaredTokens.Count > 0)
        {
            diagnostics.Add(new Antlr4PrequelDiagnostic(
                Antlr4PrequelDiagnosticCode.TokensBlockIgnored,
                null));
        }

        if (HasNonDefaultChannel(model.DeclaredChannels))
        {
            diagnostics.Add(new Antlr4PrequelDiagnostic(
                Antlr4PrequelDiagnosticCode.ChannelsBlockIgnored,
                null));
        }

        foreach (var action in model.Actions)
        {
            var subject = action.Target is null
                ? $"@{action.Name}"
                : $"@{action.Target}::{action.Name}";

            diagnostics.Add(new Antlr4PrequelDiagnostic(
                Antlr4PrequelDiagnosticCode.GrammarActionIgnored,
                subject));
        }

        return new Antlr4PrequelValidationResult(diagnostics);
    }

    /// <summary>
    /// Determines whether declared channels contain a non-default channel.
    /// </summary>
    /// <param name="channels">Declared channel names.</param>
    /// <returns><c>true</c> when a non-default channel is found; otherwise <c>false</c>.</returns>
    private static bool HasNonDefaultChannel(IEnumerable<string> channels)
    {
        foreach (var channel in channels)
        {
            if (!string.Equals(channel, "DEFAULT_CHANNEL", StringComparison.Ordinal)
                && !string.Equals(channel, "HIDDEN", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
