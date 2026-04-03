using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Utils.Parser.Generators.Internal;

/// <summary>
/// Parses syntax colorization descriptor files.
/// </summary>
internal static class SyntaxColorizationDescriptorParser
{
    /// <summary>
    /// Parses descriptor text content.
    /// </summary>
    /// <param name="source">Descriptor source text.</param>
    /// <returns>The parsed descriptor model.</returns>
    public static SyntaxColorizationDescriptor Parse(string source)
    {
        if (TryParseWithBootstrap(source, out SyntaxColorizationDescriptor? descriptor) && descriptor != null)
        {
            return descriptor;
        }

        throw new InvalidOperationException(
            "Unable to parse .syntaxcolor descriptor because Utils.Parser.Bootstrap.SyntaxColorisationGrammar is unavailable.");
    }

    /// <summary>
    /// Tries to parse descriptor content using <c>Utils.Parser.Bootstrap.SyntaxColorisationGrammar</c>.
    /// </summary>
    /// <param name="source">Descriptor source text.</param>
    /// <param name="descriptor">Parsed descriptor when successful.</param>
    /// <returns><see langword="true"/> when the bootstrap parser is available and successful.</returns>
    private static bool TryParseWithBootstrap(string source, out SyntaxColorizationDescriptor? descriptor)
    {
        descriptor = null;

        try
        {
            Assembly? parserAssembly = ResolveParserAssembly();

            if (parserAssembly == null)
            {
                return false;
            }

            Type? grammarType = parserAssembly.GetType("Utils.Parser.Bootstrap.SyntaxColorisationGrammar", throwOnError: false);
            MethodInfo? parseMethod = grammarType?.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (parseMethod == null)
            {
                return false;
            }

            object? document = parseMethod.Invoke(null, new object[] { source });
            if (document == null)
            {
                return false;
            }

            descriptor = MapBootstrapDocument(document);
            return true;
        }
        catch
        {
            descriptor = null;
            return false;
        }
    }

    /// <summary>
    /// Resolves the <c>Utils.Parser</c> assembly from the current load context or from known probing paths.
    /// </summary>
    /// <returns>The resolved parser assembly, or <see langword="null"/> when unavailable.</returns>
    private static Assembly? ResolveParserAssembly()
    {
        Assembly? loadedAssembly = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "Utils.Parser", StringComparison.OrdinalIgnoreCase));

        if (loadedAssembly != null)
        {
            return loadedAssembly;
        }

        try
        {
            return Assembly.Load(new AssemblyName("Utils.Parser"));
        }
        catch
        {
            // Continue with file probing fallback.
        }

        IEnumerable<string> candidateDirectories = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.Location)
            .Where(location => !string.IsNullOrWhiteSpace(location))
            .Select(Path.GetDirectoryName)
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Select(directory => directory!)
            .Append(AppContext.BaseDirectory)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (string directory in candidateDirectories)
        {
            string candidatePath = Path.Combine(directory, "Utils.Parser.dll");
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            try
            {
                return Assembly.LoadFrom(candidatePath);
            }
            catch
            {
                // Try next candidate.
            }
        }

        return null;
    }

    /// <summary>
    /// Maps a bootstrap syntax colorisation document to generator descriptor model.
    /// </summary>
    /// <param name="document">Bootstrap parser document instance.</param>
    /// <returns>Mapped descriptor.</returns>
    private static SyntaxColorizationDescriptor MapBootstrapDocument(object document)
    {
        var descriptor = new SyntaxColorizationDescriptor();

        PropertyInfo fileExtensionsProperty = document.GetType().GetProperty("FileExtensions")!;
        PropertyInfo stringSyntaxExtensionsProperty = document.GetType().GetProperty("StringSyntaxExtensions")!;
        PropertyInfo sectionsProperty = document.GetType().GetProperty("Sections")!;

        foreach (string extension in (IEnumerable<string>)fileExtensionsProperty.GetValue(document)!)
        {
            descriptor.FileExtensions.Add(extension);
        }

        foreach (string extension in (IEnumerable<string>)stringSyntaxExtensionsProperty.GetValue(document)!)
        {
            descriptor.StringSyntaxExtensions.Add(extension);
        }

        foreach (object section in (IEnumerable<object>)sectionsProperty.GetValue(document)!)
        {
            PropertyInfo classificationProperty = section.GetType().GetProperty("Classification")!;
            PropertyInfo rulesProperty = section.GetType().GetProperty("Rules")!;

            var entry = new SyntaxColorizationEntry((string)classificationProperty.GetValue(section)!);
            foreach (string rule in (IEnumerable<string>)rulesProperty.GetValue(section)!)
            {
                entry.Rules.Add(rule);
            }

            descriptor.Entries.Add(entry);
        }

        return descriptor;
    }
}
