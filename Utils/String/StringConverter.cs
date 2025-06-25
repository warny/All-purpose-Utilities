using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml;
using Utils.Mathematics;
using Utils.Objects;
using Utils.Reflection;

namespace Utils.String
{
	/// <summary>
	/// Provides functionality to convert strings into various types using custom and predefined converters.
	/// </summary>
	public class StringConverter : IConverter
	{
		// A dictionary to store type-specific conversion delegates.
		private readonly Dictionary<Type, Delegate> converters = new Dictionary<Type, Delegate>();

		// The default culture info to use for formatting and parsing.
		private readonly CultureInfo defaultFormatProvider;

		// A NumberFormatInfo object for formatting/parsing numbers.
		private readonly NumberFormatInfo numberFormatProvider = null;

		// The number styles to use when parsing numeric values.
		private readonly NumberStyles numberStyles;

		// A DateTimeFormatInfo object for formatting/parsing DateTime values.
		private readonly DateTimeFormatInfo dateTimeFormatProvider;

		// The DateTime styles to use when parsing DateTime values.
		private readonly DateTimeStyles dateTimeStyles;

		// An array of date/time string formats that can be used during parsing.
		private readonly string[] dateTimeStringFormats;

		// A delegate for selecting additional values for enums.
		private readonly DelegateInvoker<Attribute, string> enumAdditionalValuesSelectors;

		/// <summary>
		/// Initializes a new instance of the <see cref="StringConverter"/> class with optional formatting and parsing configurations.
		/// </summary>
		/// <param name="defaultFormatProvider">The default culture info for formatting.</param>
		/// <param name="numberFormatProvider">The number format provider.</param>
		/// <param name="numberStyles">The number styles for parsing numbers.</param>
		/// <param name="dateTimeFormatProvider">The DateTime format provider.</param>
		/// <param name="dateTimeStyles">The DateTime styles for parsing DateTime values.</param>
		/// <param name="dateTimeStringFormats">The string formats for parsing DateTime values.</param>
		/// <param name="enumAdditionalValuesSelectors">The delegate selector for enum additional values.</param>
		public StringConverter(
			CultureInfo defaultFormatProvider = null,
			NumberFormatInfo numberFormatProvider = null,
			NumberStyles numberStyles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
			DateTimeFormatInfo dateTimeFormatProvider = null,
			DateTimeStyles dateTimeStyles = DateTimeStyles.AllowWhiteSpaces,
			string[] dateTimeStringFormats = null,
			DelegateInvoker<Attribute, string> enumAdditionalValuesSelectors = null
		)
		{
			// Set the culture info or default to the current culture.
			this.defaultFormatProvider = defaultFormatProvider ?? CultureInfo.CurrentCulture;

			// Set the number format provider, number styles, DateTime format provider, and styles.
			this.numberFormatProvider = numberFormatProvider;
			this.numberStyles = numberStyles;
			this.dateTimeFormatProvider = dateTimeFormatProvider;
			this.dateTimeStyles = dateTimeStyles;
			this.dateTimeStringFormats = dateTimeStringFormats;
			this.enumAdditionalValuesSelectors = enumAdditionalValuesSelectors;

			// Pre-create converters for common types like DateTime, TimeSpan, etc.
			foreach (var type in Types.Number.Union(new[] { Types.DateTime, Types.TimeSpan, Types.DateTimeOffset, Types.Guid }))
			{
				CreateConverter(type);
			}

			// Add a specific converter for XmlDocument.
			converters.Add(typeof(XmlDocument), (string value) =>
			{
				var xml = new XmlDocument();
				xml.LoadXml(value);
			});
		}

		/// <summary>
		/// Adds a custom converter for a specific type.
		/// </summary>
		/// <typeparam name="T">The type for which the converter is added.</typeparam>
		/// <param name="converter">A function that converts a string to the specified type.</param>
		public void Add<T>(Func<string, T> converter)
		{
			var targetType = typeof(T);
			converters.Add(targetType, converter);
		}

		/// <summary>
		/// Determines if the converter can convert from the specified source type to the target type.
		/// </summary>
		/// <param name="from">The source type.</param>
		/// <param name="to">The target type.</param>
		/// <returns>True if conversion is possible; otherwise, false.</returns>
		public bool CanConvert(Type from, Type to)
		{
			if (from != Types.String) return false; 			if (converters.ContainsKey(to)) return true; 			return CreateConverter(to) != null;
		}

