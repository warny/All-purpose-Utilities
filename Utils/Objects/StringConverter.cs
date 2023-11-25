using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml;
using Utils.Mathematics;
using Utils.Reflection;

namespace Utils.Objects;

public class StringConverter : IConverter
{
	private readonly Dictionary<Type, Delegate> converters = new Dictionary<Type, Delegate>();

	private readonly CultureInfo defaultFormatProvider;
	private readonly NumberFormatInfo numberFormatProvider = null;
	private readonly NumberStyles numberStyles;
	private readonly DateTimeFormatInfo dateTimeFormatProvider;
	private readonly DateTimeStyles dateTimeStyles;
	private readonly string[] dateTimeStringFormats;
	private readonly DelegateSelector<Attribute, string> enumAdditionalValuesSelectors;


	public StringConverter(
	CultureInfo defaultFormatProvider = null,
		NumberFormatInfo numberFormatProvider = null,
		NumberStyles numberStyles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | NumberStyles.AllowThousands | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
		DateTimeFormatInfo dateTimeFormatProvider = null,
		DateTimeStyles dateTimeStyles = DateTimeStyles.AllowWhiteSpaces,
		string[] dateTimeStringFormats = null,
		DelegateSelector<Attribute, string> enumAdditionalValuesSelectors = null
		)
	{
		this.defaultFormatProvider = defaultFormatProvider ?? CultureInfo.CurrentCulture;
		this.numberFormatProvider = numberFormatProvider;
		this.numberStyles = numberStyles;
		this.dateTimeFormatProvider = dateTimeFormatProvider;
		this.dateTimeStyles = dateTimeStyles;
		this.dateTimeStringFormats = dateTimeStringFormats;
		this.enumAdditionalValuesSelectors = enumAdditionalValuesSelectors;

		foreach (var type in Types.Number.Union(new[] { Types.DateTime, Types.TimeSpan, Types.DateTimeOffset, Types.Guid }))
		{
			CreateConverter(type);
		}

		converters.Add(typeof(XmlDocument), (string value) =>
		{
			var xml = new XmlDocument();
			xml.LoadXml(value);
		});
	}

	public void Add<T>(Func<string, T> converter)
	{
		var targetType = typeof(T);
		converters.Add(targetType, converter);
	}

	public bool CanConvert(Type from, Type to)
	{
		if (from != Types.String) { return false; }
		if (converters.ContainsKey(to)) { return true; }
		return CreateConverter(to) != null;
	}

	public bool CanConvert<TFrom, TTo>() => CanConvert(typeof(TFrom), typeof(TTo));

	public TTo Convert<TFrom, TTo>(TFrom value)
	{
		if (!TryConvert(value, typeof(TFrom), typeof(TTo), out object result))
		{
			throw new InvalidOperationException($"La conversion de \"{value}\" en {typeof(TTo).FullName} n'est pas possible");
		}
		return (TTo)result;
	}

	public object Convert(object value, Type to)
	{
		if (!TryConvert(value, value.GetType(), to, out object result))
		{
			throw new InvalidOperationException($"La conversion de \"{value}\" en {to.FullName} n'est pas possible");
		}
		return result;
	}

	public bool TryConvert(object value, Type from, Type to, out object result)
	{
		result = null;
		if (from != Types.String) { return false; }
		if (!converters.TryGetValue(to, out var converter))
		{
			converter = CreateConverter(to);
			if (converter == null) { return false; }
		}
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

	private Delegate CreateConverter(Type target)
	{
		var result =
			CreateFromNullable(target)
			?? CreateFromEnum(target)
			?? CreateFromMethod(target)
			?? CreateFromConstructor(target);

		if (result != null) { converters.Add(target, result); }
		return result;
	}

	private Delegate CreateFromNullable(Type target)
	{
		if (!target.IsGenericType || target.GetGenericTypeDefinition() != typeof(Nullable<>))
		{
			return null;
		}
		if (!converters.TryGetValue(target.GetGenericArguments()[0], out var converter))
		{
			converter = CreateConverter(target.GetGenericArguments()[0]);
		}
		if (converter == null) { return null; }
		var result = (string value) => string.IsNullOrWhiteSpace(value) ? null : converter.DynamicInvoke(value);
		return result;
	}

	private Delegate CreateFromEnum(Type target)
	{
		if (!target.IsEnum) { return null; }
		List<Expression> expressions = new List<Expression>();
		var separator = defaultFormatProvider.TextInfo.ListSeparator;

		var underlyingType = Enum.GetUnderlyingType(target);

		var constZero = Expression.Constant(System.Convert.ChangeType(0, underlyingType), underlyingType);

		var paramValue = Expression.Parameter(typeof(string), "value");
		var varAllValues = Expression.Variable(typeof(string[]), "allValues");
		var varSingleValue = Expression.Variable(typeof(string), "singleValue");
		var varConvertedValue = Expression.Variable(underlyingType, "convertedValue");
		var varResult = Expression.Variable(underlyingType, "result");

		var varIndex = Expression.Variable(typeof(int), "i");
		var varElementsCount = Expression.Variable(typeof(int), "elementsCount");


		Dictionary<long, List<string>> convertions = target.GetFields(BindingFlags.Public | BindingFlags.Static)
			.ToDictionary(f => System.Convert.ToInt64(f.GetValue(null)), f => new List<string> { f.Name.ToLower() });

		expressions.Add(Expression.Assign(varResult, constZero));

		foreach (var field in target.GetFields(BindingFlags.Public | BindingFlags.Static))
		{
			long key = System.Convert.ToInt64(field.GetValue(null));

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
					{
						values.Add(value.ToLower());
					}
				}
			}
		}

