using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Numerics;
using Utils.NumberToString;
using Utils.Numerics;

namespace UtilsTest.NumberToString;

/// <summary>
/// NTS-40 (the oblique cases of Ukrainian, Czech and Slovak fractions) and NTS-41 (the Czech and Slovak slash readings
/// beyond the NTS-39 domain), implemented together on one declined-cardinal builder per language, internal to the
/// language specifics: the public conversion of a cardinal keeps no case. Sources: <c>docs/NTS-08-linguistic-sources.md</c>,
/// NTS-40/41 section.
/// </summary>
[TestClass]
public class NumberToStringNts40Nts41Tests
{
    /// <summary>Gets a built-in converter.</summary>
    /// <param name="language">The culture.</param>
    /// <returns>The converter.</returns>
    private static NumberToStringConverter Get(string language) => NumberToStringConverter.GetConverter(language);

    // ===== NTS-40 Ukrainian ==========================================================================

    /// <summary>
    /// Український правопис 2019, §107.1, verbatim: a fraction declines as an ordinary numeral (<c>двом третім</c>,
    /// <c>трьома п'ятими</c>; <c>ділити на одну двадцяту</c>).
    /// </summary>
    [TestMethod]
    [DataRow(2L, 3L, "case=давальний", "двом третім")]
    [DataRow(3L, 5L, "case=орудний", "трьома п'ятими")]
    [DataRow(1L, 20L, "case=знахідний", "одну двадцяту")]
    public void Ukrainian_Pravopys107(long numerator, long denominator, string variant, string expected)
        => Assert.AreEqual(expected, Get("UK").ConvertFraction(numerator, denominator, variant));

    /// <summary>
    /// The case matrix of 1/3, 2/3, 3/5 and 21/100: after one (and a compound ending in <c>одна</c>) the feminine singular
    /// ordinal in the case, otherwise the plural in the case, the accusative of an inanimate quantity being its
    /// nominative (<c>дві третіх</c>); numerals after §105, ordinals after VESUM (<c>третьої</c>, <c>третіми</c>).
    /// </summary>
    [TestMethod]
    [DataRow(1L, 3L, "case=родовий", "однієї третьої")]
    [DataRow(1L, 3L, "case=давальний", "одній третій")]
    [DataRow(1L, 3L, "case=знахідний", "одну третю")]
    [DataRow(1L, 3L, "case=орудний", "однією третьою")]
    [DataRow(1L, 3L, "case=місцевий", "одній третій")]
    [DataRow(2L, 3L, "case=родовий", "двох третіх")]
    [DataRow(2L, 3L, "case=знахідний", "дві третіх")]
    [DataRow(2L, 3L, "case=орудний", "двома третіми")]
    [DataRow(2L, 3L, "case=місцевий", "двох третіх")]
    [DataRow(3L, 5L, "case=родовий", "трьох п'ятих")]
    [DataRow(3L, 5L, "case=давальний", "трьом п'ятим")]
    [DataRow(3L, 5L, "case=знахідний", "три п'ятих")]
    [DataRow(3L, 5L, "case=місцевий", "трьох п'ятих")]
    [DataRow(21L, 100L, "case=родовий", "двадцяти однієї сотої")]
    [DataRow(21L, 100L, "case=давальний", "двадцяти одній сотій")]
    [DataRow(21L, 100L, "case=знахідний", "двадцять одну соту")]
    [DataRow(21L, 100L, "case=орудний", "двадцятьма однією сотою")]
    [DataRow(21L, 100L, "case=місцевий", "двадцяти одній сотій")]
    public void Ukrainian_CaseMatrix(long numerator, long denominator, string variant, string expected)
        => Assert.AreEqual(expected, Get("UK").ConvertFraction(numerator, denominator, variant));

