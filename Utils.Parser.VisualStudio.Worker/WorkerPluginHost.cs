using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

using Utils.Parser.Runtime;

namespace Utils.Parser.VisualStudio.Worker;

/// <summary>
/// Loads user plugin assemblies in isolated contexts and classifies tokens on behalf of the extension.
/// </summary>
internal sealed class WorkerPluginHost
{
    private readonly Dictionary<string, CachedPlugin> cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Classifies a batch of tokens using the ISyntaxColorisation profiles found in the requested assemblies.
    /// </summary>
    public ClassifyResponse Classify(ClassifyRequest request)
    {
        try
        {
            var profiles = LoadMatchingProfiles(request.AssemblyPaths, request.FileExtension);
            var result = new Dictionary<string, string?>(request.Tokens.Length, StringComparer.Ordinal);

            foreach (string token in request.Tokens)
            {
                result[token] = ResolveClassification(token, profiles);
            }

            return new ClassifyResponse(request.Id, result);
        }
        catch (Exception ex)
        {
            return new ClassifyResponse(request.Id, null, ex.Message);
        }
    }

    /// <summary>
    /// Loads the <see cref="ISyntaxColorisation"/> profiles from each assembly in
    /// <paramref name="assemblyPaths"/> whose declared file extensions include
    /// <paramref name="fileExtension"/>.
    /// </summary>
    /// <param name="assemblyPaths">Paths of the plugin assemblies to load from.</param>
    /// <param name="fileExtension">File extension to filter profiles by (e.g. <c>".sql"</c>).</param>
    /// <returns>All matching profiles, in assembly order.</returns>
    private List<ISyntaxColorisation> LoadMatchingProfiles(string[] assemblyPaths, string fileExtension)
    {
        var profiles = new List<ISyntaxColorisation>();

        foreach (string path in assemblyPaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            CachedPlugin cached = GetOrLoad(path);
            profiles.AddRange(cached.Profiles.Where(p =>
                p.FileExtensions.Any(ext => string.Equals(ext, fileExtension, StringComparison.OrdinalIgnoreCase))));
        }

        return profiles;
    }

    /// <summary>
    /// Returns the cached plugin entry for <paramref name="assemblyPath"/>, reloading
    /// from disk when the file's last-write timestamp has changed (hot-reload).
    /// </summary>
    /// <param name="assemblyPath">Full path to the plugin assembly.</param>
    /// <returns>The up-to-date <see cref="CachedPlugin"/> entry.</returns>
    private CachedPlugin GetOrLoad(string assemblyPath)
    {
        DateTime modifiedUtc = File.GetLastWriteTimeUtc(assemblyPath);

        if (cache.TryGetValue(assemblyPath, out CachedPlugin? cached) && cached.ModifiedUtc == modifiedUtc)
        {
            return cached;
        }

        // Unload previous context when the file has changed, enabling hot-reload and quarantine.
        if (cached is not null)
        {
            cache.Remove(assemblyPath);
            cached.Profiles.Clear();
            cached.Context.Unload();
        }

        var context = new PluginAssemblyContext(assemblyPath);
        Assembly assembly = context.LoadFromAssemblyPath(assemblyPath);
        List<ISyntaxColorisation> profiles = DiscoverProfiles(assembly);
        var entry = new CachedPlugin(context, profiles, modifiedUtc);
        cache[assemblyPath] = entry;
        return entry;
    }

    /// <summary>
    /// Discovers all <see cref="ISyntaxColorisation"/> implementations in
    /// <paramref name="assembly"/> by reflection, preferring a public static
    /// <c>Instance</c> property over the parameterless constructor.
    /// Types that fail to instantiate are silently skipped.
    /// </summary>
    /// <param name="assembly">The assembly to inspect.</param>
    /// <returns>All successfully instantiated profiles.</returns>
    private static List<ISyntaxColorisation> DiscoverProfiles(Assembly assembly)
    {
        var profiles = new List<ISyntaxColorisation>();

        IEnumerable<Type> types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null)!;
        }
        catch
        {
            return profiles;
        }

        foreach (Type type in types)
        {
            if (type.IsAbstract || !typeof(ISyntaxColorisation).IsAssignableFrom(type))
            {
                continue;
            }

            try
            {
                PropertyInfo? instanceProp = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp?.PropertyType is not null &&
                    typeof(ISyntaxColorisation).IsAssignableFrom(instanceProp.PropertyType))
                {
                    if (instanceProp.GetValue(null) is ISyntaxColorisation p)
                    {
                        profiles.Add(p);
                        continue;
                    }
                }

                if (type.GetConstructor(Type.EmptyTypes) is ConstructorInfo ctor &&
                    ctor.Invoke(null) is ISyntaxColorisation instance)
                {
                    profiles.Add(instance);
                }
            }
            catch
            {
                // Skip plugins that fail to instantiate.
            }
        }

        return profiles;
    }

    /// <summary>
    /// Returns the first non-empty classification for <paramref name="token"/> found
    /// across <paramref name="profiles"/>, trying the token as-is, upper-case, and
    /// title-case in that order.
    /// </summary>
    /// <param name="token">The identifier to classify.</param>
    /// <param name="profiles">Profiles to query in order.</param>
    /// <returns>The classification name, or <see langword="null"/> if none matched.</returns>
    private static string? ResolveClassification(string token, IEnumerable<ISyntaxColorisation> profiles)
    {
        foreach (ISyntaxColorisation profile in profiles)
        {
            try
            {
                string? v = profile.GetClassification(token);
                if (!string.IsNullOrWhiteSpace(v)) return v;

                v = profile.GetClassification(token.ToUpperInvariant());
                if (!string.IsNullOrWhiteSpace(v)) return v;

                v = profile.GetClassification(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(token));
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
            catch
            {
                // Skip broken plugins.
            }
        }

        return null;
    }

    /// <summary>
    /// Holds a loaded plugin assembly and its profiles, keyed by last-write timestamp
    /// for cache invalidation.
    /// </summary>
    private sealed class CachedPlugin
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CachedPlugin"/> class.
        /// </summary>
        /// <param name="context">The isolated assembly load context.</param>
        /// <param name="profiles">Profiles discovered in the assembly.</param>
        /// <param name="modifiedUtc">Last-write timestamp of the assembly file at load time.</param>
        public CachedPlugin(PluginAssemblyContext context, List<ISyntaxColorisation> profiles, DateTime modifiedUtc)
        {
            Context = context;
            Profiles = profiles;
            ModifiedUtc = modifiedUtc;
        }

        /// <summary>Gets the isolated assembly load context.</summary>
        public PluginAssemblyContext Context { get; }

        /// <summary>Gets the colorization profiles discovered in the assembly.</summary>
        public List<ISyntaxColorisation> Profiles { get; }

        /// <summary>Gets the assembly file's last-write timestamp at the time it was loaded.</summary>
        public DateTime ModifiedUtc { get; }
    }
}
