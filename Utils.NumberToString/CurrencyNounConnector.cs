using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Utils.NumberToString;

/// <summary>
/// The connector between the numeral of a currency amount (unit or subunit) and the currency noun it governs (NTS-52):
/// a default, then the contextual <see cref="Substitutions"/> that differ from it. Spanish <c>un millón de euros</c>,
/// French <c>un-million d'euros</c> / <c>un-million de francs</c>, Romanian <c>douăzeci de lei</c>.
/// </summary>
/// <remarks>
/// <para>
/// For each amount, the substitutions whose constraints are all satisfied are collected; the one with the greatest
/// <see cref="CurrencyNounConnectorRule.Specificity"/> wins and, at equal specificity, the first declared wins (the
/// declaration order is authoritative, never an ambiguity error). With no match the default applies.
/// </para>
/// <para>
/// The amount is assembled as <c>numeral + Separator + noun</c> when the selected connector is empty, otherwise as
/// <c>numeral + Separator + connector + nounSeparator + noun</c>, the noun separator defaulting to the language's
/// <c>Separator</c>. Instances are immutable; the substitutions are copied in the order supplied.
/// </para>
/// </remarks>
public sealed class CurrencyNounConnector
{
    /// <summary>Initializes a new instance of the <see cref="CurrencyNounConnector"/> class.</summary>
    /// <param name="defaultValue">The connector applying when no substitution matches; empty (the default) for none.</param>
    /// <param name="defaultNounSeparator">
    /// The text between the default connector and the noun, or <see langword="null"/> for the language's
    /// <c>Separator</c>. Ignored when <paramref name="defaultValue"/> is empty.
    /// </param>
    /// <param name="substitutions">The contextual substitutions, in declaration order, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="defaultValue"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="defaultValue"/> is white space, or a substitution is <see langword="null"/>.
    /// </exception>
    public CurrencyNounConnector(
        string defaultValue = "",
        string? defaultNounSeparator = null,
        IEnumerable<CurrencyNounConnectorRule>? substitutions = null)
    {
        ArgumentNullException.ThrowIfNull(defaultValue);
        if (defaultValue.Length > 0 && string.IsNullOrWhiteSpace(defaultValue))
            throw new ArgumentException("The default currency noun connector must not be white space.", nameof(defaultValue));
        var snapshot = (substitutions ?? []).ToImmutableArray();
        foreach (var rule in snapshot)
            if (rule is null)
                throw new ArgumentException("Currency noun connector substitutions must not be null.", nameof(substitutions));
        Default = defaultValue;
        DefaultNounSeparator = defaultNounSeparator;
        Substitutions = snapshot;
    }

    /// <summary>Gets the connector applying when no substitution matches (empty for none).</summary>
    public string Default { get; }

    /// <summary>
    /// Gets the text between the default connector and the noun, or <see langword="null"/> for the language's
    /// <c>Separator</c>.
    /// </summary>
    public string? DefaultNounSeparator { get; }

    /// <summary>Gets the contextual substitutions, in declaration order.</summary>
    public IReadOnlyList<CurrencyNounConnectorRule> Substitutions { get; }
}