		SwitchCase[] cases = convertions.Select(c => Expression.SwitchCase(Expression.Constant(System.Convert.ChangeType(c.Key, underlyingType), underlyingType), c.Value.Select(v => Expression.Constant(v)))).ToArray();

		var switchValue = Expression.Switch(
			varSingleValue,
			Expression.Block(
				underlyingType,
				Expression.Throw(Expression.New(typeof(FormatException).GetConstructor(new[] { typeof(string) }), paramValue)),
				constZero
			),
			cases
		);
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
		expressions.Add(Expression.Convert(varResult, target));

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

	private Delegate CreateFromConstructor(Type target)
	{
		var constructors = target.GetConstructors()
			.Select(c => new
			{
				Constructor = c,
				Parameters = c.GetParameters()
			})
			.Where(c => c.Parameters.SingleOrDefault(p => p.ParameterType == Types.String) != null);

		if (dateTimeStringFormats == null)
		{
			constructors = constructors.Where(c => c.Parameters.All(p => p.ParameterType.In(Types.String, typeof(IFormatProvider), typeof(NumberStyles), typeof(DateTimeStyles))));
		}
		else
		{
			constructors = constructors.Where(c => c.Parameters.All(p => p.ParameterType.In(Types.String, typeof(IFormatProvider), typeof(NumberStyles), typeof(DateTimeStyles)) || p.Name == "formats"));
		}

		var constructor = constructors.OrderByDescending(m => m.Parameters.Length).FirstOrDefault();

		if (constructor == null) { return null; }

		var formatProvider = defaultFormatProvider;
		if (constructor.Parameters.Any(p => p.ParameterType == typeof(NumberStyles)))
		{
			formatProvider = defaultFormatProvider;
		}
		else if (constructor.Parameters.Any(p => p.ParameterType == typeof(DateTimeStyles)))
		{
			formatProvider = defaultFormatProvider;
		}

		var valueParameter = Expression.Parameter(Types.String, "value");

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

		var result = Expression.Lambda(
			Expression.Block(
				target,
				Expression.New(constructor.Constructor, parameters)
			),
			valueParameter);
		return result.Compile();
	}

	private Delegate CreateFromMethod(Type target)
	{
		var methods = target.GetMethods(System.Reflection.BindingFlags.Static | BindingFlags.Public)
			.Where(m => m.ReturnType == target)
			.Select(m => new
			{
				Method = m,
				Parameters = m.GetParameters()
			})
			.Where(m => m.Parameters.Count(p => p.ParameterType == Types.String) == 1);

		if (dateTimeStringFormats == null)
		{
			methods = methods.Where(m => m.Parameters.All(p => p.ParameterType.In(Types.String, typeof(IFormatProvider), typeof(NumberStyles), typeof(DateTimeStyles))));
		}
		else
		{
			methods = methods.Where(m => m.Parameters.All(p => p.ParameterType.In(Types.String, typeof(IFormatProvider), typeof(NumberStyles), typeof(DateTimeStyles)) || p.Name == "formats"));
		}
		var method = methods.OrderByDescending(m => m.Parameters.Length).FirstOrDefault();

		if (method == null) { return null; }

		var formatProvider = defaultFormatProvider;
		if (method.Parameters.Any(p => p.ParameterType == typeof(NumberStyles)))
		{
			formatProvider = defaultFormatProvider;
		}
		else if (method.Parameters.Any(p => p.ParameterType == typeof(DateTimeStyles)))
		{
			formatProvider = defaultFormatProvider;
		}

		var valueParameter = Expression.Parameter(Types.String, "value");

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

		var result = Expression.Lambda(
			Expression.Block(
				target,
				Expression.Call(null, method.Method, parameters)
			),
			valueParameter);
		return result.Compile();
	}

}
