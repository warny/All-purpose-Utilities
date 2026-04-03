using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

using Utils.Parser.Runtime;

namespace Utils.Parser.VisualStudio;

/// <summary>
/// Loads syntax colorization profiles from assemblies and descriptor files.
/// </summary>
public sealed class VisualStudioSyntaxColorisationRegistry
{
    private static readonly object ProblematicAssemblyLock = new();
    private static readonly HashSet<string> ProblematicAssemblyPaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Loads all profiles from assemblies and descriptor files.
    /// </summary>
    /// <param name="assemblies">Assemblies to inspect.</param>
    /// <param name="descriptorFiles">Descriptor files to parse.</param>
    /// <returns>Discovered colorization profiles.</returns>
    public IReadOnlyList<ISyntaxColorisation> LoadProfiles(IEnumerable<Assembly> assemblies, IEnumerable<string> descriptorFiles)
    {
        return LoadProfiles(assemblies, descriptorFiles, Array.Empty<string>());
    }

    /// <summary>
    /// Loads all profiles from assemblies, descriptor files, and assembly file paths.
    /// </summary>
    /// <param name="assemblies">Already loaded assemblies to inspect.</param>
    /// <param name="descriptorFiles">Descriptor files to parse.</param>
    /// <param name="assemblyFilePaths">Assembly file paths that should be loaded when not already in memory.</param>
    /// <returns>Discovered colorization profiles.</returns>
    internal IReadOnlyList<ISyntaxColorisation> LoadProfiles(
        IEnumerable<Assembly> assemblies,
        IEnumerable<string> descriptorFiles,
        IEnumerable<string> assemblyFilePaths)
    {
        var profiles = new List<ISyntaxColorisation>();
        profiles.AddRange(DiscoverFromAssemblies(assemblies.Distinct()));
        profiles.AddRange(DiscoverFromExternalAssemblyFiles(assemblyFilePaths));
        profiles.AddRange(LoadFromDescriptorFiles(descriptorFiles));
        return profiles;
    }

    /// <summary>
    /// Discovers profiles from external assembly file paths using collectible load contexts.
    /// </summary>
    /// <param name="assemblyFilePaths">Assembly file paths to inspect.</param>
    /// <returns>Discovered external profiles.</returns>
    private static IReadOnlyList<ISyntaxColorisation> DiscoverFromExternalAssemblyFiles(IEnumerable<string> assemblyFilePaths)
    {
        var profiles = new List<ISyntaxColorisation>();

        foreach (string filePath in assemblyFilePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || IsAssemblyProblematic(filePath))
            {
                continue;
            }

            if (TryGetLoadedAssembly(filePath) is Assembly loadedAssembly)
            {
                profiles.AddRange(DiscoverSafeProfiles(loadedAssembly));
                continue;
            }

            CollectibleProfileLoadContext? loadContext = null;
            try
            {
                loadContext = new CollectibleProfileLoadContext(filePath);
                Assembly assembly = loadContext.LoadFromAssemblyPath(filePath);
                profiles.AddRange(DiscoverCollectibleProfiles(assembly, filePath, loadContext));
            }
            catch (Exception ex)
            {
                MarkAssemblyAsProblematic(filePath, $"Failed to load external colorisation assembly '{filePath}': {ex.Message}");
                loadContext?.Unload();
            }
        }

