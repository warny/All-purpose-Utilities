using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Utils.IO.Serialization.Generators;

/// <summary>Generates validated reader and writer extension methods for attributed types.</summary>
[Generator]
public sealed class ReaderWriterGenerator : IIncrementalGenerator
{
    private const string Category = "Utils.IO.Serialization";
    private static readonly DiagnosticDescriptor UnsupportedType = Create("UIOSG001", "Unsupported type", "Type '{0}' is not a supported concrete, closed serialization type");
    private static readonly DiagnosticDescriptor MissingConstructor = Create("UIOSG002", "Missing constructor", "Type '{0}' requires an accessible parameterless constructor");
    private static readonly DiagnosticDescriptor UnsupportedMember = Create("UIOSG003", "Unsupported member", "Member '{0}' must be an accessible instance member with both readable and writable access");
    private static readonly DiagnosticDescriptor DuplicateOrder = Create("UIOSG004", "Duplicate field order", "Member '{0}' duplicates field order {1}");
    private static readonly DiagnosticDescriptor AmbiguousConverter = Create("UIOSG005", "Ambiguous converter", "More than one exact {0} converter is available for '{1}'");
    private static readonly DiagnosticDescriptor InitOnlyProperty = Create("UIOSG010", "Init-only property is not supported", "Property '{0}' on '{1}' is init-only and cannot be assigned by the generated reader");

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Utils.IO.Serialization.GenerateReaderWriterAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (INamedTypeSymbol)ctx.TargetSymbol).Collect();
        context.RegisterSourceOutput(context.CompilationProvider.Combine(candidates),
            static (ctx, value) => Emit(ctx, value.Left, value.Right));
    }

    /// <summary>Creates a stable diagnostic descriptor for a contract rule.</summary>
    private static DiagnosticDescriptor Create(string id, string title, string message) =>
        new(id, title, message, Category, DiagnosticSeverity.Error, true, title);

    /// <summary>Validates candidates and emits serializers only for complete contracts.</summary>
    private static void Emit(SourceProductionContext context, Compilation compilation, ImmutableArray<INamedTypeSymbol> candidates)
    {
        INamedTypeSymbol? generateAttribute = compilation.GetTypeByMetadataName("Utils.IO.Serialization.GenerateReaderWriterAttribute");
        INamedTypeSymbol? fieldAttribute = compilation.GetTypeByMetadataName("Utils.IO.Serialization.FieldAttribute");
        INamedTypeSymbol? readerType = compilation.GetTypeByMetadataName("Utils.IO.Serialization.IReader");
        INamedTypeSymbol? writerType = compilation.GetTypeByMetadataName("Utils.IO.Serialization.IWriter");
        if (generateAttribute is null || fieldAttribute is null || readerType is null || writerType is null) return;

        var uniqueTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (INamedTypeSymbol type in candidates)
        {
            if (!uniqueTypes.Add(type) || !HasAttribute(type, generateAttribute)) continue;
            SyntaxNode? declaration = type.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken);
            Location typeLocation = declaration?.GetLocation() ?? type.Locations.FirstOrDefault() ?? Location.None;
            Location identifierLocation = declaration is TypeDeclarationSyntax typeDeclaration
                ? typeDeclaration.Identifier.GetLocation()
                : typeLocation;

            bool invalid = false;
            ImmutableArray<ITypeParameterSymbol> methodTypeParameters = GetContainingTypeParameters(type);
            bool unsupportedConstraints = methodTypeParameters.Any(parameter => parameter.HasConstructorConstraint || parameter.HasReferenceTypeConstraint ||
                parameter.HasValueTypeConstraint || parameter.HasUnmanagedTypeConstraint || parameter.ConstraintTypes.Length > 0);
            if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct) || type.IsAbstract || type.IsUnboundGenericType || unsupportedConstraints)
                invalid |= Report(context, UnsupportedType, typeLocation, type.ToDisplayString());
            if (type.TypeKind == TypeKind.Class && !type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && IsAccessible(c.DeclaredAccessibility)))
                invalid |= Report(context, MissingConstructor, identifierLocation, type.ToDisplayString());

            var members = type.GetMembers().Select(m => new { Symbol = m, Attribute = m.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, fieldAttribute)) })
                .Where(x => x.Attribute is not null)
                .Select(x => new MemberContract(x.Symbol, (int)x.Attribute!.ConstructorArguments[0].Value!)).ToList();

            foreach (MemberContract member in members)
            {
                if (member.Symbol is IPropertySymbol initProperty && initProperty.SetMethod?.IsInitOnly == true)
                {
                    invalid |= Report(context, InitOnlyProperty, initProperty.Locations.FirstOrDefault(), initProperty.Name, type.ToDisplayString());
                    continue;
                }
                bool valid = member.Symbol switch
                {
                    IFieldSymbol field => !field.IsStatic && !field.IsReadOnly && IsAccessible(field.DeclaredAccessibility),
                    IPropertySymbol property => !property.IsStatic && !property.IsIndexer && property.GetMethod is not null && property.SetMethod is not null && IsAccessible(property.GetMethod.DeclaredAccessibility) && IsAccessible(property.SetMethod.DeclaredAccessibility),
                    _ => false
                };
                if (!valid) invalid |= Report(context, UnsupportedMember, member.Symbol.Locations.FirstOrDefault(), member.Symbol.Name);
            }
            foreach (IGrouping<int, MemberContract> duplicate in members.GroupBy(m => m.Order).Where(g => g.Count() > 1))
                foreach (MemberContract member in duplicate)
                    invalid |= Report(context, DuplicateOrder, member.Symbol.Locations.FirstOrDefault(), member.Symbol.Name, duplicate.Key);
            foreach (MemberContract member in members)
            {
                int readerCount = FindConverters(compilation, "Read" + member.Type.Name,
                    m => m.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, readerType) && SymbolEqualityComparer.Default.Equals(m.ReturnType, member.Type), context).Take(2).Count();
                int writerCount = FindConverters(compilation, "Write" + member.Type.Name,
                    m => m.Parameters.Length == 2 && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, writerType) && SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, member.Type), context).Take(2).Count();
                if (readerCount > 1) invalid |= Report(context, AmbiguousConverter, member.Symbol.Locations.FirstOrDefault(), "reader", member.Type.ToDisplayString());
                if (writerCount > 1) invalid |= Report(context, AmbiguousConverter, member.Symbol.Locations.FirstOrDefault(), "writer", member.Type.ToDisplayString());
            }
            if (invalid) continue;

            string identity = StableIdentity(type);
            string identifier = Sanitize(identity) + "_" + StableHash(identity);
            string ns = type.ContainingNamespace.IsGlobalNamespace ? string.Empty : "namespace " + type.ContainingNamespace.ToDisplayString() + ";\n\n";
            string typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string genericDeclaration = methodTypeParameters.Length == 0
                ? string.Empty
                : "<" + string.Join(", ", methodTypeParameters.Select(parameter => parameter.Name)) + ">";
            var source = new StringBuilder("// <auto-generated/>\nusing Utils.IO.Serialization;\n").Append(ns)
                .Append("public static class ").Append(identifier).Append("SerializationExtensions\n{\n")
                .Append("    /// <summary>Reads the validated binary contract.</summary>\n")
                .Append("    public static ").Append(typeName).Append(" Read").Append(identifier).Append(genericDeclaration).Append("(this IReader reader)\n    {\n")
                .Append("        var result = new ").Append(typeName).Append("();\n");
            foreach (MemberContract member in members.OrderBy(m => m.Order))
                source.Append("        result.").Append(member.Symbol.Name).Append(" = ").Append(ReadExpression(compilation, context, member.Type, readerType, generateAttribute)).Append(";\n");
            source.Append("        return result;\n    }\n\n    /// <summary>Writes the validated binary contract.</summary>\n")
                .Append("    public static void Write").Append(identifier).Append(genericDeclaration).Append("(this IWriter writer, ").Append(typeName).Append(" value)\n    {\n");
            foreach (MemberContract member in members.OrderBy(m => m.Order))
                source.Append("        ").Append(WriteExpression(compilation, context, member.Type, writerType, generateAttribute, "value." + member.Symbol.Name)).Append(";\n");
            source.Append("    }\n}\n");
            context.AddSource(identifier + ".Serialization.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        }
    }

    /// <summary>Resolves one exact reader converter and emits its fully qualified static call.</summary>
    private static string ReadExpression(Compilation compilation, SourceProductionContext context, ITypeSymbol type, INamedTypeSymbol readerType, INamedTypeSymbol generateAttribute)
    {
        IMethodSymbol[] methods = FindConverters(compilation, "Read" + type.Name, m => m.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, readerType) && SymbolEqualityComparer.Default.Equals(m.ReturnType, type), context).ToArray();
        if (methods.Length == 1) return FullyQualifiedCall(methods[0], "reader");
        if (methods.Length > 1) context.ReportDiagnostic(Diagnostic.Create(AmbiguousConverter, type.Locations.FirstOrDefault(), "reader", type.ToDisplayString()));
        if (type is INamedTypeSymbol namedType && HasAttribute(namedType, generateAttribute))
            return GeneratedTypeName(namedType) + ".Read" + GeneratedIdentifier(namedType) + GeneratedTypeArguments(namedType) + "(reader)";
        return "reader.Read<" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">()";
    }

    /// <summary>Resolves one exact writer converter and emits its fully qualified static call.</summary>
    private static string WriteExpression(Compilation compilation, SourceProductionContext context, ITypeSymbol type, INamedTypeSymbol writerType, INamedTypeSymbol generateAttribute, string value)
    {
        IMethodSymbol[] methods = FindConverters(compilation, "Write" + type.Name, m => m.Parameters.Length == 2 && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, writerType) && SymbolEqualityComparer.Default.Equals(m.Parameters[1].Type, type), context).ToArray();
        if (methods.Length == 1) return FullyQualifiedCall(methods[0], "writer, " + value);
        if (methods.Length > 1) context.ReportDiagnostic(Diagnostic.Create(AmbiguousConverter, type.Locations.FirstOrDefault(), "writer", type.ToDisplayString()));
        if (type is INamedTypeSymbol namedType && HasAttribute(namedType, generateAttribute))
            return GeneratedTypeName(namedType) + ".Write" + GeneratedIdentifier(namedType) + GeneratedTypeArguments(namedType) + "(writer, " + value + ")";
        return "writer.Write<" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">(" + value + ")";
    }

    /// <summary>Finds exact static converter methods with supported visibility and signature.</summary>
    private static IEnumerable<IMethodSymbol> FindConverters(Compilation compilation, string name, Func<IMethodSymbol, bool> predicate, SourceProductionContext context) =>
        compilation.GetSymbolsWithName(name, SymbolFilter.Member, context.CancellationToken).OfType<IMethodSymbol>()
            .Where(m => m.IsStatic && IsAccessible(m.DeclaredAccessibility) && predicate(m));

    /// <summary>Formats a converter invocation without relying on imports or extension lookup.</summary>
    private static string FullyQualifiedCall(IMethodSymbol method, string arguments) =>
        method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + method.Name + "(" + arguments + ")";

    /// <summary>Returns a metadata-style identity including namespace, containing types, and arity.</summary>
    private static string StableIdentity(INamedTypeSymbol type) => type.ToDisplayString(new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters));

    /// <summary>Escapes a stable identity for both C# identifiers and source hint names.</summary>
    private static string Sanitize(string value) => string.Concat(value.Select(c => char.IsLetterOrDigit(c) ? c : '_'));

    /// <summary>Builds the common unique identifier used by generated classes, methods, and hint names.</summary>
    private static string GeneratedIdentifier(INamedTypeSymbol type)
    {
        string identity = StableIdentity(type);
        return Sanitize(identity) + "_" + StableHash(identity);
    }

    /// <summary>Builds the fully qualified generated extension class name for an attributed type.</summary>
    private static string GeneratedTypeName(INamedTypeSymbol type)
    {
        string prefix = type.ContainingNamespace.IsGlobalNamespace
            ? "global::"
            : "global::" + type.ContainingNamespace.ToDisplayString() + ".";
        return prefix + GeneratedIdentifier(type) + "SerializationExtensions";
    }

    /// <summary>Gets distinct generic parameters introduced by the type and its containing types.</summary>
    private static ImmutableArray<ITypeParameterSymbol> GetContainingTypeParameters(INamedTypeSymbol type)
    {
        var chain = new Stack<INamedTypeSymbol>();
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType) chain.Push(current);
        var parameters = ImmutableArray.CreateBuilder<ITypeParameterSymbol>();
        while (chain.Count > 0) parameters.AddRange(chain.Pop().TypeParameters);
        return parameters.ToImmutable();
    }

    /// <summary>Formats generic arguments required when invoking another generated serializer.</summary>
    private static string GeneratedTypeArguments(INamedTypeSymbol type)
    {
        var chain = new Stack<INamedTypeSymbol>();
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType) chain.Push(current);
        var arguments = new List<ITypeSymbol>();
        while (chain.Count > 0) arguments.AddRange(chain.Pop().TypeArguments);
        return arguments.Count == 0
            ? string.Empty
            : "<" + string.Join(", ", arguments.Select(argument => argument.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))) + ">";
    }

    /// <summary>Computes a deterministic FNV-1a suffix; unlike GetHashCode it is stable between processes.</summary>
    private static string StableHash(string value)
    {
        uint hash = 2166136261;
        foreach (byte valueByte in Encoding.UTF8.GetBytes(value)) hash = (hash ^ valueByte) * 16777619;
        return hash.ToString("x8");
    }

    /// <summary>Reports a user contract diagnostic and returns true for validation accumulation.</summary>
    private static bool Report(SourceProductionContext context, DiagnosticDescriptor descriptor, Location? location, params object[] args)
    {
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, args));
        return true;
    }

    /// <summary>Checks whether a symbol carries a particular attribute.</summary>
    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attribute) => symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute));

    /// <summary>Checks whether generated code can use a declared symbol.</summary>
    private static bool IsAccessible(Accessibility accessibility) => accessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;

    /// <summary>Stores validated Roslyn member data separately from generator processing.</summary>
    private sealed class MemberContract
    {
        /// <summary>Initializes a member contract.</summary>
        internal MemberContract(ISymbol symbol, int order) { Symbol = symbol; Order = order; }
        /// <summary>Gets the source symbol.</summary>
        internal ISymbol Symbol { get; }
        /// <summary>Gets the stable wire order; negative orders are supported and sort normally.</summary>
        internal int Order { get; }
        /// <summary>Gets the serialized value type.</summary>
        internal ITypeSymbol Type => Symbol is IPropertySymbol property ? property.Type : ((IFieldSymbol)Symbol).Type;
    }
}
