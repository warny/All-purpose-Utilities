using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Geography.Display;
using Utils.Geography.Model;
using Utils.Geography.Projections;

namespace UtilsTest.Geography;

[TestClass]
public class MapPointTileTests
{
    [TestMethod]
    public void TileXAndTileYAreWithinTileForPositiveCoordinates()
    {
        var point = new MapPoint<double>(300, 570, zoomLevel: 2, tileSize: 256);

        Assert.AreEqual(44, point.TileX);
        Assert.AreEqual(58, point.TileY);
        Assert.AreEqual(1, point.Tile.TileX);
        Assert.AreEqual(2, point.Tile.TileY);
    }

    [TestMethod]
    public void TileXAndTileYAreNonNegativeForNegativeCoordinates()
    {
        // Regression test: X % TileSize used to return a negative remainder for negative X,
        // which put the point outside the [0, TileSize) range expected for an in-tile offset.
        var point = new MapPoint<double>(-5, -300, zoomLevel: 2, tileSize: 256);

        Assert.AreEqual(251, point.TileX);
        Assert.AreEqual(212, point.TileY);
    }

    [TestMethod]
    public void TileGridIndexUsesFlooredDivisionForNegativeCoordinates()
    {
        // -5 belongs to tile index -1 (floor(-5/256) = -1), not 0 (truncated division).
        var point = new MapPoint<double>(-5, -300, zoomLevel: 2, tileSize: 256);

        Assert.AreEqual(-1, point.Tile.TileX);
        Assert.AreEqual(-2, point.Tile.TileY);
    }

    [TestMethod]
    public void TileEqualityComparesAllComponents()
    {
        var tile1 = new Tile<double>(1, 2, 3, 256);
        var tile2 = new Tile<double>(1, 2, 3, 256);
        var tile3 = new Tile<double>(1, 2, 4, 256);

        Assert.AreEqual(tile1, tile2);
        Assert.IsTrue(tile1 == tile2);
        Assert.AreNotEqual(tile1, tile3);
        Assert.IsTrue(tile1 != tile3);
    }

    [TestMethod]
    public void TileContainsDetectsPointsInsideAndOutsideBounds()
    {
        var tile = new Tile<double>(0, 0, 0, 256);
        var projection = Projections<double>.Equirectangular;

        Assert.IsTrue(tile.Contains(new ProjectedPoint<double>(0, 0, projection)));
        Assert.IsTrue(tile.Contains(new ProjectedPoint<double>(256, 256, projection)));
        Assert.IsFalse(tile.Contains(new ProjectedPoint<double>(-1, 0, projection)));
        Assert.IsFalse(tile.Contains(new ProjectedPoint<double>(0, 300, projection)));
    }

    [TestMethod]
    public void MapPoint1And2CornersMatchTileBounds()
    {
        var tile = new Tile<double>(2, 3, 4, 256);

        Assert.AreEqual(2 * 256, tile.MapPoint1.X);
        Assert.AreEqual(3 * 256, tile.MapPoint1.Y);
        Assert.AreEqual(3 * 256, tile.MapPoint2.X);
        Assert.AreEqual(4 * 256, tile.MapPoint2.Y);
    }

    [TestMethod]
    public void ToStringContainsCoordinatesAndZoom()
    {
        var tile = new Tile<double>(1, 2, 3, 256);
        string text = tile.ToString();

        StringAssert.Contains(text, "1");
        StringAssert.Contains(text, "2");
        StringAssert.Contains(text, "3");
    }
}
