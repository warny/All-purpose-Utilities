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

    [TestMethod]
    public void RandomFloat_CanProduceValuesOutsideZeroOneRange()
    {
        // RandomFloat fills the full IEEE-754 bit pattern, unlike Random.NextSingle()'s [0, 1) range.
        var rng = new Random(12345);
        bool foundOutsideRange = false;
        for (int i = 0; i < 1000 && !foundOutsideRange; i++)
        {
            float value = rng.RandomFloat();
            if (float.IsNaN(value) || value < 0f || value >= 1f)
                foundOutsideRange = true;
        }
        Assert.IsTrue(foundOutsideRange, "RandomFloat should be able to produce values outside [0, 1) (including NaN/Infinity).");
    }

    [TestMethod]
    public void RandomDouble_CanProduceValuesOutsideZeroOneRange()
    {
        // RandomDouble fills the full IEEE-754 bit pattern, unlike Random.NextDouble()'s [0, 1) range.
        var rng = new Random(12345);
        bool foundOutsideRange = false;
        for (int i = 0; i < 1000 && !foundOutsideRange; i++)
        {
            double value = rng.RandomDouble();
            if (double.IsNaN(value) || value < 0d || value >= 1d)
                foundOutsideRange = true;
        }
        Assert.IsTrue(foundOutsideRange, "RandomDouble should be able to produce values outside [0, 1) (including NaN/Infinity).");
    }

    [TestMethod]
    public void RandomFloat_CanProduceNaNOrInfinity()
    {
        var rng = new Random(12345);
        bool foundNonFinite = false;
        for (int i = 0; i < 100_000 && !foundNonFinite; i++)
        {
            float value = rng.RandomFloat();
            if (!float.IsFinite(value))
                foundNonFinite = true;
        }
        Assert.IsTrue(foundNonFinite, "RandomFloat should be able to produce NaN or Infinity values.");
    }

    [TestMethod]
    public void RandomDouble_CanProduceNaNOrInfinity()
    {
        var rng = new Random(12345);
        bool foundNonFinite = false;
        for (int i = 0; i < 100_000 && !foundNonFinite; i++)
        {
            double value = rng.RandomDouble();
            if (!double.IsFinite(value))
                foundNonFinite = true;
        }
        Assert.IsTrue(foundNonFinite, "RandomDouble should be able to produce NaN or Infinity values.");
    }
}
