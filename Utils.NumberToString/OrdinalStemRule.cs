namespace Utils.NumberToString;

/// <summary>
/// Describes an ordinal stem rule: when the last word that receives the ordinal suffix ends with
/// <see cref="From"/>, that ending is replaced with <see cref="To"/> before the suffix is appended.
/// </summary>
/// <remarks>
/// Unlike an <c>&lt;Ordinal from="..." to="..."&gt;</c> word rule, which replaces the whole last word
/// with a complete ordinal, a stem rule only rewrites the ending of the word and still lets the
/// effective ordinal suffix (base or variant) be appended. Matching is ordinal and case-sensitive;
/// when several rules match, the longest <see cref="From"/> wins.
/// </remarks>
/// <param name="From">The ending to match; must not be empty.</param>
/// <param name="To">The replacement ending; may be empty to remove the matched ending.</param>
public sealed record OrdinalStemRule(string From, string To);
