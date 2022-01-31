using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Utils.Objects
{
	public static class Validations
	{
		public static void ArgMustNotBeNull(this object value, [CallerArgumentExpression("value")] string valueName = "")
		{
			if (value == null)
			{
				throw new ArgumentNullException(valueName);
			}
		}

		public static void ValueMustNotBeNull(this object value, [CallerArgumentExpression("value")] string valueName = "")
		{
			if (value == null)
			{
				throw new NullReferenceException();
			}
		}

		public static void ArgMustBeLesserOrEqualsThan<T>(this T value, T max, [CallerArgumentExpression("value")] string valueName = "") where T : IComparable
		{
			if (value.CompareTo(max) > 0)
			{
				throw new ArgumentOutOfRangeException(valueName, $"{valueName} must be lesser than {max}");
			}
		}

		public static void ArgMustBeLesserThan<T>(this T value, T max, [CallerArgumentExpression("value")] string valueName = "") where T : IComparable
		{
			if (value.CompareTo(max) >= 0)
			{
				throw new ArgumentOutOfRangeException(valueName, value, $"{valueName} must be lesser or equals than {max}");
			}
		}

		public static void ArgMustBeGreaterOrEqualsThan<T>(this T value, T min, [CallerArgumentExpression("value")] string valueName = "") where T : IComparable
		{
			if (value.CompareTo(min) < 0)
			{
				throw new ArgumentOutOfRangeException(valueName, value, $"{valueName} must be greater or equals than {min}");
			}
		}

		public static void ArgMustBeGreaterThan<T>(this T value, T min, [CallerArgumentExpression("value")] string valueName = "") where T : IComparable
		{
			if (value.CompareTo(min) <= 0)
			{
				throw new ArgumentOutOfRangeException(valueName, value, $"{valueName} must be greater than {min}");
			}
		}

		public static void ArgMustBeBetween<T>(this T value, T min, T max, [CallerArgumentExpression("value")] string valueName = "") where T : IComparable
		{
			if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0) 
			{
				throw new ArgumentOutOfRangeException(valueName, value, $"{valueName} must be between {min} and {max}");
			}
		}
	}
}
