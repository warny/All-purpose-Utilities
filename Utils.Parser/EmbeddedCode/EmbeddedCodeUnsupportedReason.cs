namespace Utils.Parser.EmbeddedCode;

/// <summary>
/// Identifies why an embedded-code item is outside a runtime execution path's supported scope.
/// </summary>
public enum EmbeddedCodeUnsupportedReason
{
    /// <summary>The embedded code is supported by the selected runtime path.</summary>
    None,

    /// <summary>The embedded code is a grammar-level action such as <c>@members</c>.</summary>
    GrammarAction,

    /// <summary>The embedded code is a rule initialization action such as <c>@init</c>.</summary>
    RuleInitAction,

    /// <summary>The embedded code is a rule finalization action such as <c>@after</c>.</summary>
    RuleAfterAction,

    /// <summary>The embedded code is an opaque lexer action and is not executed by parser runtime hooks.</summary>
    LexerAction,

    /// <summary>The embedded code is a lexer predicate and is not executed by parser runtime hooks.</summary>
    LexerPredicate,

    /// <summary>The embedded code is a parser action that is not declared inline inside an alternative.</summary>
    NonInlineParserAction,

    /// <summary>The embedded action context is not supported by the selected runtime path.</summary>
    UnsupportedActionContext,

    /// <summary>The embedded action position is not supported by the selected runtime path.</summary>
    UnsupportedActionPosition,

    /// <summary>The embedded-code kind is not supported by the selected runtime path.</summary>
    UnsupportedEmbeddedCodeKind,

    /// <summary>The runtime alternative or element index required for dispatch could not be determined.</summary>
    MissingRuntimeIndex,

    /// <summary>The containing runtime shape cannot be mapped safely to parser dispatch metadata.</summary>
    UnsupportedRuntimeShape
}
