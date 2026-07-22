using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Utils.OData.Metadatas;

namespace Utils.OData;

/// <summary>
/// Provides helpers to resolve EDM field converters that materialize JSON nodes into CLR values.
/// </summary>
internal static class EdmFieldConverterRegistry
{
    /// <summary>
    /// Describes a converter able to transform a JSON node into a CLR value of the expected type.
    /// </summary>
    internal sealed record EdmFieldConverter(Type ClrType, Func<JsonNode?, object> Converter);

    // OData Edm.Duration lexical form: -?P[nD][T[nH][nM][n[.frac]S]]
    // Years and months are not supported in OData Edm.Duration.
    private static readonly Regex IsoXDurationRegex = new(
        @"^(-?)P(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly EdmFieldConverter DefaultConverter = new(
            typeof(string),
            node => node is null ? DBNull.Value : (object)(ReadNodeAsString(node) ?? string.Empty));

    private static readonly IReadOnlyDictionary<string, EdmFieldConverter> Converters =
            new Dictionary<string, EdmFieldConverter>(StringComparer.OrdinalIgnoreCase)
            {
                ["Edm.Binary"] = new(typeof(byte[]), node => ConvertBinary(node)),
                ["Edm.Boolean"] = new(typeof(bool), node => ConvertTyped(node, typeof(bool))),
                ["Edm.Byte"] = new(typeof(byte), node => ConvertTyped(node, typeof(byte))),
                ["Edm.SByte"] = new(typeof(sbyte), node => ConvertTyped(node, typeof(sbyte))),
                ["Edm.Int16"] = new(typeof(short), node => ConvertTyped(node, typeof(short))),
                ["Edm.Int32"] = new(typeof(int), node => ConvertTyped(node, typeof(int))),
                ["Edm.Int64"] = new(typeof(long), node => ConvertTyped(node, typeof(long))),
                ["Edm.Single"] = new(typeof(float), node => ConvertTyped(node, typeof(float))),
                ["Edm.Double"] = new(typeof(double), node => ConvertTyped(node, typeof(double))),
                ["Edm.Decimal"] = new(typeof(decimal), node => ConvertTyped(node, typeof(decimal))),
                ["Edm.Guid"] = new(typeof(Guid), node => ConvertGuid(node)),
                // Item 23: Edm.Date has no time-of-day component; mapped to DateOnly.
                ["Edm.Date"] = new(typeof(DateOnly), node => ConvertDate(node)),
                ["Edm.DateTimeOffset"] = new(typeof(DateTimeOffset), node => ConvertDateTimeOffset(node)),
                // Item 24: Edm.TimeOfDay is a clock time (no date); mapped to TimeOnly.
                ["Edm.TimeOfDay"] = new(typeof(TimeOnly), node => ConvertTimeOfDay(node)),
                // Item 24: Edm.Duration uses ISO 8601 P…T… lexical form; dedicated parser.
                ["Edm.Duration"] = new(typeof(TimeSpan), node => ConvertDuration(node))
            };

    /// <summary>
    /// Resolves the converter for the specified EDM type name (used in tests and low-level callers).
    /// </summary>
    /// <param name="edmType">OData EDM type string (e.g. <c>"Edm.Int32"</c>).</param>
    /// <returns>The converter describing how to materialize a value of that EDM type.</returns>
    internal static EdmFieldConverter ResolveByEdmType(string? edmType)
        => Resolve(edmType);

    /// <summary>
    /// Resolves the converter to use for the specified EDM property.
    /// </summary>
    /// <param name="property">Property extracted from the metadata document.</param>
    /// <returns>The converter describing how to materialize the property value.</returns>
    public static EdmFieldConverter Resolve(Property? property)
        => Resolve(property?.Type);

