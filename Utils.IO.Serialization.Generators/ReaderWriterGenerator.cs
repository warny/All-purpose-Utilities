using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Utils.IO.Serialization.Generators;

/// <summary>
/// Generates reader and writer extension methods for types annotated with <c>GenerateReaderWriterAttribute</c>.
/// </summary>
[Generator]
public class ReaderWriterGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax typeDeclaration && typeDeclaration.AttributeLists.Count > 0,
                static (generatorContext, _) => (TypeDeclarationSyntax)generatorContext.Node)
            .Collect();

        var compilationAndCandidates = context.CompilationProvider.Combine(candidateTypes);
        context.RegisterSourceOutput(compilationAndCandidates, static (productionContext, source) =>
        {
            EmitSources(productionContext, source.Left, source.Right);
        });
    }

    /// <summary>
    /// Emits serialization helpers for each type annotated for generation.
    /// </summary>
    /// <param name="context">Context used to publish generated source files.</param>
    /// <param name="compilation">Compilation currently being analyzed.</param>
    /// <param name="candidates">Candidate type declarations discovered by syntax filtering.</param>
    private static void EmitSources(SourceProductionContext context, Compilation compilation, IEnumerable<TypeDeclarationSyntax> candidates)
    {
        var generateAttr = compilation.GetTypeByMetadataName("Utils.IO.Serialization.GenerateReaderWriterAttribute");
        var fieldAttr = compilation.GetTypeByMetadataName("Utils.IO.Serialization.FieldAttribute");
        var iReader = compilation.GetTypeByMetadataName("Utils.IO.Serialization.IReader");
        var iWriter = compilation.GetTypeByMetadataName("Utils.IO.Serialization.IWriter");
        if (generateAttr is null || fieldAttr is null || iReader is null || iWriter is null)
        {
            return;
        }

        // Determines whether a type has a dedicated reader method or is marked for generation.
        bool HasCustomReader(ITypeSymbol type)
        {
            if (type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, generateAttr)))
            {
                return true;
            }

            string methodName = "Read" + type.Name;
            foreach (var symbol in compilation.GetSymbolsWithName(methodName, SymbolFilter.Member, context.CancellationToken))
            {
                if (symbol is IMethodSymbol method &&
                    method.IsExtensionMethod &&
                    method.Parameters.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, iReader) &&
                    SymbolEqualityComparer.Default.Equals(method.ReturnType, type))
                {
                    return true;
                }
            }
            return false;
        }

        // Determines whether a type has a dedicated writer method or is marked for generation.
        bool HasCustomWriter(ITypeSymbol type)
        {
            if (type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, generateAttr)))
            {
                return true;
            }

            string methodName = "Write" + type.Name;
            foreach (var symbol in compilation.GetSymbolsWithName(methodName, SymbolFilter.Member, context.CancellationToken))
            {
                if (symbol is IMethodSymbol method &&
                    method.IsExtensionMethod &&
                    method.Parameters.Length == 2 &&
                    SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, iWriter) &&
                    SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, type))
                {
                    return true;
                }
            }
            return false;
        }

        foreach (var candidate in candidates)
        {
            var model = compilation.GetSemanticModel(candidate.SyntaxTree);
            if (model.GetDeclaredSymbol(candidate) is not INamedTypeSymbol typeSymbol)
            {
                continue;
            }

            if (!typeSymbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, generateAttr)))
            {
                continue;
            }

            var members = typeSymbol.GetMembers()
                .Where(m => m is IPropertySymbol or IFieldSymbol)
                .Select(m => new
                {
                    Symbol = m,
                    Attribute = m.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, fieldAttr))
                })
                .Where(t => t.Attribute != null)
                .OrderBy(t => (int)t.Attribute!.ConstructorArguments[0].Value!)
                .ToList();

            var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : $"namespace {typeSymbol.ContainingNamespace.ToDisplayString()};\n\n";
            string typeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string className = typeSymbol.Name + "SerializationExtensions";
            string methodSuffix = typeSymbol.Name;

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("using Utils.IO.Serialization;");
            sb.AppendLine(ns);
            sb.AppendLine($"public static class {className}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Reads an instance of <see cref=\"{typeName}\"/> from the reader.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <param name=\"reader\">Source reader.</param>");
            sb.AppendLine("    /// <returns>Deserialized instance.</returns>");
            sb.AppendLine($"    public static {typeName} Read{methodSuffix}(this IReader reader)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var result = new {typeName}();");
            foreach (var m in members)
            {
                var memberName = m.Symbol.Name;
                var memberType = (m.Symbol as IPropertySymbol)?.Type ?? ((IFieldSymbol)m.Symbol).Type;
                string memberTypeName = memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string readExpr = HasCustomReader(memberType)
                    ? $"reader.Read{memberType.Name}()"
                    : $"reader.Read<{memberTypeName}>()";
                sb.AppendLine($"        result.{memberName} = {readExpr};");
            }
            sb.AppendLine("        return result;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Writes an instance of <see cref=\"{typeName}\"/> to the writer.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <param name=\"writer\">Target writer.</param>");
            sb.AppendLine("    /// <param name=\"value\">Instance to serialize.</param>");
            sb.AppendLine($"    public static void Write{methodSuffix}(this IWriter writer, {typeName} value)");
            sb.AppendLine("    {");
            foreach (var m in members)
            {
                string memberName = m.Symbol.Name;
                var memberType = (m.Symbol as IPropertySymbol)?.Type ?? ((IFieldSymbol)m.Symbol).Type;
                string memberTypeName = memberType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string writeExpr = HasCustomWriter(memberType)
                    ? $"writer.Write{memberType.Name}(value.{memberName});"
                    : $"writer.Write<{memberTypeName}>(value.{memberName});";
                sb.AppendLine($"        {writeExpr}");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");

            context.AddSource($"{typeSymbol.Name}.Serialization.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }
    }
}
