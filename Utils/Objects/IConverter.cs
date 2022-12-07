using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Utils.Objects
{
	public interface IConverter
	{

		/// <summary>
		/// Indique si cette instance peut convertir du type <paramref name="from"/> au type <paramref name="to"/>
		/// </summary>
		/// <param name="from">Type de départ</param>
		/// <param name="to">Type d'arrivée</param>
		/// <returns>
		///   <see cref="true"/> si l'instance peut convertir du type <paramref name="from"/> au type <paramref name="to"/> sinon <see cref="false"/>
		/// </returns>
		public bool CanConvert(Type from, Type to);

		/// <summary>
		/// Indique si cette instance peut convertir du type <typeparamref name="TFrom"/> au type <typeparamref name="TTo"/>
		/// </summary>
		/// <typeparam name="TFrom">Type de départ</typeparam>
		/// <typeparam name="TTo">Type d'arrivée</typeparam>
		/// <returns>
		///    <see cref="true"/> si cette instance peut convertir du type <typeparamref name="TFrom"/> au type <typeparamref name="TTo"/> sinon <see cref="false"/>
		/// </returns>
		public bool CanConvert<TFrom, TTo>();

		/// <summary>
		/// Converti <paramref name="value"/> du type <typeparamref name="TFrom"/> au type <typeparamref name="TTo"/>
		/// </summary>
		/// <typeparam name="TFrom">Type de départ</typeparam>
		/// <typeparam name="TTo">Type d'arrivée</typeparam>
		/// <param name="value"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException">$"La conversion de {value.GetType()} en {to} n'est pas possible</exception>
		public TTo Convert<TFrom, TTo>(TFrom value);

		/// <summary>
		/// Converti <paramref name="value"/> vers le type cible <paramref name="to"/>
		/// </summary>
		/// <param name="value">Valeur à convertir</param>
		/// <param name="to">Type cible</param>
		/// <returns></returns>
		/// <exception cref="System.InvalidOperationException">La conversion de {value.GetType()} en {to} n'est pas possible</exception>
		public object Convert(object value, Type to);

		/// <summary>
		/// Essaye de convertir <paramref name="value"/> du type <paramref name="from"/> au type <paramref name="to"/>
		/// </summary>
		/// <param name="value">Valeur à convertir</param>
		/// <param name="from">Type de départ</param>
		/// <param name="to">Type d'arrivée</param>
		/// <param name="result">Résultat de la conversion</param>
		/// <returns></returns>
		public bool TryConvert(object value, Type from, Type to, out object result);
	}

	public class ConverterAgreggator : IConverter
	{
		private List<IConverter> converters = new List<IConverter>();

		public ConverterAgreggator() { }

		public ConverterAgreggator(IEnumerable<IConverter> converters)
		{
			this.converters.AddRange(converters.Distinct());
		}

		public void Add(IConverter converter)
		{
			if (!converters.Contains(converter))
			{
				converters.Add(converter);
			}
		}

		public bool CanConvert(Type from, Type to) => converters.Any(c => c.CanConvert(from, to));

		public bool CanConvert<TFrom, TTo>() => CanConvert(typeof(TFrom), typeof(TTo));

		public TTo Convert<TFrom, TTo>(TFrom value)
		{
			if (!TryConvert(value, typeof(TFrom), typeof(TTo), out object result))
			{
				throw new InvalidOperationException($"La conversion de {value.GetType().FullName} en {typeof(TTo).FullName} n'est pas possible");
			}
			return (TTo)result;
		}

		public object Convert(object value, Type to)
		{
			if (!TryConvert(value, value.GetType(), to, out object result))
			{
				throw new InvalidOperationException($"La conversion de {value.GetType().FullName} en {to.FullName} n'est pas possible");
			}
			return result;
		}

		public bool TryConvert(object value, Type from, Type to, out object result)
		{
			var converter = converters.FirstOrDefault(c => c.CanConvert(from, to));
			if (converter != null && converter.TryConvert(value, from, to, out result))
			{
				return true;
			}
			result = null;
			return false;
		}

	}

	/// <summary>
	/// Convertisseur de valeurs
	/// </summary>
	/// <seealso cref="Utils.Objects.IConverter" />
	public class SimpleConverter : IConverter, IEnumerable<Delegate>
	{
		private readonly Dictionary<Type, Dictionary<Type, Delegate>> converters = new Dictionary<Type, Dictionary<Type, Delegate>>();

		/// <summary>
		/// Ajoute un convertisseur spécifique
		/// </summary>
		/// <param name="d">Function qui converti une valeur d'un type en un autre</param>
		/// <exception cref="System.ArgumentException">La fonction ne peut avoir qu'un </exception>
		public void Add(Delegate d)
		{
			var method = d.Method;

			var parameters = method.GetParameters();
			if (parameters.Length != 1) { throw new ArgumentException($"La fonction {method} ne peux pas convertir une valeur, elle ne doit avoir qu'un seul argument"); }
			var sourceType = parameters[0].ParameterType;
			var targetType = method.ReturnType;

			if (!converters.TryGetValue(sourceType, out var convertersList))
			{
				convertersList = new Dictionary<Type, Delegate>();
				converters.Add(sourceType, convertersList);
			}
			convertersList.Add(targetType, d);

		}

		/// <summary>
		/// Indique si cette instance peut convertir du type <paramref name="from"/> au type <paramref name="to"/>
		/// </summary>
		/// <param name="from">Type de départ</param>
		/// <param name="to">Type d'arrivée</param>
		/// <returns>
		///   <see cref="true"/> si l'instance peut convertir du type <paramref name="from"/> au type <paramref name="to"/> sinon <see cref="false"/>
		/// </returns>
		public bool CanConvert(Type from, Type to)
		{
			for (Type type = from; type != typeof(object); type = type.BaseType)
			{
				if (converters.TryGetValue(type, out var targets))
				{
					return targets.ContainsKey(to);
				}
			}
			return false;
		}

		/// <summary>
		/// Indique si cette instance peut convertir du type <typeparamref name="TFrom"/> au type <typeparamref name="TTo"/>
		/// </summary>
		/// <typeparam name="TFrom">Type de départ</typeparam>
		/// <typeparam name="TTo">Type d'arrivée</typeparam>
		/// <returns>
		///    <see cref="true"/> si cette instance peut convertir du type <typeparamref name="TFrom"/> au type <typeparamref name="TTo"/> sinon <see cref="false"/>
		/// </returns>
		public bool CanConvert<TFrom, TTo>()
			=> CanConvert(typeof(TFrom), typeof(TTo));

		/// <summary>
		/// Converti <paramref name="value"/> du type <typeparamref name="TFrom"/> au type <typeparamref name="TTo"/>
		/// </summary>
		/// <typeparam name="TFrom">Type de départ</typeparam>
		/// <typeparam name="TTo">Type d'arrivée</typeparam>
		/// <param name="value"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException">$"La conversion de {value.GetType()} en {to} n'est pas possible</exception>
		public TTo Convert<TFrom, TTo>(TFrom value)
		{
			if (TryConvert(value, typeof(TFrom), typeof(TTo), out var result)) {
				return (TTo)result;
			}
			throw new InvalidOperationException($"La conversion de {value.GetType().FullName} en {typeof(TTo).FullName} n'est pas possible");
		}

		/// <summary>
		/// Converti <paramref name="value"/> vers le type cible <paramref name="to"/>
		/// </summary>
		/// <param name="value">Valeur à convertir</param>
		/// <param name="to">Type cible</param>
		/// <returns></returns>
		/// <exception cref="System.InvalidOperationException">La conversion de {value.GetType()} en {to} n'est pas possible</exception>
		public object Convert(object value, Type to)
		{
			if (TryConvert(value, value.GetType(), to, out var result))
			{
				return result;
			}
			throw new InvalidOperationException($"La conversion de {value.GetType()} en {to} n'est pas possible");
		}


		/// <summary>
		/// Essaye de convertir <paramref name="value"/> du type <paramref name="from"/> au type <paramref name="to"/>
		/// </summary>
		/// <param name="value">Valeur à convertir</param>
		/// <param name="from">Type de départ</param>
		/// <param name="to">Type d'arrivée</param>
		/// <param name="result">Résultat de la conversion</param>
		/// <returns></returns>
		public bool TryConvert(object value, Type from, Type to, out object result)
		{
			for (Type type = from; type != typeof(object); type = type.BaseType)
			{
				if (converters.TryGetValue(type, out var targets) && targets.TryGetValue(to, out var converter))
				{
					result = converter.DynamicInvoke(value);
					return true;
				}
			}

			result = null;
			return false;
		}

		/// <summary>
		/// Liste tous les convertisseurs
		/// </summary>
		/// <returns>
		/// Liste de tous les convertisseurs
		/// </returns>
		IEnumerator IEnumerable.GetEnumerator() => converters.SelectMany(c => c.Value.Select(v => v.Value)).GetEnumerator();
		public IEnumerator<Delegate> GetEnumerator() => converters.SelectMany(c => c.Value.Select(v => v.Value)).GetEnumerator();
	}

	/// <summary>
	/// classe de base pour les convertisseurs. 
	/// </summary>
	/// <example>
	/// <code>
	/// public class NewConverter : ConverterBase {
	///		// in this case, the class will be able to convert string to int
	///		int ToInt32 (string value) => int.Parse(value);
	/// }
	/// </code>
	/// </example>
	/// <seealso cref="Utils.Objects.SimpleConverter" />
	public abstract class ConverterBase : SimpleConverter
	{
		protected ConverterBase()
		{
			var genericType = typeof(Func<,>);
			for (var t = GetType(); t != typeof(ConverterBase); t = t.BaseType)
			{
				foreach (var method in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static))
				{
					var parameters = method.GetParameters();
					if (parameters.Length == 1)
					{
						var specificType = genericType.MakeGenericType(parameters[0].ParameterType, method.ReturnType);
						Delegate d 
							= method.IsStatic
							? method.CreateDelegate(specificType)
							: method.CreateDelegate(specificType, this);
						Add(d);
					}
				}
			}
		}

	}
}
