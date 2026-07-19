using System;
using System.Collections.Generic;
using System.Globalization;

namespace Utils.Parser.Antlr4.Common;

/// <summary>Converts parsed simple literals to the built-in allowlist supported by rule-call binding.</summary>
public static class ParserLiteralTypeConverter
{
    private static readonly IReadOnlyDictionary<string, ParserLiteralTargetType> TargetTypes = new Dictionary<string, ParserLiteralTargetType>(StringComparer.Ordinal)
    {
        ["bool"] = new ParserLiteralTargetType(typeof(bool), false), ["System.Boolean"] = new ParserLiteralTargetType(typeof(bool), false),
        ["byte"] = new ParserLiteralTargetType(typeof(byte), false), ["System.Byte"] = new ParserLiteralTargetType(typeof(byte), false),
        ["sbyte"] = new ParserLiteralTargetType(typeof(sbyte), false), ["System.SByte"] = new ParserLiteralTargetType(typeof(sbyte), false),
        ["short"] = new ParserLiteralTargetType(typeof(short), false), ["System.Int16"] = new ParserLiteralTargetType(typeof(short), false),
        ["ushort"] = new ParserLiteralTargetType(typeof(ushort), false), ["System.UInt16"] = new ParserLiteralTargetType(typeof(ushort), false),
        ["int"] = new ParserLiteralTargetType(typeof(int), false), ["System.Int32"] = new ParserLiteralTargetType(typeof(int), false),
        ["uint"] = new ParserLiteralTargetType(typeof(uint), false), ["System.UInt32"] = new ParserLiteralTargetType(typeof(uint), false),
        ["long"] = new ParserLiteralTargetType(typeof(long), false), ["System.Int64"] = new ParserLiteralTargetType(typeof(long), false),
        ["ulong"] = new ParserLiteralTargetType(typeof(ulong), false), ["System.UInt64"] = new ParserLiteralTargetType(typeof(ulong), false),
        ["float"] = new ParserLiteralTargetType(typeof(float), false), ["System.Single"] = new ParserLiteralTargetType(typeof(float), false),
        ["double"] = new ParserLiteralTargetType(typeof(double), false), ["System.Double"] = new ParserLiteralTargetType(typeof(double), false),
        ["decimal"] = new ParserLiteralTargetType(typeof(decimal), false), ["System.Decimal"] = new ParserLiteralTargetType(typeof(decimal), false),
        ["char"] = new ParserLiteralTargetType(typeof(char), false), ["System.Char"] = new ParserLiteralTargetType(typeof(char), false),
        ["string"] = new ParserLiteralTargetType(typeof(string), true), ["System.String"] = new ParserLiteralTargetType(typeof(string), true),
        ["object"] = new ParserLiteralTargetType(typeof(object), true), ["System.Object"] = new ParserLiteralTargetType(typeof(object), true),
    };
    private static readonly HashSet<Type> IntegralTypes = new HashSet<Type> { typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong) };

    /// <summary>Converts one supported simple-literal value to the requested built-in target type.</summary>
    public static ParserLiteralConversionResult Convert(object? literalValue, string rawDeclaredType)
    {
        if (!TryNormalize(rawDeclaredType, out ParserLiteralTargetType target, out bool nullable, out string? error)) return Failure(error!);
        if (literalValue is null) return target.IsReferenceType || nullable ? Success(null) : Failure($"Null cannot bind to non-nullable type '{rawDeclaredType.Trim()}'.");
        if (!IsSupportedLiteralValue(literalValue)) return Failure($"Source value type '{literalValue.GetType().FullName}' is not a supported simple literal type.");
        Type targetType = target.RuntimeType;
        Type sourceType = literalValue.GetType();
        if (targetType == typeof(object) || targetType == sourceType) return Success(literalValue);
        if (targetType == typeof(string) && literalValue is char ch) return Success(ch.ToString());
        if (targetType == typeof(char) && literalValue is string text) return text.Length == 1 ? Success(text[0]) : Failure("Only a one-character string can bind to char.");
        if (IntegralTypes.Contains(targetType)) return ConvertIntegral(literalValue, targetType);
        if (targetType == typeof(float)) return ConvertSingle(literalValue);
        if (targetType == typeof(double)) return ConvertDouble(literalValue);
        if (targetType == typeof(decimal)) return ConvertDecimal(literalValue);
        return Failure($"A {sourceType.Name} literal cannot bind to '{rawDeclaredType.Trim()}'.");
    }

    /// <summary>Determines whether a declared type is in the supported allowlist.</summary>
    public static bool IsSupportedDeclaredType(string? rawDeclaredType) => rawDeclaredType is not null && TryNormalize(rawDeclaredType, out _, out _, out _);

    private static bool TryNormalize(string rawDeclaredType, out ParserLiteralTargetType target, out bool nullable, out string? error)
    {
        target = default; nullable = false; error = null;
        if (rawDeclaredType is null) { error = "The declared parameter type is unavailable."; return false; }
        string normalized = rawDeclaredType.Trim();
        if (normalized.EndsWith("?", StringComparison.Ordinal)) { nullable = true; normalized = normalized.Substring(0, normalized.Length - 1); }
        if (normalized.Length == 0 || normalized != normalized.Trim() || !TargetTypes.TryGetValue(normalized, out target)) { error = $"Declared type '{rawDeclaredType.Trim()}' is not in the supported built-in type allowlist."; return false; }
        return true;
    }
    private static bool IsSupportedLiteralValue(object value) => value is bool || value is int || value is long || value is double || value is string || value is char;
    private static ParserLiteralConversionResult ConvertIntegral(object value, Type targetType)
    {
        if (!(value is int) && !(value is long)) return Failure($"A {value.GetType().Name} literal cannot bind to integral type '{targetType.FullName}'.");
        try { return Success(System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture)); }
        catch (OverflowException) { return Failure($"Integral value '{System.Convert.ToString(value, CultureInfo.InvariantCulture)}' is outside the range of '{targetType.FullName}'."); }
    }
    private static ParserLiteralConversionResult ConvertSingle(object value)
    {
        if (value is double d) { float c = (float)d; return (!float.IsNaN(c) && !float.IsInfinity(c)) && (double)c == d ? Success(c) : Failure("The double literal is not exactly representable as System.Single."); }
        if (value is int i) { float c = i; return (int)c == i ? Success(c) : Failure("The integer literal is not exactly representable as System.Single."); }
        if (value is long l) { float c = l; return (!float.IsNaN(c) && !float.IsInfinity(c)) && (decimal)c == l ? Success(c) : Failure("The long literal is not exactly representable as System.Single."); }
        return Failure($"A {value.GetType().Name} literal cannot bind to System.Single.");
    }
    private static ParserLiteralConversionResult ConvertDouble(object value)
    {
        if (value is int i) return Success((double)i);
        if (value is long l) { double c = l; return (decimal)c == l ? Success(c) : Failure("The long literal is not exactly representable as System.Double."); }
        return Failure($"A {value.GetType().Name} literal cannot bind to System.Double.");
    }
    private static ParserLiteralConversionResult ConvertDecimal(object value) => value is int i ? Success((decimal)i) : value is long l ? Success((decimal)l) : Failure($"A {value.GetType().Name} literal cannot bind to System.Decimal.");
    private static ParserLiteralConversionResult Success(object? value) => new ParserLiteralConversionResult(true, value, null);
    private static ParserLiteralConversionResult Failure(string error) => new ParserLiteralConversionResult(false, null, error);
}

