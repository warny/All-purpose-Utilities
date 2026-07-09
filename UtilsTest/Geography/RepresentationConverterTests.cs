using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Geography.Display;
using Utils.Geography.Model;
using Utils.Geography.Projections;

namespace UtilsTest.Geography;

[TestClass]
public class RepresentationConverterTests
{
    [TestMethod]
    public void GetMapSizeGrowsWithZoomLevel()
    {
        var converter = new RepresentationConverter<double>(Projections<double>.Equirectangular, tileSize: 256);

        Assert.AreEqual(256, converter.GetMapSize(0));
        Assert.AreEqual(512, converter.GetMapSize(1));
        Assert.AreEqual(1024, converter.GetMapSize(2));
    }

    [TestMethod]
    public void ComputeGroundResolutionIsSmallerAtHigherZoom()
    {
        var converter = new RepresentationConverter<double>(Projections<double>.Equirectangular);

        double resolutionZoom0 = converter.ComputeGroundResolution(0, 0);
        double resolutionZoom5 = converter.ComputeGroundResolution(0, 5);

        Assert.IsTrue(resolutionZoom5 < resolutionZoom0);
    }

    [TestMethod]
    public void ComputeGroundResolutionIsSmallerNearThePoles()
    {
        var converter = new RepresentationConverter<double>(Projections<double>.Equirectangular);

        double resolutionAtEquator = converter.ComputeGroundResolution(0, 4);
        double resolutionAt60 = converter.ComputeGroundResolution(60, 4);

        Assert.IsTrue(resolutionAt60 < resolutionAtEquator);
    }

    [TestMethod]
    public void GeoPointToMappointDelegatesToProjection()
    {
        var projection = Projections<double>.Equirectangular;
        var converter = new RepresentationConverter<double>(projection);
        var geoPoint = new GeoPoint<double>(12, 34);

        var expected = projection.GeoPointToMapPoint(geoPoint);
        var actual = converter.GeoPointToMappoint(geoPoint, zoomFactor: 3);

        Assert.AreEqual(expected.X, actual.X);
        Assert.AreEqual(expected.Y, actual.Y);
    }

    [TestMethod]
    public void MappointToGeoPointDelegatesToProjection()
    {
        var projection = Projections<double>.Equirectangular;
        var converter = new RepresentationConverter<double>(projection);
        var geoPoint = new GeoPoint<double>(12, 34);
        var projected = projection.GeoPointToMapPoint(geoPoint);

        var roundTripped = converter.MappointToGeoPoint(projected);

        Assert.AreEqual(geoPoint.Latitude, roundTripped.Latitude, 1e-9);
        Assert.AreEqual(geoPoint.Longitude, roundTripped.Longitude, 1e-9);
    }

    [TestMethod]
    public void MappointToTileClampsWithinValidTileRange()
    {
        var projection = Projections<double>.Equirectangular;
        var converter = new RepresentationConverter<double>(projection, tileSize: 256);

        var farAway = new ProjectedPoint<double>(1_000_000, 1_000_000, projection);
        var tile = converter.MappointToTile(farAway, zoomLevel: 2);

        // At zoom level 2 there are 4 tiles per axis (indices 0..3).
        Assert.IsTrue(tile.TileX is >= 0 and <= 3);
        Assert.IsTrue(tile.TileY is >= 0 and <= 3);
    }
}
