using System;
using System.Collections.Generic;
using System.Text;
using Utils.Mathematics;

namespace Utils.Objects;

	public static class ObjectFluent
	{
		public static FluentResult<T> Success<T>(this T value) => new(value);
		public static FluentResult<T> Failure<T>(this T value) => new(value, false);

		public static FluentResult<T> IsNull<T>(this T value) => new(value, value is null);
		public static FluentResult<T> IsNotNull<T>(this T value) => new(value, value is not null);
		public static FluentResult<T> IsIn<T>(this T value, params T[] values) => new(value, value.In(values));
		public static FluentResult<T> IsNotIn<T>(this T value, params T[] values) => new(value, value.NotIn(values));
		public static FluentResult<T> Test<T>(this T value, Func<T, bool> test) => new(value, test(value));

		public static FluentResult<T> IsEqualTo<T, CT>(this T value, CT comparisonValue)
			=> new FluentResult<T>(value).IsEqualTo(comparisonValue);
		public static FluentResult<T> IsLowerThan<T, CT>(this T value, CT comparisonValue) 
			=> new FluentResult<T>(value).IsLowerThan(comparisonValue);
		public static FluentResult<T> IsLowerOrEqualsThan<T, CT>(this T value, CT comparisonValue) 
			=> new FluentResult<T>(value).IsLowerOrEqualsThan(comparisonValue);
		public static FluentResult<T> IsGreaterThan<T, CT>(this T value, CT comparisonValue)
			=> new FluentResult<T>(value).IsGreaterThan(comparisonValue);
		public static FluentResult<T> IsGreaterOrEqualsThan<T, CT>(this T value, CT comparisonValue)
			=> new FluentResult<T>(value).IsGreaterOrEqualsThan(comparisonValue);

		public static string NullOrEmptyToNull(this string value) => value.IsNullOrEmpty() ? null : value;
		public static FluentResult<string> NullOrEmptyIsNull(this FluentResult<string> value) => new(value.Value.IsNullOrEmpty() ? null : value.Value, value.Result);
		public static string NullOrWhiteSpaceToNull(this string value) => value.IsNullOrWhiteSpace() ? null : value;
		public static FluentResult<string> NullOrWhiteSpaceIsNull(this FluentResult<string> value) => new(value.Value.IsNullOrWhiteSpace() ? null : value.Value, value.Result);
	}

public struct FluentResult<T>
{
	public T Value { get; }
	public bool Result { get; }

	public FluentResult(T value)
	{
		Value = value;
		Result = true;
	}

	public FluentResult(T value, bool success)
	{
		Value = value;
		Result = success;
	}

	public readonly FluentResult<T> Not() => new(Value, !Result);
	public readonly FluentResult<T> Success() => new(Value, true);
	public readonly FluentResult<T> Failure() => new(Value, false);

	public readonly FluentResult<T> IsNull() => new(Value, Result && Value is null);
	public readonly FluentResult<T> IsNotNull() => new(Value, Result && Value is not null);
	public readonly FluentResult<T> IsIn(params T[] values) => new(Value, Result && Value.In(values));
	public readonly FluentResult<T> IsNotIn(params T[] values) => new(Value, Result && Value.NotIn(values));
	public readonly FluentResult<T> Test(Func<T, bool> test) => new(Value, Result && test(Value));

	public readonly FluentResult<T> IsEqualTo<CT>(CT comparisonValue)
	{
		if (Value is IEquatable<CT> equatable)
			return new FluentResult<T>(Value, Result && equatable.Equals(comparisonValue));
		if (Value is IComparable<CT> gcomparable)
			return new FluentResult<T>(Value, Result && gcomparable.CompareTo(comparisonValue) == 0);
		if (Value is IComparable comparable)
			return new FluentResult<T>(Value, Result && comparable.CompareTo(comparisonValue) == 0);
		return new FluentResult<T>(Value, Result && Value.Equals(comparisonValue)); ;
	}

	public readonly FluentResult<T> IsLowerThan<CT>(CT comparisonValue)
	{
		if (Value is IComparable<CT> gcomparable)
			return new FluentResult<T>(Value, Result && gcomparable.CompareTo(comparisonValue) < 0);
		if (Value is IComparable comparable)
			return new FluentResult<T>(Value, Result && comparable.CompareTo(comparisonValue) < 0);
		throw new NotSupportedException($"{typeof(CT).Name} is not comparable");
	}

	public readonly FluentResult<T> IsLowerOrEqualsThan<CT>(CT comparisonValue)
	{
		if (Value is IComparable<CT> gcomparable)
			return new FluentResult<T>(Value, Result && gcomparable.CompareTo(comparisonValue) <= 0);
		if (Value is IComparable comparable)
			return new FluentResult<T>(Value, Result && comparable.CompareTo(comparisonValue) <= 0);
		throw new NotSupportedException($"{typeof(CT).Name} is not comparable");
	}

	public readonly FluentResult<T> IsGreaterThan<CT>(CT comparisonValue)
	{
		if (Value is IComparable<CT> gcomparable)
			return new FluentResult<T>(Value, Result && gcomparable.CompareTo(comparisonValue) > 0);
		if (Value is IComparable comparable)
			return new FluentResult<T>(Value, Result && comparable.CompareTo(comparisonValue) > 0);
		throw new NotSupportedException($"{typeof(CT).Name} is not comparable");
	}

	public readonly FluentResult<T> IsGreaterOrEqualsThan<CT>(CT comparisonValue)
	{
		if (Value is IComparable<CT> gcomparable)
			return new FluentResult<T>(Value, Result && gcomparable.CompareTo(comparisonValue) >= 0);
		if (Value is IComparable comparable)
			return new FluentResult<T>(Value, Result && comparable.CompareTo(comparisonValue) >= 0);
		throw new NotSupportedException($"{typeof(CT).Name} is not comparable");
	}

	public readonly T Then(Func<FluentResult<T>, T> func) => func(this);
	public readonly T Then(Func<T, T> @true, Func<T, T> @false) => Result ? @true(Value) : @false(Value);

	public static implicit operator T(FluentResult<T> value) => value.Value;
}
