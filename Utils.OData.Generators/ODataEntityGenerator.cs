using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Utils.OData.Metadatas;

namespace Utils.OData.Generators;

/// <summary>
/// Generates strongly typed OData entity classes for contexts derived from <c>ODataContext</c>.
/// </summary>
[Generator]
public sealed class ODataEntityGenerator : IIncrementalGenerator
{
    // C# keywords that require the @ verbatim prefix when used as identifiers (item 51).
    private static readonly HashSet<string> CSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
        "checked", "class", "const", "continue", "decimal", "default", "delegate", "do",
        "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
        "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int",
        "interface", "internal", "is", "lock", "long", "namespace", "new", "null",
        "object", "operator", "out", "override", "params", "private", "protected",
        "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
        "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
        "virtual", "void", "volatile", "while"
    };

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax classDeclaration && classDeclaration.BaseList is not null,
                static (generatorContext, _) => (ClassDeclarationSyntax)generatorContext.Node)
            .Collect();

        var generationInputs = context.CompilationProvider
            .Combine(candidateClasses)
            .Combine(context.AnalyzerConfigOptionsProvider);

        context.RegisterSourceOutput(generationInputs, static (productionContext, source) =>
        {
            EmitSources(productionContext, source.Left.Left, source.Left.Right, source.Right);
        });
    }

    /// <summary>
    /// Emits generated OData entity types for each context derived from <c>ODataContext</c>.
    /// </summary>
    /// <param name="context">Context used to report diagnostics and add generated source.</param>
    /// <param name="compilation">Compilation currently being analyzed.</param>
    /// <param name="candidates">Candidate class declarations collected by syntax filtering.</param>
    /// <param name="optionsProvider">Analyzer config options provider for the current compilation.</param>
    private static void EmitSources(SourceProductionContext context, Compilation compilation, IEnumerable<ClassDeclarationSyntax> candidates, AnalyzerConfigOptionsProvider optionsProvider)
    {
        var odataContextSymbol = compilation.GetTypeByMetadataName("Utils.OData.ODataContext");
        if (odataContextSymbol is null)
        {
            return;
        }

        foreach (var candidate in candidates)
        {
            var semanticModel = compilation.GetSemanticModel(candidate.SyntaxTree);
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

            if (!TryExtractMetadataPath(classSymbol, compilation, out var metadataPath))
            {
                ReportDiagnostic(context, candidate.Identifier.GetLocation(), "ODATA002", "Unable to locate a base constructor call that provides the EDMX path.");
                continue;
            }

            if (!TryLoadMetadata(metadataPath, context, optionsProvider, out var metadata, out var error))
            {
                ReportDiagnostic(context, candidate.Identifier.GetLocation(), "ODATA003", error ?? "Unable to load EDMX metadata.");
                continue;
            }

            var source = GenerateSource(classSymbol, metadata!);
            // Item 50: use the fully qualified metadata name as the hint name to avoid
            // collisions when two contexts share the same simple class name across namespaces
            // or containing types.
            string hintName = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", string.Empty)
                .Replace('<', '_')
                .Replace('>', '_')
                .Replace(',', '_')
                .Replace(' ', '_');
            context.AddSource($"{hintName}.ODataEntities.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    /// <summary>
    /// Reports a diagnostic message from the generator.
    /// </summary>
    /// <param name="context">The current generator execution context.</param>
    /// <param name="location">The location associated with the diagnostic.</param>
    /// <param name="id">Identifier of the diagnostic.</param>
    /// <param name="message">Text describing the diagnostic.</param>
    private static void ReportDiagnostic(SourceProductionContext context, Location location, string id, string message)
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
    /// Attempts to load EDMX metadata from a file system path.
    /// </summary>
    /// <remarks>
    /// Item 49: remote HTTP(S) metadata downloads are not supported in the source generator.
    /// Network access during compilation makes builds non-hermetic, introduces non-determinism,
    /// and can cause indefinite build hangs.  Supply the EDMX as a local file or
    /// <c>AdditionalFile</c> instead.
    /// </remarks>
    /// <param name="metadataPath">The path provided in the derived context.</param>
    /// <param name="context">Current generator execution context.</param>
    /// <param name="optionsProvider">Analyzer config options provider used to resolve project paths.</param>
    /// <param name="metadata">The resulting metadata instance when successful.</param>
    /// <param name="error">Optional concise error message describing any failure (item 56).</param>
    /// <returns><see langword="true"/> when the metadata could be loaded.</returns>
    private static bool TryLoadMetadata(string metadataPath, SourceProductionContext context, AnalyzerConfigOptionsProvider optionsProvider, out Edmx? metadata, out string? error)
    {
        metadata = null;
        error = null;

        try
        {
            // Item 49: reject remote URLs — network I/O in source generators is not allowed.
            if (Uri.TryCreate(metadataPath, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                error = $"Remote EDMX URL '{uri.Host}/...' is not supported in source generators. " +
                        "Supply the EDMX as a local file or AdditionalFile instead.";
                return false;
            }

            string? projectDir = null;
            if (optionsProvider.GlobalOptions.TryGetValue("build_property.ProjectDir", out var configuredDir))
            {
                projectDir = configuredDir;
            }
            else if (optionsProvider.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var msbuildDir))
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
                error = $"EDMX file '{Path.GetFileName(resolvedPath)}' was not found.";
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
        catch (UnauthorizedAccessException)
        {
            // Item 56: emit a concise, stable message rather than ex.ToString() which can
            // leak filesystem paths, stack traces, or environment-specific details.
            error = $"Access denied when reading EDMX file '{Path.GetFileName(metadataPath)}'.";
            return false;
        }
        catch (IOException ex)
        {
            error = $"I/O error reading EDMX file '{Path.GetFileName(metadataPath)}': {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Unexpected error loading EDMX metadata: {ex.GetType().Name} — {ex.Message}";
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

        // Item 50: emit containing-type partial declarations so that nested context
        // classes produce valid C# (the generated partial must be inside the same
        // containing type hierarchy).
        var containingTypes = new Stack<INamedTypeSymbol>();
        var containing = classSymbol.ContainingType;
        while (containing is not null)
        {
            containingTypes.Push(containing);
            containing = containing.ContainingType;
        }

        int nestingDepth = 0;
        foreach (var containingType in containingTypes)
        {
            string indent = new string(' ', nestingDepth * 4);
            builder.Append(indent);
            builder.Append(AccessibilityKeyword(containingType.DeclaredAccessibility));
            builder.Append(" partial ");
            builder.Append(containingType.IsRecord ? "record " : "class ");
            builder.Append(SanitizeIdentifier(containingType.Name));
            builder.AppendLine();
            builder.Append(indent);
            builder.AppendLine("{");
            nestingDepth++;
        }

        string contextIndent = new string(' ', nestingDepth * 4);
        // Item 50: use the declared accessibility instead of hardcoding "public".
        builder.Append(contextIndent);
        builder.Append(AccessibilityKeyword(classSymbol.DeclaredAccessibility));
        builder.Append(" partial class ");
        builder.Append(SanitizeIdentifier(classSymbol.Name));
        builder.AppendLine();
        builder.Append(contextIndent);
        builder.AppendLine("{");

        string entityIndent = contextIndent + "    ";
        string memberIndent = entityIndent + "    ";

        foreach (var entity in entities)
        {
            if (string.IsNullOrEmpty(entity.Name))
                continue;

            string entityIdentifier = SanitizeIdentifier(entity.Name);

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

            builder.Append(entityIndent);
            builder.AppendLine("/// <summary>");
            builder.Append(entityIndent);
            builder.Append("/// Represents the OData entity type ");
            builder.Append(entity.Name);
            builder.AppendLine(".");
            builder.Append(entityIndent);
            builder.AppendLine("/// </summary>");
            builder.Append(entityIndent);
            builder.Append("public partial class ");
            builder.Append(entityIdentifier);
            builder.AppendLine();
            builder.Append(entityIndent);
            builder.AppendLine("{");

            foreach (var property in entity.Properties ?? Array.Empty<Property>())
            {
                AppendProperty(builder, property, keyNames.Contains(property.Name), memberIndent);
            }

            builder.Append(entityIndent);
            builder.AppendLine("}");
            builder.AppendLine();
        }

        builder.Append(contextIndent);
        builder.AppendLine("}");

        // Close containing-type braces in reverse order.
        for (int i = nestingDepth - 1; i >= 0; i--)
        {
            string indent = new string(' ', i * 4);
            builder.Append(indent);
            builder.AppendLine("}");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Appends the code of a single property to the generated class.
    /// </summary>
    /// <param name="builder">Target string builder.</param>
    /// <param name="property">Property information extracted from the metadata.</param>
    /// <param name="isKey">Indicates whether the property is a key member.</param>
    /// <param name="indent">Indentation string prepended to each line.</param>
    private static void AppendProperty(StringBuilder builder, Property property, bool isKey, string indent)
    {
        if (string.IsNullOrEmpty(property.Name))
            return;

        var (typeName, isValueType) = MapEdmType(property.Type);

        // Item 52: derive nullability from the EDM Nullable facet; do not infer it from
        // key membership alone.  Key properties are not nullable by the OData key contract,
        // but non-key properties can be declared non-nullable via Nullable="false".
        bool isNullable;
        if (isKey)
        {
            // Keys are always non-nullable in OData regardless of the Nullable facet.
            isNullable = false;
        }
        else
        {
            // Honour the EDM Nullable facet; default to true when absent (OData default).
            isNullable = property.IsNullable;
        }

        string resolvedType = typeName;
        if (isNullable && isValueType)
        {
            resolvedType = typeName + "?";
        }
        else if (isNullable && !typeName.EndsWith("?", StringComparison.Ordinal))
        {
            resolvedType = typeName + "?";
        }

        string propertyIdentifier = SanitizeIdentifier(property.Name);

        builder.Append(indent);
        builder.AppendLine("/// <summary>");
        builder.Append(indent);
        builder.Append("/// Gets or sets the ");
        builder.Append(property.Name);
        builder.AppendLine(" value.");
        builder.Append(indent);
        builder.AppendLine("/// </summary>");

        if (isKey)
        {
            builder.Append(indent);
            builder.AppendLine("[System.ComponentModel.DataAnnotations.Key]");
        }

        builder.Append(indent);
        builder.Append("public ");
        builder.Append(resolvedType);
        builder.Append(' ');
        builder.Append(propertyIdentifier);
        builder.AppendLine(" { get; set; }");
        builder.AppendLine();
    }

    /// <summary>
    /// Maps an EDM primitive type to a .NET type representation.
    /// </summary>
    /// <remarks>
    /// Item 52: <c>Edm.Date</c> maps to <c>System.DateOnly</c> and <c>Edm.TimeOfDay</c> maps
    /// to <c>System.TimeOnly</c> to match the runtime converter (items 23/24).
    /// </remarks>
    /// <param name="edmType">EDM type name as declared in the metadata.</param>
    /// <returns>Tuple containing the .NET type name and a flag indicating whether it is a value type.</returns>
    private static (string TypeName, bool IsValueType) MapEdmType(string? edmType)
    {
        return edmType switch
        {
            "Edm.Boolean" => ("bool", true),
            "Edm.Byte" => ("byte", true),
            // Item 52: aligned with item 23 — Edm.Date → DateOnly (no artificial midnight).
            "Edm.Date" => ("System.DateOnly", true),
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
            // Item 52: aligned with item 24 — Edm.TimeOfDay → TimeOnly.
            "Edm.TimeOfDay" => ("System.TimeOnly", true),
            "Edm.Binary" => ("byte[]", false),
            _ => ("object", false)
        };
    }

    /// <summary>
    /// Converts an EDM accessibility to the corresponding C# keyword.
    /// </summary>
    /// <param name="accessibility">Symbol accessibility from the Roslyn model.</param>
    /// <returns>The C# accessibility keyword string.</returns>
    private static string AccessibilityKeyword(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.Private => "private",
            _ => "public"
        };

    /// <summary>
    /// Converts an EDM name into a valid C# identifier (item 51).
    /// </summary>
    /// <remarks>
    /// Rules applied:
    /// <list type="bullet">
    /// <item>C# keywords are prefixed with <c>@</c>.</item>
    /// <item>Characters that are not letters, digits, or underscores are replaced with <c>_</c>.</item>
    /// <item>Identifiers that begin with a digit are prefixed with <c>_</c>.</item>
    /// <item>Empty names become <c>_unnamed</c>.</item>
    /// </list>
    /// </remarks>
    private static string SanitizeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "_unnamed";

        var sb = new StringBuilder(name.Length + 1);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsLetterOrDigit(c) || c == '_')
                sb.Append(c);
            else
                sb.Append('_');
        }

        if (sb.Length == 0)
            return "_unnamed";

        // Prefix with _ if the first character is a digit.
        if (char.IsDigit(sb[0]))
            sb.Insert(0, '_');

        string identifier = sb.ToString();

        // Escape C# keywords.
        if (CSharpKeywords.Contains(identifier))
            return "@" + identifier;

        return identifier;
    }
}
