using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Utils.Parser.Diagnostics;
using Utils.Parser.Generators.Internal;
using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

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
        defaultSeverity:    RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:        "An error occurred while parsing or emitting the ANTLR4 .g4 grammar file.");

    private static readonly DiagnosticDescriptor s_invalidMetadataDescriptor = new DiagnosticDescriptor(
        id:                 "APU0101",
        title:              "Invalid source generator metadata",
        messageFormat:      "Invalid source generator metadata for '{0}': {1}",
        category:           "Utils.Parser.Generators",
        defaultSeverity:    RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:        "AdditionalFiles metadata contains invalid namespace or class name values.");

    private static readonly DiagnosticDescriptor s_invalidDescriptorDescriptor = new DiagnosticDescriptor(
        id:                 "APU0102",
        title:              "Invalid syntax colorization descriptor",
        messageFormat:      "Invalid syntax colorization descriptor '{0}': {1}",
        category:           "Utils.Parser.Generators",
        defaultSeverity:    RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:        "The syntax colorization descriptor does not contain required sections or directives.");

    private static readonly DiagnosticDescriptor s_unsupportedSuperClassDescriptor = new DiagnosticDescriptor(
        id:                 "APU0103",
        title:              "ANTLR superClass is not applied by generator",
        messageFormat:      "Grammar '{0}' declares superClass='{1}', but generated runtime inheritance is not supported and the option is kept as metadata only.",
        category:           "Utils.Parser.Generators",
        defaultSeverity:    RoslynDiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:        "The current generator keeps ANTLR superClass in options metadata but does not change generated type inheritance.");

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
            var diagnostics = new DiagnosticBag();
            var grammar   = new G4Parser(tokens, diagnostics).Parse();
            if (grammar.Options.TryGetValue("superClass", out var superClass) && !string.IsNullOrWhiteSpace(superClass))
            {
                context.ReportDiagnostic(Diagnostic.Create(s_unsupportedSuperClassDescriptor, Location.None, grammar.Name, superClass));
            }
            ReportUnsupportedEmbeddedCodeDiagnostics(context, file, text, grammar);
            var generated = GrammarEmitter.Emit(grammar, namespaceName!, className!, fileName);
            ReportParserDiagnostics(context, diagnostics, fileName);

            context.AddSource($"{className}.Grammar.g.cs", SourceText.From(generated, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(s_errorDescriptor, Location.None, fileName, ex.Message));
        }
    }


    /// <summary>
    /// Reports source-generator diagnostics for embedded-code constructs that are visible in the grammar AST but are not executable generated C# hooks.
    /// </summary>
    /// <param name="context">Source production context receiving Roslyn diagnostics.</param>
    /// <param name="file">Grammar additional file.</param>
    /// <param name="text">Grammar source text used to create line-based locations.</param>
    /// <param name="grammar">Parsed grammar AST.</param>
    private static void ReportUnsupportedEmbeddedCodeDiagnostics(SourceProductionContext context, AdditionalText file, SourceText text, G4Grammar grammar)
    {
        foreach (var action in grammar.Actions)
        {
            var constructKind = FormatGrammarActionKind(action);
            if (IsInjectableParserMembersAction(action))
            {
                ReportEmbeddedMembersInjectedDiagnostic(context, file, text, action.Line, constructKind, grammar.Name);
                continue;
            }

            var reason = FormatGrammarActionReason(action);
            ReportUnsupportedEmbeddedCodeDiagnostic(context, file, text, action.Line, constructKind, grammar.Name, reason);
        }

        foreach (var rule in grammar.ParserRules)
        {
            ReportRuleLifecycleDiagnostics(context, file, text, rule);
        }

        foreach (var rule in grammar.LexerRules)
        {
            ReportRuleLifecycleDiagnostics(context, file, text, rule);
            ReportLexerEmbeddedCodeDiagnostics(context, file, text, rule);
        }

        foreach (var mode in grammar.ExtraModes)
        {
            foreach (var rule in mode.Rules)
            {
                ReportRuleLifecycleDiagnostics(context, file, text, rule);
                ReportLexerEmbeddedCodeDiagnostics(context, file, text, rule);
            }
        }
    }


    /// <summary>
    /// Determines whether a grammar-level action is a parser members block that the generator injects into the per-parse execution context.
    /// </summary>
    /// <param name="action">Grammar-level action metadata.</param>
    /// <returns><c>true</c> when the action is unscoped <c>@members</c> or <c>@parser::members</c>.</returns>
    private static bool IsInjectableParserMembersAction(G4GrammarAction action)
    {
        return string.Equals(action.Name, "members", StringComparison.Ordinal)
            && (action.Target is null || string.Equals(action.Target, "parser", StringComparison.Ordinal));
    }

    /// <summary>
    /// Reports the compatibility warning used when parser members are injected into the generated execution context.
    /// </summary>
    /// <param name="context">Source production context receiving Roslyn diagnostics.</param>
    /// <param name="file">Grammar additional file.</param>
    /// <param name="text">Grammar source text used to create line-based locations.</param>
    /// <param name="line">One-based line number associated with the construct.</param>
    /// <param name="constructKind">Human-readable construct kind.</param>
    /// <param name="ownerName">Grammar name associated with the construct.</param>
    private static void ReportEmbeddedMembersInjectedDiagnostic(
        SourceProductionContext context,
        AdditionalText file,
        SourceText text,
        int line,
        string constructKind,
        string ownerName)
    {
        var descriptor = ToRoslynDescriptor(ParserDiagnostics.EmbeddedMembersInjectedByGenerator);
        var location = CreateGrammarLocation(file, text, line);
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, constructKind, ownerName));
    }

    /// <summary>
    /// Reports unsupported diagnostics for lexer actions and lexer predicates inside one lexer rule.
    /// </summary>
    /// <param name="context">Source production context receiving Roslyn diagnostics.</param>
    /// <param name="file">Grammar additional file.</param>
    /// <param name="text">Grammar source text used to create line-based locations.</param>
    /// <param name="rule">Lexer rule to inspect.</param>
    private static void ReportLexerEmbeddedCodeDiagnostics(SourceProductionContext context, AdditionalText file, SourceText text, G4Rule rule)
    {
        foreach (var action in EnumerateEmbeddedActions(rule.Content))
        {
            var constructKind = action.IsPredicate ? "Lexer predicate" : "Lexer action";
            var reason = action.IsPredicate
                ? "Lexer predicates require lexer-state-aware predicate execution and are currently metadata-only."
                : "Lexer actions require a dedicated lexer action execution model and are currently metadata-only.";
            ReportUnsupportedEmbeddedCodeDiagnostic(context, file, text, action.Line, constructKind, rule.Name, reason);
        }
    }

    /// <summary>
    /// Reports unsupported diagnostics for rule lifecycle actions preserved on a rule.
    /// </summary>
    /// <param name="context">Source production context receiving Roslyn diagnostics.</param>
    /// <param name="file">Grammar additional file.</param>
    /// <param name="text">Grammar source text used to create line-based locations.</param>
    /// <param name="rule">Rule to inspect.</param>
    private static void ReportRuleLifecycleDiagnostics(SourceProductionContext context, AdditionalText file, SourceText text, G4Rule rule)
    {
        if (rule.InitAction is not null)
        {
            ReportUnsupportedEmbeddedCodeDiagnostic(
                context,
                file,
                text,
                rule.InitAction.Line,
                "Rule @init action",
                rule.Name,
                "Rule lifecycle actions require a dedicated execution model and are currently metadata-only.");
        }

        if (rule.AfterAction is not null)
        {
            ReportUnsupportedEmbeddedCodeDiagnostic(
                context,
                file,
                text,
                rule.AfterAction.Line,
                "Rule @after action",
                rule.Name,
                "Rule lifecycle actions require a dedicated execution model and are currently metadata-only.");
        }
    }

    /// <summary>
    /// Reports one generic unsupported embedded-code diagnostic at the most precise grammar location currently available.
    /// </summary>
    /// <param name="context">Source production context receiving Roslyn diagnostics.</param>
    /// <param name="file">Grammar additional file.</param>
    /// <param name="text">Grammar source text used to create line-based locations.</param>
    /// <param name="line">One-based line number associated with the construct.</param>
    /// <param name="constructKind">Human-readable construct kind.</param>
    /// <param name="ownerName">Grammar or rule name associated with the construct.</param>
    /// <param name="reason">Construct-specific explanation.</param>
    private static void ReportUnsupportedEmbeddedCodeDiagnostic(
        SourceProductionContext context,
        AdditionalText file,
        SourceText text,
        int line,
        string constructKind,
        string ownerName,
        string reason)
    {
        var descriptor = ToRoslynDescriptor(ParserDiagnostics.EmbeddedCodeConstructNotExecutedByGenerator);
        var location = CreateGrammarLocation(file, text, line);
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, constructKind, ownerName, reason));
    }

    /// <summary>
    /// Enumerates embedded actions and predicates under a grammar content node without assigning execution semantics.
    /// </summary>
    /// <param name="content">Grammar content node to inspect.</param>
    /// <returns>Embedded actions and predicates found under <paramref name="content"/>.</returns>
    private static IEnumerable<G4EmbeddedAction> EnumerateEmbeddedActions(G4Content content)
    {
        if (content is G4EmbeddedAction action)
        {
            yield return action;
        }
        else if (content is G4Alternation alternation)
        {
            foreach (var alternative in alternation.Alternatives)
            {
                foreach (var item in alternative.Items)
                {
                    foreach (var nested in EnumerateEmbeddedActions(item))
                    {
                        yield return nested;
                    }
                }
            }
        }
        else if (content is G4Sequence sequence)
        {
            foreach (var item in sequence.Items)
            {
                foreach (var nested in EnumerateEmbeddedActions(item))
                {
                    yield return nested;
                }
            }
        }
        else if (content is G4Quantifier quantifier)
        {
            foreach (var nested in EnumerateEmbeddedActions(quantifier.Inner))
            {
                yield return nested;
            }
        }
        else if (content is G4Negation negation)
        {
            foreach (var nested in EnumerateEmbeddedActions(negation.Inner))
            {
                yield return nested;
            }
        }
    }

    /// <summary>
    /// Formats a grammar-level action kind for diagnostics.
    /// </summary>
    /// <param name="action">Grammar-level action metadata.</param>
    /// <returns>Human-readable construct kind.</returns>
    private static string FormatGrammarActionKind(G4GrammarAction action)
    {
        var name = string.Equals(action.Name, "members", StringComparison.Ordinal) ? "@members" : "@" + action.Name;
        return action.Target is null
            ? "Grammar " + name + " action"
            : "Grammar @" + action.Target + "::" + action.Name + " action";
    }

    /// <summary>
    /// Formats a grammar-level action reason for diagnostics.
    /// </summary>
    /// <param name="action">Grammar-level action metadata.</param>
    /// <returns>Construct-specific diagnostic reason.</returns>
    private static string FormatGrammarActionReason(G4GrammarAction action)
    {
        if (string.Equals(action.Name, "members", StringComparison.Ordinal))
        {
            return "This members action is not a parser @members block supported by the generated execution context and remains metadata-only.";
        }

        return "Grammar-level actions are preserved as metadata only and are not injected into generated parser or lexer types.";
    }

    /// <summary>
    /// Creates a Roslyn location from a one-based grammar line when line information is available.
    /// </summary>
    /// <param name="file">Grammar additional file.</param>
    /// <param name="text">Grammar source text.</param>
    /// <param name="line">One-based line number.</param>
    /// <returns>A line-based location or <see cref="Location.None"/> when unavailable.</returns>
    private static Location CreateGrammarLocation(AdditionalText file, SourceText text, int line)
    {
        int zeroBasedLine = line - 1;
        if (zeroBasedLine < 0 || zeroBasedLine >= text.Lines.Count)
        {
            return Location.None;
        }

        int start = text.Lines[zeroBasedLine].Start;
        return Location.Create(
            file.Path,
            TextSpan.FromBounds(start, start),
            new LinePositionSpan(new LinePosition(zeroBasedLine, 0), new LinePosition(zeroBasedLine, 0)));
    }

    /// <summary>
    /// Converts a shared parser diagnostic descriptor to a Roslyn diagnostic descriptor.
    /// </summary>
    /// <param name="descriptor">Shared parser diagnostic descriptor.</param>
    /// <returns>Roslyn descriptor with matching code, title, category, message, and severity.</returns>
    private static DiagnosticDescriptor ToRoslynDescriptor(ParserDiagnosticDescriptor descriptor)
    {
        return new DiagnosticDescriptor(
            id: descriptor.Code,
            title: descriptor.Title,
            messageFormat: descriptor.MessageFormat,
            category: descriptor.Category ?? "Utils.Parser",
            defaultSeverity: ToRoslynSeverity(descriptor.Severity),
            isEnabledByDefault: true);
    }

    /// <summary>
    /// Determines whether a shared parser diagnostic is superseded by generator-specific embedded-code diagnostics.
    /// </summary>
    /// <param name="diagnostic">Shared parser diagnostic produced while parsing the grammar.</param>
    /// <returns><c>true</c> when the diagnostic should not be re-reported by the source generator.</returns>
    private static bool IsSupersededEmbeddedCodeParserDiagnostic(ParserDiagnostic diagnostic)
    {
        return diagnostic.Code == ParserDiagnostics.SemanticPredicateNotEnforced.Code
            || diagnostic.Code == ParserDiagnostics.InlineActionStoredNotExecuted.Code;
    }

    /// <summary>
    /// Reports shared parser diagnostics into Roslyn's diagnostic pipeline.
    /// </summary>
    /// <param name="context">Source production context.</param>
    /// <param name="diagnostics">Collected parser diagnostics.</param>
    /// <param name="fileName">Source file name used in message formatting.</param>
    private static void ReportParserDiagnostics(SourceProductionContext context, DiagnosticBag diagnostics, string fileName)
    {
        foreach (var diagnostic in diagnostics)
        {
            if (IsSupersededEmbeddedCodeParserDiagnostic(diagnostic))
            {
                continue;
            }

            var descriptor = new DiagnosticDescriptor(
                id: diagnostic.Code,
                title: diagnostic.Descriptor.Title,
                messageFormat: "{0}",
                category: diagnostic.Descriptor.Category ?? "Utils.Parser",
                defaultSeverity: ToRoslynSeverity(diagnostic.Severity),
                isEnabledByDefault: true);

            context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None, $"{fileName}: {diagnostic.Message}"));
        }
    }

    /// <summary>
    /// Converts shared parser diagnostic severity to Roslyn severity.
    /// </summary>
    /// <param name="severity">Shared diagnostic severity.</param>
    /// <returns>Roslyn severity value.</returns>
    private static RoslynDiagnosticSeverity ToRoslynSeverity(Utils.Parser.Diagnostics.DiagnosticSeverity severity)
    {
        return severity switch
        {
            Utils.Parser.Diagnostics.DiagnosticSeverity.Error => RoslynDiagnosticSeverity.Error,
            Utils.Parser.Diagnostics.DiagnosticSeverity.Warning => RoslynDiagnosticSeverity.Warning,
            Utils.Parser.Diagnostics.DiagnosticSeverity.Info => RoslynDiagnosticSeverity.Info,
            _ => RoslynDiagnosticSeverity.Hidden,
        };
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
