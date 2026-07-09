using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Geography.Model;
using Utils.Geography.Projections;

namespace UtilsTest.Geography;

/// <summary>
/// Tests for <see cref="IProjectionTransformation{T}.Bounds"/> and
/// <see cref="IProjectionTransformation{T}.Normalize"/>, added when <c>MapPoint&lt;T&gt;</c> and
/// <c>RepresentationConverter&lt;T&gt;.MappointToTile</c> were rewired to use them (see
/// <see cref="RepresentationConverterTests.MappointToTileAgreesWithMapPointForTheSameProjectedPoint"/>
/// for the regression test proving the two now agree).
/// </summary>
[TestClass]
public class ProjectionBoundsTests
{
    private const double Delta = 1e-6;

    [TestMethod]
    public void MercatorMaxLatitudeIsTheStandardWebMercatorCutoff()
    {
        // The well-known ~85.05112878 deg cutoff used by every major slippy-map implementation.
        Assert.AreEqual(85.05112877980659, MercatorProjection<double>.MaxLatitude, 1e-9);
    }

    [TestMethod]
    public void MercatorBoundsYIsPiAtTheMaxLatitudeCutoff()
    {
        // By construction, MaxLatitude is exactly the latitude at which Mercator's Y equals pi.
        var bounds = Projections<double>.Mercator.Bounds;

        Assert.AreEqual(-180, bounds.MinX, Delta);
        Assert.AreEqual(180, bounds.MaxX, Delta);
        Assert.AreEqual(-System.Math.PI, bounds.MinY, Delta);
        Assert.AreEqual(System.Math.PI, bounds.MaxY, Delta);
    }

    [TestMethod]
    public void EquirectangularBoundsMatchLatLonRange()
    {
        var bounds = Projections<double>.Equirectangular.Bounds;

        Assert.AreEqual(-180, bounds.MinX, Delta);
        Assert.AreEqual(180, bounds.MaxX, Delta);
        Assert.AreEqual(-90, bounds.MinY, Delta);
        Assert.AreEqual(90, bounds.MaxY, Delta);
    }

    [TestMethod]
    public void GallsPetersBoundsReachedAtThePoles()
    {
        var bounds = Projections<double>.GallsPeters.Bounds;
        double sqrt2 = System.Math.Sqrt(2);

        Assert.AreEqual(-sqrt2, bounds.MinY, Delta);
        Assert.AreEqual(sqrt2, bounds.MaxY, Delta);
    }

    [TestMethod]
    public void MollweideBoundsMatchTheDocumentedBoundingEllipseRectangle()
    {
        var bounds = Projections<double>.Mollweide.Bounds;
        double sqrt2 = System.Math.Sqrt(2);

        Assert.AreEqual(-2 * sqrt2, bounds.MinX, Delta);
        Assert.AreEqual(2 * sqrt2, bounds.MaxX, Delta);
        Assert.AreEqual(-sqrt2, bounds.MinY, Delta);
        Assert.AreEqual(sqrt2, bounds.MaxY, Delta);
    }

    [TestMethod]
    public void LambertBoundsReachedAtTheSouthPole()
    {
        // This Lambert implementation is polar (north-pole-centered); the south pole (its antipode)
        // is where rho reaches its maximum, finite value of exactly 2.
        var bounds = Projections<double>.Lambert.Bounds;

        Assert.AreEqual(-2, bounds.MinX, Delta);
        Assert.AreEqual(2, bounds.MaxX, Delta);
        Assert.AreEqual(-2, bounds.MinY, Delta);
        Assert.AreEqual(2, bounds.MaxY, Delta);
    }

    [TestMethod]
    public void StereographicBoundsAreReachedAtThePoles()
    {
        var bounds = Projections<double>.Stereographic.Bounds;

        Assert.AreEqual(-1, bounds.MinX, Delta);
        Assert.AreEqual(1, bounds.MaxX, Delta);
        Assert.AreEqual(-1, bounds.MinY, Delta);
        Assert.AreEqual(1, bounds.MaxY, Delta);
    }

    [TestMethod]
    public void EachProjectionsOwnCenterNormalizesToOneHalf()
    {
        // Every projection here maps some specific GeoPoint to its own (0,0) origin (Lambert's is the
        // north pole rather than the geographic (0,0), since it is polar-centered - see
        // ProjectionsRoundTripTests.LambertNorthPoleMapsToOrigin). Normalizing that origin against the
        // projection's own Bounds must land exactly at the center, (0.5, 0.5).
        (IProjectionTransformation<double> projection, GeoPoint<double> center)[] cases =
        [
            (Projections<double>.Equirectangular, new GeoPoint<double>(0, 0)),
            (Projections<double>.Mercator, new GeoPoint<double>(0, 0)),
            (Projections<double>.GallsPeters, new GeoPoint<double>(0, 0)),
            (Projections<double>.Miller, new GeoPoint<double>(0, 0)),
            (Projections<double>.Mollweide, new GeoPoint<double>(0, 0)),
            (Projections<double>.Lambert, new GeoPoint<double>(90, 0)),
            (Projections<double>.Stereographic, new GeoPoint<double>(0, 0)),
        ];

        foreach (var (projection, center) in cases)
        {
            var projected = projection.GeoPointToMapPoint(center);
            var (x, y) = projection.Normalize(projected);

            Assert.AreEqual(0.5, x, Delta, projection.GetType().Name);
            Assert.AreEqual(0.5, y, Delta, projection.GetType().Name);
        }
    }

    [TestMethod]
    public void NormalizeIsWithinUnitRangeForPointsWellWithinBounds()
    {
        var paris = new GeoPoint<double>(48.8566, 2.3522);

        foreach (var projection in new[]
                 {
                     Projections<double>.Equirectangular,
                     Projections<double>.Mercator,
                     Projections<double>.GallsPeters,
                     Projections<double>.Miller,
                     Projections<double>.Mollweide,
                 })
        {
            var projected = projection.GeoPointToMapPoint(paris);
            var (x, y) = projection.Normalize(projected);

            Assert.IsTrue(x is >= 0 and <= 1, $"{projection.GetType().Name}: x={x}");
            Assert.IsTrue(y is >= 0 and <= 1, $"{projection.GetType().Name}: y={y}");
        }
    }
}
