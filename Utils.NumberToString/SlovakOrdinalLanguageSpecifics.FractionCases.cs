using System.Collections.Generic;
using System.Numerics;

namespace Utils.NumberToString;

/// <summary>
/// The declined cardinals of Slovak fractions (NTS-40, NTS-41). The grammar of a compound numeral is set by its last
/// component (P. Ondrus, Slovenská reč 33, 1968, §8: <i>rozhoduje vždy posledný komponent</i>), so only the last
/// component is declined and the words before it keep their absolute form (Slovenská reč 43, 1978: <i>jeden lomené sto
/// štyridsiatimi dvoma</i>). Morfológia slovenského jazyka (1966) gives the forms: <c>jeden</c> (318), <c>dva</c>,
/// <c>tri</c>, <c>štyri</c> (320–323), the paradigm of <c>päť</c> for 5–99 (324: <i>piatich, piatim, piatimi</i>), the
/// compounds <c>dvadsaťdva</c> … declined in both parts (326: <i>dvadsiatich dvoch, dvadsiatim dvom, s dvadsiatimi
/// dvoma</i>), the compounds with <c>-jeden</c> indeclinable, the noun alone taking the case (326: <i>ku štyridsaťjeden
/// družstvám, so štyridsaťjeden družstvami</i>), <c>sto</c> and the compounds with <c>tisíc</c> undeclined before a noun
/// (327–328: <i>k sto robotníkom, so sto robotníkmi</i>; <i>od päťtisíc robotníkov</i>), <c>milión</c> declined after
/// the pattern <c>dub</c> with its multiplier (329–330: <i>z jedného milióna</i>, <i>so štyrmi miliónmi</i>, <i>s
/// dvadsiatimi dvoma miliónmi</i>, or the multiplier undeclined: <i>s päť, osem, dvadsaťdva miliónmi</i>) and
/// <c>miliarda</c> after the pattern <c>žena</c>.
/// </summary>
public sealed partial class SlovakOrdinalLanguageSpecifics
{
    // Every table below is in FractionCase order: nominative, genitive, dative, accusative, locative, instrumental.

    /// <summary>The feminine <c>jedna</c> (Morfológia 318).</summary>
    private static readonly string[] s_oneFeminineCases = ["jedna", "jednej", "jednej", "jednu", "jednej", "jednou"];

    /// <summary>The masculine inanimate <c>jeden</c> (Morfológia 318), the multiplier of <c>milión</c>.</summary>
    private static readonly string[] s_oneMasculineCases = ["jeden", "jedného", "jednému", "jeden", "jednom", "jedným"];

    /// <summary>The oblique cases of 2–4 (index = value; Morfológia 320–323: <i>dvoch, dvom, dvoma</i>; <i>troch, trom,
    /// troma</i>; <i>štyroch, štyrom, štyrmi</i>), the nominative and accusative left to the absolute form.</summary>
    private static readonly string[][] s_fewCases =
    [
        [],
        [],
        ["dva", "dvoch", "dvom", "dva", "dvoch", "dvoma"],
        ["tri", "troch", "trom", "tri", "troch", "troma"],
        ["štyri", "štyroch", "štyrom", "štyri", "štyroch", "štyrmi"],
    ];

    /// <summary>The singular of <c>milión</c> (pattern <c>dub</c>; Morfológia 330: <i>milióna, miliónu, milióne, miliónom</i>).</summary>
    private static readonly string[] s_millionSingular = ["milión", "milióna", "miliónu", "milión", "milióne", "miliónom"];

    /// <summary>The plural of <c>milión</c> (Morfológia 330: <i>miliónov, miliónom, miliónoch, miliónmi</i>).</summary>
    private static readonly string[] s_millionPlural = ["milióny", "miliónov", "miliónom", "milióny", "miliónoch", "miliónmi"];

    /// <summary>The singular of <c>miliarda</c> (pattern <c>žena</c>, Morfológia 101, 330).</summary>
    private static readonly string[] s_milliardSingular = ["miliarda", "miliardy", "miliarde", "miliardu", "miliarde", "miliardou"];

    /// <summary>The plural of <c>miliarda</c> (pattern <c>žena</c>; genitive <c>miliárd</c>, Krátky slovník).</summary>
    private static readonly string[] s_milliardPlural = ["miliardy", "miliárd", "miliardám", "miliardy", "miliardách", "miliardami"];

    /// <summary>The largest number this class declines: the milliards end the long scale below <c>bilión</c>.</summary>
    private const long MaxDeclinedNumber = 999_999_999_999;

