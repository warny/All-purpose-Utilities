using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.NumberToString;

namespace UtilsTest.Mathematics.Numbers;

/// <summary>Tests the inspectable number-scale support contract.</summary>
[TestClass]
public class NumberScaleContractTests
{
    /// <summary>Verifies that a static-only scale reports its exact finite range.</summary>
    [TestMethod]
    public void StaticScale_ReportsFiniteRange()
    {
        var scale = new NumberScale(["", "thousand"], []);

        Assert.IsFalse(scale.IsUnbounded);
        Assert.AreEqual(1, scale.MaximumSupportedGroupIndex);
        Assert.IsTrue(scale.CanNameGroup(1));
        Assert.IsFalse(scale.CanNameGroup(2));
    }

    /// <summary>Verifies that complete dynamic prefix tables report an unbounded scale.</summary>
    [TestMethod]
    public void CompleteDynamicScale_IsUnbounded()
    {
        string[] prefixes = ["", "a", "b", "c", "d", "e", "f", "g", "h", "i"];
        var scale = new NumberScale(
            [""], ["illion"], scale0Prefixes: prefixes,
            unitsPrefixes: prefixes, tensPrefixes: prefixes, hundredsPrefixes: prefixes);

        Assert.IsTrue(scale.IsUnbounded);
        Assert.IsNull(scale.MaximumSupportedGroupIndex);
        Assert.IsTrue(scale.CanNameGroup(500));
    }
}