    /// <summary>
    /// Compound numerators: every part declined (§105.6), <c>сорок</c>/<c>сто</c> in <c>-а</c> (§105.7), the hundreds
    /// after §105.5, the scale nouns as nouns (§105.11); only the last word of a compound ordinal is declined (§106.2).
    /// </summary>
    [TestMethod]
    [DataRow(5L, 7L, "case=родовий", "п'яти сьомих")]
    [DataRow(23L, 100L, "case=орудний", "двадцятьма трьома сотими")]
    [DataRow(105L, 7L, "case=давальний", "ста п'яти сьомим")]
    [DataRow(365L, 1000L, "case=орудний", "трьомастами шістдесятьма п'ятьма тисячними")]
    [DataRow(365L, 1000L, "case=родовий", "трьохсот шістдесяти п'яти тисячних")]
    [DataRow(365L, 1000L, "case=місцевий", "трьохстах шістдесяти п'яти тисячних")]
    [DataRow(1042L, 3L, "case=місцевий", "тисячі сорока двох третіх")]
    [DataRow(2023L, 3L, "case=давальний", "двом тисячам двадцяти трьом третім")]
    [DataRow(1001L, 3L, "case=орудний", "тисячею однією третьою")]
    [DataRow(1_000_001L, 3L, "case=орудний", "одним мільйоном однією третьою")]
    [DataRow(1_358_423L, 7L, "case=давальний", "одному мільйонові трьомстам п'ятдесяти восьми тисячам чотирьомстам двадцяти трьом сьомим")]
    [DataRow(5L, 365L, "case=орудний", "п'ятьма триста шістдесят п'ятими")]
    [DataRow(2_147_483_647L, 3L, "case=орудний",
        "двома мільярдами ста сорока сьома мільйонами чотирмастами вісімдесятьма трьома тисячами шістьмастами сорока сьома третіми")]
    public void Ukrainian_CompoundNumerators(long numerator, long denominator, string variant, string expected)
        => Assert.AreEqual(expected, Get("UK").ConvertFraction(numerator, denominator, variant));

    /// <summary>After a numerator ending in a scale noun, a noun governing the genitive (§105.11), the denominator is the genitive plural.</summary>
    [TestMethod]
    [DataRow(1000L, 3L, "case=орудний", "тисячею третіх")]
    [DataRow(1000L, 3L, "case=знахідний", "тисячу третіх")]
    [DataRow(2_000_000L, 3L, "case=давальний", "двом мільйонам третіх")]
    [DataRow(22_000_000L, 7L, "case=родовий", "двадцяти двох мільйонів сьомих")]
    [DataRow(21_000L, 37L, "case=знахідний", "двадцять одну тисячу тридцять сьомих")]
    public void Ukrainian_ScaleNumerators(long numerator, long denominator, string variant, string expected)
        => Assert.AreEqual(expected, Get("UK").ConvertFraction(numerator, denominator, variant));

    /// <summary>
    /// The accusative of an inanimate quantity is its nominative: over a range of numerators without a feminine singular
    /// (<c>одна</c>, a singular <c>тисяча</c>), the accusative reading equals the nominative one of the cardinal pipeline,
    /// so the internal builder spells the cardinals exactly as the UK configuration does.
    /// </summary>
    [TestMethod]
    public void Ukrainian_AccusativeMatchesTheNominativePipeline()
    {
        var uk = Get("UK");
        int checkedCount = 0;
        for (long n = 2; n < 3_000_000_000; n = n < 2000 ? n + 1 : n * 3 + 7)
        {
            long lastTwo = n % 100;
            long thousands = n / 1000 % 1000;
            bool feminineSingular = (lastTwo % 10 == 1 && lastTwo != 11)
                || (thousands % 10 == 1 && thousands % 100 != 11);
            if (feminineSingular)
                continue;
            Assert.AreEqual(uk.ConvertFraction(n, 7), uk.ConvertFraction(n, 7, "case=знахідний"), n.ToString());
            checkedCount++;
        }
        Assert.IsTrue(checkedCount > 800, checkedCount.ToString());
    }

    // ===== NTS-40 Czech ==============================================================================

