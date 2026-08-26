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
    /// Kept manually in sync with source.extension.vsixmanifest's Identity/DisplayName/Description
    /// (see docs/releasing/VisualStudioExtension.md for the version policy); update both together.
    /// </summary>
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        Metadata = new ExtensionMetadata(
            id: "Utils.Parser.VisualStudio",
            version: new Version(0, 0, 1),
            publisherName: "Olivier MARTY",
            displayName: "Utils Parser for Visual Studio",
            description: "Syntax highlighting for custom languages defined with .syntaxcolor descriptors, powered by Utils.Parser with isolated out-of-process extensions."),
    };
}
