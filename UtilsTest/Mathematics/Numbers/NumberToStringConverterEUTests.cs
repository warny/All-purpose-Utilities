using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterEUTests
    {
        [TestMethod]
        public void BasqueTensAdjustments()
        {
            (int Number, string Expected)[] tests = [
                (21, "hogeita bat"),
                (31, "hogeita hamaika"),
                (38, "hogeita hemezortzi"),
                (57, "berrogeita hamazazpi"),
            ];

            var converter = NumberToStringConverter.GetConverter("eu-ES");
            Assert.AreNotEqual(0, converter.Replacements.Count, "Basque configuration must expose replacements");

            foreach (var test in tests)
            {
                var actual = converter.Convert(test.Number);
                Assert.AreEqual(test.Expected, actual);
            }
        }

        [TestMethod]
        public void BasqueHundreds()
        {
            var converter = NumberToStringConverter.GetConverter("EU");
            Assert.AreEqual("ehun", converter.Convert(100));
            Assert.AreEqual("berrehun eta bost", converter.Convert(205));
        }

        [TestMethod]
        public void BasqueDecimal()
        {
            var converter = NumberToStringConverter.GetConverter("EU-es");
            Assert.AreEqual("bat koma bost", converter.Convert(1.5m));
        }

        [TestMethod]
        public void BasqueThousandsStillApplyTargetedReplacements()
        {
            var converter = NumberToStringConverter.GetConverter("EU");

            Assert.AreEqual("mila", converter.Convert(1000));
            Assert.AreEqual("hogei mila", converter.Convert(20000));
            Assert.AreEqual("hogeita hamar mila", converter.Convert(30000));
        }
    }
}
