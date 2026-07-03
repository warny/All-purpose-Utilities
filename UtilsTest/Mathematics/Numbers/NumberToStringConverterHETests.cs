using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterHETests
    {
        [TestMethod]
        public void DecimalTest()
        {
            (decimal Number, string Expected)[] tests = [
                (1.5m, "אחד נקודה חמש"),
                (12.34m, "עשר שתיים נקודה שלוש ארבע"),
            ];

            var converter = NumberToStringConverter.GetConverter("HE");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("HE");
            (long n, string expected)[] cases =
            [
                (1,   "אחד"),
                (2,   "שתיים"),
                (3,   "שלוש"),
                (10,  "עשר"),
                (20,  "עשרים"),
                (100, "מאה"),
                (200, "מאתיים"),
                // known limitation: engine prepends the unit digit (see comment in HE XML)
                (1_000, "אחד אלף"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"HE {n}");
        }

        [TestMethod]
        public void Cardinals_GenderVariants()
        {
            var c = NumberToStringConverter.GetConverter("HE");

            // zachar (masculine noun context): some units gain ה
            Assert.AreEqual("שלושה", c.Convert(3, "gender=zachar"),  "3 zachar");
            Assert.AreEqual("ארבעה", c.Convert(4, "gender=zachar"),  "4 zachar");
            Assert.AreEqual("חמישה", c.Convert(5, "gender=zachar"),  "5 zachar");
            Assert.AreEqual("שישה",  c.Convert(6, "gender=zachar"),  "6 zachar");
            Assert.AreEqual("עשרה",  c.Convert(10, "gender=zachar"), "10 zachar");

            // nekeva (feminine noun context): 1 becomes אחת
            Assert.AreEqual("אחת",   c.Convert(1, "gender=nekeva"),  "1 nekeva");
        }

        [TestMethod]
        public void Ordinals_Exceptions()
        {
            var c = NumberToStringConverter.GetConverter("HE");

            Assert.AreEqual("ראשון",   c.ConvertOrdinal(1));
            Assert.AreEqual("שני",     c.ConvertOrdinal(2));
            Assert.AreEqual("שלישי",   c.ConvertOrdinal(3));
            Assert.AreEqual("עשירי",   c.ConvertOrdinal(10));
            Assert.AreEqual("ראשונה",  c.ConvertOrdinal(1, "gender=nekeva"));
            Assert.AreEqual("שנייה",   c.ConvertOrdinal(2, "gender=nekeva"));
            Assert.AreEqual("שלישית",  c.ConvertOrdinal(3, "gender=nekeva"));
        }
    }
}
