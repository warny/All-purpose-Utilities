using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Utils.Randomization;

namespace UtilsTest.Randomization;

[TestClass]
public class RandomExtensionsTests
{
    [TestMethod]
    public void RandomString_AllCharactersReachable()
    {
        // Regression for off-by-one: the last character of the alphabet must be reachable.
        var rng = new Random(42);
        char[] alphabet = ['A', 'B', 'C'];
        var seen = new HashSet<char>();

        for (int i = 0; i < 300; i++)
            foreach (char c in rng.RandomString(10, alphabet))
                seen.Add(c);

        Assert.IsTrue(seen.Contains('A'), "first char reachable");
        Assert.IsTrue(seen.Contains('B'), "middle char reachable");
        Assert.IsTrue(seen.Contains('C'), "last char reachable (was excluded by off-by-one)");
    }

    [TestMethod]
    public void RandomString_NullCharArray_Throws()
    {
        var rng = new Random();
        Assert.ThrowsException<ArgumentNullException>(() => rng.RandomString(5, (char[])null));
    }

    [TestMethod]
    public void RandomString_NullCharArrayMinMax_Throws()
    {
        var rng = new Random();
        Assert.ThrowsException<ArgumentNullException>(() => rng.RandomString(3, 8, (char[])null));
    }

    [TestMethod]
    public void RandomString_FixedLength_ReturnsCorrectLength()
    {
        var rng = new Random(1);
        string result = rng.RandomString(7);
        Assert.AreEqual(7, result.Length);
    }
}
