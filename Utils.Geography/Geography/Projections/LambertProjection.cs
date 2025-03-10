using System.Numerics;
using Utils.Geography.Model;
using Utils.Mathematics;

namespace Utils.Geography.Projections;

public class LambertAzimuthalEqualArea<T> : IProjectionTransformation<T>
	where T : struct, IFloatingPointIeee754<T>
{
	private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;
	private static T Sqrt2 { get; } = T.Sqrt(T.CreateChecked(2));

	public ProjectedPoint<T> GeoPointToMapPoint(GeoPoint<T> geoPoint)
	{
		// lat, lon in degrees
		T lat = geoPoint.Latitude;
		T lon = geoPoint.Longitude;

		// ρ = √2 * sqrt(1 - sin(lat))
		T sinLat = degree.Sin(lat);
		T factor = T.One - sinLat;
		if (factor < T.Zero) factor = T.Zero; // clamp if needed
		T rho = Sqrt2 * T.Sqrt(factor);

		T sinLon = degree.Sin(lon);
		T cosLon = degree.Cos(lon);

		// x = ρ sin(lon)
		// y = -ρ cos(lon)
		T x = rho * sinLon;
		T y = -rho * cosLon;

		return new ProjectedPoint<T>(x, y, this);
	}

	public GeoPoint<T> MapPointToGeoPoint(ProjectedPoint<T> mapPoint)
	{
		T x = mapPoint.X;
		T y = mapPoint.Y;

		// ρ = sqrt(x^2 + y^2)
		T rho = T.Sqrt(x * x + y * y);

		// C = (ρ^2) / 2
		T c = (rho * rho) / T.CreateChecked(2);

		// lat = asin(1 - C)
		T latFactor = T.One - c;
		if (latFactor > T.One) latFactor = T.One;
		if (latFactor < -T.One) latFactor = -T.One;
		T lat = degree.Asin(latFactor);

		// lon = atan2(x, -y)
		// so if y=0 => watch sign of x
		T lon = degree.Atan2(x, -y);

		return new GeoPoint<T>(lat, lon);
	}

}
