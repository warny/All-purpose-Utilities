using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Utils.Objects;

/// <summary>
/// Provides a base implementation for value validation helpers that collect errors before throwing them.
/// </summary>
/// <typeparam name="T">Type of the value being validated.</typeparam>
public abstract class CheckBase<T>
{
	private readonly T value;

        /// <summary>
        /// Gets the validated value, throwing accumulated validation errors if any were recorded.
        /// </summary>
        public T Value {
                get {
                        if (Errors.Count > 0) ThrowErrors();
                        return value;
                }
        }

        /// <summary>
        /// Gets the human-readable name of the value being validated.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the collection of validation errors accumulated during the checks.
        /// </summary>
        protected List<Exception> Errors { get; } = new List<Exception>();

        /// <summary>
        /// Initializes a new instance of the <see cref="CheckBase{T}"/> class.
        /// </summary>
        /// <param name="value">Value to validate.</param>
        /// <param name="name">Name of the value for diagnostic messages.</param>
        public CheckBase(T value, string name)
	{
		this.value = value;
		this.Name = name;
	}

        /// <summary>
        /// Adds a validation rule ensuring the value is not <see langword="null"/>.
        /// </summary>
        /// <returns>The current validation instance for fluent chaining.</returns>
        public CheckBase<T> MustNotBeNull()
	{
		if (Value is null) OnNull();
		return this;
	}
        /// <summary>
        /// Executes custom validation checks and records produced exceptions.
        /// </summary>
        /// <param name="checks">Set of functions that return an exception when the value is invalid.</param>
        /// <returns>The current validation instance for fluent chaining.</returns>
        public CheckBase<T> Must(params Func<T, Exception>[] checks)
	{
		foreach (var check in checks)
		{
			var result = check(Value);
			if (result is not null)
			{
				OnError(result);
			}
		}
		return this;
	}

        /// <summary>
        /// Handles the null-case for the value.
        /// </summary>
        protected abstract void OnNull();

        /// <summary>
        /// Adds a validation error to the internal collection.
        /// </summary>
        /// <param name="exception">Exception describing the validation failure.</param>
        protected virtual void OnError(Exception exception) => Errors.Add(exception);

        /// <summary>
        /// Throws the accumulated validation errors, if any were recorded.
        /// </summary>
        /// <exception cref="Exception">Thrown with aggregated error information when multiple validations fail.</exception>
        public virtual void ThrowErrors()
	{
		if (Errors.Count == 0) return;
		if (Errors.Count == 1) throw Errors[0];
		throw new Exception($"{Name}{Environment.NewLine} - {string.Join(Environment.NewLine + " - ", Errors.Select(e=>e.Message))}");
	}

        /// <summary>
        /// Retrieves the validated value.
        /// </summary>
        /// <param name="checkBase">Validation instance whose value should be returned.</param>
        public static implicit operator T(CheckBase<T> checkBase) => checkBase.Value;
}

/// <summary>
/// Provides argument validation helpers that throw <see cref="ArgumentException"/> instances.
/// </summary>
/// <typeparam name="T">Type of the argument to validate.</typeparam>
public class Arg<T> : CheckBase<T>
{
        /// <summary>
        /// Initializes a new instance of the <see cref="Arg{T}"/> class.
        /// </summary>
        /// <param name="value">Argument value to validate.</param>
        /// <param name="name">Name of the argument being validated.</param>
        public Arg(T value, [CallerArgumentExpression(nameof(value))] string name = "")
                : base(value, name) { }

        /// <inheritdoc />
        protected override void OnNull() => throw new ArgumentNullException(Name);

        /// <summary>
        /// Throws the accumulated validation errors as a single <see cref="ArgumentException"/>.
        /// </summary>
        /// <exception cref="ArgumentException">Always thrown when at least one error has been recorded.</exception>
        public override void ThrowErrors()
	{
		if (Errors.Count == 0) return;
		throw new ArgumentException($"{Name}{Environment.NewLine} - {string.Join(Environment.NewLine + " - ", Errors.Select(e => e.Message))}", Name);
	}
}

/// <summary>
/// Provides validation helpers for mutable values that throw <see cref="NullReferenceException"/> when null.
/// </summary>
/// <typeparam name="T">Type of the value to validate.</typeparam>
public class Variable<T> : CheckBase<T>
{
        /// <summary>
        /// Initializes a new instance of the <see cref="Variable{T}"/> class.
        /// </summary>
        /// <param name="value">Value to validate.</param>
        /// <param name="name">Name of the value for diagnostics.</param>
        public Variable(T value, [CallerArgumentExpression(nameof(value))] string name = "")
                : base(value, name) { }

