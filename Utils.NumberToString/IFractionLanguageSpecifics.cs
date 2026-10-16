using System;
using System.Collections.Generic;
using System.Numerics;

namespace Utils.NumberToString;

/// <summary>
/// Optional language extension for <see cref="NumberToStringConverter.ConvertFraction(BigInteger, BigInteger, string[])"/>
/// and the fraction form of <c>Convert(Number)</c>, for languages whose fractions are not "numerator, connector,
/// denominator cardinal". The language supplies only what the declarative configuration cannot express: the
/// denominator word or phrase, in the form governed by the numerator (Ukrainian <c>одна третя</c> /
/// <c>дві третіх</c>, Czech <c>jedna třetina</c> / <c>dvě třetiny</c> / <c>pět třetin</c>), and the variants that
/// denominator imposes on the numerator (the feminine of <c>одна</c>, <c>dvě</c>).
/// </summary>
/// <remarks>
/// <para>
/// Implemented by the <see cref="INumberToStringLanguageSpecifics"/> instance of the language (its
/// <c>&lt;LanguageSpecifics&gt;</c>). The converter keeps every other step: the zero-denominator rejection, the move of
/// the sign to the numerator, the numerator rendered through the cardinal pipeline (scale forms, connectors,
/// shielding) with the forced variants overlaid on the caller's, the single finalization and the outer sign. Named
/// power-of-ten fractions (<c>&lt;Fractions&gt;</c>) keep precedence. Only the built-in Ukrainian, Czech and Slovak
/// readings of an oblique case spell their numerator themselves (NTS-40), the cardinal pipeline having no case forms.
/// </para>
/// <para>
/// Returning <see langword="false"/> means the language has no sourced reading for that fraction: the converter then
/// throws <see cref="NotSupportedException"/> instead of falling back to the connector reading.
/// </para>
/// </remarks>
public interface IFractionLanguageSpecifics
{
    /// <summary>Builds the denominator of a fraction in the form its numerator governs.</summary>
    /// <param name="numerator">The absolute value of the numerator (may be zero).</param>
    /// <param name="denominator">The denominator, at least one.</param>
    /// <param name="variants">The caller's effective variant query (canonical dimension names, defaults filled in).</param>
    /// <param name="reading">Receives the denominator text and the variants forced on the numerator.</param>
    /// <returns><see langword="true"/> when the language has a reading for the fraction.</returns>
    bool TryGetFractionDenominator(BigInteger numerator, BigInteger denominator, IReadOnlyDictionary<string, string> variants,
        out FractionDenominatorReading? reading);
}

/// <summary>
/// The denominator of a fraction as an <see cref="IFractionLanguageSpecifics"/> spells it, with the variants it forces
/// on the numerator. Immutable.
/// </summary>
public sealed class FractionDenominatorReading
{
    /// <summary>Initializes the reading.</summary>
    /// <param name="text">The denominator text, written after the numerator and the language separator.</param>
    /// <param name="numeratorForcedVariants">
    /// The variants forced on the numerator (overlaid on the caller's, forced values winning), or <see langword="null"/>
    /// for none. Validated against the language by the converter.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="text"/> is null, empty or white space.</exception>
    public FractionDenominatorReading(string text, ForcedVariantSet? numeratorForcedVariants = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("A fraction denominator needs a non-empty text.", nameof(text));
        Text = text;
        NumeratorForcedVariants = numeratorForcedVariants ?? ForcedVariantSet.Empty;
    }

    /// <summary>Gets the denominator text.</summary>
    public string Text { get; }

    /// <summary>Gets the variants forced on the numerator.</summary>
    public ForcedVariantSet NumeratorForcedVariants { get; }

    /// <summary>
    /// Gets the numerator text spelled by a built-in language, or <see langword="null"/> when the converter renders the
    /// numerator through its cardinal pipeline. Only the built-in Slavic readings of an oblique case set it (NTS-40): the
    /// cardinal pipeline has no case forms, and these languages decline every component of the numerator.
    /// </summary>
    internal string? NumeratorText { get; private init; }

    /// <summary>
    /// Creates a reading whose numerator is spelled by the language instead of the cardinal pipeline (NTS-40). The
    /// numerator then receives no forced variants: its text already carries the case and gender the fraction requires.
    /// </summary>
    /// <param name="numeratorText">The numerator text.</param>
    /// <param name="text">The denominator text.</param>
    /// <returns>The reading.</returns>
    /// <exception cref="ArgumentException">A text is null, empty or white space.</exception>
    internal static FractionDenominatorReading WithNumerator(string numeratorText, string text)
    {
        if (string.IsNullOrWhiteSpace(numeratorText))
            throw new ArgumentException("A fraction numerator needs a non-empty text.", nameof(numeratorText));
        return new FractionDenominatorReading(text) { NumeratorText = numeratorText };
    }
}
