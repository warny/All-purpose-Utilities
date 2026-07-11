using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.Randomization;

namespace UtilsTest.Randomization;

[TestClass]
public class DistributedRandomTests
{
    [TestMethod]
    public void NextDoubleWithRangeReturnsContinuousValuesTest()
    {
        var random = new DistributedRandom(x => x, seed: 12345);
        bool foundFractional = false;
        for (int i = 0; i < 20; i++)
        {
            double value = random.NextDouble(0.0, 100.0);
            Assert.IsTrue(value >= 0.0 && value < 100.0);
            if (value != Math.Floor(value))
            {
                foundFractional = true;
            }
        }
        Assert.IsTrue(foundFractional, "NextDouble(min, max) must be able to return non-integer values.");
    }

    [TestMethod]
    public void NextDoubleWithIdentityDistributionMatchesUnderlyingUniformRandomTest()
    {
        double expectedUniform = new Random(42).NextDouble();
        double expected = expectedUniform * (10.0 - 5.0) + 5.0;

        var distributed = new DistributedRandom(x => x, seed: 42);
        double actual = distributed.NextDouble(5.0, 10.0);

        Assert.AreEqual(expected, actual, 1e-12);
    }

    [TestMethod]
    public void NextIntStaysWithinRangeAndTruncatesTest()
    {
        var distributed = new DistributedRandom(x => x, seed: 7);
        for (int i = 0; i < 20; i++)
        {
            int value = distributed.NextInt(0, 100);
            Assert.IsTrue(value >= 0 && value < 100);
        }
    }
}
