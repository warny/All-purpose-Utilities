using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Utils.Collections;

namespace Utils.Objects
{
	public class DoubleRanges : Ranges<double>
	{
		public DoubleRanges() : base() { }
		public DoubleRanges(Ranges<double> ranges) : base(ranges) { }
		public DoubleRanges(params Range<double>[] ranges) : base(ranges) { }
		public DoubleRanges(IEnumerable<Range<double>> ranges) : base(ranges) { }

		public static DoubleRanges Parse(string range) => Parse(range, System.Globalization.CultureInfo.CurrentCulture);
		public static DoubleRanges Parse(string range, System.Globalization.CultureInfo cultureInfo) => Parse(range, cultureInfo.NumberFormat);
		public static DoubleRanges Parse(string range, System.Globalization.NumberFormatInfo formatInfo) => new DoubleRanges(InnerParse(range, formatInfo));

		protected static IEnumerable<Range<double>> InnerParse(string range, System.Globalization.NumberFormatInfo formatInfo)
		{
			string digits = string.Join("", formatInfo.NativeDigits.FollowedBy(formatInfo.NumberGroupSeparator));
			string numberSearch = $"({formatInfo.NegativeSign}?[{digits}]*)({formatInfo.NumberDecimalSeparator}([{digits}]*))?";
			return InnerParse(range, numberSearch, s => double.Parse(s, formatInfo));
		}

		public void Add(string ranges) => this.Add(ranges, System.Globalization.CultureInfo.CurrentCulture);
		public void Add(string ranges, System.Globalization.CultureInfo cultureInfo) => this.Add(ranges, cultureInfo.NumberFormat);
		public void Add(string ranges, System.Globalization.NumberFormatInfo formatInfo) => this.Add(InnerParse(ranges, formatInfo));
	}

	public class SingleRanges : Ranges<float>
	{
		public SingleRanges() : base() { }
		public SingleRanges(Ranges<float> ranges) : base(ranges) { }
		public SingleRanges(params Range<float>[] ranges) : base(ranges) { }
		public SingleRanges(IEnumerable<Range<float>> ranges) : base(ranges) { }

		public static SingleRanges Parse(string range) => Parse(range, System.Globalization.CultureInfo.CurrentCulture);
		public static SingleRanges Parse(string range, System.Globalization.CultureInfo cultureInfo) => Parse(range, cultureInfo.NumberFormat);
		public static SingleRanges Parse(string range, System.Globalization.NumberFormatInfo formatInfo) => new SingleRanges(InnerParse(range, formatInfo));

		protected static IEnumerable<Range<float>> InnerParse(string range, System.Globalization.NumberFormatInfo formatInfo)
		{
			string digits = string.Join("", formatInfo.NativeDigits.FollowedBy(formatInfo.NumberGroupSeparator));
			string numberSearch = $"({formatInfo.NegativeSign}?[{digits}]*)({formatInfo.NumberDecimalSeparator}([{digits}]*))?";
			return InnerParse(range, numberSearch, s => float.Parse(s, formatInfo));
		}

		public void Add(string ranges) => this.Add(ranges, System.Globalization.CultureInfo.CurrentCulture);
		public void Add(string ranges, System.Globalization.CultureInfo cultureInfo) => this.Add(ranges, cultureInfo.NumberFormat);
		public void Add(string ranges, System.Globalization.NumberFormatInfo formatInfo) => this.Add(InnerParse(ranges, formatInfo));
	}

	public class DateTimeRanges : Ranges<DateTime>
	{
		public DateTimeRanges() : base() { }
		public DateTimeRanges(Ranges<DateTime> ranges) : base(ranges) { }
		public DateTimeRanges(params Range<DateTime>[] ranges) : base(ranges) { }
		public DateTimeRanges(IEnumerable<Range<DateTime>> ranges) : base(ranges) { }

		public static DateTimeRanges Parse(string range) => Parse(range, System.Globalization.CultureInfo.CurrentCulture);
		public static DateTimeRanges Parse(string range, System.Globalization.CultureInfo cultureInfo) => new DateTimeRanges(InnerParse(range, cultureInfo.DateTimeFormat));

		protected static IEnumerable<Range<DateTime>> InnerParse(string range, System.Globalization.DateTimeFormatInfo formatInfo)
		{
			string dateSearch 
				= formatInfo.ShortDatePattern.Aggregate(new StringBuilder(), (StringBuilder sb, char c) =>
				{
					switch (c)
					{
						case 'd':
						case 'M':
						case 'y':
							sb.Append(@"\d");
							break;
						case '\\':
							sb.Append(@"\\");
							break;
						default:
							sb.Append(c);
							break;
					}
					return sb;
				}).ToString()
				+ @"(\s+("
				+ (formatInfo.LongTimePattern + "|" + formatInfo.ShortTimePattern).Aggregate(new StringBuilder(), (StringBuilder sb, char c) =>
				{
					switch (c)
					{
						case 'H':
						case 'h':
						case 'm':
						case 's':
							sb.Append(@"\d");
							break;
						case '\\':
							sb.Append(@"\\");
							break;
						default:
							sb.Append(c);
							break;
					}
					return sb;
				}).ToString()
				+ "))?";

			return InnerParse(range, dateSearch, s => DateTime.Parse(s, formatInfo));
		}


		public void Add(string ranges) => this.Add(ranges, System.Globalization.CultureInfo.CurrentCulture);
		public void Add(string ranges, System.Globalization.CultureInfo cultureInfo) => this.Add(ranges, cultureInfo.DateTimeFormat);
		public void Add(string ranges, System.Globalization.DateTimeFormatInfo formatInfo) => this.Add(InnerParse(ranges, formatInfo));

	}

}