    /// <summary>
    /// The noun fractions in the oblique cases: one to four agree with the noun (IJP: <c>dvě ženy, dvou žen, dvěma
    /// ženám</c>), after 5–99 the noun takes the case (<c>pěti ženám, o pěti ženách, s pěti ženami</c>), the nouns after
    /// their IJP paradigms (<c>třetině</c>, <c>třetinou</c>, <c>třetinám</c>).
    /// </summary>
    [TestMethod]
    [DataRow(1L, 3L, "case=genitiv", "jedné třetiny")]
    [DataRow(1L, 3L, "case=dativ", "jedné třetině")]
    [DataRow(1L, 3L, "case=akuzativ", "jednu třetinu")]
    [DataRow(1L, 3L, "case=lokál", "jedné třetině")]
    [DataRow(1L, 3L, "case=instrumentál", "jednou třetinou")]
    [DataRow(2L, 3L, "case=genitiv", "dvou třetin")]
    [DataRow(2L, 3L, "case=dativ", "dvěma třetinám")]
    [DataRow(2L, 3L, "case=akuzativ", "dvě třetiny")]
    [DataRow(2L, 3L, "case=lokál", "dvou třetinách")]
    [DataRow(2L, 3L, "case=instrumentál", "dvěma třetinami")]
    [DataRow(3L, 5L, "case=genitiv", "tří pětin")]
    [DataRow(3L, 5L, "case=dativ", "třem pětinám")]
    [DataRow(3L, 5L, "case=lokál", "třech pětinách")]
    [DataRow(3L, 5L, "case=instrumentál", "třemi pětinami")]
    [DataRow(5L, 7L, "case=genitiv", "pěti sedmin")]
    [DataRow(5L, 2L, "case=dativ", "pěti polovinám")]
    [DataRow(5L, 2L, "case=akuzativ", "pět polovin")]
    [DataRow(1L, 100L, "case=akuzativ", "jednu setinu")]
    [DataRow(1L, 1_000_000_000L, "case=instrumentál", "jednou miliardtinou")]
    public void Czech_NounCases(long numerator, long denominator, string variant, string expected)
        => Assert.AreEqual(expected, Get("CS").ConvertFraction(numerator, denominator, variant));

    /// <summary>
    /// Compound numerators, every part declined (IJP "Členění čísel": <c>před třemi sty šedesáti pěti lety</c>) but
    /// <c>sto</c> before another numeral (<c>ve sto dvaceti případech</c>); after a numeral ending in <c>sto</c>,
    /// <c>tisíc</c>, <c>milion</c> or <c>miliarda</c> the noun keeps the genitive plural (<c>ke stu korun</c>).
    /// </summary>
    [TestMethod]
    [DataRow(22L, 100L, "case=dativ", "dvaceti dvěma setinám")]
    [DataRow(23L, 100L, "case=lokál", "dvaceti třech setinách")]
    [DataRow(120L, 7L, "case=dativ", "sto dvaceti sedminám")]
    [DataRow(142L, 1000L, "case=genitiv", "sto čtyřiceti dvou tisícin")]
    [DataRow(365L, 1000L, "case=instrumentál", "třemi sty šedesáti pěti tisícinami")]
    [DataRow(365L, 1000L, "case=dativ", "třem stům šedesáti pěti tisícinám")]
    [DataRow(2023L, 1_000_000L, "case=dativ", "dvěma tisícům dvaceti třem miliontinám")]
    [DataRow(1_358_423L, 1_000_000L, "case=lokál", "jednom milionu třech stech padesáti osmi tisících čtyřech stech dvaceti třech miliontinách")]
    [DataRow(100L, 7L, "case=instrumentál", "stem sedmin")]
    [DataRow(2000L, 3L, "case=dativ", "dvěma tisícům třetin")]
    [DataRow(2_000_000L, 3L, "case=instrumentál", "dvěma miliony třetin")]
    [DataRow(1_000_000_000L, 3L, "case=akuzativ", "jednu miliardu třetin")]
    [DataRow(22L, 100L, "case=akuzativ", "dvacet dva setin")]
    [DataRow(21L, 100L, "case=akuzativ", "dvacet jedna setin")]
    public void Czech_CompoundNumerators(long numerator, long denominator, string variant, string expected)
        => Assert.AreEqual(expected, Get("CS").ConvertFraction(numerator, denominator, variant));

    /// <summary>
    /// Still closed: a compound numerator ending in <c>jedna</c> outside the accusative (the IJP gives only its nominative),
    /// a multiplier ending in one, zero, and the <c>lomeno</c> reading in an oblique case (no source declines it).
    /// </summary>
    [TestMethod]
    [DataRow(21L, 100L, "case=genitiv")]
    [DataRow(101L, 3L, "case=dativ")]
    [DataRow(1001L, 3L, "case=instrumentál")]
    [DataRow(21_000L, 3L, "case=genitiv")]
    [DataRow(0L, 3L, "case=akuzativ")]
    [DataRow(1L, 23L, "case=instrumentál")]
    [DataRow(2L, 365L, "case=genitiv")]
    public void Czech_ObliqueStillClosed(long numerator, long denominator, string variant)
        => Assert.ThrowsExactly<NotSupportedException>(() => Get("CS").ConvertFraction(numerator, denominator, variant));

