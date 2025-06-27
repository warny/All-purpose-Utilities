using System.Runtime.CompilerServices;
using Utils.String;

namespace Utils.Objects;

/// <summary>
/// Provides extension methods for fluent validation and processing of objects.
/// </summary>
public static class FluentExtensions
{
	/// <summary>
	/// Marks the operation as successful with the provided value.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value to be wrapped.</param>
	/// <returns>A <see cref="FluentResult{T}"/> indicating success.</returns>
	public static FluentResult<T> Success<T>(this T value) => new(value);

	/// <summary>
	/// Marks the operation as failed with the provided value.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value to be wrapped.</param>
	/// <returns>A <see cref="FluentResult{T}"/> indicating failure.</returns>
	public static FluentResult<T> Failure<T>(this T value) => new(value, false);

	/// <summary>
	/// Checks if the provided value is null.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <returns>A <see cref="FluentResult{T}"/> indicating whether the value is null.</returns>
	public static FluentResult<T> IsNull<T>(this T value) => new(value, value is null);

	/// <summary>
	/// Checks if the provided value is not null.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <returns>A <see cref="FluentResult{T}"/> indicating whether the value is not null.</returns>
	public static FluentResult<T> IsNotNull<T>(this T value) => new(value, value is not null);

	/// <summary>
	/// Checks if the provided value is present in a list of values.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="values">An array of values to check against.</param>
	/// <returns>A <see cref="FluentResult{T}"/> indicating whether the value is in the list.</returns>
	public static FluentResult<T> IsIn<T>(this T value, params T[] values) => new(value, value.In(values));

	/// <summary>
	/// Checks if the provided value is not present in a list of values.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="values">An array of values to check against.</param>
	/// <returns>A <see cref="FluentResult{T}"/> indicating whether the value is not in the list.</returns>
	public static FluentResult<T> IsNotIn<T>(this T value, params T[] values) => new(value, value.NotIn(values));

	/// <summary>
	/// Checks if the provided value satisfies a given predicate.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="test">The predicate to apply.</param>
	/// <returns>A <see cref="FluentResult{T}"/> indicating whether the predicate is satisfied.</returns>
	public static FluentResult<T> Test<T>(this T value, Func<T, bool> test) => new(value, test(value));

	/// <summary>
	/// Checks if the provided value is equal to the comparison value.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <typeparam name="CT">The type of the comparison value.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="comparisonValue">The value to compare against.</param>
	/// <returns>A <see cref="FluentResult{T}"/> indicating whether the values are equal.</returns>
	public static FluentResult<T> IsEqualTo<T, CT>(this T value, CT comparisonValue)
		=> new FluentResult<T>(value).IsEqualTo(comparisonValue);

	/// <summary>
	/// Checks if the provided value is less than the comparison value.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <typeparam name="CT">The type of the comparison value.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="comparisonValue">The value to compare against.</param>
	/// <returns>A <see cref="FluentResult{T}"/> indicating whether the value is less than the comparison value.</returns>
	public static FluentResult<T> IsLowerThan<T, CT>(this T value, CT comparisonValue)
		=> new FluentResult<T>(value).IsLowerThan(comparisonValue);

	/// <summary>
	/// Checks if the provided value is less than or equal to the comparison value.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <typeparam name="CT">The type of the comparison value.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="comparisonValue">The value to compare against.</param>
	/// <returns>A <see cref="FluentResult{T}"/> indicating whether the value is less than or equal to the comparison value.</returns>
	public static FluentResult<T> IsLowerOrEqualsThan<T, CT>(this T value, CT comparisonValue)
		=> new FluentResult<T>(value).IsLowerOrEqualsThan(comparisonValue);

