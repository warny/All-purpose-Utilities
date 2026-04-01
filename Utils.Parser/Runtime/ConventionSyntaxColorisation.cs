using System;
using System.Collections.Generic;
using System.Linq;

namespace Utils.Parser.Runtime;

/// <summary>
/// Provides convention-based syntax colorization rules for grammars that do not declare explicit mappings.
/// </summary>
public sealed class ConventionSyntaxColorisation : ISyntaxColorisation
{
    private readonly HashSet<string> keywords;
    private readonly HashSet<string> numbers;
    private readonly HashSet<string> strings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConventionSyntaxColorisation"/> class.
    /// </summary>
    /// <param name="fileExtensions">Supported file extensions.</param>
    /// <param name="stringSyntaxExtensions">Supported string syntax names.</param>
    /// <param name="keywordRules">Rules classified as keywords.</param>
    /// <param name="numberRules">Rules classified as numbers.</param>
    /// <param name="stringRules">Rules classified as strings.</param>
    public ConventionSyntaxColorisation(
        IEnumerable<string> fileExtensions,
        IEnumerable<string> stringSyntaxExtensions,
        IEnumerable<string> keywordRules,
        IEnumerable<string> numberRules,
        IEnumerable<string> stringRules)
    {
        FileExtensions = Normalize(fileExtensions);
        StringSyntaxExtensions = Normalize(stringSyntaxExtensions);
        keywords = ToLookup(keywordRules);
        numbers = ToLookup(numberRules);
        strings = ToLookup(stringRules);
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

        if (keywords.Contains(ruleName))
        {
            return VisualStudioClassificationNames.Keyword;
        }

        if (numbers.Contains(ruleName))
        {
            return VisualStudioClassificationNames.Number;
        }

        if (strings.Contains(ruleName))
        {
            return VisualStudioClassificationNames.String;
        }

        return null;
    }

    /// <summary>
    /// Creates a convention colorization profile from a generated grammar helper.
    /// </summary>
    /// <param name="fileExtensions">Supported file extensions.</param>
    /// <param name="stringSyntaxName">String syntax extension associated with the grammar.</param>
    /// <param name="keywordRules">Grammar keyword rules.</param>
    /// <param name="numberRules">Grammar numeric rules.</param>
    /// <param name="stringRules">Grammar string rules.</param>
    /// <returns>A new convention-based colorization profile.</returns>
    public static ConventionSyntaxColorisation FromGrammarConventions(
        IEnumerable<string> fileExtensions,
        string stringSyntaxName,
        IEnumerable<string> keywordRules,
        IEnumerable<string> numberRules,
        IEnumerable<string> stringRules)
    {
        return new ConventionSyntaxColorisation(
            fileExtensions,
            new[] { stringSyntaxName },
            keywordRules,
            numberRules,
            stringRules);
    }

    /// <summary>
    /// Normalizes a sequence of tokens by trimming and removing empty values.
    /// </summary>
    /// <param name="values">Values to normalize.</param>
    /// <returns>The normalized list.</returns>
    private static IReadOnlyList<string> Normalize(IEnumerable<string> values)
    {
        return values
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Creates a case-insensitive lookup set from rule names.
    /// </summary>
    /// <param name="values">Rule names.</param>
    /// <returns>The normalized lookup set.</returns>
    private static HashSet<string> ToLookup(IEnumerable<string> values)
    {
        return new HashSet<string>(Normalize(values), StringComparer.OrdinalIgnoreCase);
    }
}
