using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Utils.Objects
{
	public static class Parsers
	{

		// Vérifie qu'une valeur peut être parsée
		static bool CanParse( Type type )
		{
			if (type.GetGenericTypeDefinition() == typeof(Nullable<>)) {
				return CanParse(type.GetGenericArguments()[0]);
			}

			MethodInfo tryParseMethod = type.GetMethod("TryParse", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(string), type }, null);
			MethodInfo ParseMethod = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(string), type }, null);
			return tryParseMethod != null || ParseMethod != null;
		}

		/// <summary>
		/// Parse la valeur
		/// </summary>
		/// <typeparam name="T">Type de la valeur de sortie</typeparam>
		/// <param name="value">Valeur à parser</param>
		/// <returns>Valeur parsée</returns>
		static public T Parse<T>( string value )
		{
			object returnValue = Parse(value, typeof(T));
			return (T)returnValue;
		}

		/// <summary>
		/// Parse la valeur
		/// </summary>
		/// <typeparam name="T">Type de la valeur de sortie</typeparam>
		/// <param name="value">Valeur à parser</param>
		/// <param name="formatsProviders">Formats de valeurs à tester</param>
		/// <returns>Valeur parsée</returns>
		static public T Parse<T>( string value, IEnumerable<IFormatProvider> formatsProviders )
		{
			object returnValue = Parse(value, typeof(T), formatsProviders);
			return (T)returnValue;
		}

		/// <summary>
		/// Parse la valeur
		/// </summary>
		/// <typeparam name="T">Type de la valeur de sortie</typeparam>
		/// <param name="value">Valeur à parser</param>
		/// <param name="formatsProviders">Formats de valeurs à tester</param>
		/// <returns>Valeur parsée</returns>
		static public T Parse<T>( string value, params IFormatProvider[] formatsProviders )
		{
			object returnValue = Parse(value, typeof(T), formatsProviders);
			return (T)returnValue;
		}

		/// <summary>
		/// Parse la valeur
		/// </summary>
		/// <typeparam name="T">Type de la valeur de sortie</typeparam>
		/// <param name="value">Valeur à parser</param>
		/// <returns>Valeur parsée</returns>
		static public T ParseOrDefault<T>( string value, T defaultValue = default(T) )
		{
			object returnValue = Parse(value, typeof(T));
			return (T)(returnValue ?? defaultValue);
		}

		/// <summary>
		/// convertie une valeur chaîne dans le type spécifié
		/// </summary>
		/// <param name="value"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		public static object Parse( string value, Type type )
		{
			return Parse(value, type, CultureInfo.CurrentCulture);
		}

		/// <summary>
		/// convertie une valeur chaîne dans le type spécifié
		/// </summary>
		/// <param name="value"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		public static object Parse( string value, Type type, params IFormatProvider[] formatsProviders )
		{
			return Parse(value, type, (IEnumerable<IFormatProvider>)formatsProviders);
		}

		/// <summary>
		/// convertie une valeur chaîne dans le type spécifié
		/// </summary>
		/// <param name="value"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		public static object Parse( string value, Type type, IEnumerable<IFormatProvider> formatsProviders )
		{
			if (type.IsEnum) {
				return Enum.Parse(type, value, true);
			}

			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) {
				return Parse(value, type.GetGenericArguments()[0], formatsProviders);
			}

			MethodInfo tryParseMethod = type.GetMethod("TryParse", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(string), typeof(IFormatProvider), type.MakeByRefType() }, null);

			MethodInfo ParseMethod = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(string), typeof(IFormatProvider) }, null);

			foreach (var formatProvider in formatsProviders) {
				// on vérifie s'il existe une méthode de parsing respectueuse dans la classe
				if (tryParseMethod != null) {
					var args = new object[] { value, formatProvider, null };
					if ((bool)tryParseMethod.Invoke(null, args)) {
						return args[2];
					}
				}
				// sinon, on vérifie qu'il existe une méthode de parsing directe dans la classe
				if (ParseMethod != null) {
					try {
						return ParseMethod.Invoke(null, new object[] { value, formatProvider });
					} catch {
						// si le parse echoue, la conversion sera effectué par les convertisseurs suivants
					}
				}
			}
			// enfin, on essaye de convertir la valeur brutalement
			try {
				return Convert.ChangeType(value, type);
			} catch {
				if (string.IsNullOrEmpty(value)) {
					return null;
				}
				throw;
			}

		}

	}
}
