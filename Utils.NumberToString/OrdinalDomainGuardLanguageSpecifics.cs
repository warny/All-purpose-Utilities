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
/// The guard builds no text and finalizes nothing. It receives the absolute value of the number, so
/// negative values are bounded the same way. Each derived class documents the sources that validate
/// its domain and the reason why the rest is closed.
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
        result = null;
        if (!IsSupported(number, activeVariants))
            throw new NotSupportedException(
                $"{LanguageName} ordinal {number} is not supported: the validated ordinal domain is {SupportedDomain}.");
        // Inside the validated domain the declarative XML pipeline forms the ordinal.
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
/// Spanish ordinal domain (NTS-08). Validated by the RAE/ASALE <i>Diccionario panhispánico de dudas</i>,
/// "ordinales": 1–20, the univerbal 21–29 (<c>vigesimoprimero</c> … <c>vigesimonoveno</c>), the round tens
/// (<c>trigésimo</c> … <c>nonagésimo</c>), <c>centésimo</c> and <c>milésimo</c>. From 31 the DPD ordinal
/// juxtaposes ordinals (<c>trigésimo primero</c>, <c>ducentésimo</c>, <c>dosmilésimo</c>), which the
/// declarative last-word pipeline cannot build from the cardinal; those values fail closed.
/// </summary>
public sealed class SpanishOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Spanish";

    /// <inheritdoc />
    protected override string SupportedDomain => "1–29, the round tens to 90, 100 and 1000";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants)
        => number is >= 1 and <= 29 || IsRoundTen(number) || number is 100 or 1000;
}

/// <summary>
/// Portuguese ordinal domain (NTS-08). Validated by the Priberam dictionary entries (<c>vigésimo</c> "depois
/// do décimo nono", <c>trigésimo</c> … <c>nonagésimo</c> "após o octogésimo nono", <c>centésimo</c>,
/// <c>milésimo</c>): 1–20, the round tens, 100 and 1000. Other compounds juxtapose ordinals
/// (<c>vigésimo primeiro</c>), which the last-word pipeline cannot build; they fail closed.
/// </summary>
public sealed class PortugueseOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Portuguese";

    /// <inheritdoc />
    protected override string SupportedDomain => "1–20, the round tens to 90, 100 and 1000";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants)
        => number is >= 1 and <= 20 || IsRoundTen(number) || number is 100 or 1000;
}

/// <summary>
/// Galician ordinal domain (NTS-08). Validated by the RAG/ILG <i>Normas ortográficas e morfolóxicas do idioma
/// galego</i> (2003), §16.2: 1–20 (<c>décimo terceiro</c> … <c>décimo noveno</c>), the round tens
/// (<c>vixésimo</c> … <c>nonaxésimo</c>), <c>centésimo</c> and <c>milésimo</c>. Other compounds juxtapose
/// ordinals (<c>vixésimo primeiro</c>) and fail closed.
/// </summary>
public sealed class GalicianOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Galician";

    /// <inheritdoc />
    protected override string SupportedDomain => "1–20, the round tens to 90, 100 and 1000";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants)
        => number is >= 1 and <= 20 || IsRoundTen(number) || number is 100 or 1000;
}

/// <summary>
/// Greek ordinal domain (NTS-08). Validated by the <i>Λεξικό της Κοινής Νεοελληνικής</i> (Triantafyllides):
/// 1–20, the round tens (<c>εικοστός</c> …), the round hundreds (<c>εκατοστός</c>, <c>διακοσιοστός</c> …)
/// and <c>χιλιοστός</c>. Compounds are made of agreeing ordinals (<c>εικοστός πρώτος</c>,
/// <c>εκατοστή πρώτη</c>), which the last-word pipeline cannot build from the cardinal; they fail closed.
/// </summary>
public sealed class GreekOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Greek";

    /// <inheritdoc />
    protected override string SupportedDomain => "1–20, the round tens to 90, the round hundreds to 900 and 1000";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants)
        => number is >= 1 and <= 20 || IsRoundTen(number) || IsRoundHundred(number) || number == 1000;
}

/// <summary>
/// Finnish ordinal domain (NTS-08). Validated in the nominative by the Aalto University course "Numeroiden
/// taivutus" and the Kielitoimisto (<c>kahdeskymmenes</c>, <c>sadas</c>, <c>tuhannes</c>; <c>nollas</c>,
/// NTS-12): 0–20, the round tens, the round hundreds and 1000. Compounds fuse every component
/// (<c>kahdeskymmenesensimmäinen</c>) and the inflected ordinals change every stem (genitive
/// <c>kolmannen</c>, partitive <c>kolmatta</c>), which the declarative pipeline does not build; those
/// values and every non-nominative case fail closed.
/// </summary>
public sealed class FinnishOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <summary>The value of the case dimension that selects the nominative.</summary>
    private const string Nominative = "nominatiivi";

    /// <inheritdoc />
    protected override string LanguageName => "Finnish";

    /// <inheritdoc />
    protected override string SupportedDomain => "the nominative of 0–20, the round tens to 90, the round hundreds to 900 and 1000";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants)
    {
        if ((activeVariants.TryGetValue("case", out string? caseValue) || activeVariants.TryGetValue("sijamuoto", out caseValue))
            && caseValue != Nominative)
            return false;
        return number is >= 0 and <= 20 || IsRoundTen(number) || IsRoundHundred(number) || number == 1000;
    }
}