		/// <summary>
		/// Determines if the converter can convert from the generic source type to the target type.
		/// </summary>
		/// <typeparam name="TFrom">The source type.</typeparam>
		/// <typeparam name="TTo">The target type.</typeparam>
		/// <returns>True if conversion is possible; otherwise, false.</returns>
		public bool CanConvert<TFrom, TTo>() => CanConvert(typeof(TFrom), typeof(TTo));

		/// <summary>
		/// Converts the specified value to the target type.
		/// </summary>
		/// <typeparam name="TFrom">The source type.</typeparam>
		/// <typeparam name="TTo">The target type.</typeparam>
		/// <param name="value">The value to convert.</param>
		/// <returns>The converted value.</returns>
		/// <exception cref="InvalidOperationException">Thrown when conversion is not possible.</exception>
		public TTo Convert<TFrom, TTo>(TFrom value)
		{
			if (!TryConvert(value, typeof(TFrom), typeof(TTo), out var result))
				throw new InvalidOperationException($"Conversion from \"{value}\" to {typeof(TTo).FullName} is not possible.");
			return (TTo)result;
		}

		/// <summary>
		/// Converts the specified value to the target type.
		/// </summary>
		/// <param name="value">The value to convert.</param>
		/// <param name="to">The target type.</param>
		/// <returns>The converted value.</returns>
		/// <exception cref="InvalidOperationException">Thrown when conversion is not possible.</exception>
		public object Convert(object value, Type to)
		{
			if (!TryConvert(value, value.GetType(), to, out var result))
				throw new InvalidOperationException($"Conversion from \"{value}\" to {to.FullName} is not possible.");
			return result;
		}

		/// <summary>
		/// Tries to convert the specified value to the target type.
		/// </summary>
		/// <param name="value">The value to convert.</param>
		/// <param name="from">The source type.</param>
		/// <param name="to">The target type.</param>
		/// <param name="result">The converted value, or null if conversion failed.</param>
		/// <returns>True if conversion is successful; otherwise, false.</returns>
		public bool TryConvert(object value, Type from, Type to, out object result)
		{
			result = null;
			if (from != Types.String) return false; 			if (!converters.TryGetValue(to, out var converter))
			{
				converter = CreateConverter(to);
				if (converter == null) return false; 			}
			try
			{
				result = converter.DynamicInvoke(value);
			}
			catch
			{
				return false;
			}
			return true;
		}

		/// <summary>
		/// Creates a converter for the specified target type.
		/// </summary>
		/// <param name="target">The target type for which to create a converter.</param>
		/// <returns>A delegate that performs the conversion, or null if no suitable converter is found.</returns>
		private Delegate CreateConverter(Type target)
		{
			var result =
				CreateFromNullable(target)
				?? CreateFromEnum(target)
				?? CreateFromMethod(target)
				?? CreateFromConstructor(target);

			if (result != null) converters.Add(target, result); 			return result;
		}

		/// <summary>
		/// Creates a converter for nullable types.
		/// </summary>
		/// <param name="target">The target nullable type.</param>
		/// <returns>A delegate that converts strings to the underlying type of the nullable, or null if not applicable.</returns>
		private Delegate CreateFromNullable(Type target)
		{
			if (!target.IsGenericType || target.GetGenericTypeDefinition() != typeof(Nullable<>))
				return null;
			if (!converters.TryGetValue(target.GetGenericArguments()[0], out var converter))
				converter = CreateConverter(target.GetGenericArguments()[0]);
			if (converter == null) return null; 			var result = (string value) => string.IsNullOrWhiteSpace(value) ? null : converter.DynamicInvoke(value);
			return result;
		}

