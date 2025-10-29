using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Utils.OData.Metadatas;

namespace Utils.OData.Generators;

/// <summary>
/// Generates strongly typed OData entity classes for contexts derived from <see cref="Utils.OData.ODataContext"/>.
/// </summary>
[Generator]
public sealed class ODataEntityGenerator : ISourceGenerator
{
    /// <inheritdoc />
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(static () => new SyntaxReceiver());
    }

    /// <inheritdoc />
    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not SyntaxReceiver receiver)
        {
            return;
        }

        var odataContextSymbol = context.Compilation.GetTypeByMetadataName("Utils.OData.ODataContext");
        if (odataContextSymbol is null)
        {
            return;
        }

        foreach (var candidate in receiver.Candidates)
        {
            var semanticModel = context.Compilation.GetSemanticModel(candidate.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(candidate) is not INamedTypeSymbol classSymbol)
            {
                continue;
            }

            if (!InheritsFrom(classSymbol, odataContextSymbol))
            {
                continue;
            }

            if (!candidate.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                ReportDiagnostic(context, candidate.Identifier.GetLocation(), "ODATA001", "Derived OData context must be declared partial to allow code generation.");
                continue;
            }

            if (!TryExtractMetadataPath(classSymbol, context.Compilation, out var metadataPath))
            {
                ReportDiagnostic(context, candidate.Identifier.GetLocation(), "ODATA002", "Unable to locate a base constructor call that provides the EDMX path.");
                continue;
            }

            if (!TryLoadMetadata(metadataPath, context, out var metadata, out var error))
            {
                ReportDiagnostic(context, candidate.Identifier.GetLocation(), "ODATA003", error ?? "Unable to load EDMX metadata.");
                continue;
            }

            var source = GenerateSource(classSymbol, metadata!);
            context.AddSource($"{classSymbol.Name}.ODataEntities.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    /// <summary>
    /// Reports a diagnostic message from the generator.
    /// </summary>
    /// <param name="context">The current generator execution context.</param>
    /// <param name="location">The location associated with the diagnostic.</param>
    /// <param name="id">Identifier of the diagnostic.</param>
    /// <param name="message">Text describing the diagnostic.</param>
    private static void ReportDiagnostic(GeneratorExecutionContext context, Location location, string id, string message)
    {
        var descriptor = new DiagnosticDescriptor(id, message, message, "Usage", DiagnosticSeverity.Warning, true);
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location));
    }

    /// <summary>
    /// Determines whether a type inherits from a specified base type.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="baseType">The base type to search for in the hierarchy.</param>
    /// <returns><see langword="true"/> when <paramref name="type"/> inherits from <paramref name="baseType"/>.</returns>
    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        var current = type;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Attempts to extract the metadata path provided to the base constructor.
    /// </summary>
    /// <param name="classSymbol">The derived context symbol.</param>
    /// <param name="compilation">Compilation used to retrieve semantic models.</param>
    /// <param name="metadataPath">Resulting metadata path when available.</param>
    /// <returns><see langword="true"/> when a path could be extracted.</returns>
    private static bool TryExtractMetadataPath(INamedTypeSymbol classSymbol, Compilation compilation, out string metadataPath)
    {
        metadataPath = string.Empty;
        foreach (var syntaxRef in classSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is not ClassDeclarationSyntax classDeclaration)
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
            foreach (var constructor in classDeclaration.Members.OfType<ConstructorDeclarationSyntax>())
            {
                if (constructor.Initializer is null || !constructor.Initializer.IsKind(SyntaxKind.BaseConstructorInitializer))
                {
                    continue;
                }

                if (constructor.Initializer.ArgumentList.Arguments.Count == 0)
                {
                    continue;
                }

                var argument = constructor.Initializer.ArgumentList.Arguments[0].Expression;
                var constantValue = semanticModel.GetConstantValue(argument);
                if (!constantValue.HasValue || constantValue.Value is not string value)
                {
                    continue;
                }

                metadataPath = value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Attempts to load EDMX metadata from a file system path or HTTP resource.
    /// </summary>
    /// <param name="metadataPath">The path or URL provided in the derived context.</param>
    /// <param name="context">Current generator execution context.</param>
    /// <param name="metadata">The resulting metadata instance when successful.</param>
    /// <param name="error">Optional error message describing any failure.</param>
    /// <returns><see langword="true"/> when the metadata could be loaded.</returns>
    private static bool TryLoadMetadata(string metadataPath, GeneratorExecutionContext context, out Edmx? metadata, out string? error)
    {
        metadata = null;
        error = null;

        try
        {
            if (Uri.TryCreate(metadataPath, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                using var httpClient = new HttpClient();
                using var response = httpClient.GetAsync(uri).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    error = $"Failed to download EDMX metadata from '{uri}'.";
                    return false;
                }

                using var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                metadata = DeserializeMetadatas.Deserialize(stream);
                if (metadata is null)
                {
                    error = "The EDMX metadata could not be deserialized.";
                    return false;
                }

                return true;
            }

            string? projectDir = null;
            if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.ProjectDir", out var configuredDir))
            {
                projectDir = configuredDir;
            }
            else if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var msbuildDir))
            {
                projectDir = msbuildDir;
            }

            string resolvedPath = metadataPath;
            if (!Path.IsPathRooted(resolvedPath) && projectDir is not null)
            {
                resolvedPath = Path.Combine(projectDir, metadataPath);
            }
            else if (!Path.IsPathRooted(resolvedPath))
            {
                resolvedPath = Path.GetFullPath(metadataPath);
            }

            var fileInfo = new FileInfo(resolvedPath);
            if (!fileInfo.Exists)
            {
                error = $"EDMX file '{resolvedPath}' was not found.";
                return false;
            }

            using var fileStream = fileInfo.OpenRead();
            metadata = DeserializeMetadatas.Deserialize(fileStream);
            if (metadata is null)
            {
                error = "The EDMX metadata could not be deserialized.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Generates the source code that represents entities from the EDMX metadata.
    /// </summary>
    /// <param name="classSymbol">The derived context for which code is generated.</param>
    /// <param name="metadata">Parsed EDMX metadata.</param>
    /// <returns>The generated C# source code.</returns>
    private static string GenerateSource(INamedTypeSymbol classSymbol, Edmx metadata)
    {
        var entities = metadata.DataServices?
            .SelectMany(ds => ds.Schemas ?? Array.Empty<Schema>())
            .SelectMany(schema => schema.EntityTypes ?? Array.Empty<EntityType>())
            .ToList() ?? new List<EntityType>();

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated>");
        builder.AppendLine("// This code was generated by Utils.OData.Generators.");
        builder.AppendLine("// </auto-generated>");
        builder.AppendLine();
        var namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : classSymbol.ContainingNamespace.ToDisplayString();

        if (!string.IsNullOrEmpty(namespaceName))
        {
            builder.Append("namespace ");
            builder.Append(namespaceName);
            builder.AppendLine(";");
            builder.AppendLine();
        }

        builder.Append("public partial class ");
        builder.Append(classSymbol.Name);
        builder.AppendLine()
            .AppendLine("{");

        foreach (var entity in entities)
        {
            var keyNames = new HashSet<string>(StringComparer.Ordinal);
            if (entity.Key?.PropertyRefs is { Length: > 0 } propertyRefs)
            {
                foreach (var propertyRef in propertyRefs)
                {
                    if (!string.IsNullOrEmpty(propertyRef.Name))
                    {
                        keyNames.Add(propertyRef.Name);
                    }
                }
            }
            builder.AppendLine("    /// <summary>");
            builder.Append("    /// Represents the OData entity type ");
            builder.Append(entity.Name);
            builder.AppendLine(".");
            builder.AppendLine("    /// </summary>");
            builder.Append("    public partial class ");
            builder.Append(entity.Name);
            builder.AppendLine()
                .AppendLine("    {");

            foreach (var property in entity.Properties ?? Array.Empty<Property>())
            {
                AppendProperty(builder, property, keyNames.Contains(property.Name));
            }

            builder.AppendLine("    }");
            builder.AppendLine();
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    /// <summary>
    /// Appends the code of a single property to the generated class.
    /// </summary>
    /// <param name="builder">Target string builder.</param>
    /// <param name="property">Property information extracted from the metadata.</param>
    /// <param name="isKey">Indicates whether the property is a key member.</param>
    private static void AppendProperty(StringBuilder builder, Property property, bool isKey)
    {
        var (typeName, isValueType) = MapEdmType(property.Type);
        string resolvedType = typeName;
        if (!isKey)
        {
            if (isValueType)
            {
                resolvedType = typeName + "?";
            }
            else if (!typeName.EndsWith("?", StringComparison.Ordinal))
            {
                resolvedType = typeName + "?";
            }
        }

        builder.AppendLine("        /// <summary>");
        builder.Append("        /// Gets or sets the ");
        builder.Append(property.Name);
        builder.AppendLine(" value.");
        builder.AppendLine("        /// </summary>");
        if (isKey)
        {
            builder.AppendLine("        [System.ComponentModel.DataAnnotations.Key]");
        }

        builder.Append("        public ");
        builder.Append(resolvedType);
        builder.Append(' ');
        builder.Append(property.Name);
        builder.AppendLine(" { get; set; }");
        builder.AppendLine();
    }

    /// <summary>
    /// Maps an EDM primitive type to a .NET type representation.
    /// </summary>
    /// <param name="edmType">EDM type name as declared in the metadata.</param>
    /// <returns>Tuple containing the .NET type name and a flag indicating whether it is a value type.</returns>
    private static (string TypeName, bool IsValueType) MapEdmType(string? edmType)
    {
        return edmType switch
        {
            "Edm.Boolean" => ("bool", true),
            "Edm.Byte" => ("byte", true),
            "Edm.Date" => ("System.DateTime", true),
            "Edm.DateTimeOffset" => ("System.DateTimeOffset", true),
            "Edm.Decimal" => ("decimal", true),
            "Edm.Double" => ("double", true),
            "Edm.Duration" => ("System.TimeSpan", true),
            "Edm.Guid" => ("System.Guid", true),
            "Edm.Int16" => ("short", true),
            "Edm.Int32" => ("int", true),
            "Edm.Int64" => ("long", true),
            "Edm.SByte" => ("sbyte", true),
            "Edm.Single" => ("float", true),
            "Edm.String" => ("string", false),
            "Edm.TimeOfDay" => ("System.TimeSpan", true),
            "Edm.Binary" => ("byte[]", false),
            _ => ("object", false)
        };
    }

    /// <summary>
    /// Captures class declarations that specify base types.
    /// </summary>
    private sealed class SyntaxReceiver : ISyntaxReceiver
    {
        /// <summary>
        /// Gets the class declarations that derive from <see cref="Utils.OData.ODataContext"/>.
        /// </summary>
        public List<ClassDeclarationSyntax> Candidates { get; } = new();

        /// <inheritdoc />
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is ClassDeclarationSyntax classDeclaration && classDeclaration.BaseList is not null)
            {
                Candidates.Add(classDeclaration);
            }
        }
    }
}
