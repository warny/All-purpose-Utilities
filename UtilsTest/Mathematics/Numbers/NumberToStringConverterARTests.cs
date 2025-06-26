using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Mathematics;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterARTests
    {
        [TestMethod]
        public void DecimalTest()
        {
            (decimal Number, string Expected)[] tests = [
                (1.5m, "واحد فاصل خمسة"),
                (12.34m, "عشرة اثنان فاصل ثلاثة أربعة"),
            ];

            var converter = NumberToStringConverter.GetConverter("AR");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }
    }
}
