using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers
{
    [TestClass]
    public class NumberToStringConverterITTests
    {

        [TestMethod]
        public void ConvertCurrency_IT_Euro()
        {
            var c = NumberToStringConverter.GetConverter("IT");
            var euro = new CurrencyDefinition
            {
                UnitSingular = "euro",
                UnitPlural = "euro",
                SubunitSingular = "centesimo",
                SubunitPlural = "centesimi",
                Connector = "e",
            };

            Assert.AreEqual("uno euro", c.ConvertCurrency(1m, euro));
            Assert.AreEqual("due euro", c.ConvertCurrency(2m, euro));
            Assert.AreEqual("uno euro e cinquanta centesimi", c.ConvertCurrency(1.50m, euro));
        }
    }
}