        /// <inheritdoc />
        protected override void OnNull() => throw new NullReferenceException(Name);
}

/// <summary>
/// Provides helper methods for validating arguments and values with consistent exception messages.
/// </summary>
public static class Validations
{
        /// <summary>
        /// Creates an <see cref="Arg{T}"/> validator for the provided value.
        /// </summary>
        /// <typeparam name="T">Type of the value to validate.</typeparam>
        /// <param name="value">Value to validate.</param>
        /// <param name="name">Name of the argument, automatically captured when possible.</param>
        /// <returns>A validator that can be used to chain additional checks.</returns>
        public static Arg<T> Arg<T>(this T value, [CallerArgumentExpression(nameof(value))] string name = "") => new Arg<T>(value, name);

        /// <summary>
        /// Creates a <see cref="Variable{T}"/> validator for the provided value.
        /// </summary>
        /// <typeparam name="T">Type of the value to validate.</typeparam>
        /// <param name="value">Value to validate.</param>
        /// <param name="name">Name of the variable, automatically captured when possible.</param>
        /// <returns>A validator that can be used to chain additional checks.</returns>
        public static Variable<T> Variable<T>(this T value, [CallerArgumentExpression(nameof(value))] string name = "") => new Variable<T>(value, name);

        /// <summary>
        /// Returns an exception describing the required rank for the provided array.
        /// </summary>
        /// <param name="value">Array to inspect.</param>
        /// <param name="rank">Expected rank.</param>
        /// <returns>An exception describing the rank constraint when it is met; otherwise <see langword="null"/>.</returns>
        public static Exception MustBeOfRank(this Array value, int rank)
	{
		if (value.Rank != rank) return null;
		throw new Exception($"must be of rank {rank}");
	}

        /// <summary>
        /// Returns an exception describing the required dimension sizes for the provided array.
        /// </summary>
        /// <param name="value">Array to inspect.</param>
        /// <param name="sizes">Expected sizes per dimension, or <see langword="null"/> for unconstrained dimensions.</param>
        /// <returns>An exception describing the size constraint when it is met; otherwise <see langword="null"/>.</returns>
        public static Exception MustBeOfSize(this Array value, int?[] sizes)
	{
		if (value.Rank != sizes.Length) return new ArrayDimensionException($"must be of rank {sizes.Length}");
		for (int i = 0; i < sizes.Length; i++)
		{
			if (sizes[i] != null && value.GetLength(i) != sizes[i].Value)
			{
				return new ArrayDimensionException($"must be of size [{string.Join(", ", sizes.Select(s=>s is null ? "free" : s.ToString()))}]");
			}
		}
		return null;
	}

        /// <summary>
        /// Ensures that a reference-type value is not <see langword="null"/>.
        /// </summary>
        /// <typeparam name="T">Type of the value being checked.</typeparam>
        /// <param name="value">Value to validate.</param>
        /// <param name="valueName">Name of the value for diagnostic messages.</param>
        /// <returns>The original value when it is not <see langword="null"/>.</returns>
        /// <exception cref="NullReferenceException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        public static T ValueMustNotBeNull<T>(this T value, [CallerArgumentExpression(nameof(value))] string valueName = "")
		where T : class
	{
		if (value is null)
		{
#pragma warning disable S112 // General exceptions should never be thrown
			throw new NullReferenceException($"{valueName} must not be null");
#pragma warning restore S112 // General exceptions should never be thrown
		}
		return value;
	}

        /// <summary>
        /// Validates that an array argument has the specified rank.
        /// </summary>
        /// <param name="value">Array to validate.</param>
        /// <param name="size">Expected rank.</param>
        /// <param name="valueName">Name of the argument for diagnostic messages.</param>
        /// <exception cref="ArgumentException">Thrown when the array rank is not the expected value.</exception>
        public static void ArgMustBeOfRank(this Array value, int size, [CallerArgumentExpression(nameof(value))] string valueName = "")
	{
		if (value.Rank != size)
		{
#pragma warning disable S112 // General exceptions should never be thrown
			throw new ArgumentException($"{valueName} must be of rank {size}");
#pragma warning restore S112 // General exceptions should never be thrown
		}
	}

