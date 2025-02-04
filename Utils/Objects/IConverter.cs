using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Utils.Collections;

namespace Utils.Objects;

/// <summary>
/// Defines a contract for converting objects between different types.
/// </summary>
public interface IConverter
{
	/// <summary>
	/// Determines whether this converter instance can convert from <paramref name="sourceType"/> to <paramref name="targetType"/>.
	/// </summary>
	/// <param name="sourceType">The source <see cref="Type"/>.</param>
	/// <param name="targetType">The target <see cref="Type"/>.</param>
	/// <returns>
	/// <see langword="true"/> if this instance can perform the conversion; otherwise <see langword="false"/>.
	/// </returns>
	bool CanConvert(Type sourceType, Type targetType);

	/// <summary>
	/// Determines whether this converter instance can convert from <typeparamref name="TFrom"/> to <typeparamref name="TTo"/>.
	/// </summary>
	/// <typeparam name="TFrom">The source type.</typeparam>
	/// <typeparam name="TTo">The target type.</typeparam>
	/// <returns>
	/// <see langword="true"/> if this instance can perform the conversion; otherwise <see langword="false"/>.
	/// </returns>
	bool CanConvert<TFrom, TTo>();

	/// <summary>
	/// Converts the specified <paramref name="value"/> from type <typeparamref name="TFrom"/> to type <typeparamref name="TTo"/>.
	/// </summary>
	/// <typeparam name="TFrom">The source type.</typeparam>
	/// <typeparam name="TTo">The target type.</typeparam>
	/// <param name="value">The value to convert.</param>
	/// <returns>A value of type <typeparamref name="TTo"/>.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown if the conversion cannot be performed.
	/// </exception>
	TTo Convert<TFrom, TTo>(TFrom value);

	/// <summary>
	/// Converts the specified <paramref name="value"/> to the specified <paramref name="targetType"/>.
	/// </summary>
	/// <param name="value">The value to convert.</param>
	/// <param name="targetType">The target <see cref="Type"/>.</param>
	/// <returns>An <see cref="object"/> of type <paramref name="targetType"/>.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown if the conversion cannot be performed.
	/// </exception>
	object Convert(object value, Type targetType);

	/// <summary>
	/// Attempts to convert the specified <paramref name="value"/> from <paramref name="sourceType"/> to <paramref name="targetType"/>.
	/// </summary>
	/// <param name="value">The value to convert.</param>
	/// <param name="sourceType">The source <see cref="Type"/>.</param>
	/// <param name="targetType">The target <see cref="Type"/>.</param>
	/// <param name="result">When this method returns, contains the converted result if the conversion succeeded, or <see langword="null"/> if it failed.</param>
	/// <returns>
	/// <see langword="true"/> if the conversion was successful; otherwise <see langword="false"/>.
	/// </returns>
	bool TryConvert(object value, Type sourceType, Type targetType, out object? result);
}

/// <summary>
/// Aggregates multiple <see cref="IConverter"/> instances and tries them in order.
/// </summary>
public sealed class ConverterAggregator : IConverter
{
	private readonly List<IConverter> _converters = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="ConverterAggregator"/> class with no converters.
	/// </summary>
	public ConverterAggregator() { }

	/// <summary>
	/// Initializes a new instance of the <see cref="ConverterAggregator"/> class with a specified collection of converters.
	/// </summary>
	/// <param name="converters">A collection of converters to aggregate.</param>
	public ConverterAggregator(IEnumerable<IConverter> converters)
	{
		_converters.AddRange(converters.Distinct());
	}

	/// <summary>
	/// Adds an <see cref="IConverter"/> to the aggregator if it is not already present.
	/// </summary>
	/// <param name="converter">The converter to add.</param>
	public void Add(IConverter converter)
	{
		if (!_converters.Contains(converter))
		{
			_converters.Add(converter);
		}
	}

	/// <inheritdoc />
	public bool CanConvert(Type sourceType, Type targetType)
		=> _converters.Any(c => c.CanConvert(sourceType, targetType));

	/// <inheritdoc />
	public bool CanConvert<TFrom, TTo>()
		=> CanConvert(typeof(TFrom), typeof(TTo));

	/// <inheritdoc />
	public TTo Convert<TFrom, TTo>(TFrom value)
	{
		if (!TryConvert(value, typeof(TFrom), typeof(TTo), out var result))
		{
			throw new InvalidOperationException(
				$"Cannot convert from {typeof(TFrom).FullName} to {typeof(TTo).FullName}."
			);
		}
		return (TTo)result!;
	}

	/// <inheritdoc />
	public object Convert(object value, Type targetType)
	{
		if (!TryConvert(value, value.GetType(), targetType, out var result))
		{
			throw new InvalidOperationException(
				$"Cannot convert from {value.GetType().FullName} to {targetType.FullName}."
			);
		}
		return result!;
	}

	/// <inheritdoc />
	public bool TryConvert(object value, Type sourceType, Type targetType, out object? result)
	{
		foreach (var converter in _converters)
		{
			if (converter.CanConvert(sourceType, targetType)
				&& converter.TryConvert(value, sourceType, targetType, out var tempResult))
			{
				result = tempResult;
				return true;
			}
		}

		result = null;
		return false;
	}
}

