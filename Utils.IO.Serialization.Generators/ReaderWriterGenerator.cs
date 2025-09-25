using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Utils.IO.Serialization.Generators;

/// <summary>
/// Generates reader and writer extension methods for types annotated with <see cref="Serialization.GenerateReaderWriterAttribute"/>.
/// </summary>
[Generator]
public class ReaderWriterGenerator : ISourceGenerator
{
    /// <inheritdoc />
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
    }

    /// <inheritdoc />
    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not SyntaxReceiver receiver)
        {
            return;
        }

        var generateAttr = context.Compilation.GetTypeByMetadataName("Utils.IO.Serialization.GenerateReaderWriterAttribute");
        var fieldAttr = context.Compilation.GetTypeByMetadataName("Utils.IO.Serialization.FieldAttribute");
        var iReader = context.Compilation.GetTypeByMetadataName("Utils.IO.Serialization.IReader");
        var iWriter = context.Compilation.GetTypeByMetadataName("Utils.IO.Serialization.IWriter");
        if (generateAttr is null || fieldAttr is null || iReader is null || iWriter is null)
        {
            return;
        }

        /// <summary>
        /// Determines whether a type has a dedicated reader method or is marked for generation.
        /// </summary>
        /// <param name="type">Type to inspect.</param>
        /// <returns><see langword="true"/> if a custom reader should be invoked.</returns>
        bool HasCustomReader(ITypeSymbol type)
        {
            if (type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, generateAttr)))
            {
                return true;
            }

            string methodName = "Read" + type.Name;
            foreach (var symbol in context.Compilation.GetSymbolsWithName(methodName, SymbolFilter.Member, context.CancellationToken))
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

        /// <summary>
        /// Determines whether a type has a dedicated writer method or is marked for generation.
        /// </summary>
        /// <param name="type">Type to inspect.</param>
        /// <returns><see langword="true"/> if a custom writer should be invoked.</returns>
        bool HasCustomWriter(ITypeSymbol type)
        {
            if (type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, generateAttr)))
            {
                return true;
            }

            string methodName = "Write" + type.Name;
            foreach (var symbol in context.Compilation.GetSymbolsWithName(methodName, SymbolFilter.Member, context.CancellationToken))
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

        foreach (var candidate in receiver.Candidates)
        {
            var model = context.Compilation.GetSemanticModel(candidate.SyntaxTree);
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

    /// <summary>
    /// Collects types that are potential candidates for generation.
    /// </summary>
    private sealed class SyntaxReceiver : ISyntaxReceiver
    {
        /// <summary>
        /// Gets the list of candidate type declarations.
        /// </summary>
        public List<TypeDeclarationSyntax> Candidates { get; } = new();

        /// <summary>
        /// Invoked for each syntax node during analysis.
        /// </summary>
        /// <param name="syntaxNode">The current syntax node.</param>
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is TypeDeclarationSyntax tds && tds.AttributeLists.Count > 0)
            {
                Candidates.Add(tds);
            }
        }
    }
}