        /// <summary>
        /// Validates that an array argument matches the specified dimension sizes.
        /// </summary>
        /// <param name="value">Array to validate.</param>
        /// <param name="sizes">Expected sizes per dimension, or <see langword="null"/> for unconstrained dimensions.</param>
        /// <param name="valueName">Name of the argument for diagnostic messages.</param>
        /// <exception cref="ArgumentException">Thrown when the array shape does not match the requested sizes.</exception>
        public static void ArgMustBeOfSizes(this Array value, int?[] sizes, [CallerArgumentExpression(nameof(value))] string valueName = "")
	{
		if (value.Rank != sizes.Length)
		{
#pragma warning disable S112 // General exceptions should never be thrown
			throw new ArgumentException($"{valueName} must be of rank {sizes.Length}");
#pragma warning restore S112 // General exceptions should never be thrown
		}
		for (int i = 0; i < sizes.Length; i++)
		{
			if (sizes[i] != null && value.GetLength(i) != sizes[i].Value)
			{
#pragma warning disable S112 // General exceptions should never be thrown
				throw new ArgumentException($"{valueName} must be of size {sizes[i]}");
#pragma warning restore S112 // General exceptions should never be thrown
			}
		}
	}

        /// <summary>
        /// Validates that an array argument matches the specified bounds for each dimension.
        /// </summary>
        /// <param name="value">Array to validate.</param>
        /// <param name="bounds">Expected lower and upper bounds for each dimension.</param>
        /// <param name="valueName">Name of the argument for diagnostic messages.</param>
        /// <exception cref="ArgumentException">Thrown when any bound does not match the requested value.</exception>
        public static void ArgMustBeOfBounds(this Array value, (int? lBound, int? uBound)[] bounds, [CallerArgumentExpression(nameof(value))] string valueName = "")
	{
		if (value.Rank != bounds.Length)
		{
#pragma warning disable S112 // General exceptions should never be thrown
			throw new ArgumentException($"{valueName} must be of rank {bounds.Length}");
#pragma warning restore S112 // General exceptions should never be thrown
		}
		for (int i = 0; i < bounds.Length; i++)
		{
			if (bounds[i].lBound != null && value.GetLowerBound(i) != bounds[i].lBound.Value)
			{
#pragma warning disable S112 // General exceptions should never be thrown
				throw new ArgumentException($"{valueName}[{i}] must be of lower bound {bounds[i].lBound}");
#pragma warning restore S112 // General exceptions should never be thrown
			}
			if (bounds[i].uBound != null && value.GetUpperBound(i) != bounds[i].uBound.Value)
			{
#pragma warning disable S112 // General exceptions should never be thrown
				throw new ArgumentException($"{valueName}[{i}] must be of upper bound {bounds[i].uBound}");
#pragma warning restore S112 // General exceptions should never be thrown
			}
		}
	}

        /// <summary>
        /// Validates that a collection argument contains exactly the specified number of elements.
        /// </summary>
        /// <typeparam name="T">Type of elements in the collection.</typeparam>
        /// <param name="value">Collection to validate.</param>
        /// <param name="size">Expected number of elements.</param>
        /// <param name="valueName">Name of the argument for diagnostic messages.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the collection length does not match the expected value.</exception>
        public static void ArgMustBeOfSize<T>(this IReadOnlyCollection<T> value, int size, [CallerArgumentExpression(nameof(value))] string valueName = "")
	{
		if (value.Count != size)
		{
			throw new ArgumentOutOfRangeException(valueName, value.Count, $"{valueName} must contain {size} elements");
		}
	}

        /// <summary>
        /// Validates that a collection argument contains at least one element.
        /// </summary>
        /// <typeparam name="T">Type of elements in the collection.</typeparam>
        /// <param name="value">Collection to validate.</param>
        /// <param name="valueName">Name of the argument for diagnostic messages.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the collection is empty.</exception>
        public static void ArgMustNotBeEmpty<T>(this IReadOnlyCollection<T> value, [CallerArgumentExpression(nameof(value))] string valueName = "")
        {
                if (value.Count == 0)
                {
                        throw new ArgumentOutOfRangeException(valueName, value.Count, $"{valueName} must contain at least one element");
                }
        }

        /// <summary>
        /// Ensures that a collection size is a multiple of the specified value.
        /// </summary>
        /// <typeparam name="T">Type of elements in the collection.</typeparam>
        /// <param name="value">Collection to validate.</param>
        /// <param name="multiple">Expected divisor of the collection length.</param>
        /// <param name="valueName">Name of the argument for diagnostic messages.</param>
        /// <exception cref="ArrayDimensionException">Thrown when the collection length is not a multiple of <paramref name="multiple"/>.</exception>
        public static void ValueSizeMustBeMultipleOf<T>(this IReadOnlyCollection<T> value, int multiple, [CallerArgumentExpression(nameof(value))] string valueName = "")
	{
		if (value.Count % multiple != 0)
		{
			throw new ArrayDimensionException($"{valueName}.Length must be a multiple of {multiple}");
		}
	}

