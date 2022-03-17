using System;
using System.Collections.Generic;
using System.Text;
using Utils.Mathematics;

namespace Utils.Objects
{
	public static class ObjectFluent
	{
		public static FluentResult<T> IsNull<T>(this T value) => new FluentResult<T>(value, value is null);
		public static FluentResult<T> IsNotNull<T>(this T value) => new FluentResult<T>(value, value is not null);
		public static FluentResult<T> IsIn<T>(this T value, params T[] values) => new FluentResult<T>(value, value.In(values));
		public static FluentResult<T> IsNotIn<T>(this T value, params T[] values) => new FluentResult<T>(value, value.NotIn(values));
		public static FluentResult<T> Test<T>(this T value, Func<T, bool> test) => new FluentResult<T>(value, test(value));

		public static FluentResult<T> IsEqualTo<T, CT>(this T value, CT comparisonValue)
		{
			if (value is IEquatable<CT> equatable) 
				return new FluentResult<T>(value, equatable.Equals(comparisonValue));
			if (value is IComparable<CT> gcomparable)
			    return new FluentResult<T>(value, gcomparable.CompareTo(comparisonValue) == 0);
			if (value is IComparable comparable)
				return new FluentResult<T>(value, comparable.CompareTo(comparisonValue) == 0);
			return new FluentResult<T>(value, value.Equals(comparisonValue)); ;
		}

		public static FluentResult<T> IsLowerThan<T>(this T value, object comparisonValue) where T : IComparable
			=> new FluentResult<T>(value, value.CompareTo(comparisonValue) < 0);
		public static FluentResult<T> IsLowerThan<T, CT>(this T value, CT comparisonValue) where T : IComparable<CT>
			=> new FluentResult<T>(value, value.CompareTo(comparisonValue) < 0);
		public static FluentResult<T> IsLowerOrEqualsThan<T>(this T value, object comparisonValue) where T : IComparable
			=> new FluentResult<T>(value, value.CompareTo(comparisonValue) <= 0);
		public static FluentResult<T> IsLowerOrEqualsThan<T, CT>(this T value, CT comparisonValue) where T : IComparable<CT>
			=> new FluentResult<T>(value, value.CompareTo(comparisonValue) <= 0);
		public static FluentResult<T> IsGreaterThan<T>(this T value, object comparisonValue) where T : IComparable
			=> new FluentResult<T>(value, value.CompareTo(comparisonValue) > 0);
		public static FluentResult<T> IsGreaterThan<T, CT>(this T value, CT comparisonValue) where T : IComparable<CT>
			=> new FluentResult<T>(value, value.CompareTo(comparisonValue) > 0);
		public static FluentResult<T> IsGreaterOrEqualsThan<T>(this T value, object comparisonValue) where T : IComparable
			=> new FluentResult<T>(value, value.CompareTo(comparisonValue) >= 0);
		public static FluentResult<T> IsGreaterOrEqualsThan<T, CT>(this T value, CT comparisonValue) where T : IComparable<CT>
			=> new FluentResult<T>(value, value.CompareTo(comparisonValue) >= 0);

		public static string NullOrEmptyIsNull(this string value) => value.IsNullOrEmpty() ? null : value;
		public static FluentResult<string> NullOrEmptyIsNull(this FluentResult<string> value) => new FluentResult<string>(value.Value.IsNullOrEmpty() ? null : value.Value, value.Success);
		public static string NullOrWhiteSpaceIsNull(this string value) => value.IsNullOrWhiteSpace() ? null : value;
		public static FluentResult<string> NullOrWhiteSpaceIsNull(this FluentResult<string> value) => new FluentResult<string>(value.Value.IsNullOrWhiteSpace() ? null : value.Value, value.Success);
	}

	public struct FluentResult<T>
	{
		public T Value { get; }
		public bool Success { get; }

		public FluentResult(T value)
		{
			Value = value;
			Success = true;
		}

		public FluentResult(T value, bool success)
		{
			Value = value;
			Success = success;
		}

		public FluentResult<T> IsNull() => new FluentResult<T>(Value, Success && Value is null);
		public FluentResult<T> IsNotNull() => new FluentResult<T>(Value, Success && Value is not null);
		public FluentResult<T> IsIn(params T[] values) => new FluentResult<T>(Value, Success && Value.In(values));
		public FluentResult<T> IsNotIn(params T[] values) => new FluentResult<T>(Value, Success && Value.NotIn(values));
		public FluentResult<T> Test(Func<T, bool> test) => new FluentResult<T>(Value, Success && test(Value));

		public FluentResult<T> IsEqualTo<CT>(CT comparisonValue)
		{
			if (Value is IEquatable<CT> equatable)
				return new FluentResult<T>(Value, Success && equatable.Equals(comparisonValue));
			if (Value is IComparable<CT> gcomparable)
				return new FluentResult<T>(Value, Success && gcomparable.CompareTo(comparisonValue) == 0);
			if (Value is IComparable comparable)
				return new FluentResult<T>(Value, Success && comparable.CompareTo(comparisonValue) == 0);
			return new FluentResult<T>(Value, Success && Value.Equals(comparisonValue)); ;
		}

		public FluentResult<T> IsLowerThan<CT>(CT comparisonValue)
		{
			if (Value is IComparable<CT> gcomparable)
				return new FluentResult<T>(Value, Success && gcomparable.CompareTo(comparisonValue) < 0);
			if (Value is IComparable comparable)
				return new FluentResult<T>(Value, Success && comparable.CompareTo(comparisonValue) < 0);
			throw new NotSupportedException($"{typeof(CT).Name} is not comparable");
		}

		public FluentResult<T> IsLowerOrEqualsThan<CT>(CT comparisonValue)
		{
			if (Value is IComparable<CT> gcomparable)
				return new FluentResult<T>(Value, Success && gcomparable.CompareTo(comparisonValue) <= 0);
			if (Value is IComparable comparable)
				return new FluentResult<T>(Value, Success && comparable.CompareTo(comparisonValue) <= 0);
			throw new NotSupportedException($"{typeof(CT).Name} is not comparable");
		}

		public FluentResult<T> IsGreaterThan<CT>(CT comparisonValue)
		{
			if (Value is IComparable<CT> gcomparable)
				return new FluentResult<T>(Value, Success && gcomparable.CompareTo(comparisonValue) > 0);
			if (Value is IComparable comparable)
				return new FluentResult<T>(Value, Success && comparable.CompareTo(comparisonValue) > 0);
			throw new NotSupportedException($"{typeof(CT).Name} is not comparable");
		}

		public FluentResult<T> IsGreaterOrEqualsThan<CT>(CT comparisonValue)
		{
			if (Value is IComparable<CT> gcomparable)
				return new FluentResult<T>(Value, Success && gcomparable.CompareTo(comparisonValue) >= 0);
			if (Value is IComparable comparable)
				return new FluentResult<T>(Value, Success && comparable.CompareTo(comparisonValue) >= 0);
			throw new NotSupportedException($"{typeof(CT).Name} is not comparable");
		}

		public T Then(Func<FluentResult<T>, T> func) => func(this);
		public T Then(Func<T, T> @true, Func<T, T> @false) => Success ? @true(Value) : @false(Value);

		public static implicit operator T (FluentResult<T> value) => value.Value;
	}
}
