using System;
using System.Collections.Generic;
using System.Linq;

namespace Utils.Parser.Runtime;

/// <summary>
/// Provides a built-in syntax colorization profile for ANTLR4 grammar files (<c>.g4</c>).
/// </summary>
public sealed class G4SyntaxColorisation : ISyntaxColorisation
{
    private static readonly HashSet<string> s_keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "grammar",
        "lexer",
        "parser",
        "fragment",
        "mode",
        "options",
        "tokens",
        "channels",
        "import",
        "returns",
        "locals",
        "throws",
        "catch",
        "finally"
    };

    private static readonly HashSet<string> s_numberRules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "NUMBER",
        "DECIMAL",
        "INT",
        "DIGIT",
        "DIGITS"
    };

    /// <summary>
    /// Gets the singleton instance of the built-in G4 syntax colorization profile.
    /// </summary>
    public static G4SyntaxColorisation Instance { get; } = new G4SyntaxColorisation();

    /// <summary>
    /// Initializes a new instance of the <see cref="G4SyntaxColorisation"/> class.
    /// </summary>
    public G4SyntaxColorisation()
    {
    }

    /// <inheritdoc />
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".g4" };

    /// <inheritdoc />
    public IReadOnlyList<string> StringSyntaxExtensions { get; } = new[] { "ANTLR4", "G4" };

    /// <inheritdoc />
    public string? GetClassification(IEnumerable<string> rulePath)
    {
        if (rulePath == null)
        {
            return null;
        }

        foreach (string rule in rulePath.Reverse())
        {
            string? classification = GetClassification(rule);
            if (classification != null)
            {
                return classification;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public string? GetClassification(string ruleName)
    {
        if (string.IsNullOrWhiteSpace(ruleName))
        {
            return null;
        }

        if (s_keywords.Contains(ruleName))
        {
            return VisualStudioClassificationNames.Keyword;
        }

        if (s_numberRules.Contains(ruleName))
        {
            return VisualStudioClassificationNames.Number;
        }

        if (ruleName.IndexOf("STRING", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return VisualStudioClassificationNames.String;
        }

        return null;
    }
}
