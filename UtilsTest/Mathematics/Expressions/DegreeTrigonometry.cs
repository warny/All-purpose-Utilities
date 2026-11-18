using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using Utils.Expressions;

namespace UtilsTest.Mathematics.Expressions;

/// <summary>
/// A test type that implements <see cref="ITrigonometricFunctions{TSelf}"/> for <see cref="double"/> explicitly, so that the
/// call signatures of the trigonometric rules (<c>ExpressionCallSignature(typeof(ITrigonometricFunctions&lt;&gt;), "Sin")</c>,
/// which test the declaring type and the name only) match its own public methods: <see cref="Sin(double)"/> and
/// <see cref="Cos(double)"/> work in degrees, and <see cref="Sin(double, double)"/> and <see cref="Cos(double, double)"/> take
/// a second argument. None of them is <see cref="double.Sin"/> or <see cref="double.Cos"/>, so no trigonometric identity
/// applies to them. The interface members are never called.
/// </summary>
[SuppressMessage("Design", "CA1000", Justification = "Test asset.")]
public sealed class DegreeTrigonometry : ITrigonometricFunctions<double>
{
    /// <summary>The sine of an angle in degrees.</summary>
    /// <param name="degrees">The angle in degrees.</param>
    /// <returns>Its sine.</returns>
    [Deterministic]
    public static double Sin(double degrees) => Math.Sin(degrees * Math.PI / 180);

    /// <summary>The cosine of an angle in degrees.</summary>
    /// <param name="degrees">The angle in degrees.</param>
    /// <returns>Its cosine.</returns>
    [Deterministic]
    public static double Cos(double degrees) => Math.Cos(degrees * Math.PI / 180);

    /// <summary>The tangent of an angle in degrees.</summary>
    /// <param name="degrees">The angle in degrees.</param>
    /// <returns>Its tangent.</returns>
    [Deterministic]
    public static double Tan(double degrees) => Math.Tan(degrees * Math.PI / 180);

    /// <summary>A scaled sine: <c>scale * sin(x)</c>.</summary>
    /// <param name="x">The angle in radians.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The scaled sine.</returns>
    [Deterministic]
    public static double Sin(double x, double scale) => scale * Math.Sin(x);

    /// <summary>A scaled cosine: <c>scale * cos(x)</c>.</summary>
    /// <param name="x">The angle in radians.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The scaled cosine.</returns>
    [Deterministic]
    public static double Cos(double x, double scale) => scale * Math.Cos(x);

    /// <summary>Never called: the interface members only make the type a trigonometric one for the rules.</summary>
    /// <returns>Never returns.</returns>
    private static NotSupportedException NotUsed() => new("Test asset: interface members are not used.");

