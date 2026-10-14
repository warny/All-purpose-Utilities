using System;
using System.Collections.Generic;

namespace Utils.NumberToString;

/// <summary>
/// Shared domain guard for languages whose ordinals are formed by the declarative XML pipeline but
/// whose wording was only validated on part of the numeric range (NTS-08). Values inside the validated
/// domain fall through to the declarative pipeline unchanged; every other value is rejected with
/// <see cref="NotSupportedException"/> instead of returning an unverified or known-wrong form.
/// </summary>
/// <remarks>
/// The guard finalizes nothing. It receives the absolute value of the number, so negative values are
/// bounded the same way. Inside the domain it builds no text unless a derived class overrides
/// <see cref="TryBuildOrdinal"/> for the families the declarative pipeline cannot form (NTS-28). Each derived
/// class documents the sources that validate its domain and the reason why the rest is closed.
/// </remarks>
public abstract class OrdinalDomainGuardLanguageSpecifics : INumberToStringLanguageSpecifics, IOrdinalLanguageSpecifics
{
    /// <summary>Gets the English name of the language, used in the rejection message.</summary>
    protected abstract string LanguageName { get; }

    /// <summary>Gets a short description of the validated domain, used in the rejection message.</summary>
    protected abstract string SupportedDomain { get; }

    /// <summary>Tells whether the ordinal of a value belongs to the validated domain.</summary>
    /// <param name="number">The absolute value of the number (always ≥ 0).</param>
    /// <param name="activeVariants">The active dimension constraints.</param>
    /// <returns><see langword="true"/> when the declarative pipeline may form the ordinal.</returns>
    protected abstract bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants);

    /// <inheritdoc />
    public string FinalizeWriting(string languageIdentifier, string text) => text;

    /// <inheritdoc />
    public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
        => TryConvertOrdinal((long)number, activeVariants, out result);

    /// <inheritdoc />
    public bool TryConvertOrdinal(long number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
    {
        if (!IsSupported(number, activeVariants))
            throw new NotSupportedException(
                $"{LanguageName} ordinal {number} is not supported: the validated ordinal domain is {SupportedDomain}.");
        // Inside the validated domain the declarative XML pipeline forms the ordinal, except for the values a
        // derived class builds itself.
        return TryBuildOrdinal(number, activeVariants, out result);
    }

    /// <summary>
    /// Builds the ordinal of a supported value that the declarative pipeline cannot form. The default builds
    /// nothing, so every supported value goes to the declarative pipeline.
    /// </summary>
    /// <param name="number">The absolute value of the number, inside the supported domain.</param>
    /// <param name="activeVariants">The active dimension constraints.</param>
    /// <param name="result">Receives the ordinal when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the ordinal was built here.</returns>
    protected virtual bool TryBuildOrdinal(long number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
    {
        result = null;
        return false;
    }

    /// <summary>Tells whether a value is a round ten between 20 and 90.</summary>
    /// <param name="number">The value to test.</param>
    /// <returns><see langword="true"/> for 20, 30, … 90.</returns>
    protected static bool IsRoundTen(long number) => number is >= 20 and <= 90 && number % 10 == 0;

    /// <summary>Tells whether a value is a round hundred between 100 and 900.</summary>
    /// <param name="number">The value to test.</param>
    /// <returns><see langword="true"/> for 100, 200, … 900.</returns>
    protected static bool IsRoundHundred(long number) => number is >= 100 and <= 900 && number % 100 == 0;
}

/// <summary>
/// Spanish ordinal domain. Validated by the RAE/ASALE <i>Diccionario panhispánico de dudas</i>, "ordinales"
/// (NTS-08 for 1–29, the round tens, 100 and 1000; NTS-28 for the rest): §1 table and §2–§3, the complex
/// ordinals juxtapose the simple ones (<c>trigésimo primero</c>, <c>centésimo trigésimo cuarto</c>), the
/// round hundreds are simple (<c>ducentésimo</c>), the thousands and millions prefix the multiplying
/// cardinal (<c>dosmilésimo</c>, <c>quinientosmilésimo</c>, <c>dosmillonésimo</c>). The configuration composes
/// them; this guard admits 1–999 999 999 except the round thousands and millions whose multiplier the DPD does
/// not show soldered: a multiplier ending in <c>uno</c> (21, 31, 101 …, apocope undetermined), a multiplier
/// written in several words (150 <c>ciento cincuenta</c>) and, in the feminine, a hundreds multiplier
/// (<c>doscientas</c>). Zero and the milliard and above (whose cardinal is not composed, NTS-30) fail closed.
/// A composed value is checked part by part, so a rejected round part rejects the whole ordinal.
/// </summary>
public sealed class SpanishOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Spanish";

    /// <inheritdoc />
    protected override string SupportedDomain =>
        "1–999999999, except round thousands and millions whose multiplier is not a one-word cardinal without " +
        "apocope (and, in the feminine, not a hundreds multiplier)";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants)
    {
        if (number is < 1 or > 999_999_999) return false;
        bool feminine = activeVariants.TryGetValue("gender", out string? gender) && gender == "femenino";
        if (number % 1_000_000 == 0) return IsSolderableMultiplier(number / 1_000_000, feminine);
        if (number >= 1_000_000) return true;
        if (number % 1000 == 0) return IsSolderableMultiplier(number / 1000, feminine);
        return true;
    }

    /// <summary>
    /// Tells whether a multiplier of mil or millón is one of those the DPD shows soldered to the simple ordinal:
    /// one (the noun alone), a one-word cardinal without a final <c>uno</c> (2–29 except 21, the round tens),
    /// <c>cien</c>, and the round hundreds in the masculine.
    /// </summary>
    /// <param name="multiplier">The multiplier, between 1 and 999.</param>
    /// <param name="feminine">Whether the feminine is requested.</param>
    /// <returns><see langword="true"/> when the round ordinal is supported.</returns>
    private static bool IsSolderableMultiplier(long multiplier, bool feminine)
        => multiplier is >= 1 and <= 29 && multiplier != 21
           || IsRoundTen(multiplier)
           || multiplier == 100
           || IsRoundHundred(multiplier) && !feminine;
}

