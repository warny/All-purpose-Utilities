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

    public PluginAssemblyContext(string pluginPath)
        : base(name: pluginPath, isCollectible: true)
    {
        resolver = new AssemblyDependencyResolver(pluginPath);
    }

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
