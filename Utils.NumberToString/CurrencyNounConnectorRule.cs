using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Utils.Range;

namespace Utils.NumberToString;

/// <summary>
/// A contextual substitution of the currency noun connector (NTS-52): the text placed between the numeral of a currency
/// amount and the noun it governs when every constraint of the rule is satisfied, e.g. French <c>d'</c> with no
/// separator before a noun beginning with a vowel after a numeral ending on <c>million</c>: <c>un-million d'euros</c>.
/// </summary>
/// <remarks>
/// <para>
/// Three constraints are supported, each optional, at least one required: <see cref="TerminalScales"/> (the scale
/// noun the numeral ends on, decided on its group structure), <see cref="NumberRange"/> (the count of the amount,
/// optionally taken modulo <see cref="NumberModulo"/>) and <see cref="BeforeNoun"/> (the beginning of the noun).
/// </para>
/// <para>
/// Among the rules of a <see cref="CurrencyNounConnector"/> that match an amount, the one with the most constraints
/// (<see cref="Specificity"/>) wins; at equal specificity the first declared wins. Instances are immutable.
/// </para>
/// </remarks>
public sealed class CurrencyNounConnectorRule
{
    /// <summary>Initializes a new instance of the <see cref="CurrencyNounConnectorRule"/> class.</summary>
    /// <param name="value">
    /// The connector placed after the numeral and its <c>Separator</c>, e.g. <c>"de"</c> or <c>"d'"</c>; empty for no
    /// connector (the numeral, the <c>Separator</c>, then the noun).
    /// </param>
    /// <param name="nounSeparator">
    /// The text between the connector and the noun, e.g. <c>""</c> after an elided <c>d'</c>; <see langword="null"/>
    /// for the language's <c>Separator</c>. Ignored when <paramref name="value"/> is empty.
    /// </param>
    /// <param name="terminalScales">
    /// The scale indices (at least 1) the numeral must end on, or <see langword="null"/> for no condition.
    /// </param>
    /// <param name="numberRange">
    /// The counts (non-negative) of the amount the rule applies to, or <see langword="null"/> for no condition.
    /// </param>
    /// <param name="numberModulo">
    /// When set (at least 2), <paramref name="numberRange"/> is tested against the count modulo this value, e.g. 100
    /// for the last two digits; requires <paramref name="numberRange"/>.
    /// </param>
    /// <param name="beforeNoun">
    /// The beginnings of the currency noun (compared ordinally, ignoring case), one of which must begin the noun, or
    /// <see langword="null"/> for no condition. The sequence is copied in order.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is white space; a range covers no value, or a scale below 1, or a negative count;
    /// <paramref name="numberModulo"/> is below 2, set without <paramref name="numberRange"/>, or leaves no residue in
    /// the range; an entry of <paramref name="beforeNoun"/> is null, empty or white space; or no constraint is given.
    /// </exception>
    public CurrencyNounConnectorRule(
        string value,
        string? nounSeparator = null,
        IntRange<long>? terminalScales = null,
        IntRange<long>? numberRange = null,
        long? numberModulo = null,
        IEnumerable<string>? beforeNoun = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 0 && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("The currency noun connector must not be white space.", nameof(value));
        if (terminalScales is not null)
        {
            if (ConnectorRangeChecks.IsEmpty(terminalScales))
                throw new ArgumentException("The terminal scales of a currency noun connector cover no scale.", nameof(terminalScales));
            if (ConnectorRangeChecks.CoversBelowOne(terminalScales))
                throw new ArgumentException("The terminal scales of a currency noun connector must all be at least 1.", nameof(terminalScales));
        }
        if (numberRange is not null)
        {
            if (ConnectorRangeChecks.IsEmpty(numberRange))
                throw new ArgumentException("The number range of a currency noun connector covers no value.", nameof(numberRange));
            if (ConnectorRangeChecks.CoversNegative(numberRange))
                throw new ArgumentException("The number range of a currency noun connector must not cover a negative value.", nameof(numberRange));
        }
        if (numberModulo is long modulo)
        {
            if (modulo < 2)
                throw new ArgumentException("The number modulo of a currency noun connector must be at least 2.", nameof(numberModulo));
            if (numberRange is null)
                throw new ArgumentException("The number modulo of a currency noun connector requires a number range.", nameof(numberModulo));
            if (ConnectorRangeChecks.CoversNoResidue(numberRange, modulo))
                throw new ArgumentException($"The number range of a currency noun connector covers no value modulo {modulo}.", nameof(numberRange));
        }
        var prefixes = (beforeNoun ?? []).ToImmutableArray();
        if (prefixes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException($"The noun beginnings of the currency noun connector '{value}' must not be empty.", nameof(beforeNoun));
        if (terminalScales is null && numberRange is null && prefixes.IsEmpty)
            throw new ArgumentException(
                $"The currency noun connector substitution '{value}' declares no constraint; the default connector covers that case.",
                nameof(value));

        Value = value;
        NounSeparator = nounSeparator;
        TerminalScales = terminalScales;
        NumberRange = numberRange;
        NumberModulo = numberModulo;
        BeforeNoun = prefixes;
    }

    /// <summary>Gets the connector placed after the numeral (empty for none).</summary>
    public string Value { get; }

    /// <summary>
    /// Gets the text between the connector and the noun, or <see langword="null"/> for the language's <c>Separator</c>.
    /// </summary>
    public string? NounSeparator { get; }

    /// <summary>Gets the scale indices the numeral must end on, or <see langword="null"/> for no condition.</summary>
    public IntRange<long>? TerminalScales { get; }

    /// <summary>
    /// Gets the counts of the amount the rule applies to (taken modulo <see cref="NumberModulo"/> when it is set), or
    /// <see langword="null"/> for no condition.
    /// </summary>
    public IntRange<long>? NumberRange { get; }

    /// <summary>Gets the modulus applied to the count before <see cref="NumberRange"/> is tested, or <see langword="null"/>.</summary>
    public long? NumberModulo { get; }

    /// <summary>Gets the beginnings of the currency noun, one of which must begin it (empty for no condition).</summary>
    public IReadOnlyList<string> BeforeNoun { get; }

    /// <summary>
    /// Gets the number of constraints of the rule (1 to 3): <see cref="TerminalScales"/>, <see cref="NumberRange"/>
    /// (with or without <see cref="NumberModulo"/>) and a non-empty <see cref="BeforeNoun"/>. Neither the value nor the
    /// separator counts.
    /// </summary>
    public int Specificity => (TerminalScales is null ? 0 : 1) + (NumberRange is null ? 0 : 1) + (BeforeNoun.Count == 0 ? 0 : 1);
}
