using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Utils.Objects;

/// <summary>
/// Utility class that provides methods to parse strings into various types using reflection and expressions.
/// </summary>
public static class Parsers
{
	// Delegate definitions for different parsing methods
	private delegate bool TryParseDelegate(string s, IFormatProvider formatProvider, out object value);
	private delegate object ParseDelegate(string s, IFormatProvider formatProvider);
	private delegate object ConstructorDelegate(string s);

	// Cached Type references
	private static readonly Type typeOfString = typeof(string);
	private static readonly Type typeOfIFormatProvider = typeof(IFormatProvider);

	/// <summary>
	/// Contains the parsing methods (TryParse, Parse, Constructor) for a specific type.
	/// </summary>
	private class ParseMethods
	{
		public bool CanParse { get; }
		public TryParseDelegate TryParse { get; }
		public ParseDelegate Parse { get; }
		public ConstructorDelegate Constructor { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ParseMethods"/> class by building the parsing methods for the specified type.
		/// </summary>
		/// <param name="type">The type to build parsing methods for.</param>
		public ParseMethods(Type type)
		{
			type.Arg().MustNotBeNull();

			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
			{
				type = type.GetGenericArguments()[0];
			}

			TryParse = BuildTryParse(type);
			Parse = BuildParse(type);
			Constructor = BuildConstructor(type);

			CanParse = TryParse != null || Parse != null || Constructor != null;
		}

		public ParseMethods(TryParseDelegate tryParse, ParseDelegate parse, ConstructorDelegate constructor)
		{
			TryParse = tryParse;
			Parse = parse;
			Constructor = constructor;
			CanParse = TryParse != null || Parse != null || Constructor != null;
		}

		/// <summary>
		/// Builds a constructor delegate for types that have a constructor accepting a string parameter.
		/// </summary>
		private ConstructorDelegate BuildConstructor(Type type)
		{
			var constructorInfo = type.GetConstructor(new[] { typeOfString });
			if (constructorInfo != null)
			{
				var sParameter = Expression.Parameter(typeOfString, "s");
				var returnLabel = Expression.Label(typeof(object));

				return Expression.Lambda<ConstructorDelegate>(
					Expression.Block(
						Expression.Return(returnLabel, Expression.Convert(Expression.New(constructorInfo, sParameter), typeof(object))),
						Expression.Label(returnLabel, Expression.Default(typeof(object)))
					),
					sParameter
				).Compile();
			}
			return null;
		}

		/// <summary>
		/// Builds a TryParse delegate for the specified type.
		/// </summary>
		private TryParseDelegate BuildTryParse(Type type)
		{
			type.Arg().MustNotBeNull();
			MethodInfo numberTryParseMethod = type.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeOfString, typeof(NumberStyles), typeOfIFormatProvider, type.MakeByRefType() }, null);
			if (numberTryParseMethod is not null)
			{
				var strParameter = Expression.Parameter(typeOfString, "s");
				var numberStyleConstant = Expression.Constant(NumberStyles.Any);
				var formatProviderParameter = Expression.Parameter(typeof(IFormatProvider), "formatProvider");
				var resultParameter = Expression.Parameter(typeof(object).MakeByRefType(), "result");
				var returnValueVariable = Expression.Parameter(typeof(bool));
				var valueVariable = Expression.Parameter(type, "value");
				var returnLabel = Expression.Label(typeof(bool));
				var lambda = Expression.Lambda<TryParseDelegate>(
					Expression.Block(
						typeof(bool),
						new[] { returnValueVariable, valueVariable },
						Expression.Assign(
							returnValueVariable,
							Expression.Call(numberTryParseMethod, strParameter, numberStyleConstant, formatProviderParameter, valueVariable)
						),
						Expression.Assign(resultParameter, Expression.Convert(valueVariable, typeof(object))),
						Expression.Return(returnLabel, returnValueVariable, typeof(bool)),
						Expression.Label(returnLabel, Expression.Default(typeof(bool)))
					),
					strParameter, formatProviderParameter, resultParameter
				);
				return lambda.Compile();
			}
			MethodInfo tryParseMethod = type.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeOfString, typeOfIFormatProvider, type.MakeByRefType() }, null);
			if (tryParseMethod is not null)
			{
				var strParameter = Expression.Parameter(typeOfString, "s");
				var formatProviderParameter = Expression.Parameter(typeof(IFormatProvider), "formatProvider");
				var resultParameter = Expression.Parameter(typeof(object).MakeByRefType(), "result");
				var returnValueVariable = Expression.Parameter(typeof(bool));
				var valueVariable = Expression.Parameter(type, "value");
				var returnLabel = Expression.Label(typeof(bool));
				var lambda = Expression.Lambda<TryParseDelegate>(
					Expression.Block(
						typeof(bool),
						new[] { returnValueVariable, valueVariable },
						Expression.Assign(
							returnValueVariable,
							Expression.Call(tryParseMethod, strParameter, formatProviderParameter, valueVariable)
						),
						Expression.Assign(resultParameter, Expression.Convert(valueVariable, typeof(object))),
						Expression.Return(returnLabel, returnValueVariable, typeof(bool)),
						Expression.Label(returnLabel, Expression.Default(typeof(bool)))
					),
					strParameter, formatProviderParameter, resultParameter
				);
				return lambda.Compile();
			}
			tryParseMethod = type.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeOfString, type.MakeByRefType() }, null);
			if (tryParseMethod is not null)
			{
				var strParameter = Expression.Parameter(typeOfString, "s");
				var formatProviderParameter = Expression.Parameter(typeof(IFormatProvider), "formatProvider");
				var resultParameter = Expression.Parameter(typeof(object).MakeByRefType(), "result");
				var returnValueVariable = Expression.Parameter(typeof(bool));
				var valueVariable = Expression.Parameter(type, "value");
				var returnLabel = Expression.Label(typeof(bool));
				var lambda = Expression.Lambda<TryParseDelegate>(
					Expression.Block(
						typeof(bool),
						new[] { returnValueVariable, valueVariable },
						Expression.Assign(
							returnValueVariable,
							Expression.Call(tryParseMethod, strParameter, valueVariable)
						),
						Expression.Assign(resultParameter, Expression.Convert(valueVariable, typeof(object))),
						Expression.Return(returnLabel, returnValueVariable, typeof(bool)),
						Expression.Label(returnLabel, Expression.Default(typeof(bool)))
					),
					strParameter, formatProviderParameter, resultParameter
				);
				return lambda.Compile();
			}

			return null;
		}

		/// <summary>
		/// Builds a Parse delegate for the specified type.
		/// </summary>
		private ParseDelegate BuildParse(Type type)
		{
			MethodInfo parseMethod = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null,
				[typeOfString, typeOfIFormatProvider], null)
				?? type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null,
				[typeOfString], null);

			if (parseMethod != null)
			{
				var strParameter = Expression.Parameter(typeOfString, "s");
				var formatProviderParameter = Expression.Parameter(typeOfIFormatProvider, "formatProvider");
				var returnLabel = Expression.Label(typeof(object));

				return Expression.Lambda<ParseDelegate>(
					Expression.Block(
						Expression.Return(returnLabel, Expression.Convert(Expression.Call(parseMethod, strParameter, formatProviderParameter), typeof(object))),
						Expression.Label(returnLabel, Expression.Default(typeof(object)))
					),
					strParameter, formatProviderParameter
				).Compile();
			}
			return null;
		}
	}

	// Cache of parsers for different types
	private static readonly Dictionary<Type, ParseMethods> parsers = new Dictionary<Type, ParseMethods>();

	static Parsers()
	{
		// Adding default parser for DateTime
		parsers.Add(typeof(DateTime), new ParseMethods(
			new TryParseDelegate((string s, IFormatProvider formatProvider, out object value) =>
			{
				var result = DateTime.TryParse(s, formatProvider, DateTimeStyles.None, out DateTime datetime);
				value = datetime;
				return result;
			}),
			new ParseDelegate((string s, IFormatProvider formatProvider) => DateTime.Parse(s, formatProvider)),
			null
		));
	}

	/// <summary>
	/// Retrieves the parsing methods for a specified type.
	/// </summary>
	/// <param name="type">The type to retrieve parsing methods for.</param>
	/// <returns>The <see cref="ParseMethods"/> instance containing the parsing methods for the specified type.</returns>
	private static ParseMethods GetParseMethods(Type type)
	{
		type.Arg().MustNotBeNull();

		if (!parsers.TryGetValue(type, out var parseMethods))
		{
			parseMethods = new ParseMethods(type);
			parsers[type] = parseMethods;
		}

		return parseMethods;
	}

	/// <summary>
	/// Checks if a type can be parsed.
	/// </summary>
	/// <param name="type">The type to check.</param>
	/// <returns><c>true</c> if the type can be parsed; otherwise, <c>false</c>.</returns>
	public static bool CanParse<T>() => CanParse(typeof(T));

	/// <summary>
	/// Checks if a type can be parsed.
	/// </summary>
	/// <param name="type">The type to check.</param>
	/// <returns><c>true</c> if the type can be parsed; otherwise, <c>false</c>.</returns>
	public static bool CanParse(Type type)
	{
		type.Arg().MustNotBeNull();

		if (type.IsEnum) return true;

		if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
		{
			return CanParse(type.GetGenericArguments()[0]);
		}

		return GetParseMethods(type).CanParse;
	}

	/// <summary>
	/// Parses a string value to a specified type.
	/// </summary>
	/// <typeparam name="T">The type to parse the value to.</typeparam>
	/// <param name="value">The string value to parse.</param>
	/// <returns>The parsed value of type <typeparamref name="T"/>.</returns>
	public static T Parse<T>(string value) => (T)Parse(value, typeof(T));

	/// <summary>
	/// Parses a string value to a specified type using multiple format providers.
	/// </summary>
	/// <typeparam name="T">The type to parse the value to.</typeparam>
	/// <param name="value">The string value to parse.</param>
	/// <param name="formatsProviders">An enumerable of format providers to use for parsing.</param>
	/// <returns>The parsed value of type <typeparamref name="T"/>.</returns>
	public static T Parse<T>(string value, IEnumerable<IFormatProvider> formatsProviders) => (T)Parse(value, typeof(T), formatsProviders);

	/// <summary>
	/// Parses a string value to a specified type using multiple format providers.
	/// </summary>
	/// <typeparam name="T">The type to parse the value to.</typeparam>
	/// <param name="value">The string value to parse.</param>
	/// <param name="formatsProviders">An array of format providers to use for parsing.</param>
	/// <returns>The parsed value of type <typeparamref name="T"/>.</returns>
	public static T Parse<T>(string value, params IFormatProvider[] formatsProviders) => (T)Parse(value, typeof(T), formatsProviders);

	/// <summary>
	/// Converts a string value to the specified type using the current culture.
	/// </summary>
	/// <param name="value">The string value to convert.</param>
	/// <param name="type">The target type.</param>
	/// <returns>The converted value as an object of the specified type.</returns>
	public static object Parse(string value, Type type) => Parse(value, type, CultureInfo.CurrentCulture);

	/// <summary>
	/// Converts a string value to the specified type using multiple format providers.
	/// </summary>
	/// <param name="value">The string value to convert.</param>
	/// <param name="type">The target type.</param>
	/// <param name="formatsProviders">An array of format providers to use for conversion.</param>
	/// <returns>The converted value as an object of the specified type.</returns>
	public static object Parse(string value, Type type, params IFormatProvider[] formatsProviders) => Parse(value, type, (IEnumerable<IFormatProvider>)formatsProviders);

	/// <summary>
	/// Converts a string value to the specified type using multiple format providers.
	/// </summary>
	/// <param name="value">The string value to convert.</param>
	/// <param name="type">The target type.</param>
	/// <param name="formatsProviders">An enumerable of format providers to use for conversion.</param>
	/// <returns>The converted value as an object of the specified type.</returns>
	public static object Parse(string value, Type type, IEnumerable<IFormatProvider> formatsProviders)
	{
		if (type.IsEnum)
		{
			return Enum.Parse(type, value, true);
		}

		if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
		{
			return Parse(value, type.GetGenericArguments()[0], formatsProviders);
		}

		var methods = GetParseMethods(type);

		if (methods.TryParse != null || methods.Parse != null)
		{
			foreach (var formatProvider in formatsProviders)
			{
				if (methods.TryParse?.Invoke(value, formatProvider, out var result) == true)
				{
					return result;
				}

				if (methods.Parse != null)
				{
					try
					{
						return methods.Parse(value, formatProvider);
					}
					catch
					{
						// Ignore parse failures and continue to the next format provider
					}
				}
			}
		}

		if (methods.Constructor != null)
		{
			return methods.Constructor(value);
		}

		try
		{
			return Convert.ChangeType(value, type);
		}
		catch
		{
			if (string.IsNullOrEmpty(value))
			{
				return null;
			}
			throw;
		}
	}

	/// <summary>
	/// Parses a string value to a specified type, returning a default value if parsing fails.
	/// </summary>
	/// <typeparam name="T">The type to parse the value to.</typeparam>
	/// <param name="value">The string value to parse.</param>
	/// <param name="defaultValue">The default value to return if parsing fails.</param>
	/// <returns>The parsed value of type <typeparamref name="T"/> or the default value if parsing fails.</returns>
	public static T ParseOrDefault<T>(string value, T defaultValue = default)
	{
		var result = Parse(value, typeof(T));
		return (T)(result ?? defaultValue);
	}

	/// <summary>
	/// Parses a string value to a specified type using a format provider, returning a default value if parsing fails.
	/// </summary>
	/// <typeparam name="T">The type to parse the value to.</typeparam>
	/// <param name="value">The string value to parse.</param>
	/// <param name="formatProvider">The format provider to use for parsing.</param>
	/// <param name="defaultValue">The default value to return if parsing fails.</param>
	/// <returns>The parsed value of type <typeparamref name="T"/> or the default value if parsing fails.</returns>
	public static T ParseOrDefault<T>(string value, IFormatProvider formatProvider = null, T defaultValue = default)
		=> ParseOrDefault(value, new[] { formatProvider }, defaultValue);

	/// <summary>
	/// Parses a string value to a specified type using multiple format providers, returning a default value if parsing fails.
	/// </summary>
	/// <typeparam name="T">The type to parse the value to.</typeparam>
	/// <param name="value">The string value to parse.</param>
	/// <param name="formatsProviders">An array of format providers to use for parsing.</param>
	/// <param name="defaultValue">The default value to return if parsing fails.</param>
	/// <returns>The parsed value of type <typeparamref name="T"/> or the default value if parsing fails.</returns>
	public static T ParseOrDefault<T>(string value, IFormatProvider[] formatsProviders = null, T defaultValue = default)
	{
		var result = Parse(value, typeof(T), formatsProviders);
		return (T)(result ?? defaultValue);
	}
}
