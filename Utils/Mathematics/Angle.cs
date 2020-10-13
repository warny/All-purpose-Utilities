using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Utils.Mathematics
{
	public struct Angle	: IFormattable, IEquatable<Angle>
	{
		private double radian;

		public Angle(double angle, AngleUnitEnum unit = AngleUnitEnum.Radian)
		{
			switch (unit)
			{
				case AngleUnitEnum.Radian:
					radian = MathEx.Mod(angle, Math.PI * 2);
					break;
				case AngleUnitEnum.Degree:
					radian = MathEx.Mod(angle * MathEx.Deg2Rad, Math.PI * 2);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(unit));
			}
		}

		public Angle(string angle, params CultureInfo[] cultureInfos) : this(angle, AngleUnitEnum.Radian, cultureInfos) { }

		public Angle(string angle, AngleUnitEnum unit, params CultureInfo[] cultureInfos)
		{
			if ((cultureInfos?.Length ?? 0) == 0) cultureInfos = new[] { CultureInfo.CurrentCulture, CultureInfo.InvariantCulture };

			foreach (var cultureInfo in cultureInfos)
			{
				var regexAngle = BuildRegexAngle(cultureInfo);
				var m = regexAngle.Match(angle);
				if (!m.Success) continue;
				if (m.Groups["degrees"].Success)
				{
					double degrees = double.Parse(m.Groups["deegres"].Value, NumberStyles.Float, cultureInfo);
					double minutes = m.Groups["minutes"].Success ? double.Parse(m.Groups["minutes"].Value, NumberStyles.Float, cultureInfo) : 0D;
					double seconds = m.Groups["seconds"].Success ? double.Parse(m.Groups["seconds"].Value, NumberStyles.Float, cultureInfo) : 0D;

					double temp = degrees + minutes / 60 + seconds / 3600;

					radian = MathEx.Mod(temp * MathEx.Deg2Rad, Math.PI * 2);
					return;
				}
				if (m.Groups["radians"].Success)
				{
					radian = MathEx.Mod(double.Parse(m.Groups["radians"].Value, NumberStyles.Float, cultureInfo), Math.PI * 2);
				}
				if (m.Groups["angle"].Success)
				{
					double angled = double.Parse(m.Groups["radians"].Value, NumberStyles.Float, cultureInfo);
					switch (unit)
					{
						case AngleUnitEnum.Radian:
							radian = MathEx.Mod(angled, Math.PI * 2);
							break;
						case AngleUnitEnum.Degree:
							radian = MathEx.Mod(angled * MathEx.Deg2Rad, Math.PI * 2);
							break;
						default:
							throw new ArgumentOutOfRangeException(nameof(unit));
					}
				}

			}
			throw new InvalidDataException($"{angle} is not a valid angle");
		}

		private static Regex BuildRegexAngle(CultureInfo cultureInfo)
		{
			string digits = "[" + string.Join("", cultureInfo.NumberFormat.NativeDigits) + "]+";
			string number = digits + "([" + cultureInfo.NumberFormat.NumberDecimalSeparator + "]" + digits + ")?";

			Regex regexAngle = new Regex(@"(?<modifier>\-|\+)?((?<deegres>number)°((?<minutes>number)')?((?<seconds>number)"")?|((?<radians>number)rad)|(?<angle>number))".Replace("number", number), RegexOptions.ExplicitCapture | RegexOptions.Compiled);
			return regexAngle;
		}


		public double ToRadians() => radian;
		public double ToDegrees() => Math.Round(radian * MathEx.Rad2Deg, 10);

		public double Tan() => Math.Tan(radian);
		public double Cos() => Math.Cos(radian);
		public double Sin() => Math.Sin(radian);

		public static Angle FromDegrees(double degrees) => new Angle(degrees, AngleUnitEnum.Degree);
		public static Angle FromRadian(double radian) => new Angle(radian, AngleUnitEnum.Radian);
		public static Angle FromACos(double acos) => new Angle(Math.Acos(acos), AngleUnitEnum.Radian);
		public static Angle FromASin(double asin) => new Angle(Math.Asin(asin), AngleUnitEnum.Radian);
		public static Angle FromATan(double atan) => new Angle(Math.Atan(atan), AngleUnitEnum.Radian);

		public static Angle operator +(Angle angle1, Angle angle2) => new Angle(angle1.radian + angle2.radian);
		public static Angle operator -(Angle angle1, Angle angle2) => new Angle(angle1.radian - angle2.radian);
		public static Angle operator *(Angle angle1, double m) => new Angle(angle1.radian * m);
		public static Angle operator /(Angle angle1, double d) => new Angle(angle1.radian * d);
		public static bool operator ==(Angle angle1, Angle angle2) => angle1.Equals(angle2);
		public static bool operator !=(Angle angle1, Angle angle2) => !angle1.Equals(angle2);

		public override string ToString() => ToString("d", CultureInfo.CurrentCulture);

		public string ToString(string format, IFormatProvider formatProvider)
		{
			switch (format)
			{
				case "d":
				case "D":
					var temp = ToDegrees();
					var degrees = Math.Floor(temp);
					temp = (temp - degrees) * 60;
					var minutes = Math.Floor(temp);
					temp = (temp - minutes) * 60;
					var seconds = Math.Floor(temp);
					if (seconds != 0 || format == "D") return $"{degrees:##0}°{minutes:00}'{seconds:00}\"";
					if (minutes != 0) return $"{degrees}°{minutes:00}'";
					return $"{degrees}°";
				case "r":
					return $"{radian.ToString(formatProvider)}rad";
				case "R":
					return $"{radian.ToString(formatProvider)}";
				default:
					throw new ArgumentException($"{format} is not a valid angle format");
			}
		}

		private static readonly DoubleComparer angleComparer = new DoubleComparer(10);

		public bool Equals(Angle other)
		{
			return angleComparer.Equals(radian, other.radian);
		}

		public override bool Equals(object obj)
		{
			if (obj is Angle a) return Equals(a);
			return false;
		}

		public override int GetHashCode() => radian.GetHashCode();
	}

	public enum AngleUnitEnum 
	{
		Radian,
		Degree
	}
}
