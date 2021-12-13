using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Mathematics;

namespace Utils.Geography.Model
{
	/// <summary>
	/// Vecteur de déplacement sur une géodésique shpérique
	/// </summary>
	/// <remarks>This class has been fully ported from javascript presented in https://www.movable-type.co.uk/scripts/latlong.html </remarks>
	public class GeoVector : GeoPoint, IEquatable<GeoVector>
	{
		/// <summary>
		/// Cap en degree par rapport au nord, dans le sens des aiguille d'une montre
		/// </summary>
		public double Bearing { get; }

		/// <summary>
		/// Cap en degree par rapport au nord, dans le sens des aiguille d'une montre
		/// </summary>
		public double θ => Bearing;

		/// <summary>
		/// Create a geoVector at given <paramref name="coordinates"/> 
		/// </summary>
		/// <param name="coordinates">Coorduinates including bearing</param>
		/// <param name="cultureInfos">culture used to parse coordinates</param>
		public GeoVector(string coordinates, params CultureInfo[] cultureInfos) {
			if (cultureInfos.Length == 0) cultureInfos = new[] { CultureInfo.CurrentCulture, CultureInfo.InvariantCulture };

			foreach (var cultureInfo in cultureInfos)
			{
				var coordinatesStrings = coordinates.Split(new[] { cultureInfo.TextInfo.ListSeparator }, StringSplitOptions.None);
				if (coordinatesStrings.Length != 3) continue;
				Regex regExCoordinate = BuildRegexCoordinates(cultureInfo);
				if (!double.TryParse(coordinatesStrings[2], NumberStyles.Float, cultureInfo, out double direction)) continue;
				Bearing = MathEx.Mod(direction, 360);
				if (ParseCoordinates(coordinatesStrings[0], coordinatesStrings[1], cultureInfo, regExCoordinate)) return;
			}

			throw new ArgumentException($"\"{coordinates}\" n'est pas un vecteur valide");
		}

		/// <summary>
		/// Create geovector at <paramref name="geoPoint"/> headind to <paramref name="bearing"/>
		/// </summary>
		/// <param name="geoPoint">Point</param>
		/// <param name="bearing">bearing direction</param>
		public GeoVector(GeoPoint geoPoint, double bearing) : base(geoPoint)
		{
			Bearing = degree.Normalize0To2Max(bearing);
		}

		/// <summary>
		/// Compute geovector direction from <paramref name="geoPoint"/> to <paramref name="destination"/>
		/// </summary>
		/// <param name="geoPoint">start point</param>
		/// <param name="destination">destination point</param>
		public GeoVector(GeoPoint geoPoint, GeoPoint destination) : base(geoPoint)
		{
			Bearing = ComputeBearing(geoPoint, destination);
		}


		/// <summary>
		/// Create a geoVector at given coordinates heading to <paramref name="bearing"/>
		/// </summary>
		/// <param name="latitude">Lattitude</param>
		/// <param name="longitude">Longitude</param>
		/// <param name="bearing">Heading direction</param>
		public GeoVector(double latitude, double longitude, double bearing) : base(latitude, longitude)
		{
			Bearing = degree.Normalize0To2Max(bearing);
		}

		/// <summary>
		/// Create a geoVector at given coordinates heading to <paramref name="bearing"/>
		/// </summary>
		/// <param name="latitude">Lattitude</param>
		/// <param name="longitude">Longitude</param>
		/// <param name="bearing">Heading direction</param>
		public GeoVector(string latitudeString, string longitudeString, double bearing, params CultureInfo[] cultureInfos) : base(latitudeString, longitudeString, cultureInfos)
		{
			Bearing = degree.Normalize0To2Max(bearing);
		}

		public void Deconstruct(out double latitude, out double longitude, out double bearing) {
			latitude = Latitude;
			longitude = Longitude;
			bearing = Bearing;
		}

		/// <summary>
		/// Compute bearing from point A to point B
		/// </summary>
		/// <param name="A">Start point</param>
		/// <param name="B">End point</param>
		/// <returns>Bearing</returns>
		public static double ComputeBearing(GeoPoint A, GeoPoint B)
		{
			if (comparer.Equals(A.Longitude, B.Longitude))
			{
				return A.Latitude > B.Latitude ? 180 : 0;
			}
			if (comparer.Equals(A.Longitude, B.Longitude - 180) || comparer.Equals(A.Longitude, B.Longitude + 180))
			{
				return A.Latitude > -B.Latitude ? 180 : 0;
			}

			double y = degree.Sin(B.λ - A.λ) * degree.Cos(B.φ);
			double x = degree.Cos(A.φ) * degree.Sin(B.φ) - degree.Sin(A.φ) * degree.Cos(B.φ) * degree.Cos(B.λ - A.λ);
			return degree.Normalize0To2Max(degree.Atan2(y, x));
		}

