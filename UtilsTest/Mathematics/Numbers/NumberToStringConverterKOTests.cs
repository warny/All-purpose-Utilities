using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterKOTests
    {
        [TestMethod]
        public void Cardinals_Basic()
        {
            var c = NumberToStringConverter.GetConverter("KO");
            (long n, string expected)[] cases =
            [
                (0,   "영"),
                (1,   "일"),
                (2,   "이"),
                (3,   "삼"),
                (9,   "구"),
                (10,  "십"),
                (11,  "십일"),
                (12,  "십이"),
                (19,  "십구"),
                (20,  "이십"),
                (21,  "이십일"),
                (100, "백"),
                (101, "백일"),
                (200, "이백"),
            ];
            foreach (var (n, expected) in cases)
                Assert.AreEqual(expected, c.Convert(n), $"KO {n}");
        }

        [TestMethod]
        public void Cardinals_Thousands()
        {
            var c = NumberToStringConverter.GetConverter("KO");
            // Korean elides the multiplier "일" before "천": 1 000 = "천", not "일 천"
            Assert.AreEqual("천",       c.Convert(1_000));
            Assert.AreEqual("이 천",    c.Convert(2_000));
            // 10 000 uses the "만" scale suffix path, unaffected by the onValue=1 replacement above
            Assert.AreEqual("십 천",    c.Convert(10_000));
        }

        [TestMethod]
        public void Ordinals_Prefix()
        {
            var c = NumberToStringConverter.GetConverter("KO");
            Assert.IsTrue(c.SupportsOrdinals);
            // KO ordinals prepend prefix "제" to the cardinal
            Assert.AreEqual("제일",  c.ConvertOrdinal(1));
            Assert.AreEqual("제이",  c.ConvertOrdinal(2));
            Assert.AreEqual("제십",  c.ConvertOrdinal(10));
            Assert.AreEqual("제십일", c.ConvertOrdinal(11));
        }

        [TestMethod]
        public void Negative()
        {
            var c = NumberToStringConverter.GetConverter("KO");
            Assert.AreEqual("마이너스 일", c.Convert(-1L));
        }

        [TestMethod]
        public void KO_RegisteredUnderKO()
        {
            Assert.IsNotNull(NumberToStringConverter.GetConverter("KO"));
        }
    }
}
