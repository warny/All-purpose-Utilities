using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Linq;
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


    private static readonly DiagnosticDescriptor s_invalidGeneratedRuleArgumentBindingDescriptor = new DiagnosticDescriptor(
        id:                 "APU0107",
        title:              "Invalid generated rule-call argument binding",
        messageFormat:      "Rule call to '{0}' cannot use generated positional rule-argument binding: {1}",
        category:           "Utils.Parser.Generators",
        defaultSeverity:    RoslynDiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:        "Generated-C# positional rule-call argument binding can be emitted only for locally resolved parser-rule calls that satisfy the exact literal binding contract.");

    private static readonly DiagnosticDescriptor s_unsupportedSuperClassDescriptor = new DiagnosticDescriptor(
        id:                 "APU0103",
        title:              "ANTLR superClass is not applied by generator",
        messageFormat:      "Grammar '{0}' declares superClass='{1}', but generated runtime inheritance is not supported and the option is kept as metadata only.",
        category:           "Utils.Parser.Generators",
        defaultSeverity:    RoslynDiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:        "The current generator keeps ANTLR superClass in options metadata but does not change generated type inheritance.");


    /// <summary>Immutable parsed grammar-file model carried through the incremental pipeline.</summary>
    private sealed record ParsedGrammarFile(
        AdditionalText File,
        string Path,
        string FileName,
        SourceText? Text,
        string Source,
        string NamespaceName,
        string ClassName,
        G4Grammar? Grammar,
        DiagnosticBag ParseDiagnostics,
        bool MetadataValid,
        string MetadataError,
        Antlr4GrammarGeneratorOptions Options,
        string? ParseExceptionMessage = null);
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var generatorOptions = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => Antlr4GrammarGeneratorOptions.Parse(provider.GlobalOptions));

        context.RegisterSourceOutput(generatorOptions, static (productionContext, options) =>
        {
            foreach (Diagnostic diagnostic in options.Diagnostics)
            {
                productionContext.ReportDiagnostic(diagnostic);
            }
        });

        var grammarFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".g4", StringComparison.OrdinalIgnoreCase));

        var parsedGrammarFiles = grammarFiles
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Combine(generatorOptions)
            .Select(static (source, cancellationToken) => ParseGrammarFile(source.Left.Left, source.Left.Right, source.Right.Options, cancellationToken));

        var parsedGrammarProject = parsedGrammarFiles.Collect().Combine(generatorOptions);

        context.RegisterSourceOutput(parsedGrammarProject, static (productionContext, source) =>
        {
            ProcessGrammarProject(productionContext, source.Left, source.Right.Options);
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
    /// Parses one grammar additional file into an immutable per-file model used by project-wide validation and per-file emission.
    /// </summary>
    /// <param name="file">Grammar additional file being processed.</param>
    /// <param name="optionsProvider">Analyzer config options provider associated with the current run.</param>
    /// <param name="generatorOptions">Project-wide generator options.</param>
    /// <param name="cancellationToken">Cancellation token for reading the file text.</param>
    /// <returns>The parsed grammar-file model.</returns>
    private static ParsedGrammarFile ParseGrammarFile(AdditionalText file, AnalyzerConfigOptionsProvider optionsProvider, Antlr4GrammarGeneratorOptions generatorOptions, CancellationToken cancellationToken)
    {
        var text = file.GetText(cancellationToken);
        string fileName = Path.GetFileName(file.Path);
        var options = optionsProvider.GetOptions(file);

        options.TryGetValue("build_metadata.AdditionalFiles.Namespace", out var namespaceName);
        options.TryGetValue("build_metadata.AdditionalFiles.ClassName",  out var className);

        if (string.IsNullOrWhiteSpace(className))
            className = Path.GetFileNameWithoutExtension(file.Path);

        if (string.IsNullOrWhiteSpace(namespaceName))
            namespaceName = string.Empty;

        string metadataError = string.Empty;
        bool metadataValid = SyntaxColorizationValidation.TryValidateTypeMetadata(namespaceName!, className!, out metadataError);
        if (text == null || !metadataValid)
        {
            return new ParsedGrammarFile(file, file.Path, fileName, text, string.Empty, namespaceName!, className!, null, new DiagnosticBag(), metadataValid, metadataError, generatorOptions);
        }

        try
        {
            string source = text.ToString();
            var diagnostics = new DiagnosticBag();
            var grammar = new G4Parser(new G4Tokenizer(source).Tokenize(), diagnostics).Parse();
            return new ParsedGrammarFile(file, file.Path, fileName, text, source, namespaceName!, className!, grammar, diagnostics, true, string.Empty, generatorOptions);
        }
        catch (Exception ex)
        {
            return new ParsedGrammarFile(file, file.Path, fileName, text, text.ToString(), namespaceName!, className!, null, new DiagnosticBag(), true, string.Empty, generatorOptions, ex.Message);
        }
    }

    /// <summary>
    /// Processes all parsed project grammars so imported-rule diagnostics can use a project-wide index without reparsing files.
    /// </summary>
    /// <param name="context">Context used to report diagnostics and add generated files.</param>
    /// <param name="files">Parsed grammar files collected from project AdditionalFiles.</param>
    /// <param name="generatorOptions">Project-wide options parsed from analyzer config global options.</param>
    private static void ProcessGrammarProject(SourceProductionContext context, System.Collections.Immutable.ImmutableArray<ParsedGrammarFile> files, Antlr4GrammarGeneratorOptions generatorOptions)
    {
        var validGrammars = files
            .Where(static file => file.MetadataValid && file.Grammar is not null)
            .Select(static file => new G4GrammarProjectEntry(file.Path, file.Grammar!))
            .ToArray();
        var index = new G4GrammarProjectIndex(validGrammars);
        var resolver = new G4ImportedRuleResolver(index);

        foreach (var file in files.OrderBy(static file => file.Path, StringComparer.Ordinal))
        {
            ProcessParsedGrammarFile(context, file, generatorOptions, resolver);
        }
    }

    /// <summary>
    /// Emits diagnostics and source for one parsed grammar file.
    /// </summary>
    /// <param name="context">Context used to report diagnostics and add generated files.</param>
    /// <param name="file">Parsed grammar file.</param>
    /// <param name="generatorOptions">Project-wide options parsed from analyzer config global options.</param>
    /// <param name="resolver">Project-wide imported-rule resolver.</param>
    private static void ProcessParsedGrammarFile(SourceProductionContext context, ParsedGrammarFile file, Antlr4GrammarGeneratorOptions generatorOptions, G4ImportedRuleResolver resolver)
    {
        if (!file.MetadataValid)
        {
            context.ReportDiagnostic(Diagnostic.Create(s_invalidMetadataDescriptor, Location.None, file.FileName, file.MetadataError));
            return;
        }

        if (file.Text == null)
        {
            return;
        }

        if (file.Grammar is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(s_errorDescriptor, Location.None, file.FileName, file.ParseExceptionMessage ?? "Unable to parse grammar."));
            return;
        }

        var grammar = file.Grammar;
        if (grammar.Options.TryGetValue("superClass", out var superClass) && !string.IsNullOrWhiteSpace(superClass))
        {
            context.ReportDiagnostic(Diagnostic.Create(s_unsupportedSuperClassDescriptor, Location.None, grammar.Name, superClass));
        }
        ReportEmbeddedParserAttributeDiagnostics(context, file.File, file.Text, grammar);
        ReportUnsupportedEmbeddedCodeDiagnostics(context, file.File, file.Text, grammar);
        if (generatorOptions.EnableGeneratedRuleArgumentBinding
            && ReportGeneratedRuleArgumentBindingDiagnostics(context, file.File, file.Text, grammar, resolver))
        {
            ReportParserDiagnostics(context, file.ParseDiagnostics, file.FileName);
            return;
        }

        var generated = GrammarEmitter.Emit(
            grammar,
            file.NamespaceName,
            file.ClassName,
            file.FileName,
            enableGeneratedRuleArgumentBinding: generatorOptions.EnableGeneratedRuleArgumentBinding);
        ReportParserDiagnostics(context, file.ParseDiagnostics, file.FileName);

        context.AddSource($"{file.ClassName}.Grammar.g.cs", SourceText.From(generated, Encoding.UTF8));
    }

    /// <summary>
    /// Reports generated-C# rule-call argument binding diagnostics attached to grammar call sites.
    /// </summary>
    /// <param name="context">Source production context receiving Roslyn diagnostics.</param>
    /// <param name="file">Grammar additional file.</param>
    /// <param name="text">Grammar source text used to create locations.</param>
    /// <param name="grammar">Parsed grammar AST.</param>
    /// <param name="resolver">Project-wide resolver that identifies unique local or imported parser-rule targets.</param>
    /// <returns><see langword="true"/> when at least one deterministic binding error was reported.</returns>
    private static bool ReportGeneratedRuleArgumentBindingDiagnostics(SourceProductionContext context, AdditionalText file, SourceText text, G4Grammar grammar, G4ImportedRuleResolver resolver)
    {
        bool hasErrors = false;
        foreach (GeneratedRuleArgumentBindingIssue issue in GeneratedRuleArgumentBindingValidator.Validate(grammar, callSite => resolver.Resolve(grammar, callSite.RuleName)))
        {
            hasErrors = true;
            context.ReportDiagnostic(Diagnostic.Create(
                s_invalidGeneratedRuleArgumentBindingDescriptor,
                CreateGrammarLocation(file, text, issue.CallSite.Line, issue.CallSite.Column, issue.CallSite.RuleName.Length),
                issue.TargetRuleName,
                issue.Reason));
        }

        return hasErrors;
    }

    /// <summary>
    /// Reports deterministic validation errors for the limited embedded parser attribute rewrite.
    /// </summary>
    /// <param name="context">Source production context receiving diagnostics.</param>
    /// <param name="file">Grammar additional file.</param>
    /// <param name="text">Grammar source text.</param>
    /// <param name="grammar">Parsed grammar AST.</param>
    /// <returns><see langword="true"/> when generation must stop because at least one attribute error was found.</returns>
    private static bool ReportEmbeddedParserAttributeDiagnostics(SourceProductionContext context, AdditionalText file, SourceText text, G4Grammar grammar)
    {
        bool hasErrors = false;
        DiagnosticDescriptor descriptor = ToRoslynDescriptor(ParserDiagnostics.InvalidEmbeddedParserAttribute);
        foreach (EmbeddedParserAttributeDiagnostic diagnostic in EmbeddedParserAttributeRewriter.ValidateGrammar(grammar))
        {
            hasErrors = true;
            context.ReportDiagnostic(Diagnostic.Create(
                descriptor,
                CreateGrammarLocation(file, text, diagnostic.Line),
                grammar.Name,
                diagnostic.Message));
        }

        return hasErrors;
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
            if (EmbeddedMembersSupport.IsInjectableParserHeaderAction(grammar, action))
            {
                ReportEmbeddedHeaderInjectedDiagnostic(context, file, text, action.Line, constructKind, grammar.Name);
                continue;
            }

            if (EmbeddedMembersSupport.IsInjectableParserMembersAction(grammar, action))
            {
                ReportEmbeddedMembersInjectedDiagnostic(context, file, text, action.Line, constructKind, grammar.Name);
                continue;
            }

            if (EmbeddedMembersSupport.IsInjectableParserFooterAction(grammar, action))
            {
                ReportEmbeddedFooterInjectedDiagnostic(context, file, text, action.Line, constructKind, grammar.Name);
                continue;
            }

            if (EmbeddedMembersSupport.IsInjectableLexerHeaderAction(grammar, action))
            {
                ReportEmbeddedHeaderInjectedDiagnostic(context, file, text, action.Line, constructKind, grammar.Name);
                continue;
            }

            if (EmbeddedMembersSupport.IsInjectableLexerMembersAction(grammar, action))
            {
                ReportEmbeddedMembersInjectedDiagnostic(context, file, text, action.Line, constructKind, grammar.Name);
                continue;
            }

            if (EmbeddedMembersSupport.IsInjectableLexerFooterAction(grammar, action))
            {
                ReportEmbeddedFooterInjectedDiagnostic(context, file, text, action.Line, constructKind, grammar.Name);
                continue;
            }

            var reason = EmbeddedMembersSupport.FormatUnsupportedReason(grammar, action);
            ReportUnsupportedEmbeddedCodeDiagnostic(context, file, text, action.Line, constructKind, grammar.Name, reason);
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
    /// Reports the compatibility warning used when parser headers are injected into generated C# source.
    /// </summary>
    /// <param name="context">Source production context receiving Roslyn diagnostics.</param>
    /// <param name="file">Grammar additional file.</param>
    /// <param name="text">Grammar source text used to create line-based locations.</param>
    /// <param name="line">One-based line number associated with the construct.</param>
    /// <param name="constructKind">Human-readable construct kind.</param>
    /// <param name="ownerName">Grammar name associated with the construct.</param>
    private static void ReportEmbeddedHeaderInjectedDiagnostic(
        SourceProductionContext context,
        AdditionalText file,
        SourceText text,
        int line,
        string constructKind,
        string ownerName)
    {
        var descriptor = ToRoslynDescriptor(ParserDiagnostics.EmbeddedHeaderInjectedByGenerator);
        var location = CreateGrammarLocation(file, text, line);
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, constructKind, ownerName));
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
    /// Reports the compatibility warning used when parser footers are injected near the end of generated C# source.
    /// </summary>
    /// <param name="context">Source production context receiving Roslyn diagnostics.</param>
    /// <param name="file">Grammar additional file.</param>
    /// <param name="text">Grammar source text used to create line-based locations.</param>
    /// <param name="line">One-based line number associated with the construct.</param>
    /// <param name="constructKind">Human-readable construct kind.</param>
    /// <param name="ownerName">Grammar name associated with the construct.</param>
    private static void ReportEmbeddedFooterInjectedDiagnostic(
        SourceProductionContext context,
        AdditionalText file,
        SourceText text,
        int line,
        string constructKind,
        string ownerName)
    {
        var descriptor = ToRoslynDescriptor(ParserDiagnostics.EmbeddedFooterInjectedByGenerator);
        var location = CreateGrammarLocation(file, text, line);
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, constructKind, ownerName));
    }

    /// <summary>
    /// Keeps the lexer embedded-code diagnostic hook explicit; simple lexer actions and predicates are generated by opt-in policies.
    /// </summary>
    /// <param name="context">Source production context receiving Roslyn diagnostics.</param>
    /// <param name="file">Grammar additional file.</param>
    /// <param name="text">Grammar source text used to create line-based locations.</param>
    /// <param name="rule">Lexer rule to inspect.</param>
    private static void ReportLexerEmbeddedCodeDiagnostics(SourceProductionContext context, AdditionalText file, SourceText text, G4Rule rule)
    {
        _ = context;
        _ = file;
        _ = text;
        _ = rule;
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
    /// Creates a Roslyn location from a one-based grammar line when line information is available.
    /// </summary>
    /// <param name="file">Grammar additional file.</param>
    /// <param name="text">Grammar source text.</param>
    /// <param name="line">One-based line number.</param>
    /// <returns>A line-based location or <see cref="Location.None"/> when unavailable.</returns>
    private static Location CreateGrammarLocation(AdditionalText file, SourceText text, int line)
    {
        return CreateGrammarLocation(file, text, line, 0, 0);
    }

    /// <summary>
    /// Creates a Roslyn location from grammar source coordinates when line and column information is available.
    /// </summary>
    /// <param name="file">Grammar additional file.</param>
    /// <param name="text">Grammar source text.</param>
    /// <param name="line">One-based line number.</param>
    /// <param name="column">Zero-based column number.</param>
    /// <param name="length">Source length to highlight.</param>
    /// <returns>A source location or <see cref="Location.None"/> when unavailable.</returns>
    private static Location CreateGrammarLocation(AdditionalText file, SourceText text, int line, int column, int length)
    {
        int zeroBasedLine = line - 1;
        if (zeroBasedLine < 0 || zeroBasedLine >= text.Lines.Count)
        {
            return Location.None;
        }

        int lineStart = text.Lines[zeroBasedLine].Start;
        int boundedColumn = Math.Max(0, Math.Min(column, text.Lines[zeroBasedLine].Span.Length));
        int start = lineStart + boundedColumn;
        int end = Math.Min(start + Math.Max(0, length), text.Lines[zeroBasedLine].End);
        return Location.Create(
            file.Path,
            TextSpan.FromBounds(start, end),
            new LinePositionSpan(new LinePosition(zeroBasedLine, boundedColumn), new LinePosition(zeroBasedLine, boundedColumn + Math.Max(0, end - start))));
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
