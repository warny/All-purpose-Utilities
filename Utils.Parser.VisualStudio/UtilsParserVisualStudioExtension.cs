using System;
using Microsoft.VisualStudio.Extensibility;

namespace Utils.Parser.VisualStudio;

/// <summary>
/// Defines metadata and load configuration for the out-of-process Visual Studio extension.
/// </summary>
[VisualStudioContribution]
public sealed class UtilsParserVisualStudioExtension : Extension
{
    /// <summary>
    /// Gets the static extension configuration consumed at build time.
    /// </summary>
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        Metadata = new ExtensionMetadata(
            id: "Utils.Parser.VisualStudio",
            version: new Version(1, 0, 0),
            publisherName: "Olivier MARTY",
            displayName: "Utils Parser Visual Studio",
            description: "Out-of-process syntax colorization extension based on .syntaxcolor descriptors."),
    };
}
