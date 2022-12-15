using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Utils.Globalization
{
	public static class GlobalizationUtils
	{
		public static T GetFormat<T>(this IFormatProvider formatProvider) => (T)formatProvider.GetFormat(typeof(T));
		public static TextInfo GetTextInfo(this IFormatProvider formatProvider)
			=> formatProvider switch {
				TextInfo ti => ti,
				CultureInfo ci => ci.TextInfo,
				_ => null
			};
		
		public static NumberFormatInfo GetNumberFormatInfo(this IFormatProvider formatProvider) 
			=> formatProvider switch {
				NumberFormatInfo nfi => nfi,
				CultureInfo ci => ci.NumberFormat,
				_  => null
			};

		public static DateTimeFormatInfo GetDateTimeFormatInfo(this IFormatProvider formatProvider) 
			=> formatProvider switch {
				DateTimeFormatInfo dtfi => dtfi,
				CultureInfo ci => ci.DateTimeFormat,
				_  => null
			};

		public static CompareInfo GetCompareInfo(this IFormatProvider formatProvider) 
			=> formatProvider switch {
				CompareInfo ci => ci,
				CultureInfo ci => ci.CompareInfo,
				_ => null
			};

		public static CultureInfo GetCultureInfo(this IFormatProvider formatProvider) 
			=> formatProvider switch {
				CultureInfo ci => ci,
				_  => null
			};
	}
}
