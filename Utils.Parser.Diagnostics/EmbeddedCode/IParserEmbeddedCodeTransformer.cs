using System;
using System.Collections.Generic;
using Utils.Parser.Source;

namespace Utils.Parser.Diagnostics.EmbeddedCode;

/// <summary>
/// Transforms target-language parser embedded code before generated emission or dynamic compilation.
/// </summary>
public interface IParserEmbeddedCodeTransformer
{
    /// <summary>
    /// Transforms one embedded code block using grammar and rule metadata supplied by the caller.
    /// </summary>
    /// <param name="context">Transformation context containing the raw code and passive metadata.</param>
    /// <returns>The transformed code and optional diagnostics.</returns>
    ParserEmbeddedCodeTransformationResult Transform(ParserEmbeddedCodeTransformationContext context);
}

/// <summary>
/// Transformation result returned by <see cref="IParserEmbeddedCodeTransformer"/>.
/// </summary>
public sealed class ParserEmbeddedCodeTransformationResult
{
    /// <summary>
    /// Gets the code to emit or compile after transformation.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets diagnostics reported by the transformer.
    /// </summary>
    public IReadOnlyList<ParserEmbeddedCodeDiagnostic> Diagnostics { get; set; } = Array.Empty<ParserEmbeddedCodeDiagnostic>();
}

/// <summary>
/// Diagnostic reported while transforming embedded parser code.
/// </summary>
public sealed class ParserEmbeddedCodeDiagnostic
{
    /// <summary>
    /// Gets the human-readable diagnostic message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets the diagnostic severity.
    /// </summary>
    public ParserEmbeddedCodeDiagnosticSeverity Severity { get; set; }

    /// <summary>
    /// Gets an optional stable diagnostic code.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Gets an optional source span associated with the embedded-code fragment.
    /// </summary>
    public SourceSpan? Span { get; set; }
}

/// <summary>
/// Severity of an embedded-code transformation diagnostic.
/// </summary>
public enum ParserEmbeddedCodeDiagnosticSeverity
{
    /// <summary>Informational diagnostic.</summary>
    Info,

    /// <summary>Warning diagnostic.</summary>
    Warning,

    /// <summary>Error diagnostic that prevents safe emission or compilation.</summary>
    Error
}

/// <summary>
/// Identifies the grammar location of an embedded parser code block.
/// </summary>
public enum ParserEmbeddedCodeLocation
{
    /// <summary>Parser header action.</summary>
    ParserHeader = 0,

    /// <summary>Parser footer action.</summary>
    ParserFooter = 1,

    /// <summary>Parser members action.</summary>
    ParserMembers = 2,

    /// <summary>Rule <c>@init</c> action.</summary>
    RuleInit = 3,

    /// <summary>Rule <c>@after</c> action.</summary>
    RuleAfter = 4,

    /// <summary>Inline parser action.</summary>
    InlineAction = 5,

    /// <summary>Semantic predicate.</summary>
    SemanticPredicate = 6,

    /// <summary>Lexer header action.</summary>
    LexerHeader = 7,

    /// <summary>Lexer members action.</summary>
    LexerMembers = 8,

    /// <summary>Lexer footer action.</summary>
    LexerFooter = 9,

    /// <summary>Inline lexer action.</summary>
    LexerInlineAction = 10,

    /// <summary>Lexer semantic predicate.</summary>
    LexerSemanticPredicate = 11
}

/// <summary>
/// Context supplied to embedded-code transformers.
/// </summary>
public sealed class ParserEmbeddedCodeTransformationContext
{
    /// <summary>Gets the raw embedded code exactly as represented by the grammar parser.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets the grammar location that owns the embedded code.</summary>
    public ParserEmbeddedCodeLocation Location { get; set; }

    /// <summary>Gets the owning grammar name when available.</summary>
    public string? GrammarName { get; set; }

    /// <summary>Gets the owning parser rule name when available.</summary>
    public string? RuleName { get; set; }

    /// <summary>Gets passive parameter metadata visible to the code block.</summary>
    public IReadOnlyList<ParserEmbeddedRuleDeclarationDescriptor> Parameters { get; set; } = Array.Empty<ParserEmbeddedRuleDeclarationDescriptor>();

    /// <summary>Gets passive local metadata visible to the code block.</summary>
    public IReadOnlyList<ParserEmbeddedRuleDeclarationDescriptor> Locals { get; set; } = Array.Empty<ParserEmbeddedRuleDeclarationDescriptor>();

    /// <summary>Gets passive return metadata visible to the code block.</summary>
    public IReadOnlyList<ParserEmbeddedRuleDeclarationDescriptor> Returns { get; set; } = Array.Empty<ParserEmbeddedRuleDeclarationDescriptor>();

    /// <summary>Gets parser rule-reference labels visible to the code block.</summary>
    public IReadOnlyDictionary<string, ParserEmbeddedRuleLabelDescriptor> Labels { get; set; } = EmptyLabels;

    /// <summary>Gets an empty label map shared by default contexts.</summary>
    public static IReadOnlyDictionary<string, ParserEmbeddedRuleLabelDescriptor> EmptyLabels { get; } = new Dictionary<string, ParserEmbeddedRuleLabelDescriptor>();
}

/// <summary>
/// Passive declaration descriptor for transformer metadata.
/// </summary>
public sealed class ParserEmbeddedRuleDeclarationDescriptor
{
    /// <summary>Gets the declaration name when one can be conservatively extracted.</summary>
    public string? Name { get; set; }

    /// <summary>Gets the raw declaration text.</summary>
    public string RawDeclaration { get; set; } = string.Empty;
}

/// <summary>
/// Describes one rule-reference label visible to embedded-code transformers.
/// </summary>
public sealed class ParserEmbeddedRuleLabelDescriptor
{
    /// <summary>Gets the label name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets a value indicating whether the label is a list label.</summary>
    public bool IsList { get; set; }

    /// <summary>Gets all parser rule names targeted by the label.</summary>
    public IReadOnlyList<string> RuleNames { get; set; } = Array.Empty<string>();
}