    // ===== NTS-40 Slovak =============================================================================

    /// <summary>
    /// The noun fractions in the oblique cases (Morfológia: <c>jedna</c> 318, <c>dva</c>/<c>tri</c> 320–322, <c>päť</c>
    /// 324; the patterns <c>žena</c> and <c>ulica</c>, 101–104).
    /// </summary>
    [TestMethod]
    [DataRow(1L, 3L, "case=genitív", "jednej tretiny")]
    [DataRow(1L, 3L, "case=datív", "jednej tretine")]
    [DataRow(1L, 3L, "case=akuzatív", "jednu tretinu")]
    [DataRow(1L, 3L, "case=lokál", "jednej tretine")]
    [DataRow(1L, 3L, "case=inštrumentál", "jednou tretinou")]
    [DataRow(2L, 3L, "case=genitív", "dvoch tretín")]
    [DataRow(2L, 3L, "case=datív", "dvom tretinám")]
    [DataRow(2L, 3L, "case=akuzatív", "dve tretiny")]
    [DataRow(2L, 3L, "case=lokál", "dvoch tretinách")]
    [DataRow(2L, 3L, "case=inštrumentál", "dvoma tretinami")]
    [DataRow(3L, 5L, "case=genitív", "troch pätín")]
    [DataRow(3L, 5L, "case=datív", "trom pätinám")]
    [DataRow(3L, 5L, "case=inštrumentál", "troma pätinami")]
    [DataRow(5L, 7L, "case=genitív", "piatich sedmín")]
    [DataRow(1L, 2L, "case=datív", "jednej polovici")]
    [DataRow(1L, 2L, "case=inštrumentál", "jednou polovicou")]
    [DataRow(2L, 2L, "case=datív", "dvom poloviciam")]
    [DataRow(5L, 2L, "case=lokál", "piatich poloviciach")]
    [DataRow(1L, 1000L, "case=akuzatív", "jednu tisícinu")]
    public void Slovak_NounCases(long numerator, long denominator, string variant, string expected)
        => Assert.AreEqual(expected, Get("SK").ConvertFraction(numerator, denominator, variant));

    /// <summary>
    /// Compound numerators: the last component declined, the words before it absolute (Ondrus §8; SR 1978); <c>dvadsaťdva</c>
    /// declined in both parts (Morfológia 326: <c>dvadsiatim dvom žiakom</c>); a <c>-jeden</c> compound, <c>sto</c> and the
    /// <c>tisíc</c> compounds undeclined, the noun in the case (326–328: <c>ku štyridsaťjeden družstvám</c>, <c>k sto
    /// robotníkom</c>, <c>od päťtisíc robotníkov</c>); after <c>milión</c>/<c>miliarda</c> the noun in the genitive plural
    /// (330).
    /// </summary>
    [TestMethod]
    [DataRow(21L, 100L, "case=genitív", "dvadsaťjeden stotín")]
    [DataRow(21L, 100L, "case=datív", "dvadsaťjeden stotinám")]
    [DataRow(21L, 100L, "case=akuzatív", "dvadsaťjeden stotín")]
    [DataRow(21L, 100L, "case=lokál", "dvadsaťjeden stotinách")]
    [DataRow(21L, 100L, "case=inštrumentál", "dvadsaťjeden stotinami")]
    [DataRow(22L, 100L, "case=datív", "dvadsiatim dvom stotinám")]
    [DataRow(23L, 100L, "case=lokál", "dvadsiatich troch stotinách")]
    [DataRow(95L, 7L, "case=inštrumentál", "deväťdesiatimi piatimi sedminami")]
    [DataRow(120L, 7L, "case=datív", "sto dvadsiatim sedminám")]
    [DataRow(142L, 1000L, "case=genitív", "sto štyridsiatich dvoch tisícin")]
    [DataRow(365L, 1000L, "case=inštrumentál", "tristo šesťdesiatimi piatimi tisícinami")]
    [DataRow(1042L, 3L, "case=inštrumentál", "tisíc štyridsiatimi dvoma tretinami")]
    [DataRow(100L, 3L, "case=datív", "sto tretinám")]
    [DataRow(2000L, 3L, "case=inštrumentál", "dvetisíc tretinami")]
    [DataRow(1_000_000L, 3L, "case=inštrumentál", "jedným miliónom tretín")]
    [DataRow(2_000_000L, 3L, "case=inštrumentál", "dvoma miliónmi tretín")]
    [DataRow(22_000_000L, 3L, "case=inštrumentál", "dvadsiatimi dvoma miliónmi tretín")]
    [DataRow(21_000_000L, 3L, "case=datív", "dvadsaťjeden miliónom tretín")]
    [DataRow(1_000_000_000L, 3L, "case=akuzatív", "jednu miliardu tretín")]
    public void Slovak_CompoundNumerators(long numerator, long denominator, string variant, string expected)
        => Assert.AreEqual(expected, Get("SK").ConvertFraction(numerator, denominator, variant));