/// <summary>
/// Russian ordinal domain (NTS-08). Validated by the <i>Правила русской орфографии и пунктуации</i>, §132–133
/// (only the last word of a composite ordinal is ordinal: <c>тысяча девятьсот девяносто четвёртый</c>):
/// 0 (<c>нулевой</c>, NTS-14) to 1999. From 2000 the configured cardinal is wrong (<c>два тысяча</c>
/// instead of <c>две тысячи</c>) and the one-word round ordinals (<c>двухтысячный</c>,
/// <c>десятитысячный</c>, <c>миллионный</c>) are not built; those values fail closed.
/// </summary>
public sealed class RussianOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Russian";

    /// <inheritdoc />
    protected override string SupportedDomain => "0–1999";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants) => number < 2000;
}

/// <summary>
/// Vietnamese ordinal domain (NTS-08): <c>thứ</c> + cardinal with the forms <c>thứ nhất</c> and <c>thứ tư</c>
/// (Vietnamese dictionaries via vtudien), validated from 0 to 999. From 1000 the configured cardinal is wrong
/// (<c>nghìn</c> instead of <c>một nghìn</c>, <c>hai nghìn năm</c> instead of
/// <c>hai nghìn không trăm linh năm</c>), so those ordinals fail closed.
/// </summary>
public sealed class VietnameseOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Vietnamese";

    /// <inheritdoc />
    protected override string SupportedDomain => "0–999";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants) => number < 1000;
}

/// <summary>
/// Dutch ordinal domain (NTS-08). Taaladvies "Aaneenschrijven van telwoorden" and the Woordenlijst
/// Nederlandse Taal (<c>nulde</c>, <c>tweeduizendste</c>) validate the ordinals from 0 to 999 999, the
/// domain of the configured cardinal (<c>maxNumber</c>). Ordinals are not bounded by <c>maxNumber</c>: from a
/// million the scale lookup of the unvalidated cardinal threw an <see cref="InvalidOperationException"/>
/// (configuration error) and no <c>-ste</c> rule exists for the scale nouns, so those values fail closed.
/// </summary>
public sealed class DutchOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Dutch";

    /// <inheritdoc />
    protected override string SupportedDomain => "0–999999";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants) => number < 1_000_000;
}

/// <summary>
/// Hindi ordinal domain (NTS-08). Kamta Prasad Guru, <i>हिंदी व्याकरण</i> §180: पहला, दूसरा, तीसरा, चौथा, छठा,
/// otherwise <c>-वाँ</c> on the last word, also above a hundred (<c>एक सौ तीनवाँ</c>). Validated from 0 to
/// 99 999; from 100 000 the configured cardinal is wrong (<c>एक सौ हज़ार</c> instead of <c>एक लाख</c>), so
/// those ordinals fail closed.
/// </summary>
public sealed class HindiOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Hindi";

    /// <inheritdoc />
    protected override string SupportedDomain => "0–99999";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants) => number < 100_000;
}

/// <summary>
/// Japanese ordinal domain (NTS-08): the prefix <c>第</c> before the cardinal (デジタル大辞泉, 第: "数を表す語に付いて、
/// ものの順序を表す"), validated from 0 to 1000. Above 1000 the configured cardinal splits the thousands with
/// spaces and does not count by 万 (<c>十 千</c> for 10 000), so those ordinals fail closed.
/// </summary>
public sealed class JapaneseOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Japanese";

    /// <inheritdoc />
    protected override string SupportedDomain => "0–1000";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants) => number <= 1000;
}

/// <summary>
/// Korean ordinal domain (NTS-08): the prefix <c>제</c> before the Sino-Korean numeral (한글 맞춤법 제43항:
/// <c>제삼 항</c>), validated from 0 to 1000. Above 1000 the configured cardinal spaces the thousands
/// (<c>천 일</c>, <c>십 천</c>), whereas Korean numbers are spaced by units of 만, so those ordinals fail closed.
/// </summary>
public sealed class KoreanOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Korean";

    /// <inheritdoc />
    protected override string SupportedDomain => "0–1000";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants) => number <= 1000;
}

/// <summary>
/// Chinese ordinal domain (NTS-08): the prefix <c>第</c> before the cardinal (國語辭典: "用於整數數詞之前。表事物的
/// 順序或等級"), validated from 0 to 100 and for the round hundreds to 900. Other values above 100 rely on a
/// configured cardinal without <c>零</c> or <c>一十</c> (<c>一百一</c> for 101, <c>一百十</c> for 110,
/// <c>一 千</c> for 1000), so their ordinals fail closed.
/// </summary>
public sealed class ChineseOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Chinese";

    /// <inheritdoc />
    protected override string SupportedDomain => "0–100 and the round hundreds to 900";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants)
        => number <= 100 || IsRoundHundred(number);
}

/// <summary>
/// Basque ordinal domain (NTS-08). Euskaltzaindia, rule 18 (<c>bigarren</c>, <c>bosgarren</c>,
/// <c>hogeita batgarren</c>, <c>ehungarren</c>, <c>milagarren</c>) and rule 7 (cardinals), validated from 1 to
/// 1000. Zero has no attested ordinal (rule 7 gives only <c>zero</c>/<c>huts</c>), and above 1000 the configured
/// cardinal omits the <c>eta</c> required by rule 7 (<c>mila bat</c> instead of <c>mila eta bat</c>) or puts the
/// multiplier of a million first (<c>bat milioi</c> instead of <c>milioi bat</c>); those values fail closed.
/// </summary>
public sealed class BasqueOrdinalLanguageSpecifics : OrdinalDomainGuardLanguageSpecifics
{
    /// <inheritdoc />
    protected override string LanguageName => "Basque";

    /// <inheritdoc />
    protected override string SupportedDomain => "1–1000";

    /// <inheritdoc />
    protected override bool IsSupported(long number, IReadOnlyDictionary<string, string> activeVariants)
        => number is >= 1 and <= 1000;
}
