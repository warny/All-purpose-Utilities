using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

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

        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("AR");

            Assert.AreEqual("صفر",    c.Convert(0));
            Assert.AreEqual("واحد",   c.Convert(1));
            Assert.AreEqual("اثنان",  c.Convert(2));
            Assert.AreEqual("ثلاثة",  c.Convert(3));
            Assert.AreEqual("عشرة",   c.Convert(10));
            Assert.AreEqual("عشرون",  c.Convert(20));
            Assert.AreEqual("مائة",   c.Convert(100));
            Assert.AreEqual("ألف",    c.Convert(1_000));
        }

        [TestMethod]
        public void Cardinals_Gender_Muannath()
        {
            var c = NumberToStringConverter.GetConverter("AR");

            Assert.AreEqual("واحدة",  c.Convert(1,  "gender=muʾannath"), "1f");
            Assert.AreEqual("اثنتان", c.Convert(2,  "gender=muʾannath"), "2f");
            Assert.AreEqual("ثلاث",   c.Convert(3,  "gender=muʾannath"), "3f");
            Assert.AreEqual("أربع",   c.Convert(4,  "gender=muʾannath"), "4f");
            Assert.AreEqual("خمس",    c.Convert(5,  "gender=muʾannath"), "5f");
            Assert.AreEqual("ست",     c.Convert(6,  "gender=muʾannath"), "6f");
            Assert.AreEqual("سبع",    c.Convert(7,  "gender=muʾannath"), "7f");
            Assert.AreEqual("ثمان",   c.Convert(8,  "gender=muʾannath"), "8f");
            Assert.AreEqual("تسع",    c.Convert(9,  "gender=muʾannath"), "9f");
            Assert.AreEqual("عشر",    c.Convert(10, "gender=muʾannath"), "10f");
        }

        [TestMethod]
        public void Ordinals_Masculine()
        {
            var c = NumberToStringConverter.GetConverter("AR");

            Assert.AreEqual("أول",       c.ConvertOrdinal(1));
            Assert.AreEqual("ثانٍ",      c.ConvertOrdinal(2));
            Assert.AreEqual("ثالث",      c.ConvertOrdinal(3));
            Assert.AreEqual("عاشر",      c.ConvertOrdinal(10));
            Assert.AreEqual("حادي عشر",  c.ConvertOrdinal(11));
            Assert.AreEqual("ثاني عشر",  c.ConvertOrdinal(12));
            Assert.AreEqual("تاسع عشر",  c.ConvertOrdinal(19));
        }

        [TestMethod]
        public void Ordinals_Feminine()
        {
            var c = NumberToStringConverter.GetConverter("AR");

            Assert.AreEqual("أولى",        c.ConvertOrdinal(1,  "gender=muʾannath"));
            Assert.AreEqual("ثانية",       c.ConvertOrdinal(2,  "gender=muʾannath"));
            Assert.AreEqual("ثالثة",       c.ConvertOrdinal(3,  "gender=muʾannath"));
            Assert.AreEqual("عاشرة",       c.ConvertOrdinal(10, "gender=muʾannath"));
            Assert.AreEqual("حادية عشرة",  c.ConvertOrdinal(11, "gender=muʾannath"));
            Assert.AreEqual("تاسعة عشرة",  c.ConvertOrdinal(19, "gender=muʾannath"));
        }
    }
}
