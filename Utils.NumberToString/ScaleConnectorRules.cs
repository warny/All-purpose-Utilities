using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Utils.NumberToString;

/// <summary>
/// The connector between the multiplier of a scale group and its scale noun (NTS-53): a default, then the contextual
/// <see cref="Substitutions"/> that differ from it. Romanian, after the Academy grammar (GALR 2005): <c>de</c> from
/// twenty (<c>douăzeci de mii</c>, <c>o sută douăzeci de mii</c>) except after a multiplier whose last two digits are one
/// to nineteen (<c>o sută nouăsprezece mii</c>).
/// </summary>
/// <remarks>
/// <para>
/// For each group, the substitutions whose constraints are all satisfied are collected; the one with the greatest
/// <see cref="ScaleConnectorRule.Specificity"/> wins and, at equal specificity, the first declared wins (the declaration
/// order is authoritative, never an ambiguity error). With no match, the legacy
/// <see cref="NumberToStringConverterOptions.ScaleConnector"/> shorthand applies when its threshold is reached (it acts
/// as the last declared substitution, on <c>numberRange="threshold.."</c>), and the default otherwise.
/// </para>
/// <para>
/// A group is assembled as <c>multiplier + Separator + connector + Separator + scale</c> when the selected connector is
/// not empty, otherwise with the historical multiplier/scale junction. Instances are immutable; the substitutions are
/// copied in the order supplied.
/// </para>
/// </remarks>
public sealed class ScaleConnectorRules
{
    /// <summary>Initializes a new instance of the <see cref="ScaleConnectorRules"/> class.</summary>
    /// <param name="defaultValue">The connector applying when nothing else matches; empty (the default) for none.</param>
    /// <param name="substitutions">The contextual substitutions, in declaration order, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="defaultValue"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="defaultValue"/> is white space, or a substitution is <see langword="null"/>.
    /// </exception>
    public ScaleConnectorRules(string defaultValue = "", IEnumerable<ScaleConnectorRule>? substitutions = null)
    {
        ArgumentNullException.ThrowIfNull(defaultValue);
        if (defaultValue.Length > 0 && string.IsNullOrWhiteSpace(defaultValue))
            throw new ArgumentException("The default scale connector must not be white space.", nameof(defaultValue));
        var snapshot = (substitutions ?? []).ToImmutableArray();
        foreach (var rule in snapshot)
            if (rule is null)
                throw new ArgumentException("Scale connector substitutions must not be null.", nameof(substitutions));
        Default = defaultValue;
        Substitutions = snapshot;
    }

    /// <summary>Gets the connector applying when nothing else matches (empty for none).</summary>
    public string Default { get; }

    /// <summary>Gets the contextual substitutions, in declaration order.</summary>
    public IReadOnlyList<ScaleConnectorRule> Substitutions { get; }
}