    /// <summary>How the last component of a cardinal behaves in a case.</summary>
    private enum LastComponent
    {
        /// <summary>Declined: tens and units (<c>štyridsiatimi dvoma</c>).</summary>
        Declined,
        /// <summary>A scale noun with its multiplier (<c>dvoma miliónmi</c>); a counted noun after it is in the genitive plural.</summary>
        ScaleNoun,
        /// <summary>
        /// Indeclinable: <c>sto</c>, <c>tisíc</c> and their compounds, every compound ending in <c>jeden</c> — the tens
        /// (<c>dvadsaťjeden</c>, Morfológia 326) and the higher compounds from 101 (<c>sto jeden</c>, <c>tisíc
        /// jeden</c>, <c>jeden milión jeden</c>; Jarošová, Slovenská reč 86, 2021, 223, 226; NTS-44).
        /// </summary>
        Indeclinable,
    }

    /// <summary>
    /// Builds the reading of a fraction whose denominator has a fraction noun in a case other than the nominative. One
    /// to four agree with the noun (Morfológia 317: <i>jedna kniha, dve sestry</i>, declined in parallel); after 5 and
    /// more the noun takes the case of the phrase in the oblique cases (317: <i>k desiatim sudom, o siedmich chlapcoch,
    /// s deväťdesiatimi piatimi sedadlami</i>) and the genitive plural in the accusative, as in the nominative; after an
    /// indeclinable last component (<c>sto</c>, <c>tisíc</c>, a compound ending in <c>jeden</c>) the noun alone takes the
    /// case (<i>ku štyridsaťjeden družstvám</i>, <i>k sto robotníkom</i>; PSP 1953, 88–89, quoted by Jarošová, Slovenská
    /// reč 86, 2021, 211: <i>so stojeden družstvami</i>), so <i>sto jeden tretinám</i>, <i>tisíc jeden tretinami</i>
    /// (NTS-44); when the last component is itself <c>milión</c> or <c>miliarda</c> (a round number of millions or
    /// milliards: <i>dvoma miliónmi tretín</i>) the noun is always in the genitive plural (330: <i>pritom meno
    /// počítaného predmetu je vždy v G. pl.</i>). A scale noun followed by a lower group is not the last component and
    /// does not govern the noun: the last component does (<i>jeden milión jeden tretinám</i>, <i>jeden milión
    /// dvadsaťjeden tretinám</i>). The noun follows the pattern <c>žena</c> (<c>tretina</c>) or
    /// <c>ulica</c> (<c>polovica</c>). A zero numerator fails closed (<c>nula</c> is read only in the direct cases, 317:
    /// <i>nula stupňov</i>).
    /// </summary>
    /// <param name="numerator">The numerator.</param>
    /// <param name="forms">The fraction noun.</param>
    /// <param name="fractionCase">The requested case, not the nominative.</param>
    /// <param name="reading">Receives the reading.</param>
    /// <returns><see langword="false"/> when the reading is not sourced.</returns>
    private static bool TryBuildObliqueNounFraction(BigInteger numerator, FractionNounForms forms, FractionCase fractionCase,
        out FractionDenominatorReading? reading)
    {
        reading = null;
        if (numerator.IsZero || numerator > MaxDeclinedNumber)
            return false;
        long value = (long)numerator;
        int column = (int)fractionCase;
        bool direct = fractionCase == FractionCase.Accusative;
        if (value == 1)
        {
            reading = FractionDenominatorReading.WithNumerator(s_oneFeminineCases[column], DeclineFractionNoun(forms, plural: false, fractionCase));
            return true;
        }
        if (value <= 4)
        {
            string few = direct ? (value == 2 ? "dve" : s_fewCases[value][column]) : s_fewCases[value][column];
            reading = FractionDenominatorReading.WithNumerator(few, DeclineFractionNoun(forms, plural: true, fractionCase));
            return true;
        }
        string numeratorText = DeclineLastComponent(value, fractionCase, out LastComponent last);
        bool genitivePlural = direct || last == LastComponent.ScaleNoun;
        reading = FractionDenominatorReading.WithNumerator(numeratorText,
            DeclineFractionNoun(forms, plural: true, genitivePlural ? FractionCase.Genitive : fractionCase));
        return true;
    }

