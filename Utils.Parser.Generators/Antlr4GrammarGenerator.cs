using System;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Utils.Parser.Generators.Internal;

namespace Utils.Parser.Generators;

/// <summary>
/// Roslyn source generator that reads ANTLR4 <c>.g4</c> grammar files declared as
/// <c>AdditionalFiles</c> and emits a C# class that constructs the equivalent
/// <c>Utils.Parser.Model.ParserDefinition</c> at compile time — no runtime .g4 parsing.
/// </summary>
/// <remarks>
/// <para>
/// In your <c>.csproj</c> file, declare each grammar file as an <c>AdditionalFiles</c>
/// item and supply two optional MSBuild metadata values:
/// </para>
/// <code language="xml">
/// &lt;ItemGroup&gt;
///   &lt;AdditionalFiles Include="Parser\Exp.g4"&gt;
///     &lt;Namespace&gt;MyApp.Parser&lt;/Namespace&gt;
///     &lt;ClassName&gt;ExpGrammar&lt;/ClassName&gt;
///   &lt;/AdditionalFiles&gt;
/// &lt;/ItemGroup&gt;
/// </code>
/// <list type="bullet">
///   <item><term>Namespace</term><description>C# namespace for the generated class (defaults to empty — global namespace).</description></item>
///   <item><term>ClassName</term><description>Name of the generated <c>partial static</c> class (defaults to the file name without extension).</description></item>
/// </list>
/// </remarks>
[Generator]
public sealed class Antlr4GrammarGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor s_errorDescriptor = new DiagnosticDescriptor(
        id:                 "APU0100",
        title:              "Grammar generation failed",
        messageFormat:      "Failed to generate grammar for '{0}': {1}",
        category:           "Utils.Parser.Generators",
        defaultSeverity:    DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:        "An error occurred while parsing or emitting the ANTLR4 .g4 grammar file.");

    private static readonly DiagnosticDescriptor s_invalidMetadataDescriptor = new DiagnosticDescriptor(
        id:                 "APU0101",
        title:              "Invalid source generator metadata",
        messageFormat:      "Invalid source generator metadata for '{0}': {1}",
        category:           "Utils.Parser.Generators",
        defaultSeverity:    DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:        "AdditionalFiles metadata contains invalid namespace or class name values.");

    private static readonly DiagnosticDescriptor s_invalidDescriptorDescriptor = new DiagnosticDescriptor(
        id:                 "APU0102",
        title:              "Invalid syntax colorization descriptor",
        messageFormat:      "Invalid syntax colorization descriptor '{0}': {1}",
        category:           "Utils.Parser.Generators",
        defaultSeverity:    DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:        "The syntax colorization descriptor does not contain required sections or directives.");

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var grammarFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".g4", StringComparison.OrdinalIgnoreCase));

        var grammarFileAndOptions = grammarFiles
            .Combine(context.AnalyzerConfigOptionsProvider);

        context.RegisterSourceOutput(grammarFileAndOptions, static (productionContext, source) =>
        {
            ProcessGrammarFile(productionContext, source.Left, source.Right);
        });

        var colorizationFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".syntaxcolor", StringComparison.OrdinalIgnoreCase));

        var colorizationFileAndOptions = colorizationFiles
            .Combine(context.AnalyzerConfigOptionsProvider);

        context.RegisterSourceOutput(colorizationFileAndOptions, static (productionContext, source) =>
        {
            ProcessColorizationFile(productionContext, source.Left, source.Right);
        });
    }

    /// <summary>
    /// Processes a grammar additional file and emits the generated parser definition source.
    /// </summary>
    /// <param name="context">Context used to report diagnostics and add generated files.</param>
    /// <param name="file">Grammar additional file being processed.</param>
    /// <param name="optionsProvider">Analyzer config options provider associated with the current run.</param>
    private static void ProcessGrammarFile(SourceProductionContext context, AdditionalText file, AnalyzerConfigOptionsProvider optionsProvider)
    {
        var text = file.GetText(context.CancellationToken);
        if (text == null) return;

        // ── Read MSBuild metadata ────────────────────────────────────────
        var options = optionsProvider.GetOptions(file);

        options.TryGetValue("build_metadata.AdditionalFiles.Namespace", out var namespaceName);
        options.TryGetValue("build_metadata.AdditionalFiles.ClassName",  out var className);

        string fileName = Path.GetFileName(file.Path);

        if (string.IsNullOrWhiteSpace(className))
            className = Path.GetFileNameWithoutExtension(file.Path);

        if (string.IsNullOrWhiteSpace(namespaceName))
            namespaceName = string.Empty;

        if (!SyntaxColorizationValidation.TryValidateTypeMetadata(namespaceName!, className!, out string metadataError))
        {
            context.ReportDiagnostic(Diagnostic.Create(s_invalidMetadataDescriptor, Location.None, fileName, metadataError));
            return;
        }

        // ── Parse & emit ─────────────────────────────────────────────────
        try
        {
            var source    = text.ToString();
            var tokens    = new G4Tokenizer(source).Tokenize();
            var grammar   = new G4Parser(tokens).Parse();
            var generated = GrammarEmitter.Emit(grammar, namespaceName!, className!, fileName);

            context.AddSource($"{className}.Grammar.g.cs", SourceText.From(generated, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(s_errorDescriptor, Location.None, fileName, ex.Message));
        }
    }

    /// <summary>
    /// Processes a syntax colorization descriptor and emits an <c>ISyntaxColorisation</c> implementation.
    /// </summary>
    /// <param name="context">Context used to report diagnostics and add generated files.</param>
    /// <param name="file">Descriptor additional file being processed.</param>
    /// <param name="optionsProvider">Analyzer config options provider associated with the current run.</param>
    private static void ProcessColorizationFile(SourceProductionContext context, AdditionalText file, AnalyzerConfigOptionsProvider optionsProvider)
    {
        var text = file.GetText(context.CancellationToken);
        if (text == null)
        {
            return;
        }

        var options = optionsProvider.GetOptions(file);
        options.TryGetValue("build_metadata.AdditionalFiles.Namespace", out var namespaceName);
        options.TryGetValue("build_metadata.AdditionalFiles.ClassName", out var className);

        string fileName = Path.GetFileName(file.Path);

        if (string.IsNullOrWhiteSpace(className))
        {
            className = Path.GetFileNameWithoutExtension(file.Path);
        }

        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            namespaceName = string.Empty;
        }

        if (!SyntaxColorizationValidation.TryValidateTypeMetadata(namespaceName!, className!, out string metadataError))
        {
            context.ReportDiagnostic(Diagnostic.Create(s_invalidMetadataDescriptor, Location.None, fileName, metadataError));
            return;
        }

        try
        {
            var descriptor = SyntaxColorizationDescriptorParser.Parse(text.ToString());
            if (!SyntaxColorizationValidation.TryValidateDescriptor(descriptor, out string descriptorError))
            {
                context.ReportDiagnostic(Diagnostic.Create(s_invalidDescriptorDescriptor, Location.None, fileName, descriptorError));
                return;
            }

            var generated = SyntaxColorizationEmitter.Emit(descriptor, namespaceName!, className!, fileName);
            context.AddSource($"{className}.SyntaxColorization.g.cs", SourceText.From(generated, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(s_errorDescriptor, Location.None, fileName, ex.Message));
        }
    }

}
