using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Shared productive ordinal formation for the Scandinavian languages (Danish, Norwegian Bokmål,
/// Swedish). Below one hundred the whole number is a single ordinal word (Danish
/// <c>enogtyvende</c>, Norwegian <c>tjueførste</c>, Swedish <c>tjugoförsta</c>); above one hundred
/// only the part below one hundred becomes ordinal and follows the cardinal of the round part
/// (<c>hundrede og første</c>), while an exact round number ordinalizes its last scale word
/// (<c>tusinde</c>, <c>tusende</c>, <c>miljonte</c>).
/// </summary>
/// <remarks>
/// <see cref="IOrdinalLanguageSpecifics"/> receives no converter, so the cardinal of the round part
/// is rebuilt here from the standard orthography of each language. Only the <see cref="int"/> range
/// is implemented: larger values are declined and, with no declarative fallback configured, fail
/// closed with <see cref="System.NotSupportedException"/>.
/// </remarks>
public abstract class ScandinavianOrdinalLanguageSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
{
    /// <summary>Describes the words of one thousand-power scale.</summary>
    /// <param name="Single">The cardinal for exactly one unit of the scale (e.g. <c>tusind</c>, <c>en million</c>).</param>
    /// <param name="Plural">The scale word used after a multiplier other than one (e.g. <c>tusind</c>, <c>millioner</c>).</param>
    /// <param name="Ordinal">The ordinal of the scale word (e.g. <c>tusinde</c>, <c>millionte</c>).</param>
    /// <param name="Fused">Whether the multiplier and the scale word are written as one word.</param>
    protected sealed record ScaleWords(string Single, string Plural, string Ordinal, bool Fused);

    /// <summary>Gets the cardinals 0–19 (index 0 is unused).</summary>
    protected abstract IReadOnlyList<string> CardinalBelowTwenty { get; }

    /// <summary>Gets the cardinal tens 20–90 (indices 2–9).</summary>
    protected abstract IReadOnlyList<string> CardinalTens { get; }

    /// <summary>Gets the ordinals 0–19 (index 0 is unused).</summary>
    protected abstract IReadOnlyList<string> OrdinalBelowTwenty { get; }

    /// <summary>Gets the ordinal tens 20–90 (indices 2–9).</summary>
    protected abstract IReadOnlyList<string> OrdinalTens { get; }

    /// <summary>Gets the cardinal of exactly one hundred.</summary>
    protected abstract string Hundred { get; }

    /// <summary>Gets the word that follows a hundreds multiplier other than one.</summary>
    protected abstract string Hundreds { get; }

    /// <summary>Gets the ordinal of the hundred word.</summary>
    protected abstract string HundredOrdinal { get; }

    /// <summary>Gets whether a hundreds multiplier and the hundred word are written as one word.</summary>
    protected abstract bool HundredsFused { get; }

    /// <summary>Gets the thousand, million, and milliard scales (indices 0–2).</summary>
    protected abstract IReadOnlyList<ScaleWords> Scales { get; }

    /// <summary>Builds the cardinal of a compound 21–99 that is not a multiple of ten.</summary>
    /// <param name="tens">The tens digit (2–9).</param>
    /// <param name="unit">The unit digit (1–9).</param>
    /// <returns>The compound cardinal.</returns>
    protected abstract string ComposeCardinal(int tens, int unit);

    /// <summary>Builds the ordinal of a compound 21–99 that is not a multiple of ten.</summary>
    /// <param name="tens">The tens digit (2–9).</param>
    /// <param name="unit">The unit digit (1–9).</param>
    /// <returns>The compound ordinal.</returns>
    protected abstract string ComposeOrdinal(int tens, int unit);

    /// <summary>Joins a round cardinal head to the remainder that follows it inside the same number.</summary>
    /// <param name="head">The cardinal of the round part.</param>
    /// <param name="headEndsWithFusedScale">Whether the head ends with a hundred or thousand that fuses with what follows.</param>
    /// <param name="remainder">The cardinal or ordinal of the remainder.</param>
    /// <param name="remainderBelowHundred">Whether the remainder is below one hundred.</param>
    /// <returns>The joined text.</returns>
    protected abstract string Join(string head, bool headEndsWithFusedScale, string remainder, bool remainderBelowHundred);

    /// <inheritdoc />
    public string FinalizeWriting(string languageIdentifier, string text) => text;

    /// <inheritdoc />
    public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
    {
        if (number <= 0)
        {
            result = null;
            return false;
        }

        result = BuildOrdinal(number);
        return true;
    }

    /// <summary>Builds the ordinal of a positive number.</summary>
    /// <param name="number">A positive integer.</param>
    /// <returns>The ordinal wording.</returns>
    private string BuildOrdinal(int number)
    {
        if (number < 100)
            return OrdinalBelowHundred(number);

        int remainder = number % 100;
        int head = number - remainder;
        if (remainder > 0)
        {
            var (text, fused) = BuildCardinal(head);
            return Join(text, fused, OrdinalBelowHundred(remainder), remainderBelowHundred: true);
        }

        return BuildRoundOrdinal(head);
    }

    /// <summary>Builds the ordinal of a non-zero multiple of one hundred.</summary>
    /// <param name="number">A positive multiple of one hundred.</param>
    /// <returns>The ordinal wording, with the last scale word made ordinal.</returns>
    private string BuildRoundOrdinal(int number)
    {
        // Find the lowest non-zero group: its scale word is the one that becomes ordinal.
        int scaleIndex = -1;
        int value = number;
        while (value % 1000 == 0)
        {
            value /= 1000;
            scaleIndex++;
        }

        int lowGroup = value % 1000;
        int higher = number - lowGroup * Pow1000(scaleIndex + 1);
        string lowText;
        if (scaleIndex < 0)
        {
            // The lowest non-zero group is in the units: a multiple of one hundred below 1000.
            int multiplier = lowGroup / 100;
            lowText = multiplier == 1
                ? HundredOrdinal
                : Concat(CardinalBelowThousand(multiplier), HundredOrdinal, HundredsFused);
        }
        else
        {
            ScaleWords scale = Scales[scaleIndex];
            lowText = lowGroup == 1
                ? scale.Ordinal
                : Concat(CardinalBelowThousand(lowGroup), scale.Ordinal, scale.Fused);
        }

        if (higher == 0)
            return lowText;
        var (head, fused) = BuildCardinal(higher);
        return Join(head, fused, lowText, remainderBelowHundred: false);
    }

    /// <summary>Builds the cardinal of a positive number, reporting whether it ends with a fused scale.</summary>
    /// <param name="number">A positive integer.</param>
    /// <returns>The cardinal and whether its last word fuses with what follows.</returns>
    private (string Text, bool EndsWithFusedScale) BuildCardinal(int number)
    {
        string text = string.Empty;
        bool fused = false;
        int remaining = number;
        for (int scaleIndex = Scales.Count - 1; scaleIndex >= 0; scaleIndex--)
        {
            int unit = Pow1000(scaleIndex + 1);
            int group = remaining / unit;
            remaining %= unit;
            if (group == 0)
                continue;
            ScaleWords scale = Scales[scaleIndex];
            string part = group == 1 ? scale.Single : Concat(CardinalBelowThousand(group), scale.Plural, scale.Fused);
            text = text.Length == 0 ? part : Join(text, fused, part, remainderBelowHundred: false);
            fused = scale.Fused;
        }

        if (remaining > 0)
        {
            string part = CardinalBelowThousand(remaining);
            text = text.Length == 0 ? part : Join(text, fused, part, remaining < 100);
            fused = remaining % 100 == 0 && HundredsFused;
        }

        return (text, fused);
    }

    /// <summary>Builds the cardinal of a number between 1 and 999.</summary>
    /// <param name="number">A number between 1 and 999.</param>
    /// <returns>The cardinal wording.</returns>
    private string CardinalBelowThousand(int number)
    {
        if (number < 100)
            return CardinalBelowHundred(number);
        int multiplier = number / 100;
        int remainder = number % 100;
        string hundreds = multiplier == 1 ? Hundred : Concat(CardinalBelowHundred(multiplier), Hundreds, HundredsFused);
        return remainder == 0 ? hundreds : Join(hundreds, HundredsFused, CardinalBelowHundred(remainder), remainderBelowHundred: true);
    }

    /// <summary>Builds the cardinal of a number between 1 and 99.</summary>
    /// <param name="number">A number between 1 and 99.</param>
    /// <returns>The cardinal wording.</returns>
    private string CardinalBelowHundred(int number)
    {
        if (number < 20)
            return CardinalBelowTwenty[number];
        int unit = number % 10;
        return unit == 0 ? CardinalTens[number / 10] : ComposeCardinal(number / 10, unit);
    }

    /// <summary>Builds the ordinal of a number between 1 and 99.</summary>
    /// <param name="number">A number between 1 and 99.</param>
    /// <returns>The ordinal wording.</returns>
    private string OrdinalBelowHundred(int number)
    {
        if (number < 20)
            return OrdinalBelowTwenty[number];
        int unit = number % 10;
        return unit == 0 ? OrdinalTens[number / 10] : ComposeOrdinal(number / 10, unit);
    }

    /// <summary>Concatenates a multiplier and a scale word, fused or separated by a space.</summary>
    /// <param name="multiplier">The multiplier text.</param>
    /// <param name="word">The scale word.</param>
    /// <param name="fused">Whether the two are written as one word.</param>
    /// <returns>The combined text.</returns>
    private static string Concat(string multiplier, string word, bool fused)
        => fused ? multiplier + word : multiplier + " " + word;

    /// <summary>Computes 1000 raised to the given power.</summary>
    /// <param name="power">An exponent between 0 and 3.</param>
    /// <returns>The power of one thousand.</returns>
    private static int Pow1000(int power)
    {
        int result = 1;
        for (int i = 0; i < power; i++)
            result *= 1000;
        return result;
    }
}

/// <summary>Produces Danish ordinals (<c>første</c>, <c>enogtyvende</c>, <c>hundrede og første</c>, <c>tusinde</c>).</summary>
public sealed class DanishOrdinalLanguageSpecifics : ScandinavianOrdinalLanguageSpecifics
{
    private static readonly string[] s_cardinals =
        ["", "en", "to", "tre", "fire", "fem", "seks", "syv", "otte", "ni", "ti",
         "elleve", "tolv", "tretten", "fjorten", "femten", "seksten", "sytten", "atten", "nitten"];

    private static readonly string[] s_tens =
        ["", "", "tyve", "tredive", "fyrre", "halvtreds", "tres", "halvfjerds", "firs", "halvfems"];

    private static readonly string[] s_ordinals =
        ["", "første", "anden", "tredje", "fjerde", "femte", "sjette", "syvende", "ottende", "niende", "tiende",
         "ellevte", "tolvte", "trettende", "fjortende", "femtende", "sekstende", "syttende", "attende", "nittende"];

    private static readonly string[] s_ordinalTens =
        ["", "", "tyvende", "tredivte", "fyrretyvende", "halvtredsindstyvende", "tresindstyvende",
         "halvfjerdsindstyvende", "firsindstyvende", "halvfemsindstyvende"];

    private static readonly ScaleWords[] s_scales =
    [
        new("tusind", "tusind", "tusinde", Fused: false),
        new("en million", "millioner", "millionte", Fused: false),
        new("en milliard", "milliarder", "milliardte", Fused: false),
    ];

    /// <inheritdoc />
    protected override IReadOnlyList<string> CardinalBelowTwenty => s_cardinals;

    /// <inheritdoc />
    protected override IReadOnlyList<string> CardinalTens => s_tens;

    /// <inheritdoc />
    protected override IReadOnlyList<string> OrdinalBelowTwenty => s_ordinals;

    /// <inheritdoc />
    protected override IReadOnlyList<string> OrdinalTens => s_ordinalTens;

    /// <inheritdoc />
    protected override string Hundred => "hundrede";

    /// <inheritdoc />
    protected override string Hundreds => "hundrede";

    /// <inheritdoc />
    protected override string HundredOrdinal => "hundrede";

    /// <inheritdoc />
    protected override bool HundredsFused => false;

    /// <inheritdoc />
    protected override IReadOnlyList<ScaleWords> Scales => s_scales;

    /// <inheritdoc />
    protected override string ComposeCardinal(int tens, int unit) => s_cardinals[unit] + "og" + s_tens[tens];

    /// <inheritdoc />
    protected override string ComposeOrdinal(int tens, int unit) => s_cardinals[unit] + "og" + s_ordinalTens[tens];

    /// <inheritdoc />
    protected override string Join(string head, bool headEndsWithFusedScale, string remainder, bool remainderBelowHundred)
        => remainderBelowHundred ? head + " og " + remainder : head + " " + remainder;
}

/// <summary>Produces Norwegian Bokmål ordinals (<c>første</c>, <c>tjueførste</c>, <c>hundre og første</c>, <c>tusende</c>).</summary>
public sealed class NorwegianOrdinalLanguageSpecifics : ScandinavianOrdinalLanguageSpecifics
{
    private static readonly string[] s_cardinals =
        ["", "en", "to", "tre", "fire", "fem", "seks", "sju", "åtte", "ni", "ti",
         "elleve", "tolv", "tretten", "fjorten", "femten", "seksten", "sytten", "atten", "nitten"];

    private static readonly string[] s_tens =
        ["", "", "tjue", "tretti", "førti", "femti", "seksti", "sytti", "åtti", "nitti"];

    private static readonly string[] s_ordinals =
        ["", "første", "andre", "tredje", "fjerde", "femte", "sjette", "sjuende", "åttende", "niende", "tiende",
         "ellevte", "tolvte", "trettende", "fjortende", "femtende", "sekstende", "syttende", "attende", "nittende"];

    private static readonly string[] s_ordinalTens =
        ["", "", "tjuende", "trettiende", "førtiende", "femtiende", "sekstiende", "syttiende", "åttiende", "nittiende"];

    private static readonly ScaleWords[] s_scales =
    [
        new("tusen", "tusen", "tusende", Fused: false),
        new("en million", "millioner", "millionte", Fused: false),
        new("en milliard", "milliarder", "milliardte", Fused: false),
    ];

    /// <inheritdoc />
    protected override IReadOnlyList<string> CardinalBelowTwenty => s_cardinals;

    /// <inheritdoc />
    protected override IReadOnlyList<string> CardinalTens => s_tens;

    /// <inheritdoc />
    protected override IReadOnlyList<string> OrdinalBelowTwenty => s_ordinals;

    /// <inheritdoc />
    protected override IReadOnlyList<string> OrdinalTens => s_ordinalTens;

    /// <inheritdoc />
    protected override string Hundred => "hundre";

    /// <inheritdoc />
    protected override string Hundreds => "hundre";

    /// <inheritdoc />
    protected override string HundredOrdinal => "hundrede";

    /// <inheritdoc />
    protected override bool HundredsFused => false;

    /// <inheritdoc />
    protected override IReadOnlyList<ScaleWords> Scales => s_scales;

    /// <inheritdoc />
    protected override string ComposeCardinal(int tens, int unit) => s_tens[tens] + s_cardinals[unit];

    /// <inheritdoc />
    protected override string ComposeOrdinal(int tens, int unit) => s_tens[tens] + s_ordinals[unit];

    /// <inheritdoc />
    protected override string Join(string head, bool headEndsWithFusedScale, string remainder, bool remainderBelowHundred)
        => remainderBelowHundred ? head + " og " + remainder : head + " " + remainder;
}

/// <summary>Produces Swedish ordinals (<c>första</c>, <c>tjugoförsta</c>, <c>hundraförsta</c>, <c>tusende</c>).</summary>
public sealed class SwedishOrdinalLanguageSpecifics : ScandinavianOrdinalLanguageSpecifics
{
    private static readonly string[] s_cardinals =
        ["", "ett", "två", "tre", "fyra", "fem", "sex", "sju", "åtta", "nio", "tio",
         "elva", "tolv", "tretton", "fjorton", "femton", "sexton", "sjutton", "arton", "nitton"];

    private static readonly string[] s_tens =
        ["", "", "tjugo", "trettio", "fyrtio", "femtio", "sextio", "sjuttio", "åttio", "nittio"];

    private static readonly string[] s_ordinals =
        ["", "första", "andra", "tredje", "fjärde", "femte", "sjätte", "sjunde", "åttonde", "nionde", "tionde",
         "elfte", "tolfte", "trettonde", "fjortonde", "femtonde", "sextonde", "sjuttonde", "artonde", "nittonde"];

    private static readonly string[] s_ordinalTens =
        ["", "", "tjugonde", "trettionde", "fyrtionde", "femtionde", "sextionde", "sjuttionde", "åttionde", "nittionde"];

    private static readonly ScaleWords[] s_scales =
    [
        new("tusen", "tusen", "tusende", Fused: true),
        new("en miljon", "miljoner", "miljonte", Fused: false),
        new("en miljard", "miljarder", "miljardte", Fused: false),
    ];

    /// <inheritdoc />
    protected override IReadOnlyList<string> CardinalBelowTwenty => s_cardinals;

    /// <inheritdoc />
    protected override IReadOnlyList<string> CardinalTens => s_tens;

    /// <inheritdoc />
    protected override IReadOnlyList<string> OrdinalBelowTwenty => s_ordinals;

    /// <inheritdoc />
    protected override IReadOnlyList<string> OrdinalTens => s_ordinalTens;

    /// <inheritdoc />
    protected override string Hundred => "hundra";

    /// <inheritdoc />
    protected override string Hundreds => "hundra";

    /// <inheritdoc />
    protected override string HundredOrdinal => "hundrade";

    /// <inheritdoc />
    protected override bool HundredsFused => true;

    /// <inheritdoc />
    protected override IReadOnlyList<ScaleWords> Scales => s_scales;

    /// <inheritdoc />
    protected override string ComposeCardinal(int tens, int unit) => s_tens[tens] + s_cardinals[unit];

    /// <inheritdoc />
    protected override string ComposeOrdinal(int tens, int unit) => s_tens[tens] + s_ordinals[unit];

    /// <inheritdoc />
    protected override string Join(string head, bool headEndsWithFusedScale, string remainder, bool remainderBelowHundred)
        => headEndsWithFusedScale ? head + remainder : head + " " + remainder;
}
