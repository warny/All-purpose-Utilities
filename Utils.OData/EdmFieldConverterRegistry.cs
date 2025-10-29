using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    private static readonly EdmFieldConverter DefaultConverter = new(
            typeof(string),
            node => ConvertNode(node, typeof(string)));

    private static readonly IReadOnlyDictionary<string, EdmFieldConverter> Converters =
            new Dictionary<string, EdmFieldConverter>(StringComparer.OrdinalIgnoreCase)
            {
                ["Edm.Binary"] = new(typeof(byte[]), node => ConvertNode(node, typeof(byte[]))),
                ["Edm.Boolean"] = new(typeof(bool), node => ConvertNode(node, typeof(bool))),
                ["Edm.Byte"] = new(typeof(byte), node => ConvertNode(node, typeof(byte))),
                ["Edm.SByte"] = new(typeof(sbyte), node => ConvertNode(node, typeof(sbyte))),
                ["Edm.Int16"] = new(typeof(short), node => ConvertNode(node, typeof(short))),
                ["Edm.Int32"] = new(typeof(int), node => ConvertNode(node, typeof(int))),
                ["Edm.Int64"] = new(typeof(long), node => ConvertNode(node, typeof(long))),
                ["Edm.Single"] = new(typeof(float), node => ConvertNode(node, typeof(float))),
                ["Edm.Double"] = new(typeof(double), node => ConvertNode(node, typeof(double))),
                ["Edm.Decimal"] = new(typeof(decimal), node => ConvertNode(node, typeof(decimal))),
                ["Edm.Guid"] = new(typeof(Guid), node => ConvertNode(node, typeof(Guid))),
                ["Edm.Date"] = new(typeof(DateTime), node => ConvertNode(node, typeof(DateTime))),
                ["Edm.DateTimeOffset"] = new(typeof(DateTimeOffset), node => ConvertNode(node, typeof(DateTimeOffset))),
                ["Edm.TimeOfDay"] = new(typeof(TimeSpan), node => ConvertNode(node, typeof(TimeSpan))),
                ["Edm.Duration"] = new(typeof(TimeSpan), node => ConvertNode(node, typeof(TimeSpan)))
            };

    /// <summary>
    /// Resolves the converter to use for the specified EDM property.
    /// </summary>
    /// <param name="property">Property extracted from the metadata document.</param>
    /// <returns>The converter describing how to materialize the property value.</returns>
    public static EdmFieldConverter Resolve(Property? property)
    {
        string? edmType = property?.Type;
        if (string.IsNullOrWhiteSpace(edmType))
        {
            return DefaultConverter;
        }

        if (edmType.StartsWith("Collection(", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultConverter;
        }

        if (Converters.TryGetValue(edmType, out EdmFieldConverter? converter) && converter is not null)
        {
            return converter;
        }

        return DefaultConverter;
    }

    /// <summary>
    /// Converts the provided JSON node into the specified CLR type using resilient fallbacks.
    /// </summary>
    /// <param name="node">JSON node containing the raw value.</param>
    /// <param name="targetType">Target CLR type expected for the property.</param>
    /// <returns>The converted value or <see cref="DBNull.Value"/> when not available.</returns>
    private static object ConvertNode(JsonNode? node, Type targetType)
    {
        if (targetType is null)
        {
            throw new ArgumentNullException(nameof(targetType));
        }

        if (node is null)
        {
            return DBNull.Value;
        }

        try
        {
            object? value = node.Deserialize(targetType);
            return value ?? DBNull.Value;
        }
        catch
        {
            string? raw = ReadNodeAsString(node);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return DBNull.Value;
            }

            if (targetType == typeof(byte[]))
            {
                try
                {
                    return Convert.FromBase64String(raw);
                }
                catch
                {
                    return System.Text.Encoding.UTF8.GetBytes(raw);
                }
            }

            if (targetType == typeof(Guid) && Guid.TryParse(raw, out Guid guidValue))
            {
                return guidValue;
            }

            if (targetType == typeof(DateTimeOffset)
                    && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset dtoValue))
            {
                return dtoValue;
            }

            if (targetType == typeof(DateTime)
                    && DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dateValue))
            {
                return dateValue;
            }

            if (targetType == typeof(TimeSpan) && TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out TimeSpan timeValue))
            {
                return timeValue;
            }

            try
            {
                return System.Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                return raw;
            }
        }
    }

    /// <summary>
    /// Reads the specified JSON node as an invariant string representation to enable custom parsing.
    /// </summary>
    /// <param name="node">JSON node to convert into a string.</param>
    /// <returns>A string representation of the node suitable for invariant parsing.</returns>
    private static string? ReadNodeAsString(JsonNode node)
    {
        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<string>(out string? stringValue))
            {
                return stringValue;
            }

            if (jsonValue.TryGetValue<Guid>(out Guid guidValue))
            {
                return guidValue.ToString();
            }

            if (jsonValue.TryGetValue<DateTimeOffset>(out DateTimeOffset dtoValue))
            {
                return dtoValue.ToString("O", CultureInfo.InvariantCulture);
            }

            if (jsonValue.TryGetValue<DateTime>(out DateTime dateValue))
            {
                return dateValue.ToString("O", CultureInfo.InvariantCulture);
            }

            if (jsonValue.TryGetValue<decimal>(out decimal decimalValue))
            {
                return decimalValue.ToString(CultureInfo.InvariantCulture);
            }

            if (jsonValue.TryGetValue<double>(out double doubleValue))
            {
                return doubleValue.ToString(CultureInfo.InvariantCulture);
            }

            if (jsonValue.TryGetValue<long>(out long longValue))
            {
                return longValue.ToString(CultureInfo.InvariantCulture);
            }

            if (jsonValue.TryGetValue<bool>(out bool boolValue))
            {
                return boolValue ? bool.TrueString : bool.FalseString;
            }
        }

        return node.ToString();
    }
}
