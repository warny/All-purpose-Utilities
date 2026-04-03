using System;
using System.Collections.Generic;

namespace Utils.Parser.Runtime;

/// <summary>
/// Provides syntax colorization for <c>.syntaxcolor</c> descriptor files.
/// </summary>
public sealed class SyntaxColorisationDescriptorSyntaxColorisation : ISyntaxColorisation
{
    private static readonly HashSet<string> MethodTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        VisualStudioClassificationNames.Keyword,
        VisualStudioClassificationNames.Number,
        VisualStudioClassificationNames.String,
        VisualStudioClassificationNames.Operator,
        VisualStudioClassificationNames.Text
    };

    /// <summary>
    /// Gets a singleton profile instance.
    /// </summary>
    public static SyntaxColorisationDescriptorSyntaxColorisation Instance { get; } = new();

    /// <inheritdoc />
    public IReadOnlyList<string> FileExtensions { get; } = new[] { ".syntaxcolor" };

    /// <inheritdoc />
    public IReadOnlyList<string> StringSyntaxExtensions { get; } = Array.Empty<string>();

    /// <inheritdoc />
    public string? GetClassification(IEnumerable<string> rulePath)
    {
        if (rulePath == null)
        {
            return null;
        }

        foreach (string rule in rulePath)
        {
            string? classification = GetClassification(rule);
            if (!string.IsNullOrWhiteSpace(classification))
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

        string token = ruleName.Trim();
        if (token.StartsWith("@", StringComparison.Ordinal))
        {
            return VisualStudioClassificationNames.Keyword;
        }

        if (token == ":" || token == "|")
        {
            return VisualStudioClassificationNames.Operator;
        }

        if (MethodTokens.Contains(token))
        {
            return VisualStudioClassificationNames.Keyword;
        }

        return null;
    }
}
