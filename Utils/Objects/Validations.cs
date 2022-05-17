using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Utils.Objects
{
	public static class Validations
	{
		public static T ArgMustNotBeNull<T>(this T value, [CallerArgumentExpression("value")] string valueName = "")
			where T : class
		{
			if (value is null)
			{
				throw new ArgumentNullException(valueName);
			}
			return value;
		}

		public static T ValueMustNotBeNull<T>(this T value, [CallerArgumentExpression("value")] string valueName = "")
			where T : class
		{
			if (value is null)
			{
				throw new NullReferenceException($"{valueName} must not be null");
			}
			return value;
		}

		public static void ArgMustBeOfSize<T>(this IReadOnlyCollection<T> value, int size, [CallerArgumentExpression("value")] string valueName = "")
		{
			if (value.Count != size)
			{
				throw new ArgumentOutOfRangeException(valueName, value.Count, $"{valueName} must contain {size} elements");
			}
		}

		public static void ValueSizeMustBeMultipleOf<T>(this IReadOnlyCollection<T> value, int multiple, [CallerArgumentExpression("value")] string valueName = "")
		{
			if (value.Count % multiple != 0)
			{
				throw new ArrayDimensionException($"{valueName}.Length must be a multiple of {multiple}");
			}
		}

		public static C ArgSizeMustBeMultipleOf<C, T>(this C value, int multiple, [CallerArgumentExpression("value")] string valueName = "")
			where C : IReadOnlyCollection<T>
		{
			if (value.Count % multiple != 0)
			{
				throw new ArgumentException(valueName, $"{valueName}.Length must be a multiple of {multiple}");
			}
			return value;
		}

		public static T ArgMustBeEqualsTo<T>(this T value, T targetValue, [CallerArgumentExpression("value")] string valueName = "") where T : IComparable
		{
			if (value.CompareTo(targetValue) != 0)
			{
				throw new ArgumentOutOfRangeException(valueName, $"{valueName} must be equal to {targetValue}");
			}
			return value;
		}

		public static T ArgMustBeIn<T>(this T value, T[] targetValue, [CallerArgumentExpression("value")] string valueName = "") where T : IComparable
		{
			if (value.CompareTo(targetValue) != 0)
			{
				throw new ArgumentOutOfRangeException(valueName, $"{valueName} must be in {{ {string.Join(", ", targetValue.Select(v => v.ToString()))} }}");
			}
			return value;
		}

		public static T ArgMustBeLesserOrEqualsThan<T>(this T value, T max, [CallerArgumentExpression("value")] string valueName = "") where T : IComparable
		{
			if (value.CompareTo(max) > 0)
			{
				throw new ArgumentOutOfRangeException(valueName, $"{valueName} must be lesser than {max}");
			}
			return value;
		}

		public static T ArgMustBeLesserThan<T>(this T value, T max, [CallerArgumentExpression("value")] string valueName = "") where T : IComparable
		{
			if (value.CompareTo(max) >= 0)
			{
				throw new ArgumentOutOfRangeException(valueName, value, $"{valueName} must be lesser or equals than {max}");
			}
			return value;
		}

		public static T ArgMustBeGreaterOrEqualsThan<T>(this T value, T min, [CallerArgumentExpression("value")] string valueName = "") where T : IComparable
		{
			if (value.CompareTo(min) < 0)
			{
				throw new ArgumentOutOfRangeException(valueName, value, $"{valueName} must be greater or equals than {min}");
			}
			return value;
		}

		public static T ArgMustBeGreaterThan<T>(this T value, T min, [CallerArgumentExpression("value")] string valueName = "") where T : IComparable
		{
			if (value.CompareTo(min) <= 0)
			{
				throw new ArgumentOutOfRangeException(valueName, value, $"{valueName} must be greater than {min}");
			}
			return value;
		}

		public static T ArgMustBeBetween<T>(this T value, T min, T max, [CallerArgumentExpression("value")] string valueName = "") where T : IComparable
		{
			if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0) 
			{
				throw new ArgumentOutOfRangeException(valueName, value, $"{valueName} must be between {min} and {max}");
			}
			return value;
		}
	}
}
