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
    /// Version/DisplayName/Description/PublisherName are kept manually in sync with
    /// source.extension.vsixmanifest (see docs/releasing/VisualStudioExtension.md for the version
    /// policy and for which fields must vs. must not match between the two files) - update both
    /// together. <see cref="ExtensionMetadata.Id"/> is deliberately NOT the same value as the
    /// manifest's Identity/Id: this is a short activation-contract identifier for the
    /// VisualStudio.Extensibility framework, not the VSIX package identity Visual Studio/the
    /// Marketplace use to recognize updates - see docs/releasing/VisualStudioExtension.md.
    /// </summary>
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        Metadata = new ExtensionMetadata(
            id: "Utils.Parser.VisualStudio",
            version: new Version(0, 0, 1),
            publisherName: "Olivier MARTY",
            displayName: "Utils Parser for Visual Studio",
            description: "Syntax highlighting for custom languages defined with .syntaxcolor descriptors, powered by Utils.Parser with isolated out-of-process extensions.")
        {
            // The project targets net8.0; DotnetTargetVersions is the SDK-recommended way to
            // declare that (see ExtensionMetadata.DotnetTargetVersions XML docs) instead of the
            // classic VSIX "Microsoft.Framework.NDP" manifest dependency, which denotes the legacy
            // .NET Framework and does not apply to this out-of-process, net8.0 extension.
            DotnetTargetVersions = new[] { DotnetTarget.Net8 },
            // Mirrors source.extension.vsixmanifest's InstallationTarget/Prerequisite floor (17.14,
            // the minimum major.minor of the referenced Microsoft.VisualStudio.Extensibility package).
            InstallationTargetVersion = "17.14",
            License = "LICENSE-apache-2.0.txt",
            MoreInfo = "https://github.com/warny/All-purpose-Utilities",
        },
    };
}
