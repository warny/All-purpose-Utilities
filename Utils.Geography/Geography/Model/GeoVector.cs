﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Mathematics;

namespace Utils.Geography.Model
{
	public class GeoVector : GeoPoint, IEquatable<GeoVector>
	{
		public double Bearing { get; }

		/// <summary>
		/// Create a geoVector at given <paramref name="coordinates"/> heading to <paramref name="direction"/>
		/// </summary>
		/// <param name="latitude">Lattitude</param>
		/// <param name="longitude">Longitude</param>
		/// <param name="direction">Heading direction</param>
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
		/// <param name="bearing">Heading direction</param>
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

		public override string ToString(string format, IFormatProvider formatProvider)
		{
			formatProvider ??= CultureInfo.InvariantCulture;
			var textInfo = (TextInfo)formatProvider?.GetFormat(typeof(TextInfo));

			return base.ToString(format, formatProvider) + $"{textInfo?.ListSeparator ?? ","} {Bearing:##0.##}";
		}

		/// <summary>
		/// Compute dearing from point A to point B
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
			return degree.Normalize0To2Max(-degree.Atan2(y, x));
		}

		public GeoVector Travel(double angle)
		{

			bool negative = Math.Sign(angle) == -1;
			angle = degree.Normalize0To2Max(angle);
			if (angle == 0) return this;
			if (angle == 180) return new GeoVector(-this.Latitude, -this.Longitude, -this.Bearing);
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

			GeoPoint arrival = new GeoPoint(φ2, λ2);
			double bearing = MathEx.Mod(bearingcorrection - ComputeBearing(arrival, this), 360);
			return new GeoVector(arrival, bearing);
		}

		public override bool Equals(object obj)
		{
			if (this == obj) return true;
			if (obj is GeoVector p) return Equals(p);
			return base.Equals(obj);
		}

		public override int GetHashCode()
			=> Objects.ObjectUtils.ComputeHash(Latitude, Longitude, Bearing);

		public bool Equals(GeoVector other) 
			=> comparer.Equals(Bearing, other.Bearing) && base.Equals(other);
	}
}
