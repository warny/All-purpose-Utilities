using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Utils.Objects
{
	public static class Parsers
	{
		private delegate bool TryParseDelegate(string s, IFormatProvider formatProvider, out object value);
		private delegate object ParseDelegate(string s, IFormatProvider formatProvider);
		private delegate object ConstructorDelegate(string s);

		private static readonly Type typeOfString = typeof(string);
		private static readonly Type typeOfIFormatProvider = typeof(IFormatProvider);

		private class ParseMethods
		{

			public ParseMethods(Type type)
			{
				type.ArgMustNotBeNull();
				if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
				{
					type = type.GetGenericArguments()[0];
				}

				this.TryParse = BuildTryParse(type);
				this.Parse = BuildParse(type);
				this.Constructor = BuildConstructor(type);

				CanParse = TryParse is not null || Parse is not null || Constructor is not null;
			}
			public ParseMethods(TryParseDelegate tryParse, ParseDelegate parse, ConstructorDelegate constructor)
			{
				this.TryParse = tryParse;
				this.Parse = parse;
				this.Constructor = constructor;
				CanParse = TryParse is not null || Parse is not null || Constructor is not null;
			}

			private ConstructorDelegate BuildConstructor(Type type)
			{
				var constructorInfo = type.GetConstructor(new[] { typeOfString });
				if (constructorInfo is not null) {

					var sParameter = Expression.Parameter(typeOfString, "s");
					var returnLabel = Expression.Label(typeof(object));
					return Expression.Lambda<ConstructorDelegate>(
						Expression.Block(
							typeof(object),
							Expression.Return(
								returnLabel,
								Expression.Convert(Expression.New(constructorInfo, sParameter), typeof(object)),
								typeof(object)
							),
							Expression.Label(returnLabel, Expression.Default(typeof(object)))
							),
						sParameter
						)
						.Compile();
				}
				return null;
			}

			private TryParseDelegate BuildTryParse(Type type)
			{
				type.ArgMustNotBeNull();
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

			private ParseDelegate BuildParse(Type type)
			{
				type.ArgMustNotBeNull();
				MethodInfo parseMethod = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeOfString, typeOfIFormatProvider }, null);
				if (parseMethod is not null)
				{

					var strParameter = Expression.Parameter(typeOfString, "s");
					var formatProviderParameter = Expression.Parameter(typeof(IFormatProvider), "formatProvider");
					var returnLabel = Expression.Label(typeof(object));

					return Expression.Lambda<ParseDelegate>(
						Expression.Block(
							typeof(object),
							Expression.Return(
								returnLabel,
								Expression.Convert(
									Expression.Call(parseMethod, strParameter, formatProviderParameter), typeof(object)
								),
								typeof(object)),
							Expression.Label(returnLabel, Expression.Default(typeof(object)))
							),
						strParameter, formatProviderParameter
						).Compile();
				}

				parseMethod = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeOfString }, null);
				if (parseMethod is not null)
				{

					var strParameter = Expression.Parameter(typeOfString, "s");
					var formatProviderParameter = Expression.Parameter(typeof(IFormatProvider), "formatProvider");
					var returnLabel = Expression.Label(typeof(object));

					return Expression.Lambda<ParseDelegate>(
						Expression.Block(
							typeof(object),
							Expression.Return(
								returnLabel,
								Expression.Convert(
									Expression.Call(parseMethod, strParameter), typeof(object)
								),
								typeof(object)),
							Expression.Label(returnLabel, Expression.Default(typeof(object)))
							),
						strParameter, formatProviderParameter
						).Compile();
				}
				return null;
			}

			public bool CanParse { get; }

			public TryParseDelegate TryParse { get; }
			public ParseDelegate Parse { get; }
			public ConstructorDelegate Constructor { get; }
		}

		private static Dictionary<Type, ParseMethods> parsers = new Dictionary<Type, ParseMethods>();

		static Parsers()
		{
			parsers.Add(typeof(DateTime), new ParseMethods(
				new TryParseDelegate((string s, IFormatProvider formatProvider, out object value) => {
					var result = DateTime.TryParse(s, formatProvider, DateTimeStyles.None, out DateTime datetime);
					value = datetime;
					return result;
				}),
				new ParseDelegate((string s, IFormatProvider formatProvider) => DateTime.Parse(s, formatProvider)),
				null
			));
		}


		/// <summary>
		/// Récupère les méthodes de parsing de <paramref name="type"/>
		/// </summary>
		/// <param name="type">Type dont on veut récupérer les fonctions</param>
		/// <returns></returns>
		private static ParseMethods GetParseMethods(Type type)
		{
			type.ArgMustNotBeNull();
			if (!parsers.TryGetValue(type, out var parseMethods))
			{
				parseMethods = new ParseMethods(type);
				parsers.Add(type, parseMethods);
			}

			return parseMethods;
		}

		/// <summary>
		/// Vérifie qu'un type peut être parsé
		/// </summary>
		/// <param name="type">Type à vérifier</param>
		/// <returns><see cref="true"/> si le type peut être parsé sinon <see cref="false"/></returns>
		public static bool CanParse<T>() => CanParse(typeof(T));

		/// <summary>
		/// Vérifie qu'un type peut être parsé
		/// </summary>
		/// <param name="type">Type à vérifier</param>
		/// <returns><see cref="true"/> si le type peut être parsé sinon <see cref="false"/></returns>
		public static bool CanParse(Type type)
		{
			type.ArgMustNotBeNull();
			if (type.IsEnum) return true;
			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
			{
				return CanParse(type.GetGenericArguments()[0]);
			}

			ParseMethods parseMethods = GetParseMethods(type);
			return parseMethods.CanParse;
		}

		/// <summary>
		/// Parse la valeur
		/// </summary>
		/// <typeparam name="T">Type de la valeur de sortie</typeparam>
		/// <param name="value">Valeur à parser</param>
		/// <returns>Valeur parsée</returns>
		static public T Parse<T>(string value) => (T)Parse(value, typeof(T));

		/// <summary>
		/// Parse la valeur
		/// </summary>
		/// <typeparam name="T">Type de la valeur de sortie</typeparam>
		/// <param name="value">Valeur à parser</param>
		/// <param name="formatsProviders">Formats de valeurs à tester</param>
		/// <returns>Valeur parsée</returns>
		static public T Parse<T>(string value, IEnumerable<IFormatProvider> formatsProviders) => (T)Parse(value, typeof(T), formatsProviders);

		/// <summary>
		/// Parse la valeur
		/// </summary>
		/// <typeparam name="T">Type de la valeur de sortie</typeparam>
		/// <param name="value">Valeur à parser</param>
		/// <param name="formatsProviders">Formats de valeurs à tester</param>
		/// <returns>Valeur parsée</returns>
		static public T Parse<T>(string value, params IFormatProvider[] formatsProviders) => (T)Parse(value, typeof(T), formatsProviders);

		/// <summary>
		/// convertie une valeur chaîne dans le type spécifié
		/// </summary>
		/// <param name="value"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		public static object Parse(string value, Type type) => Parse(value, type, CultureInfo.CurrentCulture);

		/// <summary>
		/// convertie une valeur chaîne dans le type spécifié
		/// </summary>
		/// <param name="value"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		public static object Parse(string value, Type type, params IFormatProvider[] formatsProviders) => Parse(value, type, (IEnumerable<IFormatProvider>)formatsProviders);

		/// <summary>
		/// convertie une valeur chaîne dans le type spécifié
		/// </summary>
		/// <param name="value"></param>
		/// <param name="type"></param>
		/// <returns></returns>
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

			if (methods.TryParse is not null || methods.Parse is not null)
			{
				foreach (var formatProvider in formatsProviders)
				{
					object result = null;
					// on vérifie s'il existe une méthode de parsing respectueuse dans la classe
					if (methods?.TryParse(value, formatProvider, out result) ?? false)
					{
						return result;
					}
					// sinon, on vérifie qu'il existe une méthode de parsing directe dans la classe
					if (methods.Parse is not null)
					{
						try
						{
							return methods.Parse(value, formatProvider);
						}
						catch
						{
							// si le parse echoue, la conversion sera effectué par les convertisseurs suivants
						}
					}
				}
			}

			//Si tous les parsers ont échoué, on tente de construite l'objet à partir de la chaîne
			if (methods.Constructor is not null)
			{
				return methods.Constructor(value);
			}

			// enfin, on essaye de convertir la valeur brutalement
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
		/// Parse la valeur
		/// </summary>
		/// <typeparam name="T">Type de la valeur de sortie</typeparam>
		/// <param name="value">Valeur à parser</param>
		/// <returns>Valeur parsée</returns>
		static public T ParseOrDefault<T>(string value, T defaultValue = default)
		{
			object returnValue = Parse(value, typeof(T));
			return (T)(returnValue ?? defaultValue);
		}

		/// <summary>
		/// Parse la valeur
		/// </summary>
		/// <typeparam name="T">Type de la valeur de sortie</typeparam>
		/// <param name="value">Valeur à parser</param>
		/// <returns>Valeur parsée</returns>
		static public T ParseOrDefault<T>(string value, IFormatProvider formatProvider = null, T defaultValue = default)
			=> ParseOrDefault(value, new[] { formatProvider }, defaultValue);
		/// <summary>
		/// Parse la valeur
		/// </summary>
		/// <typeparam name="T">Type de la valeur de sortie</typeparam>
		/// <param name="value">Valeur à parser</param>
		/// <returns>Valeur parsée</returns>
		static public T ParseOrDefault<T>(string value, IFormatProvider[] formatsProviders = null, T defaultValue = default)
		{
			object returnValue = Parse(value, typeof(T), formatsProviders);
			return (T)(returnValue ?? defaultValue);
		}

	}
}