/// <summary>
/// A simple converter implementation that uses a dictionary of delegates to convert values between types.
/// </summary>
/// <seealso cref="Utils.Objects.IConverter" />
public class SimpleConverter : IConverter, IEnumerable<Delegate>
{
	private readonly Dictionary<Type, Dictionary<Type, Delegate>> _converters = new();

	/// <summary>
	/// Adds a specific conversion delegate to this converter.
	/// The delegate must have exactly one parameter and a return type of the target type.
	/// </summary>
	/// <param name="d">A function delegate that converts one type to another.</param>
	/// <exception cref="ArgumentException">
	/// Thrown if the provided delegate has more than one parameter.
	/// </exception>
	public void Add(Delegate d)
	{
		var method = d.Method;
		var parameters = method.GetParameters();

		if (parameters.Length != 1)
		{
			throw new ArgumentException(
				$"Delegate '{method.Name}' must have exactly one parameter to be used for conversion."
			);
		}

		var sourceType = parameters[0].ParameterType;
		var targetType = method.ReturnType;

		var targets = _converters.GetOrAdd(sourceType, () => []);
		targets[targetType] = d;
	}

	/// <inheritdoc />
	public bool CanConvert(Type sourceType, Type targetType)
	{
		// Traverse the inheritance chain looking for a matching converter
		for (var type = sourceType; type != typeof(object) && type is not null; type = type.BaseType)
		{
			if (_converters.TryGetValue(type, out var targets)
				&& targets.ContainsKey(targetType))
			{
				return true;
			}
		}
		return false;
	}

	/// <inheritdoc />
	public bool CanConvert<TFrom, TTo>()
		=> CanConvert(typeof(TFrom), typeof(TTo));

	/// <inheritdoc />
	public TTo Convert<TFrom, TTo>(TFrom value)
	{
		if (TryConvert(value, typeof(TFrom), typeof(TTo), out var result))
		{
			return (TTo)result!;
		}

		throw new InvalidOperationException(
			$"Cannot convert from {typeof(TFrom).FullName} to {typeof(TTo).FullName}."
		);
	}

	/// <inheritdoc />
	public object Convert(object value, Type targetType)
	{
		var sourceType = value.GetType();
		if (TryConvert(value, sourceType, targetType, out var result))
		{
			return result!;
		}

		throw new InvalidOperationException(
			$"Cannot convert from {sourceType.FullName} to {targetType.FullName}."
		);
	}

	/// <inheritdoc />
	public bool TryConvert(object value, Type sourceType, Type targetType, out object? result)
	{
		for (var type = sourceType; type != typeof(object) && type is not null; type = type.BaseType)
		{
			if (_converters.TryGetValue(type, out var targets)
				&& targets.TryGetValue(targetType, out var converter))
			{
				result = converter.DynamicInvoke(value);
				return true;
			}
		}

		result = null;
		return false;
	}

	/// <summary>
	/// Returns an enumerator that iterates through the conversion delegates.
	/// </summary>
	/// <returns>An enumerator for all registered delegates.</returns>
	IEnumerator<Delegate> IEnumerable<Delegate>.GetEnumerator()
		=> _converters.SelectMany(kvp => kvp.Value.Values).GetEnumerator();

	/// <summary>
	/// Returns an enumerator that iterates through the conversion delegates.
	/// </summary>
	/// <returns>An enumerator for all registered delegates.</returns>
	IEnumerator IEnumerable.GetEnumerator()
		=> _converters.SelectMany(kvp => kvp.Value.Values).GetEnumerator();
}

/// <summary>
/// An abstract base class that automatically adds public instance and static methods 
/// (with exactly one parameter) as conversion delegates.
/// </summary>
/// <remarks>
/// By inheriting this class, any single-parameter methods you define are registered 
/// as valid conversion operations in the constructor.
/// For example:
/// <code>
/// public class NewConverter : ConverterBase
/// {
///     // This method will automatically be registered to convert string to int.
///     public int ToInt32(string value) => int.Parse(value);
/// }
/// </code>
/// </remarks>
/// <seealso cref="Utils.Objects.SimpleConverter" />
public abstract class ConverterBase : SimpleConverter
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ConverterBase"/> class and 
	/// registers all single-parameter methods in the inheritance chain as conversion delegates.
	/// </summary>
	protected ConverterBase()
	{
		// We will try to create delegates of type Func<TSource, TTarget>
		var funcGenericType = typeof(Func<,>);

		// Traverse the inheritance hierarchy up to (but not including) ConverterBase
		for (Type? t = GetType(); t != typeof(ConverterBase) && t is not null; t = t.BaseType)
		{
			var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
			foreach (var method in methods)
			{
				var parameters = method.GetParameters();
				if (parameters.Length == 1 && !method.IsSpecialName)
				{
					// Create the corresponding delegate (Func<paramType, returnType>)
					var delegateType = funcGenericType.MakeGenericType(parameters[0].ParameterType, method.ReturnType);
					Delegate d = method.IsStatic
						? method.CreateDelegate(delegateType)
						: method.CreateDelegate(delegateType, this);

					Add(d);
				}
			}
		}
	}
}