/// <summary>
/// Portuguese ordinal domain: 1–1999. NTS-08 validated 1–20, the round tens, 100 and 1000 (Priberam). NTS-28
/// added the compounds, composed from agreeing ordinals (<c>vigésimo primeiro</c>, <c>centésima quadragésima
/// quinta</c>), and the round hundreds (<c>ducentésimo</c> … <c>nongentésimo</c>), after Ciberdúvidas, "A forma
/// por extenso de 2000.º (e de outros ordinais)", citing E. Bechara. From 2000 the sources disagree (<c>dois
/// milésimo</c>, <c>dois milésimos</c>, <c>segundo milésimo</c>; <c>décimo milésimo</c> for 10 000), so those
/// values fail closed, as does zero.
/// </summary>
public sealed class PortugueseOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Portuguese";

    /// <inheritdoc />
    protected override string SupportedDomain => "1–1999";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants)
        => number is >= 1 and <= 1999;
}

/// <summary>
/// Galician ordinal domain: 1–1999. Validated by the RAG/ILG <i>Normas ortográficas e morfolóxicas do idioma
/// galego</i> (2003), §16.2 (NTS-08 for 1–20, the round tens, <c>centésimo</c>, <c>milésimo</c>; NTS-28 for the
/// compounds <c>vixésimo primeiro</c> …, juxtaposed with gender on the last element only, <c>centésimo
/// primeira</c>) and the Dicionario da Real Academia Galega for the round hundreds (<c>ducentésimo</c> …
/// <c>noninxentésimo</c>). No consulted source gives the multiples of a thousand nor the millions compounds, so
/// 2000 and above fail closed, as does zero.
/// </summary>
public sealed class GalicianOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Galician";

    /// <inheritdoc />
    protected override string SupportedDomain => "1–1999";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants)
        => number is >= 1 and <= 1999;
}

