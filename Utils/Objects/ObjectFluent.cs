using System;
using System.Collections.Generic;
using System.Text;
using Utils.Mathematics;

namespace Utils.Objects
{
	public static class ObjectFluent
	{
		public static FluentResult<T> Success<T>(this T value) => new FluentResult<T>(value);
		public static FluentResult<T> Failure<T>(this T value) => new FluentResult<T>(value, false);

		public static FluentResult<T> IsNull<T>(this T value) => new FluentResult<T>(value, value is null);
		public static FluentResult<T> IsNotNull<T>(this T value) => new FluentResult<T>(value, value is not null);
		public static FluentResult<T> IsIn<T>(this T value, params T[] values) => new FluentResult<T>(value, value.In(values));
		public static FluentResult<T> IsNotIn<T>(this T value, params T[] values) => new FluentResult<T>(value, value.NotIn(values));
		public static FluentResult<T> Test<T>(this T value, Func<T, bool> test) => new FluentResult<T>(value, test(value));

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
		public static FluentResult<string> NullOrEmptyIsNull(this FluentResult<string> value) => new FluentResult<string>(value.Value.IsNullOrEmpty() ? null : value.Value, value.Result);
		public static string NullOrWhiteSpaceToNull(this string value) => value.IsNullOrWhiteSpace() ? null : value;
		public static FluentResult<string> NullOrWhiteSpaceIsNull(this FluentResult<string> value) => new FluentResult<string>(value.Value.IsNullOrWhiteSpace() ? null : value.Value, value.Result);
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

		public FluentResult<T> Not() => new FluentResult<T>(Value, !Result);
		public FluentResult<T> Success() => new FluentResult<T>(Value, true);
		public FluentResult<T> Failure() => new FluentResult<T>(Value, false);

		public FluentResult<T> IsNull() => new FluentResult<T>(Value, Result && Value is null);
		public FluentResult<T> IsNotNull() => new FluentResult<T>(Value, Result && Value is not null);
		public FluentResult<T> IsIn(params T[] values) => new FluentResult<T>(Value, Result && Value.In(values));
		public FluentResult<T> IsNotIn(params T[] values) => new FluentResult<T>(Value, Result && Value.NotIn(values));
		public FluentResult<T> Test(Func<T, bool> test) => new FluentResult<T>(Value, Result && test(Value));

		public FluentResult<T> IsEqualTo<CT>(CT comparisonValue)
		{
			if (Value is IEquatable<CT> equatable)
				return new FluentResult<T>(Value, Result && equatable.Equals(comparisonValue));
			if (Value is IComparable<CT> gcomparable)
				return new FluentResult<T>(Value, Result && gcomparable.CompareTo(comparisonValue) == 0);
			if (Value is IComparable comparable)
				return new FluentResult<T>(Value, Result && comparable.CompareTo(comparisonValue) == 0);
			return new FluentResult<T>(Value, Result && Value.Equals(comparisonValue)); ;
		}

		public FluentResult<T> IsLowerThan<CT>(CT comparisonValue)
		{
			if (Value is IComparable<CT> gcomparable)
				return new FluentResult<T>(Value, Result && gcomparable.CompareTo(comparisonValue) < 0);
			if (Value is IComparable comparable)
				return new FluentResult<T>(Value, Result && comparable.CompareTo(comparisonValue) < 0);
			throw new NotSupportedException($"{typeof(CT).Name} is not comparable");
		}

		public FluentResult<T> IsLowerOrEqualsThan<CT>(CT comparisonValue)
		{
			if (Value is IComparable<CT> gcomparable)
				return new FluentResult<T>(Value, Result && gcomparable.CompareTo(comparisonValue) <= 0);
			if (Value is IComparable comparable)
				return new FluentResult<T>(Value, Result && comparable.CompareTo(comparisonValue) <= 0);
			throw new NotSupportedException($"{typeof(CT).Name} is not comparable");
		}

		public FluentResult<T> IsGreaterThan<CT>(CT comparisonValue)
		{
			if (Value is IComparable<CT> gcomparable)
				return new FluentResult<T>(Value, Result && gcomparable.CompareTo(comparisonValue) > 0);
			if (Value is IComparable comparable)
				return new FluentResult<T>(Value, Result && comparable.CompareTo(comparisonValue) > 0);
			throw new NotSupportedException($"{typeof(CT).Name} is not comparable");
		}

		public FluentResult<T> IsGreaterOrEqualsThan<CT>(CT comparisonValue)
		{
			if (Value is IComparable<CT> gcomparable)
				return new FluentResult<T>(Value, Result && gcomparable.CompareTo(comparisonValue) >= 0);
			if (Value is IComparable comparable)
				return new FluentResult<T>(Value, Result && comparable.CompareTo(comparisonValue) >= 0);
			throw new NotSupportedException($"{typeof(CT).Name} is not comparable");
		}

		public T Then(Func<FluentResult<T>, T> func) => func(this);
		public T Then(Func<T, T> @true, Func<T, T> @false) => Result ? @true(Value) : @false(Value);

		public static implicit operator T (FluentResult<T> value) => value.Value;
	}
}
