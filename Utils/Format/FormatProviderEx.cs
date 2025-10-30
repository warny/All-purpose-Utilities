using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Format
{
    /// <summary>
    /// Extension helpers that expose strongly typed <see cref="IFormatProvider"/> data
    /// such as <see cref="TextInfo"/> or <see cref="NumberFormatInfo"/> without repeatedly
    /// casting the results of <see cref="IFormatProvider.GetFormat(Type)"/>.
    /// </summary>
    public static class FormatProviderEx
    {
        /// <summary>
        /// Retrieves the object of type <typeparamref name="T"/> exposed by the
        /// supplied <paramref name="formatProvider"/>.
        /// </summary>
        /// <typeparam name="T">The type of formatting service to obtain.</typeparam>
        /// <param name="formatProvider">The source provider.</param>
        /// <returns>The requested format instance.</returns>
        public static T GetFormat<T>(this IFormatProvider formatProvider) => (T)formatProvider.GetFormat(typeof(T));

        /// <summary>
        /// Retrieves the <see cref="TextInfo"/> associated with the
        /// <paramref name="formatProvider"/>.
        /// </summary>
        /// <param name="formatProvider">The source provider.</param>
        /// <returns>The <see cref="TextInfo"/> instance.</returns>
        public static TextInfo GetTextInfo(this IFormatProvider formatProvider) => (TextInfo)formatProvider.GetFormat(typeof(TextInfo));

        /// <summary>
        /// Retrieves the <see cref="NumberFormatInfo"/> associated with the
        /// <paramref name="formatProvider"/>.
        /// </summary>
        /// <param name="formatProvider">The source provider.</param>
        /// <returns>The <see cref="NumberFormatInfo"/> instance.</returns>
        public static NumberFormatInfo GetNumberFormatInfo(this IFormatProvider formatProvider) => (NumberFormatInfo)formatProvider.GetFormat(typeof(NumberFormatInfo));

        /// <summary>
        /// Retrieves the <see cref="CompareInfo"/> associated with the
        /// <paramref name="formatProvider"/>.
        /// </summary>
        /// <param name="formatProvider">The source provider.</param>
        /// <returns>The <see cref="CompareInfo"/> instance.</returns>
        public static CompareInfo GetCompareInfo(this IFormatProvider formatProvider) => (CompareInfo)formatProvider.GetFormat(typeof(CompareInfo));

        /// <summary>
        /// Retrieves the <see cref="DateTimeFormatInfo"/> associated with the
        /// <paramref name="formatProvider"/>.
        /// </summary>
        /// <param name="formatProvider">The source provider.</param>
        /// <returns>The <see cref="DateTimeFormatInfo"/> instance.</returns>
        public static DateTimeFormatInfo GetDateTimeFormatInfo(this IFormatProvider formatProvider) => (DateTimeFormatInfo)formatProvider.GetFormat(typeof(DateTimeFormatInfo));
    }
}