	/// <summary>
	/// Checks if the provided value is greater than the comparison value.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <typeparam name="CT">The type of the comparison value.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="comparisonValue">The value to compare against.</param>
	/// <returns>A <see cref="FluentResult{T}"/> indicating whether the value is greater than the comparison value.</returns>
	public static FluentResult<T> IsGreaterThan<T, CT>(this T value, CT comparisonValue)
		=> new FluentResult<T>(value).IsGreaterThan(comparisonValue);

	/// <summary>
	/// Checks if the provided value is greater than or equal to the comparison value.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <typeparam name="CT">The type of the comparison value.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="comparisonValue">The value to compare against.</param>
	/// <returns>A <see cref="FluentResult{T}"/> indicating whether the value is greater than or equal to the comparison value.</returns>
	public static FluentResult<T> IsGreaterOrEqualsThan<T, CT>(this T value, CT comparisonValue)
		=> new FluentResult<T>(value).IsGreaterOrEqualsThan(comparisonValue);

	/// <summary>
	/// Converts an empty or null string to null.
	/// </summary>
	/// <param name="value">The string to check.</param>
	/// <returns>Null if the string is null or empty, otherwise the original string.</returns>
	public static string NullOrEmptyToNull(this string value) => value.IsNullOrEmpty() ? null : value;

	/// <summary>
	/// Converts an empty or null string to null within a <see cref="FluentResult{T}"/>.
	/// </summary>
	/// <param name="value">The fluent result containing the string.</param>
	/// <returns>A new <see cref="FluentResult{T}"/> with the string converted to null if it was null or empty.</returns>
	public static FluentResult<string> NullOrEmptyToNull(this FluentResult<string> value)
		=> new(value.Value.IsNullOrEmpty() ? null : value.Value, value.Result);

	/// <summary>
	/// Converts a whitespace-only or null string to null.
	/// </summary>
	/// <param name="value">The string to check.</param>
	/// <returns>Null if the string is null or contains only whitespace, otherwise the original string.</returns>
	public static string NullOrWhiteSpaceToNull(this string value) => value.IsNullOrWhiteSpace() ? null : value;

	/// <summary>
	/// Converts a whitespace-only or null string to null within a <see cref="FluentResult{T}"/>.
	/// </summary>
	/// <param name="value">The fluent result containing the string.</param>
	/// <returns>A new <see cref="FluentResult{T}"/> with the string converted to null if it was null or whitespace-only.</returns>
	public static FluentResult<string> NullOrWhiteSpaceToNull(this FluentResult<string> value)
		=> new(value.Value.IsNullOrWhiteSpace() ? null : value.Value, value.Result);
}

/// <summary>
/// Represents the result of a fluent operation, allowing for chained conditional checks and transformations.
/// </summary>
/// <typeparam name="T">The type of the value being evaluated.</typeparam>
public struct FluentResult<T>
{
	/// <summary>
	/// Gets the value being evaluated.
	/// </summary>
	public T Value { get; }

	/// <summary>
	/// Gets a value indicating whether the operation was successful.
	/// </summary>
	public bool Result { get; }