    /// <summary>
    /// Declines a fraction noun after the pattern <c>žena</c> (Morfológia 101: <c>tretiny</c>, <c>tretine</c>,
    /// <c>tretinu</c>, <c>tretinou</c>; plural <c>tretinám</c>, <c>tretinách</c>, <c>tretinami</c>; every <c>-ina</c> noun
    /// has a short syllable before the ending, so the rhythmic law keeps the long <c>-ám</c>, <c>-ách</c>) or, for
    /// <c>polovica</c>, after the pattern <c>ulica</c> (104: <c>polovice</c>, <c>polovici</c>, <c>polovicu</c>,
    /// <c>polovicou</c>; plural <c>poloviciam</c>, <c>poloviciach</c>, <c>polovicami</c>).
    /// </summary>
    /// <param name="forms">The noun's nominative singular, nominative plural and genitive plural.</param>
    /// <param name="plural">Whether the plural is required.</param>
    /// <param name="fractionCase">The case.</param>
    /// <returns>The declined noun.</returns>
    private static string DeclineFractionNoun(FractionNounForms forms, bool plural, FractionCase fractionCase)
    {
        string stem = forms.One[..^1];
        bool soft = forms.One.EndsWith("ica");
        if (!plural)
        {
            return fractionCase switch
            {
                FractionCase.Nominative => forms.One,
                FractionCase.Genitive => soft ? stem + "e" : stem + "y",
                FractionCase.Accusative => stem + "u",
                FractionCase.Instrumental => stem + "ou",
                _ => soft ? stem + "i" : stem + "e",
            };
        }
        return fractionCase switch
        {
            FractionCase.Genitive => forms.Many,
            FractionCase.Dative => soft ? stem + "iam" : stem + "ám",
            FractionCase.Locative => soft ? stem + "iach" : stem + "ách",
            FractionCase.Instrumental => stem + "ami",
            _ => forms.Few,
        };
    }

    /// <summary>
    /// Declines the last component of a cardinal between 2 and 999 999 999 999 for a case, the words before it in their
    /// absolute form (see the class remarks): tens and units after <c>päť</c>, <c>dva</c> … <c>štyri</c> (two words for a
    /// compound: <c>dvadsiatimi dvoma</c>); <c>milión</c> and <c>miliarda</c> with their multiplier (one: <c>jedným
    /// miliónom</c>, <c>jednou miliardou</c>; 2–99 declined when declinable: <c>dvoma miliónmi</c>, <c>dvadsiatimi
    /// dvoma miliónmi</c>; any other multiplier in its absolute form: <c>dvadsaťjeden miliónmi</c>, <c>sto dva
    /// miliónmi</c>). An indeclinable last component returns the absolute cardinal: <c>sto</c>, <c>tisíc</c> and the
    /// other round values below the millions, and every compound whose last group ends in <c>jeden</c> but not in
    /// <c>jedenásť</c> (21–91; 101, 1001, 2001, 1 000 001: Jarošová 2021, 226, <i>vyššie zložené číslovky končiace sa na
    /// číslovku jeden (podobne ako typ dvadsaťjeden) sú nesklonné</i>; her note 18, 221, and table 2.4.1, 223–224, count
    /// the millions and milliards among the higher compounds). The nominative and
    /// accusative are the absolute cardinal, except a final <c>milión</c> or <c>miliarda</c> after one, which agrees
    /// (<c>jednu miliardu</c>).
    /// </summary>
    /// <param name="number">The number, at least 2.</param>
    /// <param name="fractionCase">The case.</param>
    /// <param name="last">Receives the behavior of the last component.</param>
    /// <returns>The cardinal with its last component in the case, or the absolute cardinal.</returns>
    private static string DeclineLastComponent(long number, FractionCase fractionCase, out LastComponent last)
    {
        int rest = (int)(number % 100);
        if (rest == 0)
        {
            long millions = number / 1_000_000 % 1000;
            if (number % 1_000_000 == 0 && millions != 0)
            {
                last = LastComponent.ScaleNoun;
                return Join(Cardinal(number - millions * 1_000_000), DeclineScale((int)millions, fractionCase, s_millionSingular, s_millionPlural, feminine: false));
            }
            if (number % 1_000_000_000 == 0)
            {
                last = LastComponent.ScaleNoun;
                return DeclineScale((int)(number / 1_000_000_000), fractionCase, s_milliardSingular, s_milliardPlural, feminine: true);
            }
            last = LastComponent.Indeclinable;
            return Cardinal(number);
        }
        if (rest % 10 == 1 && rest != 11)
        {
            last = LastComponent.Indeclinable;
            return Cardinal(number);
        }
        last = LastComponent.Declined;
        string tail = fractionCase is FractionCase.Nominative or FractionCase.Accusative ? Group(rest, joined: false) : DeclineBelowHundred(rest, fractionCase);
        return Join(Cardinal(number - rest), tail);
    }

