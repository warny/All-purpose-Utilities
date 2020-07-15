using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Utils.Objects
{
	public static class Parsers
	{
		private static readonly Type typeOfString = typeof(string);
		private static readonly Type typeOfIFormatProvider = typeof(IFormatProvider);

		private class ParseMethods
		{
			public ParseMethods(Type type)
			{
				if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
				{
					type = type.GetGenericArguments()[0];
				}

				this.TryParseMethod = type.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeOfString, typeOfIFormatProvider, type.MakeByRefType() }, null);
				this.ParseMethod = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeOfString, typeOfIFormatProvider }, null);
				this.Constructor = type.GetConstructor(new Type[] { typeOfString });
				CanParse = TryParseMethod != null || ParseMethod != null || Constructor != null;
			}

			public bool CanParse { get; }
			public MethodInfo TryParseMethod { get; }
			public MethodInfo ParseMethod { get; }
			public ConstructorInfo Constructor { get; }
		}

		private static Dictionary<Type, ParseMethods> parsers = new Dictionary<Type, ParseMethods>();
		/// <summary>
		/// Récupère les méthodes de parsing de <paramref name="type"/>
		/// </summary>
		/// <param name="type">Type dont on veut récupérer les fonctions</param>
		/// <returns></returns>
		private static ParseMethods GetParseMethods(Type type)
		{
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

			if (methods.TryParseMethod != null || methods.ParseMethod != null)
			{
				foreach (var formatProvider in formatsProviders)
				{
					// on vérifie s'il existe une méthode de parsing respectueuse dans la classe
					if (methods.TryParseMethod != null)
					{
						var args = new object[] { value, formatProvider, null };
						if ((bool)methods.TryParseMethod.Invoke(null, args))
						{
							return args[2];
						}
					}
					// sinon, on vérifie qu'il existe une méthode de parsing directe dans la classe
					if (methods.ParseMethod != null)
					{
						try
						{
							return methods.ParseMethod.Invoke(null, new object[] { value, formatProvider });
						}
						catch
						{
							// si le parse echoue, la conversion sera effectué par les convertisseurs suivants
						}
					}
				}
			}

			//Si tous les parsers ont échoué, on tente de construite l'objet à partir de la chaîne
			if (methods.Constructor != null)
			{
				return methods.Constructor.Invoke(new[] { value });
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
