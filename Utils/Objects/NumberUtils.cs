using System.Globalization;
using System.Numerics;
using Utils.Reflection;

namespace Utils.Objects;

public static class NumberUtils
{
	/// <summary>
	/// Checks whether the given <paramref name="value"/> is recognized as a numeric type
	/// by verifying that its runtime type implements <see cref="INumber{TSelf}"/>.
	/// </summary>
	/// <param name="value">The object to test.</param>
	/// <returns>True if <paramref name="value"/> is numeric, otherwise false.</returns>
	public static bool IsNumeric(object value)
	{
		if (value is null) return false;
		Type t = value.GetType();

		// Use IsDefinedBy to check if t implements INumber<> at runtime.
		return t.IsDefinedBy(typeof(INumber<>));
	}

	/// <summary>
	/// Checks whether the given <paramref name="value"/> is integral,
	/// i.e. it implements <see cref="IBinaryInteger{TSelf}"/>.
	/// </summary>
	/// <param name="value">The object to test.</param>
	/// <returns>True if <paramref name="value"/> is integral, otherwise false.</returns>
	public static bool IsIntegralType(object value)
	{
		if (value is null) return false;
		Type t = value.GetType();

		// IBinaryInteger<> is the .NET 7 interface for integral numeric types.
		return t.IsDefinedBy(typeof(IBinaryInteger<>));
	}

	/// <summary>
	/// Checks whether the given <paramref name="value"/> is a floating-point type,
	/// i.e. it implements <see cref="IFloatingPoint{TSelf}"/>.
	/// </summary>
	/// <param name="value">The object to test.</param>
	/// <returns>True if <paramref name="value"/> is floating-point, otherwise false.</returns>
	public static bool IsFloatingPointType(object value)
	{
		if (value is null) return false;
		Type t = value.GetType();

		// IFloatingPoint<> is the .NET 7 interface for float, double, decimal, etc.
		return t.IsDefinedBy(typeof(IFloatingPoint<>));
	}

	/// <summary>
	/// Compares two objects that are presumed to be numeric. If both are integral,
	/// uses integer-based comparison (by converting to long). Otherwise, falls back
	/// to decimal-based comparison to preserve as much precision as possible.
	/// </summary>
	/// <param name="left">The left numeric operand.</param>
	/// <param name="right">The right numeric operand.</param>
	/// <returns>
	/// A negative value if <paramref name="left"/> &lt; <paramref name="right"/>, 
	/// zero if they are equal, 
	/// a positive value if <paramref name="left"/> &gt; <paramref name="right"/>.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown if <paramref name="left"/> or <paramref name="right"/> is not numeric.
	/// </exception>
	public static int CompareNumeric(object left, object right)
	{
		if (!IsNumeric(left)) throw new ArgumentException($"Value '{left}' is not numeric.", nameof(left));
		if (!IsNumeric(right)) throw new ArgumentException($"Value '{right}' is not numeric.", nameof(right));

		// If both are integral, compare them as 64-bit signed integers
		if (IsIntegralType(left) && IsIntegralType(right))
		{
			try
			{
				long lVal = Convert.ToInt64(left, CultureInfo.InvariantCulture);
				long rVal = Convert.ToInt64(right, CultureInfo.InvariantCulture);
				return lVal.CompareTo(rVal);
			}
			catch
			{
				// If out of range (e.g. very large ulong), fallback to decimal below
			}
		}

		// Otherwise, or if integral conversion failed, do decimal-based comparison
		decimal dLeft = Convert.ToDecimal(left, CultureInfo.InvariantCulture);
		decimal dRight = Convert.ToDecimal(right, CultureInfo.InvariantCulture);
		return dLeft.CompareTo(dRight);
	}

	/// <summary>
	/// Tries to compare two objects as numeric values without throwing exceptions for invalid or overflow cases.
	/// If both are integral, compares them as 64-bit integers; otherwise attempts decimal-based comparison.
	/// </summary>
	/// <param name="left">The left numeric operand.</param>
	/// <param name="right">The right numeric operand.</param>
	/// <param name="result">
	/// Receives &lt; 0 if <paramref name="left"/> &lt; <paramref name="right"/>,
	/// 0 if they are equal, &gt; 0 if <paramref name="left"/> &gt; <paramref name="right"/>.
	/// </param>
	/// <returns>True if both operands were numeric and successfully compared; otherwise false.</returns>
	public static bool TryCompareNumeric(object left, object right, out int result)
	{
		result = 0;
		if (!IsNumeric(left) || !IsNumeric(right))
			return false;

		// If both are integral, attempt integer-based comparison first
		if (IsIntegralType(left) && IsIntegralType(right))
		{
			if (left is BigInteger || right is BigInteger)
			{
				BigInteger lVal = left is BigInteger ? (BigInteger)left : (BigInteger)Convert.ChangeType(left, typeof(BigInteger));
				BigInteger rVal = right is BigInteger ? (BigInteger)right : (BigInteger)Convert.ChangeType(right, typeof(BigInteger));
				return lVal.Equals(rVal);
			}
			try
			{
				long lVal = Convert.ToInt64(left, CultureInfo.InvariantCulture);
				long rVal = Convert.ToInt64(right, CultureInfo.InvariantCulture);
				return lVal.Equals(rVal);
			}
			catch
			{
				// Overflow or cast issue => fallback to decimal
			}
		}

		// Fallback: attempt decimal comparison
		try
		{
			decimal dLeft = Convert.ToDecimal(left, CultureInfo.InvariantCulture);
			decimal dRight = Convert.ToDecimal(right, CultureInfo.InvariantCulture);
			result = dLeft.CompareTo(dRight);
			return true;
		}
		catch
		{
			// If conversion to decimal also fails, we can't compare numerically
			return false;
		}
	}
}