/// <summary>Result of a simple-literal type conversion.</summary>
public readonly struct ParserLiteralConversionResult
{
    /// <summary>Initializes a conversion result.</summary>
    /// <param name="success">Whether conversion succeeded.</param>
    /// <param name="value">Converted value.</param>
    /// <param name="error">Failure reason.</param>
    public ParserLiteralConversionResult(bool success, object? value, string? error) { Success = success; Value = value; Error = error; }
    /// <summary>Gets whether conversion succeeded.</summary>
    public bool Success { get; }
    /// <summary>Gets the converted value.</summary>
    public object? Value { get; }
    /// <summary>Gets the failure reason.</summary>
    public string? Error { get; }
}

/// <summary>Describes one supported literal target type.</summary>
public readonly struct ParserLiteralTargetType
{
    /// <summary>Initializes a literal target type descriptor.</summary>
    /// <param name="runtimeType">Runtime target type.</param>
    /// <param name="isReferenceType">Whether the target accepts null as a reference type.</param>
    public ParserLiteralTargetType(Type runtimeType, bool isReferenceType) { RuntimeType = runtimeType; IsReferenceType = isReferenceType; }
    /// <summary>Gets the runtime target type.</summary>
    public Type RuntimeType { get; }
    /// <summary>Gets whether the target accepts null as a reference type.</summary>
    public bool IsReferenceType { get; }
}