/// <summary>
/// Greek ordinal domain: 1–1999. Validated by the <i>Λεξικό της Κοινής Νεοελληνικής</i> (Triantafyllides): NTS-08
/// for 1–20, the round tens, the round hundreds and <c>χιλιοστός</c>; NTS-28 for the compounds, juxtaposed
/// ordinals agreeing in gender (<c>εικοστή τρίτη</c>, <c>εκατοστή πρώτη</c>), composed by the configuration.
/// The round thousands above one (<c>δισχιλιοστός</c> …) are not established by the dictionary, so 2000 and
/// above fail closed, as does zero.
/// </summary>
public sealed class GreekOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Greek";

    /// <inheritdoc />
    protected override string SupportedDomain => "1–1999";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants)
        => number is >= 1 and <= 1999;
}

/// <summary>
/// Finnish ordinal domain: the nominative of 0–1999. NTS-08 validated 0–20, the round tens and hundreds and 1000
/// (Aalto University "Numeroiden taivutus", Kielitoimisto; <c>nollas</c>, NTS-12). NTS-28 added the compounds,
/// one word with the ordinal marker in every part (Kielitoimiston ohjepankki: <c>viideskymmenesviides</c>,
/// <c>kahdeskymmenesensimmäinen</c>), composed by the configuration. The inflected ordinals change every
/// component (genitive <c>kolmannenkymmenennenkuudennen</c>), but the consulted sources give the paradigms of
/// 1, 3, 5, 17, 20, 100 and 1000 only, so every non-nominative case fails closed. From 2000 the cardinal is
/// correct since NTS-30E (<c>kaksituhatta</c>), but no consulted source gives the round-thousand ordinals
/// (2000th, 3000th, 21 000th): those values stay deliberately unsupported until they are sourced.
/// </summary>
public sealed class FinnishOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <summary>The value of the case dimension that selects the nominative.</summary>
    private const string Nominative = "nominatiivi";

    /// <inheritdoc />
    protected override string LanguageName => "Finnish";

    /// <inheritdoc />
    protected override string SupportedDomain => "the nominative of 0–1999";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants)
    {
        if ((activeVariants.TryGetValue("case", out string? caseValue) || activeVariants.TryGetValue("sijamuoto", out caseValue))
            && caseValue != Nominative)
            return false;
        return number is >= 0 and <= 1999;
    }
}

