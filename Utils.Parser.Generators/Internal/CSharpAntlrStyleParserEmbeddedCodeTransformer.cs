using System.Collections.Generic;
using System.Linq;
using Utils.Parser.Diagnostics.EmbeddedCode;

namespace Utils.Parser.Generators.Internal;

/// <summary>
/// Optional C# transformer for the generator's conservative ANTLR-style parser attribute rewrite.
/// </summary>
internal sealed class CSharpAntlrStyleParserEmbeddedCodeTransformer : IParserEmbeddedCodeTransformer
{
    private readonly G4Grammar _grammar;
    private readonly IReadOnlyDictionary<string, G4Rule> _rules;

    /// <summary>
    /// Initializes a new transformer for one parsed grammar.
    /// </summary>
    /// <param name="grammar">Parsed grammar that owns transformed code blocks.</param>
    public CSharpAntlrStyleParserEmbeddedCodeTransformer(G4Grammar grammar)
    {
        if (grammar is null) throw new System.ArgumentNullException(nameof(grammar));
        _grammar = grammar;
        _rules = grammar.ParserRules.ToDictionary(static rule => rule.Name, System.StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public ParserEmbeddedCodeTransformationResult Transform(ParserEmbeddedCodeTransformationContext context)
    {
        if (context is null) throw new System.ArgumentNullException(nameof(context));
        if (context.RuleName is null || !_rules.TryGetValue(context.RuleName, out G4Rule? rule))
        {
            // Parser and lexer header/member/footer blocks are not tied to a current parser rule.
            // ANTLR-style current-rule attribute rewriting is intentionally skipped there.
            return new ParserEmbeddedCodeTransformationResult { Code = context.Code };
        }

        EmbeddedParserAttributeLocationKind kind = context.Location switch
        {
            ParserEmbeddedCodeLocation.RuleInit => EmbeddedParserAttributeLocationKind.Init,
            ParserEmbeddedCodeLocation.RuleAfter => EmbeddedParserAttributeLocationKind.After,
            ParserEmbeddedCodeLocation.SemanticPredicate => EmbeddedParserAttributeLocationKind.Predicate,
            _ => EmbeddedParserAttributeLocationKind.InlineAction
        };
        EmbeddedParserAttributeRewriteResult result = EmbeddedParserAttributeRewriter.Rewrite(context.Code, _grammar, rule, kind);
        var diagnostics = result.Errors.Select(static error => new ParserEmbeddedCodeDiagnostic
        {
            Message = error,
            Severity = ParserEmbeddedCodeDiagnosticSeverity.Error,
            Code = "APU0104"
        }).ToArray();
        return new ParserEmbeddedCodeTransformationResult
        {
            Code = result.Code,
            Diagnostics = diagnostics
        };
    }
}
