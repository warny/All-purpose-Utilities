using System;
using Utils.Range;

namespace Utils.NumberToString;

/// <summary>
/// A contextual substitution of the scale connector (NTS-53): the word placed between the multiplier of a scale group
/// and its scale noun when every constraint of the rule is satisfied, e.g. Romanian <c>de</c> from twenty
/// (<c>douăzeci de mii</c>) and no connector after a multiplier whose last two digits are one to nineteen
/// (<c>o sută nouăsprezece mii</c>).
/// </summary>
/// <remarks>
/// <para>
/// Two constraints are supported, each optional, at least one required: <see cref="NumberRange"/> (the value of the
/// whole multiplier, a paired multiplier included, optionally taken modulo <see cref="NumberModulo"/>) and
/// <see cref="Scales"/> (the scale index of the group).
/// </para>
/// <para>
/// Among the rules of a <see cref="ScaleConnectorRules"/> that match a group, the one with the most constraints
/// (<see cref="Specificity"/>) wins; at equal specificity the first declared wins. Instances are immutable.
/// </para>
/// </remarks>
public sealed class ScaleConnectorRule
{
    /// <summary>Initializes a new instance of the <see cref="ScaleConnectorRule"/> class.</summary>
    /// <param name="value">
    /// The connector placed between the multiplier and the scale noun, each followed by the language's <c>Separator</c>,
    /// e.g. <c>"de"</c>; empty for no connector (the historical multiplier/scale junction).
    /// </param>
    /// <param name="numberRange">
    /// The multiplier values (non-negative) the rule applies to, or <see langword="null"/> for no condition.
    /// </param>
    /// <param name="numberModulo">
    /// When set (at least 2), <paramref name="numberRange"/> is tested against the multiplier modulo this value, e.g. 100
    /// for its last two digits; requires <paramref name="numberRange"/>. A modifier of the range, not a constraint of
    /// its own.
    /// </param>
    /// <param name="scales">The scale indices (at least 1) the rule applies to, or <see langword="null"/> for no condition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is white space; a range covers no value, or a negative multiplier, or a scale below 1;
    /// <paramref name="numberModulo"/> is below 2, set without <paramref name="numberRange"/>, or leaves no residue in the
    /// range; or no constraint is given.
    /// </exception>
    public ScaleConnectorRule(
        string value,
        IntRange<long>? numberRange = null,
        long? numberModulo = null,
        IntRange<long>? scales = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 0 && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("The scale connector must not be white space.", nameof(value));
        if (numberRange is not null)
        {
            if (ConnectorRangeChecks.IsEmpty(numberRange))
                throw new ArgumentException("The number range of a scale connector covers no value.", nameof(numberRange));
            if (ConnectorRangeChecks.CoversNegative(numberRange))
                throw new ArgumentException("The number range of a scale connector must not cover a negative value.", nameof(numberRange));
        }
        if (numberModulo is long modulo)
        {
            if (modulo < 2)
                throw new ArgumentException("The number modulo of a scale connector must be at least 2.", nameof(numberModulo));
            if (numberRange is null)
                throw new ArgumentException("The number modulo of a scale connector requires a number range.", nameof(numberModulo));
            if (ConnectorRangeChecks.CoversNoResidue(numberRange, modulo))
                throw new ArgumentException($"The number range of a scale connector covers no value modulo {modulo}.", nameof(numberRange));
        }
        if (scales is not null)
        {
            if (ConnectorRangeChecks.IsEmpty(scales))
                throw new ArgumentException("The scales of a scale connector cover no scale.", nameof(scales));
            if (ConnectorRangeChecks.CoversBelowOne(scales))
                throw new ArgumentException("The scales of a scale connector must all be at least 1.", nameof(scales));
        }
        if (numberRange is null && scales is null)
            throw new ArgumentException(
                $"The scale connector substitution '{value}' declares no constraint; the default connector covers that case.",
                nameof(value));

        Value = value;
        NumberRange = numberRange;
        NumberModulo = numberModulo;
        Scales = scales;
    }

    /// <summary>Gets the connector placed between the multiplier and the scale noun (empty for none).</summary>
    public string Value { get; }

    /// <summary>
    /// Gets the multiplier values the rule applies to (taken modulo <see cref="NumberModulo"/> when it is set), or
    /// <see langword="null"/> for no condition.
    /// </summary>
    public IntRange<long>? NumberRange { get; }

    /// <summary>Gets the modulus applied to the multiplier before <see cref="NumberRange"/> is tested, or <see langword="null"/>.</summary>
    public long? NumberModulo { get; }

    /// <summary>Gets the scale indices the rule applies to, or <see langword="null"/> for no condition.</summary>
    public IntRange<long>? Scales { get; }

    /// <summary>
    /// Gets the number of constraints of the rule (1 or 2): <see cref="NumberRange"/> (with or without
    /// <see cref="NumberModulo"/>) and <see cref="Scales"/>. The value does not count.
    /// </summary>
    public int Specificity => (NumberRange is null ? 0 : 1) + (Scales is null ? 0 : 1);

    /// <summary>Determines whether every constraint of the rule holds for a group.</summary>
    /// <param name="scale">The scale index of the group (at least 1).</param>
    /// <param name="multiplier">The value of the whole multiplier of the group.</param>
    /// <returns><see langword="true"/> when the rule applies.</returns>
    internal bool Matches(int scale, long multiplier)
    {
        if (Scales is not null && !Scales.Contains((long)scale)) return false;
        if (NumberRange is not null
            && !NumberRange.Contains(NumberModulo is long modulo ? multiplier % modulo : multiplier))
            return false;
        return true;
    }
}
