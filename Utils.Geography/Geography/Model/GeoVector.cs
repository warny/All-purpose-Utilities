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

		public GeoVector(GeoPoint geoPoint, double direction) : base(geoPoint)
		{
			Direction = MathEx.Mod(direction, 360);
		}

		public GeoVector(double latitude, double longitude, double direction) : base(latitude, longitude)
		{
			Direction = MathEx.Mod(direction, 360);
		}

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
