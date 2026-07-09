using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils.Geography.Model;
using Utils.Geography.Projections;

namespace UtilsTest.Geography;

/// <summary>
/// Round-trip tests for every projection registered in <see cref="Projections{T}"/>: projecting a
/// geographic point and then unprojecting the result should return (approximately) the original point.
/// Points are chosen away from each projection's known singularities (poles for azimuthal projections,
/// and the antipodal meridian for the centered stereographic projection).
/// </summary>
[TestClass]
public class ProjectionsRoundTripTests
{
    private static readonly (double latitude, double longitude)[] SamplePoints =
    [
        (0, 0),
        (10, 20),
        (45, 45),
        (-45, -45),
        (30, -120),
        (-30, 150),
        (60, -170),
        (-60, 170),
        (5, -5),
    ];

    private static void AssertRoundTrip(IProjectionTransformation<double> projection, double tolerance = 1e-3)
    {
        foreach (var (latitude, longitude) in SamplePoints)
        {
            var original = new GeoPoint<double>(latitude, longitude);
            var projected = projection.GeoPointToMapPoint(original);
            var roundTripped = projection.MapPointToGeoPoint(projected);

            Assert.AreEqual(original.Latitude, roundTripped.Latitude, tolerance, $"latitude mismatch for ({latitude},{longitude})");
            Assert.AreEqual(original.Longitude, roundTripped.Longitude, tolerance, $"longitude mismatch for ({latitude},{longitude})");
        }
    }

    [TestMethod]
    public void EquirectangularRoundTrips() => AssertRoundTrip(Projections<double>.Equirectangular);

    [TestMethod]
    public void MercatorRoundTrips() => AssertRoundTrip(Projections<double>.Mercator);

    [TestMethod]
    public void GallsPetersRoundTrips() => AssertRoundTrip(Projections<double>.GallsPeters);

    [TestMethod]
    public void MillerRoundTrips() => AssertRoundTrip(Projections<double>.Miller);

    [TestMethod]
    public void MollweideRoundTrips() => AssertRoundTrip(Projections<double>.Mollweide);

    [TestMethod]
    public void LambertRoundTrips() => AssertRoundTrip(Projections<double>.Lambert);

    [TestMethod]
    public void StereographicRoundTrips() => AssertRoundTrip(Projections<double>.Stereographic);

    [TestMethod]
    public void GetProjectionIsCaseInsensitiveAndCached()
    {
        var lower = Projections<double>.GetProjection("mercator");
        var upper = Projections<double>.GetProjection("MERCATOR");

        Assert.AreSame(lower, upper);
        Assert.AreSame(Projections<double>.Mercator, lower);
    }

    [TestMethod]
    public void CenterOfEachProjectionMapsToOrigin()
    {
        // Lambert is intentionally excluded: this implementation is the polar aspect of the
        // Lambert azimuthal equal-area projection, centered on the north pole (see
        // LambertNorthPoleMapsToOrigin below), not on (lat=0, lon=0).
        var origin = new GeoPoint<double>(0, 0);

        foreach (var projection in new[]
                 {
                     Projections<double>.Equirectangular,
                     Projections<double>.Mercator,
                     Projections<double>.GallsPeters,
                     Projections<double>.Miller,
                     Projections<double>.Mollweide,
                     Projections<double>.Stereographic,
                 })
        {
            var projected = projection.GeoPointToMapPoint(origin);
            Assert.AreEqual(0, projected.X, 1e-9, projection.GetType().Name);
            Assert.AreEqual(0, projected.Y, 1e-9, projection.GetType().Name);
        }
    }

    [TestMethod]
    public void LambertNorthPoleMapsToOrigin()
    {
        // This Lambert azimuthal equal-area implementation is the polar aspect: the north pole
        // (lat=90) is the center of the projection and maps to (0,0), not the equator.
        var northPole = new GeoPoint<double>(90, 0);
        var projected = Projections<double>.Lambert.GeoPointToMapPoint(northPole);

        Assert.AreEqual(0, projected.X, 1e-9);
        Assert.AreEqual(0, projected.Y, 1e-9);
    }
}
