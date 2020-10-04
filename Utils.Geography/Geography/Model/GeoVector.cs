using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Mathematics;

namespace Utils.Geography.Model
{
	public class GeoVector : GeoPoint
	{
		public double Direction { get; }

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
				Direction = MathEx.Mod(direction, 360);
				if (ParseCoordinates(coordinatesStrings[0], coordinatesStrings[1], cultureInfo, regExCoordinate)) return;
			}

			throw new ArgumentException($"\"{coordinates}\" n'est pas un vecteur valide");
		}

		/// <summary>
		/// Create geovector at <paramref name="geoPoint"/> headind to <paramref name="direction"/>
		/// </summary>
		/// <param name="geoPoint">Point</param>
		/// <param name="direction">Heading direction</param>
		public GeoVector(GeoPoint geoPoint, double direction) : base(geoPoint)
		{
			Direction = MathEx.Mod(direction, 360);
		}

		/// <summary>
		/// Compute geovector direction from <paramref name="geoPoint"/> to <paramref name="destination"/>
		/// </summary>
		/// <param name="geoPoint">start point</param>
		/// <param name="destination">destination point</param>
		public GeoVector(GeoPoint geoPoint, GeoPoint destination) : base(geoPoint)
		{
			if (geoPoint.Longitude == destination.Longitude || geoPoint.Longitude == destination.Longitude - 180 || geoPoint.Longitude == destination.Longitude + 180) {
				if (geoPoint.Latitude > destination.Longitude)
				{
					Direction = 180;
					return;
				}
				else
				{
					Direction = 0;
					return;
				}
			}

			var p1 = (Lat: geoPoint.Latitude * MathEx.Deg2Rad, Lon: geoPoint.Latitude * MathEx.Deg2Rad);
			var p2 = (Lat: destination.Latitude * MathEx.Deg2Rad, Lon: destination.Latitude * MathEx.Deg2Rad);

			double cotanDirection
				= ((Math.Cos(p1.Lat) * Math.Tan(p2.Lat)) / Math.Sin(p1.Lon - p2.Lon))
				- (Math.Sin(p1.Lat) / Math.Tan(p2.Lon - p1.Lon));

			Direction = MathEx.Mod(MathEx.Rad2Deg / Math.Atan(cotanDirection), 360);
		}

		//private GeoVector maxNorth = null, maxSouth = null;

		//private void ComputeMaximums() {
		//	if (Direction == 0)
		//	{
		//		maxNorth = new GeoVector(90, this.Longitude, this.Longitude);
		//		maxSouth = new GeoVector(-90, MathEx.Mod(this.Longitude + 180, 360), MathEx.Mod(this.Longitude + 180, 360));
		//	}
		//	else if (Direction == 180)
		//	{
		//		maxNorth = new GeoVector(90, MathEx.Mod(this.Longitude + 180, 360), MathEx.Mod(this.Longitude + 180, 360));
		//		maxSouth = new GeoVector(-90, this.Longitude, this.Longitude);
		//	}

		//	var v = (Lat: this.Latitude * MathEx.Deg2Rad, Lon: this.Longitude * MathEx.Deg2Rad, Dir: Direction * MathEx.Deg2Rad);

		//	double cosLat = Math.Abs(Math.Sin(v.Dir)) * Math.Cos(v.Lat);
			
		//}

		//public GeoVector MaxNorth
		//{
		//	get {
		//		if (maxNorth == null) ComputeMaximums();
		//		return maxNorth;
		//	}
		//}

		//public GeoVector MaxSouth
		//{
		//	get {
		//		if (maxSouth == null) ComputeMaximums();
		//		return maxSouth;
		//	}
		//}

		/// <summary>
		/// Create a geoVector at given coordinates heading to <paramref name="direction"/>
		/// </summary>
		/// <param name="latitude">Lattitude</param>
		/// <param name="longitude">Longitude</param>
		/// <param name="direction">Heading direction</param>
		public GeoVector(double latitude, double longitude, double direction) : base(latitude, longitude)
		{
			Direction = MathEx.Mod(direction, 360);
		}

		/// <summary>
		/// Create a geoVector at given coordinates heading to <paramref name="direction"/>
		/// </summary>
		/// <param name="latitude">Lattitude</param>
		/// <param name="longitude">Longitude</param>
		/// <param name="direction">Heading direction</param>
		public GeoVector(string latitudeString, string longitudeString, double direction, params CultureInfo[] cultureInfos) : base(latitudeString, longitudeString, cultureInfos)
		{
			Direction = MathEx.Mod(direction, 360);
		}

		public override string ToString(string format, IFormatProvider formatProvider)
		{
			formatProvider ??= CultureInfo.InvariantCulture;
			var textInfo = (TextInfo)formatProvider?.GetFormat(typeof(TextInfo));

			return base.ToString(format, formatProvider) + $"{textInfo?.ListSeparator ?? ","} {Direction:##0.##}";
		}
	}
}
