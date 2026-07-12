using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Objects;

namespace UtilsTest.Objects;

[TestClass]
public class NumberUtilsTests
{
    [TestMethod]
    [DataRow(typeof(int), typeof(double), true)]
    [DataRow(typeof(int), typeof(long), true)]
    [DataRow(typeof(float), typeof(double), true)]
    [DataRow(typeof(decimal), typeof(double), true)]
    [DataRow(typeof(decimal), typeof(float), true)]
    [DataRow(typeof(long), typeof(short), false)]
    [DataRow(typeof(double), typeof(float), false)]
    [DataRow(typeof(double), typeof(int), false)]
    [DataRow(typeof(float), typeof(int), false)]
    [DataRow(typeof(double), typeof(decimal), false)]
    [DataRow(typeof(double), typeof(double), true)]
    public void IsWideningNumericConversion_MatchesExpectedDirection(System.Type from, System.Type to, bool expected)
    {
        Assert.AreEqual(expected, NumberUtils.IsWideningNumericConversion(from, to));
    }
}
