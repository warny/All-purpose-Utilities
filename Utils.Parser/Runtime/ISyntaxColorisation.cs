using System.Collections.Generic;

namespace Utils.Parser.Runtime;

/// <summary>
/// Defines a syntax colorization contract that can be discovered by Visual Studio tooling.
/// </summary>
/// <remarks>
/// <para>
/// Implementations of this interface are loaded and executed inside the Visual Studio extension process.
/// They must be <strong>pure</strong>: they may not perform network access, write to disk, launch
/// processes, or produce any observable side-effect beyond returning classification names.
/// Read-only file access is permitted only when explicitly required (e.g., autocomplete support).
/// Violations may be silently caught and the profile disabled.
/// </para>
/// <para>
/// Constructors and property accessors must return within a few seconds.
/// Long-running or blocking implementations will be abandoned and the profile ignored.
/// </para>
/// </remarks>
public interface ISyntaxColorisation
{
    /// <summary>
    /// Gets the file extensions supported by this colorization profile.
    /// </summary>
    IReadOnlyList<string> FileExtensions { get; }

    /// <summary>
    /// Gets the <see cref="global::System.Diagnostics.CodeAnalysis.StringSyntaxAttribute"/> names supported by this profile.
    /// </summary>
    IReadOnlyList<string> StringSyntaxExtensions { get; }

    /// <summary>
    /// Gets the Visual Studio classification name for a grammar rule path.
    /// The path must be ordered from parent rule to most specific rule.
    /// </summary>
    /// <param name="rulePath">Ordered rule path from root to leaf.</param>
    /// <returns>The classification name to use, or <see langword="null"/> when no mapping exists.</returns>
    string? GetClassification(IEnumerable<string> rulePath);

    /// <summary>
    /// Gets the Visual Studio classification name for a single grammar rule.
    /// </summary>
    /// <param name="ruleName">Grammar rule name.</param>
    /// <returns>The classification name to use, or <see langword="null"/> when no mapping exists.</returns>
    string? GetClassification(string ruleName);
}
