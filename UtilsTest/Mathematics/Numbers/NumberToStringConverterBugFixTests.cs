using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterBugFixTests
    {
        // ── FinalizeWriting applied to negative decimals and rationals ────────

        [TestMethod]
        public void NegativeDecimal_FinalizeWritingApplied_DE()
        {
            var c = NumberToStringConverter.GetConverter("DE");
            // GermanNumberToStringLanguageSpecifics turns "ein" → "eins" for standalone
            // and "ein Million" → "eine Million". For -1.5 DE the integer part is 1
            // ("eins" in standalone); full text becomes "minus eins Komma fünf".
            // Before the fix, FinalizeWriting was skipped for negatives → "minus ein Komma fünf".
            string result = c.Convert(-1.5m);
            StringAssert.StartsWith(result, "minus ");
            // The absolute-value part must pass through FinalizeWriting.
            Assert.IsFalse(result.Contains("ein Komma"), "FinalizeWriting must run on negative decimal absolute value");
        }

        [TestMethod]
        public void NegativeDecimal_SymmetricWithPositive_FR()
        {
            var c = NumberToStringConverter.GetConverter("FR");
            string pos = c.Convert(3.5m);
            string neg = c.Convert(-3.5m);
            Assert.AreEqual("moins " + pos, neg, "Negative decimal should equal 'minus ' + positive form");
        }

        [TestMethod]
        public void NegativeRational_FinalizeWritingApplied_DE()
        {
            var c = NumberToStringConverter.GetConverter("DE");
            var pos = c.Convert(new Utils.Numerics.Number(3, 4));
            var neg = c.Convert(new Utils.Numerics.Number(-3, 4));
            Assert.AreEqual("minus " + pos, neg, "Negative rational should equal 'minus ' + positive form");
        }

        [TestMethod]
        public void NegativeDecimal_SymmetricWithPositive_EN()
        {
            var c = NumberToStringConverter.GetConverter("EN");
            string pos = c.Convert(12.5m);
            string neg = c.Convert(-12.5m);
            Assert.AreEqual("minus " + pos, neg);
        }

        // ── RO scaleConnector ─────────────────────────────────────────────────

        [TestMethod]
        public void RO_ScaleConnector_De_InsertedAboveThreshold()
        {
            var c = NumberToStringConverter.GetConverter("RO");

            // Below threshold (< 20): no "de"
            Assert.AreEqual("doisprezece mii",       c.Convert(12_000));
            Assert.AreEqual("nouăsprezece mii",      c.Convert(19_000));

            // At threshold (≥ 20): connector "de" appears
            Assert.AreEqual("douăzeci de mii",       c.Convert(20_000));
            Assert.AreEqual("o sută de mii",         c.Convert(100_000));
            Assert.AreEqual("un milion",              c.Convert(1_000_000));   // < 20 × scale
            Assert.AreEqual("douăzeci de milioane",  c.Convert(20_000_000));
        }

        [TestMethod]
        public void RO_ScaleConnector_NoConnector_BelowThreshold()
        {
            var c = NumberToStringConverter.GetConverter("RO");
            // These must NOT contain " de " between multiplier and scale
            string s12k = c.Convert(12_000);
            Assert.IsFalse(s12k.Contains(" de "), $"12 000 should not have connector, got: {s12k}");
        }

        // ── ValidateVariantReferences extended ────────────────────────────────

        [TestMethod]
        public void ValidateVariantReferences_Throws_OnUnknownDimensionInVariantRule()
        {
            var options = new NumberToStringConverterOptions
            {
                Group = 3,
                Separator = " ",
                GroupSeparator = "",
                Zero = "zero",
                Minus = "minus *",
                Groups = NumberToStringConverter.GetConverter("EN").Groups
                    .ToDictionary(kv => kv.Key, kv => new DigitListType { Digits = kv.Value.Values.ToList() }),
                Scale = NumberToStringConverter.GetConverter("EN").Scale,
                VariantDimensions =
                [
                    new NumberToStringConverter.VariantDimension("gender", ["masc", "fem"], null)
                ],
                // Rule references an unknown dimension name "typo"
                VariantRules =
                [
                    new NumberToStringConverter.VariantRule(
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["typo"] = "fem" },
                        [new NumberToStringConverter.ReplacementRule("un", "une", ReplacementScope.LastWord)])
                ],
            };
            Assert.ThrowsException<InvalidOperationException>(() => new NumberToStringConverter(options),
                "Should throw when a VariantRule references an unknown dimension");
        }

        // ── ResolveLanguageSpecifics throws on unknown named type ─────────────

        [TestMethod]
        public void ResolveLanguageSpecifics_ReflectionFallback_StillWorksForKnownType()
        {
            // PL config references PolishOrdinalLanguageSpecifics via its short name.
            // The type is in the assembly so reflection should find it without explicit registration.
            var c = NumberToStringConverter.GetConverter("PL");
            Assert.IsTrue(c.SupportsOrdinals, "PL converter should support ordinals via reflected specifics");
        }
    }
}