    /// <summary>
    /// Declines a scale noun and its multiplier. After one the noun is singular and the multiplier agrees; after two to
    /// four the noun is the nominative plural in the direct cases; after the other multipliers the genitive plural in
    /// the direct cases (as the SK configuration writes them); the plural of the case otherwise.
    /// </summary>
    /// <param name="multiplier">The multiplier, 1–999.</param>
    /// <param name="fractionCase">The case.</param>
    /// <param name="singular">The singular of the noun by case.</param>
    /// <param name="plural">The plural of the noun by case.</param>
    /// <param name="feminine">Whether the noun is feminine (<c>miliarda</c>: <c>jedna</c>, <c>dve</c>).</param>
    /// <returns>The declined multiplier and noun.</returns>
    private static string DeclineScale(int multiplier, FractionCase fractionCase, string[] singular, string[] plural, bool feminine)
    {
        int column = (int)fractionCase;
        bool direct = fractionCase is FractionCase.Nominative or FractionCase.Accusative;
        if (multiplier == 1)
            return (feminine ? s_oneFeminineCases : s_oneMasculineCases)[column] + " " + singular[column];
        if (multiplier <= 4)
        {
            string few = direct && feminine && multiplier == 2 ? "dve" : s_fewCases[multiplier][column];
            return few + " " + plural[direct ? (int)FractionCase.Nominative : column];
        }
        bool declinable = multiplier < 100 && (multiplier % 10 != 1 || multiplier == 11);
        string head = direct || !declinable ? Group(multiplier, joined: false) : DeclineBelowHundred(multiplier, fractionCase);
        return head + " " + plural[direct ? (int)FractionCase.Genitive : column];
    }

    /// <summary>
    /// Declines a number between 2 and 99 (not a compound with <c>-jeden</c>) in an oblique case: 2–4 after their
    /// paradigms, the others after <c>päť</c>, whose genitive and locative in <c>-ich</c> and dative in <c>-im</c> share
    /// the stem of the instrumental in <c>-imi</c> (<c>piatich</c>, <c>piatim</c>, <c>piatimi</c>; <c>jedenástich</c>;
    /// <c>dvadsiatich</c>), a compound in two words (<c>dvadsiatim dvom</c>).
    /// </summary>
    /// <param name="number">The number.</param>
    /// <param name="fractionCase">An oblique case.</param>
    /// <returns>The declined number.</returns>
    private static string DeclineBelowHundred(int number, FractionCase fractionCase)
    {
        if (number is >= 10 and < 20)
            return LikeFive(s_instrumentalTeens[number - 10], fractionCase);
        int unit = number % 10;
        string unitText = unit == 0 ? string.Empty : unit <= 4 ? s_fewCases[unit][(int)fractionCase] : LikeFive(s_instrumentalUnits[unit], fractionCase);
        if (number < 10)
            return unitText;
        string tens = LikeFive(s_instrumentalTens[number / 10], fractionCase);
        return unit == 0 ? tens : tens + " " + unitText;
    }

    /// <summary>Forms a case of the <c>päť</c> paradigm from its instrumental in <c>-imi</c>.</summary>
    /// <param name="instrumental">The instrumental (<c>piatimi</c>).</param>
    /// <param name="fractionCase">An oblique case.</param>
    /// <returns>The form (<c>piatich</c>, <c>piatim</c>, <c>piatimi</c>).</returns>
    private static string LikeFive(string instrumental, FractionCase fractionCase)
    {
        string stem = instrumental[..^2];
        return fractionCase switch
        {
            FractionCase.Dative => stem + "m",
            FractionCase.Instrumental => instrumental,
            _ => stem + "ch",
        };
    }

    /// <summary>
    /// Writes the absolute cardinal of a number between 0 and 999 999 999 999 as the SK configuration does: <c>jedna
    /// miliarda</c>, <c>dve miliardy</c>, <c>päť miliárd</c>; <c>jeden milión</c>, <c>dva milióny</c>, <c>dvadsaťdva
    /// miliónov</c>, <c>sto dva miliónov</c> (the multiplier written as a group, the hundreds apart); the thousands and
    /// units as <see cref="Cardinal(int)"/>.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <returns>The cardinal (empty for 0).</returns>
    private static string Cardinal(long number)
    {
        int milliards = (int)(number / 1_000_000_000 % 1000);
        int millions = (int)(number / 1_000_000 % 1000);
        int rest = (int)(number % 1_000_000);
        string text = string.Empty;
        if (milliards > 0)
            text = DeclineScale(milliards, FractionCase.Nominative, s_milliardSingular, s_milliardPlural, feminine: true);
        if (millions > 0)
            text = Join(text, DeclineScale(millions, FractionCase.Nominative, s_millionSingular, s_millionPlural, feminine: false));
        return rest == 0 ? text : Join(text, Cardinal(rest));
    }

    /// <summary>Joins two word groups with a space, either of which may be empty.</summary>
    /// <param name="head">The first group.</param>
    /// <param name="tail">The second group.</param>
    /// <returns>The joined text.</returns>
    private static string Join(string head, string tail)
        => head.Length == 0 ? tail : tail.Length == 0 ? head : head + " " + tail;
}
