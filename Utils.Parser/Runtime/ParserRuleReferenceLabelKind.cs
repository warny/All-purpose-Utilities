namespace Utils.Parser.Runtime;

/// <summary>
/// Identifies the kind of a parser rule-reference label.
/// Labels are metadata only: no implicit variables, automatic binding, or typed fields are generated.
/// </summary>
public enum ParserRuleReferenceLabelKind
{
    /// <summary>No label is attached to the rule reference.</summary>
    None = 0,

    /// <summary>Assignment label (<c>x=child</c>): assigns the single match result.</summary>
    Assignment = 1,

    /// <summary>List label (<c>xs+=child</c>): appends to a list of match results.</summary>
    List = 2,
}
