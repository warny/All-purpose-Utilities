using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Format;

/// <summary>
/// Provides a customisable <see cref="ICustomFormatter"/> implementation backed by delegates registered per type.
/// </summary>
public class CustomFormatter : NullFormatter
{
    private readonly IDictionary<Type, IDictionary<string, Func<object, IFormatProvider, string>>> typeFormatters =
        new Dictionary<Type, IDictionary<string, Func<object, IFormatProvider, string>>>();

    /// <summary>
    /// Initialises a new instance of the <see cref="CustomFormatter"/> class using the current culture.
    /// </summary>
    public CustomFormatter()
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="CustomFormatter"/> class.
    /// </summary>
    /// <param name="cultureInfo">The culture to use for formatting operations.</param>
    public CustomFormatter(CultureInfo cultureInfo)
        : base(cultureInfo)
    {
    }

    /// <summary>
    /// Add a custom format
    /// </summary>
    /// <typeparam name="T">Type to be formatted</typeparam>
    /// <param name="format">Format identifier</param>
    /// <param name="formatter">Formatter function without a format provider</param>
    public void AddFormatter<T>(string format, Func<T, string> formatter)
    {
        if (!typeFormatters.TryGetValue(typeof(T), out var formatters))
        {
            formatters = new Dictionary<string, Func<object, IFormatProvider, string>>();
            typeFormatters[typeof(T)] = formatters;
        }
        formatters[format] = (object o, IFormatProvider formatProvider) => formatter((T)o);
    }

    /// <summary>
    /// Add a custom format
    /// </summary>
    /// <typeparam name="T">Type to be formatted</typeparam>
    /// <param name="format">Format identifier</param>
    /// <param name="formatter">Formatter function with a format provider</param>
    public void AddFormatter<T>(string format, Func<T, IFormatProvider, string> formatter)
    {
        if (!typeFormatters.TryGetValue(typeof(T), out var formatters))
        {
            formatters = new Dictionary<string, Func<object, IFormatProvider, string>>();
            typeFormatters[typeof(T)] = formatters;
        }
        formatters[format] = (object o, IFormatProvider formatProvider) => formatter((T)o, formatProvider);
    }

    /// <summary>
    /// Formats a string using the registered formatter for the argument type.
    /// </summary>
    /// <param name="format">Format identifier</param>
    /// <param name="arg">Object to be formatted</param>
    /// <param name="formatProvider">format provider</param>
    /// <returns>The formatted string, or the base formatting when no formatter matches.</returns>
    public override string Format(string format, object arg, IFormatProvider formatProvider)
    {
        formatProvider ??= CultureInfo;
        if (arg is null) return "";
        if (format is null)
        {
            return base.Format(format, arg, formatProvider);
        }

        if (typeFormatters.TryGetValue(arg.GetType(), out var formatters) && formatters.TryGetValue(format, out var formatter))
        {
            return formatter(arg, formatProvider);
        }
        return base.Format(format, arg, formatProvider);
    }

}


/// <summary>
/// Provides a minimal <see cref="ICustomFormatter"/> that forwards formatting to the wrapped provider or <see cref="object.ToString()"/>.
/// </summary>
public class NullFormatter : IFormatProvider, ICustomFormatter
{
    /// <summary>
    /// Gets a reusable instance of <see cref="NullFormatter"/> that uses the current culture.
    /// </summary>
    public static NullFormatter Default { get; } = new NullFormatter();

    /// <summary>
    /// Gets the culture used when no explicit provider is supplied.
    /// </summary>
    public CultureInfo CultureInfo { get; }

    /// <summary>
    /// Initialises a new instance of the <see cref="NullFormatter"/> class using the current culture.
    /// </summary>
    public NullFormatter()
        : this(CultureInfo.CurrentCulture)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="NullFormatter"/> class.
    /// </summary>
    /// <param name="cultureInfo">The culture to use as the default when formatting.</param>
    public NullFormatter(CultureInfo cultureInfo)
    {
        CultureInfo = cultureInfo;
    }

    /// <summary>
    /// Retrieves the format object associated with the specified type.
    /// </summary>
    /// <param name="formatType">The requested format type.</param>
    /// <returns>This instance when <paramref name="formatType"/> is <see cref="ICustomFormatter"/>; otherwise <see langword="null"/>.</returns>
    public object GetFormat(Type formatType)
    {
        if (formatType == typeof(ICustomFormatter))
        {
            return this;
        }

        return null;
    }

    /// <summary>
    /// Formats the specified argument using the provided format string.
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <param name="arg">The value to format.</param>
    /// <param name="formatProvider">An optional format provider.</param>
    /// <returns>The formatted representation of <paramref name="arg"/>.</returns>
    public virtual string Format(string format, object arg, IFormatProvider formatProvider) => arg switch
    {
        IFormattable formattable => formattable.ToString(format, formatProvider ?? CultureInfo),
        _ => arg?.ToString() ?? string.Empty,
    };
}
