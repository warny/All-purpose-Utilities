using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace Utils.Parser.Generators.Internal;

/// <summary>
/// Validates metadata and descriptor content used to generate syntax colorization profiles.
/// </summary>
internal static class SyntaxColorizationValidation
{
    /// <summary>
    /// Validates generated type metadata values.
    /// </summary>
    /// <param name="namespaceName">Namespace value from metadata.</param>
    /// <param name="className">Class name value from metadata.</param>
    /// <param name="errorMessage">Validation error message when invalid.</param>
    /// <returns><see langword="true"/> when metadata is valid; otherwise <see langword="false"/>.</returns>
    public static bool TryValidateTypeMetadata(string namespaceName, string className, out string errorMessage)
    {
        if (!SyntaxFacts.IsValidIdentifier(className))
        {
            errorMessage = $"Invalid ClassName metadata '{className}'. The value must be a valid C# identifier.";
            return false;
        }

        if (!IsValidNamespace(namespaceName))
        {
            errorMessage = $"Invalid Namespace metadata '{namespaceName}'. The value must be a valid C# namespace.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Validates descriptor content required to generate a runtime profile.
    /// </summary>
    /// <param name="descriptor">Parsed descriptor.</param>
    /// <param name="errorMessage">Validation error message when invalid.</param>
    /// <returns><see langword="true"/> when descriptor is valid; otherwise <see langword="false"/>.</returns>
    public static bool TryValidateDescriptor(SyntaxColorizationDescriptor descriptor, out string errorMessage)
    {
        if (descriptor.FileExtensions.Count == 0 && descriptor.StringSyntaxExtensions.Count == 0)
        {
            errorMessage = "Descriptor must declare at least one @FileExtension or @StringSyntaxExtension.";
            return false;
        }

        if (descriptor.Entries.Count == 0)
        {
            errorMessage = "Descriptor must declare at least one classification section.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Determines whether a namespace string is a valid C# namespace.
    /// </summary>
    /// <param name="namespaceName">Namespace to validate.</param>
    /// <returns><see langword="true"/> when namespace is valid; otherwise <see langword="false"/>.</returns>
    private static bool IsValidNamespace(string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return true;
        }

        string[] parts = namespaceName.Split('.');
        if (parts.Length == 0 || parts.Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        return parts.All(SyntaxFacts.IsValidIdentifier);
    }
}
