using System;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
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
public sealed class Antlr4GrammarGenerator : ISourceGenerator
{
    private static readonly DiagnosticDescriptor s_errorDescriptor = new DiagnosticDescriptor(
        id:                 "APU0100",
        title:              "Grammar generation failed",
        messageFormat:      "Failed to generate grammar for '{0}': {1}",
        category:           "Utils.Parser.Generators",
        defaultSeverity:    DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:        "An error occurred while parsing or emitting the ANTLR4 .g4 grammar file.");

    /// <inheritdoc />
    public void Initialize(GeneratorInitializationContext context) { }

    /// <inheritdoc />
    public void Execute(GeneratorExecutionContext context)
    {
        foreach (var file in context.AdditionalFiles)
        {
            if (!file.Path.EndsWith(".g4", StringComparison.OrdinalIgnoreCase))
                continue;

            ProcessFile(context, file);
        }
    }

    private static void ProcessFile(GeneratorExecutionContext context, AdditionalText file)
    {
        var text = file.GetText(context.CancellationToken);
        if (text == null) return;

        // ── Read MSBuild metadata ────────────────────────────────────────
        var options = context.AnalyzerConfigOptions.GetOptions(file);

        options.TryGetValue("build_metadata.AdditionalFiles.Namespace", out var namespaceName);
        options.TryGetValue("build_metadata.AdditionalFiles.ClassName",  out var className);

        string fileName = Path.GetFileName(file.Path);

        if (string.IsNullOrWhiteSpace(className))
            className = Path.GetFileNameWithoutExtension(file.Path);

        if (string.IsNullOrWhiteSpace(namespaceName))
            namespaceName = string.Empty;

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
}
