using System;
using System.IO;

namespace Utils.Parser.VisualStudio.Worker;

/// <summary>
/// Resolves the directory from which user plugin assemblies are loaded by the worker process.
/// Users place ISyntaxColorisation DLLs in this directory to extend syntax colorization.
/// </summary>
internal static class PluginDirectoryLocator
{
    /// <summary>
    /// Gets the user-specific plugin directory path.
    /// The directory is not created automatically; it must be created by the user.
    /// </summary>
    public static string PluginDirectory { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Utils.Parser.VisualStudio",
            "Plugins");

    /// <summary>
    /// Returns the paths of all DLL files in the plugin directory.
    /// Returns an empty array when the directory does not exist or cannot be read.
    /// </summary>
    public static string[] GetPluginAssemblyPaths()
    {
        if (!Directory.Exists(PluginDirectory))
        {
            return Array.Empty<string>();
        }

        try
        {
            return Directory.GetFiles(PluginDirectory, "*.dll", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
