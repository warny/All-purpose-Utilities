using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterELTests
    {
        [TestMethod]
        public void DecimalTest()
        {
            (decimal Number, string Expected)[] tests = [
                (1.5m, "ένα κόμμα πέντε"),
                (12.34m, "δώδεκα κόμμα τρία τέσσερα"),
            ];

            var converter = NumberToStringConverter.GetConverter("EL");

            foreach (var test in tests)
            {
                Assert.AreEqual(test.Expected, converter.Convert(test.Number));
            }
        }

        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("EL");
            (long n, string expected)[] cases =
            [
                (1,   "ένα"),
                (3,   "τρία"),
                (10,  "δέκα"),
                (11,  "έντεκα"),
                (20,  "είκοσι"),
                (100, "εκατό"),
                (1_000, "χίλια"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"EL {n}");
        }

        [TestMethod]
        public void Ordinals_GenderForms()
        {
            var c = NumberToStringConverter.GetConverter("EL");

            Assert.AreEqual("πρώτος", c.ConvertOrdinal(1),                          "1 masc");
            Assert.AreEqual("πρώτη",  c.ConvertOrdinal(1, "gender=θηλυκό"),         "1 fem");
            Assert.AreEqual("πρώτο",  c.ConvertOrdinal(1, "gender=ουδέτερο"),       "1 neut");
            Assert.AreEqual("δεύτερος", c.ConvertOrdinal(2),                        "2 masc");
            Assert.AreEqual("δεύτερη",  c.ConvertOrdinal(2, "gender=θηλυκό"),       "2 fem");
            Assert.AreEqual("δέκατος",  c.ConvertOrdinal(10),                       "10 masc");
            Assert.AreEqual("δέκατη",   c.ConvertOrdinal(10, "gender=θηλυκό"),      "10 fem");
            Assert.AreEqual("ενδέκατος", c.ConvertOrdinal(11),                      "11 masc");
            Assert.AreEqual("ενδέκατη",  c.ConvertOrdinal(11, "gender=θηλυκό"),     "11 fem");
            Assert.AreEqual("ενδέκατο",  c.ConvertOrdinal(11, "gender=ουδέτερο"),   "11 neut");
        }
    }
}
