using System.Globalization;

namespace Utils.Parser.Runtime;

/// <summary>
/// Converts values produced by <see cref="ParserSimpleLiteralParser"/> to a deliberately limited set of built-in declared types.
/// No arbitrary type resolution, reflection-based construction, expression evaluation, or string-to-numeric parsing is performed.
/// </summary>
public static class ParserLiteralTypeConverter
{
    private static readonly IReadOnlyDictionary<string, ParserLiteralTargetType> TargetTypes =
        new Dictionary<string, ParserLiteralTargetType>(StringComparer.Ordinal)
        {
            ["bool"] = new(typeof(bool), false),
            ["System.Boolean"] = new(typeof(bool), false),
            ["byte"] = new(typeof(byte), false),
            ["System.Byte"] = new(typeof(byte), false),
            ["sbyte"] = new(typeof(sbyte), false),
            ["System.SByte"] = new(typeof(sbyte), false),
            ["short"] = new(typeof(short), false),
            ["System.Int16"] = new(typeof(short), false),
            ["ushort"] = new(typeof(ushort), false),
            ["System.UInt16"] = new(typeof(ushort), false),
            ["int"] = new(typeof(int), false),
            ["System.Int32"] = new(typeof(int), false),
            ["uint"] = new(typeof(uint), false),
            ["System.UInt32"] = new(typeof(uint), false),
            ["long"] = new(typeof(long), false),
            ["System.Int64"] = new(typeof(long), false),
            ["ulong"] = new(typeof(ulong), false),
            ["System.UInt64"] = new(typeof(ulong), false),
            ["float"] = new(typeof(float), false),
            ["System.Single"] = new(typeof(float), false),
            ["double"] = new(typeof(double), false),
            ["System.Double"] = new(typeof(double), false),
            ["decimal"] = new(typeof(decimal), false),
            ["System.Decimal"] = new(typeof(decimal), false),
            ["char"] = new(typeof(char), false),
            ["System.Char"] = new(typeof(char), false),
            ["string"] = new(typeof(string), true),
            ["System.String"] = new(typeof(string), true),
            ["object"] = new(typeof(object), true),
            ["System.Object"] = new(typeof(object), true),
        };

