using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

namespace Utils.Parser.Generators;

/// <summary>
/// Immutable project-wide options consumed by the ANTLR4 grammar source generator.
/// </summary>
internal readonly record struct Antlr4GrammarGeneratorOptions(bool EnableGeneratedRuleArgumentBinding)
{
    /// <summary>
    /// Analyzer-config key used by Roslyn for the generated rule-argument binding MSBuild property.
    /// </summary>
    internal const string EnableGeneratedRuleArgumentBindingKey = "build_property.UtilsParserEnableGeneratedRuleArgumentBinding";

    /// <summary>
    /// MSBuild property name that enables generated positional literal rule-argument binding.
    /// </summary>
    internal const string EnableGeneratedRuleArgumentBindingPropertyName = "UtilsParserEnableGeneratedRuleArgumentBinding";

    /// <summary>
    /// Diagnostic emitted when a boolean generator option contains a non-boolean value.
    /// </summary>
    internal static readonly DiagnosticDescriptor InvalidBooleanOptionDescriptor = new DiagnosticDescriptor(
        id:                 "APU0106",
        title:              "Invalid parser generator boolean option",
        messageFormat:      "MSBuild property 'UtilsParserEnableGeneratedRuleArgumentBinding' has invalid value '{0}'. Expected 'true' or 'false'. Generated rule-argument binding remains disabled.",
        category:           "Utils.Parser.Generators",
        defaultSeverity:    RoslynDiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:        "A boolean MSBuild property passed to the parser source generator has an invalid value.");

    /// <summary>
    /// Gets the default generator options used when no MSBuild properties are supplied.
    /// </summary>
    internal static Antlr4GrammarGeneratorOptions Default => new(false);

    /// <summary>
    /// Parses project-wide generator options from Roslyn analyzer config global options.
    /// </summary>
    /// <param name="globalOptions">Analyzer config global options.</param>
    /// <returns>The parsed options and any configuration diagnostics that should be reported.</returns>
    internal static Antlr4GrammarGeneratorOptionsParseResult Parse(AnalyzerConfigOptions globalOptions)
    {
        if (!globalOptions.TryGetValue(EnableGeneratedRuleArgumentBindingKey, out string? rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return new Antlr4GrammarGeneratorOptionsParseResult(Default, ImmutableArray<Diagnostic>.Empty);
        }

        string trimmedValue = rawValue.Trim();
        if (bool.TryParse(trimmedValue, out bool enabled))
        {
            return new Antlr4GrammarGeneratorOptionsParseResult(new Antlr4GrammarGeneratorOptions(enabled), ImmutableArray<Diagnostic>.Empty);
        }

        Diagnostic diagnostic = Diagnostic.Create(InvalidBooleanOptionDescriptor, Location.None, rawValue);
        return new Antlr4GrammarGeneratorOptionsParseResult(Default, ImmutableArray.Create(diagnostic));
    }
}

/// <summary>
/// Immutable result of parsing project-wide ANTLR4 grammar generator options.
/// </summary>
/// <param name="Options">Parsed generator options.</param>
/// <param name="Diagnostics">Diagnostics produced while parsing options.</param>
internal readonly record struct Antlr4GrammarGeneratorOptionsParseResult(
    Antlr4GrammarGeneratorOptions Options,
    ImmutableArray<Diagnostic> Diagnostics);
