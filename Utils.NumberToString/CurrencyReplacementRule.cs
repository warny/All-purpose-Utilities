using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Utils.NumberToString;

/// <summary>
/// A replacement applied to the numeral of a currency amount (unit or subunit) once it is rendered, before the currency
/// noun is appended (NTS-51): Italian <c>uno</c> → <c>un</c> (<c>un euro</c>). It never touches a cardinal, an ordinal
/// or any other conversion.
/// </summary>
/// <remarks>
/// The replacement keeps the semantics of <see cref="NumberToStringConverter.ReplacementRule.Scope"/> on the whole
/// numeral; <c>onScale</c> and <c>onValue</c> have no meaning on a finished numeral and are rejected by the converter.
/// <see cref="ExceptBeforeNoun"/> conditions the rule on the noun the numeral governs, for a form chosen by the onset
/// of the next word (Italian <c>uno scellino</c>, <c>uno zloty</c>: Treccani, "uno", the rules of the article
/// <c>il/lo</c>). Instances are immutable.
/// </remarks>
public sealed class CurrencyReplacementRule
{
    /// <summary>Initializes a new instance of the <see cref="CurrencyReplacementRule"/> class.</summary>
    /// <param name="replacement">The replacement applied to the numeral.</param>
    /// <param name="exceptBeforeNoun">
    /// The beginnings of the currency noun (compared ordinally, ignoring case) before which the replacement does not
    /// apply, or <see langword="null"/> when it always applies. The sequence is copied.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="replacement"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An entry of <paramref name="exceptBeforeNoun"/> is null, empty or white space.</exception>
    public CurrencyReplacementRule(NumberToStringConverter.ReplacementRule replacement, IEnumerable<string>? exceptBeforeNoun = null)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        var prefixes = (exceptBeforeNoun ?? []).ToImmutableArray();
        if (prefixes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException(
                $"The noun beginnings of the currency replacement '{replacement.OldValue}' must not be empty.", nameof(exceptBeforeNoun));
        Replacement = replacement;
        ExceptBeforeNoun = prefixes;
    }

    /// <summary>Gets the replacement applied to the numeral.</summary>
    public NumberToStringConverter.ReplacementRule Replacement { get; }

    /// <summary>
    /// Gets the beginnings of the currency noun before which the replacement does not apply (empty when it always
    /// applies).
    /// </summary>
    public IReadOnlyList<string> ExceptBeforeNoun { get; }

    /// <summary>Determines whether the replacement applies before <paramref name="noun"/>.</summary>
    /// <param name="noun">The currency noun the numeral governs.</param>
    /// <returns><see langword="true"/> when no entry of <see cref="ExceptBeforeNoun"/> begins the noun.</returns>
    internal bool AppliesBefore(string noun)
    {
        foreach (string prefix in ExceptBeforeNoun)
            if (noun.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
        return true;
    }
}