    /// <summary>
    /// Still closed: zero and the <c>lomené</c> reading in an oblique case. The bare final <c>jeden</c> (101, 1 000 001)
    /// closed here by NTS-40 was opened by NTS-44 (<c>NumberToStringNts44Tests</c>).
    /// </summary>
    [TestMethod]
    [DataRow(0L, 3L, "case=akuzatív")]
    [DataRow(1L, 23L, "case=inštrumentál")]
    [DataRow(1L, 2_000_003L, "case=genitív")]
    public void Slovak_ObliqueStillClosed(long numerator, long denominator, string variant)
        => Assert.ThrowsExactly<NotSupportedException>(() => Get("SK").ConvertFraction(numerator, denominator, variant));

    /// <summary>A numerator ending in a bare <c>jeden</c> keeps its absolute form in the accusative, as in the nominative.</summary>
    [TestMethod]
    public void Slovak_BareOneAccusative()
        => Assert.AreEqual(Get("SK").ConvertFraction(101, 3), Get("SK").ConvertFraction(101, 3, "case=akuzatív"));

    // ===== NTS-40 caller variants ====================================================================

    /// <summary>
    /// The case applies to the whole fraction and composes with the fraction-local gender: the caller's gender does not
    /// change the numerator the fraction requires.
    /// </summary>
    [TestMethod]
    public void CaseAndGender_FractionLocalGenderWins()
    {
        Assert.AreEqual("двом третім", Get("UK").ConvertFraction(2, 3, "case=давальний", "gender=чоловічий"));
        Assert.AreEqual("однією третьою", Get("UK").ConvertFraction(1, 3, "case=орудний", "gender=середній"));
        Assert.AreEqual("dvě třetiny", Get("CS").ConvertFraction(2, 3, "case=akuzativ", "gender=mužský"));
        Assert.AreEqual("jednu třetinu", Get("CS").ConvertFraction(1, 3, "case=akuzativ", "gender=střední"));
        Assert.AreEqual("dve tretiny", Get("SK").ConvertFraction(2, 3, "case=akuzatív", "gender=mužský"));
        Assert.AreEqual("dvoma tretinami", Get("SK").ConvertFraction(2, 3, "case=inštrumentál", "gender=mužský"));
    }

    /// <summary>A negative fraction in an oblique case takes the language's minus once, outside the declined phrase.</summary>
    [TestMethod]
    public void ObliqueCase_Sign()
    {
        Assert.AreEqual("мінус двом третім", Get("UK").ConvertFraction(-2, 3, "case=давальний"));
        Assert.AreEqual("minus dvěma třetinám", Get("CS").ConvertFraction(2, -3, "case=dativ"));
        Assert.AreEqual("mínus dvom tretinám", Get("SK").ConvertFraction(-2, 3, "case=datív"));
    }

