using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Parser.Runtime;

namespace Utils.Parser.VisualStudio;

/// <summary>
/// Adapts a parsed descriptor to the <see cref="ISyntaxColorisation"/> contract.
/// </summary>
public sealed class DescriptorSyntaxColorisation : ISyntaxColorisation
{
    private readonly Dictionary<string, string> classificationsByRule;

    /// <summary>
    /// Initializes a new instance of the <see cref="DescriptorSyntaxColorisation"/> class.
    /// </summary>
    /// <param name="descriptor">Descriptor data.</param>
    public DescriptorSyntaxColorisation(SyntaxColorizationDescriptor descriptor)
    {
        FileExtensions = descriptor.FileExtensions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        StringSyntaxExtensions = descriptor.StringSyntaxExtensions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        classificationsByRule = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in descriptor.Entries)
        {
            string classification = NormalizeClassification(entry.Classification);
            foreach (string rule in entry.Rules)
            {
                classificationsByRule[rule] = classification;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> FileExtensions { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> StringSyntaxExtensions { get; }

    /// <inheritdoc />
    public string? GetClassification(IEnumerable<string> rulePath)
    {
        if (rulePath == null)
        {
            return null;
        }

        foreach (string ruleName in rulePath.Reverse())
        {
            string? classification = GetClassification(ruleName);
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

        return classificationsByRule.TryGetValue(ruleName, out string? classification) ? classification : null;
    }

    /// <summary>
    /// Normalizes descriptor classifications to Visual Studio standard names.
    /// </summary>
    /// <param name="classification">Descriptor classification.</param>
    /// <returns>The normalized classification name.</returns>
    private static string NormalizeClassification(string classification)
    {
        if (classification.Equals("Keyword", StringComparison.OrdinalIgnoreCase))
        {
            return VisualStudioClassificationNames.Keyword;
        }

        if (classification.Equals("Number", StringComparison.OrdinalIgnoreCase))
        {
            return VisualStudioClassificationNames.Number;
        }

        if (classification.Equals("String", StringComparison.OrdinalIgnoreCase))
        {
            return VisualStudioClassificationNames.String;
        }

        if (classification.Equals("Operator", StringComparison.OrdinalIgnoreCase))
        {
            return VisualStudioClassificationNames.Operator;
        }

        if (classification.Equals("Raw text", StringComparison.OrdinalIgnoreCase) || classification.Equals("Text", StringComparison.OrdinalIgnoreCase))
        {
            return VisualStudioClassificationNames.Text;
        }

        return classification;
    }
}
