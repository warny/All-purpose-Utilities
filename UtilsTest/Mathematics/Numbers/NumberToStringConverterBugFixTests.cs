using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterBugFixTests
    {
        // ── FinalizeWriting applied to negative decimals and rationals ────────

        // ── ValidateVariantReferences extended ────────────────────────────────

        private static NumberToStringConverterOptions BaseOptions() => new()
        {
            Group = 3,
            Separator = " ",
            GroupSeparator = "",
            Zero = "zero",
            Minus = "minus *",
            Groups = NumberToStringConverter.GetConverter("EN").Groups
                .ToDictionary(kv => kv.Key, kv => new DigitListType { Digits = kv.Value.Values.ToList() }),
            Scale = NumberToStringConverter.GetConverter("EN").Scale,
        };

        [TestMethod]
        public void ValidateVariantReferences_Throws_OnUnknownDimensionInVariantRule()
        {
            var options = BaseOptions();
            options.VariantDimensions =
            [
                new NumberToStringConverter.VariantDimension("gender", ["masc", "fem"], null)
            ];
            // Rule references an unknown dimension name "typo"
            options.VariantRules =
            [
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["typo"] = "fem" },
                    [new NumberToStringConverter.ReplacementRule("un", "une", ReplacementScope.LastWord)])
            ];
            Assert.ThrowsExactly<InvalidOperationException>(() => new NumberToStringConverter(options),
                "Should throw when a VariantRule references an unknown dimension");
        }

        [TestMethod]
        public void ValidateVariantReferences_Throws_OnConstraintsWithNoDimensionsDeclared()
        {
            // No VariantDimensions at all; a VariantRule with a constraint key is still invalid
            var options = BaseOptions();
            options.VariantRules =
            [
                new NumberToStringConverter.VariantRule(
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["gender"] = "fem" },
                    [new NumberToStringConverter.ReplacementRule("un", "une", ReplacementScope.LastWord)])
            ];
            Assert.ThrowsExactly<InvalidOperationException>(() => new NumberToStringConverter(options),
                "Should throw even when no VariantDimensions are declared but constraints reference one");
        }

    }
}
