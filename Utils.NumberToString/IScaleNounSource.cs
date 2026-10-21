using System.Collections.Generic;
using System.Numerics;

namespace Utils.NumberToString;

/// <summary>
/// Supplies the configured forms of a scale noun to a built-in language plugin (NTS-42 F), so that the plugin declines the
/// very noun the converter writes instead of keeping a second spelling of it. The converter resolves the noun exactly as
/// its cardinal pipeline does: the exact <c>&lt;ScaleForm&gt;</c> of the scale, else the <c>&lt;GeneratedScaleForm&gt;</c>
/// of its suffix, whose fragment is joined to the Conway prefix of the scale (<c>kvadri</c> + <c>li</c> + <c>onu</c>).
/// </summary>
/// <remarks>
/// A form key is any key of the configured <c>&lt;Forms&gt;</c>: the count keys the form selector returns (<c>one</c>,
/// <c>few</c>, <c>other</c>) and, for the languages whose plugins decline scale nouns, the case keys of
/// <see cref="ScaleNounFormKeys"/>. A key the configuration does not declare has no form: the plugin then fails closed.
/// </remarks>
internal interface IScaleNounSource
{
    /// <summary>Gets one configured form of the noun of a scale.</summary>
    /// <param name="scale">The scale index (1 = thousands, 2 = 10^6 with groups of three digits …).</param>
    /// <param name="formKey">The form key.</param>
    /// <param name="noun">Receives the noun.</param>
    /// <param name="multiplierVariants">Receives the variants that form forces on its multiplier (empty when none).</param>
    /// <returns><see langword="false"/> when the scale cannot be named or has no form with that key.</returns>
    bool TryGetScaleNoun(int scale, string formKey, out string noun, out ForcedVariantSet multiplierVariants);
}

/// <summary>
/// A built-in fraction plugin that declines the scale nouns of the converter (NTS-42 F). The converter calls this overload
/// instead of <see cref="IFractionLanguageSpecifics.TryGetFractionDenominator"/>, passing its own
/// <see cref="IScaleNounSource"/>; the public overload, called without the converter, keeps the readings that need no
/// configured scale noun.
/// </summary>
internal interface IScaleNounFractionLanguageSpecifics : IFractionLanguageSpecifics
{
    /// <summary>Builds the denominator of a fraction in the form its numerator governs.</summary>
    /// <param name="numerator">The absolute value of the numerator (may be zero).</param>
    /// <param name="denominator">The denominator, at least one.</param>
    /// <param name="variants">The caller's effective variant query (canonical dimension names, defaults filled in).</param>
    /// <param name="scaleNouns">The scale nouns of the converter.</param>
    /// <param name="reading">Receives the denominator text and the variants forced on the numerator.</param>
    /// <returns><see langword="true"/> when the language has a reading for the fraction.</returns>
    bool TryGetFractionDenominator(BigInteger numerator, BigInteger denominator, IReadOnlyDictionary<string, string> variants,
        IScaleNounSource scaleNouns, out FractionDenominatorReading? reading);
}

/// <summary>
/// The form keys of a declined scale noun: <c>{number}.{case}</c>, the number <c>singular</c> or <c>plural</c>, the case
/// one of the six case names below (<c>plural.dative</c>). They are configuration keys, independent of the language's own
/// case values.
/// </summary>
internal static class ScaleNounFormKeys
{
    /// <summary>The case names, in the order nominative, genitive, dative, accusative, locative, instrumental.</summary>
    private static readonly string[] s_cases = ["nominative", "genitive", "dative", "accusative", "locative", "instrumental"];

    /// <summary>The singular keys, by case index.</summary>
    private static readonly string[] s_singular = Keys("singular");

    /// <summary>The plural keys, by case index.</summary>
    private static readonly string[] s_plural = Keys("plural");

    /// <summary>The key of the form after a multiplier of one, whose forced variants give the gender of the noun.</summary>
    internal const string One = "one";

    /// <summary>Gets the number of cases.</summary>
    internal static int CaseCount => s_cases.Length;

    /// <summary>Returns the key of a declined form.</summary>
    /// <param name="plural">Whether the plural is required.</param>
    /// <param name="caseIndex">The case index (0 = nominative … 5 = instrumental).</param>
    /// <returns>The form key.</returns>
    internal static string Get(bool plural, int caseIndex) => (plural ? s_plural : s_singular)[caseIndex];

    /// <summary>Builds the keys of one number.</summary>
    /// <param name="number">The number name.</param>
    /// <returns>The keys by case index.</returns>
    private static string[] Keys(string number)
    {
        var keys = new string[s_cases.Length];
        for (int i = 0; i < keys.Length; i++)
            keys[i] = number + "." + s_cases[i];
        return keys;
    }
}