    private static readonly HashSet<Type> IntegralTypes =
    [
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong),
    ];

    /// <summary>
    /// Converts one supported simple-literal value to the requested built-in target type.
    /// Integral-to-floating conversions succeed only when converting back preserves the original integer exactly.
    /// </summary>
    /// <param name="literalValue">Value produced by <see cref="ParserSimpleLiteralParser"/>.</param>
    /// <param name="rawDeclaredType">Raw declared type alias or canonical framework type name.</param>
    /// <returns>A success value or a deterministic conservative failure.</returns>
    public static ParserLiteralConversionResult Convert(object? literalValue, string rawDeclaredType)
    {
        if (!TryNormalize(rawDeclaredType, out ParserLiteralTargetType target, out bool isNullable, out string? error))
        {
            return Failure(error!);
        }

        if (literalValue is null)
        {
            return target.IsReferenceType || isNullable
                ? Success(null)
                : Failure($"Null cannot bind to non-nullable type '{rawDeclaredType.Trim()}'.");
        }

        if (!IsSupportedLiteralValue(literalValue))
        {
            return Failure($"Source value type '{literalValue.GetType().FullName}' is not a supported simple literal type.");
        }

        Type targetType = target.RuntimeType;
        Type sourceType = literalValue.GetType();
        if (targetType == typeof(object) || targetType == sourceType)
        {
            return Success(literalValue);
        }

        if (targetType == typeof(string) && literalValue is char character)
        {
            return Success(character.ToString());
        }

        if (targetType == typeof(char) && literalValue is string text)
        {
            return text.Length == 1
                ? Success(text[0])
                : Failure("Only a one-character string can bind to char.");
        }

        if (IntegralTypes.Contains(targetType))
        {
            return ConvertIntegral(literalValue, targetType);
        }

        if (targetType == typeof(float))
        {
            return ConvertSingle(literalValue);
        }

        if (targetType == typeof(double))
        {
            return ConvertDouble(literalValue);
        }

        if (targetType == typeof(decimal))
        {
            return ConvertDecimal(literalValue);
        }

        return Failure($"A {sourceType.Name} literal cannot bind to '{rawDeclaredType.Trim()}'.");
    }

    /// <summary>
    /// Normalizes an exact supported alias or canonical framework name and an optional nullable suffix.
    /// </summary>
    /// <param name="rawDeclaredType">Raw declared type metadata.</param>
    /// <param name="target">Normalized supported target.</param>
    /// <param name="isNullable">Whether a nullable suffix was present.</param>
    /// <param name="error">Deterministic failure reason.</param>
    /// <returns><c>true</c> when the declaration is in the supported grammar; otherwise, <c>false</c>.</returns>
    private static bool TryNormalize(
        string rawDeclaredType,
        out ParserLiteralTargetType target,
        out bool isNullable,
        out string? error)
    {
        target = default;
        isNullable = false;
        error = null;
        if (rawDeclaredType is null)
        {
            error = "The declared parameter type is unavailable.";
            return false;
        }

        string normalized = rawDeclaredType.Trim();
        if (normalized.EndsWith("?", StringComparison.Ordinal))
        {
            isNullable = true;
            normalized = normalized[..^1];
        }

        if (normalized.Length == 0 || normalized != normalized.Trim()
            || !TargetTypes.TryGetValue(normalized, out target))
        {
            error = $"Declared type '{rawDeclaredType.Trim()}' is not in the supported built-in type allowlist.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether a value can originate from the simple-literal parser.
    /// </summary>
    /// <param name="value">Non-null source value.</param>
    /// <returns><c>true</c> for the closed simple-literal source set.</returns>
    private static bool IsSupportedLiteralValue(object value)
        => value is bool or int or long or double or string or char;

    /// <summary>
    /// Performs a checked conversion between integral types without accepting floating-point, Boolean, or string sources.
    /// </summary>
    /// <param name="value">Simple literal value.</param>
    /// <param name="targetType">Allowlisted integral runtime type.</param>
    /// <returns>The checked conversion result.</returns>
    private static ParserLiteralConversionResult ConvertIntegral(object value, Type targetType)
    {
        if (value is not int and not long)
        {
            return Failure($"A {value.GetType().Name} literal cannot bind to integral type '{targetType.FullName}'.");
        }

        try
        {
            return Success(System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture));
        }
        catch (OverflowException)
        {
            return Failure($"Integral value '{System.Convert.ToString(value, CultureInfo.InvariantCulture)}' is outside the range of '{targetType.FullName}'.");
        }
    }

    /// <summary>
    /// Converts an integral or double literal to single precision only when the represented value remains exact.
    /// </summary>
    /// <param name="value">Simple literal value.</param>
    /// <returns>The exact single-precision conversion result.</returns>
    private static ParserLiteralConversionResult ConvertSingle(object value)
    {
        if (value is double doubleValue)
        {
            float converted = (float)doubleValue;
            return float.IsFinite(converted) && (double)converted == doubleValue
                ? Success(converted)
                : Failure("The double literal is not exactly representable as System.Single.");
        }

        if (value is int integer)
        {
            float converted = integer;
            return (int)converted == integer
                ? Success(converted)
                : Failure("The integer literal is not exactly representable as System.Single.");
        }

        if (value is long longInteger)
        {
            float converted = longInteger;
            return float.IsFinite(converted) && (decimal)converted == longInteger
                ? Success(converted)
                : Failure("The long literal is not exactly representable as System.Single.");
        }

        return Failure($"A {value.GetType().Name} literal cannot bind to System.Single.");
    }

    /// <summary>
    /// Converts an integral literal to double precision only when the represented value remains exact.
    /// </summary>
    /// <param name="value">Simple literal value.</param>
    /// <returns>The exact double-precision conversion result.</returns>
    private static ParserLiteralConversionResult ConvertDouble(object value)
    {
        if (value is int integer)
        {
            return Success((double)integer);
        }

        if (value is long longInteger)
        {
            double converted = longInteger;
            return (decimal)converted == longInteger
                ? Success(converted)
                : Failure("The long literal is not exactly representable as System.Double.");
        }

        return Failure($"A {value.GetType().Name} literal cannot bind to System.Double.");
    }

    /// <summary>
    /// Converts integral literals to decimal exactly and rejects floating-point sources.
    /// </summary>
    /// <param name="value">Simple literal value.</param>
    /// <returns>The decimal conversion result.</returns>
    private static ParserLiteralConversionResult ConvertDecimal(object value)
    {
        return value switch
        {
            int integer => Success((decimal)integer),
            long longInteger => Success((decimal)longInteger),
            _ => Failure($"A {value.GetType().Name} literal cannot bind to System.Decimal."),
        };
    }

    /// <summary>
    /// Creates a successful conversion result.
    /// </summary>
    /// <param name="value">Converted value.</param>
    /// <returns>A successful result.</returns>
    private static ParserLiteralConversionResult Success(object? value)
        => new() { Success = true, Value = value };

    /// <summary>
    /// Creates a failed conversion result.
    /// </summary>
    /// <param name="error">Deterministic failure reason.</param>
    /// <returns>A failed result.</returns>
    private static ParserLiteralConversionResult Failure(string error)
        => new() { Success = false, Error = error };

    /// <summary>
    /// Stores normalized allowlisted target metadata.
    /// </summary>
    /// <param name="RuntimeType">Concrete allowlisted runtime type.</param>
    /// <param name="IsReferenceType">Whether null is accepted without a nullable suffix.</param>
    private readonly record struct ParserLiteralTargetType(Type RuntimeType, bool IsReferenceType);
}
