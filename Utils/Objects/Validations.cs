using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Utils.Objects;

public abstract class CheckBase<T>
{
	private readonly T value;

	public T Value {
		get {
			if (Errors.Count > 0) ThrowErrors();
			return value;
		}
	}

	public string Name { get; }

	protected List<Exception> Errors { get; } = new List<Exception>();

	public CheckBase(T value, string name)
	{
		this.value = value;
		this.Name = name;
	}

	public CheckBase<T> MustNotBeNull()
	{
		if (Value is null) OnNull();
		return this;
	}
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

	protected abstract void OnNull();
	protected virtual void OnError(Exception exception) => Errors.Add(exception);
	public virtual void ThrowErrors() 
	{
		if (Errors.Count == 0) return;
		if (Errors.Count == 1) throw Errors[0];
		throw new Exception($"{Name}{Environment.NewLine} - {string.Join(Environment.NewLine + " - ", Errors.Select(e=>e.Message))}");
	}

	public static implicit operator T(CheckBase<T> checkBase) => checkBase.Value;
}

public class Arg<T> : CheckBase<T>
{

	public Arg(T value, [CallerArgumentExpression(nameof(value))] string name = "")
		: base(value, name) { }

	protected override void OnNull() => throw new ArgumentNullException(Name);

	public override void ThrowErrors()
	{
		if (Errors.Count == 0) return;
		throw new ArgumentException($"{Name}{Environment.NewLine} - {string.Join(Environment.NewLine + " - ", Errors.Select(e => e.Message))}", Name);
	}
}

public class Variable<T> : CheckBase<T>
{
	public Variable(T value, [CallerArgumentExpression(nameof(value))] string name = "")
		: base(value, name) { }

	protected override void OnNull() => throw new NullReferenceException(Name);
}

public static class Validations
{
	public static Arg<T> Arg<T>(this T value, [CallerArgumentExpression(nameof(value))] string name = "") => new Arg<T>(value, name);
	public static Arg<T> Variable<T>(this T value, [CallerArgumentExpression(nameof(value))] string name = "") => new Arg<T>(value, name);

	public static Exception MustBeOfRank(this Array value, int rank)
	{
		if (value.Rank != rank) return null;
		throw new Exception($"must be of rank {rank}");
	}

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

	public static T ArgMustNotBeNull<T>(this T value, [CallerArgumentExpression(nameof(value))] string valueName = "")
		where T : class
	{
		if (value is null)
		{
			throw new ArgumentNullException(valueName);
		}
		return value;
	}

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

	public static void ArgMustBeOfRank(this Array value, int size, [CallerArgumentExpression(nameof(value))] string valueName = "")
	{
		if (value.Rank != size)
		{
#pragma warning disable S112 // General exceptions should never be thrown
			throw new ArgumentException($"{valueName} must be of rank {size}");
#pragma warning restore S112 // General exceptions should never be thrown
		}
	}

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

	public static void ArgMustBeOfSize<T>(this IReadOnlyCollection<T> value, int size, [CallerArgumentExpression(nameof(value))] string valueName = "")
	{
		if (value.Count != size)
		{
			throw new ArgumentOutOfRangeException(valueName, value.Count, $"{valueName} must contain {size} elements");
		}
	}

    public static void ArgMustNotBeEmpty<T>(this IReadOnlyCollection<T> value, [CallerArgumentExpression(nameof(value))] string valueName = "")
    {
        if (value.Count == 0)
        {
            throw new ArgumentOutOfRangeException(valueName, value.Count, $"{valueName} must contain at least one element");
        }
    }

    public static void ValueSizeMustBeMultipleOf<T>(this IReadOnlyCollection<T> value, int multiple, [CallerArgumentExpression(nameof(value))] string valueName = "")
	{
		if (value.Count % multiple != 0)
		{
			throw new ArrayDimensionException($"{valueName}.Length must be a multiple of {multiple}");
		}
	}

	public static void ArgSizeMustBeMultipleOf<T>(this IReadOnlyCollection<T> value, int multiple, [CallerArgumentExpression(nameof(value))] string valueName = "")
	{
		if (value.Count % multiple != 0)
		{
			throw new ArgumentException(valueName, $"{valueName}.Length must be a multiple of {multiple}");
		}
	}

	public static T ArgMustBeEqualsTo<T>(this T value, T targetValue, [CallerArgumentExpression(nameof(value))] string valueName = "") where T : IComparable
	{
		if (value.CompareTo(targetValue) != 0)
		{
			throw new ArgumentOutOfRangeException(valueName, $"{valueName} must be equal to {targetValue}");
		}
		return value;
	}

	public static T ArgMustBeIn<T>(this T value, T[] targetValue, [CallerArgumentExpression(nameof(value))] string valueName = "") where T : IComparable
	{
		if (value.NotIn(targetValue))
		{
			throw new ArgumentOutOfRangeException(valueName, $"{valueName} must be in {{ {string.Join(", ", targetValue.Select(v => v.ToString()))} }}");
		}
		return value;
	}

	public static T ArgMustBeLesserOrEqualsThan<T>(this T value, T max, [CallerArgumentExpression(nameof(value))] string valueName = "") where T : IComparable
	{
		if (value.CompareTo(max) > 0)
		{
			throw new ArgumentOutOfRangeException(valueName, $"{valueName} must be lesser than {max}");
		}
		return value;
	}

	public static T ArgMustBeLesserThan<T>(this T value, T max, [CallerArgumentExpression(nameof(value))] string valueName = "") where T : IComparable
	{
		if (value.CompareTo(max) >= 0)
		{
			throw new ArgumentOutOfRangeException(valueName, value, $"{valueName} must be lesser or equals than {max}");
		}
		return value;
	}

	public static T ArgMustBeGreaterOrEqualsThan<T>(this T value, T min, [CallerArgumentExpression(nameof(value))] string valueName = "") where T : IComparable
	{
		if (value.CompareTo(min) < 0)
		{
			throw new ArgumentOutOfRangeException(valueName, value, $"{valueName} must be greater or equals than {min}");
		}
		return value;
	}

	public static T ArgMustBeGreaterThan<T>(this T value, T min, [CallerArgumentExpression(nameof(value))] string valueName = "") where T : IComparable
	{
		if (value.CompareTo(min) <= 0)
		{
			throw new ArgumentOutOfRangeException(valueName, value, $"{valueName} must be greater than {min}");
		}
		return value;
	}

	public static T ArgMustBeBetween<T>(this T value, T min, T max, [CallerArgumentExpression(nameof(value))] string valueName = "") where T : IComparable
	{
		if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
		{
			throw new ArgumentOutOfRangeException(valueName, value, $"{valueName} must be between {min} and {max}");
		}
		return value;
	}

	public static T ArgMustBeANumber<T>(this T value, [CallerArgumentExpression(nameof(value))] string valueName = "") where T : INumberBase<T>
	{
		if (T.IsNaN(value)) throw new ArgumentOutOfRangeException(valueName, $"{value} in not a number");
		return value;
	}

	public static T ArgMustBe<T>(this T value, Func<T, bool> validationFunction, string message, [CallerArgumentExpression(nameof(value))] string valueName = "")
	{
		validationFunction.ArgMustNotBeNull();
		if (!validationFunction(value)) throw new ArgumentException(message, valueName);
		return value;
	}

	public static T ArgMustNotBe<T>(this T value, Func<T, bool> validationFunction, string message, [CallerArgumentExpression(nameof(value))] string valueName = "")
	{
		validationFunction.ArgMustNotBeNull();
		if (validationFunction(value)) throw new ArgumentException(message, valueName);
		return value;
	}
}