    /// <summary>
    /// The numerator a plugin spells for an oblique case goes through the caller's <c>AdjustFunction</c>, like the
    /// numerator the cardinal pipeline renders in the nominative, and the language finalization still runs once, on the
    /// complete phrase. Numerator 2 is the case NTS-43 fixed: its nominative is a variant replacement (UK
    /// <c>два → дві</c>, CS <c>dva → dvě</c>, SK <c>dva → dve</c>) that now runs before the adjustment.
    /// </summary>
    [TestMethod]
    [DataRow("UK", 5L, "case=давальний", "П'ЯТЬ третіх", "П'ЯТИ третім")]
    [DataRow("CS", 5L, "case=dativ", "PĚT třetin", "PĚTI třetinám")]
    [DataRow("SK", 5L, "case=datív", "PÄŤ tretín", "PIATIM tretinám")]
    [DataRow("UK", 2L, "case=давальний", "ДВІ третіх", "ДВОМ третім")]
    [DataRow("CS", 2L, "case=dativ", "DVĚ třetiny", "DVĚMA třetinám")]
    [DataRow("SK", 2L, "case=datív", "DVE tretiny", "DVOM tretinám")]
    public void ObliqueNumerator_GoesThroughTheAdjustFunction(string culture, long numerator, string variant, string nominative, string oblique)
    {
        NumberToStringConverter source = Get(culture);
        var finalizer = new CountingFractionSpecifics(source.LanguageSpecifics);
        var converter = new NumberToStringConverter(new NumberToStringConverterOptions(source)
        {
            AdjustFunction = text => text.ToUpperInvariant(),
            LanguageSpecifics = finalizer,
        });

        Assert.AreEqual(nominative, converter.ConvertFraction(numerator, 3), "the nominative is inflected, then adjusted");
        CollectionAssert.AreEqual(new[] { nominative }, finalizer.Finalized);

        finalizer.Finalized.Clear();
        Assert.AreEqual(oblique, converter.ConvertFraction(numerator, 3, variant));
        CollectionAssert.AreEqual(new[] { oblique }, finalizer.Finalized, "finalized once, on the whole phrase");
    }

    /// <summary>Wraps a built-in fraction plugin and records every finalization call.</summary>
    /// <param name="inner">The built-in language specifics.</param>
    private sealed class CountingFractionSpecifics(INumberToStringLanguageSpecifics inner)
        : INumberToStringLanguageSpecifics, IFractionLanguageSpecifics, IOrdinalLanguageSpecifics
    {
        /// <inheritdoc />
        public bool TryConvertOrdinal(int number, IReadOnlyDictionary<string, string> activeVariants, out string? result)
            => ((IOrdinalLanguageSpecifics)inner).TryConvertOrdinal(number, activeVariants, out result);

        /// <summary>Gets the texts passed to <see cref="FinalizeWriting"/>, in call order.</summary>
        public List<string> Finalized { get; } = [];

        /// <inheritdoc />
        public string FinalizeWriting(string languageIdentifier, string text)
        {
            Finalized.Add(text);
            return inner.FinalizeWriting(languageIdentifier, text);
        }

        /// <inheritdoc />
        public bool TryGetFractionDenominator(BigInteger numerator, BigInteger denominator, IReadOnlyDictionary<string, string> variants,
            out FractionDenominatorReading? reading)
            => ((IFractionLanguageSpecifics)inner).TryGetFractionDenominator(numerator, denominator, variants, out reading);
    }

    /// <summary>The ordinary cardinal keeps no case: <c>Convert</c> under a case variant is the nominative cardinal.</summary>
    [TestMethod]
    [DataRow("UK", "case=орудний", 365L)]
    [DataRow("CS", "case=instrumentál", 365L)]
    [DataRow("SK", "case=inštrumentál", 365L)]
    public void Cardinal_HasNoCase(string culture, string variant, long number)
        => Assert.AreEqual(Get(culture).Convert(number), Get(culture).Convert(number, variant));

    // ===== NTS-41 Czech ==============================================================================

    /// <summary>
    /// A final <c>jedna</c> is the feminine instrumental <c>jednou</c>: a number in arithmetic is feminine (SSČ <c>jedna a
    /// dvě jsou tři</c>; Prandstetter, Naše řeč 22, 1938: <c>čtyřmi iks minus jedna</c>) and the slash reading declines it
    /// so (Prandstetter: <c>lomeno jednou celou</c>).
    /// </summary>
    [TestMethod]
    [DataRow(1L, 21L, "jedna lomeno dvaceti jednou")]
    [DataRow(1L, 31L, "jedna lomeno třiceti jednou")]
    [DataRow(1L, 101L, "jedna lomeno sto jednou")]
    [DataRow(1L, 121L, "jedna lomeno sto dvaceti jednou")]
    [DataRow(1L, 1001L, "jedna lomeno tisícem jednou")]
    [DataRow(21L, 101L, "dvacet jedna lomeno sto jednou")]
    [DataRow(0L, 1001L, "nula lomeno tisícem jednou")]
    public void Czech_Lomeno_FinalOne(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("CS").ConvertFraction(numerator, denominator));

