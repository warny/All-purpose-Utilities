using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using Utils.Dates;

namespace UtilsTest.Objects;

[TestClass]
public class DateFormulaTests
{
        [TestMethod]
        public void BasicFrenchFormulas()
        {
                var date = new DateTime(2023, 3, 15);
                Assert.AreEqual(new DateTime(2023, 4, 1), date.Calculate("FM+1J", new CultureInfo("fr-FR")));
                Assert.AreEqual(new DateTime(2022, 12, 1), date.Calculate("DA-1M", new CultureInfo("fr-FR")));
        }

        [TestMethod]
        public void WeekDayAdjustments()
        {
                var date = new DateTime(2023, 10, 15);
                Assert.AreEqual(new DateTime(2023, 11, 6), date.Calculate("FM+1J+Lu", new CultureInfo("fr-FR")));
                Assert.AreEqual(new DateTime(2023, 10, 30), date.Calculate("FM+1JLu", new CultureInfo("fr-FR")));
        }

        [TestMethod]
        public void EnglishFormula()
        {
                var date = new DateTime(2023, 3, 15);
                Assert.AreEqual(new DateTime(2023, 4, 1), date.Calculate("EM+1D", new CultureInfo("en-US")));
        }

        [TestMethod]
        public void GermanFormula()
        {
                var date = new DateTime(2023, 3, 15);
                Assert.AreEqual(new DateTime(2023, 4, 1), date.Calculate("EM+1T", new CultureInfo("de-DE")));
        }

        [TestMethod]
        public void ArabicFormulaHijri()
        {
                var culture = new CultureInfo("ar-SA");
                var date = new DateTime(2023, 3, 15);
                Assert.AreEqual(new DateTime(2023, 3, 23), date.Calculate("NS+1Y", culture));
        }

        [TestMethod]
        public void ChineseFormula()
        {
                var date = new DateTime(2023, 3, 15);
                Assert.AreEqual(new DateTime(2023, 4, 1), date.Calculate("EM+1D", new CultureInfo("zh-CN")));
        }
}
