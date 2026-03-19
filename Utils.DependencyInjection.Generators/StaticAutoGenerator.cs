using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Utils.DependencyInjection.Generators;

/// <summary>
/// Generates implementations of <see cref="T:IServiceConfigurator"/> for classes
/// marked with <c>[StaticAuto]</c>.
/// </summary>
[Generator]
public class StaticAutoGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidateClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax classDeclaration && classDeclaration.AttributeLists.Count > 0,
                static (generatorContext, _) => (ClassDeclarationSyntax)generatorContext.Node)
            .Collect();

        var compilationAndCandidates = context.CompilationProvider.Combine(candidateClasses);
        context.RegisterSourceOutput(compilationAndCandidates, static (productionContext, source) =>
        {
            EmitSources(productionContext, source.Left, source.Right);
        });
    }

    /// <summary>
    /// Emits the generated configurator source for each valid candidate class.
    /// </summary>
    /// <param name="context">Source production context used to add generated files.</param>
    /// <param name="compilation">Compilation currently being analyzed.</param>
    /// <param name="candidates">Syntax candidates collected by the incremental pipeline.</param>
    private static void EmitSources(SourceProductionContext context, Compilation compilation, IEnumerable<ClassDeclarationSyntax> candidates)
    {
        var serviceConfiguratorSymbol = compilation.GetTypeByMetadataName("Utils.DependencyInjection.IServiceConfigurator");
        var staticAutoAttributeSymbol = compilation.GetTypeByMetadataName("Utils.DependencyInjection.StaticAutoAttribute");
        var injectableAttributeSymbol = compilation.GetTypeByMetadataName("Utils.DependencyInjection.InjectableAttribute");
        var singletonAttributeSymbol = compilation.GetTypeByMetadataName("Utils.DependencyInjection.SingletonAttribute");
        var scopedAttributeSymbol = compilation.GetTypeByMetadataName("Utils.DependencyInjection.ScopedAttribute");
        var transientAttributeSymbol = compilation.GetTypeByMetadataName("Utils.DependencyInjection.TransientAttribute");

        if (serviceConfiguratorSymbol == null || staticAutoAttributeSymbol == null)
        {
            return;
        }

        var allTypes = GetAllTypes(compilation.Assembly.GlobalNamespace)
            .Where(IsAccessible)
            .ToList();
        var injectableTypes = allTypes.Where(t => t.GetAttributes().Any(a =>
            SymbolEqualityComparer.Default.Equals(a.AttributeClass, singletonAttributeSymbol) ||
            SymbolEqualityComparer.Default.Equals(a.AttributeClass, scopedAttributeSymbol) ||
            SymbolEqualityComparer.Default.Equals(a.AttributeClass, transientAttributeSymbol))).ToList();

        foreach (var candidate in candidates)
        {
            var model = compilation.GetSemanticModel(candidate.SyntaxTree);
            if (model.GetDeclaredSymbol(candidate) is not INamedTypeSymbol classSymbol)
            {
                continue;
            }

            if (!classSymbol.AllInterfaces.Contains(serviceConfiguratorSymbol, SymbolEqualityComparer.Default))
            {
                continue;
            }

            if (!classSymbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, staticAutoAttributeSymbol)))
            {
                continue;
            }

            var source = GenerateClass(classSymbol, injectableTypes, injectableAttributeSymbol, singletonAttributeSymbol, scopedAttributeSymbol, transientAttributeSymbol);
            context.AddSource($"{classSymbol.Name}.StaticAuto.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol @namespace)
    {
        foreach (var type in @namespace.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in GetNestedTypes(type))
            {
                yield return nested;
            }
        }

        foreach (var ns in @namespace.GetNamespaceMembers())
        {
            foreach (var type in GetAllTypes(ns))
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var nestedChild in GetNestedTypes(nested))
            {
                yield return nestedChild;
            }
        }
    }

    private static bool IsAccessible(INamedTypeSymbol type)
    {
        if (type.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
        {
            return false;
        }

        var containing = type.ContainingType;
        while (containing != null)
        {
            if (containing.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
            {
                return false;
            }

            containing = containing.ContainingType;
        }

        return true;
    }

    private static string GenerateClass(INamedTypeSymbol classSymbol, IEnumerable<INamedTypeSymbol> injectableTypes,
            INamedTypeSymbol? injectableAttributeSymbol, INamedTypeSymbol? singletonAttributeSymbol,
            INamedTypeSymbol? scopedAttributeSymbol, INamedTypeSymbol? transientAttributeSymbol)
    {
        var builder = new StringBuilder();
        foreach (var type in injectableTypes)
        {
            var attr = type.GetAttributes().First(a =>
                    SymbolEqualityComparer.Default.Equals(a.AttributeClass, singletonAttributeSymbol) ||
                    SymbolEqualityComparer.Default.Equals(a.AttributeClass, scopedAttributeSymbol) ||
                    SymbolEqualityComparer.Default.Equals(a.AttributeClass, transientAttributeSymbol));

            string? domain = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string : null;
            string registration = GetRegistrationMethod(attr.AttributeClass, domain, singletonAttributeSymbol, scopedAttributeSymbol);

            var interfaces = type.Interfaces.Where(i => i.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, injectableAttributeSymbol))).ToList();
            string implementationName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (interfaces.Any())
            {
                foreach (var inter in interfaces)
                {
                    string interfaceName = inter.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (domain is null)
                    {
                        builder.AppendLine($"                serviceCollection.{registration}<{interfaceName}, {implementationName}>();");
                    }
                    else
                    {
                        builder.AppendLine($"                serviceCollection.{registration}<{interfaceName}, {implementationName}>(\"{domain}\");");
                    }
                }
            }
            else
            {
                if (domain is null)
                {
                    builder.AppendLine($"                serviceCollection.{registration}<{implementationName}>();");
                }
                else
                {
                    builder.AppendLine($"                serviceCollection.{registration}<{implementationName}>(\"{domain}\");");
                }
            }
        }

        string namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : $"namespace {classSymbol.ContainingNamespace.ToDisplayString()};\n\n";

        return $"// <auto-generated/>\nusing Microsoft.Extensions.DependencyInjection;\n{namespaceName}public partial class {classSymbol.Name}\n{{\n        /// <summary>\n        /// Registers services discovered by the StaticAuto source generator.\n        /// </summary>\n        /// <param name=\"serviceCollection\">Collection to populate.</param>\n        public void ConfigureServices(IServiceCollection serviceCollection)\n        {{\n{builder.ToString()}        }}\n}}\n";
    }

    private static string GetRegistrationMethod(INamedTypeSymbol? attributeClass, string? domain,
            INamedTypeSymbol? singletonAttributeSymbol, INamedTypeSymbol? scopedAttributeSymbol)
    {
        string lifetime = SymbolEqualityComparer.Default.Equals(attributeClass, singletonAttributeSymbol)
                ? "Singleton"
                : SymbolEqualityComparer.Default.Equals(attributeClass, scopedAttributeSymbol)
                        ? "Scoped"
                        : "Transient";

        return domain is null ? $"Add{lifetime}" : $"AddKeyed{lifetime}";
    }

}