    /// <summary>
    /// Beyond 999, every part declined (IJP "Členění čísel": <c>bez tisíce osmi set čtyřiceti sedmi korun</c>; Prandstetter
    /// <c>lomeno třemi sty padesáti šesti</c>), the scale nouns after their IJP paradigms.
    /// </summary>
    [TestMethod]
    [DataRow(1L, 1042L, "jedna lomeno tisícem čtyřiceti dvěma")]
    [DataRow(1L, 2023L, "jedna lomeno dvěma tisíci dvaceti třemi")]
    [DataRow(1L, 10_000L, "jedna lomeno deseti tisíci")]
    [DataRow(1L, 12_345L, "jedna lomeno dvanácti tisíci třemi sty čtyřiceti pěti")]
    [DataRow(1L, 1_000_001L, "jedna lomeno jedním milionem jednou")]
    [DataRow(2L, 365_142L, "dvě lomeno třemi sty šedesáti pěti tisíci sto čtyřiceti dvěma")]
    [DataRow(5L, 1_358_423L, "pět lomeno jedním milionem třemi sty padesáti osmi tisíci čtyřmi sty dvaceti třemi")]
    [DataRow(1L, 2_000_000L, "jedna lomeno dvěma miliony")]
    [DataRow(1L, 1_000_000_001L, "jedna lomeno jednou miliardou jednou")]
    public void Czech_Lomeno_Large(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("CS").ConvertFraction(numerator, denominator));

    /// <summary>The fraction nouns keep precedence over the slash reading (<c>jedna tisícina</c>, not <c>lomeno tisícem</c>).</summary>
    [TestMethod]
    [DataRow(1L, 1000L, "jedna tisícina")]
    [DataRow(1L, 100_000L, "jedna stotisícina")]
    [DataRow(1L, 1_000_000L, "jedna miliontina")]
    [DataRow(1L, 1_000_000_000L, "jedna miliardtina")]
    public void Czech_NounsTakePrecedence(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("CS").ConvertFraction(numerator, denominator));

    // ===== NTS-41 Slovak =============================================================================

    /// <summary>
    /// Beyond 999 999 the last component still decides (Ondrus §8): a final <c>milión</c>/<c>miliarda</c> is a declinable
    /// noun (Morfológia 329–330: <c>s dvadsiatimi dvoma miliónmi</c>; with an indeclinable multiplier <c>s päť, osem,
    /// dvadsaťdva miliónmi</c>), a final declinable numeral is declined alone (<c>dva milióny troma</c>), a final bare
    /// <c>jeden</c> takes <c>číslom</c>.
    /// </summary>
    [TestMethod]
    [DataRow(1L, 1_000_001L, "jedna lomené číslom jeden milión jeden")]
    [DataRow(1L, 2_000_000L, "jedna lomené dvoma miliónmi")]
    [DataRow(1L, 2_000_003L, "jedna lomené dva milióny troma")]
    [DataRow(1L, 22_000_000L, "jedna lomené dvadsiatimi dvoma miliónmi")]
    [DataRow(1L, 22_000_003L, "jedna lomené dvadsaťdva miliónov troma")]
    [DataRow(1L, 1_000_000_001L, "jedna lomené číslom jedna miliarda jeden")]
    [DataRow(1L, 21_000_000L, "jedna lomené dvadsaťjeden miliónmi")]
    [DataRow(1L, 142_000_000L, "jedna lomené sto štyridsaťdva miliónmi")]
    [DataRow(1L, 2_000_000_000L, "jedna lomené dvoma miliardami")]
    [DataRow(1L, 1_001_000_000L, "jedna lomené jedna miliarda jedným miliónom")]
    [DataRow(1L, 1_000_001_000L, "jedna lomené číslom jedna miliarda tisíc")]
    public void Slovak_Lomene_Large(long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get("SK").ConvertFraction(numerator, denominator));

