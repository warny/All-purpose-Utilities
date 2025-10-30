using System.Text;

namespace Utils.Range;

/// <summary>
/// Represents a collection of ranges for double values, with parsing support from string representations.
/// </summary>
public class DoubleRanges : Ranges<double>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DoubleRanges"/> class with no ranges.
    /// </summary>
    public DoubleRanges() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DoubleRanges"/> class by copying an existing set of ranges.
    /// </summary>
    /// <param name="ranges">The ranges to copy into this collection.</param>
    public DoubleRanges(Ranges<double> ranges) : base(ranges) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DoubleRanges"/> class with the specified ranges.
    /// </summary>
    /// <param name="ranges">The ranges to include in the collection.</param>
    public DoubleRanges(params Range<double>[] ranges) : base(ranges) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DoubleRanges"/> class with ranges provided as an enumerable collection.
    /// </summary>
    /// <param name="ranges">The ranges to include in the collection.</param>
    public DoubleRanges(IEnumerable<Range<double>> ranges) : base(ranges) { }

    /// <summary>
    /// Parses a string representation of ranges into a DoubleRanges object using the current culture.
    /// </summary>
    /// <param name="range">The string representation of the ranges.</param>
    /// <returns>A DoubleRanges object containing the parsed ranges.</returns>
    public static DoubleRanges Parse(string range) => Parse(range, System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>
    /// Parses a string representation of ranges into a DoubleRanges object using a specified culture.
    /// </summary>
    /// <param name="range">The string representation of the ranges.</param>
    /// <param name="cultureInfo">The culture information to use for parsing.</param>
    /// <returns>A DoubleRanges object containing the parsed ranges.</returns>
    public static DoubleRanges Parse(string range, System.Globalization.CultureInfo cultureInfo) => Parse(range, cultureInfo.NumberFormat);

    /// <summary>
    /// Parses a string representation of ranges into a DoubleRanges object using a specified number format.
    /// </summary>
    /// <param name="range">The string representation of the ranges.</param>
    /// <param name="formatInfo">The number format information to use for parsing.</param>
    /// <returns>A DoubleRanges object containing the parsed ranges.</returns>
    public static DoubleRanges Parse(string range, System.Globalization.NumberFormatInfo formatInfo) => new DoubleRanges(InnerParse(range, formatInfo));

    /// <summary>
    /// Helper method to parse ranges from a string using specified number format information.
    /// </summary>
    /// <param name="range">The string representation of the ranges.</param>
    /// <param name="formatInfo">The number format information to use for parsing.</param>
    /// <returns>An enumerable collection of Range&lt;double&gt; objects parsed from the string.</returns>
    protected static IEnumerable<Range<double>> InnerParse(string range, System.Globalization.NumberFormatInfo formatInfo)
    {
        // Construct a regex pattern to match double values based on the provided number format.
        string digits = string.Join("", formatInfo.NativeDigits);
        string numberSearch = $"{formatInfo.NegativeSign}?[{digits}]*(\\{formatInfo.NumberDecimalSeparator}[{digits}]*)?";

        // Parse the string into ranges using the constructed pattern.
        return InnerParse(range, numberSearch, new[] { "-", ".." }, s => double.Parse(s, formatInfo));
    }

    /// <summary>
    /// Adds ranges to the collection by parsing them from a string using the current culture.
    /// </summary>
    /// <param name="ranges">The string representation of the ranges to add.</param>
    public void Add(string ranges) => this.Add(ranges, System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>
    /// Adds ranges to the collection by parsing them from a string using a specified culture.
    /// </summary>
    /// <param name="ranges">The string representation of the ranges to add.</param>
    /// <param name="cultureInfo">The culture information to use for parsing.</param>
    public void Add(string ranges, System.Globalization.CultureInfo cultureInfo) => this.Add(ranges, cultureInfo.NumberFormat);

    /// <summary>
    /// Adds ranges to the collection by parsing them from a string using a specified number format.
    /// </summary>
    /// <param name="ranges">The string representation of the ranges to add.</param>
    /// <param name="formatInfo">The number format information to use for parsing.</param>
    public void Add(string ranges, System.Globalization.NumberFormatInfo formatInfo) => this.AddAll(InnerParse(ranges, formatInfo));
}

/// <summary>
/// Represents a collection of ranges for float values, with parsing support from string representations.
/// </summary>
public class SingleRanges : Ranges<float>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SingleRanges"/> class with no ranges.
    /// </summary>
    public SingleRanges() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleRanges"/> class by copying existing ranges.
    /// </summary>
    /// <param name="ranges">The ranges to copy into this collection.</param>
    public SingleRanges(Ranges<float> ranges) : base(ranges) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleRanges"/> class with the provided ranges.
    /// </summary>
    /// <param name="ranges">The ranges to include in the collection.</param>
    public SingleRanges(params Range<float>[] ranges) : base(ranges) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleRanges"/> class with ranges supplied as an enumerable collection.
    /// </summary>
    /// <param name="ranges">The ranges to include in the collection.</param>
    public SingleRanges(IEnumerable<Range<float>> ranges) : base(ranges) { }

    /// <summary>
    /// Parses a string representation of ranges into a SingleRanges object using the current culture.
    /// </summary>
    /// <param name="range">The string representation of the ranges.</param>
    /// <returns>A SingleRanges object containing the parsed ranges.</returns>
    public static SingleRanges Parse(string range) => Parse(range, System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>
    /// Parses a string representation of ranges into a SingleRanges object using a specified culture.
    /// </summary>
    /// <param name="range">The string representation of the ranges.</param>
    /// <param name="cultureInfo">The culture information to use for parsing.</param>
    /// <returns>A SingleRanges object containing the parsed ranges.</returns>
    public static SingleRanges Parse(string range, System.Globalization.CultureInfo cultureInfo) => Parse(range, cultureInfo.NumberFormat);

    /// <summary>
    /// Parses a string representation of ranges into a SingleRanges object using a specified number format.
    /// </summary>
    /// <param name="range">The string representation of the ranges.</param>
    /// <param name="formatInfo">The number format information to use for parsing.</param>
    /// <returns>A SingleRanges object containing the parsed ranges.</returns>
    public static SingleRanges Parse(string range, System.Globalization.NumberFormatInfo formatInfo) => new SingleRanges(InnerParse(range, formatInfo));

    /// <summary>
    /// Helper method to parse ranges from a string using specified number format information.
    /// </summary>
    /// <param name="range">The string representation of the ranges.</param>
    /// <param name="formatInfo">The number format information to use for parsing.</param>
    /// <returns>An enumerable collection of Range&lt;float&gt; objects parsed from the string.</returns>
    protected static IEnumerable<Range<float>> InnerParse(string range, System.Globalization.NumberFormatInfo formatInfo)
    {
        // Construct a regex pattern to match float values based on the provided number format.
        string digits = string.Join("", formatInfo.NativeDigits);
        string numberSearch = $"{formatInfo.NegativeSign}?[{digits}]*(\\{formatInfo.NumberDecimalSeparator}[{digits}]*)?";

        // Parse the string into ranges using the constructed pattern.
        return InnerParse(range, numberSearch, new[] { "-", ".." }, s => float.Parse(s, formatInfo));
    }

    /// <summary>
    /// Adds ranges to the collection by parsing them from a string using the current culture.
    /// </summary>
    /// <param name="ranges">The string representation of the ranges to add.</param>
    public void Add(string ranges) => this.Add(ranges, System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>
    /// Adds ranges to the collection by parsing them from a string using a specified culture.
    /// </summary>
    /// <param name="ranges">The string representation of the ranges to add.</param>
    /// <param name="cultureInfo">The culture information to use for parsing.</param>
    public void Add(string ranges, System.Globalization.CultureInfo cultureInfo) => this.Add(ranges, cultureInfo.NumberFormat);

    /// <summary>
    /// Adds ranges to the collection by parsing them from a string using a specified number format.
    /// </summary>
    /// <param name="ranges">The string representation of the ranges to add.</param>
    /// <param name="formatInfo">The number format information to use for parsing.</param>
    public void Add(string ranges, System.Globalization.NumberFormatInfo formatInfo) => this.AddAll(InnerParse(ranges, formatInfo));
}

/// <summary>
/// Represents a collection of ranges for DateTime values, with parsing support from string representations.
/// </summary>
public class DateTimeRanges : Ranges<DateTime>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DateTimeRanges"/> class with no ranges.
    /// </summary>
    public DateTimeRanges() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DateTimeRanges"/> class by copying an existing set of ranges.
    /// </summary>
    /// <param name="ranges">The ranges to copy into this collection.</param>
    public DateTimeRanges(Ranges<DateTime> ranges) : base(ranges) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DateTimeRanges"/> class with the specified ranges.
    /// </summary>
    /// <param name="ranges">The ranges to include in the collection.</param>
    public DateTimeRanges(params Range<DateTime>[] ranges) : base(ranges) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DateTimeRanges"/> class with ranges provided as an enumerable collection.
    /// </summary>
    /// <param name="ranges">The ranges to include in the collection.</param>
    public DateTimeRanges(IEnumerable<Range<DateTime>> ranges) : base(ranges) { }

    /// <summary>
    /// Parses a string representation of ranges into a DateTimeRanges object using the current culture.
    /// </summary>
    /// <param name="range">The string representation of the ranges.</param>
    /// <returns>A DateTimeRanges object containing the parsed ranges.</returns>
    public static DateTimeRanges Parse(string range) => Parse(range, System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>
    /// Parses a string representation of ranges into a DateTimeRanges object using a specified culture.
    /// </summary>
    /// <param name="range">The string representation of the ranges.</param>
    /// <param name="cultureInfo">The culture information to use for parsing.</param>
    /// <returns>A DateTimeRanges object containing the parsed ranges.</returns>
    public static DateTimeRanges Parse(string range, System.Globalization.CultureInfo cultureInfo) => new DateTimeRanges(InnerParse(range, cultureInfo.DateTimeFormat));

    /// <summary>
    /// Helper method to parse ranges from a string using specified DateTime format information.
    /// </summary>
    /// <param name="range">The string representation of the ranges.</param>
    /// <param name="formatInfo">The DateTime format information to use for parsing.</param>
    /// <returns>An enumerable collection of Range&lt;DateTime&gt; objects parsed from the string.</returns>
    protected static IEnumerable<Range<DateTime>> InnerParse(string range, System.Globalization.DateTimeFormatInfo formatInfo)
    {
        // Construct a regex pattern to match DateTime values based on the provided format.
        string dateSearch
            = PatternToRegEx(formatInfo.ShortDatePattern)
            + @"(\s+("
            + PatternToRegEx(formatInfo.LongTimePattern)
            + "|"
            + PatternToRegEx(formatInfo.ShortTimePattern)
            + "))?";

        var separators = new string[] { "-", ".." }.Where(s => !dateSearch.Contains(s)).ToArray();

        // Parse the string into ranges using the constructed pattern.
        return InnerParse(range, dateSearch, separators, s => DateTime.Parse(s, formatInfo));
    }

    /// <summary>
    /// Converts a DateTime format pattern into a regular expression for parsing.
    /// </summary>
    /// <param name="pattern">The DateTime format pattern.</param>
    /// <returns>A string representing the regular expression equivalent of the pattern.</returns>
    private static string PatternToRegEx(string pattern)
    {
        StringBuilder result = new StringBuilder(pattern.Length * 2);  // Pre-allocate a larger buffer to reduce reallocations.
        char last = '\0';
        bool inLiteral = false;

        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];

            if (inLiteral)
            {
                if (c == '\'')
                {
                    inLiteral = false;  // End of literal.
                }
                else
                {
                    result.Append(c);  // Append literal character.
                }
                continue;
            }

            switch (c)
            {
                case '\'':
                    inLiteral = true;  // Start of literal.
                    break;

                case 'y':
                case 'M':
                case 'd':
                case 'H':
                case 'h':
                case 'm':
                case 's':
                case 'f':
                    if (c != last)
                    {
                        result.Append(@"\d+");
                    }
                    break;

                case 't':
                    if (c != last)
                    {
                        result.Append(@"(AM|PM)");
                    }
                    break;

                case ' ':
                    if (c != last)
                    {
                        result.Append(@"\s+");
                    }
                    break;

                case '\\':
                    result.Append(@"\\");
                    break;

                default:
                    result.Append(c);
                    break;
            }

            last = c;
        }

        return result.ToString();
    }

    /// <summary>
    /// Adds ranges to the collection by parsing them from a string using the current culture.
    /// </summary>
    /// <param name="ranges">The string representation of the ranges to add.</param>
    public void Add(string ranges) => this.Add(ranges, System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>
    /// Adds ranges to the collection by parsing them from a string using a specified culture.
    /// </summary>
    /// <param name="ranges">The string representation of the ranges to add.</param>
    /// <param name="cultureInfo">The culture information to use for parsing.</param>
    public void Add(string ranges, System.Globalization.CultureInfo cultureInfo) => this.Add(ranges, cultureInfo.DateTimeFormat);

    /// <summary>
    /// Adds ranges to the collection by parsing them from a string using a specified DateTime format.
    /// </summary>
    /// <param name="ranges">The string representation of the ranges to add.</param>
    /// <param name="formatInfo">The DateTime format information to use for parsing.</param>
    public void Add(string ranges, System.Globalization.DateTimeFormatInfo formatInfo) => this.AddAll(InnerParse(ranges, formatInfo));
}
