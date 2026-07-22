using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Utils.Geography.Display;

namespace UtilsTest.Geography;

[TestClass]
public class CoordinatesUtilTests
{
    [TestMethod]
    public void DegreesToMicrodegreesConvertsUsingMillionFactor()
    {
        Assert.AreEqual(48_856_600, CoordinatesUtil<double>.DegreesToMicrodegrees(48.8566));
    }

    [TestMethod]
    public void MicrodegreesToDegreesIsInverseOfDegreesToMicrodegrees()
    {
        int microdegrees = CoordinatesUtil<double>.DegreesToMicrodegrees(2.3522);
        double degrees = CoordinatesUtil<double>.MicrodegreesToDegrees(microdegrees);

        Assert.AreEqual(2.3522, degrees, 1e-6);
    }

    [TestMethod]
    public void ParseCoordinatestringReturnsExpectedValuesInOrder()
    {
        double[] values = CoordinatesUtil<double>.ParseCoordinatestring("1,2,3,4", 4);

        CollectionAssert.AreEqual(new[] { 1.0, 2.0, 3.0, 4.0 }, values);
    }

    [TestMethod]
    public void ParseCoordinatestringThrowsWhenCountDoesNotMatch()
    {
        Assert.ThrowsException<ArgumentException>(() => CoordinatesUtil<double>.ParseCoordinatestring("1,2,3", 4));
    }

    [TestMethod]
    public void ValidateLatitudeAcceptsBoundaryValues()
    {
        CoordinatesUtil<double>.ValidateLatitude(90);
        CoordinatesUtil<double>.ValidateLatitude(-90);
        CoordinatesUtil<double>.ValidateLatitude(0);
    }

    [TestMethod]
    public void ValidateLatitudeRejectsOutOfRangeValues()
    {
        Assert.ThrowsException<ArgumentException>(() => CoordinatesUtil<double>.ValidateLatitude(90.1));
        Assert.ThrowsException<ArgumentException>(() => CoordinatesUtil<double>.ValidateLatitude(-90.1));
    }

    [TestMethod]
    public void ValidateLatitudeRejectsNaN()
    {
        Assert.ThrowsException<ArgumentException>(() => CoordinatesUtil<double>.ValidateLatitude(double.NaN));
    }

    [TestMethod]
    public void ParseCoordinatestringRejectsMiddleEmptyToken()
    {
        // Before the fix, RemoveEmptyEntries caused "1,,2,3,4" to become 4 tokens ["1","2","3","4"],
        // which was accepted when 4 coordinates were expected, silently shifting all values after
        // the empty position. StringSplitOptions.None preserves empty tokens so the count mismatch
        // (or explicit empty-token check) causes a proper rejection.
        Assert.ThrowsException<ArgumentException>(
            () => CoordinatesUtil<double>.ParseCoordinatestring("1,,2,3,4", 4));
    }

    [TestMethod]
    public void ParseCoordinatestringRejectsLeadingEmptyToken()
    {
        Assert.ThrowsException<ArgumentException>(
            () => CoordinatesUtil<double>.ParseCoordinatestring(",1,2,3", 4));
    }

    [TestMethod]
    public void ParseCoordinatestringRejectsTrailingEmptyToken()
    {
        Assert.ThrowsException<ArgumentException>(
            () => CoordinatesUtil<double>.ParseCoordinatestring("1,2,3,", 4));
    }

    [TestMethod]
    public void ParseCoordinatestringRejectsWhitespaceOnlyToken()
    {
        Assert.ThrowsException<ArgumentException>(
            () => CoordinatesUtil<double>.ParseCoordinatestring("1, ,3,4", 4));
    }
}