		/// <summary>
		/// Calcul le vecteur résultant d'un déplacement
		/// </summary>
		/// <param name="angle"></param>
		/// <returns></returns>
		public GeoVector Travel(double angle)
		{

			bool negative = Math.Sign(angle) == -1;
			angle = degree.Normalize0To2Max(angle);
			if (angle == 0) return this;
			if (angle == 180) return new GeoVector(-this.Latitude, this.Longitude + 180, this.Bearing + 180);
			double bearingcorrection = (angle <= 180) ^ negative ? 180 : 0;

			if (this.φ == 90) return new GeoVector(90 - angle, bearingcorrection + this.λ, bearingcorrection);
			if (this.φ == -90) return new GeoVector(-90 + angle, bearingcorrection + this.λ, 180 + bearingcorrection);

			double φ2 = degree.Asin(
					degree.Sin(φ) * degree.Cos(angle) 
					+ degree.Cos(φ) * degree.Sin(angle) * degree.Cos(-Bearing)
				);
			if (φ2 == 90) return new GeoVector(90, λ, MathEx.Mod(Bearing + 180 + bearingcorrection, 360));
			if (φ2 == -90) return new GeoVector(-90, λ, MathEx.Mod(Bearing + 180 + bearingcorrection, 360));

			double λ2 = λ + degree.Atan2(
					degree.Sin(Bearing) * degree.Sin(angle) * degree.Cos(φ),
					degree.Cos(angle) - degree.Sin(φ) * degree.Sin(φ2)
				);

			φ2 = Math.Round(φ2, 5);
			λ2 = Math.Round(λ2, 5);
			GeoPoint arrival = new GeoPoint(φ2, λ2);
			double bearing = MathEx.Mod(bearingcorrection + ComputeBearing(arrival, this), 360);
			return new GeoVector(arrival, bearing);
		}

		/// <summary>
		/// Calcule les intersections entre 2 grands cercles
		/// </summary>
		/// <param name="other">Grand cercle à comparer</param>
		/// <returns>Intersection <see cref="GeoPoint"/> or <see cref="null"/> if great circles are the same</returns>
		public GeoPoint[] Intersections(GeoVector other)
		{
			var temp = (a: Travel(this.AngleWith(other)), b: (-this).Travel(this.AngleWith(other)));
			//si les deux vecteurs décrivent le même grand cercle, il n'est pas possible de renvoyer les points d'intersection
			if (other.In(temp.a, -temp.a, temp.b, -temp.b))
			{
				return null;
			}


			//si les deux vecteurs suivents des méridiens, ils se croisents aux pôles
			if (MathEx.Mod(this.Bearing, 180) == 0 && MathEx.Mod(other.Bearing, 180) == 0) {
				return new[] {
					new GeoPoint(90, 0),
					new GeoPoint(-90, 180)
				};
			}

			if (MathEx.Mod(this.Bearing, 180) == 0)
			{
				var result = other.Travel(this.λ - other.λ);

				return new[] {
					new GeoPoint(result.φ, result.λ),
					new GeoPoint(-result.φ, result.λ + 180),
				};
			}

			if (MathEx.Mod(other.Bearing, 180) == 0)
			{
				var result = this.Travel(this.λ - other.λ);

				return new[] {
					new GeoPoint(result.φ, result.λ),
					new GeoPoint(-result.φ, result.λ + 180),
				};
			}

			double Δφ = this.φ - other.φ;
			double Δλ = this.λ - other.λ;

			double δ12 = 2 * degree.Asin(Math.Sqrt(Math.Pow(degree.Sin(Δφ / 2), 2) + degree.Cos(this.φ) * degree.Cos(other.φ) * Math.Pow(degree.Sin(Δλ / 2), 2))); //	angular dist. p1–p2
			double θa = degree.Acos((degree.Sin(other.φ) - degree.Sin(this.φ) * degree.Cos(δ12)) / (degree.Sin(δ12) * degree.Cos(this.φ)));
			double θb = degree.Acos((degree.Sin(this.φ) - degree.Sin(other.φ) * degree.Cos(δ12)) / (degree.Sin(δ12) * degree.Cos(other.φ)));    // initial / final bearings between points 1 & 2
			double θ12, θ21;
			if (degree.Sin(Δλ) <= 0) {
				θ12 = θa;
				θ21 = 360 - θb;
			}
			else {
				θ12 = 360 - θa;
				θ21 = θb;
			}
			double α1 = this.Bearing - θ12; //angle p2–p1–p3 
			double α2 = θ21 - other.Bearing; //angle p1–p2–p3	


			double α3 = degree.Acos(-degree.Cos(α1) * degree.Cos(α2) + degree.Sin(α1) * degree.Sin(α2) * degree.Cos(δ12));  //angle p1–p2–p3
			double δ13 = degree.Atan2(degree.Sin (δ12) * degree.Sin (α1) * degree.Sin (α2), degree.Cos (α2) + degree.Cos (α1) * degree.Cos (α3));  //angular dist. p1–p3
			double φ3 = degree.Asin(degree.Sin (this.φ) * degree.Cos (δ13) + degree.Cos (this.φ) * degree.Sin( δ13) * degree.Cos (this.Bearing)); 	//p3 lat
			double Δλ13 = degree.Atan2(degree.Sin (this.Bearing) * degree.Sin (δ13) * degree.Cos (this.φ), degree.Cos (δ13) - degree.Sin (this.φ) * degree.Sin (φ3));   //long p1–p3
			double λ3 = this.λ + Δλ13;

			φ3 = Math.Round(φ3, 5);
			λ3 = Math.Round(λ3, 5);

			return new[] {
				new GeoPoint(φ3, λ3),
				new GeoPoint(-φ3, λ3 + 180),
			};
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj)) return true;
			if (obj is GeoVector p) return Equals(p);
			return base.Equals(obj);
		}

		public override int GetHashCode()
			=> Objects.ObjectUtils.ComputeHash(Latitude, Longitude, Bearing);

		public bool Equals(GeoVector other) 
			=> comparer.Equals(Bearing, other.Bearing) && base.Equals(other);

		public static GeoVector operator -(GeoVector geoVector) => new GeoVector(geoVector.φ, geoVector.λ, 180 + geoVector.θ);

		public override string ToString(string format, IFormatProvider formatProvider)
		{
			formatProvider ??= CultureInfo.InvariantCulture;
			var textInfo = (TextInfo)formatProvider?.GetFormat(typeof(TextInfo));

			return base.ToString(format, formatProvider) + $"{textInfo?.ListSeparator ?? ","} {Bearing:##0.##}";
		}
	}
}