		/// <summary>
		/// Creates a converter for enum types.
		/// </summary>
		/// <param name="target">The target enum type.</param>
		/// <returns>A delegate that converts strings to the enum, or null if not applicable.</returns>
		private Delegate CreateFromEnum(Type target)
		{
			if (!target.IsEnum) return null; 
			// Expression tree construction to handle enum conversion.
			var expressions = new List<Expression>();
			var separator = defaultFormatProvider.TextInfo.ListSeparator;
			var underlyingType = Enum.GetUnderlyingType(target);
			var constZero = Expression.Constant(System.Convert.ChangeType(0, underlyingType), underlyingType);

			// Parameter and variable declarations.
			var paramValue = Expression.Parameter(typeof(string), "value");
			var varAllValues = Expression.Variable(typeof(string[]), "allValues");
			var varSingleValue = Expression.Variable(typeof(string), "singleValue");
			var varConvertedValue = Expression.Variable(underlyingType, "convertedValue");
			var varResult = Expression.Variable(underlyingType, "result");
			var varIndex = Expression.Variable(typeof(int), "i");
			var varElementsCount = Expression.Variable(typeof(int), "elementsCount");

			// Create a dictionary to map enum values to their string representations.
			Dictionary<long, List<string>> convertions = target.GetFields(BindingFlags.Public | BindingFlags.Static)
				.ToDictionary(f => System.Convert.ToInt64(f.GetValue(null)), f => new List<string> { f.Name.ToLower() });

			// Initialize the result variable with the default value (zero).
			expressions.Add(Expression.Assign(varResult, constZero));

			// Populate the dictionary with additional values for each enum field, if provided.
			foreach (var field in target.GetFields(BindingFlags.Public | BindingFlags.Static))
			{
				var key = System.Convert.ToInt64(field.GetValue(null));

				if (!convertions.TryGetValue(key, out var values))
				{
					values = new List<string>();
					convertions.Add(key, values);
				}
				values.Add(field.Name);

				if (enumAdditionalValuesSelectors != null)
				{
					foreach (Attribute attribute in field.GetCustomAttributes(true))
					{
						if (enumAdditionalValuesSelectors.TryInvoke(attribute, out var value))
							values.Add(value.ToLower());
					}
				}
			}

			// Create switch cases for the conversion expressions.
			SwitchCase[] cases = convertions.Select(c => Expression.SwitchCase(Expression.Constant(System.Convert.ChangeType(c.Key, underlyingType), underlyingType), c.Value.Select(v => Expression.Constant(v)))).ToArray();

			// Create a switch expression to handle the conversion of individual enum values.
			var switchValue = Expression.Switch(
				varSingleValue,
				Expression.Block(
					underlyingType,
					Expression.Throw(Expression.New(typeof(FormatException).GetConstructor(new[] { typeof(string) }), paramValue)),
					constZero
				),
				cases
			);

			// Create expressions to split the input string and loop through each part for conversion.
			expressions.Add(
				Expression.Assign(
					varAllValues,
					Expression.Call(
						paramValue,
						typeof(string).GetMethod("Split", new[] { typeof(char[]), typeof(StringSplitOptions) }),
						Expression.Constant(new[] { '|', separator[0] }),
						Expression.Constant(StringSplitOptions.RemoveEmptyEntries)
					)
				)
			);

			expressions.Add(Expression.Assign(varIndex, Expression.Constant(0)));
			expressions.Add(Expression.Assign(varElementsCount, Expression.ArrayLength(varAllValues)));
			var lblBreak = Expression.Label();
			expressions.Add(
				Expression.Loop(
					Expression.Block(
						Expression.IfThen(
							Expression.GreaterThanOrEqual(varIndex, varElementsCount),
							Expression.Break(lblBreak)
						),
						Expression.Assign(
							varSingleValue,
							Expression.Call(
								Expression.Call(
									Expression.ArrayIndex(varAllValues, varIndex),
									typeof(string).GetMethod("Trim", new Type[] { })
								),
								typeof(string).GetMethod("ToLower", new Type[] { })
							)
						),
						Expression.Assign(
							varResult,
							Expression.Or(
								varResult,
								Expression.Condition(
									Expression.Call(underlyingType.GetMethod("TryParse", new[] { typeof(string), underlyingType.MakeByRefType() }), varSingleValue, varConvertedValue),
									varConvertedValue,
									switchValue
								)
							)
						),
						Expression.PostIncrementAssign(varIndex)
					),
					lblBreak
				)
			);

			// Convert the result back to the enum type.
			expressions.Add(Expression.Convert(varResult, target));

			// Compile the expression tree into a lambda function and return it as a delegate.
			var result = Expression.Lambda(
				Expression.Block(
					target,
					new[] { varAllValues, varSingleValue, varResult, varIndex, varElementsCount, varConvertedValue },
					expressions
				),
				paramValue
			);
			return result.Compile();
		}

