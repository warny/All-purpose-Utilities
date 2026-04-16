using System.Data;
using System.Globalization;

namespace Utils.Format;

/// <summary>
/// Defines an injectable builder for creating compiled string-format delegates.
/// </summary>
public interface IStringFormatBuilder
{
    /// <summary>
    /// Creates a formatter delegate.
    /// </summary>
    /// <typeparam name="T">Delegate type to build.</typeparam>
    /// <param name="formatString">Interpolated-like format string.</param>
    /// <param name="names">Optional argument names used in format expressions.</param>
    /// <returns>A compiled delegate matching <typeparamref name="T"/>.</returns>
    T Create<T>(string formatString, params string[] names) where T : Delegate;

    /// <summary>
    /// Creates a formatter delegate using an explicit formatter and culture.
    /// </summary>
    /// <typeparam name="T">Delegate type to build.</typeparam>
    /// <param name="formatString">Interpolated-like format string.</param>
    /// <param name="customFormatter">Formatter used for value rendering.</param>
    /// <param name="cultureInfo">Culture used for formatting operations.</param>
    /// <param name="names">Optional argument names used in format expressions.</param>
    /// <returns>A compiled delegate matching <typeparamref name="T"/>.</returns>
    T Create<T>(string formatString, ICustomFormatter? customFormatter, CultureInfo? cultureInfo, params string[] names) where T : Delegate;

    /// <summary>
    /// Creates a formatter delegate using a custom interpolated string handler.
    /// </summary>
    /// <typeparam name="T">Delegate type to build.</typeparam>
    /// <typeparam name="THandler">Interpolated string handler type.</typeparam>
    /// <param name="formatString">Interpolated-like format string.</param>
    /// <param name="names">Optional argument names used in format expressions.</param>
    /// <returns>A compiled delegate matching <typeparamref name="T"/>.</returns>
    T Create<T, THandler>(string formatString, params string[] names) where T : Delegate;

    /// <summary>
    /// Creates a formatter delegate using a custom interpolated string handler and explicit formatter context.
    /// </summary>
    /// <typeparam name="T">Delegate type to build.</typeparam>
    /// <typeparam name="THandler">Interpolated string handler type.</typeparam>
    /// <param name="formatString">Interpolated-like format string.</param>
    /// <param name="customFormatter">Formatter used for value rendering.</param>
    /// <param name="cultureInfo">Culture used for formatting operations.</param>
    /// <param name="names">Optional argument names used in format expressions.</param>
    /// <returns>A compiled delegate matching <typeparamref name="T"/>.</returns>
    T Create<T, THandler>(string formatString, ICustomFormatter? customFormatter, CultureInfo? cultureInfo, params string[] names) where T : Delegate;

    /// <summary>
    /// Creates a formatter function for data records.
    /// </summary>
    /// <param name="formatString">Interpolated-like format string.</param>
    /// <param name="customFormatter">Formatter used for value rendering.</param>
    /// <param name="cultureInfo">Culture used for formatting operations.</param>
    /// <param name="dataRecord">Data record model used to resolve field variables.</param>
    /// <returns>A compiled function that formats an <see cref="IDataRecord"/>.</returns>
    Func<IDataRecord, string> Create(string formatString, ICustomFormatter? customFormatter, CultureInfo? cultureInfo, IDataRecord dataRecord);
}