        /// <summary>
        /// Ensures that the size of a collection argument is a multiple of the specified value.
        /// </summary>
        /// <typeparam name="T">Type of elements in the collection.</typeparam>
        /// <param name="value">Collection to validate.</param>
        /// <param name="multiple">Expected divisor of the collection length.</param>
        /// <param name="valueName">Name of the argument for diagnostic messages.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the collection length is not a multiple of <paramref name="multiple"/>.</exception>
        public static void ArgSizeMustBeMultipleOf<T>(this IReadOnlyCollection<T> value, int multiple, [CallerArgumentExpression(nameof(value))] string valueName = "")
	{
		if (value.Count % multiple != 0)
		{
			throw new ArgumentException(valueName, $"{valueName}.Length must be a multiple of {multiple}");
		}
	}

        /// <summary>
        /// Ensures that the argument is equal to the specified target value.
        /// </summary>
        /// <typeparam name="T">Type of the compared values.</typeparam>
        /// <param name="value">Value to validate.</param>
        /// <param name="targetValue">Value that <paramref name="value"/> must equal.</param>
        /// <param name="valueName">Name of the argument for diagnostic messages.</param>
        /// <returns>The validated value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the values are not equal.</exception>
        public static T ArgMustBeEqualsTo<T>(this T value, T targetValue, [CallerArgumentExpression(nameof(value))] string valueName = "") where T : IComparable
	{
		if (value.CompareTo(targetValue) != 0)
		{
			throw new ArgumentOutOfRangeException(valueName, $"{valueName} must be equal to {targetValue}");
		}
		return value;
	}

        /// <summary>
        /// Ensures that the argument is present in the provided list of allowed values.
        /// </summary>
        /// <typeparam name="T">Type of the compared values.</typeparam>
        /// <param name="value">Value to validate.</param>
        /// <param name="targetValue">Collection of allowed values.</param>
        /// <param name="valueName">Name of the argument for diagnostic messages.</param>
        /// <returns>The validated value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not present in the collection.</exception>
        public static T ArgMustBeIn<T>(this T value, T[] targetValue, [CallerArgumentExpression(nameof(value))] string valueName = "") where T : IComparable
	{
		if (value.NotIn(targetValue))
		{
			throw new ArgumentOutOfRangeException(valueName, $"{valueName} must be in {{ {string.Join(", ", targetValue.Select(v => v.ToString()))} }}");
		}
		return value;
	}

        /// <summary>
        /// Ensures that the argument is less than or equal to a maximum value.
        /// </summary>
        /// <typeparam name="T">Type of the compared values.</typeparam>
        /// <param name="value">Value to validate.</param>
        /// <param name="max">Maximum allowed value.</param>
        /// <param name="valueName">Name of the argument for diagnostic messages.</param>
        /// <returns>The validated value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is greater than the allowed maximum.</exception>
        public static T ArgMustBeLesserOrEqualsThan<T>(this T value, T max, [CallerArgumentExpression(nameof(value))] string valueName = "") where T : IComparable
	{
		if (value.CompareTo(max) > 0)
		{
			throw new ArgumentOutOfRangeException(valueName, $"{valueName} must be lesser than {max}");
		}
		return value;
	}

        /// <summary>
        /// Ensures that the argument is strictly less than a maximum value.
        /// </summary>
        /// <typeparam name="T">Type of the compared values.</typeparam>
        /// <param name="value">Value to validate.</param>
        /// <param name="max">Maximum exclusive value.</param>
        /// <param name="valueName">Name of the argument for diagnostic messages.</param>
        /// <returns>The validated value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is greater than or equal to the maximum.</exception>
        public static T ArgMustBeLesserThan<T>(this T value, T max, [CallerArgumentExpression(nameof(value))] string valueName = "") where T : IComparable
	{
		if (value.CompareTo(max) >= 0)
		{
			throw new ArgumentOutOfRangeException(valueName, value, $"{valueName} must be lesser or equals than {max}");
		}
		return value;
	}