		/// <summary>
		/// Creates a converter from a constructor that accepts a string as a parameter.
		/// </summary>
		/// <param name="target">The target type.</param>
		/// <returns>A delegate that converts strings using the constructor, or null if not applicable.</returns>
		private Delegate CreateFromConstructor(Type target)
		{
			var constructors = target.GetConstructors()
				.Select(c => new
				{
					Constructor = c,
					Parameters = c.GetParameters()
				})
				.Where(c => c.Parameters.SingleOrDefault(p => p.ParameterType == Types.String) != null);

			// Filter constructors based on the types of parameters they accept.
			if (dateTimeStringFormats == null)
				constructors = constructors.Where(c => c.Parameters.All(p => p.ParameterType.In(Types.String, typeof(IFormatProvider), typeof(NumberStyles), typeof(DateTimeStyles))));
			else
			{
				constructors = constructors.Where(c => c.Parameters.All(p => p.ParameterType.In(Types.String, typeof(IFormatProvider), typeof(NumberStyles), typeof(DateTimeStyles)) || p.Name == "formats"));
			}

			var constructor = constructors.OrderByDescending(m => m.Parameters.Length).FirstOrDefault();

			if (constructor == null) return null; 
			var formatProvider = defaultFormatProvider;

			// Choose the appropriate format provider based on the constructor's parameters.
			if (constructor.Parameters.Any(p => p.ParameterType == typeof(NumberStyles)))
				formatProvider = defaultFormatProvider;
			else if (constructor.Parameters.Any(p => p.ParameterType == typeof(DateTimeStyles)))
			{
				formatProvider = defaultFormatProvider;
			}

			var valueParameter = Expression.Parameter(Types.String, "value");

			// Create expressions for the constructor's parameters.
			var parameters = constructor.Parameters
				.Select(p =>
				{
					if (p.ParameterType == Types.String) return valueParameter;
					if (p.ParameterType == typeof(IFormatProvider)) return Expression.Constant(formatProvider, typeof(IFormatProvider));
					if (p.ParameterType == typeof(NumberStyles)) return Expression.Constant(numberStyles, typeof(NumberStyles));
					if (p.ParameterType == typeof(DateTimeStyles)) return Expression.Constant(dateTimeStyles, typeof(DateTimeStyles));
					if (p.ParameterType == typeof(string[])) return Expression.Constant(dateTimeStringFormats, typeof(string[]));
					return (Expression)null;
				})
				.ToArray();

			// Compile the constructor call into a lambda function and return it as a delegate.
			var result = Expression.Lambda(
				Expression.Block(
					target,
					Expression.New(constructor.Constructor, parameters)
				),
				valueParameter);
			return result.Compile();
		}

		/// <summary>
		/// Creates a converter from a static method that accepts a string as a parameter.
		/// </summary>
		/// <param name="target">The target type.</param>
		/// <returns>A delegate that converts strings using the static method, or null if not applicable.</returns>
		private Delegate CreateFromMethod(Type target)
		{
			var methods = target.GetMethods(BindingFlags.Static | BindingFlags.Public)
				.Where(m => m.ReturnType == target)
				.Select(m => new
				{
					Method = m,
					Parameters = m.GetParameters()
				})
				.Where(m => m.Parameters.Count(p => p.ParameterType == Types.String) == 1);

			// Filter methods based on the types of parameters they accept.
			if (dateTimeStringFormats == null)
				methods = methods.Where(m => m.Parameters.All(p => p.ParameterType.In(Types.String, typeof(IFormatProvider), typeof(NumberStyles), typeof(DateTimeStyles))));
			else
			{
				methods = methods.Where(m => m.Parameters.All(p => p.ParameterType.In(Types.String, typeof(IFormatProvider), typeof(NumberStyles), typeof(DateTimeStyles)) || p.Name == "formats"));
			}
			var method = methods.OrderByDescending(m => m.Parameters.Length).FirstOrDefault();

			if (method == null) return null; 
			var formatProvider = defaultFormatProvider;

			// Choose the appropriate format provider based on the method's parameters.
			if (method.Parameters.Any(p => p.ParameterType == typeof(NumberStyles)))
				formatProvider = defaultFormatProvider;
			else if (method.Parameters.Any(p => p.ParameterType == typeof(DateTimeStyles)))
			{
				formatProvider = defaultFormatProvider;
			}

			var valueParameter = Expression.Parameter(Types.String, "value");

			// Create expressions for the method's parameters.
			var parameters = method.Parameters
				.Select(p =>
				{
					if (p.ParameterType == Types.String) return valueParameter;
					if (p.ParameterType == typeof(IFormatProvider)) return Expression.Constant(formatProvider, typeof(IFormatProvider));
					if (p.ParameterType == typeof(NumberStyles)) return Expression.Constant(numberStyles, typeof(NumberStyles));
					if (p.ParameterType == typeof(DateTimeStyles)) return Expression.Constant(dateTimeStyles, typeof(DateTimeStyles));
					if (p.ParameterType == typeof(string[])) return Expression.Constant(dateTimeStringFormats, typeof(string[]));
					return (Expression)null;
				})
				.ToArray();

			// Compile the method call into a lambda function and return it as a delegate.
			var result = Expression.Lambda(
				Expression.Block(
					target,
					Expression.Call(null, method.Method, parameters)
				),
				valueParameter);
			return result.Compile();
		}
	}
}