    private static EdmFieldConverter Resolve(string? edmType)
    {
        if (string.IsNullOrWhiteSpace(edmType))
        {
            return DefaultConverter;
        }

        // Item 25: Collection types and other complex/unknown EDM types are serialized as their
        // JSON string representation.  They use the DefaultConverter intentionally and the
        // caller receives a string (or DBNull for null nodes).  This is documented rather than
        // silently hidden.
        if (edmType.StartsWith("Collection(", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultConverter;
        }

        if (Converters.TryGetValue(edmType, out EdmFieldConverter? converter) && converter is not null)
        {
            return converter;
        }

        // Unknown primitive, enum, complex, or spatial type: fall back to string representation.
        return DefaultConverter;
    }

    // -----------------------------------------------------------------------
    // Type-specific converters
    // -----------------------------------------------------------------------

    /// <summary>
    /// Converts a JSON node to the specified numeric or boolean CLR type.
    /// Returns <see cref="DBNull.Value"/> on null input; returns <see cref="DBNull.Value"/>
    /// when conversion fails (items 21 and 22: never returns a value of the wrong CLR type).
    /// </summary>
    private static object ConvertTyped(JsonNode? node, Type targetType)
    {
        if (node is null)
            return DBNull.Value;

        try
        {
            object? value = node.Deserialize(targetType);
            return value ?? DBNull.Value;
        }
        catch (JsonException) { }
        catch (FormatException) { }
        catch (OverflowException) { }
        catch (InvalidCastException) { }

        string? raw = ReadNodeAsString(node);
        if (string.IsNullOrEmpty(raw))
            return DBNull.Value;

        try
        {
            return Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
        }
        catch (FormatException) { }
        catch (OverflowException) { }
        catch (InvalidCastException) { }

        // Items 21+22: all typed fallbacks exhausted — return DBNull rather than returning
        // raw (a string) under a non-string column contract, which caused runtime type mismatches.
        return DBNull.Value;
    }

    /// <summary>
    /// Converts a JSON node to <see cref="byte[]"/>.
    /// Accepts Base64-encoded strings as a fallback (standard OData binary encoding).
    /// </summary>
    private static object ConvertBinary(JsonNode? node)
    {
        if (node is null)
            return DBNull.Value;

        try
        {
            byte[]? value = node.Deserialize<byte[]>();
            if (value is not null)
                return value;
        }
        catch (JsonException) { }

        string? raw = ReadNodeAsString(node);
        if (string.IsNullOrEmpty(raw))
            return DBNull.Value;

        try
        {
            return Convert.FromBase64String(raw);
        }
        catch (FormatException) { }

        // Raw bytes from UTF-8 encoding as last resort.
        return System.Text.Encoding.UTF8.GetBytes(raw);
    }

    /// <summary>
    /// Converts a JSON node to <see cref="Guid"/>.
    /// </summary>
    private static object ConvertGuid(JsonNode? node)
    {
        if (node is null)
            return DBNull.Value;

        if (node is JsonValue jv && jv.TryGetValue<Guid>(out Guid directGuid))
            return directGuid;

        string? raw = ReadNodeAsString(node);
        if (!string.IsNullOrEmpty(raw) && Guid.TryParse(raw, out Guid parsed))
            return parsed;

        return DBNull.Value;
    }

    /// <summary>
    /// Converts a JSON node to <see cref="DateOnly"/> for <c>Edm.Date</c> values
    /// (item 23: avoids the artificial midnight and Kind ambiguity of <see cref="DateTime"/>).
    /// </summary>
    private static object ConvertDate(JsonNode? node)
    {
        if (node is null)
            return DBNull.Value;

        string? raw = ReadNodeAsString(node);
        if (string.IsNullOrEmpty(raw))
            return DBNull.Value;

        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
            return date;

        // Wider fallback for partial ISO 8601 dates embedded in full timestamps.
        if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly fallback))
            return fallback;