        /// <summary>
        /// Ensures that the argument is greater than or equal to the specified minimum value.
        /// </summary>
        /// <typeparam name="T">Type of the compared values.</typeparam>
        /// <param name="value">Value to validate.</param>
        /// <param name="min">Minimum allowed value.</param>
        /// <param name="valueName">Name of the argument for diagnostic messages.</param>
        /// <returns>The validated value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than the allowed minimum.</exception>
        public static T ArgMustBeGreaterOrEqualsThan<T>(this T value, T min, [CallerArgumentExpression(nameof(value))] string valueName = "") where T : IComparable
	{
		if (value.CompareTo(min) < 0)
		{
			throw new ArgumentOutOfRangeException(valueName, value, $"{valueName} must be greater or equals than {min}");
		}
		return value;
	}

        /// <summary>
        /// Ensures that the argument is strictly greater than the specified minimum value.
        /// </summary>
        /// <typeparam name="T">Type of the compared values.</typeparam>
        /// <param name="value">Value to validate.</param>
        /// <param name="min">Minimum exclusive value.</param>
        /// <param name="valueName">Name of the argument for diagnostic messages.</param>
        /// <returns>The validated value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than or equal to the allowed minimum.</exception>
        public static T ArgMustBeGreaterThan<T>(this T value, T min, [CallerArgumentExpression(nameof(value))] string valueName = "") where T : IComparable
	{
		if (value.CompareTo(min) <= 0)
		{
			throw new ArgumentOutOfRangeException(valueName, value, $"{valueName} must be greater than {min}");
		}
		return value;
	}

        /// <summary>
        /// Ensures that the argument falls within the specified inclusive range.
        /// </summary>
        /// <typeparam name="T">Type of the compared values.</typeparam>
        /// <param name="value">Value to validate.</param>
        /// <param name="min">Minimum inclusive value.</param>
        /// <param name="max">Maximum inclusive value.</param>
        /// <param name="valueName">Name of the argument for diagnostic messages.</param>
        /// <returns>The validated value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside the allowed range.</exception>
        public static T ArgMustBeBetween<T>(this T value, T min, T max, [CallerArgumentExpression(nameof(value))] string valueName = "") where T : IComparable
	{
		if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
		{
			throw new ArgumentOutOfRangeException(valueName, value, $"{valueName} must be between {min} and {max}");
		}
		return value;
	}

        /// <summary>
        /// Ensures that the numeric argument is not <see cref="double.NaN"/> (or the type-specific equivalent).
        /// </summary>
        /// <typeparam name="T">Numeric type of the value.</typeparam>
        /// <param name="value">Value to validate.</param>
        /// <param name="valueName">Name of the argument for diagnostic messages.</param>
        /// <returns>The validated value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value represents an undefined number.</exception>
        public static T ArgMustBeANumber<T>(this T value, [CallerArgumentExpression(nameof(value))] string valueName = "") where T : INumberBase<T>
	{
		if (T.IsNaN(value)) throw new ArgumentOutOfRangeException(valueName, $"{value} in not a number");
		return value;
	}

        /// <summary>
        /// Validates that a predicate evaluates to <see langword="true"/> for the provided value.
        /// </summary>
        /// <typeparam name="T">Type of the value.</typeparam>
        /// <param name="value">Value to validate.</param>
        /// <param name="validationFunction">Predicate that defines the validation rule.</param>
        /// <param name="message">Error message used when the predicate fails.</param>
        /// <param name="valueName">Name of the argument for diagnostic messages.</param>
        /// <returns>The validated value.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="validationFunction"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when the predicate evaluates to <see langword="false"/>.</exception>
        public static T ArgMustBe<T>(this T value, Func<T, bool> validationFunction, string message, [CallerArgumentExpression(nameof(value))] string valueName = "")
	{
		validationFunction.Arg().MustNotBeNull();
		if (!validationFunction(value)) throw new ArgumentException(message, valueName);
		return value;
	}

        /// <summary>
        /// Validates that a predicate evaluates to <see langword="false"/> for the provided value.
        /// </summary>
        /// <typeparam name="T">Type of the value.</typeparam>
        /// <param name="value">Value to validate.</param>
        /// <param name="validationFunction">Predicate that defines the invalid condition.</param>
        /// <param name="message">Error message used when the predicate succeeds.</param>
        /// <param name="valueName">Name of the argument for diagnostic messages.</param>
        /// <returns>The validated value.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="validationFunction"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when the predicate evaluates to <see langword="true"/>.</exception>
        public static T ArgMustNotBe<T>(this T value, Func<T, bool> validationFunction, string message, [CallerArgumentExpression(nameof(value))] string valueName = "")
	{
		validationFunction.Arg().MustNotBeNull();
		if (validationFunction(value)) throw new ArgumentException(message, valueName);
		return value;
	}
}
