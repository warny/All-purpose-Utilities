using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utils.Collections
{
	public static class CollectionUtils
	{
		/// <summary>
		/// Returns an element from a dictionary with the specific key or <paramref name="defaultValue"/> if if doesn't exists
		/// </summary>
		/// <typeparam name="TKey">Key type</typeparam>
		/// <typeparam name="TValue">Value type</typeparam>
		/// <param name="dictionary">Dictionary</param>
		/// <param name="key">Key whose value to get</param>
		/// <param name="defaultValue">Default value</param>
		/// <returns>Value of specific key or default value</returns>
		/// <exception cref="ArgumentNullException">Raise if dictionary is null</exception>
		public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
		{
			_ = dictionary ?? throw new NullReferenceException();
			if (dictionary.TryGetValue(key, out TValue result)) return result;
			return defaultValue;
		}

		/// <summary>
		/// Returns true if collection is null or has no elements
		/// </summary>
		/// <typeparam name="T"></typeparam>
		public static bool IsNullOrEmptyCollection<T>(this IEnumerable<T> coll) => coll is null || !coll.Any();

		/// <summary>
		/// Returns an enumerator with each element is the combination of the combined enumeration of <paramref name="leftEnumeration"/> and <paramref name="rightEnumeration"/> enumerations
		/// </summary>
		/// <typeparam name="T1">Type for left elements</typeparam>
		/// <typeparam name="T2">Type for right elements</typeparam>
		/// <param name="leftEnumeration">Enumeration for left elements</param>
		/// <param name="rightEnumeration">Enumeration for right elements</param>
		/// <param name="defaultLeft">Default value after the left enumeration has end</param>
		/// <param name="defaultRight">Default value after the right enumeration has end</param>
		/// <param name="continueAfterShortestListEnds">Continue to enumerate the longuest enumeration when the shortest has end</param>
		/// <returns></returns>
		public static IEnumerable<(T1 Left, T2 Right)> Zip<T1, T2>(IEnumerable<T1> leftEnumeration, IEnumerable<T2> rightEnumeration, T1 defaultLeft = default(T1), T2 defaultRight = default(T2), bool continueAfterShortestListEnds = true)
		{
			var leftEnumerator = leftEnumeration.GetEnumerator();
			var rightEnumerator = rightEnumeration.GetEnumerator();

			bool continueLeft;

			while ((continueLeft = leftEnumerator.MoveNext()) & rightEnumerator.MoveNext())
			{
				yield return (leftEnumerator.Current, rightEnumerator.Current);
			}
			if (!continueAfterShortestListEnds) yield break;
			if (continueLeft)
			{
				do
				{
					yield return (leftEnumerator.Current, defaultRight);
				} while (leftEnumerator.MoveNext());
			}
			else 
			{
				do
				{
					yield return (defaultLeft, rightEnumerator.Current);
				} while (rightEnumerator.MoveNext());
			}
		}

		/// <summary>
		/// Returns an enumerator with each element is the combination of the combined enumeration of <paramref name="leftEnumeration"/> and <paramref name="rightEnumeration"/> enumerations
		/// </summary>
		/// <typeparam name="T1">Type for left elements</typeparam>
		/// <typeparam name="T2">Type for right elements</typeparam>
		/// <param name="leftEnumeration">Enumeration for left elements</param>
		/// <param name="rightEnumeration">Enumeration for right elements</param>
		/// <param name="func"></param>
		/// <param name="continueAfterShortestListEnds">Continue to enumerate the longuest enumeration when the shortest has end</param>
		/// <returns></returns>
		public static IEnumerable<TResult> Zip<T1, T2, TResult>(IEnumerable<T1> leftEnumeration, IEnumerable<T2> rightEnumeration, Func<T1, T2, TResult> func, bool continueAfterShortestListEnds = true)
			=> Zip(leftEnumeration, rightEnumeration, default(T1), default(T2), continueAfterShortestListEnds).Select(e => func(e.Left, e.Right));

		/// <summary>
		/// Pack the list, grouping subsequent equals items and their count
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="c"></param>
		/// <returns></returns>
		public static IEnumerable<(T item, int repetition)> Pack<T>(IEnumerable<T> c)
		{
			var e = c.GetEnumerator();
			if (!e.MoveNext()) yield break;
			var lastValue = e.Current;

			var repetition = 0;
			while (e.MoveNext())
			{
				repetition++;
				if (!e.Current.Equals(lastValue))
				{
					yield return (lastValue, repetition);
					repetition = 0;
					lastValue = e.Current;
				}
			}
			yield return (lastValue, repetition);
		}

		/// <summary>
		/// Unpack an enumeration previously packed
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="c"></param>
		/// <returns></returns>
		public static IEnumerable<T> Unpack<T>(IEnumerable<(T item, int repetition)> c) 
		{
			foreach (var item in c)
			{
				for (int i = 0; i < item.repetition; i++)
				{
					yield return item.item;
				}
			}
		}
	}
}