        return profiles;
    }

    /// <summary>
    /// Tries to get an already loaded assembly matching the provided file path.
    /// </summary>
    /// <param name="assemblyFilePath">Assembly file path.</param>
    /// <returns>Matching loaded assembly, or <see langword="null"/> when unavailable.</returns>
    private static Assembly? TryGetLoadedAssembly(string assemblyFilePath)
    {
        try
        {
            AssemblyName targetName = AssemblyName.GetAssemblyName(assemblyFilePath);
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(assembly => string.Equals(assembly.FullName, targetName.FullName, StringComparison.Ordinal));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Determines whether an assembly path is marked as problematic.
    /// </summary>
    /// <param name="assemblyFilePath">Assembly file path.</param>
    /// <returns><see langword="true"/> when the assembly is blacklisted; otherwise <see langword="false"/>.</returns>
    private static bool IsAssemblyProblematic(string assemblyFilePath)
    {
        lock (ProblematicAssemblyLock)
        {
            return ProblematicAssemblyPaths.Contains(assemblyFilePath);
        }
    }

    /// <summary>
    /// Marks an external assembly as problematic and emits a warning.
    /// </summary>
    /// <param name="assemblyFilePath">Assembly file path.</param>
    /// <param name="warningMessage">Warning message.</param>
    private static void MarkAssemblyAsProblematic(string assemblyFilePath, string warningMessage)
    {
        lock (ProblematicAssemblyLock)
        {
            ProblematicAssemblyPaths.Add(assemblyFilePath);
        }

        Trace.TraceWarning(warningMessage);
    }

    /// <summary>
    /// Discovers wrapped profiles from an already loaded assembly.
    /// </summary>
    /// <param name="assembly">Assembly to inspect.</param>
    /// <returns>Discovered safe profiles.</returns>
    private static IReadOnlyList<ISyntaxColorisation> DiscoverSafeProfiles(Assembly assembly)
    {
        var profiles = new List<ISyntaxColorisation>();

        foreach (Type type in GetLoadableTypes(assembly))
        {
            if (type.IsAbstract || !typeof(ISyntaxColorisation).IsAssignableFrom(type))
            {
                continue;
            }

            if (TryCreateProfile(type, out ISyntaxColorisation? profile, out _))
            {
                profiles.Add(new SafeSyntaxColorisation(profile!));
            }
        }

        return profiles;
    }

    /// <summary>
    /// Discovers wrapped profiles from a collectible load context.
    /// </summary>
    /// <param name="assembly">Assembly to inspect.</param>
    /// <param name="assemblyFilePath">Source assembly file path.</param>
    /// <param name="loadContext">Load context used for the assembly.</param>
    /// <returns>Discovered safe profiles.</returns>
    private static IReadOnlyList<ISyntaxColorisation> DiscoverCollectibleProfiles(
        Assembly assembly,
        string assemblyFilePath,
        CollectibleProfileLoadContext loadContext)
    {
        var profiles = new List<ISyntaxColorisation>();

        foreach (Type type in GetLoadableTypes(assembly))
        {
            if (type.IsAbstract || !typeof(ISyntaxColorisation).IsAssignableFrom(type))
            {
                continue;
            }

            if (TryCreateProfile(type, out ISyntaxColorisation? profile, out string? error))
            {
                profiles.Add(new IsolatedSafeSyntaxColorisation(profile!, assemblyFilePath, loadContext));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                MarkAssemblyAsProblematic(assemblyFilePath, error!);
                loadContext.Unload();
                return Array.Empty<ISyntaxColorisation>();
            }
        }

        if (profiles.Count == 0)
        {
            loadContext.Unload();
        }

        return profiles;
    }

    /// <summary>
    /// Discovers profiles by scanning loaded assemblies.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan.</param>
    /// <returns>Discovered profiles.</returns>
    public IReadOnlyList<ISyntaxColorisation> DiscoverFromAssemblies(IEnumerable<Assembly> assemblies)
    {
        var profiles = new List<ISyntaxColorisation>();
        var errors = new List<string>();

        foreach (Assembly assembly in assemblies.Distinct())
        {
            foreach (Type type in GetLoadableTypes(assembly))
            {
                if (type.IsAbstract || !typeof(ISyntaxColorisation).IsAssignableFrom(type))
                {
                    continue;
                }

                if (TryCreateProfile(type, out ISyntaxColorisation? profile, out string? error))
                {
                    profiles.Add(new SafeSyntaxColorisation(profile!));
                }
                else if (!string.IsNullOrWhiteSpace(error))
                {
                    errors.Add(error!);
                }
            }
        }

        ThrowIfErrors(errors);
        return profiles;
    }

    /// <summary>
    /// Loads profiles from descriptor files.
    /// </summary>
    /// <param name="descriptorFiles">Descriptor file paths.</param>
    /// <returns>Loaded profiles.</returns>
    public IReadOnlyList<ISyntaxColorisation> LoadFromDescriptorFiles(IEnumerable<string> descriptorFiles)
    {
        var parser = new SyntaxColorizationDescriptorFileParser();
        var profiles = new List<ISyntaxColorisation>();
        var errors = new List<string>();

        foreach (string descriptorFile in descriptorFiles.Where(File.Exists))
        {
            try
            {
                SyntaxColorizationDescriptor descriptor = parser.ParseFile(descriptorFile);
                profiles.Add(new SafeSyntaxColorisation(new DescriptorSyntaxColorisation(descriptor)));
            }
            catch (Exception ex)
            {
                errors.Add($"Descriptor '{descriptorFile}' is invalid: {ex.Message}");
            }
        }

        ThrowIfErrors(errors);
        return profiles;
    }

    /// <summary>
    /// Tries to instantiate one colorization profile from a runtime type.
    /// </summary>
    /// <param name="profileType">Type to instantiate.</param>
    /// <param name="profile">Created profile instance.</param>
    /// <returns><see langword="true"/> when an instance is created; otherwise <see langword="false"/>.</returns>
    private static bool TryCreateProfile(Type profileType, out ISyntaxColorisation? profile, out string? error)
    {
        try
        {
            PropertyInfo? instanceProperty = profileType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProperty?.PropertyType != null && typeof(ISyntaxColorisation).IsAssignableFrom(instanceProperty.PropertyType))
            {
                profile = instanceProperty.GetValue(null) as ISyntaxColorisation;
                error = null;
                return profile != null;
            }

            ConstructorInfo? constructor = profileType.GetConstructor(Type.EmptyTypes);
            if (constructor != null)
            {
                profile = constructor.Invoke(null) as ISyntaxColorisation;
                error = null;
                return profile != null;
            }
        }
        catch (Exception ex)
        {
            profile = null;
            error = $"Failed to create syntax colorization profile '{profileType.FullName}': {ex.Message}";
            return false;
        }

        profile = null;
        error = null;
        return false;
    }

    /// <summary>
    /// Throws one exception containing every profile loading error.
    /// </summary>
    /// <param name="errors">Errors discovered while loading profiles.</param>
    private static void ThrowIfErrors(IReadOnlyList<string> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "One or more syntax colorization profiles could not be loaded:" + Environment.NewLine +
            string.Join(Environment.NewLine, errors));
    }

    /// <summary>
    /// Returns loadable types from an assembly, handling partial reflection load failures.
    /// </summary>
    /// <param name="assembly">Assembly to inspect.</param>
    /// <returns>Loadable types.</returns>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type != null)!;
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }

    /// <summary>
    /// Wraps a profile and protects callers from exceptions thrown by profile implementations.
    /// </summary>
    private sealed class SafeSyntaxColorisation : ISyntaxColorisation
    {
        private readonly ISyntaxColorisation inner;

        /// <summary>
        /// Initializes a new instance of the <see cref="SafeSyntaxColorisation"/> class.
        /// </summary>
        /// <param name="inner">Wrapped profile.</param>
        public SafeSyntaxColorisation(ISyntaxColorisation inner)
        {
            this.inner = inner;
        }

        /// <inheritdoc />
        public IReadOnlyList<string> FileExtensions
        {
            get
            {
                try
                {
                    return inner.FileExtensions;
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<string> StringSyntaxExtensions
        {
            get
            {
                try
                {
                    return inner.StringSyntaxExtensions;
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }
        }

        /// <inheritdoc />
        public string? GetClassification(IEnumerable<string> rulePath)
        {
            try
            {
                return inner.GetClassification(rulePath);
            }
            catch
            {
                return null;
            }
        }

        /// <inheritdoc />
        public string? GetClassification(string ruleName)
        {
            try
            {
                return inner.GetClassification(ruleName);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Wraps a profile loaded from a collectible context and blacklists its assembly when it fails.
    /// </summary>
    private sealed class IsolatedSafeSyntaxColorisation : ISyntaxColorisation
    {
        private ISyntaxColorisation? inner;
        private CollectibleProfileLoadContext? loadContext;
        private readonly string assemblyFilePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="IsolatedSafeSyntaxColorisation"/> class.
        /// </summary>
        /// <param name="inner">Wrapped profile.</param>
        /// <param name="assemblyFilePath">Profile assembly file path.</param>
        /// <param name="loadContext">Collectible assembly load context.</param>
        public IsolatedSafeSyntaxColorisation(ISyntaxColorisation inner, string assemblyFilePath, CollectibleProfileLoadContext loadContext)
        {
            this.inner = inner;
            this.assemblyFilePath = assemblyFilePath;
            this.loadContext = loadContext;
        }

        /// <inheritdoc />
        public IReadOnlyList<string> FileExtensions => Execute(profile => profile.FileExtensions, Array.Empty<string>());

        /// <inheritdoc />
        public IReadOnlyList<string> StringSyntaxExtensions => Execute(profile => profile.StringSyntaxExtensions, Array.Empty<string>());

        /// <inheritdoc />
        public string? GetClassification(IEnumerable<string> rulePath) => Execute(profile => profile.GetClassification(rulePath), null);

        /// <inheritdoc />
        public string? GetClassification(string ruleName) => Execute(profile => profile.GetClassification(ruleName), null);

        /// <summary>
        /// Executes profile access and handles failures by blacklisting and unloading the assembly.
        /// </summary>
        /// <typeparam name="T">Return type.</typeparam>
        /// <param name="action">Profile access action.</param>
        /// <param name="fallback">Fallback value on failure.</param>
        /// <returns>Action result or fallback.</returns>
        private T Execute<T>(Func<ISyntaxColorisation, T> action, T fallback)
        {
            if (inner == null)
            {
                return fallback;
            }

            try
            {
                return action(inner);
            }
            catch (Exception ex)
            {
                MarkAssemblyAsProblematic(
                    assemblyFilePath,
                    $"Syntax colorisation assembly '{assemblyFilePath}' failed and will be disabled: {ex.Message}");

                inner = null;
                loadContext?.Unload();
                loadContext = null;
                return fallback;
            }
        }
    }

    /// <summary>
    /// Collectible context used to load external syntax colorisation assemblies.
    /// </summary>
    private sealed class CollectibleProfileLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver resolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="CollectibleProfileLoadContext"/> class.
        /// </summary>
        /// <param name="mainAssemblyPath">Main assembly path loaded in this context.</param>
        public CollectibleProfileLoadContext(string mainAssemblyPath)
            : base($"SyntaxColorisation:{Path.GetFileNameWithoutExtension(mainAssemblyPath)}", isCollectible: true)
        {
            resolver = new AssemblyDependencyResolver(mainAssemblyPath);
        }

        /// <inheritdoc />
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            Assembly? defaultAssembly = AssemblyLoadContext.Default
                .Assemblies
                .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));

            if (defaultAssembly != null)
            {
                return defaultAssembly;
            }

            string? assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
        }
    }
}