        return DBNull.Value;
    }

    /// <summary>
    /// Converts a JSON node to <see cref="DateTimeOffset"/> for <c>Edm.DateTimeOffset</c> values.
    /// </summary>
    private static object ConvertDateTimeOffset(JsonNode? node)
    {
        if (node is null)
            return DBNull.Value;

        if (node is JsonValue jv && jv.TryGetValue<DateTimeOffset>(out DateTimeOffset direct))
            return direct;

        string? raw = ReadNodeAsString(node);
        if (!string.IsNullOrEmpty(raw)
                && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset parsed))
        {
            return parsed;
        }

        return DBNull.Value;
    }

    /// <summary>
    /// Converts a JSON node to <see cref="TimeOnly"/> for <c>Edm.TimeOfDay</c> values
    /// (item 24: dedicated clock-time parser; <c>TimeSpan.TryParse</c> is not appropriate here).
    /// </summary>
    private static object ConvertTimeOfDay(JsonNode? node)
    {
        if (node is null)
            return DBNull.Value;

        string? raw = ReadNodeAsString(node);
        if (string.IsNullOrEmpty(raw))
            return DBNull.Value;

        // Edm.TimeOfDay lexical form: HH:MM:SS[.fractionalSeconds]
        ReadOnlySpan<char> formats = default;
        string[] fmts = ["HH:mm:ss", "HH:mm:ss.f", "HH:mm:ss.ff", "HH:mm:ss.fff",
                         "HH:mm:ss.ffff", "HH:mm:ss.fffff", "HH:mm:ss.ffffff", "HH:mm:ss.fffffff"];
        if (TimeOnly.TryParseExact(raw, fmts, CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly time))
            return time;

        return DBNull.Value;
    }

    /// <summary>
    /// Converts a JSON node to <see cref="TimeSpan"/> for <c>Edm.Duration</c> values
    /// (item 24: dedicated ISO 8601 duration parser; OData duration uses the P…T… form
    /// that is not reliably parsed by <c>TimeSpan.TryParse</c>).
    /// </summary>
    /// <remarks>
    /// Components are accumulated as ticks using <see langword="checked"/> arithmetic so that
    /// overflowing durations return <see cref="DBNull.Value"/> instead of throwing.
    /// The fractional-seconds part is converted via <see cref="decimal"/> to avoid the
    /// floating-point rounding that caused millisecond values of 1000 (e.g. <c>PT0.9999S</c>).
    /// </remarks>
    private static object ConvertDuration(JsonNode? node)
    {
        if (node is null)
            return DBNull.Value;

        string? raw = ReadNodeAsString(node);
        if (string.IsNullOrEmpty(raw))
            return DBNull.Value;

        var match = IsoXDurationRegex.Match(raw);
        if (!match.Success)
            return DBNull.Value;

        try
        {
            bool negative = match.Groups[1].Value == "-";

            long days = match.Groups[2].Success
                ? long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)
                : 0L;
            long hours = match.Groups[3].Success
                ? long.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
                : 0L;
            long minutes = match.Groups[4].Success
                ? long.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture)
                : 0L;

            // Use decimal to preserve sub-millisecond precision and avoid the
            // floating-point rounding bug where PT0.9999S → ms=1000 (invalid).
            long secondTicks = 0L;
            if (match.Groups[5].Success)
            {
                decimal secDecimal = decimal.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);
                secondTicks = checked((long)(secDecimal * TimeSpan.TicksPerSecond));
            }

            long ticks;
            checked
            {
                ticks = days * TimeSpan.TicksPerDay
                      + hours * TimeSpan.TicksPerHour
                      + minutes * TimeSpan.TicksPerMinute
                      + secondTicks;
            }

            return new TimeSpan(negative ? -ticks : ticks);
        }
        catch (OverflowException)
        {
            return DBNull.Value;
        }
        catch (FormatException)
        {
            return DBNull.Value;
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads the specified JSON node as an invariant string representation to enable custom parsing.
    /// </summary>
    private static string? ReadNodeAsString(JsonNode node)
    {
        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<string>(out string? stringValue))
                return stringValue;

            if (jsonValue.TryGetValue<Guid>(out Guid guidValue))
                return guidValue.ToString();

            if (jsonValue.TryGetValue<DateTimeOffset>(out DateTimeOffset dtoValue))
                return dtoValue.ToString("O", CultureInfo.InvariantCulture);

            if (jsonValue.TryGetValue<DateTime>(out DateTime dateValue))
                return dateValue.ToString("O", CultureInfo.InvariantCulture);

            if (jsonValue.TryGetValue<decimal>(out decimal decimalValue))
                return decimalValue.ToString(CultureInfo.InvariantCulture);

            if (jsonValue.TryGetValue<double>(out double doubleValue))
                return doubleValue.ToString(CultureInfo.InvariantCulture);

            if (jsonValue.TryGetValue<long>(out long longValue))
                return longValue.ToString(CultureInfo.InvariantCulture);

            if (jsonValue.TryGetValue<bool>(out bool boolValue))
                return boolValue ? bool.TrueString : bool.FalseString;
        }

        return node.ToString();
    }
}