    static double ITrigonometricFunctions<double>.Acos(double x) => throw NotUsed();
    static double ITrigonometricFunctions<double>.AcosPi(double x) => throw NotUsed();
    static double ITrigonometricFunctions<double>.Asin(double x) => throw NotUsed();
    static double ITrigonometricFunctions<double>.AsinPi(double x) => throw NotUsed();
    static double ITrigonometricFunctions<double>.Atan(double x) => throw NotUsed();
    static double ITrigonometricFunctions<double>.AtanPi(double x) => throw NotUsed();
    static double ITrigonometricFunctions<double>.Cos(double x) => throw NotUsed();
    static double ITrigonometricFunctions<double>.CosPi(double x) => throw NotUsed();
    static double ITrigonometricFunctions<double>.Sin(double x) => throw NotUsed();
    static (double Sin, double Cos) ITrigonometricFunctions<double>.SinCos(double x) => throw NotUsed();
    static (double SinPi, double CosPi) ITrigonometricFunctions<double>.SinCosPi(double x) => throw NotUsed();
    static double ITrigonometricFunctions<double>.SinPi(double x) => throw NotUsed();
    static double ITrigonometricFunctions<double>.Tan(double x) => throw NotUsed();
    static double ITrigonometricFunctions<double>.TanPi(double x) => throw NotUsed();
    static double IFloatingPointConstants<double>.E => throw NotUsed();
    static double IFloatingPointConstants<double>.Pi => throw NotUsed();
    static double IFloatingPointConstants<double>.Tau => throw NotUsed();
    static double INumberBase<double>.One => throw NotUsed();
    static int INumberBase<double>.Radix => throw NotUsed();
    static double INumberBase<double>.Zero => throw NotUsed();
    static double IAdditiveIdentity<double, double>.AdditiveIdentity => throw NotUsed();
    static double IMultiplicativeIdentity<double, double>.MultiplicativeIdentity => throw NotUsed();
    static double INumberBase<double>.Abs(double value) => throw NotUsed();
    static bool INumberBase<double>.IsCanonical(double value) => throw NotUsed();
    static bool INumberBase<double>.IsComplexNumber(double value) => throw NotUsed();
    static bool INumberBase<double>.IsEvenInteger(double value) => throw NotUsed();
    static bool INumberBase<double>.IsFinite(double value) => throw NotUsed();
    static bool INumberBase<double>.IsImaginaryNumber(double value) => throw NotUsed();
    static bool INumberBase<double>.IsInfinity(double value) => throw NotUsed();
    static bool INumberBase<double>.IsInteger(double value) => throw NotUsed();
    static bool INumberBase<double>.IsNaN(double value) => throw NotUsed();
    static bool INumberBase<double>.IsNegative(double value) => throw NotUsed();
    static bool INumberBase<double>.IsNegativeInfinity(double value) => throw NotUsed();
    static bool INumberBase<double>.IsNormal(double value) => throw NotUsed();
    static bool INumberBase<double>.IsOddInteger(double value) => throw NotUsed();
    static bool INumberBase<double>.IsPositive(double value) => throw NotUsed();
    static bool INumberBase<double>.IsPositiveInfinity(double value) => throw NotUsed();
    static bool INumberBase<double>.IsRealNumber(double value) => throw NotUsed();
    static bool INumberBase<double>.IsSubnormal(double value) => throw NotUsed();
    static bool INumberBase<double>.IsZero(double value) => throw NotUsed();
    static double INumberBase<double>.MaxMagnitude(double x, double y) => throw NotUsed();
    static double INumberBase<double>.MaxMagnitudeNumber(double x, double y) => throw NotUsed();
    static double INumberBase<double>.MinMagnitude(double x, double y) => throw NotUsed();
    static double INumberBase<double>.MinMagnitudeNumber(double x, double y) => throw NotUsed();
    static double INumberBase<double>.Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider) => throw NotUsed();
    static double INumberBase<double>.Parse(string s, NumberStyles style, IFormatProvider? provider) => throw NotUsed();
    static double ISpanParsable<double>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => throw NotUsed();
    static double IParsable<double>.Parse(string s, IFormatProvider? provider) => throw NotUsed();
    static bool INumberBase<double>.TryConvertFromChecked<TOther>(TOther value, out double result) => throw NotUsed();
    static bool INumberBase<double>.TryConvertFromSaturating<TOther>(TOther value, out double result) => throw NotUsed();
    static bool INumberBase<double>.TryConvertFromTruncating<TOther>(TOther value, out double result) => throw NotUsed();
    static bool INumberBase<double>.TryConvertToChecked<TOther>(double value, [MaybeNullWhen(false)] out TOther result) => throw NotUsed();
    static bool INumberBase<double>.TryConvertToSaturating<TOther>(double value, [MaybeNullWhen(false)] out TOther result) => throw NotUsed();
    static bool INumberBase<double>.TryConvertToTruncating<TOther>(double value, [MaybeNullWhen(false)] out TOther result) => throw NotUsed();
    static bool INumberBase<double>.TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out double result) => throw NotUsed();
    static bool INumberBase<double>.TryParse([NotNullWhen(true)] string? s, NumberStyles style, IFormatProvider? provider, out double result) => throw NotUsed();
    static bool ISpanParsable<double>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out double result) => throw NotUsed();
    static bool IParsable<double>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out double result) => throw NotUsed();
    static double IAdditionOperators<double, double, double>.operator +(double left, double right) => throw NotUsed();
    static double IDecrementOperators<double>.operator --(double value) => throw NotUsed();
    static double IDivisionOperators<double, double, double>.operator /(double left, double right) => throw NotUsed();
    static bool IEqualityOperators<double, double, bool>.operator ==(double left, double right) => throw NotUsed();
    static bool IEqualityOperators<double, double, bool>.operator !=(double left, double right) => throw NotUsed();
    static double IIncrementOperators<double>.operator ++(double value) => throw NotUsed();
    static double IMultiplyOperators<double, double, double>.operator *(double left, double right) => throw NotUsed();
    static double ISubtractionOperators<double, double, double>.operator -(double left, double right) => throw NotUsed();
    static double IUnaryNegationOperators<double, double>.operator -(double value) => throw NotUsed();
    static double IUnaryPlusOperators<double, double>.operator +(double value) => throw NotUsed();
    bool IEquatable<double>.Equals(double other) => throw NotUsed();
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) => throw NotUsed();
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => throw NotUsed();
}
