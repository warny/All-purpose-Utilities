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
}