	/// <summary>
	/// Gets the name of the initial value
	/// </summary>
	public string VariableName { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="FluentResult{T}"/> struct with the provided value, marking it as successful.
	/// </summary>
	/// <param name="value">The value to wrap.</param>
	public FluentResult(T value, [CallerArgumentExpression(nameof(value))] string variableName = "")
	{
		Value = value;
		Result = true;
		VariableName = variableName;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FluentResult{T}"/> struct with the provided value and result.
	/// </summary>
	/// <param name="value">The value to wrap.</param>
	/// <param name="success">Indicates whether the operation was successful.</param>
	public FluentResult(T value, bool success, [CallerArgumentExpression(nameof(value))] string variableName = "")
	{
		Value = value;
		Result = success;
		VariableName = variableName;
	}

	/// <summary>
	/// Negates the current result.
	/// </summary>
	/// <returns>A new <see cref="FluentResult{T}"/> with the result negated.</returns>
	public readonly FluentResult<T> Not() => new(Value, !Result, VariableName);

	/// <summary>
	/// Marks the operation as successful.
	/// </summary>
	/// <returns>A new <see cref="FluentResult{T}"/> marked as successful.</returns>
	public readonly FluentResult<T> Success() => new(Value, true, VariableName);

	/// <summary>
	/// Marks the operation as failed.
	/// </summary>
	/// <returns>A new <see cref="FluentResult{T}"/> marked as failed.</returns>
	public readonly FluentResult<T> Failure() => new(Value, false, VariableName);

	/// <summary>
	/// Checks if the value verifies the given predicate.
	/// </summary>
	/// <returns>A new <see cref="FluentResult{T}"/> indicating whether verifies the given predicate.</returns>
	public readonly FluentResult<T> Is(Func<T, bool> test) => new(Value, Result && test(Value), VariableName);

	/// <summary>
	/// Checks if the value does not verify the given predicate.
	/// </summary>
	/// <returns>A new <see cref="FluentResult{T}"/> indicating whether does not verify the given predicate.</returns>
	public readonly FluentResult<T> IsNot(Func<T, bool> test) => new(Value, Result && !test(Value), VariableName);

	/// <summary>
	/// Checks if the value is null.
	/// </summary>
	/// <returns>A new <see cref="FluentResult{T}"/> indicating whether the value is null.</returns>
	public readonly FluentResult<T> IsNull() => Is(v => v is null);

	/// <summary>
	/// Checks if the value is not null.
	/// </summary>
	/// <returns>A new <see cref="FluentResult{T}"/> indicating whether the value is not null.</returns>
	public readonly FluentResult<T> IsNotNull() => Is(v => v is not null);

	/// <summary>
	/// Checks if the value is present in a list of values.
	/// </summary>
	/// <param name="values">An array of values to check against.</param>
	/// <returns>A new <see cref="FluentResult{T}"/> indicating whether the value is in the list.</returns>
	public readonly FluentResult<T> IsIn(params T[] values) => Is(v=> v.In(values));

	/// <summary>
	/// Checks if the value is not present in a list of values.
	/// </summary>
	/// <param name="values">An array of values to check against.</param>
	/// <returns>A new <see cref="FluentResult{T}"/> indicating whether the value is not in the list.</returns>
	public readonly FluentResult<T> IsNotIn(params T[] values) => Is(v => v.NotIn(values));

	/// <summary>
	/// Checks if the value is equal to the comparison value.
	/// </summary>
	/// <typeparam name="CT">The type of the comparison value.</typeparam>
	/// <param name="comparisonValue">The value to compare against.</param>
	/// <returns>A new <see cref="FluentResult{T}"/> indicating whether the values are equal.</returns>
	public readonly FluentResult<T> IsEqualTo<CT>(CT comparisonValue)
	{
		if (Value is IEquatable<CT> equatable)
			return new FluentResult<T>(Value, Result && equatable.Equals(comparisonValue), VariableName);
		if (Value is IComparable<CT> gcomparable)
			return new FluentResult<T>(Value, Result && gcomparable.CompareTo(comparisonValue) == 0, VariableName);
		if (Value is IComparable comparable)
			return new FluentResult<T>(Value, Result && comparable.CompareTo(comparisonValue) == 0, VariableName);
		return new FluentResult<T>(Value, Result && Value.Equals(comparisonValue), VariableName);
	}

	/// <summary>
	/// Checks if the value is less than the comparison value.
	/// </summary>
	/// <typeparam name="CT">The type of the comparison value.</typeparam>
	/// <param name="comparisonValue">The value to compare against.</param>
	/// <returns>A new <see cref="FluentResult{T}"/> indicating whether the value is less than the comparison value.</returns>
	public readonly FluentResult<T> IsLowerThan<CT>(CT comparisonValue)
	{
		if (Value is IComparable<CT> gcomparable)
			return new FluentResult<T>(Value, Result && gcomparable.CompareTo(comparisonValue) < 0);
		if (Value is IComparable comparable)
			return new FluentResult<T>(Value, Result && comparable.CompareTo(comparisonValue) < 0);
		throw new NotSupportedException($"{typeof(CT).Name} is not comparable");
	}

	/// <summary>
	/// Checks if the value is less than or equal to the comparison value.
	/// </summary>
	/// <typeparam name="CT">The type of the comparison value.</typeparam>
	/// <param name="comparisonValue">The value to compare against.</param>
	/// <returns>A new <see cref="FluentResult{T}"/> indicating whether the value is less than or equal to the comparison value.</returns>
	public readonly FluentResult<T> IsLowerOrEqualsThan<CT>(CT comparisonValue)
	{
		if (Value is IComparable<CT> gcomparable)
			return new FluentResult<T>(Value, Result && gcomparable.CompareTo(comparisonValue) <= 0, VariableName);
		if (Value is IComparable comparable)
			return new FluentResult<T>(Value, Result && comparable.CompareTo(comparisonValue) <= 0, VariableName);
		throw new NotSupportedException($"{typeof(CT).Name} is not comparable");
	}

	/// <summary>
	/// Checks if the value is greater than the comparison value.
	/// </summary>
	/// <typeparam name="CT">The type of the comparison value.</typeparam>
	/// <param name="comparisonValue">The value to compare against.</param>
	/// <returns>A new <see cref="FluentResult{T}"/> indicating whether the value is greater than the comparison value.</returns>
	public readonly FluentResult<T> IsGreaterThan<CT>(CT comparisonValue)
	{
		if (Value is IComparable<CT> gcomparable)
			return new FluentResult<T>(Value, Result && gcomparable.CompareTo(comparisonValue) > 0);
		if (Value is IComparable comparable)
			return new FluentResult<T>(Value, Result && comparable.CompareTo(comparisonValue) > 0);
		throw new NotSupportedException($"{typeof(CT).Name} is not comparable");
	}

	/// <summary>
	/// Checks if the value is greater than or equal to the comparison value.
	/// </summary>
	/// <typeparam name="CT">The type of the comparison value.</typeparam>
	/// <param name="comparisonValue">The value to compare against.</param>
	/// <returns>A new <see cref="FluentResult{T}"/> indicating whether the value is greater than or equal to the comparison value.</returns>
	public readonly FluentResult<T> IsGreaterOrEqualsThan<CT>(CT comparisonValue)
	{
		if (Value is IComparable<CT> gcomparable)
			return new FluentResult<T>(Value, Result && gcomparable.CompareTo(comparisonValue) >= 0);
		if (Value is IComparable comparable)
			return new FluentResult<T>(Value, Result && comparable.CompareTo(comparisonValue) >= 0);
		throw new NotSupportedException($"{typeof(CT).Name} is not comparable");
	}

	/// <summary>
	/// Applies a function to the current <see cref="FluentResult{T}"/> and returns the result.
	/// </summary>
	/// <param name="func">The function to apply.</param>
	/// <returns>The result of the function.</returns>
	public readonly RT Then<RT>(Func<FluentResult<T>, RT> func) => func(this);

	/// <summary>
	/// Applies one of two functions based on the result of the current <see cref="FluentResult{T}"/>.
	/// </summary>
	/// <param name="true">The function to apply if the result is true.</param>
	/// <param name="false">The function to apply if the result is false.</param>
	/// <returns>The result of the applied function.</returns>
	public readonly RT Then<RT>(Func<T, RT> @true, Func<T, RT> @false) => Result ? @true(Value) : @false(Value);

	/// <summary>
	/// Implicitly converts a <see cref="FluentResult{T}"/> to its wrapped value.
	/// </summary>
	/// <param name="value">The <see cref="FluentResult{T}"/> to convert.</param>
	public static implicit operator T(FluentResult<T> value) => value.Value;
}
