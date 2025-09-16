using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Format
{
	public static class FormatProviderEx
	{
		public static T GetFormat<T>(this IFormatProvider formatProvider) => (T)formatProvider.GetFormat(typeof(T));
		public static TextInfo GetTextInfo(this IFormatProvider formatProvider) => (TextInfo)formatProvider.GetFormat(typeof(TextInfo));
		public static NumberFormatInfo GetNumberFormatInfo(this IFormatProvider formatProvider) => (NumberFormatInfo)formatProvider.GetFormat(typeof(NumberFormatInfo));
		public static CompareInfo GetCompareInfo(this IFormatProvider formatProvider) => (CompareInfo)formatProvider.GetFormat(typeof(CompareInfo));
		public static DateTimeFormatInfo GetDateTimeFormatInfo(this IFormatProvider formatProvider) => (DateTimeFormatInfo)formatProvider.GetFormat(typeof(DateTimeFormatInfo));
	}
}