/// <summary>
/// Russian ordinal domain, after the <i>Правила русской орфографии и пунктуации</i> (2006). §133б: a composite
/// ordinal is written separately and only its last part is ordinal (<c>тысяча девятьсот девяносто
/// четвёртый</c>, <c>две тысячи первый</c>, <c>миллион двести тысяч сто пятый</c>); the declarative pipeline
/// forms them on the NTS-27 cardinal (the configuration drops the <c>один</c> of a single million). §132б: an
/// ordinal whose last part is <c>-тысячный</c> or <c>-миллионный</c> is one word with its first part in the
/// genitive (<c>пятнадцатитысячный</c>, <c>шестисотмиллионный</c>); this class builds them, declined for
/// gender and case, also after the millions (<c>два миллиона трёхтысячный</c>).
/// </summary>
/// <remarks>
/// Domain: 0 (<c>нулевой</c>, NTS-14) to 999 999 999. A round thousand or million is supported when its
/// multiplier is one or has a genitive combining form established by the examples (2–99 except a final
/// <c>один</c> after the tens, and 200–999 whose hundreds take the genitive, <c>шестисот</c>); a final
/// <c>один</c> (21 000) and the hundreds of <c>сто</c> (100 000 – 199 000) fail closed, as does the milliard and
/// above, whose generated scale names do not agree yet (NTS-30).
/// </remarks>
public sealed class RussianOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <summary>The genitive combining forms of 0–9 (index = value; 0 and 1 have none).</summary>
    private static readonly string?[] s_unitsGenitive = [null, null, "двух", "трёх", "четырёх", "пяти", "шести", "семи", "восьми", "девяти"];

    /// <summary>The genitive combining forms of 10–19.</summary>
    private static readonly string[] s_teensGenitive =
        ["десяти", "одиннадцати", "двенадцати", "тринадцати", "четырнадцати", "пятнадцати", "шестнадцати", "семнадцати", "восемнадцати", "девятнадцати"];

    /// <summary>The genitive combining forms of the round tens (index = tens digit).</summary>
    private static readonly string?[] s_tensGenitive = [null, null, "двадцати", "тридцати", "сорока", "пятидесяти", "шестидесяти", "семидесяти", "восьмидесяти", "девяноста"];

    /// <summary>The genitive combining forms of the round hundreds from 200 (index = hundreds digit).</summary>
    private static readonly string?[] s_hundredsGenitive = [null, null, "двухсот", "трёхсот", "четырёхсот", "пятисот", "шестисот", "семисот", "восьмисот", "девятисот"];

    /// <summary>Masculine cardinal units (the gender of миллион).</summary>
    private static readonly string[] s_units = ["", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять"];

    /// <summary>Cardinal 10–19.</summary>
    private static readonly string[] s_teens =
        ["десять", "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать"];

    /// <summary>Cardinal round tens (index = tens digit).</summary>
    private static readonly string[] s_tens = ["", "", "двадцать", "тридцать", "сорок", "пятьдесят", "шестьдесят", "семьдесят", "восемьдесят", "девяносто"];

    /// <summary>Cardinal round hundreds (index = hundreds digit).</summary>
    private static readonly string[] s_hundreds = ["", "сто", "двести", "триста", "четыреста", "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот"];

    /// <summary>The case values of the configuration, in the column order of <see cref="s_endings"/>.</summary>
    private static readonly string[] s_cases = ["именительный", "родительный", "дательный", "винительный", "творительный", "предложный"];

    /// <summary>The gender values of the configuration, in the row order of <see cref="s_endings"/>.</summary>
    private static readonly string[] s_genders = ["maskulin", "feminin", "neutrum", "plural"];

    /// <summary>
    /// Hard adjective endings [gender][case], as configured for <c>тысячный</c> (inanimate accusative).
    /// </summary>
    private static readonly string[][] s_endings =
    [
        ["ый", "ого", "ому", "ый", "ым", "ом"],
        ["ая", "ой", "ой", "ую", "ой", "ой"],
        ["ое", "ого", "ому", "ое", "ым", "ом"],
        ["ые", "ых", "ым", "ые", "ыми", "ых"],
    ];

    /// <inheritdoc />
    protected override string LanguageName => "Russian";

    /// <inheritdoc />
    protected override string SupportedDomain =>
        "0–999999999, except round thousands and millions whose multiplier ends in один after the tens or lies " +
        "between 100 and 199";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants)
    {
        if (number < 2000) return true;
        if (number > 999_999_999) return false;
        if (number % 1000 != 0) return true;
        long thousands = number / 1000 % 1000;
        return thousands != 0 ? IsCombinable(thousands) : IsCombinable(number / 1_000_000);
    }

    /// <inheritdoc />
    protected override bool TryBuildOrdinal(long number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
    {
        result = null;
        if (number < 2000 || number % 1000 != 0) return false;

        int genderIndex = Math.Max(0, Array.IndexOf(s_genders, activeVariants.TryGetValue("gender", out string? gender) ? gender : null));
        int caseIndex = Math.Max(0, Array.IndexOf(s_cases, activeVariants.TryGetValue("case", out string? caseValue) ? caseValue : null));
        string ending = s_endings[genderIndex][caseIndex];

        long millions = number / 1_000_000;
        long thousands = number / 1000 % 1000;
        if (thousands == 0)
        {
            result = Combining(millions) + "миллионн" + ending;
            return true;
        }
        string word = Combining(thousands) + "тысячн" + ending;
        result = millions == 0 ? word : MillionsCardinal(millions) + " " + word;
        return true;
    }

    /// <summary>Tells whether a multiplier of тысяча or миллион has an established combining form.</summary>
    /// <param name="multiplier">The multiplier, 1–999.</param>
    /// <returns><see langword="true"/> for one and the multipliers <see cref="Combining"/> can write.</returns>
    private static bool IsCombinable(long multiplier)
    {
        if (multiplier == 1) return true;
        long hundreds = multiplier / 100;
        long rest = multiplier % 100;
        if (hundreds == 1) return false;
        return rest is 0 or >= 2 and <= 19 || rest >= 20 && rest % 10 != 1;
    }

    /// <summary>Writes the genitive combining form of a multiplier accepted by <see cref="IsCombinable"/>.</summary>
    /// <param name="multiplier">The multiplier, 1–999 (one gives the empty string: тысячный, миллионный).</param>
    /// <returns>The combining form (e.g. <c>двадцатипяти</c>, <c>шестисот</c>).</returns>
    private static string Combining(long multiplier)
    {
        if (multiplier == 1) return string.Empty;
        string hundreds = s_hundredsGenitive[multiplier / 100] ?? string.Empty;
        long rest = multiplier % 100;
        string tail = rest switch
        {
            0 => string.Empty,
            < 10 => s_unitsGenitive[rest]!,
            < 20 => s_teensGenitive[rest - 10],
            _ => s_tensGenitive[rest / 10] + (rest % 10 == 0 ? string.Empty : s_unitsGenitive[rest % 10]),
        };
        return hundreds + tail;
    }

    /// <summary>Writes the cardinal of a millions group with the agreeing noun (миллион, два миллиона, пять миллионов).</summary>
    /// <param name="millions">The millions multiplier, 1–999.</param>
    /// <returns>The cardinal words.</returns>
    private static string MillionsCardinal(long millions)
    {
        if (millions == 1) return "миллион";
        var words = new List<string>();
        if (millions / 100 > 0) words.Add(s_hundreds[millions / 100]);
        long rest = millions % 100;
        if (rest is >= 10 and < 20) words.Add(s_teens[rest - 10]);
        else
        {
            if (rest >= 20) words.Add(s_tens[rest / 10]);
            if (rest % 10 > 0) words.Add(s_units[rest % 10]);
        }
        long last = rest % 10;
        bool teen = rest is >= 10 and < 20;
        words.Add(!teen && last == 1 ? "миллион" : !teen && last is >= 2 and <= 4 ? "миллиона" : "миллионов");
        return string.Join(" ", words);
    }
}

