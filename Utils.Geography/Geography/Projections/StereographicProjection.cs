using System.Numerics;
using Utils.Geography.Model;
using Utils.Mathematics;

namespace Utils.Geography.Projections;

public class StereographicProjection<T> : IProjectionTransformation<T>
	where T : struct, IFloatingPointIeee754<T>
{
	private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

	public ProjectedPoint<T> GeoPointToMapPoint(GeoPoint<T> geoPoint)
	{
		T lat = geoPoint.Latitude;
		T lon = geoPoint.Longitude;

		// k = 1 / [1 + cos(lat)*cos(lon)]
		T cosLat = degree.Cos(lat);
		T cosLon = degree.Cos(lon);
		T denom = T.One + (cosLat * cosLon);
		if (T.IsZero(denom)) denom = T.Epsilon; // avoid /0
		T k = T.One / denom;

		T sinLat = degree.Sin(lat);
		T sinLon = degree.Sin(lon);

		// x = k*cos(lat)*sin(lon)
		// y = k*sin(lat)
		T x = k * cosLat * sinLon;
		T y = k * sinLat;

		return new ProjectedPoint<T>(x, y, this);
	}

	public GeoPoint<T> MapPointToGeoPoint(ProjectedPoint<T> mapPoint)
	{
		T x = mapPoint.X;
		T y = mapPoint.Y;

		// ρ = sqrt(x^2 + y^2)
		T rho = T.Sqrt(x * x + y * y);
		if (T.IsZero(rho))
		{
			// (x=0,y=0) => lat=0, lon=0
			return new GeoPoint<T>(T.Zero, T.Zero);
		}

		// c = 2 * atan(ρ)
		// We'll do it in degrees, so we use degree.Atan
		T c = T.CreateChecked(2) * degree.Atan(rho);

		// lat = asin( (y*sin(c)) / ρ )
		T sinC = degree.Sin(c);
		T latFactor = (y * sinC) / rho;
		if (latFactor > T.One) latFactor = T.One;
		if (latFactor < -T.One) latFactor = -T.One;
		T lat = degree.Asin(latFactor);

		// lon = atan2( x*sin(c), ρ*cos(c) )
		T cosC = degree.Cos(c);
		T lon = degree.Atan2(x * sinC, rho * cosC);

		return new GeoPoint<T>(lat, lon);
	}
}