    /// <summary>
    /// Parity with the configuration: whenever the last component is indeclinable or a bare one, the text after
    /// <c>číslom</c> is the cardinal of <c>Convert</c>; otherwise the reading keeps the cardinal's words before the last
    /// component. Sampled up to 999 999 999 999.
    /// </summary>
    [TestMethod]
    public void Slovak_Lomene_LargeParityWithTheCardinal()
    {
        var sk = Get("SK");
        int checkedCount = 0;
        for (long denominator = 1_000_001; denominator < 1_000_000_000_000; denominator = denominator * 3 + 1)
        {
            foreach (long candidate in new[] { denominator, denominator - denominator % 1_000_000, denominator - denominator % 100 + 1 })
            {
                string reading = sk.ConvertFraction(1, candidate);
                string cardinal = sk.Convert(candidate);
                if (reading.Contains("číslom", StringComparison.Ordinal))
                    Assert.AreEqual("jedna lomené číslom " + cardinal, reading, candidate.ToString());
                else if (candidate % 1_000_000 != 0 && reading.StartsWith("jedna lomené ", StringComparison.Ordinal))
                {
                    int lastSpace = cardinal.LastIndexOf(' ');
                    Assert.IsTrue(reading.StartsWith("jedna lomené " + cardinal[..(lastSpace + 1)], StringComparison.Ordinal), candidate + ": " + reading);
                }
                checkedCount++;
            }
        }
        Assert.IsTrue(checkedCount > 20);
    }

    /// <summary>The fraction nouns keep precedence and <c>lomené číslom</c> remains where the last component is indeclinable.</summary>
    [TestMethod]
    public void Slovak_NounsAndCislomKept()
    {
        Assert.AreEqual("jedna milióntina", Get("SK").ConvertFraction(1, 1_000_000));
        Assert.AreEqual("jedna miliardtina", Get("SK").ConvertFraction(1, 1_000_000_000));
        Assert.AreEqual("jedna lomené číslom dvesto", Get("SK").ConvertFraction(1, 200));
        Assert.AreEqual("jedna lomené číslom tisíc jeden", Get("SK").ConvertFraction(1, 1001));
    }

    /// <summary>
    /// The slash readings beyond 10^12, closed by NTS-41, follow the same rules since NTS-42 F (the scale nouns read from
    /// the converter); a CS multiplier ending in one after the tens stays closed there as below (NTS-42 B).
    /// </summary>
    [TestMethod]
    public void Large_OpenedByNts42F()
    {
        Assert.AreEqual("jedna lomeno jedním bilionem jednou", Get("CS").ConvertFraction(1, 1_000_000_000_001));
        Assert.AreEqual("jedna lomené číslom jeden bilión jeden", Get("SK").ConvertFraction(1, 1_000_000_000_001));
        Assert.AreEqual("jedna lomeno jedním kvintilionem třemi", Get("CS").ConvertFraction(BigInteger.One, BigInteger.Pow(10, 30) + 3));
        Assert.ThrowsExactly<NotSupportedException>(() => Get("CS").ConvertFraction(1, 21_000_000_000_000));
    }

    /// <summary>The fraction form of <c>Convert(Number)</c> uses the same readings.</summary>
    [TestMethod]
    public void NumberFractions()
    {
        Assert.AreEqual("jedna lomeno tisícem jednou", Get("CS").Convert(new Number(1, 1001)));
        Assert.AreEqual("jedna lomené dvoma miliónmi", Get("SK").Convert(new Number(1, 2_000_000)));
    }

    // ===== nominative regression =====================================================================

    /// <summary>The NTS-39 nominative readings are unchanged; Bulgarian has no NTS-40 item.</summary>
    [TestMethod]
    [DataRow("UK", 21L, 37L, "двадцять одна тридцять сьома")]
    [DataRow("BG", 21L, 37L, "двадесет и една тридесет и седми")]
    [DataRow("CS", 1L, 23L, "jedna lomeno dvaceti třemi")]
    [DataRow("CS", 2L, 365L, "dvě lomeno třemi sty šedesáti pěti")]
    [DataRow("SK", 1L, 142L, "jedna lomené sto štyridsiatimi dvoma")]
    [DataRow("SK", 2L, 365L, "dva lomené tristo šesťdesiatimi piatimi")]
    [DataRow("SK", 1L, 200L, "jedna lomené číslom dvesto")]
    [DataRow("SK", 1L, 1001L, "jedna lomené číslom tisíc jeden")]
    public void Nominative_Unchanged(string culture, long numerator, long denominator, string expected)
        => Assert.AreEqual(expected, Get(culture).ConvertFraction(numerator, denominator));

    /// <summary>Bulgarian has no NTS-40 item: its readings are untouched.</summary>
    [TestMethod]
    public void Bulgarian_Unchanged()
        => Assert.AreEqual("две трети", Get("BG").ConvertFraction(2, 3));
}
