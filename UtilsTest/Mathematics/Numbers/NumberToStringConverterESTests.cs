using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterESTests
    {
        [TestMethod]
        public void DecimalTest()
        {
            (decimal Number, string Expected)[] tests = [
                (1.5m, "uno coma cinco"),
                (12.34m, "doce coma tres cuatro"),
            ];

            var converter = NumberToStringConverter.GetConverter("ES");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void CastilianCultureAliases()
        {
            var reference = NumberToStringConverter.GetConverter("ES");

            (string Culture, decimal Value, string Expected)[] tests = [
                ("es-ES", 21.4m, "veintiuno coma cuatro"),
                ("ES-es", 1000m, "mil"),
            ];

            foreach (var test in tests)
            {
                var converter = NumberToStringConverter.GetConverter(test.Culture);
                Assert.AreEqual(reference.Convert(test.Value), converter.Convert(test.Value));
                Assert.AreEqual(test.Expected, converter.Convert(test.Value));
            }
        }
    }
}
