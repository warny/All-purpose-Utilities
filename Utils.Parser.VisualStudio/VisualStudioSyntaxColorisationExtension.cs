using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Utils.Parser.Runtime;

namespace Utils.Parser.VisualStudio;

/// <summary>
/// Represents the runtime integration point that a Visual Studio extension can use
/// to load syntax colorization profiles from project assemblies and descriptor files.
/// </summary>
public sealed class VisualStudioSyntaxColorisationExtension
{
    private readonly VisualStudioSyntaxColorisationRegistry registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="VisualStudioSyntaxColorisationExtension"/> class.
    /// </summary>
    public VisualStudioSyntaxColorisationExtension()
    {
        registry = new VisualStudioSyntaxColorisationRegistry();
    }

    /// <summary>
    /// Loads all available colorization profiles for the current Visual Studio workspace context.
    /// </summary>
    /// <param name="projectAssemblies">Assemblies produced by loaded projects and references.</param>
    /// <param name="descriptorFiles">Descriptor files discovered in loaded projects.</param>
    /// <returns>Resolved colorization profiles.</returns>
    public IReadOnlyList<ISyntaxColorisation> LoadProfiles(IEnumerable<Assembly> projectAssemblies, IEnumerable<string> descriptorFiles)
    {
        return registry.LoadProfiles(projectAssemblies, descriptorFiles);
    }

    /// <summary>
    /// Returns project profiles for file editor usage only when the requested extension is not already handled
    /// by an installed Visual Studio colorization.
    /// </summary>
    /// <param name="profiles">Loaded project profiles.</param>
    /// <param name="fileExtension">Target file extension (for example <c>.sql</c>).</param>
    /// <param name="installedFileExtensions">File extensions already handled by installed colorizers.</param>
    /// <returns>
    /// Matching project profiles when no installed support exists for the extension; otherwise an empty list.
    /// </returns>
    public IReadOnlyList<ISyntaxColorisation> GetSecondaryProfilesForFileExtension(
        IEnumerable<ISyntaxColorisation> profiles,
        string fileExtension,
        IEnumerable<string> installedFileExtensions)
    {
        string? normalizedExtension = NormalizeToken(fileExtension);
        if (normalizedExtension == null)
        {
            return Array.Empty<ISyntaxColorisation>();
        }

        if (ContainsToken(installedFileExtensions, normalizedExtension))
        {
            return Array.Empty<ISyntaxColorisation>();
        }

        return profiles
            .Where(profile => ContainsToken(profile.FileExtensions, normalizedExtension))
            .ToArray();
    }

    /// <summary>
    /// Returns project profiles for StringSyntax usage only when the requested syntax is not already handled
    /// by an installed Visual Studio colorization.
    /// </summary>
    /// <param name="profiles">Loaded project profiles.</param>
    /// <param name="stringSyntaxExtension">Target StringSyntax name (for example <c>SQL</c>).</param>
    /// <param name="installedStringSyntaxExtensions">StringSyntax names already handled by installed colorizers.</param>
    /// <returns>
    /// Matching project profiles when no installed support exists for the StringSyntax name; otherwise an empty list.
    /// </returns>
    public IReadOnlyList<ISyntaxColorisation> GetSecondaryProfilesForStringSyntax(
        IEnumerable<ISyntaxColorisation> profiles,
        string stringSyntaxExtension,
        IEnumerable<string> installedStringSyntaxExtensions)
    {
        string? normalizedExtension = NormalizeToken(stringSyntaxExtension);
        if (normalizedExtension == null)
        {
            return Array.Empty<ISyntaxColorisation>();
        }

        if (ContainsToken(installedStringSyntaxExtensions, normalizedExtension))
        {
            return Array.Empty<ISyntaxColorisation>();
        }

        return profiles
            .Where(profile => ContainsToken(profile.StringSyntaxExtensions, normalizedExtension))
            .ToArray();
    }

    /// <summary>
    /// Returns a classification and applies context-aware default values when no mapping is available.
    /// </summary>
    /// <param name="profile">Colorization profile to query.</param>
    /// <param name="rulePath">Ordered rule path from root to leaf.</param>
    /// <param name="isStringSyntaxContext"><see langword="true"/> for StringSyntax literals; otherwise file editor context.</param>
    /// <returns>Resolved classification name.</returns>
    public string GetClassificationOrDefault(ISyntaxColorisation profile, IEnumerable<string> rulePath, bool isStringSyntaxContext)
    {
        try
        {
            string? classification = profile.GetClassification(rulePath);
            if (!string.IsNullOrWhiteSpace(classification))
            {
                return classification;
            }
        }
        catch
        {
            // Fallback below ensures stability.
        }

        return isStringSyntaxContext
            ? VisualStudioClassificationNames.String
            : VisualStudioClassificationNames.Text;
    }

    /// <summary>
    /// Returns a classification and applies context-aware default values when no mapping is available.
    /// </summary>
    /// <param name="profile">Colorization profile to query.</param>
    /// <param name="ruleName">Single rule name.</param>
    /// <param name="isStringSyntaxContext"><see langword="true"/> for StringSyntax literals; otherwise file editor context.</param>
    /// <returns>Resolved classification name.</returns>
    public string GetClassificationOrDefault(ISyntaxColorisation profile, string ruleName, bool isStringSyntaxContext)
    {
        try
        {
            string? classification = profile.GetClassification(ruleName);
            if (!string.IsNullOrWhiteSpace(classification))
            {
                return classification;
            }
        }
        catch
        {
            // Fallback below ensures stability.
        }

        return isStringSyntaxContext
            ? VisualStudioClassificationNames.String
            : VisualStudioClassificationNames.Text;
    }

    /// <summary>
    /// Normalizes a token by trimming it and rejecting empty values.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <returns>Normalized token, or <see langword="null"/> when invalid.</returns>
    private static string? NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    /// <summary>
    /// Determines whether a sequence contains a token using case-insensitive matching.
    /// </summary>
    /// <param name="values">Values to inspect.</param>
    /// <param name="expectedToken">Expected normalized token.</param>
    /// <returns><see langword="true"/> when the token exists; otherwise <see langword="false"/>.</returns>
    private static bool ContainsToken(IEnumerable<string> values, string expectedToken)
    {
        foreach (string value in values)
        {
            string? normalized = NormalizeToken(value);
            if (normalized != null && string.Equals(normalized, expectedToken, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