/// <summary>
/// Vietnamese ordinal domain: <c>thứ</c> + cardinal, with the forms <c>thứ nhất</c> and <c>thứ tư</c> (vtudien,
/// Việt-Việt: thứ "Đặt trước số từ để chỉ thứ tự", before any numeral). NTS-08 validated 0–999; NTS-28 extends the
/// rule to the NTS-27 cardinal domain (to 999 999 999 999). A number with a whole zero class after a higher class
/// (1 000 005) still has a cardinal defect (the class is skipped instead of read <c>không nghìn</c>, NTS-30), so
/// its ordinal fails closed.
/// </summary>
public sealed class VietnameseOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Vietnamese";

    /// <inheritdoc />
    protected override string SupportedDomain => "0–999999999999 without a whole zero class after a higher class";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants)
    {
        if (number > 999_999_999_999) return false;
        // Scan the classes from the units upward: a zero class is a defect when a lower class is non-zero and a
        // higher class too (it is then an inner class the cardinal skips).
        bool lowerNonZero = false;
        for (long rest = number; rest >= 1000; rest /= 1000)
        {
            long group = rest % 1000;
            if (group == 0 && lowerNonZero) return false;
            lowerNonZero |= group != 0;
        }
        return true;
    }
}

/// <summary>
/// Dutch ordinal domain (NTS-08). Taaladvies "Aaneenschrijven van telwoorden" and the Woordenlijst
/// Nederlandse Taal (<c>nulde</c>, <c>tweeduizendste</c>) validate the ordinals from 0 to 999 999. NTS-31 extends
/// them to every magnitude up to <see cref="long.MaxValue"/> (<see cref="long.MinValue"/> is rejected by the
/// engine, its absolute value not being representable): the e-ANS (rangtelwoorden) gives the scale ordinals
/// (<c>miljoenste</c>, <c>miljardste</c>) and the Besluit bekendmaking spellingvoorschriften 2005, rule 6.N, the
/// compounds (<c>twee miljoenste</c>, <c>twee miljoen tweehonderdduizendste</c>): the cardinal keeps its spelling and
/// only its final element becomes ordinal, which the declarative pipeline forms on every value. The guard therefore
/// rejects nothing; it keeps the validated domain explicit.
/// </summary>
public sealed class DutchOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Dutch";

    /// <inheritdoc />
    protected override string SupportedDomain => "0–9223372036854775807";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants) => true;
}

