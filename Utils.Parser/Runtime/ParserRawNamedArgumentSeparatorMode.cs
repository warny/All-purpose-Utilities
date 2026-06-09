namespace Utils.Parser.Runtime;

/// <summary>
/// Specifies which separator characters are recognized when parsing named raw rule-call argument slices.
/// </summary>
public enum ParserRawNamedArgumentSeparatorMode
{
    /// <summary>Only <c>:</c> is recognized as a name–value separator.</summary>
    ColonOnly,

    /// <summary>Only <c>=</c> is recognized as a name–value separator.</summary>
    EqualsOnly,

    /// <summary>Either <c>:</c> or <c>=</c> (whichever appears first at top level) is accepted.</summary>
    ColonOrEquals,
}
