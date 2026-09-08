using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterVNTests
    {
        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("VN");
            (long n, string expected)[] cases =
            [
                (0,  "không"),
                (1,  "một"),
                (2,  "hai"),
                (9,  "chín"),
                (10, "mười"),
                (11, "mười một"),
                (15, "mười lăm"),   // "mười năm" → "mười lăm" allomorph
                (19, "mười chín"),
                (20, "hai mươi"),
                (21, "hai mươi mốt"),  // "mươi một" → "mươi mốt" allomorph
                (25, "hai mươi lăm"),  // "mươi năm" → "mươi lăm" allomorph
                (100, "một trăm"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"VN {n}");
        }

        [TestMethod]
        public void Cardinals_Thousands()
        {
            var c = NumberToStringConverter.GetConverter("VN");
            // "một nghìn" is elided to "nghìn"
            Assert.AreEqual("nghìn", c.Convert(1_000));
            Assert.AreEqual("hai nghìn", c.Convert(2_000));
        }

        [TestMethod]
        public void Cardinals_MillionsAndBillions()
        {
            var c = NumberToStringConverter.GetConverter("VN");
            // "một triệu"/"một tỷ" are correct Vietnamese; no elision at these scales
            Assert.AreEqual("một triệu", c.Convert(1_000_000));
            Assert.AreEqual("hai triệu", c.Convert(2_000_000));
            Assert.AreEqual("một tỷ", c.Convert(1_000_000_000));
            Assert.AreEqual(
                "chín trăm chín mươi chín tỷ chín trăm chín mươi chín triệu chín trăm chín mươi chín nghìn chín trăm chín mươi chín",
                c.Convert(999_999_999_999L));
        }

        [TestMethod]
        public void Cardinals_AboveMaximum_Throws()
        {
            var c = NumberToStringConverter.GetConverter("VN");
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => c.Convert(1_000_000_000_000L));
        }

        [TestMethod]
        public void Ordinals_PrefixAndException()
        {
            var c = NumberToStringConverter.GetConverter("VN");
            Assert.IsTrue(c.SupportsOrdinals);
            Assert.AreEqual("thứ nhất", c.ConvertOrdinal(1));  // exception, not "thứ một"
            Assert.AreEqual("thứ hai", c.ConvertOrdinal(2));
            Assert.AreEqual("thứ mười", c.ConvertOrdinal(10));
        }

        [TestMethod]
        public void VN_RegisteredUnderVNAndVIAndVIVN()
        {
            Assert.IsNotNull(NumberToStringConverter.GetConverter("VN"));
            Assert.IsNotNull(NumberToStringConverter.GetConverter("VI"));
            Assert.IsNotNull(NumberToStringConverter.GetConverter("VI-VN"));
            Assert.AreEqual(
                NumberToStringConverter.GetConverter("VN").Convert(21),
                NumberToStringConverter.GetConverter("VI-VN").Convert(21));
        }
    }
}