/// <summary>
/// Hindi ordinal domain. Kamta Prasad Guru, <i>हिंदी व्याकरण</i> §180: पहला, दूसरा, तीसरा, चौथा, छठा, otherwise
/// <c>-वाँ</c>, and (इ) above a hundred <c>-वाँ</c> on the end of the last word, whatever the number (<c>एक सौ
/// तीनवाँ</c>). NTS-08 validated 0–99 999; NTS-28 extends the rule to the NTS-27 cardinal domain with लाख and करोड़
/// (<c>एक लाखवाँ</c>, <c>एक करोड़वाँ</c>), to 9 999 999 999 999.
/// </summary>
public sealed class HindiOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Hindi";

    /// <inheritdoc />
    protected override string SupportedDomain => "0–9999999999999";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants) => number <= 9_999_999_999_999;
}

/// <summary>
/// Japanese ordinal domain: the prefix <c>第</c> before the cardinal (デジタル大辞泉, 第: "数を表す語に付いて、ものの順序を
/// 表す", before any numeral). NTS-08 validated 0–1000; NTS-28 extends it to the NTS-27 cardinal domain, counted by
/// 万 (to 9999兆, 10^16 − 1).
/// </summary>
public sealed class JapaneseOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Japanese";

    /// <inheritdoc />
    protected override string SupportedDomain => "0–9999999999999999";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants) => number <= 9_999_999_999_999_999;
}

/// <summary>
/// Korean ordinal domain: the prefix <c>제</c> before the Sino-Korean numeral (한글 맞춤법 제43항: <c>제삼 항</c>), with
/// any numeral. NTS-08 validated 0–1000; NTS-28 extends it to the NTS-27 cardinal domain, spaced by units of 만 (to
/// 9999조, 10^16 − 1).
/// </summary>
public sealed class KoreanOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Korean";

    /// <inheritdoc />
    protected override string SupportedDomain => "0–9999999999999999";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants) => number <= 9_999_999_999_999_999;
}

/// <summary>
/// Chinese ordinal domain: the prefix <c>第</c> before the cardinal (國語辭典: "用於整數數詞之前。表事物的順序或等級", before
/// any integer numeral). NTS-08 validated 0–100 and the round hundreds; NTS-28 extends it to the NTS-27 cardinal
/// domain with <c>零</c> and <c>一十</c>, counted by 万 (below 万亿, 10^12).
/// </summary>
public sealed class ChineseOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Chinese";

    /// <inheritdoc />
    protected override string SupportedDomain => "0–999999999999";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants)
        => number <= 999_999_999_999;
}

/// <summary>
/// Basque ordinal domain. Euskaltzaindia, rule 18: the ordinal is the cardinal written in letters followed by
/// <c>-garren</c> (<c>bigarren</c>, <c>bosgarren</c>, <c>hogeita batgarren</c>, <c>ehungarren</c>, <c>milagarren</c>
/// …). NTS-08 validated 1–1000; NTS-28 extends the rule to the NTS-27 cardinal (rule 7: <c>mila eta batgarren</c>,
/// <c>milioi batgarren</c>), to 999 999 999. Zero has no attested ordinal (rule 7 gives only
/// <c>zero</c>/<c>huts</c>), and from the milliard the cardinal does not compose <c>mila milioi</c> with the
/// millions (NTS-30); those values fail closed.
/// </summary>
public sealed class BasqueOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Basque";

    /// <inheritdoc />
    protected override string SupportedDomain => "1–999999999";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants)
        => number is >= 1 and <= 999_999_999;
}
