namespace Utils.Parser.EmbeddedCode;

/// <summary>
/// Identifies the ANTLR embedded-code construct that owns a raw source block.
/// </summary>
public enum EmbeddedCodeKind
{
    /// <summary>Semantic predicate source from a parser construct such as <c>{ condition }?</c>.</summary>
    SemanticPredicate,

    /// <summary>Inline parser action source from a parser alternative construct such as <c>{ code }</c>.</summary>
    ParserInlineAction,

    /// <summary>Rule initialization action source from a rule prequel construct such as <c>@init { }</c>.</summary>
    RuleInitAction,

    /// <summary>Rule finalization action source from a rule prequel construct such as <c>@after { }</c>.</summary>
    RuleAfterAction,

    /// <summary>Grammar-level action source from a prequel construct such as <c>@members { }</c>.</summary>
    GrammarAction
}
