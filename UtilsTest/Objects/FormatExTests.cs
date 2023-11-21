using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Objects;

namespace UtilsTest.Objects
{
    [TestClass]
    public class StringFormatTests
    {
        [TestMethod]
        public void StringFormatTest()
        {
            var format = StringFormat.Create<Func<int, int, string>>("ceci est {var1} test de {var2,3:X2} formatage", Expression.Parameter(typeof(int), "var1"), Expression.Parameter(typeof(int), "var2"));

            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Assert.AreEqual($"ceci est {i} test de {j,3:X2} formatage", format(i, j));
                }
            }
        }
    }
}
