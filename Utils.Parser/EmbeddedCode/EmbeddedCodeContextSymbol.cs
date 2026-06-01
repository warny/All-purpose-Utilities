namespace Utils.Parser.EmbeddedCode;

/// <summary>
/// Identifies a contextual symbol that an embedded-code preparer may make available to source code.
/// </summary>
internal enum EmbeddedCodeContextSymbol
{
    /// <summary>Name of the parser rule that owns the embedded source block.</summary>
    RuleName,

    /// <summary>Current input position associated with the embedded source block.</summary>
    InputPosition,

    /// <summary>Zero-based alternative index associated with the embedded source block when known.</summary>
    AlternativeIndex,

    /// <summary>Zero-based element index associated with the embedded source block when known.</summary>
    ElementIndex
}
