using System;
using System.Globalization;
using System.Numerics;

namespace Utils.Numerics;

/// <summary>
/// Represents an arbitrary size rational number similar to JavaScript numbers.
/// Stores values as a fraction of two <see cref="BigInteger"/> instances and
/// provides basic arithmetic operations.
/// </summary>
public readonly struct Number :
    IFloatingPoint<Number>,
    IPowerFunctions<Number>,
    ITrigonometricFunctions<Number>,
    IRootFunctions<Number>,
    IComparable<Number>,
    IComparable
{
    private readonly BigInteger _numerator;
    private readonly BigInteger _denominator;

    /// <summary>
    /// Gets the value zero.
    /// </summary>
    public static Number Zero => new(BigInteger.Zero);

    /// <summary>
    /// Gets the additive identity value.
    /// </summary>
    public static Number AdditiveIdentity => Zero;

    /// <summary>
    /// Gets the value one.
    /// </summary>
    public static Number One => new(BigInteger.One);

    /// <summary>
    /// Gets the multiplicative identity value.
    /// </summary>
    public static Number MultiplicativeIdentity => One;

    /// <summary>
    /// Gets the value negative one.
    /// </summary>
    public static Number NegativeOne => new(BigInteger.MinusOne);

    /// <summary>
    /// Gets the mathematical constant e.
    /// </summary>
    public static Number E => Parse(Math.E.ToString("R", CultureInfo.InvariantCulture));

    /// <summary>
    /// Gets the mathematical constant pi.
    /// </summary>
    public static Number Pi => Parse(Math.PI.ToString("R", CultureInfo.InvariantCulture));

    /// <summary>
    /// Gets the mathematical constant tau (2 * pi).
    /// </summary>
    public static Number Tau => Parse((Math.PI * 2).ToString("R", CultureInfo.InvariantCulture));

    /// <summary>
    /// Initializes a new instance of the <see cref="Number"/> struct from a
    /// numerator and denominator. The fraction is automatically reduced.
    /// </summary>
    /// <param name="numerator">Numerator of the fraction.</param>
    /// <param name="denominator">Denominator of the fraction.</param>
    /// <exception cref="DivideByZeroException">Thrown when <paramref name="denominator"/> is zero.</exception>
    public Number(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero)
            throw new DivideByZeroException();

        if (denominator.Sign < 0)
        {
            numerator = BigInteger.Negate(numerator);
            denominator = BigInteger.Abs(denominator);
        }

        BigInteger gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        _numerator = numerator / gcd;
        _denominator = denominator / gcd;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Number"/> struct from an integer.
    /// </summary>
    /// <param name="value">Integer value to store.</param>
    public Number(BigInteger value) : this(value, BigInteger.One) { }

    /// <summary>
    /// Parses a textual representation of a number.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="provider">Number format provider or <c>null</c> for invariant culture.</param>
    /// <returns>A new <see cref="Number"/> instance.</returns>
    public static Number Parse(string text, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.InvariantCulture;
        NumberFormatInfo info = NumberFormatInfo.GetInstance(provider);
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            BigInteger value = BigInteger.Parse(text[2..], NumberStyles.HexNumber, provider);
            return new Number(value);
        }

        if (text.Contains(info.NumberDecimalSeparator))
        {
            string[] parts = text.Split(info.NumberDecimalSeparator);
            BigInteger integerPart = BigInteger.Parse(parts[0], provider);
            string fractionText = parts[1];
            BigInteger fractionPart = BigInteger.Parse(fractionText, provider);
            BigInteger denominator = BigInteger.Pow(10, fractionText.Length);
            BigInteger numerator = integerPart * denominator + (integerPart.Sign >= 0 ? fractionPart : -fractionPart);
            return new Number(numerator, denominator);
        }

        BigInteger intValue = BigInteger.Parse(text, provider);
        return new Number(intValue);
    }

    /// <summary>
    /// Attempts to parse a textual representation of a number.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="provider">Format provider or <c>null</c> for invariant culture.</param>
    /// <param name="result">Receives the parsed value when successful.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise <c>false</c>.</returns>
    public static bool TryParse(string text, IFormatProvider? provider, out Number result)
    {
        try
        {
            result = Parse(text, provider);
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }

    /// <inheritdoc/>
    static Number ISpanParsable<Number>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => Parse(s.ToString(), provider);

    /// <inheritdoc/>
    static bool ISpanParsable<Number>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Number result)
        => TryParse(s.ToString(), provider, out result);

    /// <inheritdoc/>
    static Number INumberBase<Number>.Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider)
        => Parse(s.ToString(), provider);

    /// <inheritdoc/>
    static Number INumberBase<Number>.Parse(string s, NumberStyles style, IFormatProvider? provider)
        => Parse(s, provider);

    /// <inheritdoc/>
    static bool INumberBase<Number>.TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out Number result)
        => TryParse(s.ToString(), provider, out result);

    /// <inheritdoc/>
    static bool INumberBase<Number>.TryParse(string? s, NumberStyles style, IFormatProvider? provider, out Number result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        return TryParse(s, provider, out result);
    }

    /// <summary>
    /// Converts this number to its decimal representation when possible.
    /// </summary>
    /// <returns>The decimal value.</returns>
    /// <exception cref="OverflowException">Thrown when the value does not fit in a <see cref="decimal"/>.</exception>
    public decimal ToDecimal() => (decimal)_numerator / (decimal)_denominator;

    /// <inheritdoc/>
    public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        formatProvider ??= CultureInfo.InvariantCulture;
        if (_denominator.IsOne)
            return _numerator.ToString(formatProvider);
        try
        {
            decimal value = ToDecimal();
            return value.ToString(format, formatProvider);
        }
        catch (OverflowException)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1}", _numerator, _denominator);
        }
    }

    /// <inheritdoc/>
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        string s = ToString(format.Length == 0 ? null : new string(format), provider);
        if (s.AsSpan().TryCopyTo(destination))
        {
            charsWritten = s.Length;
            return true;
        }

        charsWritten = 0;
        return false;
    }

    /// <summary>
    /// Gets the numerator of this number.
    /// </summary>
    public BigInteger Numerator => _numerator;

    /// <summary>
    /// Gets the denominator of this number.
    /// </summary>
    public BigInteger Denominator => _denominator;

    /// <summary>
    /// Returns the absolute value of the specified number.
    /// </summary>
    public static Number Abs(Number value) => value._numerator.Sign >= 0 ? value : -value;

    /// <inheritdoc/>
    static Number INumberBase<Number>.Abs(Number value) => Abs(value);

    /// <inheritdoc/>
    static bool INumberBase<Number>.IsCanonical(Number value) => true;

    /// <inheritdoc/>
    static bool INumberBase<Number>.IsComplexNumber(Number value) => false;

    /// <inheritdoc/>
    static bool INumberBase<Number>.IsEvenInteger(Number value) => value._denominator.IsOne && value._numerator.IsEven;

    /// <inheritdoc/>
    static bool INumberBase<Number>.IsFinite(Number value) => true;

    /// <inheritdoc/>
    static bool INumberBase<Number>.IsImaginaryNumber(Number value) => false;

    /// <inheritdoc/>
    static bool INumberBase<Number>.IsInfinity(Number value) => false;

    /// <inheritdoc/>
    static bool INumberBase<Number>.IsInteger(Number value) => value._denominator.IsOne;

    /// <inheritdoc/>
    static bool INumberBase<Number>.IsNaN(Number value) => false;

    /// <inheritdoc/>
    static bool INumberBase<Number>.IsNegative(Number value) => value._numerator.Sign < 0;

    /// <inheritdoc/>
    static bool INumberBase<Number>.IsNegativeInfinity(Number value) => false;

    /// <inheritdoc/>
    static bool INumberBase<Number>.IsNormal(Number value) => true;

    /// <inheritdoc/>
    static bool INumberBase<Number>.IsOddInteger(Number value) => value._denominator.IsOne && !value._numerator.IsEven;

    /// <inheritdoc/>
    static bool INumberBase<Number>.IsPositive(Number value) => value._numerator.Sign > 0;

    /// <inheritdoc/>
    static bool INumberBase<Number>.IsPositiveInfinity(Number value) => false;

    /// <inheritdoc/>
    static bool INumberBase<Number>.IsRealNumber(Number value) => true;

    /// <inheritdoc/>
    static bool INumberBase<Number>.IsSubnormal(Number value) => false;

    /// <inheritdoc/>
    static bool INumberBase<Number>.IsZero(Number value) => value._numerator.IsZero;

    /// <inheritdoc/>
    /// <summary>
    /// Returns the value with the greater magnitude.
    /// </summary>
    public static Number MaxMagnitude(Number x, Number y) => Abs(x) >= Abs(y) ? x : y;

    /// <inheritdoc/>
    static Number INumberBase<Number>.MaxMagnitude(Number x, Number y) => MaxMagnitude(x, y);

    /// <inheritdoc/>
    static Number INumberBase<Number>.MaxMagnitudeNumber(Number x, Number y) => MaxMagnitude(x, y);

    /// <inheritdoc/>
    /// <summary>
    /// Returns the value with the smaller magnitude.
    /// </summary>
    public static Number MinMagnitude(Number x, Number y) => Abs(x) <= Abs(y) ? x : y;

    /// <inheritdoc/>
    static Number INumberBase<Number>.MinMagnitude(Number x, Number y) => MinMagnitude(x, y);

    /// <inheritdoc/>
    static Number INumberBase<Number>.MinMagnitudeNumber(Number x, Number y) => MinMagnitude(x, y);

    /// <inheritdoc/>
    static int INumberBase<Number>.Radix => 10;

    /// <summary>
    /// Raises a number to the specified power.
    /// </summary>
    /// <param name="x">Base value.</param>
    /// <param name="y">Exponent value.</param>
    /// <returns><c>x</c> raised to the power <c>y</c>.</returns>
    public static Number Pow(Number x, Number y)
    {
        // Handle simple integer exponents for exact results
        if (y._denominator.IsOne && y._numerator >= 0 && y._numerator <= int.MaxValue)
        {
            int exp = (int)y._numerator;
            BigInteger numerator = BigInteger.Pow(x._numerator, exp);
            BigInteger denominator = BigInteger.Pow(x._denominator, exp);
            return new Number(numerator, denominator);
        }
        if (y._denominator.IsOne && y._numerator < 0 && y._numerator >= int.MinValue)
        {
            int exp = (int)BigInteger.Abs(y._numerator);
            BigInteger numerator = BigInteger.Pow(x._denominator, exp);
            BigInteger denominator = BigInteger.Pow(x._numerator, exp);
            return new Number(numerator, denominator);
        }

        // Fallback to double-based computation for fractional powers
        double baseValue = double.Parse(x.ToString(), CultureInfo.InvariantCulture);
        double expValue = double.Parse(y.ToString(), CultureInfo.InvariantCulture);
        double result = Math.Pow(baseValue, expValue);
        return Parse(result.ToString("R", CultureInfo.InvariantCulture));
    }

    /// <inheritdoc/>
    static Number IPowerFunctions<Number>.Pow(Number x, Number y) => Pow(x, y);

    private static double ToDouble(Number value)
        => double.Parse(value.ToString(), CultureInfo.InvariantCulture);

    private static Number FromDouble(double value)
        => Parse(value.ToString("R", CultureInfo.InvariantCulture));

    /// <inheritdoc/>
    public static Number Sqrt(Number x) => FromDouble(Math.Sqrt(ToDouble(x)));

    /// <inheritdoc/>
    public static Number Cbrt(Number x) => FromDouble(Math.Cbrt(ToDouble(x)));

    /// <inheritdoc/>
    public static Number Hypot(Number x, Number y)
        => FromDouble(Math.Sqrt(Math.Pow(ToDouble(x), 2) + Math.Pow(ToDouble(y), 2)));

    /// <inheritdoc/>
    public static Number RootN(Number x, int n)
        => FromDouble(Math.Pow(ToDouble(x), 1.0 / n));

    /// <inheritdoc/>
    public static Number Sin(Number x) => FromDouble(Math.Sin(ToDouble(x)));

    /// <inheritdoc/>
    public static Number Cos(Number x) => FromDouble(Math.Cos(ToDouble(x)));

    /// <inheritdoc/>
    public static Number Tan(Number x) => FromDouble(Math.Tan(ToDouble(x)));

    /// <inheritdoc/>
    public static Number Asin(Number x) => FromDouble(Math.Asin(ToDouble(x)));

    /// <inheritdoc/>
    public static Number Acos(Number x) => FromDouble(Math.Acos(ToDouble(x)));

    /// <inheritdoc/>
    public static Number Atan(Number x) => FromDouble(Math.Atan(ToDouble(x)));

    /// <inheritdoc/>
    public static Number AsinPi(Number x) => FromDouble(Math.Asin(ToDouble(x)) / Math.PI);

    /// <inheritdoc/>
    public static Number AcosPi(Number x) => FromDouble(Math.Acos(ToDouble(x)) / Math.PI);

    /// <inheritdoc/>
    public static Number AtanPi(Number x) => FromDouble(Math.Atan(ToDouble(x)) / Math.PI);

    /// <inheritdoc/>
    public static Number SinPi(Number x) => FromDouble(Math.Sin(ToDouble(x) * Math.PI));

    /// <inheritdoc/>
    public static Number CosPi(Number x) => FromDouble(Math.Cos(ToDouble(x) * Math.PI));

    /// <inheritdoc/>
    public static Number TanPi(Number x) => FromDouble(Math.Tan(ToDouble(x) * Math.PI));

    /// <inheritdoc/>
    public static (Number Sin, Number Cos) SinCos(Number x)
    {
        double d = ToDouble(x);
        (double s, double c) = Math.SinCos(d);
        return (FromDouble(s), FromDouble(c));
    }

    /// <inheritdoc/>
    public static (Number SinPi, Number CosPi) SinCosPi(Number x)
    {
        double d = ToDouble(x) * Math.PI;
        (double s, double c) = Math.SinCos(d);
        return (FromDouble(s), FromDouble(c));
    }

    /// <inheritdoc/>
    public static Number DegreesToRadians(Number degrees)
        => FromDouble(ToDouble(degrees) * Math.PI / 180.0);

    /// <inheritdoc/>
    public static Number RadiansToDegrees(Number radians)
        => FromDouble(ToDouble(radians) * 180.0 / Math.PI);

    /// <inheritdoc/>
    public static Number Ceiling(Number x) => FromDouble(Math.Ceiling(ToDouble(x)));

    /// <inheritdoc/>
    public static Number Floor(Number x) => FromDouble(Math.Floor(ToDouble(x)));

    /// <inheritdoc/>
    public static Number Round(Number x) => FromDouble(Math.Round(ToDouble(x)));

    /// <inheritdoc/>
    public static Number Round(Number x, int digits) => FromDouble(Math.Round(ToDouble(x), digits));

    /// <inheritdoc/>
    public static Number Round(Number x, MidpointRounding mode) => FromDouble(Math.Round(ToDouble(x), mode));

    /// <inheritdoc/>
    public static Number Round(Number x, int digits, MidpointRounding mode)
        => FromDouble(Math.Round(ToDouble(x), digits, mode));

    /// <inheritdoc/>
    public static Number Truncate(Number x) => FromDouble(Math.Truncate(ToDouble(x)));

    /// <inheritdoc/>
    public static TInteger ConvertToInteger<TInteger>(Number value)
        where TInteger : IBinaryInteger<TInteger>
        => TInteger.CreateTruncating(ToDouble(value));

    /// <inheritdoc/>
    public static TInteger ConvertToIntegerNative<TInteger>(Number value)
        where TInteger : IBinaryInteger<TInteger>
        => TInteger.CreateTruncating(ToDouble(value));

    int IFloatingPoint<Number>.GetExponentByteCount()
        => ((IFloatingPoint<double>)ToDouble(this)).GetExponentByteCount();

    int IFloatingPoint<Number>.GetExponentShortestBitLength()
        => ((IFloatingPoint<double>)ToDouble(this)).GetExponentShortestBitLength();

    int IFloatingPoint<Number>.GetSignificandBitLength()
        => ((IFloatingPoint<double>)ToDouble(this)).GetSignificandBitLength();

    int IFloatingPoint<Number>.GetSignificandByteCount()
        => ((IFloatingPoint<double>)ToDouble(this)).GetSignificandByteCount();

    bool IFloatingPoint<Number>.TryWriteExponentBigEndian(Span<byte> destination, out int bytesWritten)
        => ((IFloatingPoint<double>)ToDouble(this)).TryWriteExponentBigEndian(destination, out bytesWritten);

    bool IFloatingPoint<Number>.TryWriteExponentLittleEndian(Span<byte> destination, out int bytesWritten)
        => ((IFloatingPoint<double>)ToDouble(this)).TryWriteExponentLittleEndian(destination, out bytesWritten);

    bool IFloatingPoint<Number>.TryWriteSignificandBigEndian(Span<byte> destination, out int bytesWritten)
        => ((IFloatingPoint<double>)ToDouble(this)).TryWriteSignificandBigEndian(destination, out bytesWritten);

    bool IFloatingPoint<Number>.TryWriteSignificandLittleEndian(Span<byte> destination, out int bytesWritten)
        => ((IFloatingPoint<double>)ToDouble(this)).TryWriteSignificandLittleEndian(destination, out bytesWritten);

    int IFloatingPoint<Number>.WriteExponentBigEndian(byte[] destination)
        => ((IFloatingPoint<double>)ToDouble(this)).WriteExponentBigEndian(destination);

    int IFloatingPoint<Number>.WriteExponentBigEndian(byte[] destination, int startIndex)
        => ((IFloatingPoint<double>)ToDouble(this)).WriteExponentBigEndian(destination, startIndex);

    int IFloatingPoint<Number>.WriteExponentBigEndian(Span<byte> destination)
        => ((IFloatingPoint<double>)ToDouble(this)).WriteExponentBigEndian(destination);

    int IFloatingPoint<Number>.WriteExponentLittleEndian(byte[] destination)
        => ((IFloatingPoint<double>)ToDouble(this)).WriteExponentLittleEndian(destination);

    int IFloatingPoint<Number>.WriteExponentLittleEndian(byte[] destination, int startIndex)
        => ((IFloatingPoint<double>)ToDouble(this)).WriteExponentLittleEndian(destination, startIndex);

    int IFloatingPoint<Number>.WriteExponentLittleEndian(Span<byte> destination)
        => ((IFloatingPoint<double>)ToDouble(this)).WriteExponentLittleEndian(destination);

    int IFloatingPoint<Number>.WriteSignificandBigEndian(byte[] destination)
        => ((IFloatingPoint<double>)ToDouble(this)).WriteSignificandBigEndian(destination);

    int IFloatingPoint<Number>.WriteSignificandBigEndian(byte[] destination, int startIndex)
        => ((IFloatingPoint<double>)ToDouble(this)).WriteSignificandBigEndian(destination, startIndex);

    int IFloatingPoint<Number>.WriteSignificandBigEndian(Span<byte> destination)
        => ((IFloatingPoint<double>)ToDouble(this)).WriteSignificandBigEndian(destination);

    int IFloatingPoint<Number>.WriteSignificandLittleEndian(byte[] destination)
        => ((IFloatingPoint<double>)ToDouble(this)).WriteSignificandLittleEndian(destination);

    int IFloatingPoint<Number>.WriteSignificandLittleEndian(byte[] destination, int startIndex)
        => ((IFloatingPoint<double>)ToDouble(this)).WriteSignificandLittleEndian(destination, startIndex);

    int IFloatingPoint<Number>.WriteSignificandLittleEndian(Span<byte> destination)
        => ((IFloatingPoint<double>)ToDouble(this)).WriteSignificandLittleEndian(destination);

    /// <summary>
    /// Adds two numbers.
    /// </summary>
    public static Number operator +(Number left, Number right)
    {
        BigInteger numerator = left._numerator * right._denominator + right._numerator * left._denominator;
        BigInteger denominator = left._denominator * right._denominator;
        return new Number(numerator, denominator);
    }

    /// <summary>
    /// Subtracts one number from another.
    /// </summary>
    public static Number operator -(Number left, Number right)
    {
        BigInteger numerator = left._numerator * right._denominator - right._numerator * left._denominator;
        BigInteger denominator = left._denominator * right._denominator;
        return new Number(numerator, denominator);
    }

    /// <summary>
    /// Multiplies two numbers.
    /// </summary>
    public static Number operator *(Number left, Number right)
    {
        return new Number(left._numerator * right._numerator, left._denominator * right._denominator);
    }

    /// <summary>
    /// Divides one number by another.
    /// </summary>
    public static Number operator /(Number left, Number right)
    {
        return new Number(left._numerator * right._denominator, left._denominator * right._numerator);
    }

    /// <summary>
    /// Computes the remainder of division of one number by another.
    /// </summary>
    public static Number operator %(Number left, Number right)
    {
        BigInteger leftScaled = left._numerator * right._denominator;
        BigInteger rightScaled = left._denominator * right._numerator;
        BigInteger remainder = BigInteger.Remainder(leftScaled, rightScaled);
        return new Number(remainder, left._denominator * right._denominator);
    }

    /// <summary>
    /// Returns the value unchanged.
    /// </summary>
    public static Number operator +(Number value) => value;

    /// <summary>
    /// Negates the specified number.
    /// </summary>
    public static Number operator -(Number value) => new Number(BigInteger.Negate(value._numerator), value._denominator);

    /// <summary>
    /// Increments the specified value by one.
    /// </summary>
    public static Number operator ++(Number value) => value + One;

    /// <summary>
    /// Decrements the specified value by one.
    /// </summary>
    public static Number operator --(Number value) => value - One;

    /// <summary>
    /// Determines whether the first value is greater than the second.
    /// </summary>
    public static bool operator >(Number left, Number right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Determines whether the first value is greater than or equal to the second.
    /// </summary>
    public static bool operator >=(Number left, Number right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// Determines whether the first value is less than the second.
    /// </summary>
    public static bool operator <(Number left, Number right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Determines whether the first value is less than or equal to the second.
    /// </summary>
    public static bool operator <=(Number left, Number right) => left.CompareTo(right) <= 0;

    /// <inheritdoc/>
    public bool Equals(Number other) => _numerator.Equals(other._numerator) && _denominator.Equals(other._denominator);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Number other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_numerator, _denominator);

    /// <summary>
    /// Compares two numbers.
    /// </summary>
    public int CompareTo(Number other)
    {
        BigInteger left = _numerator * other._denominator;
        BigInteger right = other._numerator * _denominator;
        return left.CompareTo(right);
    }

    /// <inheritdoc/>
    int IComparable.CompareTo(object? obj)
    {
        if (obj is Number other)
            return CompareTo(other);
        throw new ArgumentException("Object must be of type Number.", nameof(obj));
    }

    /// <summary>
    /// Determines whether two numbers are equal.
    /// </summary>
    public static bool operator ==(Number left, Number right) => left.Equals(right);

    /// <summary>
    /// Determines whether two numbers are not equal.
    /// </summary>
    public static bool operator !=(Number left, Number right) => !left.Equals(right);

    /// <summary>
    /// Implicitly converts an integer to a <see cref="Number"/>.
    /// </summary>
    public static implicit operator Number(int value) => new Number(new BigInteger(value));

    /// <summary>
    /// Implicitly converts a long integer to a <see cref="Number"/>.
    /// </summary>
    public static implicit operator Number(long value) => new Number(new BigInteger(value));

    /// <summary>
    /// Implicitly converts a <see cref="BigInteger"/> to a <see cref="Number"/>.
    /// </summary>
    public static implicit operator Number(BigInteger value) => new Number(value);

    /// <summary>
    /// Implicitly converts a <see cref="double"/> to a <see cref="Number"/>.
    /// </summary>
    public static implicit operator Number(double value) => Parse(value.ToString("R", CultureInfo.InvariantCulture));

    /// <summary>
    /// Implicitly converts a <see cref="decimal"/> to a <see cref="Number"/>.
    /// </summary>
    public static implicit operator Number(decimal value) => Parse(value.ToString(CultureInfo.InvariantCulture));

    private static bool TryConvertFromHelper<TOther>(TOther value, out Number result)
    {
        try
        {
            result = Number.Parse(value?.ToString() ?? string.Empty, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }

    static bool INumberBase<Number>.TryConvertFromChecked<TOther>(TOther value, out Number result)
        => TryConvertFromHelper(value, out result);

    static bool INumberBase<Number>.TryConvertFromSaturating<TOther>(TOther value, out Number result)
        => TryConvertFromHelper(value, out result);

    static bool INumberBase<Number>.TryConvertFromTruncating<TOther>(TOther value, out Number result)
        => TryConvertFromHelper(value, out result);

    private static bool TryConvertToHelper<TOther>(Number value, out TOther result)
    {
        try
        {
            result = (TOther)Convert.ChangeType(value.ToDecimal(), typeof(TOther), CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            result = default!;
            return false;
        }
    }

    static bool INumberBase<Number>.TryConvertToChecked<TOther>(Number value, out TOther result)
        => TryConvertToHelper(value, out result);

    static bool INumberBase<Number>.TryConvertToSaturating<TOther>(Number value, out TOther result)
        => TryConvertToHelper(value, out result);

    static bool INumberBase<Number>.TryConvertToTruncating<TOther>(Number value, out TOther result)
        => TryConvertToHelper(value, out result);

}
