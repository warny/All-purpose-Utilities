using System.Reflection;
using System.Runtime.Loader;

namespace Utils.Parser.VisualStudio.Worker;

/// <summary>
/// Isolated, collectible assembly load context for a single user plugin assembly.
/// Allows the plugin to be unloaded without restarting the worker process.
/// </summary>
internal sealed class PluginAssemblyContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver resolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginAssemblyContext"/> class
    /// and creates a dependency resolver rooted at <paramref name="pluginPath"/>.
    /// </summary>
    /// <param name="pluginPath">Full path to the plugin assembly.</param>
    public PluginAssemblyContext(string pluginPath)
        : base(name: pluginPath, isCollectible: true)
    {
        resolver = new AssemblyDependencyResolver(pluginPath);
    }

    /// <summary>
    /// Resolves an assembly for this isolated context.
    /// <c>Utils.Parser</c> is intentionally forwarded to the default context so that
    /// <see cref="Utils.Parser.Runtime.ISyntaxColorisation"/> type identity is preserved
    /// across the isolation boundary.
    /// </summary>
    /// <param name="assemblyName">The name of the assembly to resolve.</param>
    /// <returns>
    /// The resolved <see cref="Assembly"/>, or <see langword="null"/> to fall back
    /// to the default context.
    /// </returns>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Utils.Parser must be shared with the worker's default context so that
        // ISyntaxColorisation type identity is preserved across the isolation boundary.
        if (string.Equals(assemblyName.Name, "Utils.Parser", System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? path = resolver.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }
}
