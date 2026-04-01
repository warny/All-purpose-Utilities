using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Utils.Parser.Runtime;

namespace Utils.Parser.VisualStudio;

/// <summary>
/// Loads syntax colorization profiles from assemblies and descriptor files.
/// </summary>
public sealed class VisualStudioSyntaxColorisationRegistry
{
    /// <summary>
    /// Loads all profiles from assemblies and descriptor files.
    /// </summary>
    /// <param name="assemblies">Assemblies to inspect.</param>
    /// <param name="descriptorFiles">Descriptor files to parse.</param>
    /// <returns>Discovered colorization profiles.</returns>
    public IReadOnlyList<ISyntaxColorisation> LoadProfiles(IEnumerable<Assembly> assemblies, IEnumerable<string> descriptorFiles)
    {
        var profiles = new List<ISyntaxColorisation>();
        profiles.AddRange(DiscoverFromAssemblies(assemblies));
        profiles.AddRange(LoadFromDescriptorFiles(descriptorFiles));
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
}
