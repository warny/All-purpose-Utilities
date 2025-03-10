using System;
using System.Numerics;
using Utils.Geography.Model;
using Utils.Geography.Projections;
using Utils.Mathematics;

namespace Utils.Geography.Projections
{
	/// <summary>
	/// Mollweide (Homalographic) equal-area projection, implemented with degree-based trigonometry.
	/// 
	/// Lat/Lon in degrees => (x,y) in "Mollweide units" with:
	///   - (0°,0°) maps to (0,0).
	///   - The bounding ellipse is roughly x ∈ [-2√2..+2√2], y ∈ [-√2..+√2].
	///   
	/// Usage:
	///   var projector = new MollweideProjectionDegrees<double>();
	///   var projected = projector.GeoPointToMapPoint(new GeoPoint<double>(latDeg, lonDeg));
	///   var unprojected = projector.MapPointToGeoPoint(projected);
	/// </summary>
	/// <typeparam name="T">
	/// A floating point type implementing IFloatingPointIeee754, e.g. float/double/decimal.
	/// </typeparam>
	public class MollweideProjection<T> : IProjectionTransformation<T>
		where T : struct, IFloatingPointIeee754<T>
	{
		// We'll use "degree" so that degree.Sin(45) = sin(45°) in normal radians internally.
		private static readonly IAngleCalculator<T> degree = Trigonometry<T>.Degree;

		// For iteration tolerance
		private static readonly T Eps = T.CreateChecked(1.0e-7);
		private const int MaxIter = 10;

		/// <summary>
		/// Projects (latitude, longitude) in degrees => Mollweide (x, y).
		/// 
		/// Forward formula in "degree-based" Mollweide:
		///   (1) Solve 2θ + sin(2θ) = 180 * sin(lat), for θ ∈ [-90..+90].
		///   (2) x = (2√2 / 180) * (lon) * cos(θ)
		///   (3) y = √2 * sin(θ)
		/// </summary>
		public ProjectedPoint<T> GeoPointToMapPoint(GeoPoint<T> geoPoint)
		{
			// lat, lon in degrees
			T latDeg = geoPoint.Latitude;
			T lonDeg = geoPoint.Longitude;

			// 1) Solve for θDeg using Newton's method:
			T sinLatDeg = degree.Sin(latDeg); // = sin(latDeg°)
											  // Right side of eqn: 180° * sin(lat)
			T target = T.CreateChecked(180) * sinLatDeg;

			T thetaDeg = SolveThetaDeg(target);

			// 2) x = (2√2/180) * lon * cos(θDeg)
			// 3) y = √2 * sin(θDeg)
			T cosTheta = degree.Cos(thetaDeg);
			T sinTheta = degree.Sin(thetaDeg);

			T x = (T.CreateChecked(2) * Sqrt2 / T.CreateChecked(180)) * lonDeg * cosTheta;
			T y = Sqrt2 * sinTheta;

			return new ProjectedPoint<T>(x, y, this);
		}

		/// <summary>
		/// Unprojects (x,y) in Mollweide => (lat, lon) in degrees.
		/// 
		/// Inverse:
		///   (1) θ = asin( y / √2 )   [in degrees]
		///   (2) lon = x * (180 / 2√2 cos(θ))
		///   (3) lat = asin( [2θ + sin(2θ)] / 180 )
		/// </summary>
		public GeoPoint<T> MapPointToGeoPoint(ProjectedPoint<T> mapPoint)
		{
			T x = mapPoint.X;
			T y = mapPoint.Y;

			// 1) θDeg = asin(y/√2) in degrees
			T ratio = y / Sqrt2;
			// If ratio is outside [-1..1], clamp it
			if (ratio > T.One) ratio = T.One;
			else if (ratio < -T.One) ratio = -T.One;

			T thetaDeg = degree.Asin(ratio);  // => in degrees
			T cosTheta = degree.Cos(thetaDeg);

			// 2) lonDeg = x / [ (2√2/180) * cos(θDeg ) ]
			// => multiply by reciprocal => lonDeg = x * 180 / (2√2 cos(θDeg))
			T lonDeg = T.Zero;
			if (!T.IsZero(cosTheta))
			{
				lonDeg = x * T.CreateChecked(180)
					   / (T.CreateChecked(2) * Sqrt2 * cosTheta);
			}

			// 3) latDeg => from sin(latDeg) = [2θDeg + sin(2θDeg)] / 180
			// => latDeg = asin(...)
			// We'll compute 2θ and sin(2θ) in degrees
			T twoTheta = thetaDeg + thetaDeg;
			T sin2θ = degree.Sin(twoTheta);
			T numerator = twoTheta + sin2θ; // dimension: degrees + dimensionless is "Ok" in this mollweide-degree sense
											// => latFactor = numerator / 180 => sin(latDeg)
			T latFactor = numerator / T.CreateChecked(180);

			// clamp if out of [-1..1]
			if (latFactor > T.One) latFactor = T.One;
			else if (latFactor < -T.One) latFactor = -T.One;

			T latDeg = degree.Asin(latFactor);

			return new GeoPoint<T>(latDeg, lonDeg);
		}

		/// <summary>
		/// Solve 2θDeg + sin(2θDeg) = target, with θDeg in degrees.
		/// 
		/// Newton iteration:
		///   f(θ) = 2θ + sin(2θ) - target
		///   f'(θ) = 2 + 2 cos(2θ)
		/// </summary>
		private static T SolveThetaDeg(T target)
		{
			// We'll pick an initial guess:
			// if target=0 => lat=0 => θ=0
			// a quick guess: θDeg = target/2
			T thetaDeg = target / T.CreateChecked(2);

			for (int i = 0; i < MaxIter; i++)
			{
				// f(θ)=2θ + sin(2θ) - target
				T twoTheta = thetaDeg + thetaDeg;
				T sin2θ = degree.Sin(twoTheta); // sin(2θDeg in degrees)
				T f = twoTheta + sin2θ - target;

				// f'(θ)=2 + 2 cos(2θ)
				T cos2θ = degree.Cos(twoTheta);
				T fprime = T.CreateChecked(2) + (T.CreateChecked(2) * cos2θ);

				if (T.IsZero(fprime))
					break; // degenerate case, not likely unless θ=±90

				T dθ = f / fprime;
				thetaDeg -= dθ;

				if (T.Abs(dθ) < Eps)
					break;
			}

			return thetaDeg;
		}

		/// <summary>
		/// Helper that returns √2 in type T, so we don't keep converting or re-computing.
		/// </summary>
		private readonly static T Sqrt2 = T.Sqrt(T.CreateChecked(2));
	}
}
